using System;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Runtime;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Applies local enhanced spectator freecam movement before the spectator camera renders.
/// </summary>
public sealed class SpectatorFreecamController
{
    private const float MaxPitch = 85f;
    private const float MinPitch = -85f;
    private const int TargetSwitchRecoveryFrames = 4;
    private const int CameraInactiveRecoveryFrames = 3;
    private const float AnchorTeleportThreshold = 12f;
    private const float ThirdPersonCollisionPadding = 0.15f;
    private const float ThirdPersonZoomStep = 0.5f;
    private const float ThirdPersonMinimumDistance = 1.5f;
    private const float ThirdPersonMaximumDistance = 15f;

    private readonly IGameSpectatorAdapter _adapter;
    private readonly SpectatorSnapshotCache _snapshotCache;
    private readonly SpectatorAnchorService _anchorService;
    private readonly SpectatorInputService _inputService;
    private readonly SpectatorFreecamSettings _settings;
    private readonly SpectatorCameraState _state = new SpectatorCameraState();

    private bool _wasSpectating;
    private bool _hasPose;
    private bool _recenterRequested;
    private float _yaw;
    private float _pitch;
    private Vector3 _smoothVelocity;
    private Vector3 _smoothedPosition;
    private bool _hasSmoothedPosition;
    private Vector3 _previousAnchorPosition;
    private bool _hasPreviousAnchorPosition;
    private int _lastSmoothingFrame = -1;
    private int _lastPreCullApplyFrame = -1;
    private int _nextApplyDebugFrame;
    private int _nextMenuBlockDebugFrame;
    private int _targetSwitchGraceUntilFrame = -1;
    private int _cameraInactiveGraceUntilFrame = -1;
    private int _nextEligibilityDebugFrame;
    private SpectatorFreecamIneligibleReason _lastEligibilityDebugReason = SpectatorFreecamIneligibleReason.None;
    private bool _cameraInactiveGraceStarted;
    private float _thirdPersonDistance;

    /// <summary>
    /// Creates a spectator freecam controller.
    /// </summary>
    public SpectatorFreecamController(
        IGameSpectatorAdapter adapter,
        SpectatorSnapshotCache snapshotCache,
        SpectatorAnchorService anchorService,
        SpectatorInputService inputService,
        SpectatorFreecamSettings settings)
    {
        _adapter = adapter;
        _snapshotCache = snapshotCache ?? throw new ArgumentNullException(nameof(snapshotCache));
        _anchorService = anchorService;
        _inputService = inputService;
        _settings = settings;
        _state.UserEnabled = settings.FreecamDefaultOn;
        _thirdPersonDistance = settings.ThirdPersonDistance;
    }

    /// <summary>
    /// Gets the current freecam state.
    /// </summary>
    public SpectatorCameraState State => _state;

    /// <summary>
    /// Handles low-risk spectator lifecycle notifications from patches.
    /// </summary>
    public void NotifyLifecycleEvent(SpectatorLifecycleEventKind kind)
    {
        switch (kind)
        {
            case SpectatorLifecycleEventKind.PlayerDied:
            case SpectatorLifecycleEventKind.CameraSwitched:
                _hasPose = false;
                _cameraInactiveGraceUntilFrame = SpectatorFreecamRecoveryPolicy.ExtendGraceUntilFrame(
                    Time.frameCount,
                    CameraInactiveRecoveryFrames);
                break;
            case SpectatorLifecycleEventKind.SpectatedPlayerEffectsApplied:
                BeginTargetSwitchRecoveryWindow();
                if (_settings.RecenterOnTargetSwitch)
                {
                    _recenterRequested = true;
                }

                break;
            case SpectatorLifecycleEventKind.GameOverOverrideChanged:
                if (_settings.DisableDuringGameOverOverride && _adapter.IsGameOverSpectateOverrideActive())
                {
                    Deactivate(clearAnchor: false);
                }

                break;
            case SpectatorLifecycleEventKind.Revived:
                ResetForNonSpectator();
                break;
        }
    }

