using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

public sealed partial class SpectatorFreecamController
{
    private Camera? _ownedCamera;
    private float _vanillaDistance = 1.3f;
    /// <summary>Vanilla orbit distance, reset on death and independent of enhanced entry framing.</summary>
    public bool TryGetVanillaDistance(out float distance)
    {
        distance = _vanillaDistance;
        return _settings.EnableEnhancedSpectator && _wasSpectating && !_state.UserEnabled;
    }
    private float _savedFov;
    private bool _firstPersonFovApplied;
    private float _lastConfiguredFreeDistance = float.NaN;
    private float _savedNearClip;
    private string? _savedCameraName;
    private float _collisionDistance = -1f;
    private float _orbitAngle;
    private double _cinematicPhase;
    private Vector3 _cinematicVelocity;
    private int _lastModeFrame = -1;
    private bool _followingReferenceCaptured;
    private Vector3 _followingLocalOffset;
    private Vector3 _cinematicPosition;
    private Quaternion _cinematicRotation;
    private bool _hasCinematicPose;
    private bool _snapCinematicPose;
    private Vector3 _cinematicAnchor;
    private int _cinematicStyle;
    private float _cinematicBasisYaw, _cinematicGoalYaw;
    private float _cinematicBlendElapsed = CinematicStyles.BlendSeconds;
    private CinematicShotPath.Shot _cinematicShot, _cinematicBlendFrom;

    internal bool AllowsModelShortcut(KeyCode key) => !CameraInputBlocked
        && !CinematicStyles.ReservesKey(_state.UserEnabled ? _state.Mode : (SpectatorCameraMode?)null, key);

    /// <summary>The local controller, used by the retained options view and narrow patch guard.</summary>
    public static SpectatorFreecamController? Current { get; private set; }

    /// <summary>Whether enhanced rendering has acquired the spectator camera this session.</summary>
    public bool OwnsCamera => _ownedCamera != null && _state.IsActive && _state.UserEnabled;

    /// <summary>Selects a local mode without touching the watched player's camera or input.</summary>
    public void SelectMode(SpectatorCameraMode mode)
    {
        if (!TryGetEligibleSnapshot(out _) || (mode == SpectatorCameraMode.ThirdPerson && !_settings.EnableThirdPerson)) return;
        LethalCompanyFirstPersonVisibility.Clear();
        _state.Mode = mode;
        _state.UserEnabled = true;
        _collisionDistance = -1f;
        _followingReferenceCaptured = false;
        _hasCinematicPose = false;
        _lastModeFrame = -1;
        _smoothVelocity = Vector3.zero;
        ModLog.Info($"Spectator camera mode: {mode}.");
        if (mode == SpectatorCameraMode.Cinematic)
            ModLog.Info($"Cinematic styles: build=cinema-r7-20260924; selected={_settings.Camera.CinematicStyle.Value}; count={CinematicStyles.Count}; arrows=Left/Right.");
    }

    /// <summary>Resets the visible view preference and radial distance even when values were already default.</summary>
    public void ResetOptionsView()
    {
        RestoreCamera();
        _state.Mode = SpectatorCameraMode.Freecam;
        _state.UserEnabled = true;
        _lastConfiguredFreeDistance = float.NaN;
        ApplyFreecamDistance(_settings.Camera.FreecamDistance.Value);
        ResetFollowHistory();
    }

    /// <summary>Returns camera ownership to vanilla.</summary>
    public void ReturnToVanilla()
    {
        _state.UserEnabled = false;
        Deactivate(clearAnchor: true);
    }

    /// <summary>Restores borrowed state and releases this controller.</summary>
    public void Dispose()
    {
        ReturnToVanilla();
        if (ReferenceEquals(Current, this)) Current = null;
    }

    private bool CameraInputBlocked => _adapter is IGameSpectatorCameraAdapter cameraAdapter
        ? cameraAdapter.IsCameraInputBlocked() : _adapter.IsLocalQuickMenuOpen();

    private void AcquireCamera(Camera camera)
    {
        if (_ownedCamera == camera) return;
        RestoreCamera();
        _ownedCamera = camera;
        _savedFov = camera.fieldOfView;
        _savedNearClip = camera.nearClipPlane;
        _savedCameraName = camera.name;
        // CustomFOV 1.0.0 rewrites all FOV setters on objects named MainCamera.
        // Only the borrowed spectator object is renamed, and restored on release.

        ModLog.Debug($"Acquired spectator camera: originalName={_savedCameraName}, fov={_savedFov}.");
    }

