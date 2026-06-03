using System;
using EnhancedSpectator.GameInterop;
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
                _freecamController.State.IsActive);
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
        if (_initialized
            && _snapshotCache.TryGetCurrentFrameSnapshot(out GameSpectatorSnapshot snapshot)
            && snapshot.HasRound
            && snapshot.HasLocalPlayer
            && snapshot.LocalPlayerSlotId.HasValue
            && snapshot.LocalPlayerActualClientId.HasValue)
        {
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
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            if (hasPose && useFreecamPose)
            {
                position = _freecamController.State.WorldPosition;
                rotation = _freecamController.State.Rotation;
            }
            else if (hasPose && snapshot.SpectateCamera != null)
            {
                Transform cameraTransform = snapshot.SpectateCamera.transform;
                position = cameraTransform.position;
                rotation = cameraTransform.rotation;
            }

            state = new SpectatorPoseState(
                hasPose,
                snapshot.LocalPlayerActualClientId.Value,
                snapshot.LocalPlayerSlotId.Value,
                hasPose ? snapshot.SpectatedPlayerActualClientId : null,
                hasPose ? snapshot.SpectatedPlayerSlotId : null,
                position,
                rotation,
                DateTime.UtcNow.Ticks);
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

        _initialized = false;
        SpectatorLifecycleEvents.Changed -= OnSpectatorLifecycleChanged;
        Current = SpectatorState.Unavailable;
        _snapshotCache.Clear();
        ModLog.Debug("Spectator freecam module disposed.");
    }

    private void OnSpectatorLifecycleChanged(SpectatorLifecycleEventKind kind)
    {
        _snapshotCache.Clear();
        _freecamController.NotifyLifecycleEvent(kind);
    }
}
