using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Runtime;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace EnhancedSpectator.Features.Social;

/// <summary>
/// Host-authoritative social messages (emotes, betting) on one isolated named message. Clients greet the host and
/// send requests only to it; the host validates them (sender dead, cooldowns, stakes) and sends the results to the
/// peers that greeted it, so players without this version never receive these packets. A client whose host does not
/// answer (no mod, or another version) runs none of it, except emotes: those then travel through the Steam lobby
/// (each player's own lobby member data), so players with the mod still see each other's in the audience row; every
/// receiver checks death and cooldown itself. The host also tells its peers which features it runs. The spectator
/// chat group travels the same way: a line goes to the host, which checks its length and pace and passes it on.
/// </summary>
internal sealed class SocialNetworkService : IDisposable
{
    internal const string MessageName = "EnhancedSpectator.Social.V1";
    // 2: bets carry their late flag in the book snapshot. 3: markets carry several answers (players dying together).
    // 4: the host says whether it runs audience bets (in its welcome, and again whenever that changes).
    // 5: the spectator chat group.
    internal const int Version = 5;
    private const int HelloRetryFrames = 300, PacketCapacity = 16384;
    /// <summary>The longest chat line the group carries, and the shortest gap between one player's lines.</summary>
    internal const int ChatMaxLength = 200;
    internal const float ChatMinSeconds = .3f;
    internal enum Kind : byte { Hello, Welcome, EmoteRequest, Emote, BetRequest, BetState, RateRequest, RateSummary, Settings, ChatRequest, Chat }

    private readonly EnhancedSpectatorConfig _config;
    private readonly LethalCompanySocialAdapter _game = new LethalCompanySocialAdapter();
    private readonly HashSet<ulong> _peers = new HashSet<ulong>();
    private readonly Dictionary<ulong, float> _lastEmote = new Dictionary<ulong, float>(), _lastChat = new Dictionary<ulong, float>();
    private readonly LobbyEmoteTracker _lobby = new LobbyEmoteTracker();
    private readonly List<(ulong SteamId, ulong ClientId, string Value)> _lobbyData = new List<(ulong, ulong, string)>(32);
    private float _nextLobbyPoll;
    private NetworkManager? _manager;
    private CustomMessagingManager? _messaging;
    private bool _registered, _welcomed, _hostBets, _sentBets;
    private int _nextHello;
    private float _localCooldownUntil;

    /// <summary>An emote to show: sender client id and emote index (also raised for the local player's own).</summary>
    internal event Action<ulong, int>? EmoteReceived;
    /// <summary>Raw betting packets for the betting service (sender, reader positioned after the kind).</summary>
    internal event Action<ulong, Kind, FastBufferReader>? BettingPacket;
    /// <summary>A spectator chat line: sender client id and text (also raised for the local player's own).</summary>
    internal event Action<ulong, string>? ChatReceived;

    internal SocialNetworkService(EnhancedSpectatorConfig config) { _config = config; }

    internal bool IsHost => _manager != null && _manager.IsHost;
    internal bool Ready => _registered && (IsHost || _welcomed);
    /// <summary>Emotes work: through the host when it runs this mod, otherwise through the Steam lobby.</summary>
    internal bool CanEmote => Ready || _registered && _game.LobbyAvailable;
    /// <summary>Audience bets run: the host's own option, which its clients receive.</summary>
    internal bool BetsEnabled => Ready && (IsHost ? _config.Camera.EnableAudienceBets.Value : _hostBets);
    internal ulong LocalClientId => _manager != null ? _manager.LocalClientId : 0;
    internal IReadOnlyCollection<ulong> Peers => _peers;
    internal float CooldownRemaining => Mathf.Max(0, _localCooldownUntil - Time.realtimeSinceStartup);
    internal LethalCompanySocialAdapter Game => _game;
    /// <summary>A new connection (a game hosted or joined): state from an earlier one no longer applies.</summary>
    internal event Action? Connected;

    internal void Tick()
    {
        if (!_config.EnableNetworking.Value) { Unregister(); return; }
        // A scene change does not interrupt the channel: the same connection keeps its registration (and the book
        // its points) and goes on handling and sending messages, so no update is lost. The short unsafe window only
        // holds back registering, greeting and lobby polling. A connection that is gone or replaced lets go, and a
        // new one announces itself (Connected).
        if (!RuntimeConnectionState.CanUseModNetworking(out _)) { if (!SameConnection) Unregister(); return; }
        if (!EnsureRegistered()) return;
        if (IsHost && _sentBets != _config.Camera.EnableAudienceBets.Value)
        {
            _sentBets = _config.Camera.EnableAudienceBets.Value;
            foreach (ulong peer in _peers) Send(peer, Kind.Settings, WriteSettings);
        }
        if (!IsHost && !_welcomed && Time.frameCount >= _nextHello)
        { _nextHello = Time.frameCount + HelloRetryFrames; Send(NetworkManager.ServerClientId, Kind.Hello, null); }
        PollLobby();
    }

