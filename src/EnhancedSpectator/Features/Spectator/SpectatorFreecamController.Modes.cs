using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

public sealed partial class SpectatorFreecamController
{
    private readonly ShiningFollowRig _shiningRig = new ShiningFollowRig();
    private readonly SpectatorAutoCenter _autoCenter = new SpectatorAutoCenter();
    private int _autoCenterFrame = -1;
    private Quaternion _autoCenterRotation;
    private readonly SpectatorFollowStabilizer _followRig = new SpectatorFollowStabilizer();
    private Vector3 _followAnchor;
    private int _followFrame = -1;
    private Transform? _followTarget, _followSurface;
    private Vector3 _supportPoint, _supportLocalPoint;
    private Vector3 FollowAnchor(Transform anchor)
    {
        if (_followTarget != anchor || Time.frameCount-_followFrame>2) { ClearFollowRig(); _followTarget=anchor; }
        if (_followFrame == Time.frameCount) return _followAnchor;
        _followFrame=Time.frameCount;
        bool enabled=_settings.Camera.StabilizeFollow.Value;
        Vector3 target=anchor.position;
        if(enabled && _adapter is IGameFollowSupportAdapter support && support.TryGetFollowSupport(out float height,out var surface))
        {
            target.y=height;
            if (surface!=null && surface==_followSurface)
            {
                Vector3 travel=surface.TransformPoint(_supportLocalPoint)-_supportPoint;
                if (travel.sqrMagnitude<64) _followRig.Transport(travel);
                else _followRig.Clear();
            }
            _followSurface=surface; _supportPoint=target;
            if(surface!=null) _supportLocalPoint=surface.InverseTransformPoint(target);
        }
        else _followSurface=null;
        _followAnchor=_followRig.Update(target,Time.unscaledDeltaTime,enabled,_settings.Camera.FollowSpeed.Value);
        return _followAnchor;
    }
    private void ClearFollowRig() { _shiningRig.Clear(); _autoCenter.Rebase(); _autoCenterFrame=-1; _followRig.Clear(); _followFrame=-1; _followTarget=null; _followSurface=null; }
    private Camera? _ownedCamera;
    private readonly MonitorModePreference _monitorPreference = new MonitorModePreference();
    private readonly MonitorCameraDirector _monitorDirector = new MonitorCameraDirector();
    private readonly MonitorCabinLens _monitorCabinLens = new MonitorCabinLens();
    private float _nextMonitorDiagnostic;
    private bool _monitorEyeFallback;
    private float _monitorRecoveryOpacity;
    private MonitorTrackingState _monitorReportedState;
    private Vector3 _monitorPosition;
    private Quaternion _monitorRotation;
    private float _vanillaDistance = 1.3f;
    /// <summary>The wheel-set vanilla orbit distance (also the split-screen vanilla large view's).</summary>
    internal float VanillaDistance => _vanillaDistance;
    /// <summary>Vanilla's default spectate distance again (a split-screen large view opened anew).</summary>
    internal void ResetVanillaDistance() => _vanillaDistance = 1.3f;
    private readonly SpectatorVanillaIdle _vanillaIdle = new();
    /// <summary>
    /// Vanilla spectating keeps the game's own view, so it has no idle turn or height of its own; after the idle wait
    /// with no view mouse or wheel input the ghost simply counts as centred on its teammate (hidden for others,
    /// translucent while speaking).
    /// </summary>
    private bool UsesVanillaView => _wasSpectating && !_state.UserEnabled && !IsPreview && (!_splitScreenActive || _splitScreenFocused);
    internal bool IsVanillaIdle => UsesVanillaView && _vanillaIdle.IsIdle;
    /// <summary>
    /// The last frame the player moved the camera with the mouse (not through a menu, a free cursor or watching
    /// together); a reader sampling less often than every frame still sees a movement between its samples.
    /// </summary>
    internal int CameraMovedFrame { get; private set; } = -1;
    /// <summary>Vanilla orbit distance, reset on death and independent of enhanced entry framing.</summary>
    public bool TryGetVanillaDistance(out float distance)
    {
        distance = _vanillaDistance;
        return _settings.EnableEnhancedSpectator && _wasSpectating && !_state.UserEnabled;
    }
    private float _savedFov;
    private bool _lensOverrideApplied;
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
    internal float TransitionOpacity => OwnsCamera && _state.Mode == SpectatorCameraMode.Monitor ? Mathf.Max(_monitorDirector.TransitionOpacity, _monitorRecoveryOpacity) : 0;
    /// <summary>Whether enhanced rendering owns the spectator camera.</summary>
    public bool OwnsCamera => _ownedCamera != null && _state.IsActive && _state.UserEnabled;
    internal bool IsAutoCentering => OwnsCamera && _settings.Camera.AutoCenter.Value && _autoCenter.IsCentering;
    internal int EffectiveMonitorStyle => _monitorPreference.Automatic ? 2 : MonitorCameraStyles.Available(_settings.Camera.MonitorStyle.Value);
    internal void SelectMonitorStyle(int style)
    {
        _settings.Camera.MonitorStyle.Value=MonitorCameraStyles.Available(style);
        if(_wasSpectating) _monitorPreference.SelectStyle(_state.Mode);
    }
    private void UpdateAutomaticMonitor()
    {
        var mode=_monitorPreference.ResolveAutomatic(_state.Mode,_state.UserEnabled,CanUseMonitorView,
            _settings.Camera.AutoMonitorIndoors.Value,out bool enabled);
        if(mode!=_state.Mode || enabled!=_state.UserEnabled)
        {
            if(enabled) SetCameraMode(mode,false);
            else { _state.Mode=mode; _state.UserEnabled=false; }
        }
    }
    internal bool CanUseMonitorView => _adapter is IGameMonitorCameraAdapter { IsMonitorTargetIndoors: true };

