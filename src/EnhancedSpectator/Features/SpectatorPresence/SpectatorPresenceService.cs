using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Networking;
using EnhancedSpectator.Runtime;

namespace EnhancedSpectator.Features.SpectatorPresence;

/// <summary>
/// Infers which remote modded spectators are visible to the local player.
/// </summary>
public sealed class SpectatorPresenceService : ISpectatorPresenceProvider
{
    private readonly EnhancedSpectatorConfig _config;
    private readonly IGameSpectatorAdapter _gameSpectatorAdapter;
    private readonly IEnhancedSpectatorNetworkService _networkService;
    private readonly Dictionary<ulong, RemoteSpectatorInfo> _currentSpectators =
        new Dictionary<ulong, RemoteSpectatorInfo>();
    private readonly Dictionary<ulong, RemoteSpectatorInfo> _nextSpectators =
        new Dictionary<ulong, RemoteSpectatorInfo>();
    private readonly List<RemoteSpectatorInfo> _candidateSpectators = new List<RemoteSpectatorInfo>();
    private readonly List<RemoteSpectatorInfo> _publishedSpectators = new List<RemoteSpectatorInfo>();
    private readonly List<SpectatorTargetState> _remoteTargetScratch = new List<SpectatorTargetState>();
    private readonly LocalSpectatorPresenceState _activePresenceState;

