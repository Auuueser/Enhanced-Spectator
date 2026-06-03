namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Identifies the transform source used for local spectator camera anchoring.
/// </summary>
public enum SpectatorTargetAnchorSource
{
    /// <summary>
    /// No usable anchor is available.
    /// </summary>
    None = 0,

    /// <summary>
    /// Stable player root transform.
    /// </summary>
    PlayerRoot = 1,

    /// <summary>
    /// Animated lower-spine transform.
    /// </summary>
    LowerSpine = 2,

    /// <summary>
    /// Animated global head transform.
    /// </summary>
    GlobalHead = 3,
}

/// <summary>
/// Chooses the spectator camera anchor source without touching Unity objects.
/// </summary>
public static class SpectatorTargetAnchorSelectionRules
{
    /// <summary>
    /// Resolves the preferred anchor source for a spectated player.
    /// </summary>
    public static SpectatorTargetAnchorSource Resolve(
        bool hasPlayerRoot,
        bool hasLowerSpine,
        bool hasGlobalHead)
    {
        if (hasPlayerRoot)
        {
            return SpectatorTargetAnchorSource.PlayerRoot;
        }

        if (hasLowerSpine)
        {
            return SpectatorTargetAnchorSource.LowerSpine;
        }

        if (hasGlobalHead)
        {
            return SpectatorTargetAnchorSource.GlobalHead;
        }

        return SpectatorTargetAnchorSource.None;
    }
}
