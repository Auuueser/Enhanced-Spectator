namespace EnhancedSpectator.Features.SplitScreen;

/// <summary>Local preview actions; mode values are explicit UI choices rather than game enum values.</summary>
internal enum SplitScreenTestAction
{
    SetCount, KillSelected, KillRandom, Revive, Reset, TogglePlayback, SelectMode, CycleQuality, Exit, ToggleHud, ToggleThermal,
    // Social preview: handled by the social module through SplitScreenModule.SocialTest.
    ToggleSpeaking, AudienceEmote, OpenEmotes, OpenBets, OpenRatings, EndRound,
    // Audience-only preview identities (value: count).
    SetAudience,
    // Demo: play chapter `value` (-1 for all); captions on/off; close every social panel between chapters.
    Demo, ToggleCaptions, CloseSocial,
    // The end-of-round report in split-screen style, with preview data.
    ShowReport,
    // The early-leave vote played through: this player's hold and vote, the others', then the departure.
    ShowLeaveVote
}

/// <summary>Social panel elements the demo points at and zooms onto (through SplitScreenModule.SocialFocus).</summary>
internal enum SplitScreenDemoTarget
{
    // The emote row; one emote (a: index).
    Emotes, Emote,
    // The bets panel; a stake chip (a: stake); the open "who dies next" market; its option (a: index); the leaderboard;
    // the result card.
    Bets, Stake, DeathMarket, DeathOption, Leaderboard, BetResult,
    // The review panel; a colleague's row (a: row); a tag (a: row, b: tag); the notice with its first cards (a: count).
    Reviews, ReviewRow, ReviewTag, Notice
}
