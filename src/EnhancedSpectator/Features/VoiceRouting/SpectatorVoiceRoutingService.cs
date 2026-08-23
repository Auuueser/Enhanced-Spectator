using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.SpectatorPresence;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Networking;
using EnhancedSpectator.Runtime;
using UnityEngine;

namespace EnhancedSpectator.Features.VoiceRouting;

/// <summary>
/// Restores remote dead spectator voice playback for configured local modded listeners when explicitly enabled.
/// </summary>
public sealed class SpectatorVoiceRoutingService : IDisposable
{
    private const int RouteSkipDebugIntervalFrames = 120;

    private readonly EnhancedSpectatorConfig _config;
    private readonly IEnhancedSpectatorNetworkService _networkService;
    private readonly IGameSpectatorVoiceRoutingAdapter _adapter;
    private readonly ISpectatorVoiceMuteState _muteState;
    private readonly HashSet<ulong> _activeRoutes = new HashSet<ulong>();
    private readonly Dictionary<ulong, ulong> _activeSlots = new Dictionary<ulong, ulong>();
    private readonly HashSet<ulong> _desiredRoutes = new HashSet<ulong>();
    private readonly List<ulong> _routesToClear = new List<ulong>();
    private readonly List<SpectatorTargetState> _remoteTargets = new List<SpectatorTargetState>();
    private readonly Dictionary<ulong, RouteSkipDiagnostic> _lastRouteSkips =
        new Dictionary<ulong, RouteSkipDiagnostic>();
    private bool _disposed;

    /// <summary>
    /// Creates the spectator voice routing service.
    /// </summary>
    public SpectatorVoiceRoutingService(
        EnhancedSpectatorConfig config,
        IEnhancedSpectatorNetworkService networkService,
        IGameSpectatorVoiceRoutingAdapter adapter)
        : this(config, networkService, adapter, UnmutedSpectatorVoiceState.Instance)
    {
    }

