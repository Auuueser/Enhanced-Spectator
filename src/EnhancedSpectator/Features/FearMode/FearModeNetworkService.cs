using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Networking;
using EnhancedSpectator.Runtime;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Owns the isolated optional fear protocol and host-validated per-player selections.
/// </summary>
public sealed class FearModeNetworkService : IFearModeStateProvider, IDisposable
{
    private const int CapabilityRetryFrames = 300;
    private const int DefaultSelectionRetryFrames = 300;

    private readonly EnhancedSpectatorConfig _config;
    private readonly IGameFearModeAdapter _gameAdapter;
    private readonly FearModelCatalog _catalog;
    private readonly FearModeSelectionRegistry _selections = new FearModeSelectionRegistry();
    private readonly HashSet<ulong> _capablePeers = new HashSet<ulong>();
    private readonly HashSet<ulong> _soundCapablePeers = new HashSet<ulong>();
    private readonly List<FearModeSelectionState> _selectionScratch = new List<FearModeSelectionState>();
    private readonly List<AudioClip> _soundClipScratch = new List<AudioClip>();
    private readonly Queue<FearSoundEventState> _pendingSoundEvents = new Queue<FearSoundEventState>();
    private readonly Dictionary<ulong, long> _lastAcceptedSoundTicks = new Dictionary<ulong, long>();

    private NetworkManager? _networkManager;
    private CustomMessagingManager? _messagingManager;
    private bool _registered;
    private bool _disposed;
    private bool _sessionEnabled;
    private bool _lastHostGate;
    private bool _hasReceivedSession;
    private int _nextCapabilityFrame;
    private int _nextCatalogRefreshFrame;
    private int _nextDefaultSelectionFrame;
    private long _localSelectionRevision;
    private long _localSoundRequestSequence;
    private long _hostSoundSequence;
    private long _lastReceivedSoundSequence;

    /// <summary>Creates the fear-mode network service.</summary>
    public FearModeNetworkService(
        EnhancedSpectatorConfig config,
        IGameFearModeAdapter gameAdapter,
        FearModelCatalog catalog)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _gameAdapter = gameAdapter ?? throw new ArgumentNullException(nameof(gameAdapter));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <inheritdoc />
    public bool IsSessionEnabled => _sessionEnabled;

    /// <summary>Gets the current model catalog.</summary>
    public FearModelCatalog Catalog => _catalog;

    /// <summary>Refreshes the runtime catalog immediately when the user opens its retained view.</summary>
    public void RefreshCatalogNow()
    {
        RefreshCatalogWhenNeeded(force: true);
    }

    /// <summary>Gets whether this local viewer opted into fear-model rendering.</summary>
    public bool IsLocalRenderingEnabled => _config.RenderFearModelsLocally.Value;

    /// <summary>Gets whether the local transport is currently the host.</summary>
    public bool IsHost => _networkManager != null && _networkManager.IsHost;

    /// <summary>Advances registration, capability, session, and cleanup state.</summary>
    public void Tick()
    {
        if (_disposed)
        {
            return;
        }

        if (!RuntimeConnectionState.CanUseModNetworking(out _))
        {
            Unregister(clearState: true);
            return;
        }

        if (!EnsureRegistered())
        {
            return;
        }

        RefreshCatalogWhenNeeded(force: false);
        NetworkManager manager = _networkManager!;
        if (manager.IsHost)
        {
            bool desiredGate = _config.EnableFearModeAsHost.Value;
            if (desiredGate != _lastHostGate)
            {
                _lastHostGate = desiredGate;
                _sessionEnabled = desiredGate;
                if (!desiredGate)
                {
                    _selections.Clear();
                }

                BroadcastSession();
                ModLog.Info(desiredGate
                    ? "Fear mode session gate enabled by host."
                    : "Fear mode session gate disabled by host.");
            }
        }
        else if (!_hasReceivedSession && Time.frameCount >= _nextCapabilityFrame)
        {
            _nextCapabilityFrame = Time.frameCount + CapabilityRetryFrames;
            SendCapabilityToHost();
        }

        EnsureLocalDefaultSelection();
    }

