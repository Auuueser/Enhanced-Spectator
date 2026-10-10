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
    private readonly List<(ulong ClientId, ulong SlotId)> _deadPlayers = new List<(ulong ClientId, ulong SlotId)>();
    private readonly HashSet<ulong> _handled = new HashSet<ulong>();
    private readonly Dictionary<ulong, RouteSkipDiagnostic> _lastRouteSkips =
        new Dictionary<ulong, RouteSkipDiagnostic>();
    private bool _disposed;
    private ulong _localClientId;
    private readonly Func<ulong, ulong?> _followingOf;

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
        _followingOf = id => id == _localClientId ? SplitScreen.SplitScreenModule.LocalFollowing
            : _networkService.TryGetRemoteSpectatorPose(id, out SpectatorPoseState pose) ? pose.FollowingClientId : null;
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

        if (!hasLocalPlayer)
        {
            ClearAllRoutes();
            return;
        }

        _desiredRoutes.Clear();
        _handled.Clear();
        // A local split-screen watcher must mute vanilla dead players even when nobody else has the mod.
        // Only remote mod routes require a compatible peer; local playback ownership does not.
        if (_networkService.IsNetworkAvailable && _networkService.IsTargetSyncEnabled)
            _networkService.CopyRemoteSpectatorTargetsTo(_remoteTargets);
        else _remoteTargets.Clear();
        // Split-screen: the audience and the watchers of enlarged views hear each other apart (see SplitScreenPresenceRules).
        SpectatorSplitView listener = isLocalPlayerDead ? SplitScreen.SplitScreenModule.LocalView : SpectatorSplitView.None;
        _localClientId = localClientId;
        ulong localLeader = SpectatorPartyRules.Leader(localClientId, _followingOf);

        SpectatorVoiceAudienceMode audienceMode = _config.SpectatorVoiceAudienceMode.Value;
        foreach (SpectatorTargetState remoteTarget in _remoteTargets)
        {
            if (remoteTarget.LocalClientId == localClientId)
            {
                continue;
            }

            _handled.Add(remoteTarget.LocalClientId);
            SplitVoiceRoute split = SplitScreenPresenceRules.Voice(
                _networkService.TryGetRemoteSpectatorPose(remoteTarget.LocalClientId, out SpectatorPoseState latest) ? latest.SplitView : SpectatorSplitView.None,
                listener, SpectatorPartyRules.Leader(remoteTarget.LocalClientId, _followingOf) == localLeader);
            if (split == SplitVoiceRoute.Vanilla) continue;
            if (split == SplitVoiceRoute.Muted)
            {
                Mute(remoteTarget.LocalClientId, remoteTarget.LocalPlayerSlotId);
                continue;
            }

            bool isWatchingLocalPlayer = RemoteSpectatorVisibilityRules.IsWatchingLocalPlayer(
                remoteTarget,
                localClientId,
                localPlayerSlotId);
            if (split == SplitVoiceRoute.Configured && !SpectatorVoiceRoutingRules.ShouldRouteToLocalPlayer(
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
                if (split != SplitVoiceRoute.Configured) Mute(remoteTarget.LocalClientId, remoteTarget.LocalPlayerSlotId);
                continue;
            }

            SpectatorPoseState? poseState = TryGetMatchingPose(remoteTarget);
            SpectatorVoicePlaybackSettings playbackSettings = CreatePlaybackSettings(split);

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
            // A split watcher must stay silent until positional playback succeeds. Restoring vanilla here would
            // make a dead listener hear that speaker globally, including during target/pose packet transitions.
            if (split != SplitVoiceRoute.Configured) Mute(remoteTarget.LocalClientId, remoteTarget.LocalPlayerSlotId);
            else if (_activeRoutes.Contains(remoteTarget.LocalClientId))
            {
                ClearRoute(remoteTarget.LocalClientId, reason);
            }
        }

        if (SplitScreenPresenceRules.MutesUnmoddedDead(listener))
        {
            _adapter.CopyDeadPlayers(_deadPlayers);
            foreach (var (clientId, slotId) in _deadPlayers)
                if (!_handled.Contains(clientId)) Mute(clientId, slotId);
        }

        ClearRoutesNotIn(_desiredRoutes);
    }

    // Held like a route: cleared (and the voice restored) once the rule no longer mutes it.
    private void Mute(ulong clientId, ulong slotId)
    {
        if (!_adapter.TryMuteSpectatorVoice(clientId, slotId)) return;
        _desiredRoutes.Add(clientId);
        _activeSlots[clientId] = slotId;
        _activeRoutes.Add(clientId);
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

    private SpectatorVoicePlaybackSettings CreatePlaybackSettings(SplitVoiceRoute route)
    {
        bool positional = route == SplitVoiceRoute.Positional;
        // Watching together: the configured volume, without direction or distance (the party shares one camera).
        return new SpectatorVoicePlaybackSettings(
            Mathf.Clamp01(_config.SpectatorVoiceToTargetVolume.Value),
            positional || route != SplitVoiceRoute.Party && _config.SpectatorVoiceUseRemotePosePosition.Value,
            _config.SpectatorVoiceEnableDistanceAttenuation.Value,
            _config.SpectatorVoiceMinDistance.Value,
            _config.SpectatorVoiceMaxDistance.Value,
            _config.SpectatorVoiceRolloffPower.Value,
            _config.SpectatorVoiceMinimumVolume.Value,
            !positional && _config.SpectatorVoiceFallbackTo2DWhenPoseMissing.Value);
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
