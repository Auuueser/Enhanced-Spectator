using EnhancedSpectator.Features.Spectator;
using GameNetcodeStuff;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Invokes confirmed vanilla spectator target switching through direct game member access.
/// </summary>
public sealed class LethalCompanySpectatorTargetSwitchAdapter : IGameSpectatorTargetSwitchAdapter
{
    /// <inheritdoc />
    public bool TryGetDisconnectTargetSwitchContext(out SpectatorDisconnectTargetSwitchContext context)
    {
        StartOfRound round = StartOfRound.Instance;
        if (round == null)
        {
            context = default;
            return false;
        }

        PlayerControllerB localPlayer = round.localPlayerController;
        if (localPlayer == null)
        {
            context = default;
            return false;
        }

        PlayerControllerB currentTarget = localPlayer.spectatedPlayerScript;
        bool hasCurrentTarget = currentTarget != null;
        bool currentTargetIsValid = hasCurrentTarget && LethalCompanySpectatorTargetRules.IsValidSpectateTarget(round, currentTarget);
        bool currentTargetDisconnected = hasCurrentTarget && LethalCompanySpectatorTargetRules.IsDisconnectedTarget(round, currentTarget);
        context = new SpectatorDisconnectTargetSwitchContext(
            localPlayer.isPlayerDead,
            round.spectateCamera != null,
            round.overrideSpectateCamera,
            hasCurrentTarget,
            currentTargetIsValid,
            currentTargetDisconnected,
            HasReplacementTarget(round, localPlayer, currentTarget));
        return true;
    }

    /// <inheritdoc />
    public bool TrySwitchToNextSpectatorTarget()
    {
        StartOfRound round = StartOfRound.Instance;
        if (round == null)
        {
            return false;
        }

        PlayerControllerB localPlayer = round.localPlayerController;
        if (localPlayer == null || !localPlayer.isPlayerDead)
        {
            return false;
        }

        PlayerControllerB previousTarget = localPlayer.spectatedPlayerScript;
        SpectatorVanillaInputGuard.BeginInternalTargetSwitch();
        try
        {
            localPlayer.SpectateNextPlayer(false);
        }
        finally
        {
            SpectatorVanillaInputGuard.EndInternalTargetSwitch();
        }

        PlayerControllerB nextTarget = localPlayer.spectatedPlayerScript;
        return SpectatorDisconnectTargetSwitchResultRules.DidSwitchToValidTarget(
            targetSwitchInvoked: true,
            targetChanged: nextTarget != null && !ReferenceEquals(nextTarget, previousTarget),
            newTargetIsValid: LethalCompanySpectatorTargetRules.IsValidSpectateTarget(round, nextTarget));
    }

    private static bool HasReplacementTarget(
        StartOfRound round,
        PlayerControllerB localPlayer,
        PlayerControllerB? currentTarget)
    {
        if (round.allPlayerScripts == null)
        {
            return false;
        }

        for (int index = 0; index < round.allPlayerScripts.Length; index++)
        {
            PlayerControllerB player = round.allPlayerScripts[index];
            if (player == null || player == localPlayer || player == currentTarget)
            {
                continue;
            }

            if (LethalCompanySpectatorTargetRules.IsValidSpectateTarget(round, player))
            {
                return true;
            }
        }

        return false;
    }
}