    /// <summary>Attempts to submit a model selection for the local dead player.</summary>
    public bool TrySelectLocalModel(string modelKey, out string reason)
    {
        reason = string.Empty;
        if (!_registered || _networkManager == null || _messagingManager == null)
        {
            reason = "fear transport unavailable";
            return false;
        }

        if (!_sessionEnabled)
        {
            reason = "host fear-mode gate is disabled";
            return false;
        }

        if (!_gameAdapter.TryGetLocalDeadPlayerIdentity(out ulong clientId, out ulong slotId))
        {
            reason = "local player is not dead";
            return false;
        }

        if (!_catalog.IsAllowed(modelKey))
        {
            reason = "model key is not in the current safe catalog";
            return false;
        }

        if (!_networkManager.IsHost
            && _selections.TryGet(clientId, out FearModeSelectionState current)
            && !string.Equals(current.ModelKey, modelKey, StringComparison.Ordinal))
        {
            TryStopLocalSound(out _);
        }

        _localSelectionRevision++;
        FearModeSelectionState state = new FearModeSelectionState(
            clientId,
            slotId,
            modelKey,
            _localSelectionRevision);

        if (_networkManager.IsHost)
        {
            if (!TryAcceptHostSelection(
                _networkManager.LocalClientId,
                state,
                out FearModeSelectionState accepted,
                out reason))
            {
                return false;
            }

            BroadcastSelection(accepted);
            return true;
        }

        return SendSelection(NetworkManager.ServerClientId, state, out reason);
    }

    /// <inheritdoc />
    public bool TryGetSelection(ulong clientId, out FearModeSelectionState selection)
    {
        return _selections.TryGet(clientId, out selection);
    }

    /// <inheritdoc />
    public void CopySelectionsTo(List<FearModeSelectionState> destination)
    {
        _selections.CopyTo(destination);
    }

    /// <summary>Attempts to request one selected-model sound for the local dead player.</summary>
    public bool TryTriggerLocalSound(int clipIndex, FearSoundAction action, out string reason)
    {
        reason = string.Empty;
        if (!_registered || _networkManager == null || _messagingManager == null)
        {
            reason = "fear sound transport unavailable";
            return false;
        }

        if (!_sessionEnabled || !_config.RenderFearModelsLocally.Value)
        {
            reason = "fear mode is not active locally";
            return false;
        }

        if (!_gameAdapter.TryGetLocalDeadPlayerIdentity(out ulong clientId, out ulong slotId)
            || !_selections.TryGet(clientId, out FearModeSelectionState selection))
        {
            reason = "local dead-player fear selection unavailable";
            return false;
        }

        if (action != FearSoundAction.Play && action != FearSoundAction.PlayNext)
        {
            reason = "invalid local fear sound play action";
            return false;
        }

        _localSoundRequestSequence++;
        FearSoundEventState request = new FearSoundEventState(
            clientId,
            slotId,
            selection.ModelKey,
            action,
            clipIndex,
            _localSoundRequestSequence);
        if (_networkManager.IsHost)
        {
            if (!TryAcceptHostSound(_networkManager.LocalClientId, request, out FearSoundEventState accepted, out reason))
            {
                return false;
            }

            EnqueueSoundEvent(accepted);
            BroadcastSoundEvent(accepted);
            return true;
        }

        return SendSoundPacket(
            FearModeNetworkConstants.SoundRequestMessageName,
            NetworkManager.ServerClientId,
            request,
            out reason);
    }

