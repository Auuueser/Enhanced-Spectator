using System.Collections.Generic;

namespace EnhancedSpectator.Networking;

/// <summary>
/// Stores the last spectator camera pose received from remote modded peers.
/// </summary>
public sealed class RemoteSpectatorPoseRegistry
{
    private readonly Dictionary<ulong, (SpectatorPoseState Pose, double ReceivedAt)> _poses = new Dictionary<ulong, (SpectatorPoseState, double)>();

    /// <summary>A watcher is currently following this peer's large view.</summary>
    internal bool HasCameraFollower(ulong clientId)
    {
        foreach (var entry in _poses.Values)
            if (entry.Pose.SplitView == SpectatorSplitView.Watching && entry.Pose.FollowingClientId == clientId) return true;
        return false;
    }

    internal double ReceivedAt(ulong clientId) => _poses.TryGetValue(clientId, out var entry) ? entry.ReceivedAt : 0;

    /// <summary>
    /// Registers or updates a remote spectator pose.
    /// </summary>
    public void Update(SpectatorPoseState state, double receivedAt = 0)
    {
        if (!state.IsSpectating)
        {
            _poses.Remove(state.LocalClientId);
            return;
        }

        _poses[state.LocalClientId] = (state, receivedAt);
    }

    /// <summary>
    /// Attempts to get the last known active pose for a remote peer.
    /// </summary>
    public bool TryGet(ulong clientId, out SpectatorPoseState state)
    {
        if (_poses.TryGetValue(clientId, out var stored))
        {
            state = stored.Pose;
            return true;
        }

        state = null!;
        return false;
    }

    /// <summary>
    /// Gets a copy of all stored remote poses.
    /// </summary>
    public List<SpectatorPoseState> GetSnapshot()
    {
        List<SpectatorPoseState> snapshot = new List<SpectatorPoseState>();
        foreach (var entry in _poses.Values)
        {
            snapshot.Add(entry.Pose);
        }

        return snapshot;
    }

    /// <summary>
    /// Removes one remote pose.
    /// </summary>
    public void Remove(ulong clientId)
    {
        _poses.Remove(clientId);
    }

    /// <summary>
    /// Removes all remote poses.
    /// </summary>
    public void Clear()
    {
        _poses.Clear();
    }
}
