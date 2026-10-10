using System.Collections.Generic;

namespace EnhancedSpectator.Features.Social;

/// <summary>
/// One round of the betting book: it opens "who dies next" (re-opened after every death) and "does anyone get out
/// alive"; deaths settle the death market, everyone dead settles survival, and the end of the round settles what is
/// left. Players dying within <see cref="TogetherSeconds"/> of the first are one answer (an explosion, a pack), so
/// a bet on any of them wins and the pool is not split into empty markets. Survival is decided when the ship starts
/// to leave. The host's live game and the local preview both drive it, with times on the host's clock.
/// </summary>
internal sealed class BettingRound
{
    internal const double TogetherSeconds = 1;
    private readonly HashSet<ulong> _alive = new HashSet<ulong>();
    private readonly List<ulong> _order = new List<ulong>(32);
    private readonly List<ulong> _scratch = new List<ulong>(32);
    private readonly List<ulong> _pending = new List<ulong>(32);
    private readonly List<int> _winners = new List<int>(32);
    private double _leftAt = -1, _firstDeathAt = -1;

    internal bool Open { get; private set; }

    /// <summary>Starts a round for these players (in display order); false when fewer than two are alive.</summary>
    internal bool Begin(BettingState state, IReadOnlyList<(ulong ClientId, string Name, bool Alive)> players)
    {
        int living = 0; foreach (var player in players) if (player.Alive) living++;
        if (living < 2) return false;
        Open = true; _alive.Clear(); _order.Clear(); _pending.Clear(); _leftAt = _firstDeathAt = -1;
        foreach (var player in players) { _order.Add(player.ClientId); if (player.Alive) _alive.Add(player.ClientId); }
        state.BeginRound(_order);
        OpenNextDeath(state);
        state.Open(BetMarketKind.Survivors, new ulong[] { 1, 0 });
        return true;
    }

    /// <summary>
    /// Collects deaths and, once <see cref="TogetherSeconds"/> have passed since the first, settles them as one
    /// answer; true when the book changed.
    /// </summary>
    internal bool Update(BettingState state, IReadOnlyList<(ulong ClientId, string Name, bool Alive)> players, double now, bool leaving)
    {
        if (!Open) return false;
        if (leaving && _leftAt < 0) _leftAt = now;
        // Players only ever leave the alive set this round; revival at round end does not count.
        foreach (ulong id in _alive)
            if (!IsAlive(players, id) && !_pending.Contains(id)) { if (_pending.Count == 0) _firstDeathAt = now; _pending.Add(id); }
        if (_pending.Count == 0 || now - _firstDeathAt < TogetherSeconds) return false;
        Decide(state);
        return true;
    }

    /// <summary>Ends the round: survival is decided by who is still alive, an undecided death market is refunded.</summary>
    internal void Finish(BettingState state, double now)
    {
        if (!Open) return;
        if (_pending.Count > 0) Decide(state);
        if (state.OpenMarket(BetMarketKind.Survivors) is { } survivors) state.Settle(survivors, _alive.Count > 0 ? 0 : 1, _leftAt >= 0 ? _leftAt : now);
        if (state.OpenMarket(BetMarketKind.NextDeath) is { } unresolved) state.Refund(unresolved);
        Open = false;
    }

    // The collected deaths answer the open death market together; the next one opens for whoever is left.
    private void Decide(BettingState state)
    {
        double at = _firstDeathAt;
        foreach (ulong dead in _pending) _alive.Remove(dead);
        if (state.OpenMarket(BetMarketKind.NextDeath) is { } next)
        {
            _winners.Clear();
            foreach (ulong dead in _pending) { int index = next.Options.IndexOf(dead); if (index >= 0) _winners.Add(index); }
            if (_winners.Count > 0) state.Settle(next, _winners, at); else state.Refund(next);
        }
        _pending.Clear(); _firstDeathAt = -1;
        OpenNextDeath(state);
        if (_alive.Count == 0 && state.OpenMarket(BetMarketKind.Survivors) is { } survivors) state.Settle(survivors, 1, at);
    }

    private static bool IsAlive(IReadOnlyList<(ulong ClientId, string Name, bool Alive)> players, ulong id)
    { foreach (var player in players) if (player.ClientId == id) return player.Alive; return false; }

    private void OpenNextDeath(BettingState state)
    {
        if (_alive.Count < 2 || state.OpenMarket(BetMarketKind.NextDeath) != null) return;
        _scratch.Clear(); foreach (ulong id in _order) if (_alive.Contains(id)) _scratch.Add(id);
        state.Open(BetMarketKind.NextDeath, _scratch);
    }
}

/// <summary>Reports each market once, when it is first seen decided after being open (or arrives decided).</summary>
internal sealed class BetResolutionTracker
{
    private readonly Dictionary<int, BetMarketStatus> _seen = new Dictionary<int, BetMarketStatus>();

    internal void CopyNewlyResolvedTo(BettingState state, List<BetMarket> output)
    {
        output.Clear();
        foreach (var market in state.Markets)
        {
            bool known = _seen.TryGetValue(market.Id, out var before);
            _seen[market.Id] = market.Status;
            if (market.Status != BetMarketStatus.Open && (!known || before == BetMarketStatus.Open)) output.Add(market);
        }
    }

    internal void Clear() => _seen.Clear();
}
