using System.Collections.Generic;

namespace EnhancedSpectator.Networking;

/// <summary>
/// Stores the last spectator target state received from remote modded peers.
/// </summary>
public sealed class RemoteSpectatorTargetRegistry
{
    private readonly Dictionary<ulong, SpectatorTargetState> _targets = new Dictionary<ulong, SpectatorTargetState>();

    /// <summary>
    /// Gets a monotonic counter that changes when stored target state changes.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>
    /// Registers or updates a remote spectator target state.
    /// </summary>
    public void Update(SpectatorTargetState state)
    {
        bool changed = !_targets.TryGetValue(state.LocalClientId, out SpectatorTargetState existing)
            || !existing.Equals(state);
        _targets[state.LocalClientId] = state;
        if (changed)
        {
            Revision++;
        }
    }

    /// <summary>
    /// Attempts to get the last known target state for a remote peer.
    /// </summary>
    public bool TryGet(ulong clientId, out SpectatorTargetState state)
    {
        return _targets.TryGetValue(clientId, out state!);
    }

    /// <summary>
    /// Gets a copy of all stored remote target states.
    /// </summary>
    public List<SpectatorTargetState> GetSnapshot()
    {
        return new List<SpectatorTargetState>(_targets.Values);
    }

    /// <summary>
    /// Copies all stored remote target states into a caller-owned list.
    /// </summary>
    public void CopySnapshotTo(List<SpectatorTargetState> destination)
    {
        destination.Clear();
        foreach (SpectatorTargetState state in _targets.Values)
        {
            destination.Add(state);
        }
    }

    /// <summary>
    /// Removes one remote target state.
    /// </summary>
    public void Remove(ulong clientId)
    {
        if (_targets.Remove(clientId))
        {
            Revision++;
        }
    }

    /// <summary>
    /// Removes all remote target states.
    /// </summary>
    public void Clear()
    {
        if (_targets.Count == 0)
        {
            return;
        }

        _targets.Clear();
        Revision++;
    }
}
