using System;
using System.Collections.Generic;
using EnhancedSpectator.Logging;
using Unity.Netcode;
using UnityEngine;

namespace EnhancedSpectator.Features.Social;

/// <summary>
/// The host runs the betting book from the live game: a round opens "who dies next" (re-opened after every death)
/// and "does anyone get out alive"; deaths and the ship's return settle them. Dead players bet through the host,
/// which checks death, points and stakes, then sends the whole book to every peer.
/// </summary>
internal sealed class BettingService
{
    private const int KeptResolved = 4;
    private readonly SocialNetworkService _net;
    private readonly List<(ulong ClientId, string Name, bool Alive)> _players = new List<(ulong, string, bool)>(32);
    private BettingRound _round = new BettingRound();
    private readonly BetResolutionTracker _resolutions = new BetResolutionTracker();
    private readonly List<BetMarket> _resolved = new List<BetMarket>();

    internal BettingState State { get; private set; } = new BettingState();
    /// <summary>A market was decided (settled or refunded); raised once per market on every client.</summary>
    internal event Action<BetMarket>? Resolved;

    internal BettingService(SocialNetworkService net) { _net = net; _net.BettingPacket += OnPacket; _net.Connected += Reset; }

    // Points belong to one game (one connection): hosting or joining another starts from scratch. A joining client
    // then asks the host, which answers with the current book, so a late joiner sees it at once.
    private void Reset() { State = new BettingState(); _round = new BettingRound(); _resolutions.Clear(); }

    internal bool Ready => _net.Ready;
    internal ulong LocalClientId => _net.LocalClientId;
    internal bool LocalDead => _net.Game.IsDead(_net.LocalClientId);
    internal string NameOf(ulong clientId) => _net.Game.NameOf(clientId);

    internal void Tick()
    {
        if (!_net.Ready || !_net.IsHost) return;
        // Turned off by the host: open bets are returned and no round runs until it is turned on again.
        if (!_net.BetsEnabled) { if (_round.Open) StopRound(); return; }
        var game = _net.Game;
        game.CopyPlayersTo(_players);
        bool changed = false;
        double now = Time.realtimeSinceStartupAsDouble;
        if (!_round.Open && game.RoundInProgress) changed = _round.Begin(State, _players);
        if (_round.Open)
        {
            changed |= _round.Update(State, _players, now, game.ShipLeaving);
            if (game.InShipPhase) { _round.Finish(State, now); changed = true; }
        }
        if (changed) { Prune(); Broadcast(); }
    }

    private void StopRound()
    {
        _round = new BettingRound();
        foreach (var market in State.Markets) if (market.Status == BetMarketStatus.Open) State.Refund(market);
        Prune(); Broadcast();
    }

    /// <summary>Places a bet for the local player (dead players only; the host checks again).</summary>
    internal void PlaceBet(int marketId, int option, int stake)
    {
        if (!_net.BetsEnabled || !LocalDead) return;
        if (_net.IsHost)
        {
            if (State.TryBet(_net.LocalClientId, marketId, option, stake, Time.realtimeSinceStartupAsDouble, out string reason)) Broadcast();
            else ModLog.Debug("Bet rejected: " + reason);
            return;
        }
        _net.SendBetting(SocialNetworkService.Kind.BetRequest, w => { w.WriteValueSafe(marketId); w.WriteValueSafe(option); w.WriteValueSafe(stake); });
    }

    // Open markets plus the last few results keep the snapshot small.
    private void Prune()
    {
        int resolved = 0;
        for (int i = State.Markets.Count - 1; i >= 0; i--)
            if (State.Markets[i].Status != BetMarketStatus.Open && ++resolved > KeptResolved) State.Markets.RemoveAt(i);
    }

    private void Broadcast()
    {
        _net.SendBetting(SocialNetworkService.Kind.BetState, Write);
        RaiseResolved();
    }

    private void OnPacket(ulong sender, SocialNetworkService.Kind kind, FastBufferReader reader)
    {
        switch (kind)
        {
            case SocialNetworkService.Kind.Hello:
                _net.SendBetting(SocialNetworkService.Kind.BetState, Write, sender); break;
            case SocialNetworkService.Kind.BetRequest:
                reader.ReadValueSafe(out int market); reader.ReadValueSafe(out int option); reader.ReadValueSafe(out int stake);
                if (_net.BetsEnabled && _net.Game.IsDead(sender) && State.TryBet(sender, market, option, stake, Time.realtimeSinceStartupAsDouble, out _)) Broadcast();
                break;
            case SocialNetworkService.Kind.BetState:
                State = Read(reader); RaiseResolved(); break;
        }
    }

    // Each client learns of a decision once, when a market it knew as open (or a new one) arrives resolved.
    private void RaiseResolved()
    {
        _resolutions.CopyNewlyResolvedTo(State, _resolved);
        foreach (var market in _resolved) Resolved?.Invoke(market);
    }

    private void Write(FastBufferWriter w)
    {
        w.WriteValueSafe(State.Revision); w.WriteValueSafe(State.NextMarketId);
        w.WriteValueSafe(State.Points.Count);
        foreach (var pair in State.Points) { w.WriteValueSafe(pair.Key); w.WriteValueSafe(pair.Value); }
        w.WriteValueSafe(State.Markets.Count);
        foreach (var market in State.Markets)
        {
            w.WriteValueSafe(market.Id); w.WriteValueSafe((byte)market.Kind); w.WriteValueSafe((byte)market.Status);
            w.WriteValueSafe(market.Winners.Count); foreach (int winner in market.Winners) w.WriteValueSafe(winner);
            w.WriteValueSafe(market.Options.Count); foreach (ulong option in market.Options) w.WriteValueSafe(option);
            w.WriteValueSafe(market.Bets.Count);
            foreach (var bet in market.Bets) { w.WriteValueSafe(bet.Player); w.WriteValueSafe(bet.Option); w.WriteValueSafe(bet.Stake); w.WriteValueSafe(bet.Payout); w.WriteValueSafe(bet.Late); }
        }
    }

    // Every count is read in full, exactly as written; the book is never cut short on the way.
    private static BettingState Read(FastBufferReader r)
    {
        var state = new BettingState();
        r.ReadValueSafe(out state.Revision); r.ReadValueSafe(out state.NextMarketId);
        r.ReadValueSafe(out int points);
        for (int i = 0; i < points; i++) { r.ReadValueSafe(out ulong player); r.ReadValueSafe(out int value); state.Points[player] = value; }
        r.ReadValueSafe(out int markets);
        for (int m = 0; m < markets; m++)
        {
            var market = new BetMarket();
            r.ReadValueSafe(out market.Id); r.ReadValueSafe(out byte kind); r.ReadValueSafe(out byte status);
            r.ReadValueSafe(out int winners);
            for (int i = 0; i < winners; i++) { r.ReadValueSafe(out int winner); market.Winners.Add(winner); }
            market.Kind = (BetMarketKind)kind; market.Status = (BetMarketStatus)status;
            r.ReadValueSafe(out int options);
            for (int i = 0; i < options; i++) { r.ReadValueSafe(out ulong option); market.Options.Add(option); }
            r.ReadValueSafe(out int bets);
            for (int i = 0; i < bets; i++)
            {
                r.ReadValueSafe(out ulong player); r.ReadValueSafe(out int option); r.ReadValueSafe(out int stake); r.ReadValueSafe(out int payout);
                r.ReadValueSafe(out bool late);
                market.Bets.Add(new BetEntry(player, option, stake) { Payout = payout, Late = late });
            }
            state.Markets.Add(market);
        }
        return state;
    }
}
