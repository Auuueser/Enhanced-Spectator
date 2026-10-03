using System;
using System.Collections.Generic;
using EnhancedSpectator.Networking;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>A connected player identity captured through GameInterop.</summary>
public readonly struct SpectatorRosterPlayer
{
    /// <summary>Creates a connected player record.</summary>
    public SpectatorRosterPlayer(ulong clientId, ulong slotId, string name, bool dead)
    { ClientId = clientId; SlotId = slotId; Name = name; Dead = dead; }
    /// <summary>Authoritative network identity.</summary>
    public ulong ClientId { get; }
    /// <summary>Current player slot; never interchangeable with a network ID.</summary>
    public ulong SlotId { get; }
    /// <summary>Current display name.</summary>
    public string Name { get; }
    /// <summary>Whether the player is dead.</summary>
    public bool Dead { get; }
}

/// <summary>A connected identity and its watch relationships; only alive identities become display cards.</summary>
public sealed class SpectatorRosterRow
{
    private readonly List<SpectatorRosterPlayer> _viewers = new List<SpectatorRosterPlayer>();
    internal SpectatorRosterRow(SpectatorRosterPlayer player) { Player = player; }
    /// <summary>Connected player identity.</summary>
    public SpectatorRosterPlayer Player { get; }
    /// <summary>Known viewers, including the local spectator.</summary>
    public int Viewers { get; internal set; }
    /// <summary>Known spectators watching this alive player; remote vanilla targets cannot be inferred.</summary>
    public IReadOnlyList<SpectatorRosterPlayer> ViewerPlayers => _viewers;
    internal void AddViewer(SpectatorRosterPlayer player) { Viewers++; _viewers.Add(player); }
    internal void SortViewers() => _viewers.Sort((a, b) => a.SlotId.CompareTo(b.SlotId));
    /// <summary>Whether this dead player's target state is known.</summary>
    public bool TargetKnown { get; internal set; }
    /// <summary>Connected watched player, when selected.</summary>
    public SpectatorRosterPlayer? Target { get; internal set; }
}

/// <summary>Reusable identity-based aggregation; no new networking messages or game objects.</summary>
public sealed class SpectatorRoster
{
    /// <summary>Maximum number of player cards shown on the spectator HUD.</summary>
    public const int DisplayLimit = 32;
    private readonly Dictionary<ulong, SpectatorRosterRow> _clients = new Dictionary<ulong, SpectatorRosterRow>();
    private readonly Dictionary<ulong, SpectatorRosterRow> _slots = new Dictionary<ulong, SpectatorRosterRow>();
    private readonly Dictionary<ulong, SpectatorTargetState> _states = new Dictionary<ulong, SpectatorTargetState>();
    private readonly List<SpectatorRosterRow> _rows = new List<SpectatorRosterRow>(DisplayLimit);
    /// <summary>Stable slot-ordered visible rows.</summary>
    public IReadOnlyList<SpectatorRosterRow> Rows => _rows;
    /// <summary>Connected dead players, whether or not their target is synchronized.</summary>
    public int Spectators { get; private set; }
    /// <summary>Dead players whose watching target cannot be determined reliably.</summary>
    public int UnknownTargets { get; private set; }
    /// <summary>Players beyond the 32-card display bound.</summary>
    public int HiddenPlayers { get; private set; }
    /// <summary>Known spectators watching the current local target.</summary>
    public int CurrentViewers { get; private set; }

