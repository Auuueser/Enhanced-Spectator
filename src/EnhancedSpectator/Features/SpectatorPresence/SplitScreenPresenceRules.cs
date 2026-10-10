using EnhancedSpectator.Networking;

namespace EnhancedSpectator.Features.SpectatorPresence;

/// <summary>How a remote dead spectator's voice reaches this listener.</summary>
internal enum SplitVoiceRoute
{
    /// <summary>The configured spectator voice routing (positional at the ghost).</summary>
    Configured,
    /// <summary>Positional at the ghost whatever the configured audience, without a global 2D fallback.</summary>
    Positional,
    /// <summary>Watching together (the same party): without direction, at full volume, as if beside one another.</summary>
    Party,
    /// <summary>The game's own dead-player voice (heard by the dead, everywhere, without direction).</summary>
    Vanilla,
    /// <summary>Not heard.</summary>
    Muted,
}

/// <summary>
/// Split-screen's rule that enlarging a view means watching that player. A watching spectator stands by the player:
/// seen and heard where they are, and hearing only the spectators who watch with them. With every view tiled the
/// spectator sits in the audience: unseen, heard only by the rest of the audience (the other dead who are not
/// watching, vanilla players included) without direction. Spectators outside split-screen never appear in, or speak
/// into, a split-screen player's views. Spectators watching together (one party, see SpectatorPartyRules) share
/// one camera: they hear each other without direction, and everyone else hears them at their formation places.
/// </summary>
internal static class SplitScreenPresenceRules
{
    internal static bool HidesModel(SpectatorSplitView speaker, SpectatorSplitView viewer)
        => speaker == SpectatorSplitView.Audience || viewer != SpectatorSplitView.None && speaker == SpectatorSplitView.None;

    /// <summary>
    /// <paramref name="listener"/> is <see cref="SpectatorSplitView.None"/> for a living listener;
    /// <paramref name="party"/>: speaker and listener watch together.
    /// </summary>
    internal static SplitVoiceRoute Voice(SpectatorSplitView speaker, SpectatorSplitView listener, bool party = false) => listener switch
    {
        SpectatorSplitView.Watching => speaker != SpectatorSplitView.Watching ? SplitVoiceRoute.Muted
            : party ? SplitVoiceRoute.Party : SplitVoiceRoute.Positional,
        SpectatorSplitView.Audience => speaker == SpectatorSplitView.Watching ? SplitVoiceRoute.Muted : SplitVoiceRoute.Vanilla,
        _ => speaker == SpectatorSplitView.Audience ? SplitVoiceRoute.Vanilla : SplitVoiceRoute.Configured,
    };

    /// <summary>Dead players without the mod (no published state): a watching listener does not hear them.</summary>
    internal static bool MutesUnmoddedDead(SpectatorSplitView listener) => listener == SpectatorSplitView.Watching;
}