    /// <summary>Selects a local mode without touching the watched player's camera or input.</summary>
    public void SelectMode(SpectatorCameraMode mode) => SetCameraMode(mode,true);

    private void SetCameraMode(SpectatorCameraMode mode,bool explicitSelection)
    {
        if (mode == SpectatorCameraMode.Director) return; // Shelved prototypes have no runtime entry.
        if ((!IsPreview && !TryGetEligibleSnapshot(out _)) || (mode == SpectatorCameraMode.ThirdPerson && !_settings.EnableThirdPerson)) return;
        if (explicitSelection && _splitScreenActive) _splitScreenMode = mode;
        if (mode == SpectatorCameraMode.Monitor
            && !(_adapter is IGameMonitorCameraAdapter { IsMonitorTargetIndoors: true })) return;
        if(explicitSelection) _monitorPreference.Select(mode);
        ClearDeathHandoff();
        _monitorDirector.Clear();
        ClearFollowRig(); _autoCenter.Clear();
        _monitorEyeFallback = false;
        _monitorRecoveryOpacity = 0;
        (_adapter as IGameMonitorCameraAdapter)?.ClearMonitorRoom();
        LethalCompanyFirstPersonVisibility.Clear();
        _state.Mode = mode;
        _state.UserEnabled = true;
        _collisionDistance = -1f;
        _followingReferenceCaptured = false;
        _hasCinematicPose = false;
        _lastModeFrame = -1;
        _smoothVelocity = Vector3.zero;
        ModLog.Debug($"Spectator camera mode: {mode}.");
        if (mode == SpectatorCameraMode.Cinematic)
            ModLog.Debug($"Cinematic styles: build=cinema-r7-20260924; selected={_settings.Camera.CinematicStyle.Value}; count={CinematicStyles.Count}; arrows=Left/Right.");
    }

    /// <summary>Resets the visible view preference and radial distance even when values were already default.</summary>
    public void ResetOptionsView()
    {
        _monitorPreference.Clear();
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
        if (_splitScreenActive) _splitScreenMode = null;
        _monitorPreference.Select(SpectatorCameraMode.Freecam);
        _state.UserEnabled = false;
        Deactivate(clearAnchor: true);
    }

    /// <summary>Restores borrowed state and releases this controller.</summary>
    public void Dispose()
    {
        ReturnToVanilla();
        if (ReferenceEquals(Current, this)) Current = null;
    }

