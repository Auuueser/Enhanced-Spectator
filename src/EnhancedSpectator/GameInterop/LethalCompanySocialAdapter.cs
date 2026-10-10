using System.Collections.Generic;
using GameNetcodeStuff;
using Steamworks.Data;

namespace EnhancedSpectator.GameInterop;

/// <summary>Connected-player facts for social features, keyed by Netcode client id.</summary>
internal sealed class LethalCompanySocialAdapter
{
    internal bool TryGetPlayer(ulong clientId, out PlayerControllerB player)
    {
        player = null!;
        var round = StartOfRound.Instance;
        if (round == null || round.ClientPlayerList == null || !round.ClientPlayerList.TryGetValue(clientId, out int slot)
            || slot < 0 || slot >= round.allPlayerScripts.Length) return false;
        player = round.allPlayerScripts[slot];
        return player != null && player.actualClientId == clientId && !player.disconnectedMidGame;
    }

    internal bool IsDead(ulong clientId) => TryGetPlayer(clientId, out var player) && player.isPlayerDead;
    /// <summary>The local player is dead (spectating).</summary>
    internal bool LocalDead => StartOfRound.Instance != null && StartOfRound.Instance.localPlayerController is { isPlayerDead: true };
    /// <summary>Someone in the game is dead (the spectator chat group has listeners).</summary>
    internal bool AnyoneDead => StartOfRound.Instance is { } round && round.livingPlayers < round.connectedPlayersAmount + 1;
    /// <summary>The current game (lobby session): a new StartOfRound comes with each game, 0 outside one.</summary>
    internal int Session => StartOfRound.Instance != null ? StartOfRound.Instance.GetInstanceID() : 0;
    internal ulong SlotOf(ulong clientId) => TryGetPlayer(clientId, out var player) ? player.playerClientId : ulong.MaxValue;
    internal string NameOf(ulong clientId) => TryGetPlayer(clientId, out var player) ? PlayerDisplayNames.Of(player) : "?";

    /// <summary>Connected players in slot order: client id, name and whether they are alive and in the round.</summary>
    internal void CopyPlayersTo(List<(ulong ClientId, string Name, bool Alive)> output)
    {
        output.Clear();
        var round = StartOfRound.Instance;
        if (round == null || round.ClientPlayerList == null) return;
        foreach (var entry in round.ClientPlayerList)
            if (TryGetPlayer(entry.Key, out var player))
                output.Add((entry.Key, PlayerDisplayNames.Of(player), player.isPlayerControlled && !player.isPlayerDead));
        output.Sort((a, b) => SlotOf(a.ClientId).CompareTo(SlotOf(b.ClientId)));
    }

    /// <summary>This game has a Steam lobby (not a LAN game).</summary>
    internal bool LobbyAvailable => Lobby.HasValue;

    /// <summary>Sets one of the local player's own lobby member values, seen by every member.</summary>
    internal void PublishLobby(string key, string value) { if (Lobby is { } lobby) lobby.SetMemberData(key, value); }

    /// <summary>Every lobby member's value for <paramref name="key"/>, with their Steam id and the client id of their player.</summary>
    internal void CopyLobbyData(string key, List<(ulong SteamId, ulong ClientId, string Value)> output)
    {
        output.Clear();
        if (Lobby is not { } lobby) return;
        foreach (var member in lobby.Members)
        {
            ulong steamId = member.Id;
            if (TryGetClientOfSteamId(steamId, out ulong clientId)) output.Add((steamId, clientId, lobby.GetMemberData(member, key)));
        }
    }

    private static Lobby? Lobby => GameNetworkManager.Instance != null ? GameNetworkManager.Instance.currentLobby : null;

    private bool TryGetClientOfSteamId(ulong steamId, out ulong clientId)
    {
        clientId = 0;
        var round = StartOfRound.Instance;
        if (round == null || round.ClientPlayerList == null) return false;
        foreach (var entry in round.ClientPlayerList)
            if (TryGetPlayer(entry.Key, out var player) && player.playerSteamId == steamId) { clientId = entry.Key; return true; }
        return false;
    }

    /// <summary>A round is being played: the ship has landed and is not leaving.</summary>
    internal bool RoundInProgress => StartOfRound.Instance is { } round && !round.inShipPhase && !round.shipIsLeaving;
    internal bool ShipLeaving => StartOfRound.Instance is { } round && round.shipIsLeaving;
    /// <summary>Back in orbit: the round is over.</summary>
    internal bool InShipPhase => StartOfRound.Instance is { } round && round.inShipPhase;
}