    // Lobby emotes from players whose host could not relay them (ten looks a second).
    private void PollLobby()
    {
        if (Time.realtimeSinceStartup < _nextLobbyPoll) return;
        _nextLobbyPoll = Time.realtimeSinceStartup + .1f;
        _game.CopyLobbyData(LobbyEmoteTracker.Key, _lobbyData);
        foreach (var entry in _lobbyData)
            if (_lobby.Read(entry.SteamId, entry.Value, out int emote) && entry.ClientId != LocalClientId) AcceptLobbyEmote(entry.ClientId, emote);
    }

    private void AcceptLobbyEmote(ulong origin, int emote)
    {
        float now = Time.realtimeSinceStartup;
        if (!SpectatorEmotes.Valid(emote) || !_game.IsDead(origin)
            || _lastEmote.TryGetValue(origin, out float last) && now - last < SpectatorEmotes.CooldownSeconds - .1f) return;
        _lastEmote[origin] = now;
        EmoteReceived?.Invoke(origin, emote);
    }

    /// <summary>Sends one emote while dead; shows it locally at once. False while cooling down or unavailable.</summary>
    internal bool SendEmote(int emote)
    {
        if (!CanEmote || !SpectatorEmotes.Valid(emote) || CooldownRemaining > 0 || !_game.IsDead(LocalClientId)) return false;
        _localCooldownUntil = Time.realtimeSinceStartup + SpectatorEmotes.CooldownSeconds;
        EmoteReceived?.Invoke(LocalClientId, emote);
        if (!Ready) _game.PublishLobby(LobbyEmoteTracker.Key, _lobby.Next(emote));
        else if (IsHost) AcceptEmote(LocalClientId, emote);
        else Send(NetworkManager.ServerClientId, Kind.EmoteRequest, w => w.WriteValueSafe(emote));
        return true;
    }

    /// <summary>Sends a betting packet: to the host as a client, or to every greeted peer as the host.</summary>
    internal void SendBetting(Kind kind, Action<FastBufferWriter> write, ulong? only = null)
    {
        if (!Ready) return;
        if (!IsHost) { Send(NetworkManager.ServerClientId, kind, write); return; }
        if (only.HasValue) { if (only.Value != LocalClientId) Send(only.Value, kind, write); return; }
        foreach (ulong peer in _peers) Send(peer, kind, write);
    }

    private void AcceptEmote(ulong origin, int emote)
    {
        // The host repeats the cooldown and death checks, so a modified client cannot spam or emote while alive.
        float now = Time.realtimeSinceStartup;
        if (!SpectatorEmotes.Valid(emote) || !_game.IsDead(origin)
            || _lastEmote.TryGetValue(origin, out float last) && now - last < SpectatorEmotes.CooldownSeconds - .1f) return;
        _lastEmote[origin] = now;
        if (origin != LocalClientId) EmoteReceived?.Invoke(origin, emote);
        foreach (ulong peer in _peers)
            if (peer != origin) Send(peer, Kind.Emote, w => { w.WriteValueSafe(origin); w.WriteValueSafe(emote); });
    }

    /// <summary>Sends one line to the spectator chat group and shows it locally at once; false when the host cannot carry it.</summary>
    internal bool SendChat(string text)
    {
        text = ChatLine(text);
        if (!Ready || text.Length == 0) return false;
        if (IsHost) AcceptChat(LocalClientId, text);
        else { ChatReceived?.Invoke(LocalClientId, text); Send(NetworkManager.ServerClientId, Kind.ChatRequest, w => w.WriteValueSafe(text)); }
        return true;
    }

    /// <summary>A line as the group carries it: trimmed, and cut to <see cref="ChatMaxLength"/>.</summary>
    internal static string ChatLine(string text)
    {
        text = text.Trim();
        return text.Length > ChatMaxLength ? text.Substring(0, ChatMaxLength) : text;
    }

    private void AcceptChat(ulong origin, string text)
    {
        // The host keeps each player's pace and the length, so a modified client cannot flood the group.
        float now = Time.realtimeSinceStartup;
        text = ChatLine(text);
        if (text.Length == 0 || _lastChat.TryGetValue(origin, out float last) && now - last < ChatMinSeconds) return;
        _lastChat[origin] = now;
        ChatReceived?.Invoke(origin, text);
        foreach (ulong peer in _peers)
            if (peer != origin) Send(peer, Kind.Chat, w => { w.WriteValueSafe(origin); w.WriteValueSafe(text); });
    }

