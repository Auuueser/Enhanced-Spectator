using System;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Runtime;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Applies local enhanced spectator freecam movement before the spectator camera renders.
/// </summary>
public sealed partial class SpectatorFreecamController
{
    private const float MaxPitch = 85f;
    private const float MinPitch = -85f;
    private const int TargetSwitchRecoveryFrames = 4;
    private const int CameraInactiveRecoveryFrames = 3;
    private const float AnchorTeleportThreshold = 12f;

    private IGameSpectatorAdapter _adapter;
    private readonly IGameSpectatorAdapter _productionAdapter;
    private readonly SpectatorSnapshotCache _snapshotCache;
    private readonly SpectatorAnchorService _anchorService;
    private readonly SpectatorInputService _inputService;
    private readonly SpectatorFreecamSettings _settings;
    private readonly SpectatorCameraState _state = new SpectatorCameraState();

    private bool _wasSpectating;
    private bool _hasPose;
    private bool _recenterRequested;
    private bool _entryRecenterPending;
    private float _yaw;
    private float _pitch;
    private Vector3 _smoothVelocity;
    private Vector3 _smoothedPosition;
    private bool _hasSmoothedPosition;
    private Vector3 _previousAnchorPosition;
    private bool _hasPreviousAnchorPosition;
    private int _lastSmoothingFrame = -1;
    private int _nextApplyDebugFrame;
    private int _nextMenuBlockDebugFrame;
    private int _targetSwitchGraceUntilFrame = -1;
    private int _cameraInactiveGraceUntilFrame = -1;
    private int _nextEligibilityDebugFrame;
    private SpectatorFreecamIneligibleReason _lastEligibilityDebugReason = SpectatorFreecamIneligibleReason.None;
    private bool _cameraInactiveGraceStarted;

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
        Current = this;
        _adapter = adapter;
        _productionAdapter = adapter;
        _snapshotCache = snapshotCache ?? throw new ArgumentNullException(nameof(snapshotCache));
        _anchorService = anchorService;
        _inputService = inputService;
        _settings = settings;
        _state.UserEnabled = settings.FreecamDefaultOn;
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
                if (_deathHandoffPhase == DeathHandoffPhase.None) _hasPose = false;
                _cameraInactiveGraceUntilFrame = SpectatorFreecamRecoveryPolicy.ExtendGraceUntilFrame(
                    Time.frameCount,
                    CameraInactiveRecoveryFrames);
                break;
            case SpectatorLifecycleEventKind.SpectatedPlayerEffectsApplied:
                BeginTargetSwitchRecoveryWindow();
                if (_settings.RecenterOnTargetSwitch && _deathHandoffPhase == DeathHandoffPhase.None)
                {
                    _recenterRequested = true;
                }

                break;
            case SpectatorLifecycleEventKind.GameOverOverrideChanged:
                ClearDeathHandoff();
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
            // Vanilla spectating is driven by the mouse (orbit, wheel distance, clicks to switch teammate); keys are left
            // out so push-to-talk keeps an idle ghost centred (translucent while speaking) instead of revealing it. A
            // menu, chat, another window or a free cursor means the mouse is busy elsewhere, not with the view.
            bool active = !CameraInputBlocked && _inputService.HasCameraMouseActivity;
            if (active) CameraMovedFrame = Time.frameCount;
            _vanillaIdle.Update(UsesVanillaView, active, Time.unscaledDeltaTime);
            if (_splitScreenHandledInputFrame == Time.frameCount || Mirrored) return;
            if (_splitScreenActive && !_splitScreenFocused) return;
            if (!IsPreview && !TryGetEligibleSnapshot(out _))
            {
                SpectatorVanillaInputGuard.Clear();
                return;
            }

            bool quickMenuOpen = CameraInputBlocked;
            UpdateVanillaInputGuard(enabled: _state.UserEnabled, quickMenuBlocksInput: quickMenuOpen);

            bool modeInputBlocked = _splitScreenActive && _adapter is IGameSpectatorModeInputAdapter modeInput
                ? modeInput.IsViewModeInputBlocked() : quickMenuOpen;
            if (modeInputBlocked)
            {
                LogQuickMenuInputBlocked();
                return;
            }

