using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Networking;
using EnhancedSpectator.Runtime;
using EnhancedSpectator.Features.SplitScreen;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Four-Hz retained spectator distribution; target switches refresh immediately.</summary>
public sealed class SpectatorRosterModule : IFeatureModule, IRuntimeLateTickable
{
    private readonly EnhancedSpectatorConfig _config;
    private readonly IEnhancedSpectatorNetworkService _network;
    private readonly IGameSpectatorRosterAdapter _adapter;
    private readonly SpectatorRoster _roster = new SpectatorRoster();
    private readonly List<SpectatorRosterPlayer> _players = new List<SpectatorRosterPlayer>(32);
    private readonly List<SpectatorTargetState> _targets = new List<SpectatorTargetState>(32);
    private SpectatorTargetState? _local;
    private float _next;
    private int? _fingerprint;

    /// <summary>Creates the spectator-only overlay using the existing target synchronization service.</summary>
    public SpectatorRosterModule(EnhancedSpectatorConfig config, IEnhancedSpectatorNetworkService network, IGameSpectatorRosterAdapter adapter)
    { _config = config; _network = network; _adapter = adapter; }
    /// <inheritdoc />
    public void Initialize() { }
    /// <inheritdoc />
    public void LateTick()
    {
        if (!_config.EnableEnhancedSpectator.Value || !RuntimeConnectionState.CanRunLocalDiagnostics(out _)
            || !_adapter.TryGetLocalTarget(out var local))
        { _adapter.SetVisible(false); _local = null; _fingerprint = null; return; }
        // The split-screen shows everyone as views, its audience row and viewer counts; the roster is for the
        // single view only.
        if (SplitScreenModule.Current?.Active == true) { _adapter.SetVisible(false); _local = null; _fingerprint = null; return; }
        if (_adapter.IsTogglePressed(_config.Camera.ToggleSpectatorRosterKey.Value))
            _config.Camera.ShowSpectatorRoster.Value = !_config.Camera.ShowSpectatorRoster.Value;
        if (!_config.Camera.ShowSpectatorRoster.Value) { _adapter.SetVisible(false); _local = null; _fingerprint = null; return; }
        _adapter.SetVisible(true);
        _adapter.UpdateInteraction(_config.Camera.ToggleSpectatorCursorKey.Value);
        bool changed = _local == null || !_local.Equals(local);
        if (!changed && Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + .25f; _local = local;
        _adapter.CopyPlayersTo(_players);
        if (_config.EnableNetworking.Value && _config.EnableSpectatorTargetSync.Value) _network.CopyRemoteSpectatorTargetsTo(_targets);
        else _targets.Clear();
        // Include actual player life/name/membership; target registry revisions alone miss resurrection and slot reuse.
        int fingerprint = local.GetHashCode();
        unchecked
        {
            foreach (var player in _players)
                fingerprint = fingerprint * 397 ^ HashCodeFor(player);
            foreach (var target in _targets) fingerprint = fingerprint * 397 ^ target.GetHashCode();
            fingerprint = fingerprint * 397 ^ (_config.UseChineseText ? 1 : 0);
        }
        if (_fingerprint == fingerprint && _adapter.IsViewReady) return;
        if (_fingerprint != fingerprint) _roster.Rebuild(_players, _targets, local);
        _fingerprint = fingerprint;
        _adapter.Present(_roster, local, _config.UseChineseText);
    }
    private static int HashCodeFor(SpectatorRosterPlayer player)
    { unchecked { return player.ClientId.GetHashCode() * 397 ^ player.SlotId.GetHashCode() ^ player.Name.GetHashCode() ^ (player.Dead ? 1 : 0); } }
    /// <inheritdoc />
    public void Dispose() { _adapter.Dispose(); _players.Clear(); _targets.Clear(); _local = null; _fingerprint = null; _next = 0; }
}