    /// <summary>
    /// Reads local freecam control keys while the local player is spectating.
    /// </summary>
    public void Tick()
    {
        try
        {
            if (!TryGetEligibleSnapshot(out _))
            {
                SpectatorVanillaInputGuard.Clear();
                return;
            }

            bool quickMenuOpen = _adapter.IsLocalQuickMenuOpen();
            UpdateVanillaInputGuard(enabled: _state.UserEnabled, quickMenuBlocksInput: quickMenuOpen);

            if (quickMenuOpen)
            {
                LogQuickMenuInputBlocked();
                return;
            }

            if (_inputService.ResetToVanillaPressed)
            {
                _state.UserEnabled = false;
                Deactivate(clearAnchor: false);
                ModLog.Info("Enhanced spectator freecam disabled until toggled again.");
                return;
            }

            if (_inputService.ToggleFreecamPressed && _settings.EnableFreecam)
            {
                _state.UserEnabled = !_state.UserEnabled;
                _state.Mode = SpectatorCameraMode.Freecam;
                _hasPose = false;
                _recenterRequested = _state.UserEnabled;
                ModLog.Info(_state.UserEnabled
                    ? "Enhanced spectator freecam enabled."
                    : "Enhanced spectator freecam disabled.");
            }

            if (_inputService.ToggleThirdPersonPressed && _settings.EnableThirdPerson)
            {
                _state.UserEnabled = true;
                _state.Mode = _state.Mode == SpectatorCameraMode.ThirdPerson
                    ? SpectatorCameraMode.Freecam
                    : SpectatorCameraMode.ThirdPerson;
                _smoothVelocity = Vector3.zero;
                _lastSmoothingFrame = -1;
                ModLog.Info(_state.Mode == SpectatorCameraMode.ThirdPerson
                    ? "Self-ghost third-person view enabled."
                    : "Self-ghost third-person view disabled; freecam restored.");
            }

            if (_state.Mode == SpectatorCameraMode.ThirdPerson)
            {
                float scrollDelta = _inputService.ReadScrollDelta();
                if (!Mathf.Approximately(scrollDelta, 0f))
                {
                    _thirdPersonDistance = SpectatorThirdPersonCameraRules.ResolveZoomDistance(
                        _thirdPersonDistance,
                        scrollDelta,
                        ThirdPersonZoomStep,
                        ThirdPersonMinimumDistance,
                        ThirdPersonMaximumDistance);
                }
            }

            if (_inputService.RecenterPressed)
            {
                _recenterRequested = true;
            }
        }
        catch (Exception ex)
        {
            DisableAfterFailure(ex);
        }
    }

    /// <summary>
    /// Applies the camera transform after vanilla spectator camera updates.
    /// </summary>
    public void LateTick()
    {
        try
        {
            if (!TryGetEligibleSnapshot(out GameSpectatorSnapshot snapshot))
            {
                return;
            }

            if (!_state.UserEnabled)
            {
                Deactivate(clearAnchor: false);
                return;
            }

            if (!_anchorService.TryUpdate(snapshot, out Transform? anchor, out bool targetChanged) || anchor == null)
            {
                if (!TrySoftPause(SpectatorFreecamIneligibleReason.MissingCameraAnchorOrTarget, snapshot))
                {
                    DeactivateWithReason(SpectatorFreecamIneligibleReason.MissingCameraAnchorOrTarget, snapshot, clearAnchor: true);
                }

                return;
            }

            Camera camera = snapshot.SpectateCamera!;
            if (!_hasPose)
            {
                InitializePoseFromCamera(camera, anchor);
            }

            if (_recenterRequested || (targetChanged && _settings.RecenterOnTargetSwitch))
            {
                Recenter(camera, anchor);
                _recenterRequested = false;
            }

            bool quickMenuOpen = _adapter.IsLocalQuickMenuOpen();
            UpdateVanillaInputGuard(enabled: _state.UserEnabled, quickMenuBlocksInput: quickMenuOpen);
            if (quickMenuOpen)
            {
                LogQuickMenuInputBlocked();
            }
            else
            {
                ApplyInput();
            }

            ApplyCameraTransform(camera, anchor);
            UpdateState(snapshot);
            _state.IsActive = true;
        }
        catch (Exception ex)
        {
            DisableAfterFailure(ex);
        }
    }

