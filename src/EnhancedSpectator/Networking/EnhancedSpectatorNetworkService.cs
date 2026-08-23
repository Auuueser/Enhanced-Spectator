using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Features.VoiceActivity;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Runtime;
using UnityEngine;

namespace EnhancedSpectator.Networking;

/// <summary>
/// Coordinates capability handshake and spectator target state synchronization.
/// </summary>
public sealed class EnhancedSpectatorNetworkService : IEnhancedSpectatorNetworkService
{
    private const float CapabilityStableDelaySeconds = 0.35f;
    private const int CapabilityStableDelayFrames = 3;
    private const float CompatiblePeerProbeTimeoutSeconds = 2.5f;
    private const float CapabilityProbeRetryIntervalSeconds = 2.5f;
    private const float SpectatorPoseHeartbeatIntervalSeconds = 0.5f;

    private readonly EnhancedSpectatorConfig _config;
    private readonly ISpectatorTargetStateProvider _spectatorTargetStateProvider;
    private readonly ISpectatorPoseStateProvider _spectatorPoseStateProvider;
    private readonly IPeerIdentityStateProvider _peerIdentityStateProvider;
    private readonly IVoiceActivityProvider _voiceActivityProvider;
    private readonly IModNetworkTransport _transport;
    private readonly INetworkRuntimeState _runtimeState;
    private readonly RemotePeerRegistry _peerRegistry = new RemotePeerRegistry();
    private readonly RemoteSpectatorTargetRegistry _remoteTargetRegistry = new RemoteSpectatorTargetRegistry();
    private readonly RemoteSpectatorPoseRegistry _remotePoseRegistry = new RemoteSpectatorPoseRegistry();
    private readonly RemotePeerIdentityRegistry _remoteIdentityRegistry = new RemotePeerIdentityRegistry();
    private readonly RemoteVoiceActivityRegistry _remoteVoiceActivityRegistry = new RemoteVoiceActivityRegistry();
    private readonly VoiceActivityDebugLimiter _voiceDebugLimiter = new VoiceActivityDebugLimiter();

    private SpectatorTargetState? _lastObservedTargetState;
    private SpectatorTargetState? _lastSentTargetState;
    private SpectatorTargetState? _pendingTargetState;
    private SpectatorPoseState? _lastObservedPoseState;
    private SpectatorPoseState? _lastSentPoseState;
    private SpectatorPoseState? _pendingPoseState;
    private bool _pendingPoseRefresh;
    private VoiceActivityState? _lastObservedVoiceActivityState;
    private VoiceActivityState? _lastSentVoiceActivityState;
    private VoiceActivityState? _pendingVoiceActivityState;
    private PeerIdentityState? _lastSentIdentityState;
    private bool _pendingVoiceActivityRefresh;
    private bool _initialized;
    private bool _networkAvailable;
    private bool _targetSyncReady;
    private bool _hasCompatibleModPeer;
    private bool _noCompatiblePeerLocalOnly;
    private bool _capabilitySent;
    private float _capabilityProbeSentRealtime = -1f;
    private ulong? _lastLocalClientId;
    private float _nextTargetSyncTime;
    private float _nextPoseSyncTime;
    private float _nextVoiceActivitySyncTime;
    private float _nextTargetSampleTime;
    private float _nextPoseSampleTime;
    private float _lastPoseSampleTime = -1f;
    private float _nextPoseRefreshTime;
    private float _nextVoiceActivitySampleTime;
    private float _nextVoiceActivityRefreshTime;
    private float _nextPeerPruneTime;
    private float _transportRegisteredRealtime;
    private int _transportRegisteredFrame = -1;
    private int _nextLifecycleDebugFrame;
    private int _nextCapabilityDelayDebugFrame;
    private string? _lastDegradationReason;
    private string? _lastTargetSyncWaitReason;
    private NetworkLifecycleState _lifecycleState = NetworkLifecycleState.Unavailable;

    /// <summary>
    /// Creates the networking service.
    /// </summary>
    public EnhancedSpectatorNetworkService(
        EnhancedSpectatorConfig config,
        SpectatorModule spectatorModule)
        : this(
            config,
            spectatorModule,
            spectatorModule,
            spectatorModule,
            new LethalCompanyVoiceActivityProvider(),
            new UnityNetcodeMessagingTransport(() => config.DebugNetworkMessages.Value),
            UnityNetworkRuntimeState.Instance)
    {
    }

    /// <summary>
    /// Creates the networking service with an explicit transport.
    /// </summary>
    public EnhancedSpectatorNetworkService(
        EnhancedSpectatorConfig config,
        ISpectatorTargetStateProvider spectatorTargetStateProvider,
        ISpectatorPoseStateProvider spectatorPoseStateProvider,
        IPeerIdentityStateProvider peerIdentityStateProvider,
        IVoiceActivityProvider voiceActivityProvider,
        IModNetworkTransport transport)
        : this(
            config,
            spectatorTargetStateProvider,
            spectatorPoseStateProvider,
            peerIdentityStateProvider,
            voiceActivityProvider,
            transport,
            UnityNetworkRuntimeState.Instance)
    {
    }

