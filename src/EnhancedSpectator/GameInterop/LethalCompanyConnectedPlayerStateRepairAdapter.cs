using System;
using System.Collections.Generic;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Networking;
using GameNetcodeStuff;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Repairs confirmed Lethal Company connected-player flags using vanilla ClientPlayerList as the source of truth.
/// </summary>
public sealed class LethalCompanyConnectedPlayerStateRepairAdapter : IConnectedPlayerStateRepairAdapter
{
    // LC Chinese Project manages real names when installed; synced names then only fill the gaps it leaves.
    private readonly bool _chineseProjectNames;

    /// <summary>Creates the adapter; <paramref name="chineseProjectNames"/> is whether LC Chinese Project is installed.</summary>
    public LethalCompanyConnectedPlayerStateRepairAdapter(bool chineseProjectNames) => _chineseProjectNames = chineseProjectNames;

    /// <inheritdoc />
    public int RepairConnectedPlayerState(
        IReadOnlyList<PeerIdentityState> peerIdentities,
        IReadOnlyList<SpectatorTargetState> remoteSpectatorTargets,
        bool updatePlayerNames,
        bool updateQuickMenu,
        bool debug,
        out string summary)
    {
        summary = string.Empty;
        StartOfRound? round = StartOfRound.Instance;
        if (round == null || round.ClientPlayerList == null || round.allPlayerScripts == null)
        {
            summary = "StartOfRound player state unavailable";
            return 0;
        }

        NetworkManager? networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            summary = "NetworkManager unavailable";
            return 0;
        }

        ulong localClientId = networkManager.LocalClientId;
        QuickMenuManager? quickMenuManager = UnityEngine.Object.FindObjectOfType<QuickMenuManager>();
        bool[] repairedSlots = new bool[round.allPlayerScripts.Length];
        int repairs = 0;
        if (peerIdentities != null)
        {
            for (int index = 0; index < peerIdentities.Count; index++)
            {
                PeerIdentityState identity = peerIdentities[index];
                if (identity.ClientId == localClientId)
                {
                    continue;
                }

                if (!IsClientCurrentlyConnected(networkManager, identity.ClientId))
                {
                    if (debug)
                    {
                        ModLog.Debug($"Skipping connected-player repair for client={identity.ClientId}; client is not currently connected.");
                    }

                    continue;
                }

                if (!round.ClientPlayerList.TryGetValue(identity.ClientId, out int slot))
                {
                    continue;
                }

                if (slot < 0 || slot >= round.allPlayerScripts.Length)
                {
                    continue;
                }

                if (identity.PlayerSlotId != (ulong)slot)
                {
                    if (debug)
                    {
                        ModLog.Debug(
                            $"Skipping connected-player repair for client={identity.ClientId}; identity slot={identity.PlayerSlotId}, vanilla slot={slot}.");
                    }

                    continue;
                }

                PlayerControllerB player = round.allPlayerScripts[slot];
                if (player == null)
                {
                    continue;
                }

                repairedSlots[slot] = true;
                if (RepairOneConnectedPlayerSlot(
                    round,
                    networkManager,
                    quickMenuManager,
                    player,
                    slot,
                    identity.ClientId,
                    identity,
                    remoteSpectatorTargets,
                    updatePlayerNames,
                    updateQuickMenu,
                    debug))
                {
                    repairs++;
                }
            }
        }

        foreach (KeyValuePair<ulong, int> clientPlayer in round.ClientPlayerList)
        {
            ulong clientId = clientPlayer.Key;
            int slot = clientPlayer.Value;
            if (clientId == localClientId
                || slot < 0
                || slot >= round.allPlayerScripts.Length
                || repairedSlots[slot]
                || !IsClientCurrentlyConnected(networkManager, clientId))
            {
                continue;
            }

            PlayerControllerB player = round.allPlayerScripts[slot];
            if (player == null)
            {
                continue;
            }

            if (RepairOneConnectedPlayerSlot(
                round,
                networkManager,
                quickMenuManager,
                player,
                slot,
                clientId,
                identity: null,
                remoteSpectatorTargets,
                updatePlayerNames,
                updateQuickMenu,
                debug))
            {
                repairs++;
            }
        }

        if (repairs > 0)
        {
            summary = $"repaired {repairs} connected player slot(s)";
        }

