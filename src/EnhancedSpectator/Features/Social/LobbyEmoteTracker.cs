using System.Collections.Generic;

namespace EnhancedSpectator.Features.Social;

/// <summary>
/// Spectator emotes through the Steam lobby, for games whose host does not run this mod (so nothing can be relayed
/// through it): each player publishes "counter:emote" in their own lobby member data, and every other player with
/// the mod shows an emote whenever a member's counter changes. The first value seen from a member is only recorded,
/// so joining a lobby never replays old emotes; a member first seen with nothing published counts as counter 0, so
/// their very first emote shows.
/// </summary>
internal sealed class LobbyEmoteTracker
{
    internal const string Key = "EnhancedSpectator.Emote";
    private readonly Dictionary<ulong, int> _seen = new Dictionary<ulong, int>();
    private int _sequence;

    /// <summary>The value to publish for a new emote.</summary>
    internal string Next(int emote) => ++_sequence + ":" + emote;

    /// <summary>Reads a member's published value; true with the emote when it changed since the last look.</summary>
    internal bool Read(ulong member, string? value, out int emote)
    {
        emote = -1;
        if (string.IsNullOrEmpty(value)) { if (!_seen.ContainsKey(member)) _seen[member] = 0; return false; }
        int colon = value!.IndexOf(':');
        if (colon <= 0 || !int.TryParse(value.Substring(0, colon), out int sequence) || !int.TryParse(value.Substring(colon + 1), out emote)) return false;
        bool known = _seen.TryGetValue(member, out int last);
        _seen[member] = sequence;
        return known && sequence != last;
    }

    /// <summary>A new game: members are looked at afresh.</summary>
    internal void Clear() => _seen.Clear();
}