    private bool CameraInputBlocked => Mirrored || (_adapter is IGameSpectatorCameraAdapter cameraAdapter
        ? cameraAdapter.IsCameraInputBlocked() : _adapter.IsLocalQuickMenuOpen());

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
        _monitorDirector.Clear();
        LethalCompanyCameraTransition.Clear();
        if (_ownedCamera != null) { ClearFollowRig(); _autoCenter.Clear(); }
        _monitorEyeFallback = false;
        _monitorRecoveryOpacity = 0;
        (_adapter as IGameMonitorCameraAdapter)?.ClearMonitorRoom();
        LethalCompanyFirstPersonVisibility.Clear();
        if (_ownedCamera != null)
        {
            if (_lensOverrideApplied) _ownedCamera.fieldOfView = _savedFov;
            _ownedCamera.nearClipPlane = _savedNearClip;
            if (_savedCameraName != null) _ownedCamera.name = _savedCameraName;
        }
        _lensOverrideApplied = false;
        _monitorCabinLens.Clear();
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
        _monitorDirector.Clear();
        ClearFollowRig();
        _monitorEyeFallback = false;
        _monitorRecoveryOpacity = 0;
        (_adapter as IGameMonitorCameraAdapter)?.ClearMonitorRoom();
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

    private void ApplyViewLens(Camera camera)
    {
        bool firstPerson=_state.Mode==SpectatorCameraMode.FirstPerson;
        bool monitor=_state.Mode==SpectatorCameraMode.Monitor;
        bool cabin=monitor && !_monitorEyeFallback && _monitorDirector.CabinLensReady;
        if(!monitor) _monitorCabinLens.Clear();
        if (firstPerson || cabin || (monitor && _monitorCabinLens.Active))
        {
            if (!_lensOverrideApplied)
            {
                _savedFov = camera.fieldOfView;
                _savedCameraName = camera.name;
                if (camera.name == "MainCamera") camera.name = "EnhancedSpectatorCamera";
                _lensOverrideApplied = true;
            }
            camera.fieldOfView = firstPerson ? _settings.Camera.FirstPersonFov.Value
                : _monitorCabinLens.Update(cabin,camera.fieldOfView,_savedFov,
                    _lastModeFrame==Time.frameCount ? 0 : Time.unscaledDeltaTime,_monitorDirector.TransitionOpacity>=.999f);
        }
        else if (_lensOverrideApplied)
        {
            camera.fieldOfView = _savedFov;
            if (_savedCameraName != null) camera.name = _savedCameraName;
            _lensOverrideApplied = false;
        }
    }

