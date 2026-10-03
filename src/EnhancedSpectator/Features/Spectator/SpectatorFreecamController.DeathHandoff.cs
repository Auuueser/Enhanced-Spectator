using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

public sealed partial class SpectatorFreecamController
{
    private enum DeathHandoffPhase { None, WaitingForVanilla, Ready }
    private DeathHandoffPhase _deathHandoffPhase;
    private SpectatorHandoffTarget _lastLivingTarget, _deadTarget, _deathReplacement;
    private Vector3 _deathCameraPosition;
    private Quaternion _deathCameraRotation;

    // Rendering reads the photographer's committed pose rather than the vanilla culler's old origin.
    internal bool TryGetTravellingRenderPosition(out Vector3 position)
    {
        position = _state.RenderedWorldPosition;
        return _state.UserEnabled && _state.Mode == SpectatorCameraMode.Monitor && EffectiveMonitorStyle == 2
            && _hasPose && _state.HasWorldPose && _monitorDirector.CanTransferTarget;
    }

    // Vanilla waits 1.5 seconds with an invalid watched target before its automatic replacement.
    // Keep the rendered pose through that interval; do not run the photographer against a dead subject.
    private bool CaptureDeathHandoff()
    {
        if (_deathHandoffPhase != DeathHandoffPhase.None) return true;
        if (!_state.UserEnabled || _state.Mode != SpectatorCameraMode.Monitor || EffectiveMonitorStyle != 2
            || !_hasPose || !_state.HasWorldPose || !_monitorDirector.CanTransferTarget
            || !(_adapter is IGameSpectatorDeathHandoffAdapter deaths)
            || !deaths.TryGetSpectatedHandoffTarget(out var target) || !target.IsDead
            || !_lastLivingTarget.IsValid || !_lastLivingTarget.IsIndoors
            || _lastLivingTarget.SlotId != target.SlotId || _lastLivingTarget.ClientId != target.ClientId) return false;

        // KillPlayer clears isInsideFactory, and dead-player transforms can be moved by vanilla.
        // The last living observation retains the actual watched scene and death location.
        _deadTarget = new SpectatorHandoffTarget(target.SlotId, target.ClientId,
            _lastLivingTarget.BodyPosition, true, _lastLivingTarget.IsIndoors, false);
        _deathCameraPosition = _state.RenderedWorldPosition;
        _deathCameraRotation = _monitorRotation;
        _deathHandoffPhase = DeathHandoffPhase.WaitingForVanilla;
        return true;
    }

    internal void BeginVanillaTargetSwitch(bool automatic)
    {
        if (automatic) CaptureDeathHandoff();
        else ClearDeathHandoff();
    }

    internal void EndVanillaTargetSwitch(bool automatic)
    {
        if (_deathHandoffPhase != DeathHandoffPhase.WaitingForVanilla) return;
        if (_adapter is IGameSpectatorDeathHandoffAdapter deaths
            && deaths.TryGetSpectatedHandoffTarget(out var next)
            && SpectatorDeathHandoffRules.ShouldTravel(automatic, _deadTarget.IsDead, _deadTarget.IsIndoors,
                next.IsIndoors, next.IsValid,
                next.ClientId != _deadTarget.ClientId || next.SlotId != _deadTarget.SlotId,
                (next.BodyPosition - _deadTarget.BodyPosition).sqrMagnitude))
        {
            _deathReplacement = next;
            _deathHandoffPhase = DeathHandoffPhase.Ready;
            _recenterRequested = false;
            _hasPose = true;
            _lastModeFrame = -1;
        }
        else ClearDeathHandoff();
    }

    private bool HoldDeathHandoffCamera(GameSpectatorSnapshot snapshot)
    {
        if (snapshot.IsGameOverOverride || snapshot.SpectateCamera == null || !CaptureDeathHandoff()) return false;
        if (_deathHandoffPhase != DeathHandoffPhase.WaitingForVanilla
            || !(_adapter is IGameSpectatorDeathHandoffAdapter deaths)
            || (deaths.TryGetSpectatedHandoffTarget(out var target) && (!target.IsDead
                || target.SlotId != _deadTarget.SlotId || target.ClientId != _deadTarget.ClientId)))
        {
            ClearDeathHandoff();
            return false;
        }

        snapshot.SpectateCamera.transform.SetPositionAndRotation(_deathCameraPosition, _deathCameraRotation);
        _state.IsActive = true;
        return true;
    }

    private bool TryStartDeathHandoff(GameSpectatorSnapshot snapshot, Camera camera, Transform anchor)
    {
        if (_deathHandoffPhase != DeathHandoffPhase.Ready) return false;
        ClearDeathHandoff();
        if (_state.Mode != SpectatorCameraMode.Monitor || EffectiveMonitorStyle != 2 || !CanUseMonitorView
            || snapshot.SpectatedPlayerSlotId != _deathReplacement.SlotId
            || snapshot.SpectatedPlayerActualClientId != _deathReplacement.ClientId) return false;

        ClearFollowRig();
        (_adapter as IGameMonitorCameraAdapter)?.ClearMonitorRoom();
        LethalCompanyFirstPersonVisibility.Clear();
        _monitorEyeFallback = false;
        _monitorRecoveryOpacity = 0;
        _followingReferenceCaptured = false;
        _hasPreviousAnchorPosition = false;
        _smoothVelocity = Vector3.zero;
        _smoothedPosition = _deathCameraPosition;
        _hasSmoothedPosition = true;
        _lastSmoothingFrame = -1;
        _lastModeFrame = -1;
        _state.Offset = _deathCameraPosition - anchor.position;
        _hasPose = true;
        _entryRecenterPending = false;
        _recenterRequested = false;
        _monitorDirector.BeginTargetTransfer(_deathCameraPosition, _deathCameraRotation,
            FollowAnchor(anchor) + Vector3.up * 1.05f);
        camera.transform.SetPositionAndRotation(_deathCameraPosition, _deathCameraRotation);
        ModLog.Debug($"Travelling death handoff: previous={_deadTarget.ClientId}/{_deadTarget.SlotId},next={_deathReplacement.ClientId}/{_deathReplacement.SlotId},subjectDistance={Vector3.Distance(_deadTarget.BodyPosition, _deathReplacement.BodyPosition):F2},camera={_deathCameraPosition:F2}.");
        return true;
    }

    private void ClearDeathHandoff() => _deathHandoffPhase = DeathHandoffPhase.None;

    private void RememberLivingHandoffTarget(GameSpectatorSnapshot snapshot)
    {
        if (_adapter is IGameSpectatorDeathHandoffAdapter deaths
            && deaths.TryGetSpectatedHandoffTarget(out var target) && target.IsValid
            && snapshot.SpectatedPlayerSlotId == target.SlotId
            && snapshot.SpectatedPlayerActualClientId == target.ClientId) _lastLivingTarget = target;
    }
}
