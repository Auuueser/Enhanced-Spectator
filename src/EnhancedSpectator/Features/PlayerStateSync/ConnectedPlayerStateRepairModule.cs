using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Networking;
using EnhancedSpectator.Runtime;
using UnityEngine;

namespace EnhancedSpectator.Features.PlayerStateSync;

/// <summary>
/// Periodically repairs late vanilla connected-player state for already identified modded peers.
/// </summary>
public sealed class ConnectedPlayerStateRepairModule : IFeatureModule, IRuntimeTickable
{
    private const float RepairIntervalSeconds = 0.5f;
    private const float IdleFallbackRepairIntervalSeconds = 5f;

    private readonly EnhancedSpectatorConfig _config;
    private readonly IEnhancedSpectatorNetworkService _networkService;
    private readonly IConnectedPlayerStateRepairAdapter _repairAdapter;
    private readonly List<PeerIdentityState> _identityScratch = new List<PeerIdentityState>();
    private readonly List<SpectatorTargetState> _targetScratch = new List<SpectatorTargetState>();

    private float _nextRepairTime;
    private float _nextIdleFallbackRepairTime;
    private int _lastIdentityRevision = -1;
    private int _lastTargetRevision = -1;
    private bool _repeatAfterRepair;
    private bool _initialized;

    /// <summary>
    /// Creates a connected player state repair module.
    /// </summary>
    public ConnectedPlayerStateRepairModule(
        EnhancedSpectatorConfig config,
        IEnhancedSpectatorNetworkService networkService,
        IConnectedPlayerStateRepairAdapter repairAdapter)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
        _repairAdapter = repairAdapter ?? throw new ArgumentNullException(nameof(repairAdapter));
    }

    /// <inheritdoc />
    public void Initialize()
    {
        _initialized = true;
        _nextRepairTime = 0f;
        _nextIdleFallbackRepairTime = 0f;
        _lastIdentityRevision = -1;
        _lastTargetRevision = -1;
        _repeatAfterRepair = false;
    }

    /// <inheritdoc />
    public void Tick()
    {
        if (!_initialized)
        {
            return;
        }

        if (!_config.EnableNetworking.Value || !_config.RepairVanillaConnectedPlayerState.Value)
        {
            ResetSchedule();
            return;
        }

        float now = Time.unscaledTime;
        int currentIdentityRevision = _networkService.RemotePeerIdentityRevision;
        int currentTargetRevision = _networkService.RemoteSpectatorTargetRevision;
        if (!ConnectedPlayerStateRepairScheduleRules.ShouldRunRepair(
                _initialized,
                enabled: true,
                now,
                _nextRepairTime,
                _lastIdentityRevision,
                currentIdentityRevision,
                _lastTargetRevision,
                currentTargetRevision,
                _nextIdleFallbackRepairTime,
                _repeatAfterRepair))
        {
            return;
        }

        _nextRepairTime = now + RepairIntervalSeconds;
        if (!RuntimeConnectionState.CanRepairVanillaPlayerState(out _))
        {
            return;
        }

        _networkService.CopyRemotePeerIdentitiesTo(_identityScratch);
        _networkService.CopyRemoteSpectatorTargetsTo(_targetScratch);
        int repairs = _repairAdapter.RepairConnectedPlayerState(
            _identityScratch,
            _targetScratch,
            updatePlayerNames: _config.RepairVanillaPlayerNames.Value,
            updateQuickMenu: true,
            debug: _config.DebugPlayerStateRepair.Value,
            out _);

        _lastIdentityRevision = currentIdentityRevision;
        _lastTargetRevision = currentTargetRevision;
        _repeatAfterRepair = repairs > 0;
        _nextIdleFallbackRepairTime = now
            + (repairs > 0 ? RepairIntervalSeconds : IdleFallbackRepairIntervalSeconds);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _initialized = false;
        ResetSchedule();
    }

    private void ResetSchedule()
    {
        _nextRepairTime = 0f;
        _nextIdleFallbackRepairTime = 0f;
        _lastIdentityRevision = -1;
        _lastTargetRevision = -1;
        _repeatAfterRepair = false;
    }
}