    /// <summary>
    /// Applies the camera transform immediately before Unity renders the spectator camera.
    /// </summary>
    public void CameraPreCullTick(Camera renderingCamera)
    {
        try
        {
            if (renderingCamera == null || !_state.UserEnabled || !_hasPose)
            {
                return;
            }

            if (!TryGetEligibleSnapshot(out GameSpectatorSnapshot snapshot))
            {
                return;
            }

            Camera? spectateCamera = snapshot.SpectateCamera;
            if (spectateCamera == null || renderingCamera != spectateCamera)
            {
                return;
            }

            if (_lastPreCullApplyFrame == Time.frameCount)
            {
                return;
            }

            if (!_anchorService.TryUpdate(snapshot, out Transform? anchor, out bool targetChanged) || anchor == null)
            {
                if (!TrySoftPause(SpectatorFreecamIneligibleReason.MissingCameraAnchorOrTarget, snapshot))
                {
                    DeactivateWithReason(SpectatorFreecamIneligibleReason.MissingCameraAnchorOrTarget, snapshot, clearAnchor: true);
                }

                return;
            }

            if (targetChanged && _settings.RecenterOnTargetSwitch)
            {
                Recenter(spectateCamera, anchor);
            }

            ApplyCameraTransform(spectateCamera, anchor);
            _lastPreCullApplyFrame = Time.frameCount;
            UpdateState(snapshot);
        }
        catch (Exception ex)
        {
            DisableAfterFailure(ex);
        }
    }

    private bool TryGetEligibleSnapshot(out GameSpectatorSnapshot snapshot)
    {
        if (!_settings.EnableEnhancedSpectator || (!_settings.EnableFreecam && !_settings.EnableThirdPerson))
        {
            DeactivateWithReason(SpectatorFreecamIneligibleReason.FeatureDisabled, GameSpectatorSnapshot.Unavailable, clearAnchor: false);
            snapshot = GameSpectatorSnapshot.Unavailable;
            return false;
        }

        if (!RuntimeConnectionState.CanRunLocalDiagnostics(out _))
        {
            snapshot = GameSpectatorSnapshot.Unavailable;
            if (!TrySoftPause(SpectatorFreecamIneligibleReason.LifecycleUnsafe, snapshot))
            {
                DeactivateWithReason(SpectatorFreecamIneligibleReason.LifecycleUnsafe, snapshot, clearAnchor: false);
            }

            return false;
        }

        if (!_snapshotCache.TryGetCurrentFrameSnapshot(out snapshot))
        {
            ResetForNonSpectator();
            return false;
        }

        if (!snapshot.HasRound || !snapshot.HasLocalPlayer || !snapshot.IsLocalPlayerDead)
        {
            ResetForNonSpectator();
            return false;
        }

        if (!_wasSpectating)
        {
            EnterSpectatorState();
        }

        if (snapshot.SpectateCamera == null || snapshot.Anchor == null || !snapshot.HasSpectatedTarget)
        {
            if (!TrySoftPause(SpectatorFreecamIneligibleReason.MissingCameraAnchorOrTarget, snapshot))
            {
                DeactivateWithReason(SpectatorFreecamIneligibleReason.MissingCameraAnchorOrTarget, snapshot, clearAnchor: true);
            }

            return false;
        }

        if (!snapshot.IsSpectateCameraActive)
        {
            BeginCameraInactiveRecoveryWindow();
            if (!TrySoftPause(SpectatorFreecamIneligibleReason.SpectateCameraInactive, snapshot))
            {
                DeactivateWithReason(SpectatorFreecamIneligibleReason.SpectateCameraInactive, snapshot, clearAnchor: false);
            }

            return false;
        }

        if (snapshot.IsGameOverOverride && _settings.DisableDuringGameOverOverride)
        {
            DeactivateWithReason(SpectatorFreecamIneligibleReason.GameOverOverride, snapshot, clearAnchor: false);
            return false;
        }

        _targetSwitchGraceUntilFrame = -1;
        _cameraInactiveGraceUntilFrame = -1;
        _cameraInactiveGraceStarted = false;
        _lastEligibilityDebugReason = SpectatorFreecamIneligibleReason.None;
        return true;
    }

