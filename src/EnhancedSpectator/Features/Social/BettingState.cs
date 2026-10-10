using System;
using System.Collections.Generic;

namespace EnhancedSpectator.Features.Social;

internal enum BetMarketKind : byte { NextDeath, Survivors }
internal enum BetMarketStatus : byte { Open, Settled, Refunded }

internal sealed class BetEntry
{
    internal ulong Player;
    internal int Option, Stake, Payout;
    /// <summary>Host time the bet was placed; only the host compares it.</summary>
    internal double Placed;
    /// <summary>Placed too close to the decision: refunded instead of counted.</summary>
    internal bool Late;
    internal BetEntry(ulong player, int option, int stake) { Player = player; Option = option; Stake = stake; }
}

/// <summary>One question; options are player client ids (next death) or 1 yes / 0 no (survivors).</summary>
internal sealed class BetMarket
{
    internal int Id;
    internal BetMarketKind Kind;
    internal BetMarketStatus Status;
    internal readonly List<ulong> Options = new List<ulong>();
    internal readonly List<BetEntry> Bets = new List<BetEntry>();
    /// <summary>The answer: one option, several when players died together, none when undecided.</summary>
    internal readonly List<int> Winners = new List<int>();
    /// <summary>The first answer, or -1 when undecided.</summary>
    internal int Winner => Winners.Count > 0 ? Winners[0] : -1;

    internal int Pool { get { int pool = 0; foreach (var bet in Bets) pool += bet.Stake; return pool; } }
    internal int Staked(int option) { int sum = 0; foreach (var bet in Bets) if (bet.Option == option) sum += bet.Stake; return sum; }
    internal BetEntry? BetOf(ulong player) { foreach (var bet in Bets) if (bet.Player == player) return bet; return null; }
}

/// <summary>
/// Host-owned betting book shared with every peer as a snapshot: points per player, the markets of the current
/// round and their bets. Pari-mutuel: winners split the whole pool in proportion to their stakes; a market
/// nobody won, or one the round ended without deciding, refunds every stake. A bet placed less than
/// <see cref="LateSeconds"/> before the decision does not count and is refunded, so watching someone about to die
/// (or the ship about to leave) earns nothing.
/// </summary>
internal sealed class BettingState
{
    internal const int StartingPoints = 100, Floor = 10, TopUp = 20;
    internal const double LateSeconds = 5;
    internal static readonly int[] Stakes = { 10, 20, 50 };
    internal int Revision, NextMarketId = 1;
    internal readonly Dictionary<ulong, int> Points = new Dictionary<ulong, int>();
    internal readonly List<BetMarket> Markets = new List<BetMarket>();

    internal int PointsOf(ulong player) => Points.TryGetValue(player, out int points) ? points : StartingPoints;
    internal BetMarket? Find(int id) { foreach (var market in Markets) if (market.Id == id) return market; return null; }
    internal BetMarket? OpenMarket(BetMarketKind kind)
    { foreach (var market in Markets) if (market.Kind == kind && market.Status == BetMarketStatus.Open) return market; return null; }

    /// <summary>Host validation of one bet; the stake leaves the player's points at once.</summary>
    internal bool TryBet(ulong player, int marketId, int option, int stake, double now, out string reason)
    {
        var market = Find(marketId);
        reason = market == null ? "no such market" : market.Status != BetMarketStatus.Open ? "market closed"
            : option < 0 || option >= market.Options.Count ? "invalid option" : Array.IndexOf(Stakes, stake) < 0 ? "invalid stake"
            : market.BetOf(player) != null ? "already bet" : PointsOf(player) < stake ? "not enough points"
            : market.Kind == BetMarketKind.NextDeath && market.Options[option] == player ? "cannot bet on yourself" : string.Empty;
        if (reason.Length > 0) return false;
        Points[player] = PointsOf(player) - stake;
        market!.Bets.Add(new BetEntry(player, option, stake) { Placed = now });
        Revision++;
        return true;
    }

    internal BetMarket Open(BetMarketKind kind, IEnumerable<ulong> options)
    {
        var market = new BetMarket { Id = NextMarketId++, Kind = kind };
        market.Options.AddRange(options);
        Markets.Add(market); Revision++;
        return market;
    }

    internal void Settle(BetMarket market, int winner, double decidedAt) => Settle(market, new[] { winner }, decidedAt);

    /// <summary>
    /// Decides a market at host time <paramref name="decidedAt"/>: late bets are refunded, the others' pool goes to
    /// the bettors on any winning option (players who died together all count); nobody on them refunds everyone.
    /// The answer is kept either way.
    /// </summary>
    internal void Settle(BetMarket market, IReadOnlyList<int> winners, double decidedAt)
    {
        if (market.Status != BetMarketStatus.Open) return;
        market.Winners.Clear(); market.Winners.AddRange(winners);
        int pool = 0, winning = 0;
        foreach (var bet in market.Bets)
        {
            bet.Late = bet.Placed > decidedAt - LateSeconds;
            if (bet.Late) continue;
            pool += bet.Stake; if (market.Winners.Contains(bet.Option)) winning += bet.Stake;
        }
        if (winning == 0) { Refund(market); return; }
        market.Status = BetMarketStatus.Settled;
        int paid = 0; BetEntry? last = null;
        foreach (var bet in market.Bets)
        {
            if (bet.Late) { bet.Payout = bet.Stake; continue; }
            if (!market.Winners.Contains(bet.Option)) continue;
            bet.Payout = pool * bet.Stake / winning; paid += bet.Payout; last = bet;
        }
        // Integer division remainder goes to the last winner, so the pool is paid out exactly.
        last!.Payout += pool - paid;
        foreach (var bet in market.Bets) if (bet.Payout > 0) Points[bet.Player] = PointsOf(bet.Player) + bet.Payout;
        Revision++;
    }

    internal void Refund(BetMarket market)
    {
        if (market.Status != BetMarketStatus.Open) return;
        market.Status = BetMarketStatus.Refunded;
        foreach (var bet in market.Bets) { bet.Payout = bet.Stake; Points[bet.Player] = PointsOf(bet.Player) + bet.Stake; }
        Revision++;
    }

    /// <summary>
    /// A new round: earlier markets are dropped, players who have left drop off the leaderboard (points are kept by
    /// Netcode client id, and a player who rejoins gets a new one, so nothing could be carried over to them) and
    /// nearly broke players get a little back.
    /// </summary>
    internal void BeginRound(IEnumerable<ulong> players)
    {
        foreach (var market in Markets) Refund(market);
        Markets.Clear();
        var present = new HashSet<ulong>(players);
        foreach (ulong gone in new List<ulong>(Points.Keys)) if (!present.Contains(gone)) Points.Remove(gone);
        // Everyone joins the leaderboard; nearly broke players are topped up.
        foreach (ulong player in present) Points[player] = PointsOf(player) < Floor ? TopUp : PointsOf(player);
        Revision++;
    }
}