            if (_inputService.ResetToVanillaPressed)
            {
                if (_splitScreenActive) _splitScreenMode = null;
                _monitorPreference.Select(SpectatorCameraMode.Freecam);
                _state.UserEnabled = false;
                Deactivate(clearAnchor: false);
                ModLog.Debug("Enhanced spectator freecam disabled until toggled again.");
                return;
            }

            if (_inputService.ToggleFreecamPressed && _settings.EnableFreecam)
            {
                if (_state.UserEnabled && _state.Mode == SpectatorCameraMode.Freecam) ReturnToVanilla();
                else SelectMode(SpectatorCameraMode.Freecam);
            }

            if (_inputService.ToggleThirdPersonPressed && _settings.EnableThirdPerson)
                SelectMode(SpectatorCameraRules.ResolveToggleMode(_state.Mode, SpectatorCameraMode.ThirdPerson, _state.UserEnabled));
            if (SpectatorInputService.IsKeyPressedThisFrame(_settings.Camera.FirstPersonKey.Value))
                SelectMode(SpectatorCameraRules.ResolveToggleMode(_state.Mode, SpectatorCameraMode.FirstPerson, _state.UserEnabled));
            if (SpectatorInputService.IsKeyPressedThisFrame(_settings.Camera.CinematicKey.Value))
                SelectMode(SpectatorCameraRules.ResolveToggleMode(_state.Mode, SpectatorCameraMode.Cinematic, _state.UserEnabled));
            if (SpectatorInputService.IsKeyPressedThisFrame(_settings.Camera.MonitorKey.Value) && (CanUseMonitorView || _monitorPreference.Selected))
                SelectMode(SpectatorCameraRules.ResolveToggleMode(_monitorPreference.Selected ? SpectatorCameraMode.Monitor : _state.Mode, SpectatorCameraMode.Monitor, _state.UserEnabled));

            if (_state.UserEnabled && (_state.Mode == SpectatorCameraMode.Cinematic || _state.Mode == SpectatorCameraMode.Monitor))
            {
                int direction = CinematicStyles.InputDirection(
                    SpectatorInputService.IsKeyPressedThisFrame(KeyCode.LeftArrow),
                    SpectatorInputService.IsKeyPressedThisFrame(KeyCode.RightArrow));
                if (direction != 0 && _state.Mode == SpectatorCameraMode.Monitor)
                    SelectMonitorStyle(MonitorCameraStyles.Cycle(EffectiveMonitorStyle, direction));
                else if (direction != 0) _settings.Camera.CinematicStyle.Value = CinematicStyles.Cycle(_settings.Camera.CinematicStyle.Value, direction);
            }

            if(SpectatorInputService.IsKeyPressedThisFrame(_settings.Camera.MonitorInfraredKey.Value))
                _settings.Camera.MonitorInfrared.Value=!_settings.Camera.MonitorInfrared.Value;
            CycleThermalPalette(_settings.Camera);

            float scrollDelta = _inputService.ReadScrollDelta();
            if (!Mathf.Approximately(scrollDelta, 0f))
            {
                // Changing the distance is operating the camera: recentering waits again before it backs off.
                _autoCenter.Touch();
                var action = SpectatorCameraRules.WheelAction(_state.UserEnabled, _state.Mode,
                    SpectatorInputService.IsKeyHeld(_settings.Camera.SelfCameraDistanceModifier.Value));
                if (action == SpectatorWheelAction.VanillaDistance)
                    _vanillaDistance = SpectatorCameraRules.AdjustDistance(_vanillaDistance, scrollDelta, .5f, _settings.FreecamRadius);
                else if (action == SpectatorWheelAction.SelfCameraDistance)
                    _settings.SetThirdPersonDistance(SpectatorCameraRules.AdjustDistance(_settings.ThirdPersonDistance, scrollDelta, 1.5f, SpectatorCameraRules.MaximumFollowDistance));
                else if (action == SpectatorWheelAction.CinematicDistance)
                    _settings.Camera.CinematicDistance.Value = SpectatorCameraRules.AdjustDistance(_settings.Camera.CinematicDistance.Value, scrollDelta, 1.5f, SpectatorCameraRules.MaximumFollowDistance);
                else if (action == SpectatorWheelAction.TargetDistance)
                {
                    float distance = SpectatorCameraRules.AdjustDistance(_state.Offset.magnitude, scrollDelta, 0.5f, _settings.FreecamRadius);
                    _settings.Camera.FreecamDistance.Value = distance;
                    ApplyFreecamDistance(distance);
                }
            }

