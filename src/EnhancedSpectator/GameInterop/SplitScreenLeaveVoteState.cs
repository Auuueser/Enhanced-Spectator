namespace EnhancedSpectator.GameInterop;

/// <summary>The game's early-leave vote as a dead player has it: the hold prompt with its votes, or the departure time.</summary>
internal readonly struct SplitScreenLeaveVoteState
{
    internal readonly bool Prompt, Voted, Controller;
    internal readonly int Votes, Needed;
    /// <summary>The held button's progress towards casting the vote (0–1).</summary>
    internal readonly float Hold;
    /// <summary>When the ship leaves, once the game has set it (a vote passed, or the hour before midnight).</summary>
    internal readonly string? Departure;
    internal SplitScreenLeaveVoteState(bool prompt, bool voted, bool controller, int votes, int needed, float hold, string? departure)
    { Prompt = prompt; Voted = voted; Controller = controller; Votes = votes; Needed = needed; Hold = hold; Departure = departure; }
}