    private void EnterSpectatorState()
    {
        _wasSpectating = true;
        _state.UserEnabled = _settings.EnableFreecam && _settings.FreecamDefaultOn;
        _state.Mode = SpectatorCameraMode.Freecam;
        _thirdPersonDistance = _settings.ThirdPersonDistance;
        _state.IsActive = false;
        _hasPose = false;
        _recenterRequested = _state.UserEnabled;
        _smoothVelocity = Vector3.zero;
        _hasSmoothedPosition = false;
        _hasPreviousAnchorPosition = false;
        _lastSmoothingFrame = -1;
        _lastPreCullApplyFrame = -1;
        _targetSwitchGraceUntilFrame = -1;
        _cameraInactiveGraceUntilFrame = -1;
        _cameraInactiveGraceStarted = false;
        ModLog.Debug("Entered local spectator state.");
    }

    private void ResetForNonSpectator()
    {
        bool hadState = _wasSpectating
            || _hasPose
            || _state.IsActive
            || _state.TargetSlotId.HasValue
            || _state.TargetActualClientId.HasValue;

        _wasSpectating = false;
        _hasPose = false;
        _recenterRequested = false;
        _smoothVelocity = Vector3.zero;
        _hasSmoothedPosition = false;
        _lastSmoothingFrame = -1;
        _lastPreCullApplyFrame = -1;
        _targetSwitchGraceUntilFrame = -1;
        _cameraInactiveGraceUntilFrame = -1;
        _cameraInactiveGraceStarted = false;
        SpectatorVanillaInputGuard.Clear();
        _anchorService.Clear();
        _state.IsActive = false;
        _state.UserEnabled = _settings.EnableFreecam && _settings.FreecamDefaultOn;
        _state.Mode = SpectatorCameraMode.Freecam;
        _thirdPersonDistance = _settings.ThirdPersonDistance;
        _state.TargetSlotId = null;
        _state.TargetActualClientId = null;
        _state.Offset = Vector3.zero;
        _state.Rotation = Quaternion.identity;
        _state.RepresentationRotation = Quaternion.identity;
        _state.WorldPosition = Vector3.zero;
        _state.RenderedWorldPosition = Vector3.zero;
        _state.HasWorldPose = false;
        _hasPreviousAnchorPosition = false;
        if (hadState)
        {
            ModLog.Debug("Reset local spectator freecam state.");
        }
    }

    private void Deactivate(bool clearAnchor)
    {
        bool wasActive = _state.IsActive;
        _state.IsActive = false;
        _smoothVelocity = Vector3.zero;
        _lastSmoothingFrame = -1;
        SpectatorVanillaInputGuard.Clear();

        if (clearAnchor)
        {
            _anchorService.Clear();
            _hasPose = false;
            _hasSmoothedPosition = false;
            _hasPreviousAnchorPosition = false;
            _targetSwitchGraceUntilFrame = -1;
            _cameraInactiveGraceStarted = false;
            _state.TargetSlotId = null;
            _state.TargetActualClientId = null;
            _state.WorldPosition = Vector3.zero;
            _state.RenderedWorldPosition = Vector3.zero;
            _state.HasWorldPose = false;
        }

        if (wasActive)
        {
            ModLog.Debug(clearAnchor
                ? "Deactivated local spectator freecam and cleared anchor."
                : "Deactivated local spectator freecam.");
        }
    }

    private void BeginTargetSwitchRecoveryWindow()
    {
        _targetSwitchGraceUntilFrame = SpectatorFreecamRecoveryPolicy.ExtendGraceUntilFrame(
            Time.frameCount,
            TargetSwitchRecoveryFrames);
    }

    private void BeginCameraInactiveRecoveryWindow()
    {
        if (!_hasPose || _cameraInactiveGraceStarted)
        {
            return;
        }

        _cameraInactiveGraceStarted = true;
        _cameraInactiveGraceUntilFrame = SpectatorFreecamRecoveryPolicy.ExtendGraceUntilFrame(
            Time.frameCount,
            CameraInactiveRecoveryFrames);
    }

