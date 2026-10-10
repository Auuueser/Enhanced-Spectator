using System;

namespace EnhancedSpectator.Features.SpectatorPresence;

/// <summary>
/// Watch-together parties: a spectator following another (published as FollowingClientId), directly or along a chain,
/// belongs to the party of the spectator at the chain's end, its leader, and watches through the leader's camera.
/// </summary>
internal static class SpectatorPartyRules
{
    // Longer chains than the lobby can hold only come from a loop that does not pass through the member.
    private const int MaxChain = 64;

    /// <summary>The leader of <paramref name="member"/>'s party: itself when it follows nobody, or when its chain loops.</summary>
    internal static ulong Leader(ulong member, Func<ulong, ulong?> following)
    {
        ulong current = member;
        for (int step = 0; step < MaxChain; step++)
        {
            if (following(current) is not { } next) return current;
            if (next == member) return member;
            current = next;
        }
        return member;
    }
}
