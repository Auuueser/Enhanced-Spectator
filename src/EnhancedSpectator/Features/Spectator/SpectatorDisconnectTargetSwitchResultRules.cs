namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Pure rules for reporting whether a vanilla spectator target switch recovered from a disconnect.
/// </summary>
public static class SpectatorDisconnectTargetSwitchResultRules
{
    /// <summary>
    /// Gets whether a vanilla switch call produced a valid replacement target.
    /// </summary>
    public static bool DidSwitchToValidTarget(
        bool targetSwitchInvoked,
        bool targetChanged,
        bool newTargetIsValid)
    {
        return targetSwitchInvoked && targetChanged && newTargetIsValid;
    }
}