            if (_inputService.RecenterPressed) RecenterView();
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
        if (!_splitScreenActive) EvaluateCameraPose();
    }

    internal void EvaluateSplitScreenPose() => EvaluateCameraPose();

    /// <summary>While thermal is on, Up and Down step through its colour schemes.</summary>
    internal static void CycleThermalPalette(Config.SpectatorCameraConfig camera)
    {
        if (!camera.MonitorInfrared.Value) return;
        int step = SpectatorInputService.IsKeyPressedThisFrame(KeyCode.UpArrow) ? 1 : SpectatorInputService.IsKeyPressedThisFrame(KeyCode.DownArrow) ? -1 : 0;
        if (step != 0) camera.ThermalPalette.Value = ThermalPalettes.Cycle(camera.ThermalPalette.Value, step);
    }

    private void EvaluateCameraPose()
    {
        try
        {
            if (_splitScreenActive && !CanRenderSplitScreenMode) { SuspendSplitScreenView(); return; }
            if (IsPreview) { ApplyPreviewPose(); return; }
            if (_splitScreenActive && (!_state.UserEnabled || _state.Mode != _splitScreenMode.GetValueOrDefault()))
                SetCameraMode(_splitScreenMode.GetValueOrDefault(), false);
            if (!TryGetEligibleSnapshot(out GameSpectatorSnapshot snapshot))
            {
                return;
            }

            if (!_splitScreenActive) UpdateAutomaticMonitor();
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
            bool deathHandoff = TryStartDeathHandoff(snapshot, camera, anchor);
            if (targetChanged && !deathHandoff) ResetFollowHistory();
            if (!_hasPose)
            {
                InitializePoseFromCamera(camera, anchor);
            }

            if (!deathHandoff && (_recenterRequested || (targetChanged && _settings.RecenterOnTargetSwitch)))
            {
                Recenter(camera, anchor);
                _recenterRequested = false;
            }

            bool quickMenuOpen = CameraInputBlocked;
            UpdateVanillaInputGuard(enabled: _state.UserEnabled, quickMenuBlocksInput: quickMenuOpen);
            if (quickMenuOpen)
            {
                LogQuickMenuInputBlocked();
            }
            else
            {
                if (_state.Mode == SpectatorCameraMode.Freecam || _state.Mode == SpectatorCameraMode.ThirdPerson) ApplyInput();
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
            if (_splitScreenActive)
            {
                if (SplitScreenCameraContext.IsPrimary(renderingCamera) && CanRenderSplitScreenMode && UsesFirstPersonRendering)
                    LethalCompanyFirstPersonVisibility.HideForSpectatorCamera(renderingCamera);
                return;
            }
            if (IsPreview)
            {
                if (renderingCamera == _previewCamera && CanRenderSplitScreenMode)
                {
                    ApplyPreviewPose();
                    if (_state.Mode == SpectatorCameraMode.FirstPerson || (_state.Mode == SpectatorCameraMode.Monitor && _monitorEyeFallback))
                        LethalCompanyFirstPersonVisibility.HideForSpectatorCamera(renderingCamera);
                }
                return;
            }
            if(renderingCamera!=null)
            {
                string? interior=SpectatorInteriorVisibility.BeforeCamera(renderingCamera,_settings.Camera.DebugMonitorCamera.Value);
                if(interior!=null) ModLog.Info(interior);
            }
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

            if (!_anchorService.TryUpdate(snapshot, out Transform? anchor, out bool targetChanged) || anchor == null)
            {
                if (!TrySoftPause(SpectatorFreecamIneligibleReason.MissingCameraAnchorOrTarget, snapshot))
                {
                    DeactivateWithReason(SpectatorFreecamIneligibleReason.MissingCameraAnchorOrTarget, snapshot, clearAnchor: true);
                }

                return;
            }

            bool deathHandoff = TryStartDeathHandoff(snapshot, spectateCamera, anchor);
            if (targetChanged && !deathHandoff) ResetFollowHistory();
            if (targetChanged && !deathHandoff && _settings.RecenterOnTargetSwitch)
            {
                Recenter(spectateCamera, anchor);
            }

            ApplyCameraTransform(spectateCamera, anchor);
            if (_state.Mode == SpectatorCameraMode.FirstPerson || (_state.Mode == SpectatorCameraMode.Monitor && _monitorEyeFallback)) LethalCompanyFirstPersonVisibility.HideForSpectatorCamera(spectateCamera);
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
            if (HoldDeathHandoffCamera(snapshot)) return false;
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
        _monitorPreference.Clear();
        _vanillaDistance = 1.3f;
        _entryRecenterPending = true;
        _wasSpectating = true;
        _state.UserEnabled = _settings.EnableFreecam && _settings.FreecamDefaultOn;
        _state.Mode = SpectatorCameraMode.Freecam;
        _state.IsActive = false;
        _hasPose = false;
        _recenterRequested = _state.UserEnabled;
        _smoothVelocity = Vector3.zero;
        _hasSmoothedPosition = false;
        _hasPreviousAnchorPosition = false;
        _lastSmoothingFrame = -1;
        _targetSwitchGraceUntilFrame = -1;
        _cameraInactiveGraceUntilFrame = -1;
        _cameraInactiveGraceStarted = false;
        ModLog.Debug("Entered local spectator state.");
    }

    private void ResetForNonSpectator()
    {
        ClearDeathHandoff();
        _lastLivingTarget = default;
        _monitorPreference.Clear();
        ClearFollowRig();
        RestoreCamera();
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
        _targetSwitchGraceUntilFrame = -1;
        _cameraInactiveGraceUntilFrame = -1;
        _cameraInactiveGraceStarted = false;
        SpectatorVanillaInputGuard.Clear();
        _anchorService.Clear();
        _state.IsActive = false;
        _state.UserEnabled = _settings.EnableFreecam && _settings.FreecamDefaultOn;
        _state.Mode = SpectatorCameraMode.Freecam;
        _state.TargetSlotId = null;
        _state.TargetActualClientId = null;
        _state.Offset = Vector3.zero;
        _state.Rotation = Quaternion.identity;
        _state.RepresentationRotation = Quaternion.identity;
        _state.WorldPosition = Vector3.zero;
        _state.RenderedWorldPosition = Vector3.zero;
        _state.RenderedWorldRotation = Quaternion.identity; _state.RenderedFieldOfView = 0;
        _state.HasWorldPose = false;
        _hasPreviousAnchorPosition = false;
        if (hadState)
        {
            ModLog.Debug("Reset local spectator freecam state.");
        }
    }

    private void Deactivate(bool clearAnchor)
    {
        RestoreCamera();
        bool wasActive = _state.IsActive;
        _state.IsActive = false;
        _smoothVelocity = Vector3.zero;
        _lastSmoothingFrame = -1;
        SpectatorVanillaInputGuard.Clear();

        if (clearAnchor)
        {
            ClearDeathHandoff();
            _lastLivingTarget = default;
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
            _state.RenderedWorldRotation = Quaternion.identity; _state.RenderedFieldOfView = 0;
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
        bool entering = _entryRecenterPending;
        _entryRecenterPending = false;
        float distance = entering ? SpectatorCameraRules.EntryDistance(radius) : Mathf.Min(_state.Offset.magnitude, radius);
        float height = radius > 0.01f ? Mathf.Min(1.2f, radius * 0.3f) : 0f;
        Vector3 direction = ResolveRecenterDirection(camera, anchor);
        // 0.3.1 entry: horizontal 4m + vertical 1.2m. Radius is only an upper bound.
        _state.Offset = entering ? direction * distance + Vector3.up * height
            : (direction * distance + Vector3.up * height).normalized * distance;
        if (entering) _lastConfiguredFreeDistance = _settings.Camera.FreecamDistance.Value;
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
        Vector2 lookDelta = Mirrored ? Vector2.zero : _inputService.ReadLookDelta();
        if (_settings.Camera.AutoCenter.Value && _autoCenterFrame >= 0 && lookDelta.sqrMagnitude > .0001f
            && _state.Mode == SpectatorCameraMode.Freecam)
            SetYawPitchFromRotation(_autoCenterRotation);
        _yaw += lookDelta.x * _settings.FreecamLookSensitivity;
        _pitch = Mathf.Clamp(_pitch - lookDelta.y * _settings.FreecamLookSensitivity, MinPitch, MaxPitch);

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 moveInput = Mirrored ? Vector3.zero : _inputService.ReadMoveInput();
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
            Quaternion movementRotation = _settings.Camera.AutoCenter.Value && _autoCenterFrame >= 0 ? _autoCenterRotation : rotation;
            Vector3 movement =
                movementRotation * Vector3.right * moveInput.x
                + Vector3.up * moveInput.y
                + movementRotation * Vector3.forward * moveInput.z;

            _state.Offset += movement * speed * Time.unscaledDeltaTime;
            ClampOffset();
        }

        _state.Rotation = rotation;
        _state.RepresentationRotation = SpectatorThirdPersonCameraRules.ResolveRepresentationRotation(_state.Rotation);
    }

    private void ApplyCameraTransform(Camera camera, Transform anchor)
    {
        SpectatorCameraMode effectiveMode=_monitorPreference.Resolve(_state.Mode,_state.UserEnabled,CanUseMonitorView);
        if(effectiveMode!=_state.Mode) SetCameraMode(effectiveMode,false);
        if ((_state.Mode == SpectatorCameraMode.Freecam || _state.Mode == SpectatorCameraMode.ThirdPerson)
            && _lastConfiguredFreeDistance != _settings.Camera.FreecamDistance.Value)
            ApplyFreecamDistance(_settings.Camera.FreecamDistance.Value);
        Vector3 representationTarget = anchor.position + _state.Offset;
        if (_state.Mode == SpectatorCameraMode.FirstPerson || _state.Mode == SpectatorCameraMode.Cinematic
            || _state.Mode == SpectatorCameraMode.Monitor)
        {
            if (!_followingReferenceCaptured)
            {
                _followingLocalOffset = anchor.InverseTransformPoint(representationTarget);
                _followingReferenceCaptured = true;
            }
            representationTarget = anchor.TransformPoint(_followingLocalOffset);
            _state.Offset = representationTarget - anchor.position;
        }
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
                _hasCinematicPose = false;
                _monitorDirector.Clear();
                ClearFollowRig();
                (_adapter as IGameMonitorCameraAdapter)?.ClearMonitorRoom();
                _snapCinematicPose = true;
                _collisionDistance = -1f;
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
            Vector3 focus = _smoothedPosition + (_state.HasLocalModelBounds ? _state.RepresentationRotation * _state.LocalModelCenter : Vector3.zero);
            float framingDistance = SpectatorCameraRules.FramingDistance(_settings.ThirdPersonDistance,
                _state.HasLocalModelBounds ? _state.LocalModelRadius : 0f);
            Vector3 desiredPosition = SpectatorThirdPersonCameraRules.ResolveDesiredCameraPosition(
                focus, _state.Rotation, framingDistance, _settings.ThirdPersonHeight);
            renderedPosition = ResolveThirdPersonCollision(focus, desiredPosition);
            Vector3 lookDirection = focus - renderedPosition;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                renderedRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }

        // Filter only the target translation; preserve manual position/rotation and ghost/voice pose.
        if (_state.Mode == SpectatorCameraMode.Freecam || _state.Mode == SpectatorCameraMode.ThirdPerson)
        {
            Vector3 correction = FollowAnchor(anchor) - anchor.position;
            if (_state.Mode == SpectatorCameraMode.ThirdPerson && _settings.Camera.RetractAtWalls.Value
                && _adapter is IGameMonitorCameraAdapter geometry && !geometry.IsMonitorPathClear(renderedPosition,renderedPosition+correction))
                correction=Vector3.zero;
            renderedPosition += correction;
        }
        ApplySelectedView(camera, anchor, ref renderedPosition, ref renderedRotation);
        bool autoView = _state.Mode == SpectatorCameraMode.Freecam || _state.Mode == SpectatorCameraMode.ThirdPerson
            || (_state.Mode == SpectatorCameraMode.Cinematic && _settings.Camera.CinematicStyle.Value == 11);
        if (autoView && (_settings.Camera.AutoCenter.Value || _state.Mode == SpectatorCameraMode.Cinematic))
        {
            if (_autoCenterFrame != Time.frameCount)
            {
                Vector2 look = CameraInputBlocked ? Vector2.zero : _inputService.ReadLookDelta() * _settings.FreecamLookSensitivity;
                // A menu, chat, another window or a free cursor (ours or another mod's) zero `look`: the idle time keeps
                // counting, so a player busy elsewhere recentres (and hides) like one who simply stopped moving the mouse.
                _autoCenterRotation = _autoCenter.Update(renderedRotation, _settings.Camera.AutoCenter.Value ? FollowAnchor(anchor) + Vector3.up * SpectatorAutoCenter.HeadHeight - renderedPosition : Vector3.zero,
                    look, Time.unscaledDeltaTime, true, _settings.Camera.FollowSpeed.Value, _state.Mode != SpectatorCameraMode.Cinematic);
                _autoCenterFrame = Time.frameCount;
                // Third person: the camera orbits the ghost by its rotation, so centering turns that orbit; turning only
                // the view left the ghost off to one side, and later mouse input continued from the old orbit.
                if (_state.Mode == SpectatorCameraMode.ThirdPerson && _autoCenter.IsCentering)
                {
                    SetYawPitchFromRotation(_autoCenterRotation);
                    _state.Rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                    _state.RepresentationRotation = SpectatorThirdPersonCameraRules.ResolveRepresentationRotation(_state.Rotation);
                }
                // The free and third-person ghosts also rise above the teammate's head, and back off to the spectate
                // distance when closer (takes effect next frame).
                if (_settings.Camera.AutoCenter.Value && _state.Mode != SpectatorCameraMode.Cinematic)
                {
                    var offset = _state.Offset;
                    offset.y = _autoCenter.EaseHeight(offset.y, Time.unscaledDeltaTime, _settings.Camera.FollowSpeed.Value);
                    // The default spectate distance: the wheel rewrites the configured one, so a camera scrolled in close
                    // would otherwise never back off.
                    _state.Offset = _autoCenter.EaseOutward(offset, Mathf.Min((float)_settings.Camera.FreecamDistance.DefaultValue, _settings.FreecamRadius),
                        -(_state.Rotation * Vector3.forward), Time.unscaledDeltaTime, _settings.Camera.FollowSpeed.Value);
                }
            }
            renderedRotation = _autoCenterRotation;
        }
        else { _autoCenter.Clear(); _autoCenterFrame = -1; }
        cameraTransform.position = renderedPosition;
        cameraTransform.rotation = renderedRotation;
        _state.IsActive = true;
        _state.WorldPosition = _smoothedPosition;
        _state.RenderedWorldPosition = renderedPosition;
        _state.RenderedWorldRotation = renderedRotation; _state.RenderedFieldOfView = camera.fieldOfView;
        _state.HasWorldPose = true;
        LethalCompanyCameraTransition.Tick();

        if (ModLog.IsDebugEnabled && Time.frameCount >= _nextApplyDebugFrame)
        {
            _nextApplyDebugFrame = Time.frameCount + 120;
            ModLog.Debug(
                $"Applied spectator camera frame={Time.frameCount} mode={_state.Mode} camera={camera.name} "
                + $"offset={_state.Offset} representation={_smoothedPosition} rendered={renderedPosition}");
        }
    }

    private Vector3 ResolveThirdPersonCollision(Vector3 representationPosition, Vector3 desiredPosition)
    {
        Vector3 direction = desiredPosition - representationPosition;
        float distance = direction.magnitude;
        if (distance <= 0.0001f) return desiredPosition;
        direction /= distance;
        return representationPosition + direction * ResolveSafeDistance(representationPosition, direction, distance);
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
        RememberLivingHandoffTarget(snapshot);
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