    /// <summary>Requests that the local dead player's current fear sound stop everywhere.</summary>
    public bool TryStopLocalSound(out string reason)
    {
        reason = string.Empty;
        if (!_registered || _networkManager == null || _messagingManager == null)
        {
            reason = "fear sound transport unavailable";
            return false;
        }

        if (!_sessionEnabled
            || !_gameAdapter.TryGetLocalDeadPlayerIdentity(out ulong clientId, out ulong slotId)
            || !_selections.TryGet(clientId, out FearModeSelectionState selection))
        {
            reason = "local fear sound selection unavailable";
            return false;
        }

        _localSoundRequestSequence++;
        FearSoundEventState request = new FearSoundEventState(
            clientId,
            slotId,
            selection.ModelKey,
            FearSoundAction.Stop,
            -1,
            _localSoundRequestSequence);
        if (_networkManager.IsHost)
        {
            if (!TryAcceptHostSound(_networkManager.LocalClientId, request, out FearSoundEventState accepted, out reason))
            {
                return false;
            }

            EnqueueSoundEvent(accepted);
            BroadcastSoundEvent(accepted);
            return true;
        }

        if (!SendSoundPacket(
            FearModeNetworkConstants.SoundRequestMessageName,
            NetworkManager.ServerClientId,
            request,
            out reason))
        {
            return false;
        }

        // The initiating client should stop immediately instead of waiting for the host round trip.
        EnqueueSoundEvent(request);
        return true;
    }

    /// <summary>Attempts to dequeue one host-authorized fear sound for local playback.</summary>
    public bool TryDequeueSoundEvent(out FearSoundEventState state)
    {
        if (_pendingSoundEvents.Count > 0)
        {
            state = _pendingSoundEvents.Dequeue();
            return true;
        }

        state = null!;
        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Unregister(clearState: true);
    }

    private bool EnsureRegistered()
    {
        NetworkManager? manager = NetworkManager.Singleton;
        if (_registered)
        {
            if (ReferenceEquals(manager, _networkManager)
                && manager != null
                && (manager.IsClient || manager.IsHost)
                && ReferenceEquals(manager.CustomMessagingManager, _messagingManager))
            {
                return true;
            }

            Unregister(clearState: true);
        }

        if (manager == null || (!manager.IsClient && !manager.IsHost) || manager.CustomMessagingManager == null)
        {
            return false;
        }

        try
        {
            _networkManager = manager;
            _messagingManager = manager.CustomMessagingManager;
            _messagingManager.RegisterNamedMessageHandler(
                FearModeNetworkConstants.CapabilityMessageName,
                HandleCapabilityMessage);
            _messagingManager.RegisterNamedMessageHandler(
                FearModeNetworkConstants.SessionMessageName,
                HandleSessionMessage);
            _messagingManager.RegisterNamedMessageHandler(
                FearModeNetworkConstants.SelectionMessageName,
                HandleSelectionMessage);
            _messagingManager.RegisterNamedMessageHandler(
                FearModeNetworkConstants.SoundCapabilityMessageName,
                HandleSoundCapabilityMessage);
            _messagingManager.RegisterNamedMessageHandler(
                FearModeNetworkConstants.SoundRequestMessageName,
                HandleSoundRequestMessage);
            _messagingManager.RegisterNamedMessageHandler(
                FearModeNetworkConstants.SoundEventMessageName,
                HandleSoundEventMessage);
            manager.OnClientDisconnectCallback += OnClientDisconnected;
            _registered = true;
            _sessionEnabled = manager.IsHost && _config.EnableFearModeAsHost.Value;
            _lastHostGate = _sessionEnabled;
            _hasReceivedSession = manager.IsHost;
            _nextCapabilityFrame = 0;
            _nextCatalogRefreshFrame = 0;
            ModLog.Debug("Registered isolated fear-mode named message handlers.");
            return true;
        }
        catch (Exception ex)
        {
            ModLog.Warning($"Fear-mode handler registration failed: {ex.GetType().Name}.");
            Unregister(clearState: true);
            return false;
        }
    }

