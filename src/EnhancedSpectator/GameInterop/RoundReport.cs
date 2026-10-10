using System.Collections.Generic;

namespace EnhancedSpectator.GameInterop;

internal enum RoundReportState { Alive, Deceased, Missing }
/// <summary>The game's end-of-round sounds, for the report preview.</summary>
internal enum RoundReportSound { Music, Experience, LevelUp, DaysLeft, ExperienceDown, LevelDown }

internal sealed class RoundReportPlayer
{
    internal string Name = string.Empty;
    internal ulong SteamId;
    internal RoundReportState State;
    internal bool Local;
    /// <summary>The game's notes text for this player as its panel shows it ("Notes:" then a "* " line per note).</summary>
    internal string NotesText = string.Empty;
}

/// <summary>The end-of-round performance report, as the game's own panel shows it.</summary>
internal sealed class RoundReport
{
    internal string Planet = string.Empty, Grade = string.Empty;
    internal bool AllDead;
    internal int Collected, Total;
    internal readonly List<RoundReportPlayer> Players = new List<RoundReportPlayer>();
    internal bool Penalty, Level;
    internal string PenaltyLines = string.Empty, PenaltyDue = string.Empty, LevelName = string.Empty, LevelExperience = string.Empty;
    internal float LevelFill;

    internal void CopyTo(RoundReport other)
    {
        other.Planet = Planet; other.Grade = Grade; other.AllDead = AllDead; other.Collected = Collected; other.Total = Total;
        other.Penalty = Penalty; other.Level = Level; other.PenaltyLines = PenaltyLines; other.PenaltyDue = PenaltyDue;
        other.LevelName = LevelName; other.LevelExperience = LevelExperience; other.LevelFill = LevelFill;
        while (other.Players.Count < Players.Count) other.Players.Add(new RoundReportPlayer());
        other.Players.RemoveRange(Players.Count, other.Players.Count - Players.Count);
        for (int i = 0; i < Players.Count; i++)
        {
            var from = Players[i]; var to = other.Players[i];
            to.Name = from.Name; to.SteamId = from.SteamId; to.State = from.State; to.Local = from.Local; to.NotesText = from.NotesText;
        }
    }

    /// <summary>The notes in a player's notes text: its "* " lines (the first line is the "Notes:" heading).</summary>
    internal static void ParseNotes(string text, List<string> output)
    {
        output.Clear();
        foreach (var line in text.Split('\n'))
        {
            string note = line.Trim();
            if (note.Length > 1 && note[0] == '*') output.Add(note.Substring(1).Trim());
        }
    }

    // The local preview's report: the preview people with typical notes, so the split-screen report can be seen
    // without playing a round. Level and penalty arrive later, as in a real report.
    internal void FillPreview(IReadOnlyList<(string Name, bool Alive)> people, string planet, bool chinese)
    {
        string[] notes = chinese
            ? new[] { "最懒惰的员工", "最偏执的员工", "受伤最多的员工", "最赚钱的员工" }
            : new[] { "The laziest employee.", "The most paranoid employee.", "Sustained the most injuries.", "Most profitable" };
        Planet = planet; Grade = "B"; AllDead = false; Collected = 742; Total = 1180;
        Penalty = Level = false; PenaltyLines = PenaltyDue = LevelName = LevelExperience = string.Empty; LevelFill = 0;
        Players.Clear();
        for (int i = 0; i < people.Count; i++)
        {
            var player = new RoundReportPlayer
            {
                Name = people[i].Name, Local = i == 0,
                State = people[i].Alive ? RoundReportState.Alive : i % 5 == 3 ? RoundReportState.Missing : RoundReportState.Deceased,
                NotesText = (chinese ? "备注：\n" : "Notes:\n") + (i < notes.Length ? "* " + notes[i] + "\n" : string.Empty)
                    + (i == 1 ? "* " + notes[3] + "\n" : string.Empty)
            };
            Players.Add(player);
        }
    }
}