    /// <summary>
    /// Creates the networking service with explicit testable runtime dependencies.
    /// </summary>
    public EnhancedSpectatorNetworkService(
        EnhancedSpectatorConfig config,
        ISpectatorTargetStateProvider spectatorTargetStateProvider,
        ISpectatorPoseStateProvider spectatorPoseStateProvider,
        IPeerIdentityStateProvider peerIdentityStateProvider,
        IVoiceActivityProvider voiceActivityProvider,
        IModNetworkTransport transport,
        INetworkRuntimeState runtimeState)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _spectatorTargetStateProvider = spectatorTargetStateProvider ?? throw new ArgumentNullException(nameof(spectatorTargetStateProvider));
        _spectatorPoseStateProvider = spectatorPoseStateProvider ?? throw new ArgumentNullException(nameof(spectatorPoseStateProvider));
        _peerIdentityStateProvider = peerIdentityStateProvider ?? throw new ArgumentNullException(nameof(peerIdentityStateProvider));
        _voiceActivityProvider = voiceActivityProvider ?? throw new ArgumentNullException(nameof(voiceActivityProvider));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _runtimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
    }

    /// <inheritdoc />
    public bool IsNetworkAvailable => _networkAvailable;

    /// <inheritdoc />
    public bool IsTargetSyncEnabled => _targetSyncReady;

    /// <inheritdoc />
    public bool HasCompatibleModPeer => _hasCompatibleModPeer;

    /// <inheritdoc />
    public NetworkLifecycleState LifecycleState => _lifecycleState;

    /// <inheritdoc />
    public int RemotePeerIdentityRevision => _remoteIdentityRegistry.Revision;

    /// <inheritdoc />
    public int RemoteSpectatorTargetRevision => _remoteTargetRegistry.Revision;

    /// <inheritdoc />
    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _lifecycleState = NetworkLifecycleState.LocalOnly;
        ModLog.Debug("Networking service initialized.");
    }

    /// <inheritdoc />
    public void Tick()
    {
        if (!_initialized)
        {
            return;
        }

        try
        {
            if (!_config.EnableNetworking.Value)
            {
                Degrade("networking config disabled");
                return;
            }

            if (!_runtimeState.CanUseModNetworking(out string lifecycleReason))
            {
                StopNetworkingForLifecycle(lifecycleReason);
                return;
            }

            if (!EnsureTransportRegistered())
            {
                return;
            }

            _networkAvailable = true;
            _lifecycleState = NetworkLifecycleState.TransportRegistered;
            RegisterLocalCapability();
            PruneDisconnectedPeers();
            SendLocalCapabilityIfNeeded();
            UpdateTargetSyncReadiness();
            UpdateCompatiblePeerProbeState();
            if (!NetworkCompatibilityPolicy.ShouldRunBusinessSync(_lifecycleState, _targetSyncReady))
            {
                ClearBusinessSyncState();
                return;
            }

            UpdateAndSendLocalPeerIdentity();
            UpdateLocalSpectatorTargetIfDue();
            UpdateLocalSpectatorPoseIfDue();
            UpdateLocalVoiceActivityIfDue();
            UpdateTargetSyncReadiness();
            TrySendPendingTargetState();
            TrySendPendingPoseState();
            TrySendPendingVoiceActivityState();
        }
        catch (Exception ex)
        {
            Degrade($"networking exception: {ex.GetType().Name}");
            ModLog.Error($"Enhanced Spectator networking failed and degraded to local-only mode: {ex}");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _transport.Dispose();
        ClearNetworkState();
        _initialized = false;
        _lifecycleState = NetworkLifecycleState.Disposed;
        ModLog.Debug("Networking service disposed.");
    }

    /// <inheritdoc />
    public bool TryGetPeerCapability(ulong clientId, out ModPeerCapability capability)
    {
        return _peerRegistry.TryGetCapability(clientId, out capability);
    }

    /// <inheritdoc />
    public bool TryGetRemoteSpectatorTarget(ulong clientId, out SpectatorTargetState state)
    {
        return _remoteTargetRegistry.TryGet(clientId, out state);
    }

    /// <inheritdoc />
    public bool TryGetRemoteSpectatorPose(ulong clientId, out SpectatorPoseState state)
    {
        return _remotePoseRegistry.TryGet(clientId, out state);
    }

    /// <inheritdoc />
    public bool TryGetRemotePeerIdentity(ulong clientId, out PeerIdentityState state)
    {
        return _remoteIdentityRegistry.TryGet(clientId, out state);
    }

    /// <inheritdoc />
    public bool TryGetRemoteVoiceActivity(ulong clientId, out VoiceActivityState state)
    {
        if (!_remoteVoiceActivityRegistry.TryGet(clientId, out state, out long receivedAtTicks))
        {
            return false;
        }

        long staleTicks = TimeSpan.FromSeconds(Mathf.Max(0f, _config.VoiceActivityStaleSeconds.Value)).Ticks;
        if (VoiceActivitySyncRules.IsFresh(receivedAtTicks, _runtimeState.UtcNowTicks, staleTicks))
        {
            return true;
        }

        DebugVoice($"Remote voice activity expired: peer={clientId}.");
        _remoteVoiceActivityRegistry.Remove(clientId);
        state = VoiceActivityState.NoData;
        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<SpectatorTargetState> GetRemoteSpectatorTargets()
    {
        return _remoteTargetRegistry.GetSnapshot().AsReadOnly();
    }

    /// <inheritdoc />
    public void CopyRemoteSpectatorTargetsTo(List<SpectatorTargetState> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        _remoteTargetRegistry.CopySnapshotTo(destination);
    }

    /// <inheritdoc />
    public IReadOnlyList<SpectatorPoseState> GetRemoteSpectatorPoses()
    {
        return _remotePoseRegistry.GetSnapshot().AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<PeerIdentityState> GetRemotePeerIdentities()
    {
        return _remoteIdentityRegistry.GetSnapshot().AsReadOnly();
    }

    /// <inheritdoc />
    public void CopyRemotePeerIdentitiesTo(List<PeerIdentityState> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        _remoteIdentityRegistry.CopySnapshotTo(destination);
    }

    /// <inheritdoc />
    public IReadOnlyList<VoiceActivityState> GetRemoteVoiceActivities()
    {
        return _remoteVoiceActivityRegistry.GetSnapshot().AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<ModPeerCapability> GetKnownModdedPeers()
    {
        return _peerRegistry.GetCapabilitiesSnapshot().AsReadOnly();
    }

    private bool EnsureTransportRegistered()
    {
        if (_transport.IsRegistered)
        {
            if (!_transport.IsNetworkAvailable)
            {
                Degrade("custom messaging transport no longer available");
                return false;
            }

            return true;
        }

        if (!_transport.TryRegister(
            OnCapabilityReceived,
            OnSpectatorTargetReceived,
            OnSpectatorPoseReceived,
            OnPeerIdentityReceived,
            OnVoiceActivityReceived,
            out string reason))
        {
            Degrade(reason);
            return false;
        }

        _capabilitySent = false;
        _capabilityProbeSentRealtime = -1f;
        _noCompatiblePeerLocalOnly = false;
        _lastLocalClientId = _transport.LocalClientId;
        _transportRegisteredFrame = _runtimeState.FrameCount;
        _transportRegisteredRealtime = _runtimeState.RealtimeSinceStartup;
        _lastDegradationReason = null;
        Debug("Custom messaging transport registered.");
        return true;
    }

    private void RegisterLocalCapability()
    {
        ulong localClientId = _transport.LocalClientId;
        if (_lastLocalClientId.HasValue && _lastLocalClientId.Value != localClientId)
        {
            _peerRegistry.Clear();
            _remoteTargetRegistry.Clear();
            _remotePoseRegistry.Clear();
            _remoteIdentityRegistry.Clear();
            _remoteVoiceActivityRegistry.Clear();
            _lastObservedTargetState = null;
            _lastSentTargetState = null;
            _pendingTargetState = null;
            _lastObservedPoseState = null;
            _lastSentPoseState = null;
            _pendingPoseState = null;
            _pendingPoseRefresh = false;
            _lastObservedVoiceActivityState = null;
            _lastSentVoiceActivityState = null;
            _pendingVoiceActivityState = null;
            _pendingVoiceActivityRefresh = false;
            _lastSentIdentityState = null;
            _nextTargetSyncTime = 0f;
            _nextPoseSyncTime = 0f;
            _nextVoiceActivitySyncTime = 0f;
            _nextTargetSampleTime = 0f;
            _nextPoseSampleTime = 0f;
            _lastPoseSampleTime = -1f;
            _nextPoseRefreshTime = 0f;
            _nextVoiceActivitySampleTime = 0f;
            _nextVoiceActivityRefreshTime = 0f;
            _capabilitySent = false;
            _capabilityProbeSentRealtime = -1f;
            _noCompatiblePeerLocalOnly = false;
            _transportRegisteredFrame = _runtimeState.FrameCount;
            _transportRegisteredRealtime = _runtimeState.RealtimeSinceStartup;
            Debug($"Local Netcode client id changed from {_lastLocalClientId.Value} to {localClientId}; reset network state.");
        }

        _lastLocalClientId = localClientId;
        _peerRegistry.RegisterLocal(CreateLocalCapability(localClientId));
    }

    private void SendLocalCapabilityIfNeeded()
    {
        if (!_config.EnableCapabilityHandshake.Value)
        {
            _capabilitySent = false;
            return;
        }

        if (_capabilitySent
            && !NetworkCompatibilityPolicy.ShouldRetryCapabilityProbe(
                _targetSyncReady,
                _capabilitySent,
                _capabilityProbeSentRealtime,
                _runtimeState.RealtimeSinceStartup,
                CapabilityProbeRetryIntervalSeconds))
        {
            return;
        }

        if (!_runtimeState.CanUseModNetworking(out string lifecycleReason))
        {
            StopNetworkingForLifecycle(lifecycleReason);
            return;
        }

        if (!IsTransportStableForCapability(out string stableReason))
        {
            DebugCapabilityDelay(stableReason);
            return;
        }

        ModPeerCapability capability = CreateLocalCapability(_transport.LocalClientId);
        _peerRegistry.RegisterLocal(capability);
        if (_transport.SendCapability(capability, null, out string reason))
        {
            _capabilitySent = true;
            _capabilityProbeSentRealtime = _runtimeState.RealtimeSinceStartup;
            _noCompatiblePeerLocalOnly = false;
            _lifecycleState = NetworkLifecycleState.TransportRegistered;
            Debug(
                $"Capability sent: client={capability.ClientId}, targetSync={capability.SupportsSpectatorTargetSync}, voiceSync={capability.SupportsVoiceActivitySync}, voiceRoute={capability.SupportsSpectatorVoiceToTarget}.");
            return;
        }

        Degrade($"capability send failed: {reason}");
    }

    private bool IsTransportStableForCapability(out string reason)
    {
        int framesSinceRegistration = _runtimeState.FrameCount - _transportRegisteredFrame;
        if (_transportRegisteredFrame < 0 || framesSinceRegistration < CapabilityStableDelayFrames)
        {
            reason = $"transport registered {Mathf.Max(0, framesSinceRegistration)} frame(s) ago";
            return false;
        }

        float secondsSinceRegistration = _runtimeState.RealtimeSinceStartup - _transportRegisteredRealtime;
        if (secondsSinceRegistration < CapabilityStableDelaySeconds)
        {
            reason = $"transport registered {secondsSinceRegistration:0.00}s ago";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void UpdateAndSendLocalPeerIdentity()
    {
        if (!_capabilitySent
            || !_targetSyncReady
            || !_peerIdentityStateProvider.TryGetLocalPeerIdentity(out PeerIdentityState identity)
            || string.IsNullOrWhiteSpace(identity.DisplayName)
            || IdentityEquals(_lastSentIdentityState, identity))
        {
            return;
        }

        List<ulong> recipients = _peerRegistry.GetSpectatorTargetSyncPeerIds(_transport.LocalClientId);
        if (recipients.Count == 0)
        {
            return;
        }

        if (_transport.SendPeerIdentity(identity, recipients, out string reason))
        {
            _lastSentIdentityState = identity;
            Debug($"Peer identity sent: client={identity.ClientId}, slot={identity.PlayerSlotId}, name={identity.DisplayName}, voiceName={FormatVoiceName(identity.VoicePlayerName)}.");
            return;
        }

        Degrade($"peer identity send failed: {reason}");
    }

    private void PruneDisconnectedPeers(bool force = false)
    {
        if (!force && _runtimeState.UnscaledTime < _nextPeerPruneTime)
        {
            return;
        }

        _nextPeerPruneTime = _runtimeState.UnscaledTime + 1f;
        if (!_lastLocalClientId.HasValue)
        {
            return;
        }

        List<ulong> peerIds = _peerRegistry.GetRemotePeerIds(_lastLocalClientId.Value);
        foreach (ulong peerId in peerIds)
        {
            if (!_transport.IsHost && _peerRegistry.IsRelayedPeer(peerId))
            {
                continue;
            }

            if (_transport.IsPeerConnected(peerId))
            {
                continue;
            }

            RelayDisconnectedPeerCleanup(peerId);
            _peerRegistry.Remove(peerId);
            _remoteTargetRegistry.Remove(peerId);
            _remotePoseRegistry.Remove(peerId);
            _remoteIdentityRegistry.Remove(peerId);
            _remoteVoiceActivityRegistry.Remove(peerId);
            Debug($"Removed disconnected network peer state: peer={peerId}.");
        }
    }

    private void UpdateTargetSyncReadiness()
    {
        string? waitReason = null;
        if (!_config.EnableCapabilityHandshake.Value)
        {
            waitReason = "capability handshake disabled";
        }
        else if (!_config.EnableSpectatorTargetSync.Value)
        {
            waitReason = "spectator target sync disabled";
        }
        else if (!_transport.IsRegistered)
        {
            waitReason = "custom messaging transport not registered";
        }
        else if (!_peerRegistry.HasSpectatorTargetSyncPeer(_transport.LocalClientId))
        {
            waitReason = "waiting for compatible remote peer capability";
        }

        _targetSyncReady = waitReason == null;
        _hasCompatibleModPeer = _targetSyncReady;
        if (_config.DebugNetworkMessages.Value && _lastTargetSyncWaitReason != waitReason)
        {
            _lastTargetSyncWaitReason = waitReason;
            if (waitReason == null)
            {
                ModLog.Debug("Spectator target sync is ready.");
            }
            else
            {
                ModLog.Debug($"Spectator target sync is not ready: {waitReason}.");
            }
        }
    }

    private void UpdateCompatiblePeerProbeState()
    {
        _lifecycleState = NetworkCompatibilityPolicy.ResolveLifecycleState(
            _targetSyncReady,
            _capabilitySent,
            _capabilityProbeSentRealtime,
            _runtimeState.RealtimeSinceStartup,
            CompatiblePeerProbeTimeoutSeconds);
        _noCompatiblePeerLocalOnly = _lifecycleState == NetworkLifecycleState.NoCompatiblePeerLocalOnly;
        if (!_noCompatiblePeerLocalOnly)
        {
            return;
        }

        if (_lastDegradationReason != "no compatible Enhanced Spectator peer capability received")
        {
            _lastDegradationReason = "no compatible Enhanced Spectator peer capability received";
            Debug("Networking remains local-only because no compatible Enhanced Spectator peer answered the capability probe.");
        }
    }

    private void ClearBusinessSyncState()
    {
        _lastObservedTargetState = null;
        _lastSentTargetState = null;
        _pendingTargetState = null;
        _lastObservedPoseState = null;
        _lastSentPoseState = null;
        _pendingPoseState = null;
        _pendingPoseRefresh = false;
        _lastObservedVoiceActivityState = null;
        _lastSentVoiceActivityState = null;
        _pendingVoiceActivityState = null;
        _pendingVoiceActivityRefresh = false;
        _lastSentIdentityState = null;
        _nextTargetSampleTime = 0f;
        _nextPoseSampleTime = 0f;
        _lastPoseSampleTime = -1f;
        _nextPoseRefreshTime = 0f;
        _nextVoiceActivitySampleTime = 0f;
    }

    private void UpdateLocalSpectatorTargetIfDue()
    {
        if (!_config.EnableSpectatorTargetSync.Value)
        {
            _lastObservedTargetState = null;
            _lastSentTargetState = null;
            _pendingTargetState = null;
            return;
        }

        if (!NetworkSyncSamplingRules.ShouldSample(
            _lastObservedTargetState != null,
            _runtimeState.UnscaledTime,
            _nextTargetSampleTime))
        {
            return;
        }

        UpdateLocalSpectatorTarget();
        _nextTargetSampleTime = NetworkSyncSamplingRules.ResolveNextSampleTime(
            _runtimeState.UnscaledTime,
            (float)ModNetworkConstants.TargetSyncMinIntervalSeconds);
    }

    private void UpdateLocalSpectatorTarget()
    {
        if (!_spectatorTargetStateProvider.TryGetCurrentSpectatorTarget(out SpectatorTargetState state))
        {
            state = new SpectatorTargetState(false, _transport.LocalClientId, 0, null, null, _runtimeState.UtcNowTicks);
        }

        if (_lastObservedTargetState != null && _lastObservedTargetState.Equals(state))
        {
            return;
        }

        _lastObservedTargetState = state;
        if (_lastSentTargetState != null && _lastSentTargetState.Equals(state))
        {
            _pendingTargetState = null;
        }
        else
        {
            _pendingTargetState = state;
        }

        Debug(
            $"Observed spectator target change: spectating={state.IsSpectating}, localClient={state.LocalClientId}, localSlot={state.LocalPlayerSlotId}, targetClient={FormatNullable(state.TargetClientId)}, targetSlot={FormatNullable(state.TargetPlayerSlotId)}.");
    }

    private void UpdateLocalSpectatorPoseIfDue()
    {
        if (!_config.EnableSpectatorPoseSync.Value)
        {
            _lastObservedPoseState = null;
            _lastSentPoseState = null;
            _pendingPoseState = null;
            _pendingPoseRefresh = false;
            _lastPoseSampleTime = -1f;
            return;
        }

        if (!NetworkSyncSamplingRules.ShouldSample(
            _lastObservedPoseState != null,
            _runtimeState.UnscaledTime,
            _nextPoseSampleTime))
        {
            return;
        }

        float now = _runtimeState.UnscaledTime;
        float sampleDeltaTime = _lastPoseSampleTime >= 0f
            ? now - _lastPoseSampleTime
            : 0f;
        bool useBurstRate = UpdateLocalSpectatorPose(sampleDeltaTime);
        _lastPoseSampleTime = now;
        _nextPoseSampleTime = NetworkSyncSamplingRules.ResolveNextSampleTime(
            now,
            RemoteSpectatorPoseNetworkRateRules.ResolveSamplingInterval(GetPoseSyncInterval()));
        if (useBurstRate)
        {
            _nextPoseSyncTime = Mathf.Min(_nextPoseSyncTime, now);
        }
    }

    private void TrySendPendingTargetState()
    {
        if (!_targetSyncReady || _pendingTargetState == null)
        {
            return;
        }

        if (!_transport.IsRegistered || !_transport.IsNetworkAvailable)
        {
            StopNetworkingForLifecycle("custom messaging transport unavailable before target send");
            return;
        }

        if (!_runtimeState.CanUseModNetworking(out string lifecycleReason))
        {
            StopNetworkingForLifecycle(lifecycleReason);
            return;
        }

        if (_lastSentTargetState != null && _lastSentTargetState.Equals(_pendingTargetState))
        {
            _pendingTargetState = null;
            return;
        }

        if (_runtimeState.UnscaledTime < _nextTargetSyncTime)
        {
            return;
        }

        List<ulong> recipients = _peerRegistry.GetSpectatorTargetSyncPeerIds(_transport.LocalClientId);
        if (recipients.Count == 0)
        {
            UpdateTargetSyncReadiness();
            return;
        }

        SpectatorTargetSyncMessage message = new SpectatorTargetSyncMessage(
            ModNetworkConstants.ProtocolVersion,
            _pendingTargetState,
            _runtimeState.UtcNowTicks);

        if (_transport.SendSpectatorTarget(message, recipients, out string reason))
        {
            _lastSentTargetState = _pendingTargetState;
            _pendingTargetState = null;
            _nextTargetSyncTime = _runtimeState.UnscaledTime + (float)ModNetworkConstants.TargetSyncMinIntervalSeconds;
            Debug(
                $"Spectator target sent to {recipients.Count} peer(s): spectating={message.State.IsSpectating}, targetClient={FormatNullable(message.State.TargetClientId)}, targetSlot={FormatNullable(message.State.TargetPlayerSlotId)}.");
            return;
        }

        Degrade($"spectator target send failed: {reason}");
    }

    private bool UpdateLocalSpectatorPose(float sampleDeltaTime)
    {
        if (!_config.EnableSpectatorPoseSync.Value)
        {
            _lastObservedPoseState = null;
            _lastSentPoseState = null;
            _pendingPoseState = null;
            _pendingPoseRefresh = false;
            return false;
        }

        if (!_spectatorPoseStateProvider.TryGetCurrentSpectatorPose(out SpectatorPoseState state))
        {
            state = new SpectatorPoseState(
                false,
                _transport.LocalClientId,
                0,
                null,
                null,
                Vector3.zero,
                Quaternion.identity,
                _runtimeState.UtcNowTicks);
        }

        bool useBurstRate = RemoteSpectatorPoseNetworkRateRules.ShouldUseBurstRate(
            _lastObservedPoseState,
            state,
            sampleDeltaTime);

        if (_lastObservedPoseState != null && _lastObservedPoseState.ApproximatelyEquals(state))
        {
            if (NetworkSyncSamplingRules.ShouldRefreshUnchangedState(
                state.IsSpectating,
                _lastSentPoseState != null && _lastSentPoseState.IsSpectating,
                _runtimeState.UnscaledTime,
                _nextPoseRefreshTime))
            {
                _pendingPoseState = state;
                _pendingPoseRefresh = true;
            }

            return useBurstRate;
        }

        _lastObservedPoseState = state;
        if (_lastSentPoseState != null && _lastSentPoseState.ApproximatelyEquals(state))
        {
            _pendingPoseState = null;
            _pendingPoseRefresh = false;
        }
        else
        {
            _pendingPoseState = state;
            _pendingPoseRefresh = false;
        }

        if (IsPoseDebugEnabled())
        {
            ModLog.Debug(
                $"Observed spectator pose change: spectating={state.IsSpectating}, localClient={state.LocalClientId}, targetClient={FormatNullable(state.TargetClientId)}, position={FormatVector(state.Position)}.");
        }

        return useBurstRate;
    }

    private void UpdateLocalVoiceActivityIfDue()
    {
        if (!_config.EnableVoiceActivitySync.Value)
        {
            _lastObservedVoiceActivityState = null;
            _lastSentVoiceActivityState = null;
            _pendingVoiceActivityState = null;
            _pendingVoiceActivityRefresh = false;
            return;
        }

        if (!NetworkSyncSamplingRules.ShouldSample(
            _lastObservedVoiceActivityState != null,
            _runtimeState.UnscaledTime,
            _nextVoiceActivitySampleTime))
        {
            return;
        }

        UpdateLocalVoiceActivity();
        _nextVoiceActivitySampleTime = NetworkSyncSamplingRules.ResolveNextSampleTime(
            _runtimeState.UnscaledTime,
            GetVoiceActivitySyncInterval());
    }

    private void TrySendPendingPoseState()
    {
        if (!_targetSyncReady || !_config.EnableSpectatorPoseSync.Value || _pendingPoseState == null)
        {
            return;
        }

        if (!_transport.IsRegistered || !_transport.IsNetworkAvailable)
        {
            StopNetworkingForLifecycle("custom messaging transport unavailable before pose send");
            return;
        }

        if (!_runtimeState.CanUseModNetworking(out string lifecycleReason))
        {
            StopNetworkingForLifecycle(lifecycleReason);
            return;
        }

        if (!_pendingPoseRefresh
            && _lastSentPoseState != null
            && _lastSentPoseState.ApproximatelyEquals(_pendingPoseState))
        {
            _pendingPoseState = null;
            return;
        }

        if (_runtimeState.UnscaledTime < _nextPoseSyncTime)
        {
            return;
        }

        List<ulong> recipients = _peerRegistry.GetSpectatorTargetSyncPeerIds(_transport.LocalClientId);
        if (recipients.Count == 0)
        {
            UpdateTargetSyncReadiness();
            return;
        }

        SpectatorPoseSyncMessage message = new SpectatorPoseSyncMessage(
            ModNetworkConstants.ProtocolVersion,
            _pendingPoseState,
            _runtimeState.UtcNowTicks);

        if (_transport.SendSpectatorPose(message, recipients, out string reason))
        {
            _lastSentPoseState = _pendingPoseState;
            _pendingPoseState = null;
            _pendingPoseRefresh = false;
            _nextPoseSyncTime = _runtimeState.UnscaledTime + GetPoseSyncInterval();
            _nextPoseRefreshTime = _runtimeState.UnscaledTime + GetPoseHeartbeatInterval();
            if (IsPoseDebugEnabled())
            {
                ModLog.Debug(
                    $"Spectator pose sent to {recipients.Count} peer(s): spectating={message.State.IsSpectating}, targetClient={FormatNullable(message.State.TargetClientId)}, position={FormatVector(message.State.Position)}.");
            }

            return;
        }

        Degrade($"spectator pose send failed: {reason}");
    }

    private void UpdateLocalVoiceActivity()
    {
        if (!_config.EnableVoiceActivitySync.Value)
        {
            _lastObservedVoiceActivityState = null;
            _lastSentVoiceActivityState = null;
            _pendingVoiceActivityState = null;
            _pendingVoiceActivityRefresh = false;
            return;
        }

        if (!TryGetLocalVoiceIdentity(out ulong localClientId, out ulong localSlotId))
        {
            return;
        }

        VoiceActivityState state;
        if (!_voiceActivityProvider.TryGetVoiceActivity(localClientId, localSlotId, out state) || !state.HasData)
        {
            state = new VoiceActivityState(
                false,
                false,
                0f,
                0f,
                localClientId,
                localSlotId,
                _runtimeState.UtcNowTicks);
        }

        if (_lastObservedVoiceActivityState == null && !state.HasData)
        {
            _lastObservedVoiceActivityState = state;
            return;
        }

        if (VoiceActivitySyncRules.ApproximatelyEquals(_lastObservedVoiceActivityState, state))
        {
            if (ShouldRefreshVoiceActivity(state))
            {
                _pendingVoiceActivityState = state;
                _pendingVoiceActivityRefresh = true;
            }

            return;
        }

        _lastObservedVoiceActivityState = state;
        if (VoiceActivitySyncRules.ApproximatelyEquals(_lastSentVoiceActivityState, state))
        {
            _pendingVoiceActivityState = null;
            _pendingVoiceActivityRefresh = false;
        }
        else
        {
            _pendingVoiceActivityState = state;
            _pendingVoiceActivityRefresh = false;
        }

        DebugObservedVoiceActivity(state);
    }

    private void TrySendPendingVoiceActivityState()
    {
        if (!_targetSyncReady || !_config.EnableVoiceActivitySync.Value || _pendingVoiceActivityState == null)
        {
            return;
        }

        if (!_transport.IsRegistered || !_transport.IsNetworkAvailable)
        {
            StopNetworkingForLifecycle("custom messaging transport unavailable before voice activity send");
            return;
        }

        if (!_runtimeState.CanUseModNetworking(out string lifecycleReason))
        {
            StopNetworkingForLifecycle(lifecycleReason);
            return;
        }

        if (!_pendingVoiceActivityRefresh
            && VoiceActivitySyncRules.ApproximatelyEquals(_lastSentVoiceActivityState, _pendingVoiceActivityState))
        {
            _pendingVoiceActivityState = null;
            return;
        }

        if (!_pendingVoiceActivityState.HasData
            && (_lastSentVoiceActivityState == null || !_lastSentVoiceActivityState.HasData))
        {
            _pendingVoiceActivityState = null;
            return;
        }

        if (_runtimeState.UnscaledTime < _nextVoiceActivitySyncTime)
        {
            return;
        }

        List<ulong> recipients = _peerRegistry.GetVoiceActivitySyncPeerIds(_transport.LocalClientId);
        if (recipients.Count == 0)
        {
            UpdateTargetSyncReadiness();
            return;
        }

        VoiceActivitySyncMessage message = new VoiceActivitySyncMessage(
            ModNetworkConstants.ProtocolVersion,
            _pendingVoiceActivityState,
            _runtimeState.UtcNowTicks);

        if (_transport.SendVoiceActivity(message, recipients, out string reason))
        {
            _lastSentVoiceActivityState = _pendingVoiceActivityState;
            _pendingVoiceActivityState = null;
            _pendingVoiceActivityRefresh = false;
            _nextVoiceActivitySyncTime = _runtimeState.UnscaledTime + GetVoiceActivitySyncInterval();
            _nextVoiceActivityRefreshTime = _runtimeState.UnscaledTime + GetVoiceActivityRefreshInterval();
            DebugSentVoiceActivity(message.State, recipients.Count);
            return;
        }

        Degrade($"voice activity send failed: {reason}");
    }

    private void OnCapabilityReceived(ulong senderClientId, ModPeerCapability capability)
    {
        if (!_initialized || !_config.EnableNetworking.Value || !_config.EnableCapabilityHandshake.Value)
        {
            return;
        }

        if (!_runtimeState.CanUseModNetworking(out string lifecycleReason))
        {
            Debug($"Dropped capability from sender={senderClientId}: {lifecycleReason}.");
            return;
        }

        if (capability.ClientId == _transport.LocalClientId)
        {
            _peerRegistry.RegisterLocal(CreateLocalCapability(_transport.LocalClientId));
            return;
        }

        if (_transport.IsHost && capability.ClientId != senderClientId)
        {
            Debug($"Dropped capability with mismatched sender and peer id: sender={senderClientId}, peer={capability.ClientId}.");
            return;
        }

        if (!_transport.IsHost
            && senderClientId != capability.ClientId
            && senderClientId != _transport.ServerClientId)
        {
            Debug($"Dropped relayed capability from non-server sender={senderClientId}, peer={capability.ClientId}.");
            return;
        }

        bool isRelayedCapability = !_transport.IsHost && senderClientId != capability.ClientId;
        if (!ModPeerCapabilityRules.SupportsCurrentSpectatorTargetSync(capability))
        {
            RemoveRemotePeerState(
                capability.ClientId,
                $"capability no longer compatible: sender={senderClientId}, targetSync={capability.SupportsSpectatorTargetSync}, voiceSync={capability.SupportsVoiceActivitySync}, voiceRoute={capability.SupportsSpectatorVoiceToTarget}, relayed={isRelayedCapability}");
            return;
        }

        bool compatible = _peerRegistry.RegisterRemote(capability, isRelayedCapability);
        if (compatible)
        {
            _noCompatiblePeerLocalOnly = false;
            _lifecycleState = NetworkLifecycleState.TransportRegistered;
        }

        Debug(
            $"Capability received from sender={senderClientId}, peer={capability.ClientId}, protocol={capability.ProtocolVersion}, targetSync={capability.SupportsSpectatorTargetSync}, voiceSync={capability.SupportsVoiceActivitySync}, voiceRoute={capability.SupportsSpectatorVoiceToTarget}, compatible={compatible}, relayed={isRelayedCapability}.");

        if (compatible && _transport.IsHost)
        {
            ModPeerCapability localCapability = CreateLocalCapability(_transport.LocalClientId);
            _peerRegistry.RegisterLocal(localCapability);
            if (_transport.SendCapability(localCapability, new[] { capability.ClientId }, out string reason))
            {
                Debug($"Capability reply sent to peer={capability.ClientId}.");
            }
            else
            {
                Debug($"Capability reply to peer={capability.ClientId} failed: {reason}.");
            }

            SendLocalPeerIdentityToRecipients(new[] { capability.ClientId });
            SendLocalSpectatorStateToPeer(capability.ClientId);
            PruneDisconnectedPeers(force: true);
            RelayCapabilityToCompatiblePeers(capability);
            RelayKnownCapabilitiesToPeer(capability.ClientId);
            RelayKnownSpectatorStatesToPeer(capability.ClientId);
            RelayKnownPeerIdentitiesToPeer(capability.ClientId);
            RelayKnownVoiceActivitiesToPeer(capability.ClientId);
        }
    }

    private void RemoveRemotePeerState(ulong peerId, string reason)
    {
        if (_transport.IsRegistered && peerId == _transport.LocalClientId)
        {
            return;
        }

        bool hadState = _peerRegistry.TryGetCapability(peerId, out _)
            || _remoteTargetRegistry.TryGet(peerId, out _)
            || _remotePoseRegistry.TryGet(peerId, out _)
            || _remoteIdentityRegistry.TryGet(peerId, out _)
            || _remoteVoiceActivityRegistry.TryGet(peerId, out _);

        _peerRegistry.Remove(peerId);
        _remoteTargetRegistry.Remove(peerId);
        _remotePoseRegistry.Remove(peerId);
        _remoteIdentityRegistry.Remove(peerId);
        _remoteVoiceActivityRegistry.Remove(peerId);

        if (hadState)
        {
            Debug($"Removed remote peer state for peer={peerId}: {reason}.");
        }
        else
        {
            Debug($"Ignored incompatible remote peer capability for peer={peerId}: {reason}.");
        }
    }

    private void OnSpectatorTargetReceived(ulong senderClientId, SpectatorTargetState state)
    {
        if (!_initialized || !_config.EnableNetworking.Value || !_config.EnableSpectatorTargetSync.Value)
        {
            return;
        }

        if (!_runtimeState.CanUseModNetworking(out string lifecycleReason))
        {
            Debug($"Dropped spectator target from sender={senderClientId}: {lifecycleReason}.");
            return;
        }

        if (state.LocalClientId == _transport.LocalClientId)
        {
            return;
        }

        if (!HostRelayPlanner.CanAcceptSpectatorState(
            _transport.IsHost,
            _transport.LocalClientId,
            _transport.ServerClientId,
            senderClientId,
            state.LocalClientId,
            _peerRegistry,
            out string acceptReason))
        {
            Debug(
                $"Dropped spectator target from sender={senderClientId}, peer={state.LocalClientId}: {acceptReason}.");
            return;
        }

        _remoteTargetRegistry.Update(state);
        Debug(
            $"Spectator target received from peer={state.LocalClientId}: spectating={state.IsSpectating}, targetClient={FormatNullable(state.TargetClientId)}, targetSlot={FormatNullable(state.TargetPlayerSlotId)}.");
        RelaySpectatorTargetState(senderClientId, state);
    }

    private void OnSpectatorPoseReceived(ulong senderClientId, SpectatorPoseState state)
    {
        if (!_initialized || !_config.EnableNetworking.Value || !_config.EnableSpectatorPoseSync.Value)
        {
            return;
        }

        if (!_runtimeState.CanUseModNetworking(out string lifecycleReason))
        {
            Debug($"Dropped spectator pose from sender={senderClientId}: {lifecycleReason}.");
            return;
        }

        if (state.LocalClientId == _transport.LocalClientId)
        {
            return;
        }

        if (!HostRelayPlanner.CanAcceptSpectatorState(
            _transport.IsHost,
            _transport.LocalClientId,
            _transport.ServerClientId,
            senderClientId,
            state.LocalClientId,
            _peerRegistry,
            out string acceptReason))
        {
            Debug(
                $"Dropped spectator pose from sender={senderClientId}, peer={state.LocalClientId}: {acceptReason}.");
            return;
        }

        _remotePoseRegistry.Update(state);
        if (IsPoseDebugEnabled())
        {
            ModLog.Debug(
                $"Spectator pose received from peer={state.LocalClientId}: spectating={state.IsSpectating}, targetClient={FormatNullable(state.TargetClientId)}, position={FormatVector(state.Position)}.");
        }

        RelaySpectatorPoseState(senderClientId, state);
    }

    private void OnPeerIdentityReceived(ulong senderClientId, PeerIdentityState state)
    {
        if (!_initialized || !_config.EnableNetworking.Value || !_config.EnableCapabilityHandshake.Value)
        {
            return;
        }

        if (!_runtimeState.CanUseModNetworking(out string lifecycleReason))
        {
            Debug($"Dropped peer identity from sender={senderClientId}: {lifecycleReason}.");
            return;
        }

        if (state.ClientId == _transport.LocalClientId)
        {
            return;
        }

        if (!HostRelayPlanner.CanAcceptSpectatorState(
            _transport.IsHost,
            _transport.LocalClientId,
            _transport.ServerClientId,
            senderClientId,
            state.ClientId,
            _peerRegistry,
            out string acceptReason))
        {
            Debug($"Dropped peer identity from sender={senderClientId}, peer={state.ClientId}: {acceptReason}.");
            return;
        }

        _remoteIdentityRegistry.Update(state);
        Debug($"Peer identity received from peer={state.ClientId}: slot={state.PlayerSlotId}, name={state.DisplayName}, voiceName={FormatVoiceName(state.VoicePlayerName)}.");
        RelayPeerIdentity(senderClientId, state);
    }

    private void OnVoiceActivityReceived(ulong senderClientId, VoiceActivityState state)
    {
        if (!_initialized || !_config.EnableNetworking.Value || !_config.EnableVoiceActivitySync.Value)
        {
            return;
        }

        if (!_runtimeState.CanUseModNetworking(out string lifecycleReason))
        {
            DebugVoice($"Dropped voice activity from sender={senderClientId}: {lifecycleReason}.");
            return;
        }

        if (state.ClientId == _transport.LocalClientId)
        {
            return;
        }

        if (!HostRelayPlanner.CanAcceptSpectatorState(
            _transport.IsHost,
            _transport.LocalClientId,
            _transport.ServerClientId,
            senderClientId,
            state.ClientId,
            _peerRegistry,
            out string acceptReason))
        {
            DebugVoice($"Dropped voice activity from sender={senderClientId}, peer={state.ClientId}: {acceptReason}.");
            return;
        }

        _remoteVoiceActivityRegistry.Update(state, _runtimeState.UtcNowTicks);
        DebugReceivedVoiceActivity(senderClientId, state);
        RelayVoiceActivityState(senderClientId, state);
    }

    private void RelayCapabilityToCompatiblePeers(ModPeerCapability capability)
    {
        if (!_transport.IsHost || !_config.EnableHostRelay.Value)
        {
            return;
        }

        IReadOnlyList<ulong> recipients = HostRelayPlanner.GetRelayRecipients(
            _peerRegistry,
            _transport.LocalClientId,
            capability.ClientId);
        if (recipients.Count == 0)
        {
            return;
        }

        if (_transport.SendCapability(capability, recipients, out string reason))
        {
            Debug($"Relayed capability for peer={capability.ClientId} to {recipients.Count} peer(s).");
        }
        else
        {
            Debug($"Capability relay for peer={capability.ClientId} failed: {reason}.");
        }
    }

    private void RelayKnownCapabilitiesToPeer(ulong recipientClientId)
    {
        if (!_transport.IsHost || !_config.EnableHostRelay.Value)
        {
            return;
        }

        List<ModPeerCapability> capabilities = _peerRegistry.GetCapabilitiesSnapshot();
        foreach (ModPeerCapability knownCapability in capabilities)
        {
            if (knownCapability.ClientId == _transport.LocalClientId
                || knownCapability.ClientId == recipientClientId
                || !knownCapability.HandshakeComplete
                || knownCapability.ProtocolVersion != ModNetworkConstants.ProtocolVersion
                || !knownCapability.SupportsCapabilityHandshake
                || !knownCapability.SupportsSpectatorTargetSync)
            {
                continue;
            }

            if (_transport.SendCapability(knownCapability, new[] { recipientClientId }, out string reason))
            {
                Debug($"Relayed known capability for peer={knownCapability.ClientId} to peer={recipientClientId}.");
            }
            else
            {
                Debug($"Known capability relay for peer={knownCapability.ClientId} to peer={recipientClientId} failed: {reason}.");
            }
        }
    }

    private void RelayKnownSpectatorStatesToPeer(ulong recipientClientId)
    {
        if (!_transport.IsHost || !_config.EnableHostRelay.Value)
        {
            return;
        }

        List<SpectatorTargetState> targets = _remoteTargetRegistry.GetSnapshot();
        foreach (SpectatorTargetState state in targets)
        {
            if (state.LocalClientId == _transport.LocalClientId || state.LocalClientId == recipientClientId)
            {
                continue;
            }

            if (!IsConnectedForKnownState(state.LocalClientId))
            {
                Debug($"Skipped known spectator target relay for peer={state.LocalClientId} to peer={recipientClientId}: origin is not connected.");
                continue;
            }

            if (state.TargetClientId.HasValue && !IsConnectedForKnownState(state.TargetClientId.Value))
            {
                Debug($"Skipped known spectator target relay for peer={state.LocalClientId} to peer={recipientClientId}: target={state.TargetClientId.Value} is not connected.");
                continue;
            }

            SpectatorTargetSyncMessage message = new SpectatorTargetSyncMessage(
                ModNetworkConstants.ProtocolVersion,
                state,
                _runtimeState.UtcNowTicks);
            if (_transport.SendSpectatorTarget(message, new[] { recipientClientId }, out string reason))
            {
                Debug($"Relayed known spectator target for peer={state.LocalClientId} to peer={recipientClientId}.");
            }
            else
            {
                Debug($"Known spectator target relay for peer={state.LocalClientId} to peer={recipientClientId} failed: {reason}.");
            }
        }

        if (!_config.EnableSpectatorPoseSync.Value)
        {
            return;
        }

        List<SpectatorPoseState> poses = _remotePoseRegistry.GetSnapshot();
        foreach (SpectatorPoseState state in poses)
        {
            if (state.LocalClientId == _transport.LocalClientId || state.LocalClientId == recipientClientId)
            {
                continue;
            }

            if (!IsConnectedForKnownState(state.LocalClientId))
            {
                DebugPose($"Skipped known spectator pose relay for peer={state.LocalClientId} to peer={recipientClientId}: origin is not connected.");
                continue;
            }

            if (state.TargetClientId.HasValue && !IsConnectedForKnownState(state.TargetClientId.Value))
            {
                DebugPose($"Skipped known spectator pose relay for peer={state.LocalClientId} to peer={recipientClientId}: target={state.TargetClientId.Value} is not connected.");
                continue;
            }

            if (!HasMatchingStoredTarget(state))
            {
                DebugPose($"Skipped known spectator pose relay for peer={state.LocalClientId} to peer={recipientClientId}: no matching active target.");
                continue;
            }

            SpectatorPoseSyncMessage message = new SpectatorPoseSyncMessage(
                ModNetworkConstants.ProtocolVersion,
                state,
                _runtimeState.UtcNowTicks);
            if (_transport.SendSpectatorPose(message, new[] { recipientClientId }, out string reason))
            {
                DebugPose($"Relayed known spectator pose for peer={state.LocalClientId} to peer={recipientClientId}.");
            }
            else
            {
                DebugPose($"Known spectator pose relay for peer={state.LocalClientId} to peer={recipientClientId} failed: {reason}.");
            }
        }
    }

    private bool HasMatchingStoredPose(SpectatorTargetState targetState)
    {
        return _remotePoseRegistry.TryGet(targetState.LocalClientId, out SpectatorPoseState poseState)
            && poseState.IsSpectating
            && poseState.TargetClientId == targetState.TargetClientId
            && poseState.TargetPlayerSlotId == targetState.TargetPlayerSlotId;
    }

    private bool HasMatchingStoredTarget(SpectatorPoseState poseState)
    {
        return _remoteTargetRegistry.TryGet(poseState.LocalClientId, out SpectatorTargetState targetState)
            && targetState.IsSpectating
            && targetState.TargetClientId == poseState.TargetClientId
            && targetState.TargetPlayerSlotId == poseState.TargetPlayerSlotId;
    }

    private bool IsConnectedForKnownState(ulong clientId)
    {
        return clientId == _transport.LocalClientId || _transport.IsPeerConnected(clientId);
    }

    private bool IsConnectedForRelayState(
        ulong originClientId,
        bool isSpectating,
        ulong? targetClientId,
        out string reason)
    {
        if (!IsConnectedForKnownState(originClientId))
        {
            reason = "origin is not connected";
            return false;
        }

        if (isSpectating && targetClientId.HasValue && !IsConnectedForKnownState(targetClientId.Value))
        {
            reason = $"target={targetClientId.Value} is not connected";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void RelaySpectatorTargetState(ulong senderClientId, SpectatorTargetState state)
    {
        if (!_transport.IsHost
            || !_config.EnableHostRelay.Value
            || senderClientId != state.LocalClientId)
        {
            return;
        }

        if (!IsConnectedForRelayState(state.LocalClientId, state.IsSpectating, state.TargetClientId, out string relaySkipReason))
        {
            Debug($"Skipped spectator target relay from peer={state.LocalClientId}: {relaySkipReason}.");
            return;
        }

        IReadOnlyList<ulong> recipients = HostRelayPlanner.GetRelayRecipients(
            _peerRegistry,
            _transport.LocalClientId,
            state.LocalClientId);
        if (recipients.Count == 0)
        {
            return;
        }

        SpectatorTargetSyncMessage message = new SpectatorTargetSyncMessage(
            ModNetworkConstants.ProtocolVersion,
            state,
            _runtimeState.UtcNowTicks);
        if (_transport.SendSpectatorTarget(message, recipients, out string reason))
        {
            Debug($"Relayed spectator target from peer={state.LocalClientId} to {recipients.Count} peer(s).");
        }
        else
        {
            Debug($"Spectator target relay from peer={state.LocalClientId} failed: {reason}.");
        }
    }

    private void RelaySpectatorPoseState(ulong senderClientId, SpectatorPoseState state)
    {
        if (!_transport.IsHost
            || !_config.EnableHostRelay.Value
            || senderClientId != state.LocalClientId)
        {
            return;
        }

        if (!IsConnectedForRelayState(state.LocalClientId, state.IsSpectating, state.TargetClientId, out string relaySkipReason))
        {
            DebugPose($"Skipped spectator pose relay from peer={state.LocalClientId}: {relaySkipReason}.");
            return;
        }

        IReadOnlyList<ulong> recipients = HostRelayPlanner.GetRelayRecipients(
            _peerRegistry,
            _transport.LocalClientId,
            state.LocalClientId);
        if (recipients.Count == 0)
        {
            return;
        }

        SpectatorPoseSyncMessage message = new SpectatorPoseSyncMessage(
            ModNetworkConstants.ProtocolVersion,
            state,
            _runtimeState.UtcNowTicks);
        if (_transport.SendSpectatorPose(message, recipients, out string reason))
        {
            DebugPose($"Relayed spectator pose from peer={state.LocalClientId} to {recipients.Count} peer(s).");
        }
        else
        {
            DebugPose($"Spectator pose relay from peer={state.LocalClientId} failed: {reason}.");
        }
    }

    private void RelayVoiceActivityState(ulong senderClientId, VoiceActivityState state)
    {
        if (!_transport.IsHost
            || !_config.EnableHostRelay.Value
            || senderClientId != state.ClientId)
        {
            return;
        }

        if (!IsConnectedForKnownState(state.ClientId))
        {
            DebugVoice($"Skipped voice activity relay from peer={state.ClientId}: origin is not connected.");
            return;
        }

        IReadOnlyList<ulong> recipients = HostRelayPlanner.GetVoiceActivityRelayRecipients(
            _peerRegistry,
            _transport.LocalClientId,
            state.ClientId);
        if (recipients.Count == 0)
        {
            return;
        }

        VoiceActivitySyncMessage message = new VoiceActivitySyncMessage(
            ModNetworkConstants.ProtocolVersion,
            state,
            _runtimeState.UtcNowTicks);
        if (_transport.SendVoiceActivity(message, recipients, out string reason))
        {
            DebugRelayedVoiceActivity(state, recipients.Count);
        }
        else
        {
            DebugVoice($"Voice activity relay from peer={state.ClientId} failed: {reason}.");
        }
    }

    private void RelayDisconnectedPeerCleanup(ulong peerId)
    {
        if (!_transport.IsHost || !_config.EnableHostRelay.Value)
        {
            return;
        }

        IReadOnlyList<ulong> recipients = HostRelayPlanner.GetRelayRecipients(
            _peerRegistry,
            _transport.LocalClientId,
            peerId);
        if (recipients.Count == 0)
        {
            return;
        }

        ModPeerCapability cleanupCapability = new ModPeerCapability(
            peerId,
            ModNetworkConstants.ProtocolVersion,
            false,
            false,
            false,
            _runtimeState.UtcNowTicks,
            false);
        if (_transport.SendCapability(cleanupCapability, recipients, out string capabilityReason))
        {
            Debug($"Relayed disconnected peer capability cleanup for peer={peerId} to {recipients.Count} peer(s).");
        }
        else
        {
            Debug($"Disconnected peer capability cleanup relay for peer={peerId} failed: {capabilityReason}.");
        }

        SpectatorTargetState targetState = new SpectatorTargetState(
            false,
            peerId,
            0,
            null,
            null,
            _runtimeState.UtcNowTicks);
        SpectatorTargetSyncMessage targetMessage = new SpectatorTargetSyncMessage(
            ModNetworkConstants.ProtocolVersion,
            targetState,
            _runtimeState.UtcNowTicks);
        if (_transport.SendSpectatorTarget(targetMessage, recipients, out string targetReason))
        {
            Debug($"Relayed disconnected peer target cleanup for peer={peerId} to {recipients.Count} peer(s).");
        }
        else
        {
            Debug($"Disconnected peer target cleanup relay for peer={peerId} failed: {targetReason}.");
        }

        if (_config.EnableVoiceActivitySync.Value)
        {
            IReadOnlyList<ulong> voiceRecipients = HostRelayPlanner.GetVoiceActivityRelayRecipients(
                _peerRegistry,
                _transport.LocalClientId,
                peerId);
            VoiceActivityState voiceState = new VoiceActivityState(
                false,
                false,
                0f,
                0f,
                peerId,
                0,
                _runtimeState.UtcNowTicks);
            VoiceActivitySyncMessage voiceMessage = new VoiceActivitySyncMessage(
                ModNetworkConstants.ProtocolVersion,
                voiceState,
                _runtimeState.UtcNowTicks);
            if (voiceRecipients.Count > 0)
            {
                if (_transport.SendVoiceActivity(voiceMessage, voiceRecipients, out string voiceReason))
                {
                    DebugVoice($"Relayed disconnected peer voice activity cleanup for peer={peerId} to {voiceRecipients.Count} peer(s).");
                }
                else
                {
                    DebugVoice($"Disconnected peer voice activity cleanup relay for peer={peerId} failed: {voiceReason}.");
                }
            }
        }

        if (_config.EnableSpectatorPoseSync.Value)
        {
            SpectatorPoseState poseState = new SpectatorPoseState(
                false,
                peerId,
                0,
                null,
                null,
                Vector3.zero,
                Quaternion.identity,
                _runtimeState.UtcNowTicks);
            SpectatorPoseSyncMessage poseMessage = new SpectatorPoseSyncMessage(
                ModNetworkConstants.ProtocolVersion,
                poseState,
                _runtimeState.UtcNowTicks);
            if (_transport.SendSpectatorPose(poseMessage, recipients, out string poseReason))
            {
                DebugPose($"Relayed disconnected peer pose cleanup for peer={peerId} to {recipients.Count} peer(s).");
            }
            else
            {
                DebugPose($"Disconnected peer pose cleanup relay for peer={peerId} failed: {poseReason}.");
            }
        }
    }

    private void SendLocalPeerIdentityToRecipients(IEnumerable<ulong> recipients)
    {
        if (!_peerIdentityStateProvider.TryGetLocalPeerIdentity(out PeerIdentityState identity)
            || string.IsNullOrWhiteSpace(identity.DisplayName))
        {
            return;
        }

        if (_transport.SendPeerIdentity(identity, recipients, out string reason))
        {
            _lastSentIdentityState = identity;
            Debug($"Peer identity reply sent: client={identity.ClientId}, slot={identity.PlayerSlotId}, name={identity.DisplayName}, voiceName={FormatVoiceName(identity.VoicePlayerName)}.");
        }
        else
        {
            Debug($"Peer identity reply failed: {reason}.");
        }
    }

    private void SendLocalSpectatorStateToPeer(ulong recipientClientId)
    {
        if (!_config.EnableSpectatorTargetSync.Value
            || !_spectatorTargetStateProvider.TryGetCurrentSpectatorTarget(out SpectatorTargetState targetState)
            || !targetState.IsSpectating)
        {
            return;
        }

        if (!IsConnectedForRelayState(
            targetState.LocalClientId,
            targetState.IsSpectating,
            targetState.TargetClientId,
            out string targetSkipReason))
        {
            Debug($"Skipped local spectator target replay to peer={recipientClientId}: {targetSkipReason}.");
            return;
        }

        SpectatorTargetSyncMessage targetMessage = new SpectatorTargetSyncMessage(
            ModNetworkConstants.ProtocolVersion,
            targetState,
            _runtimeState.UtcNowTicks);
        if (_transport.SendSpectatorTarget(targetMessage, new[] { recipientClientId }, out string targetReason))
        {
            Debug($"Replayed local spectator target to peer={recipientClientId}.");
        }
        else
        {
            Debug($"Local spectator target replay to peer={recipientClientId} failed: {targetReason}.");
        }

        if (!_config.EnableSpectatorPoseSync.Value
            || !_spectatorPoseStateProvider.TryGetCurrentSpectatorPose(out SpectatorPoseState poseState)
            || !poseState.IsSpectating
            || !HasMatchingLocalTarget(targetState, poseState))
        {
            return;
        }

        SpectatorPoseSyncMessage poseMessage = new SpectatorPoseSyncMessage(
            ModNetworkConstants.ProtocolVersion,
            poseState,
            _runtimeState.UtcNowTicks);
        if (_transport.SendSpectatorPose(poseMessage, new[] { recipientClientId }, out string poseReason))
        {
            DebugPose($"Replayed local spectator pose to peer={recipientClientId}.");
        }
        else
        {
            DebugPose($"Local spectator pose replay to peer={recipientClientId} failed: {poseReason}.");
        }
    }

    private static bool HasMatchingLocalTarget(SpectatorTargetState targetState, SpectatorPoseState poseState)
    {
        return targetState.IsSpectating
            && poseState.TargetClientId == targetState.TargetClientId
            && poseState.TargetPlayerSlotId == targetState.TargetPlayerSlotId;
    }

    private void RelayPeerIdentity(ulong senderClientId, PeerIdentityState state)
    {
        if (!_transport.IsHost
            || !_config.EnableHostRelay.Value
            || senderClientId != state.ClientId)
        {
            return;
        }

        IReadOnlyList<ulong> recipients = HostRelayPlanner.GetRelayRecipients(
            _peerRegistry,
            _transport.LocalClientId,
            state.ClientId);
        if (recipients.Count == 0)
        {
            return;
        }

        if (_transport.SendPeerIdentity(state, recipients, out string reason))
        {
            Debug($"Relayed peer identity from peer={state.ClientId} to {recipients.Count} peer(s).");
        }
        else
        {
            Debug($"Peer identity relay from peer={state.ClientId} failed: {reason}.");
        }
    }

    private void RelayKnownPeerIdentitiesToPeer(ulong recipientClientId)
    {
        if (!_transport.IsHost || !_config.EnableHostRelay.Value)
        {
            return;
        }

        List<PeerIdentityState> identities = _remoteIdentityRegistry.GetSnapshot();
        foreach (PeerIdentityState identity in identities)
        {
            if (identity.ClientId == _transport.LocalClientId || identity.ClientId == recipientClientId)
            {
                continue;
            }

            if (_transport.SendPeerIdentity(identity, new[] { recipientClientId }, out string reason))
            {
                Debug($"Relayed known peer identity for peer={identity.ClientId} to peer={recipientClientId}.");
            }
            else
            {
                Debug($"Known peer identity relay for peer={identity.ClientId} to peer={recipientClientId} failed: {reason}.");
            }
        }
    }

    private void RelayKnownVoiceActivitiesToPeer(ulong recipientClientId)
    {
        if (!_transport.IsHost || !_config.EnableHostRelay.Value || !_config.EnableVoiceActivitySync.Value)
        {
            return;
        }

        if (!_peerRegistry.TryGetCapability(recipientClientId, out ModPeerCapability recipientCapability)
            || !ModPeerCapabilityRules.SupportsCurrentVoiceActivitySync(recipientCapability))
        {
            return;
        }

        List<VoiceActivityState> states = _remoteVoiceActivityRegistry.GetSnapshot();
        foreach (VoiceActivityState state in states)
        {
            if (state.ClientId == _transport.LocalClientId || state.ClientId == recipientClientId)
            {
                continue;
            }

            if (!IsConnectedForKnownState(state.ClientId))
            {
                DebugVoice($"Skipped known voice activity relay for peer={state.ClientId} to peer={recipientClientId}: origin is not connected.");
                continue;
            }

            VoiceActivitySyncMessage message = new VoiceActivitySyncMessage(
                ModNetworkConstants.ProtocolVersion,
                state,
                _runtimeState.UtcNowTicks);
            if (_transport.SendVoiceActivity(message, new[] { recipientClientId }, out string reason))
            {
                DebugVoice($"Relayed known voice activity for peer={state.ClientId} to peer={recipientClientId}.");
            }
            else
            {
                DebugVoice($"Known voice activity relay for peer={state.ClientId} to peer={recipientClientId} failed: {reason}.");
            }
        }
    }

    private void Degrade(string reason)
    {
        _transport.Unregister();
        ClearNetworkState();
        _lifecycleState = NetworkLifecycleState.LocalOnly;

        if (_lastDegradationReason == reason)
        {
            return;
        }

        _lastDegradationReason = reason;
        Debug($"Networking degraded to local-only mode: {reason}.");
    }

    private void StopNetworkingForLifecycle(string reason)
    {
        if (_transport.IsRegistered)
        {
            _transport.Unregister();
        }

        ClearNetworkState();
        _lifecycleState = NetworkLifecycleState.LocalOnly;

        if (_lastDegradationReason == reason)
        {
            return;
        }

        _lastDegradationReason = reason;
        if (_runtimeState.FrameCount >= _nextLifecycleDebugFrame)
        {
            _nextLifecycleDebugFrame = _runtimeState.FrameCount + 120;
            Debug($"Networking stopped because shutdown/disconnect was detected: {reason}.");
        }
    }

    private void ClearNetworkState()
    {
        _peerRegistry.Clear();
        _remoteTargetRegistry.Clear();
        _remotePoseRegistry.Clear();
        _remoteIdentityRegistry.Clear();
        _remoteVoiceActivityRegistry.Clear();
        _lastObservedTargetState = null;
        _lastSentTargetState = null;
        _pendingTargetState = null;
        _lastObservedPoseState = null;
        _lastSentPoseState = null;
        _pendingPoseState = null;
        _pendingPoseRefresh = false;
        _lastObservedVoiceActivityState = null;
        _lastSentVoiceActivityState = null;
        _pendingVoiceActivityState = null;
        _pendingVoiceActivityRefresh = false;
        _lastSentIdentityState = null;
        _networkAvailable = false;
        _targetSyncReady = false;
        _hasCompatibleModPeer = false;
        _noCompatiblePeerLocalOnly = false;
        _capabilitySent = false;
        _capabilityProbeSentRealtime = -1f;
        _lastLocalClientId = null;
        _nextPeerPruneTime = 0f;
        _transportRegisteredRealtime = 0f;
        _transportRegisteredFrame = -1;
        _lastTargetSyncWaitReason = null;
        _nextTargetSyncTime = 0f;
        _nextPoseSyncTime = 0f;
        _nextVoiceActivitySyncTime = 0f;
        _nextTargetSampleTime = 0f;
        _nextPoseSampleTime = 0f;
        _lastPoseSampleTime = -1f;
        _nextPoseRefreshTime = 0f;
        _nextVoiceActivitySampleTime = 0f;
        _nextVoiceActivityRefreshTime = 0f;
        _voiceDebugLimiter.Clear();
    }

    private ModPeerCapability CreateLocalCapability(ulong localClientId)
    {
        return new ModPeerCapability(
            localClientId,
            ModNetworkConstants.ProtocolVersion,
            _config.EnableCapabilityHandshake.Value,
            _config.EnableSpectatorTargetSync.Value,
            _config.EnableCapabilityHandshake.Value,
            _runtimeState.UtcNowTicks,
            _config.EnableVoiceActivitySync.Value,
            _config.EnableSpectatorVoiceToTarget.Value);
    }

    private void Debug(string message)
    {
        if (_config.DebugNetworkMessages.Value)
        {
            ModLog.Debug(message);
        }
    }

    private void DebugPose(string message)
    {
        if (IsPoseDebugEnabled())
        {
            ModLog.Debug(message);
        }
    }

    private bool IsPoseDebugEnabled()
    {
        return _config.DebugNetworkMessages.Value && _config.DebugPoseMessages.Value;
    }

    private float GetPoseSyncInterval()
    {
        return Mathf.Max(0.02f, _config.SpectatorPoseSyncInterval.Value);
    }

    private float GetPoseHeartbeatInterval()
    {
        return Mathf.Max(SpectatorPoseHeartbeatIntervalSeconds, GetPoseSyncInterval() * 5f);
    }

    private float GetVoiceActivitySyncInterval()
    {
        return NetworkSyncSamplingRules.ResolveVoiceActivitySyncInterval(_config.VoiceActivitySyncInterval.Value);
    }

    private float GetVoiceActivityRefreshInterval()
    {
        float staleSeconds = Mathf.Max(0.1f, _config.VoiceActivityStaleSeconds.Value);
        return Mathf.Clamp(staleSeconds * 0.5f, 0.1f, 0.25f);
    }

    private bool ShouldRefreshVoiceActivity(VoiceActivityState state)
    {
        if (!state.HasData || (!state.IsSpeaking && state.Amplitude <= 0f))
        {
            return false;
        }

        return _runtimeState.UnscaledTime >= _nextVoiceActivityRefreshTime;
    }

    private bool TryGetLocalVoiceIdentity(out ulong clientId, out ulong slotId)
    {
        if (_peerIdentityStateProvider.TryGetLocalPeerIdentity(out PeerIdentityState identity))
        {
            clientId = identity.ClientId;
            slotId = identity.PlayerSlotId;
            return true;
        }

        if (_transport.IsRegistered)
        {
            clientId = _transport.LocalClientId;
            slotId = 0;
            return true;
        }

        clientId = 0;
        slotId = 0;
        return false;
    }

    private void DebugVoice(string message)
    {
        if (IsVoiceDebugEnabled())
        {
            ModLog.Debug(message);
        }
    }

    private void DebugObservedVoiceActivity(VoiceActivityState state)
    {
        if (!ShouldLogVoiceActivity("observed", state.ClientId, state, isRelayed: false))
        {
            return;
        }

        ModLog.Debug(
            $"Observed local voice activity change: hasData={state.HasData}, speaking={state.IsSpeaking}, amplitude={state.Amplitude:0.00}, client={state.ClientId}, slot={state.SlotId}.");
    }

    private void DebugSentVoiceActivity(VoiceActivityState state, int recipientCount)
    {
        if (!ShouldLogVoiceActivity("sent", state.ClientId, state, isRelayed: false))
        {
            return;
        }

        ModLog.Debug(
            $"Voice activity sent to {recipientCount} peer(s): hasData={state.HasData}, speaking={state.IsSpeaking}, amplitude={state.Amplitude:0.00}.");
    }

    private void DebugReceivedVoiceActivity(ulong senderClientId, VoiceActivityState state)
    {
        bool isRelayed = senderClientId != state.ClientId;
        if (!ShouldLogVoiceActivity("received", state.ClientId, state, isRelayed))
        {
            return;
        }

        ModLog.Debug(
            $"Voice activity received from peer={state.ClientId}: hasData={state.HasData}, speaking={state.IsSpeaking}, amplitude={state.Amplitude:0.00}, relayed={isRelayed}.");
    }

    private void DebugRelayedVoiceActivity(VoiceActivityState state, int recipientCount)
    {
        if (!ShouldLogVoiceActivity("relayed", state.ClientId, state, isRelayed: true))
        {
            return;
        }

        ModLog.Debug($"Relayed voice activity from peer={state.ClientId} to {recipientCount} peer(s).");
    }

    private bool ShouldLogVoiceActivity(string category, ulong peerId, VoiceActivityState state, bool isRelayed)
    {
        return IsVoiceDebugEnabled()
            && _voiceDebugLimiter.ShouldLog(category, peerId, _runtimeState.FrameCount, state, isRelayed);
    }

    private bool IsVoiceDebugEnabled()
    {
        return _config.DebugNetworkMessages.Value && _config.DebugVoiceActivitySync.Value;
    }

    private void DebugCapabilityDelay(string reason)
    {
        if (_config.DebugNetworkMessages.Value && _runtimeState.FrameCount >= _nextCapabilityDelayDebugFrame)
        {
            _nextCapabilityDelayDebugFrame = _runtimeState.FrameCount + 120;
            ModLog.Debug($"Capability delayed because network is not stable yet: {reason}.");
        }
    }

    private static string FormatNullable(ulong? value)
    {
        return value.HasValue ? value.Value.ToString() : "none";
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:0.00}, {value.y:0.00}, {value.z:0.00})";
    }

    private static string FormatVoiceName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "none" : "present";
    }

    private static bool IdentityEquals(PeerIdentityState? left, PeerIdentityState right)
    {
        return left != null
            && left.ClientId == right.ClientId
            && left.PlayerSlotId == right.PlayerSlotId
            && string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
            && string.Equals(left.VoicePlayerName, right.VoicePlayerName, StringComparison.Ordinal);
    }
}