    /// <summary>
    /// Creates a spectator presence service.
    /// </summary>
    public SpectatorPresenceService(
        EnhancedSpectatorConfig config,
        IGameSpectatorAdapter gameSpectatorAdapter,
        IEnhancedSpectatorNetworkService networkService)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _gameSpectatorAdapter = gameSpectatorAdapter ?? throw new ArgumentNullException(nameof(gameSpectatorAdapter));
        _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
        _activePresenceState = new LocalSpectatorPresenceState(true, _publishedSpectators);
        Current = LocalSpectatorPresenceState.Empty;
    }

    /// <summary>
    /// Gets the latest inferred local presence state.
    /// </summary>
    public LocalSpectatorPresenceState Current { get; private set; }

    /// <summary>
    /// Updates remote spectator presence inference.
    /// </summary>
    public void Tick()
    {
        if (!_config.EnableSpectatorPresenceDebug.Value)
        {
            ClearPresenceState(false);
            return;
        }

        if (!RuntimeConnectionState.CanUseModNetworking(out _)
            || !_networkService.IsNetworkAvailable
            || !_networkService.IsTargetSyncEnabled)
        {
            ClearPresenceState(false);
            return;
        }

        if (!TryGetLocalContext(
            out ulong localClientId,
            out ulong localPlayerSlotId,
            out bool localPlayerIsDeadOrSpectating))
        {
            ClearPresenceState(false);
            return;
        }

        _networkService.CopyRemoteSpectatorTargetsTo(_remoteTargetScratch);
        BuildRemoteSpectatorSet(
            localClientId,
            localPlayerSlotId,
            localPlayerIsDeadOrSpectating,
            _remoteTargetScratch);

        PublishPresenceChanges();
    }

    /// <summary>
    /// Clears any inferred state without emitting stop logs.
    /// </summary>
    public void Clear()
    {
        ClearPresenceState(false);
    }

    private bool TryGetLocalContext(
        out ulong localClientId,
        out ulong localPlayerSlotId,
        out bool localPlayerIsDeadOrSpectating)
    {
        if (_gameSpectatorAdapter.TryGetLocalSpectatorSnapshot(out GameSpectatorSnapshot snapshot)
            && snapshot.HasRound
            && snapshot.HasLocalPlayer
            && snapshot.LocalPlayerActualClientId.HasValue
            && snapshot.LocalPlayerSlotId.HasValue)
        {
            localClientId = snapshot.LocalPlayerActualClientId.Value;
            localPlayerSlotId = snapshot.LocalPlayerSlotId.Value;
            localPlayerIsDeadOrSpectating = snapshot.IsLocalPlayerDead || snapshot.HasBegunSpectating;
            return true;
        }

        localClientId = 0;
        localPlayerSlotId = 0;
        localPlayerIsDeadOrSpectating = false;
        return false;
    }

    private void BuildRemoteSpectatorSet(
        ulong localClientId,
        ulong localPlayerSlotId,
        bool localPlayerIsDeadOrSpectating,
        IReadOnlyList<SpectatorTargetState> remoteTargets)
    {
        _nextSpectators.Clear();
        _candidateSpectators.Clear();
        int maxVisible = Math.Max(0, _config.MaxFloatingHeadsVisible.Value);
        if (maxVisible == 0)
        {
            return;
        }

        foreach (SpectatorTargetState remoteTarget in remoteTargets)
        {
            if (!RemoteSpectatorVisibilityRules.IsRemoteSpectator(remoteTarget, localClientId))
            {
                continue;
            }

            bool isWatchingLocalPlayer = RemoteSpectatorVisibilityRules.IsWatchingLocalPlayer(
                remoteTarget,
                localClientId,
                localPlayerSlotId);
            SpectatorPoseState? poseState = TryGetMatchingPose(remoteTarget);
            if (!RemoteSpectatorVisibilityRules.ShouldShowRemoteSpectator(
                remoteTarget,
                isWatchingLocalPlayer,
                poseState != null,
                localPlayerIsDeadOrSpectating,
                _config.ShowRemoteSpectators.Value,
                _config.ShowOnlySpectatorsWatchingMe.Value,
                _config.ShowDeadSpectatorsToAlivePlayers.Value,
                _config.ShowDeadSpectatorsToDeadPlayers.Value))
            {
                continue;
            }

            _candidateSpectators.Add(new RemoteSpectatorInfo(
                remoteTarget.LocalClientId,
                remoteTarget.LocalPlayerSlotId,
                isWatchingLocalPlayer,
                remoteTarget.TimestampTicks,
                poseState));
        }

        _candidateSpectators.Sort((left, right) => left.SpectatorClientId.CompareTo(right.SpectatorClientId));
        int visibleCount = Math.Min(maxVisible, _candidateSpectators.Count);
        for (int index = 0; index < visibleCount; index++)
        {
            RemoteSpectatorInfo spectator = _candidateSpectators[index];
            _nextSpectators[spectator.SpectatorClientId] = spectator;
        }
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

    private void PublishPresenceChanges()
    {
        if (_config.DebugLogPresenceChanges.Value)
        {
            foreach (RemoteSpectatorInfo spectator in _nextSpectators.Values)
            {
                if (!_currentSpectators.ContainsKey(spectator.SpectatorClientId))
                {
                    LogSpectatorStarted(spectator);
                }
            }

            foreach (RemoteSpectatorInfo spectator in _currentSpectators.Values)
            {
                if (!_nextSpectators.ContainsKey(spectator.SpectatorClientId))
                {
                    LogSpectatorStopped(spectator);
                }
            }
        }

        _currentSpectators.Clear();
        _publishedSpectators.Clear();
        foreach (KeyValuePair<ulong, RemoteSpectatorInfo> pair in _nextSpectators)
        {
            _currentSpectators[pair.Key] = pair.Value;
            _publishedSpectators.Add(pair.Value);
        }

        Current = _activePresenceState;
    }

    private void ClearPresenceState(bool logStops)
    {
        if (logStops && _config.DebugLogPresenceChanges.Value)
        {
            foreach (RemoteSpectatorInfo spectator in _currentSpectators.Values)
            {
                ModLog.Debug(
                    spectator.IsWatchingLocalPlayer
                        ? $"Remote spectator stopped watching local player: spectatorClient={spectator.SpectatorClientId}, spectatorSlot={spectator.SpectatorSlotId}."
                        : $"Remote spectator no longer visible: spectatorClient={spectator.SpectatorClientId}, spectatorSlot={spectator.SpectatorSlotId}.");
            }
        }

        _currentSpectators.Clear();
        _nextSpectators.Clear();
        _candidateSpectators.Clear();
        _publishedSpectators.Clear();
        _remoteTargetScratch.Clear();
        Current = LocalSpectatorPresenceState.Empty;
    }

    private static void LogSpectatorStarted(RemoteSpectatorInfo spectator)
    {
        ModLog.Debug(
            spectator.IsWatchingLocalPlayer
                ? $"Remote spectator started watching local player: spectatorClient={spectator.SpectatorClientId}, spectatorSlot={spectator.SpectatorSlotId}."
                : $"Remote spectator became visible while spectating: spectatorClient={spectator.SpectatorClientId}, spectatorSlot={spectator.SpectatorSlotId}.");
    }

    private static void LogSpectatorStopped(RemoteSpectatorInfo spectator)
    {
        ModLog.Debug(
            spectator.IsWatchingLocalPlayer
                ? $"Remote spectator stopped watching local player: spectatorClient={spectator.SpectatorClientId}, spectatorSlot={spectator.SpectatorSlotId}."
                : $"Remote spectator no longer visible: spectatorClient={spectator.SpectatorClientId}, spectatorSlot={spectator.SpectatorSlotId}.");
    }
}
