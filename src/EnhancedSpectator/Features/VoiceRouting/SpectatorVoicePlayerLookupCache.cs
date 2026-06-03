using System;
using System.Collections.Generic;

namespace EnhancedSpectator.Features.VoiceRouting;

/// <summary>
/// Caches spectator voice player lookup by client id and slot id.
/// </summary>
public sealed class SpectatorVoicePlayerLookupCache<TPlayer>
    where TPlayer : class
{
    private readonly Dictionary<ulong, TPlayer> _byClientId = new Dictionary<ulong, TPlayer>();
    private readonly Dictionary<ulong, TPlayer> _bySlotId = new Dictionary<ulong, TPlayer>();

    /// <summary>
    /// Stores one player lookup entry. First entry for each key wins.
    /// </summary>
    public void Store(TPlayer player, ulong clientId, ulong slotId)
    {
        if (player == null)
        {
            throw new ArgumentNullException(nameof(player));
        }

        if (!_byClientId.ContainsKey(clientId))
        {
            _byClientId.Add(clientId, player);
        }

        if (!_bySlotId.ContainsKey(slotId))
        {
            _bySlotId.Add(slotId, player);
        }
    }

    /// <summary>
    /// Attempts to get a player by client id first, then slot id.
    /// </summary>
    public bool TryGet(ulong clientId, ulong slotId, out TPlayer player)
    {
        if (_byClientId.TryGetValue(clientId, out TPlayer? clientPlayer))
        {
            player = clientPlayer;
            return true;
        }

        if (_bySlotId.TryGetValue(slotId, out TPlayer? slotPlayer))
        {
            player = slotPlayer;
            return true;
        }

        player = null!;
        return false;
    }

    /// <summary>
    /// Removes all cached lookup entries.
    /// </summary>
    public void Clear()
    {
        _byClientId.Clear();
        _bySlotId.Clear();
    }
}
