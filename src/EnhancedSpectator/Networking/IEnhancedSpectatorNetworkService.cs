using System.Collections.Generic;
using EnhancedSpectator.Features.VoiceActivity;

namespace EnhancedSpectator.Networking;

/// <summary>
/// Coordinates Enhanced Spectator mod-owned networking state.
/// </summary>
public interface IEnhancedSpectatorNetworkService
{
    /// <summary>
    /// Gets whether the networking service is currently usable.
    /// </summary>
    bool IsNetworkAvailable { get; }

    /// <summary>
    /// Gets whether spectator target sync is currently allowed.
    /// </summary>
    bool IsTargetSyncEnabled { get; }

    /// <summary>
    /// Gets whether at least one compatible Enhanced Spectator peer has completed capability handshake.
    /// </summary>
    bool HasCompatibleModPeer { get; }

    /// <summary>
    /// Gets the current mod network lifecycle state.
    /// </summary>
    NetworkLifecycleState LifecycleState { get; }

    /// <summary>
    /// Gets a revision that changes when stored remote peer identity data changes.
    /// </summary>
    int RemotePeerIdentityRevision { get; }

    /// <summary>
    /// Gets a revision that changes when stored remote spectator target data changes.
    /// </summary>
    int RemoteSpectatorTargetRevision { get; }

    /// <summary>
    /// Initializes the service.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Ticks local network state and spectator target change detection.
    /// </summary>
    void Tick();

    /// <summary>
    /// Shuts down the service.
    /// </summary>
    void Dispose();

    /// <summary>
    /// Attempts to get a known peer capability.
    /// </summary>
    bool TryGetPeerCapability(ulong clientId, out ModPeerCapability capability);

    /// <summary>
    /// Attempts to get the last received spectator target state for a remote peer.
    /// </summary>
    bool TryGetRemoteSpectatorTarget(ulong clientId, out SpectatorTargetState state);

    /// <summary>
    /// Attempts to get the last received spectator camera pose for a remote peer.
    /// </summary>
    bool TryGetRemoteSpectatorPose(ulong clientId, out SpectatorPoseState state);

    /// <summary>
    /// Attempts to get the last received mod-owned identity for a remote peer.
    /// </summary>
    bool TryGetRemotePeerIdentity(ulong clientId, out PeerIdentityState state);

    /// <summary>
    /// Attempts to get the last received visual-only voice activity for a remote peer.
    /// </summary>
    bool TryGetRemoteVoiceActivity(ulong clientId, out VoiceActivityState state);

    /// <summary>
    /// Gets a copy of the last received spectator target states for remote peers.
    /// </summary>
    IReadOnlyList<SpectatorTargetState> GetRemoteSpectatorTargets();

    /// <summary>
    /// Copies the last received spectator target states into a caller-owned list.
    /// </summary>
    void CopyRemoteSpectatorTargetsTo(List<SpectatorTargetState> destination);

    /// <summary>
    /// Gets a copy of the last received spectator camera poses for remote peers.
    /// </summary>
    IReadOnlyList<SpectatorPoseState> GetRemoteSpectatorPoses();

    /// <summary>
    /// Gets a copy of the last received mod-owned peer identities.
    /// </summary>
    IReadOnlyList<PeerIdentityState> GetRemotePeerIdentities();

    /// <summary>
    /// Copies the last received mod-owned peer identities into a caller-owned list.
    /// </summary>
    void CopyRemotePeerIdentitiesTo(List<PeerIdentityState> destination);

    /// <summary>
    /// Gets a copy of the last received remote voice activity snapshots.
    /// </summary>
    IReadOnlyList<VoiceActivityState> GetRemoteVoiceActivities();

    /// <summary>
    /// Gets a copy of known mod peer capabilities.
    /// </summary>
    IReadOnlyList<ModPeerCapability> GetKnownModdedPeers();
}