        return repairs;
    }

    private bool RepairOneConnectedPlayerSlot(
        StartOfRound round,
        NetworkManager networkManager,
        QuickMenuManager? quickMenuManager,
        PlayerControllerB player,
        int slot,
        ulong clientId,
        PeerIdentityState? identity,
        IReadOnlyList<SpectatorTargetState>? remoteSpectatorTargets,
        bool updatePlayerNames,
        bool updateQuickMenu,
        bool debug)
    {
        bool hasRemoteSpectatorTargetState = TryGetRemoteSpectatorTarget(
            remoteSpectatorTargets,
            clientId,
            out SpectatorTargetState? remoteSpectatorTargetState);
        bool remotePeerIsSpectating = hasRemoteSpectatorTargetState
            && remoteSpectatorTargetState != null
            && remoteSpectatorTargetState.IsSpectating;
        bool repaired = false;
        if (remotePeerIsSpectating)
        {
            // A peer that reports an active spectator target may have died before this client joined.
            // Vanilla join sync does not reliably replay that death state to late joiners.
            if (!player.isPlayerDead)
            {
                player.isPlayerDead = true;
                repaired = true;
            }

            if (player.isPlayerControlled)
            {
                player.isPlayerControlled = false;
                repaired = true;
            }
        }
        else if (hasRemoteSpectatorTargetState && player.isPlayerDead)
        {
            // A known non-spectating target state means the remote peer has left spectator mode.
            // This clears mod-repaired death flags after revive without guessing from stale absence.
            player.isPlayerDead = false;
            if (!player.isPlayerControlled)
            {
                player.isPlayerControlled = true;
            }

            repaired = true;
        }
        else if (ConnectedPlayerStateRepairRules.ShouldRestoreConnectedAliveControl(
                     isLocalPlayer: clientId == networkManager.LocalClientId,
                     isCurrentlyConnected: true,
                     isPlayerControlled: player.isPlayerControlled,
                     isPlayerDead: player.isPlayerDead))
        {
            player.isPlayerControlled = true;
            repaired = true;
        }

        if (ConnectedPlayerStateRepairRules.ShouldClearDisconnectedMidGame(
                isLocalPlayer: clientId == networkManager.LocalClientId,
                isCurrentlyConnected: true,
                player.disconnectedMidGame))
        {
            player.disconnectedMidGame = false;
            repaired = true;
        }

        if (!remotePeerIsSpectating && RepairAliveConnectedPlayerVisualState(round, slot, player))
        {
            repaired = true;
        }

        if (player.actualClientId != clientId)
        {
            player.actualClientId = clientId;
            repaired = true;
        }

        if (player.playerClientId != (ulong)slot)
        {
            player.playerClientId = (ulong)slot;
            repaired = true;
        }

        string displayName = string.Empty;
        bool hasModIdentityDisplayName = identity != null
            && PlayerDisplayNameRules.TryNormalize(identity.DisplayName, out displayName);
        if (updatePlayerNames && hasModIdentityDisplayName)
        {
            if (ConnectedPlayerStateRepairRules.ShouldWriteIdentityDisplayName(player.playerUsername, displayName, _chineseProjectNames))
            {
                player.playerUsername = displayName;
                repaired = true;
            }
        }
        else if (ConnectedPlayerStateRepairRules.ShouldUseVanillaFallbackDisplayName(
                     updatePlayerNames,
                     hasModIdentityDisplayName,
                     player.playerUsername)
                 && TryResolveVanillaFallbackDisplayName(slot, player, out string fallbackDisplayName, out ulong fallbackSteamId))
        {
            if (!StringComparer.Ordinal.Equals(player.playerUsername, fallbackDisplayName))
            {
                player.playerUsername = fallbackDisplayName;
                repaired = true;
            }

            displayName = fallbackDisplayName;
            if (fallbackSteamId != 0UL && player.playerSteamId != fallbackSteamId)
            {
                player.playerSteamId = fallbackSteamId;
                repaired = true;
            }
        }

        if (updateQuickMenu && quickMenuManager != null)
        {
            string quickMenuName = !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : player.playerUsername;
            quickMenuManager.AddUserToPlayerList(player.playerSteamId, quickMenuName, slot);
        }

        if (repaired && debug)
        {
            string identitySource = identity != null ? "mod-identity" : "vanilla-client-list";
            ModLog.Debug(
                $"Repaired vanilla connected player slot: source={identitySource}, client={clientId}, slot={slot}, name={player.playerUsername}, controlled={player.isPlayerControlled}, dead={player.isPlayerDead}, disconnected={player.disconnectedMidGame}, setPositionOfDeadPlayer={player.setPositionOfDeadPlayer}, modelHidden={IsPlayerModelHidden(player)}, targetStateKnown={hasRemoteSpectatorTargetState}, spectator={remotePeerIsSpectating}.");
        }

        return repaired;
    }

    private static bool TryGetRemoteSpectatorTarget(
        IReadOnlyList<SpectatorTargetState>? remoteSpectatorTargets,
        ulong clientId,
        out SpectatorTargetState? spectatorTargetState)
    {
        spectatorTargetState = default;
        if (remoteSpectatorTargets == null)
        {
            return false;
        }

        for (int index = 0; index < remoteSpectatorTargets.Count; index++)
        {
            SpectatorTargetState state = remoteSpectatorTargets[index];
            if (state.LocalClientId == clientId)
            {
                spectatorTargetState = state;
                return true;
            }
        }

        return false;
    }

    private static bool RepairAliveConnectedPlayerVisualState(StartOfRound round, int slot, PlayerControllerB player)
    {
        bool repaired = false;
        if (player.setPositionOfDeadPlayer)
        {
            player.setPositionOfDeadPlayer = false;
            repaired = true;
        }

        if (IsPlayerModelHidden(player)
            && round.allPlayerObjects != null
            && slot >= 0
            && slot < round.allPlayerObjects.Length
            && round.allPlayerObjects[slot] != null)
        {
            // Death and disconnect paths can disable the live SkinnedMeshRenderers, while
            // vanilla reconnect only marks the slot controlled again. Re-enable the same
            // live model renderers that vanilla ReviveDeadPlayers enables for alive players.
            player.DisablePlayerModel(round.allPlayerObjects[slot], enable: true, disableLocalArms: true);
            repaired = true;
        }

        if (repaired && player.playerBodyAnimator != null)
        {
            player.playerBodyAnimator.SetBool("Limp", false);
        }

        return repaired;
    }

    private static bool IsPlayerModelHidden(PlayerControllerB player)
    {
        return (player.thisPlayerModel != null && !player.thisPlayerModel.enabled)
            || (player.thisPlayerModelLOD1 != null && !player.thisPlayerModelLOD1.enabled)
            || (player.thisPlayerModelLOD2 != null && !player.thisPlayerModelLOD2.enabled);
    }

    private static bool IsClientCurrentlyConnected(NetworkManager networkManager, ulong clientId)
    {
        if (clientId == networkManager.LocalClientId)
        {
            return true;
        }

        IReadOnlyList<ulong> connectedClientIds = networkManager.ConnectedClientsIds;
        for (int index = 0; index < connectedClientIds.Count; index++)
        {
            if (connectedClientIds[index] == clientId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveVanillaFallbackDisplayName(
        int slot,
        PlayerControllerB player,
        out string displayName,
        out ulong steamId)
    {
        displayName = string.Empty;
        steamId = player.playerSteamId;
        if (steamId == 0UL)
        {
            steamId = TryGetLobbySteamIdForSlot(slot, out ulong lobbySteamId) ? lobbySteamId : 0UL;
        }

        if (steamId == 0UL)
        {
            return false;
        }

        string candidate = NormalizeSteamFriendName(new Friend(steamId).Name);
        return PlayerDisplayNameRules.TryNormalize(candidate, out displayName);
    }

    private static bool TryGetLobbySteamIdForSlot(int slot, out ulong steamId)
    {
        steamId = 0UL;
        GameNetworkManager? gameNetworkManager = GameNetworkManager.Instance;
        if (gameNetworkManager == null
            || gameNetworkManager.disableSteam
            || slot < 0)
        {
            return false;
        }

        if (gameNetworkManager.steamIdsInLobby != null
            && slot < gameNetworkManager.steamIdsInLobby.Count)
        {
            steamId = gameNetworkManager.steamIdsInLobby[slot];
            if (steamId != 0UL)
            {
                return true;
            }
        }

        if (!gameNetworkManager.currentLobby.HasValue)
        {
            return false;
        }

        int index = 0;
        foreach (Friend member in gameNetworkManager.currentLobby.Value.Members)
        {
            if (index == slot)
            {
                steamId = member.Id;
                return steamId != 0UL;
            }

            index++;
        }

        return false;
    }

    private static string NormalizeSteamFriendName(string friendName)
    {
        if (string.IsNullOrWhiteSpace(friendName))
        {
            return "Nameless";
        }

        char[] buffer = new char[friendName.Length];
        int writeIndex = 0;
        for (int index = 0; index < friendName.Length; index++)
        {
            char current = friendName[index];
            if (char.IsLetter(current) || char.IsDigit(current) || current == '_' || current == '.')
            {
                buffer[writeIndex] = current;
                writeIndex++;
            }
        }

        if (writeIndex == 0)
        {
            return "Nameless";
        }

        string normalized = new string(buffer, 0, writeIndex);
        return normalized.Length <= 2 ? normalized + "0" : normalized;
    }
}
