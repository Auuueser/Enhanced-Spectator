using System;
using EnhancedSpectator.Features;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Switches away from a disconnected watched player after vanilla disconnect cleanup.
/// </summary>
public sealed class SpectatorDisconnectTargetSwitchService : IFeatureModule, IRuntimeTickable
{
    private const float TargetValidationIntervalSeconds = 0.25f;

    private readonly IGameSpectatorTargetSwitchAdapter _adapter;
    private NetworkManager? _subscribedNetworkManager;
    private bool _initialized;
    private bool _pendingDisconnectSwitch;
    private float _nextTargetValidationTime;

    /// <summary>
    /// Creates the disconnect target-switch service.
    /// </summary>
    public SpectatorDisconnectTargetSwitchService(IGameSpectatorTargetSwitchAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    /// <summary>
    /// Initializes lifecycle subscriptions.
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        SpectatorLifecycleEvents.Changed += OnSpectatorLifecycleChanged;
        RefreshDisconnectSubscription();
        _initialized = true;
    }

    /// <inheritdoc />
    public void Tick()
    {
        if (!_initialized)
        {
            return;
        }

        RefreshDisconnectSubscription();
        if (!_pendingDisconnectSwitch && Time.unscaledTime < _nextTargetValidationTime)
        {
            return;
        }

        _pendingDisconnectSwitch = false;
        _nextTargetValidationTime = Time.unscaledTime + TargetValidationIntervalSeconds;
        TrySwitchAwayFromInvalidTarget();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        SpectatorLifecycleEvents.Changed -= OnSpectatorLifecycleChanged;
        UnsubscribeDisconnectCallback();
        _pendingDisconnectSwitch = false;
        _nextTargetValidationTime = 0f;
        _initialized = false;
    }

    private void OnSpectatorLifecycleChanged(SpectatorLifecycleEventKind kind)
    {
        if (kind != SpectatorLifecycleEventKind.Revived)
        {
            return;
        }

        _pendingDisconnectSwitch = false;
    }

    private void RefreshDisconnectSubscription()
    {
        NetworkManager? manager = NetworkManager.Singleton;
        if (ReferenceEquals(manager, _subscribedNetworkManager))
        {
            return;
        }

        UnsubscribeDisconnectCallback();
        if (manager == null)
        {
            return;
        }

        manager.OnClientDisconnectCallback += OnClientDisconnected;
        _subscribedNetworkManager = manager;
    }

    private void UnsubscribeDisconnectCallback()
    {
        if (_subscribedNetworkManager == null)
        {
            return;
        }

        _subscribedNetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        _subscribedNetworkManager = null;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _ = clientId;
        _pendingDisconnectSwitch = true;
    }

    private void TrySwitchAwayFromInvalidTarget()
    {
        if (RuntimeConnectionState.ShouldSkipVanillaSpectatorTargetSwitch(out string reason))
        {
            if (ModLog.IsDebugEnabled)
            {
                ModLog.Debug($"Skipped disconnect spectator target auto-switch: {reason}.");
            }

            return;
        }

        if (!_adapter.TryGetDisconnectTargetSwitchContext(out SpectatorDisconnectTargetSwitchContext context))
        {
            return;
        }

        if (!SpectatorDisconnectTargetSwitchRules.ShouldAutoSwitch(
            context.IsLocalPlayerDead,
            context.HasSpectateCamera,
            context.GameOverOverrideActive,
            context.HasCurrentTarget,
            context.CurrentTargetIsValid,
            context.CurrentTargetDisconnected,
            context.HasReplacementTarget))
        {
            return;
        }

        if (_adapter.TrySwitchToNextSpectatorTarget())
        {
            ModLog.Debug("Switched spectator target after watched player disconnected.");
        }
    }
}