    private void ApplySelectedView(Camera camera, Transform anchor, ref Vector3 position, ref Quaternion rotation)
    {
        AcquireCamera(camera);
        camera.nearClipPlane = _state.Mode == SpectatorCameraMode.FirstPerson ? 0.03f : _savedNearClip;
        if (_state.Mode == SpectatorCameraMode.FirstPerson || _state.Mode == SpectatorCameraMode.Cinematic
            || _state.Mode == SpectatorCameraMode.Monitor)
        {
            if (!(_adapter is IGameSpectatorCameraAdapter adapter)
                || !adapter.TryGetTargetEyePose(out Vector3 eyes, out Quaternion eyeRotation))
            {
                // Missing eye data is not a user's decision to abandon monitor mode.
                if(_state.Mode==SpectatorCameraMode.Monitor)
                { position=camera.transform.position; rotation=camera.transform.rotation; }
                else SetCameraMode(SpectatorCameraMode.Freecam,false);
                ApplyViewLens(camera);
                return;
            }
            if (_state.Mode == SpectatorCameraMode.FirstPerson)
            {
                position = eyes;
                rotation = eyeRotation;
            }
            else if (_state.Mode == SpectatorCameraMode.Monitor)
            {
                // LateUpdate and pre-cull share one result: no duplicate probes or smoothing advancement.
                if (_lastModeFrame != Time.frameCount)
                {
                    bool wasEyeFallback = _monitorEyeFallback;
                    _monitorRecoveryOpacity = Mathf.Max(0, _monitorRecoveryOpacity-Time.unscaledDeltaTime/.25f);
                    Vector3 focus = FollowAnchor(anchor) + Vector3.up * 1.05f;
                    _monitorDirector.DiagnosticsEnabled=_settings.Camera.DebugMonitorCamera.Value;
                    if(MonitorCameraStyles.IsContinuous(EffectiveMonitorStyle) && _adapter is IGameMonitorCameraAdapter seedWorld)
                        _monitorDirector.SeedTravelling(seedWorld,camera.transform.position,camera.transform.rotation,EffectiveMonitorStyle);
                    if (!(_adapter is IGameMonitorCameraAdapter monitor)
                        || !_monitorDirector.TryUpdate(monitor, focus, Time.unscaledTime,
                            Mathf.Min(.1f, Time.unscaledDeltaTime), out _monitorPosition, out _monitorRotation, EffectiveMonitorStyle, _settings.Camera.FollowSpeed.Value,
                            _settings.Camera.TravellingCatchUpDistance.Value))
                    {
                        // Last resort: the target's known eye pose while room stations recover.
                        // Keep Monitor selected and the ghost stowed; no saved FOV or input preference changes.
                        _monitorPosition = eyes; _monitorRotation = eyeRotation;
                        _monitorEyeFallback = true;
                    }
                    else _monitorEyeFallback = false;
                    if(_monitorDirector.DiagnosticsEnabled && Time.unscaledTime>=_nextMonitorDiagnostic)
                    {
                        _nextMonitorDiagnostic=Time.unscaledTime+2f;
                        string geometry=(_adapter as IGameMonitorDiagnosticsAdapter)?.DescribeMonitorGeometry(_monitorPosition)??"unavailable";
                        ModLog.Info($"[MonitorDiag travel-r3-concave-20260927] frame={Time.frameCount},t={Time.unscaledTime:F2},dt={Time.unscaledDeltaTime:F3},style={EffectiveMonitorStyle}/{MonitorCameraStyles.Name(EffectiveMonitorStyle,false)},state={_monitorDirector.State},eyeFallback={_monitorEyeFallback},camera={_monitorPosition:F2},focus={focus:F2},body={anchor.position:F2},eyes={eyes:F2}; {_monitorDirector.Describe(Time.unscaledTime)}; geometry=[{geometry}]");
                    }
                    if (wasEyeFallback != _monitorEyeFallback && _lastModeFrame >= 0) _monitorRecoveryOpacity = 1;
                    if (_monitorReportedState != _monitorDirector.State)
                    {
                        _monitorReportedState = _monitorDirector.State;
                        ModLog.Debug($"Monitor tracking: {_monitorReportedState}; style={EffectiveMonitorStyle}.");
                    }
                }
                camera.nearClipPlane = _monitorEyeFallback ? .03f : _savedNearClip;
                position = _monitorPosition; rotation = _monitorRotation;
            }
            else
            {
                ApplyCinematicView(camera, anchor, eyes, ref position, ref rotation);
            }
        }
        ApplyViewLens(camera);
        _lastModeFrame = Time.frameCount;
    }