    private bool TrySoftPause(SpectatorFreecamIneligibleReason reason, GameSpectatorSnapshot snapshot)
    {
        SpectatorFreecamRecoveryAction action = SpectatorFreecamRecoveryPolicy.GetIneligibleAction(
            reason,
            Time.frameCount,
            _hasPose,
            _targetSwitchGraceUntilFrame,
            _cameraInactiveGraceUntilFrame);

        if (action != SpectatorFreecamRecoveryAction.SoftPausePreservePose)
        {
            return false;
        }

        SoftPauseWithReason(reason, snapshot);
        return true;
    }

    private void SoftPauseWithReason(SpectatorFreecamIneligibleReason reason, GameSpectatorSnapshot snapshot)
    {
        _state.IsActive = false;
        _smoothVelocity = Vector3.zero;
        _lastSmoothingFrame = -1;
        SpectatorVanillaInputGuard.Clear();
        LogEligibilityBlocked(reason, snapshot, softPaused: true);
    }

    private void DeactivateWithReason(
        SpectatorFreecamIneligibleReason reason,
        GameSpectatorSnapshot snapshot,
        bool clearAnchor)
    {
        LogEligibilityBlocked(reason, snapshot, softPaused: false);
        Deactivate(clearAnchor);
    }

    private void LogEligibilityBlocked(
        SpectatorFreecamIneligibleReason reason,
        GameSpectatorSnapshot snapshot,
        bool softPaused)
    {
        if (!ModLog.IsDebugEnabled)
        {
            return;
        }

        if (Time.frameCount < _nextEligibilityDebugFrame && reason == _lastEligibilityDebugReason)
        {
            return;
        }

        _lastEligibilityDebugReason = reason;
        _nextEligibilityDebugFrame = Time.frameCount + 60;
        ModLog.Debug(
            $"Local spectator freecam {(softPaused ? "soft-paused" : "inactive")} reason={reason} "
            + $"frame={Time.frameCount} hasPose={_hasPose} userEnabled={_state.UserEnabled} "
            + $"hasTarget={snapshot.HasSpectatedTarget} hasCamera={snapshot.SpectateCamera != null} "
            + $"hasAnchor={snapshot.Anchor != null} activeCamera={snapshot.IsSpectateCameraActive} "
            + $"targetSlot={snapshot.SpectatedPlayerSlotId?.ToString() ?? "none"} "
            + $"targetClient={snapshot.SpectatedPlayerActualClientId?.ToString() ?? "none"}");
    }

    private void InitializePoseFromCamera(Camera camera, Transform anchor)
    {
        _state.Offset = camera.transform.position - anchor.position;
        ClampOffset();
        SetYawPitchFromRotation(camera.transform.rotation);
        _state.Rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        _state.RepresentationRotation = SpectatorThirdPersonCameraRules.ResolveRepresentationRotation(_state.Rotation);
        _smoothVelocity = Vector3.zero;
        _smoothedPosition = camera.transform.position;
        _hasSmoothedPosition = true;
        _lastSmoothingFrame = -1;
        _previousAnchorPosition = anchor.position;
        _hasPreviousAnchorPosition = true;
        _hasPose = true;
    }

    private void Recenter(Camera camera, Transform anchor)
    {
        float radius = _settings.FreecamRadius;
        float distance = radius > 0.01f ? Mathf.Min(4f, radius * 0.6f) : 0f;
        float height = radius > 0.01f ? Mathf.Min(1.2f, radius * 0.3f) : 0f;
        Vector3 direction = ResolveRecenterDirection(camera, anchor);
        _state.Offset = direction * distance + Vector3.up * height;
        ClampOffset();

        Vector3 cameraPosition = anchor.position + _state.Offset;
        Vector3 lookTarget = anchor.position + Vector3.up * Mathf.Min(0.6f, radius * 0.2f);
        Vector3 lookDirection = lookTarget - cameraPosition;
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            SetYawPitchFromRotation(Quaternion.LookRotation(lookDirection.normalized, Vector3.up));
        }

