using System.Collections.Generic;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The game's end-of-round report. Patches on HUDManager mark its stages (stats filled, penalty, level, HUD shown
/// again); the wording is read back from the game's own panel, so a translation mod's text (LC Chinese Project
/// translates the notes and penalty in place) is what the split-screen report shows too.
/// </summary>
internal static class LethalCompanyRoundResults
{
    /// <summary>Counts reports: a new number is a new report.</summary>
    internal static int Round { get; private set; }
    /// <summary>From the stats being filled until the game shows its HUD again.</summary>
    internal static bool Open { get; private set; }
    internal static bool Shown { get; private set; }
    /// <summary>The moon has a day, so the game shows its report as the ship leaves (not at the Company).</summary>
    internal static bool ReportComing => StartOfRound.Instance is { currentLevel: { } level } && level.planetHasTime;
    /// <summary>The frame and unscaled time the game started its stats animation.</summary>
    internal static int ShownFrame { get; private set; }
    internal static float ShownAt { get; private set; }
    private static bool _dayPassed;
    private static readonly RoundReport Captured = new RoundReport();
    /// <summary>The current moon's name, for the report's heading.</summary>
    internal static string PlanetName
    {
        get { var round = StartOfRound.Instance; return round != null && round.currentLevel != null ? round.currentLevel.PlanetName : string.Empty; }
    }
    private static readonly List<int> Slots = new List<int>();

    // HUDManager.FillEndGameStats postfix. Who was alive, dead or missing is taken now: the dead are revived while
    // the report is still on screen.
    internal static void StatsFilled(int scrapCollected)
    {
        var round = StartOfRound.Instance;
        var stats = HUDManager.Instance.statsUIElements;
        Captured.Players.Clear(); Slots.Clear();
        for (int i = 0; i < round.allPlayerScripts.Length; i++)
        {
            var player = round.allPlayerScripts[i];
            // The players the game's panel lists.
            if (!player.disconnectedMidGame && !player.isPlayerDead && !player.isPlayerControlled) continue;
            Captured.Players.Add(new RoundReportPlayer
            {
                Name = PlayerDisplayNames.Of(player), SteamId = player.playerSteamId, Local = player == round.localPlayerController,
                NotesText = EnglishNotes(i),
                State = !player.isPlayerDead ? RoundReportState.Alive
                    : player.causeOfDeath == CauseOfDeath.Abandoned ? RoundReportState.Missing : RoundReportState.Deceased
            });
            Slots.Add(i);
        }
        Captured.Planet = round.currentLevel.PlanetName;
        Captured.Grade = stats.gradeLetter.text.Trim();
        Captured.AllDead = round.allPlayersDead;
        Captured.Collected = scrapCollected;
        Captured.Total = Mathf.RoundToInt(RoundManager.Instance.totalScrapValueInLevel);
        Captured.Penalty = Captured.Level = false;
        Open = true; Shown = _dayPassed = false; Round++;
    }

    // HUDManager.ApplyPenalty postfix.
    internal static void PenaltyApplied() { if (Open) Captured.Penalty = true; }
    // HUDManager.SetPlayerLevel postfix: the level bar animates from here.
    internal static void LevelShown() { if (Open) Captured.Level = true; }
    // The real stats animation starts the UI. An unscaled one-second guess can fire before the game does.
    internal static void ReportTriggered(Animator animator, string trigger)
    {
        if (trigger != "displayStats" && trigger != "displayStatsChallenge") return;
        if (Open && HUDManager.Instance != null && animator == HUDManager.Instance.endgameStatsAnimator)
        { Shown = true; ShownFrame = Time.frameCount; ShownAt = Time.unscaledTime; }
    }
    // EndOfGame passes the day after level/revive/penalty, then reveals the HUD. Earlier HideHUD(false) calls
    // (a menu/mod restoring HUD) are not the end of this report.
    internal static void DayPassed() { if (Open) _dayPassed = true; }
    /// <summary>The day has passed: the game's days-left banner and its sound start, and its penalty box fades.</summary>
    internal static bool DayEnded => _dayPassed;
    internal static void HudShown() { if (_dayPassed) Open = Shown = false; }
    internal static void Clear() { Open = Shown = _dayPassed = false; Captured.Players.Clear(); Slots.Clear(); }

    /// <summary>The report as the game's panel shows it this frame.</summary>
    internal static void Read(RoundReport report)
    {
        Captured.CopyTo(report);
        var hud = HUDManager.Instance;
        if (hud == null) return; // The captured report survives the scene's HUD being torn down.
        var stats = hud.statsUIElements;
        for (int i = 0; i < Slots.Count; i++)
        {
            int slot = Slots[i];
            // A player beyond the game's panel (a mod for more players may not widen it) keeps the untranslated notes.
            if (slot < stats.playerNotesText.Length) report.Players[i].NotesText = stats.playerNotesText[slot].text;
        }
        if (report.Penalty) { report.PenaltyLines = stats.penaltyAddition.text; report.PenaltyDue = stats.penaltyTotal.text; }
        if (report.Level)
        {
            report.LevelName = hud.playerLevelText.text; report.LevelExperience = hud.playerLevelXPCounter.text;
            report.LevelFill = hud.playerLevelMeter.fillAmount;
        }
    }

    private static string EnglishNotes(int slot)
    {
        var notes = StartOfRound.Instance.gameStats.allPlayerStats[slot].playerNotes;
        var text = new System.Text.StringBuilder("Notes:\n");
        for (int i = 0; i < 3 && i < notes.Count; i++) text.Append("* ").Append(notes[i]).Append('\n');
        return text.ToString();
    }
}