    private void RestoreCamera()
    {
        LethalCompanyFirstPersonVisibility.Clear();
        if (_ownedCamera != null)
        {
            if (_firstPersonFovApplied) _ownedCamera.fieldOfView = _savedFov;
            _ownedCamera.nearClipPlane = _savedNearClip;
            if (_savedCameraName != null) _ownedCamera.name = _savedCameraName;
        }
        _firstPersonFovApplied = false;
        _ownedCamera = null;
        _collisionDistance = -1f;
        _followingReferenceCaptured = false;
        _hasCinematicPose = false;
    }

    private void ApplyFreecamDistance(float distance)
    {
        _lastConfiguredFreeDistance = _settings.Camera.FreecamDistance.Value;
        Vector3 direction = _state.Offset.sqrMagnitude > 0.0001f ? _state.Offset.normalized : -(_state.Rotation * Vector3.forward);
        _state.Offset = direction * Mathf.Clamp(distance, 0f, _settings.FreecamRadius);
        _smoothVelocity = Vector3.zero;
    }

    private float ResolveSafeDistance(Vector3 origin, Vector3 direction, float distance)
    {
        if (!_settings.Camera.RetractAtWalls.Value)
        {
            _collisionDistance = -1f;
            return distance;
        }
        float safe = _adapter is IGameSpectatorCameraAdapter cameraAdapter
            ? cameraAdapter.GetSafeCameraDistance(origin, direction, distance) : distance;
        float dt = _lastModeFrame == Time.frameCount ? 0f : Time.unscaledDeltaTime;
        _collisionDistance = SpectatorCameraRules.RecoverDistance(_collisionDistance, safe, dt);
        return _collisionDistance;
    }

    private void ResetFollowHistory()
    {
        LethalCompanyFirstPersonVisibility.Clear();
        _followingReferenceCaptured = false;
        _hasCinematicPose = false;
        _snapCinematicPose = true;
        _collisionDistance = -1f;
        _hasSmoothedPosition = false;
        _hasPreviousAnchorPosition = false;
        _lastSmoothingFrame = -1;
        _lastModeFrame = -1;
        _smoothVelocity = Vector3.zero;
    }

    private void ApplySelectedView(Camera camera, Transform anchor, ref Vector3 position, ref Quaternion rotation)
    {
        AcquireCamera(camera);
        if (_state.Mode == SpectatorCameraMode.FirstPerson)
        {
            if (!_firstPersonFovApplied)
            {
                _savedFov = camera.fieldOfView;
                _savedCameraName = camera.name;
                if (camera.name == "MainCamera") camera.name = "EnhancedSpectatorCamera";
                _firstPersonFovApplied = true;
            }
            camera.fieldOfView = _settings.Camera.FirstPersonFov.Value;
        }
        else if (_firstPersonFovApplied)
        {
            camera.fieldOfView = _savedFov;
            if (_savedCameraName != null) camera.name = _savedCameraName;
            _firstPersonFovApplied = false;
        }
        camera.nearClipPlane = _state.Mode == SpectatorCameraMode.FirstPerson ? 0.03f : _savedNearClip;
        if (_state.Mode == SpectatorCameraMode.FirstPerson || _state.Mode == SpectatorCameraMode.Cinematic)
        {
            if (!(_adapter is IGameSpectatorCameraAdapter adapter)
                || !adapter.TryGetTargetEyePose(out Vector3 eyes, out Quaternion eyeRotation))
            {
                SelectMode(SpectatorCameraMode.Freecam);
                return;
            }
            if (_state.Mode == SpectatorCameraMode.FirstPerson)
            {
                position = eyes;
                rotation = eyeRotation;
            }
            else
            {
                ApplyCinematicView(camera, anchor, eyes, ref position, ref rotation);
            }
        }
        _lastModeFrame = Time.frameCount;
    }

