namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Pure rules for deciding whether local vanilla spectator target switching input should be suppressed.
/// </summary>
public static class SpectatorVanillaInputGuardRules
{
    /// <summary>
    /// Gets whether vanilla target switching should be suppressed for the current input state.
    /// </summary>
    public static bool ShouldSuppressTargetSwitchInput(
        bool internalTargetSwitchInProgress,
        bool freecamWantsVerticalInput,
        bool ascendKeyHeld,
        bool descendKeyHeld,
        out string reason)
    {
        if (internalTargetSwitchInProgress)
        {
            reason = string.Empty;
            return false;
        }

        if (freecamWantsVerticalInput && (ascendKeyHeld || descendKeyHeld))
        {
            reason = "freecam vertical movement is held";
            return true;
        }

        reason = string.Empty;
        return false;
    }
}
