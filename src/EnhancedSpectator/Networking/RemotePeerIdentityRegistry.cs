using System;
using System.Collections.Generic;

namespace EnhancedSpectator.Networking;

/// <summary>
/// Stores mod-owned identity data received from remote peers.
/// </summary>
public sealed class RemotePeerIdentityRegistry
{
    private readonly Dictionary<ulong, PeerIdentityState> _identities = new Dictionary<ulong, PeerIdentityState>();

    /// <summary>
    /// Gets a monotonic counter that changes when stored identity data changes.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>
    /// Registers or updates a remote peer identity.
    /// </summary>
    public void Update(PeerIdentityState state)
    {
        bool changed = true;
        if (_identities.TryGetValue(state.ClientId, out PeerIdentityState existing))
        {
            if (state.TimestampTicks < existing.TimestampTicks)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(state.VoicePlayerName)
                && !string.IsNullOrWhiteSpace(existing.VoicePlayerName))
            {
                state = new PeerIdentityState(
                    state.ClientId,
                    state.PlayerSlotId,
                    state.DisplayName,
                    existing.VoicePlayerName,
                    state.TimestampTicks);
            }

            changed = !IdentityEquals(existing, state);
        }

        _identities[state.ClientId] = state;
        if (changed)
        {
            Revision++;
        }
    }

    /// <summary>
    /// Attempts to get the last known identity for a peer.
    /// </summary>
    public bool TryGet(ulong clientId, out PeerIdentityState state)
    {
        return _identities.TryGetValue(clientId, out state!);
    }

    /// <summary>
    /// Gets a copy of all stored identities.
    /// </summary>
    public List<PeerIdentityState> GetSnapshot()
    {
        return new List<PeerIdentityState>(_identities.Values);
    }

    /// <summary>
    /// Copies all stored identities into a caller-owned list.
    /// </summary>
    public void CopySnapshotTo(List<PeerIdentityState> destination)
    {
        destination.Clear();
        foreach (PeerIdentityState state in _identities.Values)
        {
            destination.Add(state);
        }
    }

    /// <summary>
    /// Removes one peer identity.
    /// </summary>
    public void Remove(ulong clientId)
    {
        if (_identities.Remove(clientId))
        {
            Revision++;
        }
    }

    /// <summary>
    /// Removes all stored identities.
    /// </summary>
    public void Clear()
    {
        if (_identities.Count == 0)
        {
            return;
        }

        _identities.Clear();
        Revision++;
    }

    private static bool IdentityEquals(PeerIdentityState left, PeerIdentityState right)
    {
        return left.ClientId == right.ClientId
            && left.PlayerSlotId == right.PlayerSlotId
            && string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
            && string.Equals(left.VoicePlayerName, right.VoicePlayerName, StringComparison.Ordinal);
    }
}
