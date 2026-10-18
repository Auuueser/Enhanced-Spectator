using EnhancedSpectator.Networking;

namespace EnhancedSpectator.Features.SpectatorPresence;

/// <summary>How a remote dead spectator's voice reaches this listener.</summary>
internal enum SplitVoiceRoute
{
    /// <summary>The configured spectator voice routing (positional at the ghost).</summary>
    Configured,
    /// <summary>One large-view group (watching the same player, or together): without direction, at full volume.</summary>
    Party,
    /// <summary>The game's own dead-player voice (heard by the dead, everywhere, without direction).</summary>
    Vanilla,
    /// <summary>Not heard.</summary>
    Muted,
}

/// <summary>
/// Split-screen's rule that enlarging a view means watching that player. A watching spectator stands by the player:
/// seen there, heard by the living where they are, and in voice one group with the other spectators watching the same
/// player (or watching together, see SpectatorPartyRules): they hear each other without direction at full volume,
/// whatever their cameras, and do not hear the watchers of anyone else. With one player left alive every dead
/// spectator watches that player, enlarged or tiled: in split-screen the dead then all hear one another (those outside
/// split-screen and without the mod too) as the game's own dead voice, without direction. With every view tiled the spectator sits in
/// the audience: unseen, heard only by the rest of the audience (the other dead who are not watching, vanilla players
/// included) without direction. Spectators outside split-screen never appear in, or speak into, a split-screen
/// player's views.
/// </summary>
internal static class SplitScreenPresenceRules
{
    /// <summary>Watchers share a group by watched client identity or by their watch-together party.</summary>
    internal static bool SameGroup(ulong? target, ulong? otherTarget, bool sameParty)
        => sameParty || target.HasValue && target == otherTarget;

    internal static bool HidesModel(SpectatorSplitView speaker, SpectatorSplitView viewer)
        => speaker == SpectatorSplitView.Audience || viewer != SpectatorSplitView.None && speaker == SpectatorSplitView.None;

    /// <summary>
    /// <paramref name="listener"/> is <see cref="SpectatorSplitView.None"/> for a living listener;
    /// <paramref name="group"/>: speaker and listener watch the same player, or watch together;
    /// <paramref name="lastOne"/>: one player is left alive (split-screen listeners only).
    /// </summary>
    internal static SplitVoiceRoute Voice(SpectatorSplitView speaker, SpectatorSplitView listener, bool group = false, bool lastOne = false)
        => lastOne && listener != SpectatorSplitView.None ? SplitVoiceRoute.Vanilla : listener switch
    {
        SpectatorSplitView.Watching => speaker == SpectatorSplitView.Watching && group ? SplitVoiceRoute.Party : SplitVoiceRoute.Muted,
        SpectatorSplitView.Audience => speaker == SpectatorSplitView.Watching ? SplitVoiceRoute.Muted : SplitVoiceRoute.Vanilla,
        _ => speaker == SpectatorSplitView.Audience ? SplitVoiceRoute.Vanilla : SplitVoiceRoute.Configured,
    };

    /// <summary>Dead players without the mod (no published state): a watching listener does not hear them.</summary>
    internal static bool MutesUnmoddedDead(SpectatorSplitView listener, bool lastOne = false) => listener == SpectatorSplitView.Watching && !lastOne;
}