        _state.Rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        _state.RepresentationRotation = SpectatorThirdPersonCameraRules.ResolveRepresentationRotation(_state.Rotation);
        _smoothVelocity = Vector3.zero;
        _smoothedPosition = anchor.position + _state.Offset;
        _hasSmoothedPosition = true;
        _lastSmoothingFrame = -1;
        _previousAnchorPosition = anchor.position;
        _hasPreviousAnchorPosition = true;
        _hasPose = true;
    }

    private static Vector3 ResolveRecenterDirection(Camera camera, Transform anchor)
    {
        if (TryGetHorizontalDirection(camera.transform.position - anchor.position, out Vector3 direction))
        {
            return direction;
        }

        if (TryGetHorizontalDirection(-camera.transform.forward, out direction))
        {
            return direction;
        }

        if (TryGetHorizontalDirection(-anchor.forward, out direction))
        {
            return direction;
        }

        return Vector3.back;
    }

    private static bool TryGetHorizontalDirection(Vector3 source, out Vector3 direction)
    {
        source.y = 0f;
        float magnitude = source.magnitude;
        if (magnitude > 0.0001f)
        {
            direction = source / magnitude;
            return true;
        }

        direction = Vector3.zero;
        return false;
    }

    private void ApplyInput()
    {
        Vector2 lookDelta = _inputService.ReadLookDelta();
        _yaw += lookDelta.x * _settings.FreecamLookSensitivity;
        _pitch = Mathf.Clamp(_pitch - lookDelta.y * _settings.FreecamLookSensitivity, MinPitch, MaxPitch);

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 moveInput = _inputService.ReadMoveInput();
        if (moveInput.sqrMagnitude > 0f)
        {
            float multiplier = 1f;
            if (_inputService.FastMoveHeld)
            {
                multiplier *= _settings.FreecamFastMoveMultiplier;
            }

            if (_inputService.SlowMoveHeld)
            {
                multiplier *= _settings.FreecamSlowMoveMultiplier;
            }

            float speed = _settings.FreecamMoveSpeed * multiplier;
            Vector3 movement =
                rotation * Vector3.right * moveInput.x
                + Vector3.up * moveInput.y
                + rotation * Vector3.forward * moveInput.z;

            _state.Offset += movement * speed * Time.unscaledDeltaTime;
            ClampOffset();
        }

        _state.Rotation = rotation;
        _state.RepresentationRotation = SpectatorThirdPersonCameraRules.ResolveRepresentationRotation(_state.Rotation);
    }

    private void ApplyCameraTransform(Camera camera, Transform anchor)
    {
        Vector3 representationTarget = anchor.position + _state.Offset;
        Transform cameraTransform = camera.transform;

        if (Time.frameCount != _lastSmoothingFrame)
        {
            bool resetForAnchorTeleport = SpectatorCameraMotionCompensationRules.ShouldResetForAnchorDelta(
                _previousAnchorPosition,
                anchor.position,
                _hasPreviousAnchorPosition && _hasSmoothedPosition,
                AnchorTeleportThreshold);
            _smoothedPosition = SpectatorCameraMotionCompensationRules.ApplyAnchorTranslation(
                _smoothedPosition,
                _previousAnchorPosition,
                anchor.position,
                _hasPreviousAnchorPosition && _hasSmoothedPosition,
                AnchorTeleportThreshold);

            if (resetForAnchorTeleport)
            {
                _smoothedPosition = representationTarget;
                _hasSmoothedPosition = true;
                _smoothVelocity = Vector3.zero;
            }
            else if (_settings.FreecamSmoothTime > 0f && _state.IsActive && _hasSmoothedPosition)
            {
                _smoothedPosition = Vector3.SmoothDamp(
                    _smoothedPosition,
                    representationTarget,
                    ref _smoothVelocity,
                    _settings.FreecamSmoothTime,
                    float.PositiveInfinity,
                    Time.unscaledDeltaTime);
            }
            else
            {
                _smoothedPosition = representationTarget;
                _hasSmoothedPosition = true;
                _smoothVelocity = Vector3.zero;
            }

            _previousAnchorPosition = anchor.position;
            _hasPreviousAnchorPosition = true;
            _lastSmoothingFrame = Time.frameCount;
        }

        Vector3 renderedPosition = _smoothedPosition;
        Quaternion renderedRotation = _state.Rotation;
        if (_state.Mode == SpectatorCameraMode.ThirdPerson)
        {
            Vector3 desiredPosition = SpectatorThirdPersonCameraRules.ResolveDesiredCameraPosition(
                _smoothedPosition,
                _state.Rotation,
                _thirdPersonDistance,
                _settings.ThirdPersonHeight);
            renderedPosition = ResolveThirdPersonCollision(_smoothedPosition, desiredPosition);
            Vector3 lookDirection = _smoothedPosition - renderedPosition;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                renderedRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }

        cameraTransform.position = renderedPosition;
        cameraTransform.rotation = renderedRotation;
        _state.IsActive = true;
        _state.WorldPosition = _smoothedPosition;
        _state.RenderedWorldPosition = renderedPosition;
        _state.HasWorldPose = true;

        if (ModLog.IsDebugEnabled && Time.frameCount >= _nextApplyDebugFrame)
        {
            _nextApplyDebugFrame = Time.frameCount + 120;
            ModLog.Debug(
                $"Applied spectator camera frame={Time.frameCount} mode={_state.Mode} camera={camera.name} "
                + $"offset={_state.Offset} representation={_smoothedPosition} rendered={renderedPosition}");
        }
    }

    private static Vector3 ResolveThirdPersonCollision(Vector3 representationPosition, Vector3 desiredPosition)
    {
        Vector3 direction = desiredPosition - representationPosition;
        float distance = direction.magnitude;
        if (distance <= 0.0001f)
        {
            return desiredPosition;
        }

        direction /= distance;
        if (!Physics.Raycast(
                representationPosition,
                direction,
                out RaycastHit hit,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            return desiredPosition;
        }

        return hit.point - (direction * ThirdPersonCollisionPadding);
    }

    private void ClampOffset()
    {
        if (!_settings.ClampCameraToRadius)
        {
            return;
        }

        float radius = _settings.FreecamRadius;
        if (radius <= 0f)
        {
            _state.Offset = Vector3.zero;
            return;
        }

        float magnitude = _state.Offset.magnitude;
        if (magnitude > radius)
        {
            _state.Offset = _state.Offset / magnitude * radius;
        }
    }

    private void UpdateState(GameSpectatorSnapshot snapshot)
    {
        _state.TargetSlotId = snapshot.SpectatedPlayerSlotId;
        _state.TargetActualClientId = snapshot.SpectatedPlayerActualClientId;
    }

    private void SetYawPitchFromRotation(Quaternion rotation)
    {
        Vector3 euler = rotation.eulerAngles;
        _yaw = euler.y;
        _pitch = NormalizePitch(euler.x);
        _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
    }

    private static float NormalizePitch(float pitch)
    {
        return pitch > 180f ? pitch - 360f : pitch;
    }

    private void DisableAfterFailure(Exception ex)
    {
        ModLog.Error($"Enhanced spectator freecam failed and will fall back to vanilla camera: {ex}");
        _state.UserEnabled = false;
        Deactivate(clearAnchor: true);
    }

    private void UpdateVanillaInputGuard(bool enabled)
    {
        UpdateVanillaInputGuard(enabled, quickMenuBlocksInput: false);
    }

    private void UpdateVanillaInputGuard(bool enabled, bool quickMenuBlocksInput)
    {
        SpectatorVanillaInputGuard.Update(
            enabled,
            _settings.AscendKey,
            _settings.DescendKey,
            quickMenuBlocksInput);
    }

    private void LogQuickMenuInputBlocked()
    {
        if (!ModLog.IsDebugEnabled)
        {
            return;
        }

        if (Time.frameCount < _nextMenuBlockDebugFrame)
        {
            return;
        }

        _nextMenuBlockDebugFrame = Time.frameCount + 120;
        ModLog.Debug("Local spectator freecam input is paused while the quick menu is open.");
    }
}
