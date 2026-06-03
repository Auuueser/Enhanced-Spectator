using EnhancedSpectator.Features.Spectator;
using GameNetcodeStuff;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Shared confirmed Lethal Company spectator target predicates.
/// </summary>
internal static class LethalCompanySpectatorTargetRules
{
    /// <summary>
    /// Gets whether a player is currently a valid vanilla spectator target.
    /// </summary>
    public static bool IsValidSpectateTarget(StartOfRound round, PlayerControllerB? player)
    {
        if (player == null
            || !player.isPlayerControlled
            || player.isPlayerDead
            || player.disconnectedMidGame)
        {
            return false;
        }

        if (round == null || round.ClientPlayerList == null || round.allPlayerScripts == null)
        {
            return false;
        }

        if (!round.ClientPlayerList.TryGetValue(player.actualClientId, out int slot)
            || slot < 0
            || slot >= round.allPlayerScripts.Length
            || round.allPlayerScripts[slot] != player)
        {
            return false;
        }

        return player.playerClientId == (ulong)slot;
    }

    /// <summary>
    /// Gets whether the watched target has been marked disconnected or removed from the vanilla client map.
    /// </summary>
    public static bool IsDisconnectedTarget(StartOfRound round, PlayerControllerB? player)
    {
        if (player == null)
        {
            return false;
        }

        if (round == null || round.ClientPlayerList == null)
        {
            return SpectatorDisconnectTargetSwitchRules.IsDisconnectLikeInvalidTarget(
                player.disconnectedMidGame,
                player.isPlayerControlled,
                player.isPlayerDead,
                removedFromClientMap: false);
        }

        bool removedFromClientMap = !round.ClientPlayerList.TryGetValue(player.actualClientId, out int slot)
            || slot < 0
            || round.allPlayerScripts == null
            || slot >= round.allPlayerScripts.Length
            || round.allPlayerScripts[slot] != player;
        return SpectatorDisconnectTargetSwitchRules.IsDisconnectLikeInvalidTarget(
            player.disconnectedMidGame,
            player.isPlayerControlled,
            player.isPlayerDead,
            removedFromClientMap);
    }
}
