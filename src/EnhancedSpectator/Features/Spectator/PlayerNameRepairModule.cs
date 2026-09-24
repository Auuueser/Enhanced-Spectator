using System;
using EnhancedSpectator.Config;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Runtime;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

internal sealed class PlayerNameRepairModule : IFeatureModule, IRuntimeTickable
{
    private readonly SpectatorCameraConfig _config;
    private readonly LethalCompanyPlayerNameRepair _adapter = new LethalCompanyPlayerNameRepair();
    private float _next;
    private bool _enabled;
    internal PlayerNameRepairModule(SpectatorCameraConfig config) => _config = config;
    public void Initialize() { }
    public void Tick()
    {
        bool enabled = _config.RepairPlayerNames.Value;
        if (enabled == _enabled && Time.unscaledTime < _next) return;
        _enabled = enabled;
        _next = Time.unscaledTime + 1f;
        try { _adapter.Refresh(enabled); }
        catch (Exception ex) { ModLog.Debug("Name repair deferred: " + ex.GetType().Name); }
    }
    public void Dispose() => _adapter.Dispose();
}
