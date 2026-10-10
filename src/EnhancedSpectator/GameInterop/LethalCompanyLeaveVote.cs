namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The game's early-leave vote as a dead player has it, read from what HUDManager.Update drives with the held ping-scan
/// button (the vote itself stays the game's): its prompt, votes and hold meter until the ship's leaving is announced,
/// then the departure time (a passed vote moves it to 0.1 of the day ahead; otherwise the hour-before-midnight alert).
/// </summary>
internal static class LethalCompanyLeaveVote
{
    private static float _departureAt = -1;
    private static string? _departure;
    // The game's own automatic leave time (TimeOfDay's field default, and the value each day starts with).
    private static bool LeavesAtMidnight(float time) => time == .996f || time == .998f;

    internal static SplitScreenLeaveVoteState Read()
    {
        var round = StartOfRound.Instance; var time = TimeOfDay.Instance; var hud = HUDManager.Instance;
        if (round.inShipPhase || round.shipIsLeaving || !round.currentLevel.planetHasTime) return default;
        if (time.shipLeavingAlertCalled)
        {
            if (_departureAt != time.shipLeaveAutomaticallyTime)
            {
                // The automatic leave the game calls midnight (11:57 PM) shows as midnight; a passed vote's time as it is.
                _departure = FormatTime(LeavesAtMidnight(time.shipLeaveAutomaticallyTime) ? 1 : time.shipLeaveAutomaticallyTime, time.numberOfHours);
                _departureAt = time.shipLeaveAutomaticallyTime;
            }
            // Whether this player voted stays known, for the strip's "voted" stage when their vote was the last one.
            return new SplitScreenLeaveVoteState(false, time.votedShipToLeaveEarlyThisRound, false, time.votesForShipToLeaveEarly, 0, 0, _departure);
        }
        var meter = hud.holdButtonToEndGameEarlyMeter;
        return new SplitScreenLeaveVoteState(true, time.votedShipToLeaveEarlyThisRound, round.localPlayerUsingController, time.votesForShipToLeaveEarly,
            round.connectedPlayersAmount + 1 - round.livingPlayers, meter.gameObject.activeSelf ? meter.fillAmount : 0, null);
    }

    /// <summary>Preview only: when a vote passed now would have the ship leave.</summary>
    internal static string PreviewDeparture()
    {
        var time = TimeOfDay.Instance;
        return FormatTime(time.normalizedTimeOfDay + .1f, time.numberOfHours);
    }

    // In the HUD clock's own words, so the strip and the clock always agree: the game's format, or a clock mod's
    // (LCBetterClock's 24-hour time, leading zero and fixes, a SetClock postfix). The time is put through the clock,
    // which is then given back exactly as it was (its text and the values SetClock keeps), before anything draws.
    private static string FormatTime(float timeNormalized, int numberOfHours)
    {
        var hud = HUDManager.Instance; var clock = hud.clockNumber;
        string shown = clock.text, newLine = hud.newLine, amPM = hud.amPM;
        int minutes = hud.previousMinutes, hours = hud.previousHours;
        try
        {
            hud.previousMinutes = -1;
            hud.SetClock(timeNormalized, numberOfHours, createNewLine: false);
            string text = clock.text.Replace('\n', ' ').Trim();
            // The game's midnight is "12:00   AM" on one line.
            while (text.Contains("  ")) text = text.Replace("  ", " ");
            return text;
        }
        finally
        {
            // SetClock includes other mods' patches; borrowed HUD state must be returned if any of them throws.
            clock.text = shown; hud.newLine = newLine; hud.amPM = amPM; hud.previousMinutes = minutes; hud.previousHours = hours;
        }
    }
}