    /// <summary>
    /// Creates the spectator voice routing service with an explicit local mute state.
    /// </summary>
    public SpectatorVoiceRoutingService(
        EnhancedSpectatorConfig config,
        IEnhancedSpectatorNetworkService networkService,
        IGameSpectatorVoiceRoutingAdapter adapter,
        ISpectatorVoiceMuteState muteState)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _muteState = muteState ?? throw new ArgumentNullException(nameof(muteState));
    }

    /// <summary>
    /// Applies current spectator voice routes.
    /// </summary>
    public void LateTick()
    {
        if (_disposed)
        {
            return;
        }

        if (!SpectatorVoiceMuteRules.ShouldEvaluateRoutes(
                _config.EnableSpectatorVoiceToTarget.Value,
                _muteState.IsMuted)
            || !RuntimeConnectionState.CanUseModNetworking(out _)
            || !_adapter.TryGetLocalVoiceReceiverState(
                out bool hasLocalPlayer,
                out bool isLocalPlayerDead,
                out ulong localClientId,
                out ulong localPlayerSlotId))
        {
            ClearAllRoutes();
            return;
        }

        if (!hasLocalPlayer || !_networkService.IsNetworkAvailable || !_networkService.IsTargetSyncEnabled)
        {
            ClearAllRoutes();
            return;
        }

        _desiredRoutes.Clear();
        _networkService.CopyRemoteSpectatorTargetsTo(_remoteTargets);
        if (_remoteTargets.Count == 0)
        {
            ClearRoutesNotIn(_desiredRoutes);
            return;
        }

        SpectatorVoiceAudienceMode audienceMode = _config.SpectatorVoiceAudienceMode.Value;
        SpectatorVoicePlaybackSettings playbackSettings = default;
        bool hasPlaybackSettings = false;
        foreach (SpectatorTargetState remoteTarget in _remoteTargets)
        {
            if (remoteTarget.LocalClientId == localClientId)
            {
                continue;
            }

            bool isWatchingLocalPlayer = RemoteSpectatorVisibilityRules.IsWatchingLocalPlayer(
                remoteTarget,
                localClientId,
                localPlayerSlotId);
            if (!SpectatorVoiceRoutingRules.ShouldRouteToLocalPlayer(
                featureEnabled: true,
                hasLocalPlayer,
                isLocalPlayerDead,
                remoteTarget.IsSpectating,
                isWatchingLocalPlayer,
                audienceMode))
            {
                continue;
            }

            if (!IsRemoteVoiceRoutingPeer(remoteTarget.LocalClientId))
            {
                DebugRouteSkipped(remoteTarget.LocalClientId, "remote peer did not advertise spectator voice routing capability");
                continue;
            }

            SpectatorPoseState? poseState = TryGetMatchingPose(remoteTarget);
            if (!hasPlaybackSettings)
            {
                playbackSettings = CreatePlaybackSettings();
                hasPlaybackSettings = true;
            }

            if (_adapter.TryApplySpectatorVoiceRoute(
                remoteTarget.LocalClientId,
                remoteTarget.LocalPlayerSlotId,
                poseState,
                playbackSettings,
                out string reason))
            {
                _desiredRoutes.Add(remoteTarget.LocalClientId);
                _activeSlots[remoteTarget.LocalClientId] = remoteTarget.LocalPlayerSlotId;
                _lastRouteSkips.Remove(remoteTarget.LocalClientId);
                if (_activeRoutes.Add(remoteTarget.LocalClientId))
                {
                    DebugRouteEnabled(remoteTarget, audienceMode);
                }

                continue;
            }

            DebugRouteSkipped(remoteTarget.LocalClientId, reason);
            if (_activeRoutes.Contains(remoteTarget.LocalClientId))
            {
                ClearRoute(remoteTarget.LocalClientId, reason);
            }
        }

        ClearRoutesNotIn(_desiredRoutes);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ClearAllRoutes();
        _disposed = true;
    }

    private void ClearRoutesNotIn(HashSet<ulong> desiredRoutes)
    {
        _routesToClear.Clear();
        foreach (ulong clientId in _activeRoutes)
        {
            if (!desiredRoutes.Contains(clientId))
            {
                _routesToClear.Add(clientId);
            }
        }

        foreach (ulong clientId in _routesToClear)
        {
            ClearRoute(clientId, "presence lost");
        }

        _routesToClear.Clear();
    }

    private void ClearAllRoutes()
    {
        if (_activeRoutes.Count == 0)
        {
            _activeSlots.Clear();
            _lastRouteSkips.Clear();
            _remoteTargets.Clear();
            _adapter.ClearCachedVoiceRouteLookups();
            return;
        }

        _routesToClear.Clear();
        _routesToClear.AddRange(_activeRoutes);
        foreach (ulong clientId in _routesToClear)
        {
            ClearRoute(clientId, "routing disabled or lifecycle unavailable");
        }

        _routesToClear.Clear();
        _activeSlots.Clear();
        _lastRouteSkips.Clear();
        _remoteTargets.Clear();
        _adapter.ClearCachedVoiceRouteLookups();
    }

    private void ClearRoute(ulong clientId, string reason)
    {
        if (!_activeRoutes.Remove(clientId))
        {
            return;
        }

        ulong slotId = _activeSlots.TryGetValue(clientId, out ulong storedSlotId) ? storedSlotId : clientId;
        _activeSlots.Remove(clientId);
        _adapter.ClearSpectatorVoiceRoute(clientId, slotId);
        _lastRouteSkips.Remove(clientId);
        if (_activeRoutes.Count == 0)
        {
            _adapter.ClearCachedVoiceRouteLookups();
        }

        DebugRouteCleared(clientId, reason);
    }

    private bool ShouldLogDebug()
    {
        return ModLog.IsDebugEnabled && _config.EnableDebugLogging.Value && _config.DebugSpectatorVoiceRouting.Value;
    }

    private void DebugRouteEnabled(SpectatorTargetState remoteTarget, SpectatorVoiceAudienceMode audienceMode)
    {
        if (!ShouldLogDebug())
        {
            return;
        }

        ModLog.Debug(
            $"Spectator voice route enabled: spectatorClient={remoteTarget.LocalClientId}, spectatorSlot={remoteTarget.LocalPlayerSlotId}, audienceMode={audienceMode}.");
    }

    private void DebugRouteCleared(ulong clientId, string reason)
    {
        if (!ShouldLogDebug())
        {
            return;
        }

        ModLog.Debug($"Spectator voice route cleared: spectatorClient={clientId}, reason={reason}.");
    }

    private void DebugRouteSkipped(ulong clientId, string reason)
    {
        if (!ShouldLogDebug())
        {
            return;
        }

        int frame = Time.frameCount;
        if (_lastRouteSkips.TryGetValue(clientId, out RouteSkipDiagnostic previous)
            && previous.Reason == reason
            && frame - previous.Frame < RouteSkipDebugIntervalFrames)
        {
            return;
        }

        _lastRouteSkips[clientId] = new RouteSkipDiagnostic(frame, reason);
        ModLog.Debug($"Spectator voice route skipped: spectatorClient={clientId}, reason={reason}.");
    }

    private bool IsRemoteVoiceRoutingPeer(ulong clientId)
    {
        return _networkService.TryGetPeerCapability(clientId, out ModPeerCapability capability)
            && ModPeerCapabilityRules.SupportsCurrentSpectatorVoiceToTarget(capability);
    }

    private SpectatorVoicePlaybackSettings CreatePlaybackSettings()
    {
        return new SpectatorVoicePlaybackSettings(
            Mathf.Clamp01(_config.SpectatorVoiceToTargetVolume.Value),
            _config.SpectatorVoiceUseRemotePosePosition.Value,
            _config.SpectatorVoiceEnableDistanceAttenuation.Value,
            _config.SpectatorVoiceMinDistance.Value,
            _config.SpectatorVoiceMaxDistance.Value,
            _config.SpectatorVoiceRolloffPower.Value,
            _config.SpectatorVoiceMinimumVolume.Value,
            _config.SpectatorVoiceFallbackTo2DWhenPoseMissing.Value);
    }

    private SpectatorPoseState? TryGetMatchingPose(SpectatorTargetState remoteTarget)
    {
        if (!_networkService.TryGetRemoteSpectatorPose(remoteTarget.LocalClientId, out SpectatorPoseState poseState))
        {
            return null;
        }

        if (!poseState.IsSpectating
            || poseState.TargetClientId != remoteTarget.TargetClientId
            || poseState.TargetPlayerSlotId != remoteTarget.TargetPlayerSlotId)
        {
            return null;
        }

        return poseState;
    }

    private readonly struct RouteSkipDiagnostic
    {
        public RouteSkipDiagnostic(int frame, string reason)
        {
            Frame = frame;
            Reason = reason;
        }

        public int Frame { get; }

        public string Reason { get; }
    }
}
