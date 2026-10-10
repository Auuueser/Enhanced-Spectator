using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace EnhancedSpectator.Features.Social;

/// <summary>
/// Dead players tag living teammates during a round; the host keeps the tallies private and, when the ship is
/// back in orbit, sends everyone the round's summary. Each client remembers only its own choices.
/// </summary>
internal sealed class RatingService
{
    private readonly SocialNetworkService _net;
    private readonly RatingState _host = new RatingState();
    private readonly RatingState _mine = new RatingState();
    private readonly List<(ulong ClientId, string Name, bool Alive)> _players = new List<(ulong, string, bool)>(32);
    private bool _hostRound, _localRound;

    /// <summary>The finished round's summary: every rated player with their tag counts.</summary>
    internal event Action<List<(ulong Target, int[] Counts)>>? Summary;

    internal RatingService(SocialNetworkService net) { _net = net; _net.BettingPacket += OnPacket; _net.Connected += Reset; }

    // Reviews belong to one game (one connection): leaving mid-round and hosting or joining another starts clean.
    private void Reset() { _host.Clear(); _mine.Clear(); _hostRound = _localRound = false; }

    internal bool CanRate => _net.Ready && _net.Game.RoundInProgress && _net.Game.IsDead(_net.LocalClientId);
    internal ulong LocalClientId => _net.LocalClientId;
    internal string NameOf(ulong clientId) => _net.Game.NameOf(clientId);
    /// <summary>The local player's own choices this round.</summary>
    internal RatingState Mine => _mine;

    /// <summary>Living teammates who can be rated now, in slot order.</summary>
    internal void CopyTargetsTo(List<(ulong ClientId, string Name, bool Alive)> output)
    {
        _net.Game.CopyPlayersTo(output);
        output.RemoveAll(player => !player.Alive || player.ClientId == _net.LocalClientId);
    }

    internal void Tick()
    {
        var game = _net.Game;
        // Each player's own choices belong to one round.
        if (game.RoundInProgress && !_localRound) { _localRound = true; _mine.Clear(); }
        if (game.InShipPhase) _localRound = false;
        if (!_net.Ready || !_net.IsHost) return;
        if (!_hostRound && game.RoundInProgress) { _hostRound = true; _host.Clear(); }
        if (_hostRound && game.InShipPhase)
        {
            _hostRound = false;
            var summary = Collect();
            if (summary.Count == 0) return;
            _net.SendBetting(SocialNetworkService.Kind.RateSummary, w => Write(w, summary));
            Summary?.Invoke(summary);
        }
    }

    /// <summary>Gives or withdraws one tag; false when it cannot be given now.</summary>
    internal bool Toggle(ulong target, int tag)
    {
        if (!CanRate) return false;
        ulong local = _net.LocalClientId;
        bool on = !_mine.Has(local, target, tag);
        if (!_mine.Toggle(local, target, tag, on)) return false;
        if (_net.IsHost) _host.Toggle(local, target, tag, on);
        else _net.SendBetting(SocialNetworkService.Kind.RateRequest, w => { w.WriteValueSafe(target); w.WriteValueSafe(tag); w.WriteValueSafe(on); });
        return true;
    }

    private void OnPacket(ulong sender, SocialNetworkService.Kind kind, FastBufferReader reader)
    {
        if (kind == SocialNetworkService.Kind.RateRequest)
        {
            reader.ReadValueSafe(out ulong target); reader.ReadValueSafe(out int tag); reader.ReadValueSafe(out bool on);
            // Only dead players rate, only during the round, and only teammates still taking part.
            if (_hostRound && _net.Game.IsDead(sender) && _net.Game.TryGetPlayer(target, out _)) _host.Toggle(sender, target, tag, on);
        }
        else if (kind == SocialNetworkService.Kind.RateSummary)
        {
            reader.ReadValueSafe(out int count);
            var summary = new List<(ulong, int[])>();
            for (int i = 0; i < Math.Min(count, 64); i++)
            {
                reader.ReadValueSafe(out ulong target);
                var counts = new int[RatingState.TagCount];
                for (int tag = 0; tag < counts.Length; tag++) reader.ReadValueSafe(out counts[tag]);
                summary.Add((target, counts));
            }
            Summary?.Invoke(summary);
        }
    }

    private List<(ulong Target, int[] Counts)> Collect()
    {
        var summary = new List<(ulong, int[])>();
        foreach (var pair in _host.Tallies)
        {
            int total = 0; foreach (int count in pair.Value) total += count;
            if (total > 0) summary.Add((pair.Key, (int[])pair.Value.Clone()));
        }
        summary.Sort((a, b) => _net.Game.SlotOf(a.Item1).CompareTo(_net.Game.SlotOf(b.Item1)));
        return summary;
    }

    private static void Write(FastBufferWriter w, List<(ulong Target, int[] Counts)> summary)
    {
        w.WriteValueSafe(summary.Count);
        foreach (var entry in summary) { w.WriteValueSafe(entry.Target); foreach (int count in entry.Counts) w.WriteValueSafe(count); }
    }
}
