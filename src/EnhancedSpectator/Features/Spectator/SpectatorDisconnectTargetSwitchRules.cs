namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Pure decision rules for leaving an invalid spectator target after a watched player disconnects.
/// </summary>
public static class SpectatorDisconnectTargetSwitchRules
{
    /// <summary>
    /// Gets whether vanilla spectator target switching should be invoked after disconnect cleanup.
    /// </summary>
    public static bool ShouldAutoSwitch(
        bool isLocalPlayerDead,
        bool hasSpectateCamera,
        bool gameOverOverrideActive,
        bool hasCurrentTarget,
        bool currentTargetIsValid,
        bool currentTargetDisconnected,
        bool hasReplacementTarget)
    {
        return isLocalPlayerDead
            && hasSpectateCamera
            && !gameOverOverrideActive
            && hasCurrentTarget
            && !currentTargetIsValid
            && currentTargetDisconnected
            && hasReplacementTarget;
    }

    /// <summary>
    /// Gets whether an invalid current target looks like a disconnected player rather than a normal death.
    /// </summary>
    public static bool IsDisconnectLikeInvalidTarget(
        bool disconnectedMidGame,
        bool isPlayerControlled,
        bool isPlayerDead,
        bool removedFromClientMap)
    {
        return disconnectedMidGame
            || removedFromClientMap
            || (!isPlayerControlled && !isPlayerDead);
    }
}