    private void ApplyCinematicView(Camera camera, Transform anchor, Vector3 eyes, ref Vector3 position, ref Quaternion rotation)
    {
        Vector3 body = FollowAnchor(anchor);
        if (_settings.Camera.StabilizeFollow.Value) eyes = body + Vector3.up * 1.6f;
        bool newFrame = _lastModeFrame != Time.frameCount;
        float dt = newFrame ? Mathf.Min(.1f, Time.unscaledDeltaTime) : 0f;
        if (!_hasCinematicPose)
        {
            Vector3 offset = _snapCinematicPose ? -anchor.forward : camera.transform.position - body;
            _orbitAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            _cinematicPhase = 0; _cinematicVelocity = Vector3.zero;
            _cinematicPosition = camera.transform.position; _cinematicRotation = camera.transform.rotation;
            _cinematicAnchor = body; _hasCinematicPose = true;
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
            _shiningRig.Clear();
            _cinematicBlendFrom = _cinematicShot;
            _cinematicStyle = selectedStyle;
            _cinematicPhase = 0;
            _cinematicBasisYaw = selectedStyle == 0 ? _cinematicShot.Yaw : Mathf.Atan2(anchor.forward.x, anchor.forward.z) * Mathf.Rad2Deg;
            _cinematicGoalYaw = _cinematicShot.Yaw;
            _cinematicBlendElapsed = 0;
            ModLog.Debug($"Cinematic style: {selectedStyle} ({CinematicStyles.Name(selectedStyle, false)}).");
        }
        if (newFrame)
        {
            _cinematicPhase = CinematicShotPath.Advance(_cinematicPhase, _settings.Camera.CinematicSpeed.Value, dt, _cinematicStyle < 10 ? 1.8f : 1f);
            Vector3 velocity = dt > .0001f ? (body - _cinematicAnchor) / dt : Vector3.zero;
            velocity.y = 0; velocity = Vector3.ClampMagnitude(velocity, 12f);
            _cinematicVelocity = Vector3.Lerp(_cinematicVelocity, velocity, 1f - Mathf.Exp(-dt / .6f));
            if (CinematicStyles.FollowsHeading(_cinematicStyle))
            {
                float heading = Mathf.Atan2(anchor.forward.x, anchor.forward.z) * Mathf.Rad2Deg;
                _cinematicBasisYaw = CinematicStyles.FollowHeading(_cinematicBasisYaw, heading, dt, _cinematicStyle < 10);
            }
        }
        var shot = CinematicStyles.Sample(_cinematicStyle, _cinematicPhase);
        _cinematicGoalYaw += CinematicStyles.AngleDelta(_cinematicGoalYaw, _cinematicBasisYaw + shot.Yaw);
        shot = CinematicStyles.WithYaw(shot, _cinematicGoalYaw);
        if (_cinematicStyle == 10)
        {
            _cinematicBasisYaw = Mathf.Atan2(anchor.forward.x, anchor.forward.z) * Mathf.Rad2Deg;
            shot = CinematicStyles.WithYaw(CinematicStyles.Sample(10,0), _cinematicBasisYaw + 180f);
            _cinematicBlendElapsed = CinematicStyles.BlendSeconds;
        }
        if (_cinematicBlendElapsed < CinematicStyles.BlendSeconds)
        {
            _cinematicBlendElapsed = Mathf.Min(CinematicStyles.BlendSeconds, _cinematicBlendElapsed + dt);
            shot = CinematicStyles.Blend(_cinematicBlendFrom, shot, _cinematicBlendElapsed);
        }
        _cinematicShot = shot;
        float radius = Mathf.Clamp(_settings.Camera.CinematicDistance.Value * shot.Radius, 1.5f, SpectatorCameraRules.MaximumFollowDistance);
        Vector3 direction = Quaternion.Euler(0, shot.Yaw, 0) * Vector3.forward;
        Vector3 lead = _cinematicStyle >= 10 ? Vector3.zero : Vector3.ClampMagnitude(_cinematicVelocity * .18f, 1f);
        Vector3 focus = Vector3.Lerp(body, eyes, .62f) + Vector3.up * (shot.FocusHeight - 1.15f) + lead;
        focus += Vector3.Cross(Vector3.up, -direction) * (radius * shot.Composition);
        Vector3 desired = body + Vector3.up * shot.Height + direction * radius + lead * .25f;
        Vector3 candidate = _cinematicPosition + (body - _cinematicAnchor);
        candidate = _snapCinematicPose ? desired : Vector3.Lerp(candidate, desired, 1f - Mathf.Exp(-dt / ((_cinematicStyle < 10 ? .22f : .65f) * SpectatorFollowStabilizer.ResponseScale(_settings.Camera.FollowSpeed.Value))));
        if (_cinematicStyle == 10)
            candidate = _shiningRig.Update(body, anchor.forward, _snapCinematicPose ? desired : _cinematicPosition+(body-_cinematicAnchor), radius, shot.Height, dt, _settings.Camera.FollowSpeed.Value);
        Vector3 delta = candidate - focus; float length = delta.magnitude;
        position = length > .001f ? focus + delta / length * ResolveSafeDistance(focus, delta / length, Mathf.Min(length, SpectatorCameraRules.MaximumFollowDistance)) : focus;
        Quaternion look = (focus - position).sqrMagnitude > .001f ? Quaternion.LookRotation(focus - position, Vector3.up) : _cinematicRotation;
        rotation = _snapCinematicPose ? look : Quaternion.Slerp(_cinematicRotation, look, 1f - Mathf.Exp(-dt / ((_cinematicStyle == 10 ? .065f : _cinematicStyle < 10 ? .16f : .30f) * SpectatorFollowStabilizer.ResponseScale(_settings.Camera.FollowSpeed.Value))));
        _snapCinematicPose = false;
        _cinematicPosition = position; _cinematicRotation = rotation; _cinematicAnchor = body;
    }
}
