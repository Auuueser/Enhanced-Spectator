using System;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Features.SpectatorPresence;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Networking;
using EnhancedSpectator.Runtime;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Coordinates the local spectator freecam feature module.
/// </summary>
public sealed class SpectatorModule :
    IFeatureModule,
    ISpectatorStateService,
    ISpectatorTargetStateProvider,
    ISpectatorPoseStateProvider,
    IPeerIdentityStateProvider,
    IRuntimeTickable,
    IRuntimeLateTickable,
    IRuntimeCameraPreCullTickable
{
    private readonly IGameSpectatorAdapter _gameSpectatorAdapter;
    private readonly SpectatorSnapshotCache _snapshotCache;
    private readonly SpectatorFreecamController _freecamController;
    private bool _initialized;
    private bool _lastPoseHadShipMotionReference;
    private bool _centeringHeld, _untouchedSinceDeath, _wasDead;
    private int _seenCameraMove = -1;

    /// <summary>
    /// Creates a spectator module with a game adapter and freecam settings.
    /// </summary>
    public SpectatorModule(IGameSpectatorAdapter gameSpectatorAdapter, SpectatorFreecamSettings freecamSettings)
    {
        _gameSpectatorAdapter = gameSpectatorAdapter ?? throw new ArgumentNullException(nameof(gameSpectatorAdapter));
        SpectatorFreecamSettings settings = freecamSettings ?? throw new ArgumentNullException(nameof(freecamSettings));
        _snapshotCache = new SpectatorSnapshotCache(_gameSpectatorAdapter);
        SpectatorAnchorService anchorService = new SpectatorAnchorService();
        SpectatorInputService inputService = new SpectatorInputService(settings);
        _freecamController = new SpectatorFreecamController(
            _gameSpectatorAdapter,
            _snapshotCache,
            anchorService,
            inputService,
            settings);
        Current = SpectatorState.Unavailable;
    }

    /// <inheritdoc />
    public SpectatorState Current { get; private set; }

    /// <summary>
    /// Gets the current local enhanced spectator camera and representation state.
    /// </summary>
    public SpectatorCameraState CameraState => _freecamController.State;

    /// <inheritdoc />
    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        Current = _gameSpectatorAdapter.ReadSpectatorState();
        SpectatorLifecycleEvents.Changed += OnSpectatorLifecycleChanged;
        _initialized = true;
        ModLog.Debug("Spectator freecam module initialized.");
    }

    /// <inheritdoc />
    public void Refresh()
    {
        if (!_initialized)
        {
            return;
        }

        if (_snapshotCache.TryGetCurrentFrameSnapshot(out GameSpectatorSnapshot snapshot))
        {
            Current = new SpectatorState(
                true,
                snapshot.IsLocalPlayerDead ? "Local spectator state is available." : "Local player is not spectating.",
                snapshot.IsLocalPlayerDead,
                _freecamController.State.IsActive && !_freecamController.IsPreview);
            return;
        }

        Current = SpectatorState.Unavailable;
    }

    /// <inheritdoc />
    public bool TryGetCurrentSpectatorTarget(out SpectatorTargetState state)
    {
        if (_initialized
            && _snapshotCache.TryGetCurrentFrameSnapshot(out GameSpectatorSnapshot snapshot)
            && snapshot.HasRound
            && snapshot.HasLocalPlayer
            && snapshot.LocalPlayerSlotId.HasValue
            && snapshot.LocalPlayerActualClientId.HasValue)
        {
            bool isLocalSpectating = snapshot.IsLocalPlayerDead;
            state = new SpectatorTargetState(
                isLocalSpectating,
                snapshot.LocalPlayerActualClientId.Value,
                snapshot.LocalPlayerSlotId.Value,
                isLocalSpectating && snapshot.HasSpectatedTarget ? snapshot.SpectatedPlayerActualClientId : null,
                isLocalSpectating && snapshot.HasSpectatedTarget ? snapshot.SpectatedPlayerSlotId : null,
                DateTime.UtcNow.Ticks);
            return true;
        }

        state = new SpectatorTargetState(false, 0, 0, null, null, DateTime.UtcNow.Ticks);
        return false;
    }

    /// <inheritdoc />
    public bool TryGetCurrentSpectatorPose(out SpectatorPoseState state)
    {
        if (!_freecamController.IsPreview && _initialized
            && _snapshotCache.TryGetCurrentFrameSnapshot(out GameSpectatorSnapshot snapshot)
            && snapshot.HasRound
            && snapshot.HasLocalPlayer
            && snapshot.LocalPlayerSlotId.HasValue
            && snapshot.LocalPlayerActualClientId.HasValue)
        {
            SpectatorSplitView splitView = SplitScreen.SplitScreenModule.LocalView;
            bool useFreecamPose = SpectatorPoseSourceRules.ShouldUseFreecamPose(
                _freecamController.State.IsActive,
                _freecamController.State.HasWorldPose);
            bool useVanillaSpectatorPose = !useFreecamPose
                && SpectatorPoseSourceRules.ShouldUseVanillaSpectatorPose(
                    snapshot.SpectateCamera != null,
                    snapshot.IsSpectateCameraActive);
            bool hasPose = SpectatorPoseSourceRules.ShouldPublishSpectatorPose(
                snapshot.IsLocalPlayerDead,
                snapshot.HasSpectatedTarget,
                useFreecamPose,
                useVanillaSpectatorPose);
            // Poses are sampled less often than every frame: a camera movement since the last sample counts.
            bool moved = _freecamController.CameraMovedFrame > _seenCameraMove;
            _seenCameraMove = _freecamController.CameraMovedFrame;
            // A spectating life starts centred (hidden from others), until the camera is first moved.
            if (snapshot.IsLocalPlayerDead && !_wasDead) _centeringHeld = _untouchedSinceDeath = true;
            _wasDead = snapshot.IsLocalPlayerDead;
            _untouchedSinceDeath &= !moved;
            // A moment without a pose (the watched player switching) keeps what was held.
            if (hasPose)
                _centeringHeld = SpectatorAutoCenter.HoldCentering(_centeringHeld,
                    useFreecamPose ? _freecamController.IsAutoCentering : useVanillaSpectatorPose && _freecamController.IsVanillaIdle,
                    splitView != SpectatorSplitView.None || _untouchedSinceDeath, moved);
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            Vector3 cameraPosition = Vector3.zero;
            Quaternion cameraRotation = Quaternion.identity;
            float cameraFov = 0;
            if (hasPose && useFreecamPose)
            {
                position = _freecamController.State.WorldPosition;
                rotation = _freecamController.State.RepresentationRotation;
                cameraPosition = _freecamController.State.RenderedWorldPosition;
                cameraRotation = _freecamController.State.RenderedWorldRotation;
                cameraFov = _freecamController.State.RenderedFieldOfView;
            }
            else if (hasPose && snapshot.SpectateCamera != null)
            {
                Transform cameraTransform = snapshot.SpectateCamera.transform;
                position = cameraTransform.position;
                rotation = cameraTransform.rotation;
                cameraPosition = position; cameraRotation = rotation; cameraFov = snapshot.SpectateCamera.fieldOfView;
            }

            bool isInsideShipBounds = hasPose
                && _gameSpectatorAdapter.IsWorldPositionInsideShip(position);
            bool shipIsLeaving = _gameSpectatorAdapter is IGameShipMotionStateAdapter shipMotionState
                && shipMotionState.IsShipLeaving();
            bool shouldCaptureShipReference = hasPose
                && ShipMotionReferenceCaptureRules.ShouldCapture(
                    isInsideShipBounds,
                    shipIsLeaving,
                    _lastPoseHadShipMotionReference);
            SpectatorMotionReferencePose motionReference = default;
            bool hasMotionReference = shouldCaptureShipReference
                && _gameSpectatorAdapter.TryGetShipMotionReference(out motionReference);
            _lastPoseHadShipMotionReference = hasMotionReference;
            Vector3 motionReferenceLocalPosition = hasMotionReference
                ? RemoteSpectatorMotionCompensationRules.CaptureLocalPosition(position, motionReference)
                : Vector3.zero;
            Quaternion motionReferenceLocalRotation = hasMotionReference
                ? RemoteSpectatorMotionCompensationRules.CaptureLocalRotation(rotation, motionReference)
                : Quaternion.identity;
            SpectatorMotionReferencePose targetMotionReference = default;
            bool hasTargetMotionReference = SpectatorPoseSourceRules.ShouldCaptureTargetMotionReference(
                    hasPose,
                    snapshot.HasSpectatedTarget)
                && _gameSpectatorAdapter is IGameSpectatedTargetMotionReferenceAdapter targetMotionReferenceAdapter
                && targetMotionReferenceAdapter.TryGetSpectatedTargetMotionReference(
                    snapshot.SpectatedPlayerActualClientId,
                    snapshot.SpectatedPlayerSlotId,
                    out targetMotionReference);
            Vector3 targetMotionReferenceLocalPosition = hasTargetMotionReference
                ? RemoteSpectatorMotionCompensationRules.CaptureLocalPosition(position, targetMotionReference)
                : Vector3.zero;
            Quaternion targetMotionReferenceLocalRotation = hasTargetMotionReference
                ? RemoteSpectatorMotionCompensationRules.CaptureLocalRotation(rotation, targetMotionReference)
                : Quaternion.identity;

            state = new SpectatorPoseState(
                hasPose,
                snapshot.LocalPlayerActualClientId.Value,
                snapshot.LocalPlayerSlotId.Value,
                hasPose ? snapshot.SpectatedPlayerActualClientId : null,
                hasPose ? snapshot.SpectatedPlayerSlotId : null,
                position,
                rotation,
                DateTime.UtcNow.Ticks,
                hasMotionReference,
                motionReferenceLocalPosition,
                motionReferenceLocalRotation,
                hasTargetMotionReference,
                targetMotionReferenceLocalPosition,
                targetMotionReferenceLocalRotation,
                // In the split-screen audience (every view tiled) the model is stowed for older versions too.
                modelStowed: hasPose && (useFreecamPose && _freecamController.State.ModelStowed || splitView == SpectatorSplitView.Audience),
                // The vanilla spectate camera (normal spectating, split-screen tiles and the vanilla large view) keeps the
                // game's view: its ghost stays visible like any other, and counts as centred (hidden, translucent while
                // speaking) only once the player has left it alone for the idle wait. No turn or height is applied.
                autoCentering: hasPose && _centeringHeld,
                splitView: splitView,
                followingClientId: SplitScreen.SplitScreenModule.LocalFollowing,
                firstPersonView: hasPose && useFreecamPose && _freecamController.State.ModelStowed && _freecamController.State.Mode == SpectatorCameraMode.FirstPerson,
                // The camera this pose comes through, for a follower watching together to take the same one.
                cameraMode: hasPose && useFreecamPose ? (byte)(_freecamController.State.Mode + 1) : (byte)0,
                cameraStyle: (byte)_freecamController.EffectiveMonitorStyle,
                hasCameraPose: hasPose,
                cameraLocalPosition: Quaternion.Inverse(rotation) * (cameraPosition - position),
                cameraLocalRotation: Quaternion.Inverse(rotation) * cameraRotation,
                cameraFieldOfView: cameraFov);
            return true;
        }

        state = new SpectatorPoseState(false, 0, 0, null, null, Vector3.zero, Quaternion.identity, DateTime.UtcNow.Ticks);
        return false;
    }

    /// <inheritdoc />
    public bool TryGetLocalPeerIdentity(out PeerIdentityState state)
    {
        if (_initialized
            && _gameSpectatorAdapter.TryGetLocalPlayerIdentity(out ulong clientId, out ulong slotId)
            && _gameSpectatorAdapter.TryGetPlayerDisplayName(clientId, slotId, out string displayName))
        {
            string voicePlayerName = _gameSpectatorAdapter.TryGetLocalVoicePlayerName(out string localVoicePlayerName)
                ? localVoicePlayerName
                : string.Empty;
            state = new PeerIdentityState(clientId, slotId, displayName, voicePlayerName, DateTime.UtcNow.Ticks);
            return true;
        }

        state = new PeerIdentityState(0, 0, string.Empty, DateTime.UtcNow.Ticks);
        return false;
    }

    /// <inheritdoc />
    public void Tick()
    {
        if (!_initialized)
        {
            return;
        }

        _freecamController.Tick();
    }

    /// <inheritdoc />
    public void LateTick()
    {
        if (!_initialized)
        {
            return;
        }

        _freecamController.LateTick();
        Refresh();
    }

    /// <inheritdoc />
    public void CameraPreCullTick(Camera camera)
    {
        if (!_initialized)
        {
            return;
        }

        _freecamController.CameraPreCullTick(camera);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        _freecamController.Dispose();
        _initialized = false;
        SpectatorLifecycleEvents.Changed -= OnSpectatorLifecycleChanged;
        Current = SpectatorState.Unavailable;
        _lastPoseHadShipMotionReference = false;
        _snapshotCache.Clear();
        ModLog.Debug("Spectator freecam module disposed.");
    }

    private void OnSpectatorLifecycleChanged(SpectatorLifecycleEventKind kind)
    {
        _snapshotCache.Clear();
        _lastPoseHadShipMotionReference = false;
        _freecamController.NotifyLifecycleEvent(kind);
    }
}