    /// <summary>Rebuilds on a bounded HUD update, filtering disconnects, stale slots and dead targets.</summary>
    public void Rebuild(IReadOnlyList<SpectatorRosterPlayer> players,
        IReadOnlyList<SpectatorTargetState> targets, SpectatorTargetState? local)
    {
        _clients.Clear(); _slots.Clear(); _states.Clear(); _rows.Clear();
        Spectators = UnknownTargets = HiddenPlayers = CurrentViewers = 0;
        foreach (SpectatorRosterPlayer player in players)
        {
            if (_clients.ContainsKey(player.ClientId) || _slots.ContainsKey(player.SlotId)) continue;
            var row = new SpectatorRosterRow(player);
            _clients[player.ClientId] = row; _slots[player.SlotId] = row;
            if (player.Dead) Spectators++;
        }
        foreach (SpectatorTargetState state in targets)
            if (!_states.TryGetValue(state.LocalClientId, out var old) || state.TimestampTicks >= old.TimestampTicks)
                _states[state.LocalClientId] = state;
        if (local != null) _states[local.LocalClientId] = local;
        foreach (SpectatorRosterRow row in _clients.Values)
        {
            if (!row.Player.Dead) continue;
            if (!_states.TryGetValue(row.Player.ClientId, out var state)
                || state.LocalPlayerSlotId != row.Player.SlotId || !state.IsSpectating)
            { UnknownTargets++; continue; }
            SpectatorRosterRow? target = ResolveTarget(state);
            row.TargetKnown = !state.TargetClientId.HasValue && !state.TargetPlayerSlotId.HasValue
                || target != null && !target.Player.Dead && target.Player.ClientId != row.Player.ClientId;
            if (!row.TargetKnown) { UnknownTargets++; continue; }
            if (target != null)
            { row.Target = target.Player; target.AddViewer(row.Player); }
        }
        foreach (SpectatorRosterRow row in _clients.Values)
            if (!row.Player.Dead) { row.SortViewers(); _rows.Add(row); }
        _rows.Sort((a, b) => a.Player.SlotId.CompareTo(b.Player.SlotId));
        if (local != null) CurrentViewers = ResolveTarget(local)?.Viewers ?? 0;
        if (_rows.Count > DisplayLimit)
        { HiddenPlayers = _rows.Count - DisplayLimit; _rows.RemoveRange(DisplayLimit, HiddenPlayers); }
    }

    private SpectatorRosterRow? ResolveTarget(SpectatorTargetState state)
    {
        if (state.TargetClientId.HasValue)
            return _clients.TryGetValue(state.TargetClientId.Value, out var client) ? client : null;
        return state.TargetPlayerSlotId.HasValue && _slots.TryGetValue(state.TargetPlayerSlotId.Value, out var slot) ? slot : null;
    }
}

/// <summary>Compact card layout and plain-text presentation for up to 32 players.</summary>
public static class SpectatorRosterPresentation
{
    /// <summary>Uses at most eight rows, expanding horizontally before shrinking text.</summary>
    public static int Columns(int count) => count <= 4 ? 1 : count <= 16 ? 2 : 4;
    /// <summary>Maximum eight rows for the supported 32-player view.</summary>
    public static int RowCount(int count) => Math.Max(1, (Math.Min(32, Math.Max(0, count)) + Columns(count) - 1) / Columns(count));
    /// <summary>Bounds player names and flattens multiline text; the renderer disables rich text.</summary>
    public static string Name(string value)
    {
        string name = (value ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ').Trim();
        if (name.Length > 64) name = name.Substring(0, char.IsHighSurrogate(name[63]) ? 63 : 64);
        return string.IsNullOrEmpty(name) ? "?" : name;
    }
    /// <summary>Shows a lower bound rather than fabricating exact counts when targets are unknown.</summary>
    public static string Count(int count, bool incomplete, bool chinese) =>
        (incomplete ? "≥" : string.Empty) + count + (chinese ? " 人观看" : " watching");
    /// <summary>Compact per-card viewer count, right-aligned beside the player name.</summary>
    public static string Badge(int count, bool incomplete, bool chinese) =>
        (incomplete ? "≥" : string.Empty) + count + (chinese ? " 人" : count == 1 ? " viewer" : " viewers");
    /// <summary>Groups outgoing spectator relationships under alive targets without retaining dead player cards.</summary>
    public static string Watching(SpectatorRosterRow row, bool chinese)
    {
        if (row.ViewerPlayers.Count == 0) return chinese ? "暂无观众" : "No viewers";
        string text = (chinese ? "观众：" : "Viewers: ") + Name(row.ViewerPlayers[0].Name);
        if (row.ViewerPlayers.Count > 1) text += (chinese ? "、" : ", ") + Name(row.ViewerPlayers[1].Name);
        if (row.ViewerPlayers.Count > 2) text += chinese ? $" 等 {row.Viewers} 人" : $" +{row.Viewers - 2}";
        return text;
    }
}