    private void ApplyCinematicView(Camera camera, Transform anchor, Vector3 eyes, ref Vector3 position, ref Quaternion rotation)
    {
        bool newFrame = _lastModeFrame != Time.frameCount;
        float dt = newFrame ? Mathf.Min(.1f, Time.unscaledDeltaTime) : 0f;
        if (!_hasCinematicPose)
        {
            Vector3 offset = _snapCinematicPose ? -anchor.forward : camera.transform.position - anchor.position;
            _orbitAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            _cinematicPhase = 0; _cinematicVelocity = Vector3.zero;
            _cinematicPosition = camera.transform.position; _cinematicRotation = camera.transform.rotation;
            _cinematicAnchor = anchor.position; _hasCinematicPose = true;
            _cinematicStyle = CinematicStyles.Valid(_settings.Camera.CinematicStyle.Value);
            _cinematicBasisYaw = _cinematicStyle == 0 ? _orbitAngle : Mathf.Atan2(anchor.forward.x, anchor.forward.z) * Mathf.Rad2Deg;
            _cinematicShot = CinematicStyles.Sample(_cinematicStyle, 0);
            _cinematicGoalYaw = _cinematicBasisYaw + _cinematicShot.Yaw;
            _cinematicShot = CinematicStyles.WithYaw(_cinematicShot, _cinematicGoalYaw);
            _cinematicBlendElapsed = CinematicStyles.BlendSeconds;
        }
        int selectedStyle = CinematicStyles.Valid(_settings.Camera.CinematicStyle.Value);
        if (selectedStyle != _cinematicStyle)
        {
            _cinematicBlendFrom = _cinematicShot;
            _cinematicStyle = selectedStyle;
            _cinematicPhase = 0;
            _cinematicBasisYaw = selectedStyle == 0 ? _cinematicShot.Yaw : Mathf.Atan2(anchor.forward.x, anchor.forward.z) * Mathf.Rad2Deg;
            _cinematicGoalYaw = _cinematicShot.Yaw;
            _cinematicBlendElapsed = 0;
            ModLog.Info($"Cinematic style: {selectedStyle} ({CinematicStyles.Name(selectedStyle, false)}).");
        }
        if (newFrame)
        {
            _cinematicPhase = CinematicShotPath.Advance(_cinematicPhase, _settings.Camera.CinematicSpeed.Value, dt);
            Vector3 velocity = dt > .0001f ? (anchor.position - _cinematicAnchor) / dt : Vector3.zero;
            velocity.y = 0; velocity = Vector3.ClampMagnitude(velocity, 12f);
            _cinematicVelocity = Vector3.Lerp(_cinematicVelocity, velocity, 1f - Mathf.Exp(-dt / .6f));
            if (CinematicStyles.FollowsHeading(_cinematicStyle))
            {
                float heading = Mathf.Atan2(anchor.forward.x, anchor.forward.z) * Mathf.Rad2Deg;
                _cinematicBasisYaw = CinematicStyles.FollowHeading(_cinematicBasisYaw, heading, dt);
            }
        }
        var shot = CinematicStyles.Sample(_cinematicStyle, _cinematicPhase);
        _cinematicGoalYaw += CinematicStyles.AngleDelta(_cinematicGoalYaw, _cinematicBasisYaw + shot.Yaw);
        shot = CinematicStyles.WithYaw(shot, _cinematicGoalYaw);
        if (_cinematicBlendElapsed < CinematicStyles.BlendSeconds)
        {
            _cinematicBlendElapsed = Mathf.Min(CinematicStyles.BlendSeconds, _cinematicBlendElapsed + dt);
            shot = CinematicStyles.Blend(_cinematicBlendFrom, shot, _cinematicBlendElapsed);
        }
        _cinematicShot = shot;
        float radius = Mathf.Clamp(_settings.Camera.CinematicDistance.Value * shot.Radius, 1.5f, SpectatorCameraRules.MaximumFollowDistance);
        Vector3 direction = Quaternion.Euler(0, shot.Yaw, 0) * Vector3.forward;
        Vector3 lead = Vector3.ClampMagnitude(_cinematicVelocity * .18f, 1f);
        Vector3 focus = Vector3.Lerp(anchor.position, eyes, .62f) + Vector3.up * (shot.FocusHeight - 1.15f) + lead;
        focus += Vector3.Cross(Vector3.up, -direction) * (radius * shot.Composition);
        Vector3 desired = anchor.position + Vector3.up * shot.Height + direction * radius + lead * .25f;
        Vector3 candidate = _cinematicPosition + (anchor.position - _cinematicAnchor);
        candidate = _snapCinematicPose ? desired : Vector3.Lerp(candidate, desired, 1f - Mathf.Exp(-dt / .65f));
        Vector3 delta = candidate - focus; float length = delta.magnitude;
        position = length > .001f ? focus + delta / length * ResolveSafeDistance(focus, delta / length, Mathf.Min(length, SpectatorCameraRules.MaximumFollowDistance)) : focus;
        Quaternion look = (focus - position).sqrMagnitude > .001f ? Quaternion.LookRotation(focus - position, Vector3.up) : _cinematicRotation;
        rotation = _snapCinematicPose ? look : Quaternion.Slerp(_cinematicRotation, look, 1f - Mathf.Exp(-dt / .30f));
        _snapCinematicPose = false;
        _cinematicPosition = position; _cinematicRotation = rotation; _cinematicAnchor = anchor.position;
    }
}