    private void Unregister(bool clearState)
    {
        if (_networkManager != null)
        {
            _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        if (_messagingManager != null)
        {
            try
            {
                _messagingManager.UnregisterNamedMessageHandler(FearModeNetworkConstants.CapabilityMessageName);
                _messagingManager.UnregisterNamedMessageHandler(FearModeNetworkConstants.SessionMessageName);
                _messagingManager.UnregisterNamedMessageHandler(FearModeNetworkConstants.SelectionMessageName);
                _messagingManager.UnregisterNamedMessageHandler(FearModeNetworkConstants.SoundCapabilityMessageName);
                _messagingManager.UnregisterNamedMessageHandler(FearModeNetworkConstants.SoundRequestMessageName);
                _messagingManager.UnregisterNamedMessageHandler(FearModeNetworkConstants.SoundEventMessageName);
            }
            catch (Exception ex)
            {
                ModLog.Debug($"Fear-mode handler cleanup failed: {ex.GetType().Name}.");
            }
        }

        _registered = false;
        _networkManager = null;
        _messagingManager = null;
        _hasReceivedSession = false;
        _capablePeers.Clear();
        _soundCapablePeers.Clear();
        _pendingSoundEvents.Clear();
        _lastReceivedSoundSequence = 0;
        _lastAcceptedSoundTicks.Clear();
        _nextDefaultSelectionFrame = 0;
        if (clearState)
        {
            _sessionEnabled = false;
            _lastHostGate = false;
            _selections.Clear();
        }
    }

    private void RefreshCatalogWhenNeeded(bool force)
    {
        if (!force && Time.frameCount < _nextCatalogRefreshFrame)
        {
            return;
        }

        try
        {
            _catalog.Refresh();
            _nextCatalogRefreshFrame = Time.frameCount
                + FearModelCatalogRefreshRules.ResolveDelayFrames(_catalog.ModelKeys.Count);
        }
        catch (Exception ex)
        {
            _nextCatalogRefreshFrame = Time.frameCount
                + FearModelCatalogRefreshRules.ResolveDelayFrames(modelCount: 0);
            ModLog.Warning($"Fear model catalog refresh failed: {ex.GetType().Name}.");
        }
    }

    private void EnsureLocalDefaultSelection()
    {
        bool isLocalDead = _gameAdapter.TryGetLocalDeadPlayerIdentity(out ulong clientId, out _);
        bool hasSelection = isLocalDead && _selections.TryGet(clientId, out _);
        if (!FearDefaultSelectionRules.ShouldRequestDefault(
            _sessionEnabled,
            isLocalDead,
            hasSelection,
            Time.frameCount,
            _nextDefaultSelectionFrame))
        {
            if (!_sessionEnabled || !isLocalDead || hasSelection)
            {
                _nextDefaultSelectionFrame = 0;
            }

            return;
        }

        _nextDefaultSelectionFrame = Time.frameCount + DefaultSelectionRetryFrames;
        if (TrySelectLocalModel(FearModeRules.DefaultModelKey, out string reason))
        {
            ModLog.Debug("Initialized the local dead-player fear selection to Default.");
            return;
        }

        ModLog.Debug($"Default fear selection initialization deferred: {reason}.");
    }

    private void HandleCapabilityMessage(ulong senderClientId, FastBufferReader payload)
    {
        if (_networkManager == null || !_networkManager.IsHost)
        {
            return;
        }

        FastBufferReader reader = payload;
        if (!FearModeNetworkSerializer.TryReadCapability(ref reader, out string reason))
        {
            ModLog.Debug($"Dropped fear capability from {senderClientId}: {reason}.");
            return;
        }

        _capablePeers.Add(senderClientId);
        SendSession(senderClientId, _sessionEnabled);
        _selections.CopyTo(_selectionScratch);
        for (int index = 0; index < _selectionScratch.Count; index++)
        {
            SendSelection(senderClientId, _selectionScratch[index], out _);
        }
    }

    private void HandleSessionMessage(ulong senderClientId, FastBufferReader payload)
    {
        if (_networkManager == null
            || _networkManager.IsHost
            || senderClientId != NetworkManager.ServerClientId)
        {
            return;
        }

        FastBufferReader reader = payload;
        if (!FearModeNetworkSerializer.TryReadSession(ref reader, out bool enabled, out string reason))
        {
            ModLog.Debug($"Dropped fear session message: {reason}.");
            return;
        }

        _sessionEnabled = enabled;
        _hasReceivedSession = true;
        if (!enabled)
        {
            _selections.Clear();
        }
    }

    private void HandleSelectionMessage(ulong senderClientId, FastBufferReader payload)
    {
        if (_networkManager == null)
        {
            return;
        }

        FastBufferReader reader = payload;
        if (!FearModeNetworkSerializer.TryReadSelection(ref reader, out FearModeSelectionState state, out string reason))
        {
            ModLog.Debug($"Dropped fear selection from {senderClientId}: {reason}.");
            return;
        }

        if (_networkManager.IsHost)
        {
            if (!TryAcceptHostSelection(
                senderClientId,
                state,
                out FearModeSelectionState accepted,
                out reason))
            {
                ModLog.Debug($"Rejected fear selection from {senderClientId}: {reason}.");
                return;
            }

            BroadcastSelection(accepted);
            return;
        }

        if (senderClientId == NetworkManager.ServerClientId && _sessionEnabled)
        {
            _selections.Update(state);
        }
    }

    private void HandleSoundCapabilityMessage(ulong senderClientId, FastBufferReader payload)
    {
        if (_networkManager == null || !_networkManager.IsHost)
        {
            return;
        }

        FastBufferReader reader = payload;
        if (FearSoundNetworkSerializer.TryReadCapability(ref reader, out string reason))
        {
            _soundCapablePeers.Add(senderClientId);
        }
        else
        {
            ModLog.Debug($"Dropped fear sound capability from {senderClientId}: {reason}.");
        }
    }

    private void HandleSoundRequestMessage(ulong senderClientId, FastBufferReader payload)
    {
        if (_networkManager == null || !_networkManager.IsHost)
        {
            return;
        }

        FastBufferReader reader = payload;
        if (!FearSoundNetworkSerializer.TryReadEvent(ref reader, out FearSoundEventState request, out string reason)
            || !TryAcceptHostSound(senderClientId, request, out FearSoundEventState accepted, out reason))
        {
            ModLog.Debug($"Rejected fear sound request from {senderClientId}: {reason}.");
            return;
        }

        EnqueueSoundEvent(accepted);
        BroadcastSoundEvent(accepted);
    }

    private void HandleSoundEventMessage(ulong senderClientId, FastBufferReader payload)
    {
        if (_networkManager == null
            || _networkManager.IsHost
            || senderClientId != NetworkManager.ServerClientId
            || !_sessionEnabled)
        {
            return;
        }

        FastBufferReader reader = payload;
        if (!FearSoundNetworkSerializer.TryReadEvent(ref reader, out FearSoundEventState state, out string reason))
        {
            ModLog.Debug($"Dropped fear sound event: {reason}.");
            return;
        }

        if (state.Sequence <= _lastReceivedSoundSequence)
        {
            ModLog.Debug($"Dropped stale fear sound event sequence {state.Sequence}.");
            return;
        }

        _lastReceivedSoundSequence = state.Sequence;
        EnqueueSoundEvent(state);
    }

    private bool TryAcceptHostSelection(
        ulong senderClientId,
        FearModeSelectionState state,
        out FearModeSelectionState accepted,
        out string reason)
    {
        accepted = null!;
        bool modelAllowed = _catalog.IsAllowed(state.ModelKey);
        if (!FearModeRules.CanHostAcceptSelection(
            isHost: _networkManager != null && _networkManager.IsHost,
            _sessionEnabled,
            senderClientId,
            state.ClientId,
            _gameAdapter.IsPlayerDead(state.ClientId, state.PlayerSlotId),
            modelAllowed))
        {
            reason = "host authority validation failed";
            return false;
        }

        long canonicalRevision = _selections.TryGet(state.ClientId, out FearModeSelectionState previous)
            ? previous.Revision + 1
            : 1;
        accepted = new FearModeSelectionState(
            state.ClientId,
            state.PlayerSlotId,
            state.ModelKey,
            canonicalRevision);
        if (!_selections.Update(accepted))
        {
            reason = "host could not store canonical selection revision";
            return false;
        }

        if (previous != null
            && !string.Equals(previous.ModelKey, accepted.ModelKey, StringComparison.Ordinal)
            && !string.Equals(previous.ModelKey, FearModeRules.DefaultModelKey, StringComparison.Ordinal))
        {
            AuthorizeHostStop(previous);
        }

        reason = string.Empty;
        return true;
    }

    private bool TryAcceptHostSound(
        ulong senderClientId,
        FearSoundEventState request,
        out FearSoundEventState accepted,
        out string reason)
    {
        accepted = null!;
        reason = string.Empty;
        _soundClipScratch.Clear();
        if (request.Action != FearSoundAction.Stop)
        {
            _gameAdapter.CopyFearSoundClipsTo(request.ModelKey, _soundClipScratch);
        }
        long nowTicks = DateTime.UtcNow.Ticks;
        long lastAcceptedTicks = _lastAcceptedSoundTicks.TryGetValue(
            request.ClientId,
            out long previousTicks)
            ? previousTicks
            : 0;
        string selectedModelKey = _selections.TryGet(
            request.ClientId,
            out FearModeSelectionState selection)
            ? selection.ModelKey
            : string.Empty;
        if (!FearSoundRules.CanHostAccept(
            isHost: _networkManager != null && _networkManager.IsHost,
            _sessionEnabled,
            senderClientId,
            request.ClientId,
            _gameAdapter.IsPlayerDead(request.ClientId, request.PlayerSlotId),
            selectedModelKey,
            request.ModelKey,
            request.Action,
            request.ClipIndex,
            _soundClipScratch.Count,
            nowTicks,
            lastAcceptedTicks,
            _config.FearSoundCooldownSeconds.Value))
        {
            reason = "host fear sound validation failed";
            return false;
        }

        _hostSoundSequence++;
        accepted = new FearSoundEventState(
            request.ClientId,
            request.PlayerSlotId,
            request.ModelKey,
            request.Action,
            request.ClipIndex,
            _hostSoundSequence);
        if (request.Action != FearSoundAction.Stop)
        {
            _lastAcceptedSoundTicks[request.ClientId] = nowTicks;
        }
        else
        {
            _lastAcceptedSoundTicks.Remove(request.ClientId);
        }

        return true;
    }

    private void AuthorizeHostStop(FearModeSelectionState previous)
    {
        if (_networkManager == null || !_networkManager.IsHost)
        {
            return;
        }

        _hostSoundSequence++;
        FearSoundEventState stop = new FearSoundEventState(
            previous.ClientId,
            previous.PlayerSlotId,
            previous.ModelKey,
            FearSoundAction.Stop,
            -1,
            _hostSoundSequence);
        _lastAcceptedSoundTicks.Remove(previous.ClientId);
        EnqueueSoundEvent(stop);
        BroadcastSoundEvent(stop);
    }

    private void SendCapabilityToHost()
    {
        if (_networkManager == null || _messagingManager == null || _networkManager.IsHost)
        {
            return;
        }

        FastBufferWriter writer = new FastBufferWriter(
            FearModeNetworkSerializer.CapabilityMessageSize,
            Allocator.Temp);
        try
        {
            FearModeNetworkSerializer.WriteCapability(ref writer);
            _messagingManager.SendNamedMessage(
                FearModeNetworkConstants.CapabilityMessageName,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.ReliableSequenced);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"Fear capability send failed: {ex.GetType().Name}.");
        }
        finally
        {
            writer.Dispose();
        }

        FastBufferWriter soundWriter = new FastBufferWriter(
            FearSoundNetworkSerializer.CapabilityMessageSize,
            Allocator.Temp);
        try
        {
            FearSoundNetworkSerializer.WriteCapability(ref soundWriter);
            _messagingManager.SendNamedMessage(
                FearModeNetworkConstants.SoundCapabilityMessageName,
                NetworkManager.ServerClientId,
                soundWriter,
                NetworkDelivery.ReliableSequenced);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"Fear sound capability send failed: {ex.GetType().Name}.");
        }
        finally
        {
            soundWriter.Dispose();
        }
    }

    private void BroadcastSession()
    {
        foreach (ulong clientId in _capablePeers)
        {
            SendSession(clientId, _sessionEnabled);
        }
    }

    private void SendSession(ulong clientId, bool enabled)
    {
        if (_messagingManager == null)
        {
            return;
        }

        FastBufferWriter writer = new FastBufferWriter(
            FearModeNetworkSerializer.SessionMessageSize,
            Allocator.Temp);
        try
        {
            FearModeNetworkSerializer.WriteSession(ref writer, enabled);
            _messagingManager.SendNamedMessage(
                FearModeNetworkConstants.SessionMessageName,
                clientId,
                writer,
                NetworkDelivery.ReliableSequenced);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"Fear session send failed for {clientId}: {ex.GetType().Name}.");
        }
        finally
        {
            writer.Dispose();
        }
    }

    private void BroadcastSelection(FearModeSelectionState state)
    {
        foreach (ulong clientId in _capablePeers)
        {
            SendSelection(clientId, state, out _);
        }
    }

    private bool SendSelection(ulong clientId, FearModeSelectionState state, out string reason)
    {
        reason = string.Empty;
        if (_messagingManager == null)
        {
            reason = "messaging manager unavailable";
            return false;
        }

        FastBufferWriter writer = new FastBufferWriter(
            FearModeNetworkSerializer.SelectionMessageSize,
            Allocator.Temp);
        try
        {
            FearModeNetworkSerializer.WriteSelection(ref writer, state);
            _messagingManager.SendNamedMessage(
                FearModeNetworkConstants.SelectionMessageName,
                clientId,
                writer,
                NetworkDelivery.ReliableSequenced);
            return true;
        }
        catch (Exception ex)
        {
            reason = $"fear selection send failed: {ex.GetType().Name}";
            return false;
        }
        finally
        {
            writer.Dispose();
        }
    }

    private void BroadcastSoundEvent(FearSoundEventState state)
    {
        foreach (ulong clientId in _soundCapablePeers)
        {
            SendSoundPacket(FearModeNetworkConstants.SoundEventMessageName, clientId, state, out _);
        }
    }

    private void EnqueueSoundEvent(FearSoundEventState state)
    {
        _pendingSoundEvents.Enqueue(state);
    }

    private bool SendSoundPacket(
        string messageName,
        ulong clientId,
        FearSoundEventState state,
        out string reason)
    {
        reason = string.Empty;
        if (_messagingManager == null)
        {
            reason = "messaging manager unavailable";
            return false;
        }

        FastBufferWriter writer = new FastBufferWriter(
            FearSoundNetworkSerializer.EventMessageSize,
            Allocator.Temp);
        try
        {
            FearSoundNetworkSerializer.WriteEvent(ref writer, state);
            _messagingManager.SendNamedMessage(
                messageName,
                clientId,
                writer,
                NetworkDelivery.ReliableSequenced);
            return true;
        }
        catch (Exception ex)
        {
            reason = $"fear sound send failed: {ex.GetType().Name}";
            return false;
        }
        finally
        {
            writer.Dispose();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _capablePeers.Remove(clientId);
        _soundCapablePeers.Remove(clientId);
        _lastAcceptedSoundTicks.Remove(clientId);
        if (_networkManager == null)
        {
            return;
        }

        if (!_networkManager.IsHost)
        {
            _selections.Remove(clientId);
            return;
        }

        if (_selections.TryGet(clientId, out FearModeSelectionState previous))
        {
            FearModeSelectionState tombstone = new FearModeSelectionState(
                previous.ClientId,
                previous.PlayerSlotId,
                FearModeRules.DefaultModelKey,
                previous.Revision + 1);
            _selections.Update(tombstone);
            BroadcastSelection(tombstone);
            _selections.Remove(clientId);
        }
    }
}
