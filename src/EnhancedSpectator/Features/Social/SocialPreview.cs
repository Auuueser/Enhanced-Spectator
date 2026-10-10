using System;
using System.Collections.Generic;

namespace EnhancedSpectator.Features.Social;

/// <summary>
/// The social features on the split-screen preview's identities, entirely local: the same betting round and
/// rating rules as the host, with the preview audience placing bets when they die and filing reviews when the
/// round ends, so the panels and animations can be tried alone. Nothing is sent to other players.
/// </summary>
internal sealed class SocialPreview
{
    internal const ulong LocalId = 900;
    private const ulong FirstId = 1000;
    private readonly List<(ulong ClientId, string Name, bool Alive)> _people = new List<(ulong, string, bool)>(32);
    private readonly HashSet<ulong> _died = new HashSet<ulong>();
    private BettingRound _round = new BettingRound();
    // Everyone in the current round; a living identity outside it (more views, a revival) starts a new round.
    private readonly HashSet<ulong> _members = new HashSet<ulong>();
    private readonly BetResolutionTracker _resolutions = new BetResolutionTracker();
    private readonly RatingState _tallies = new RatingState();
    private readonly Random _random;
    private readonly bool _chinese;

    internal BettingState Book { get; } = new BettingState();
    /// <summary>The local player's own review choices this round.</summary>
    internal RatingState Mine { get; } = new RatingState();
    internal bool RoundOpen => _round.Open;

    internal SocialPreview(bool chinese, int seed = 6) { _chinese = chinese; _random = new Random(seed); }

    internal static ulong IdOf(int slot) => FirstId + (ulong)slot;
    internal static int SlotOf(ulong id) => (int)(id - FirstId);

    internal string NameOf(ulong id)
    {
        if (id == LocalId) return _chinese ? "你" : "You";
        foreach (var person in _people) if (person.ClientId == id) return person.Name;
        return "?";
    }

    /// <summary>
    /// Follows the preview population: opens a round once two identities are alive, settles bets as they die,
    /// lets each newly dead identity bet, and lists markets decided since the last call.
    /// </summary>
    internal void Update(IReadOnlyList<(int Slot, string Name, bool Alive)> population, double now, List<BetMarket> resolved)
    {
        _people.Clear();
        foreach (var person in population) _people.Add((IdOf(person.Slot), person.Name, person.Alive));
        // The local viewer is in the audience from the start.
        _people.Add((LocalId, NameOf(LocalId), false));
        // Roster edits and revivals start a fresh test round; ordinary deaths remain in the current one.
        bool reset = _round.Open && !CanContinueRound();
        if (reset) _round = new BettingRound();
        bool started = !_round.Open && _round.Begin(Book, _people);
        if (started || reset)
        {
            Mine.Clear(); _tallies.Clear(); _died.Clear(); _members.Clear();
            foreach (var person in _people) _members.Add(person.ClientId);
            // Even when zero or one survivor cannot open a replacement round, refund and remove the old markets.
            if (!started) Book.BeginRound(_members);
        }
        if (_round.Open)
        {
            _round.Update(Book, _people, now, leaving: false);
            foreach (var person in _people)
                if (!person.Alive && person.ClientId != LocalId && _died.Add(person.ClientId)) AudienceBets(person.ClientId, now);
        }
        _resolutions.CopyNewlyResolvedTo(Book, resolved);
    }

    /// <summary>Living identities that can be reviewed now.</summary>
    internal void CopyTargetsTo(List<(ulong ClientId, string Name, bool Alive)> output)
    {
        output.Clear();
        if (!_round.Open) return;
        foreach (var person in _people) if (person.Alive) output.Add(person);
    }

    internal bool PlaceBet(int marketId, int option, int stake, double now) => Book.TryBet(LocalId, marketId, option, stake, now, out _);

    /// <summary>Gives or withdraws one of the local player's tags, by the host's rules.</summary>
    internal bool Rate(ulong target, int tag)
    {
        if (!_round.Open || !IsAlive(target)) return false;
        bool on = !Mine.Has(LocalId, target, tag);
        if (!Mine.Toggle(LocalId, target, tag, on)) return false;
        _tallies.Toggle(LocalId, target, tag, on);
        return true;
    }

    /// <summary>
    /// Ends the round: bets settle as at the ship's return and the audience adds their reviews to the local ones.
    /// Returns the review summary in slot order (empty when nobody was reviewed). The click stands in for the ship
    /// leaving, so bets from the last few seconds before it are late.
    /// </summary>
    internal List<(ulong Target, int[] Counts)> EndRound(double now, List<BetMarket> resolved)
    {
        var summary = new List<(ulong Target, int[] Counts)>();
        resolved.Clear();
        if (!_round.Open) return summary;
        _round.Finish(Book, now);
        var living = new List<ulong>();
        foreach (var person in _people) if (person.Alive) living.Add(person.ClientId);
        foreach (ulong rater in _died)
            for (int review = _random.Next(1, 3); review > 0 && living.Count > 0; review--)
                _tallies.Toggle(rater, living[_random.Next(living.Count)], _random.Next(RatingState.TagCount), true);
        foreach (var person in _people)
            if (_tallies.Tallies.TryGetValue(person.ClientId, out var counts) && Array.Exists(counts, count => count > 0))
                summary.Add((person.ClientId, (int[])counts.Clone()));
        _resolutions.CopyNewlyResolvedTo(Book, resolved);
        return summary;
    }

    /// <summary>A random audience identity's slot, or -1 when nobody has died yet.</summary>
    internal int RandomAudienceSlot()
    {
        if (_died.Count == 0) return -1;
        int pick = _random.Next(_died.Count);
        foreach (ulong id in _died) if (pick-- == 0) return SlotOf(id);
        return -1;
    }

    internal int RandomEmote() => _random.Next(SpectatorEmotes.Count);

    private bool CanContinueRound()
    {
        int present = 0;
        foreach (var person in _people)
        {
            if (person.Alive && _died.Contains(person.ClientId)) return false;
            if (_members.Contains(person.ClientId)) present++;
            else if (person.Alive) return false;
        }
        return present == _members.Count;
    }

    private bool IsAlive(ulong id) { foreach (var person in _people) if (person.ClientId == id) return person.Alive; return false; }

    // A newly dead identity stakes on each open market, so the panel shows other bettors and shares.
    private void AudienceBets(ulong player, double now)
    {
        foreach (var market in Book.Markets)
            if (market.Status == BetMarketStatus.Open && market.Options.Count > 0)
                Book.TryBet(player, market.Id, _random.Next(market.Options.Count), BettingState.Stakes[_random.Next(BettingState.Stakes.Length)], now, out _);
    }
}