    private bool SameConnection => _registered && NetworkManager.Singleton is { } manager && ReferenceEquals(manager, _manager)
        && ReferenceEquals(manager.CustomMessagingManager, _messaging) && manager.IsListening && !manager.ShutdownInProgress;

    private void Handle(ulong sender, FastBufferReader reader)
    {
        // Handled even during a scene change: game lookups here are null-safe, and what reaches the visuals runs
        // inside this try.
        try
        {
            reader.ReadValueSafe(out int version);
            if (version != Version) return;
            reader.ReadValueSafe(out byte raw);
            var kind = (Kind)raw;
            switch (kind)
            {
                case Kind.Hello when IsHost:
                    _peers.Add(sender); Send(sender, Kind.Welcome, WriteSettings); BettingPacket?.Invoke(sender, kind, reader); break;
                case Kind.Welcome when !IsHost && sender == NetworkManager.ServerClientId:
                case Kind.Settings when !IsHost && sender == NetworkManager.ServerClientId:
                    reader.ReadValueSafe(out _hostBets); _welcomed = true; break;
                case Kind.EmoteRequest when IsHost && _peers.Contains(sender):
                    reader.ReadValueSafe(out int requested); AcceptEmote(sender, requested); break;
                case Kind.Emote when !IsHost && sender == NetworkManager.ServerClientId:
                    reader.ReadValueSafe(out ulong origin); reader.ReadValueSafe(out int emote);
                    if (SpectatorEmotes.Valid(emote) && origin != LocalClientId) EmoteReceived?.Invoke(origin, emote);
                    break;
                case Kind.BetRequest when IsHost && _peers.Contains(sender):
                case Kind.BetState when !IsHost && sender == NetworkManager.ServerClientId:
                case Kind.RateRequest when IsHost && _peers.Contains(sender):
                case Kind.RateSummary when !IsHost && sender == NetworkManager.ServerClientId:
                    BettingPacket?.Invoke(sender, kind, reader); break;
                case Kind.ChatRequest when IsHost && _peers.Contains(sender):
                    reader.ReadValueSafe(out string line); AcceptChat(sender, line); break;
                case Kind.Chat when !IsHost && sender == NetworkManager.ServerClientId:
                    reader.ReadValueSafe(out ulong speaker); reader.ReadValueSafe(out string said);
                    if (speaker != LocalClientId) ChatReceived?.Invoke(speaker, ChatLine(said));
                    break;
            }
        }
        catch (Exception ex) { ModLog.Debug($"Dropped social packet from {sender}: {ex.GetType().Name}."); }
    }

    private void WriteSettings(FastBufferWriter w) => w.WriteValueSafe(_config.Camera.EnableAudienceBets.Value);

    private void Send(ulong target, Kind kind, Action<FastBufferWriter>? write)
    {
        if (_messaging == null) return;
        var writer = new FastBufferWriter(64, Allocator.Temp, PacketCapacity);
        try
        {
            writer.WriteValueSafe(Version); writer.WriteValueSafe((byte)kind);
            write?.Invoke(writer);
            // The betting book can exceed one transport packet; it travels fragmented.
            _messaging.SendNamedMessage(MessageName, target, writer,
                kind == Kind.BetState || kind == Kind.RateSummary ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.ReliableSequenced);
        }
        catch (Exception ex) { ModLog.Debug($"Social packet send failed: {ex.GetType().Name}."); }
        finally { writer.Dispose(); }
    }

    private bool EnsureRegistered()
    {
        var manager = NetworkManager.Singleton;
        if (_registered && ReferenceEquals(manager, _manager) && manager != null && ReferenceEquals(manager.CustomMessagingManager, _messaging)) return true;
        Unregister();
        if (manager == null || (!manager.IsClient && !manager.IsHost) || manager.CustomMessagingManager == null) return false;
        _manager = manager; _messaging = manager.CustomMessagingManager;
        _messaging.RegisterNamedMessageHandler(MessageName, Handle);
        manager.OnClientDisconnectCallback += OnClientDisconnected;
        _registered = true; _welcomed = _hostBets = false; _nextHello = 0; _lobby.Clear(); _sentBets = _config.Camera.EnableAudienceBets.Value;
        Connected?.Invoke();
        return true;
    }

    private void Unregister()
    {
        if (!_registered) return;
        if (_manager != null) _manager.OnClientDisconnectCallback -= OnClientDisconnected;
        _messaging?.UnregisterNamedMessageHandler(MessageName);
        _registered = _welcomed = false; _manager = null; _messaging = null;
        _peers.Clear(); _lastEmote.Clear(); _lastChat.Clear();
    }

    private void OnClientDisconnected(ulong clientId) { _peers.Remove(clientId); _lastEmote.Remove(clientId); _lastChat.Remove(clientId); }

    public void Dispose() => Unregister();
}
