namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Snapshot of local spectator target state after a player disconnect.
/// </summary>
public readonly struct SpectatorDisconnectTargetSwitchContext
{
    /// <summary>
    /// Creates a disconnect target-switch context.
    /// </summary>
    public SpectatorDisconnectTargetSwitchContext(
        bool isLocalPlayerDead,
        bool hasSpectateCamera,
        bool gameOverOverrideActive,
        bool hasCurrentTarget,
        bool currentTargetIsValid,
        bool currentTargetDisconnected,
        bool hasReplacementTarget)
    {
        IsLocalPlayerDead = isLocalPlayerDead;
        HasSpectateCamera = hasSpectateCamera;
        GameOverOverrideActive = gameOverOverrideActive;
        HasCurrentTarget = hasCurrentTarget;
        CurrentTargetIsValid = currentTargetIsValid;
        CurrentTargetDisconnected = currentTargetDisconnected;
        HasReplacementTarget = hasReplacementTarget;
    }

    /// <summary>
    /// Gets whether the local player is dead.
    /// </summary>
    public bool IsLocalPlayerDead { get; }

    /// <summary>
    /// Gets whether the vanilla spectator camera exists.
    /// </summary>
    public bool HasSpectateCamera { get; }

    /// <summary>
    /// Gets whether vanilla game-over spectate override is active.
    /// </summary>
    public bool GameOverOverrideActive { get; }

    /// <summary>
    /// Gets whether the local dead spectator still has a current target reference.
    /// </summary>
    public bool HasCurrentTarget { get; }

    /// <summary>
    /// Gets whether the current target is still valid for vanilla spectating.
    /// </summary>
    public bool CurrentTargetIsValid { get; }

    /// <summary>
    /// Gets whether the current target has been marked disconnected or removed from the vanilla client map.
    /// </summary>
    public bool CurrentTargetDisconnected { get; }

    /// <summary>
    /// Gets whether at least one other valid spectator target exists.
    /// </summary>
    public bool HasReplacementTarget { get; }
}
