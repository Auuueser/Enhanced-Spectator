namespace EnhancedSpectator.Features.PlayerStateSync;

/// <summary>
/// Contains pure scheduling rules for connected-player state repair.
/// </summary>
public static class ConnectedPlayerStateRepairScheduleRules
{
    /// <summary>
    /// Gets whether the repair adapter should run on this tick.
    /// </summary>
    public static bool ShouldRunRepair(
        bool initialized,
        bool enabled,
        float now,
        float nextRepairTime,
        int lastIdentityRevision,
        int currentIdentityRevision,
        int lastTargetRevision,
        int currentTargetRevision,
        float nextIdleFallbackRepairTime,
        bool repeatAfterRepair)
    {
        if (!initialized || !enabled || now < nextRepairTime)
        {
            return false;
        }

        return repeatAfterRepair
            || lastIdentityRevision < 0
            || lastTargetRevision < 0
            || lastIdentityRevision != currentIdentityRevision
            || lastTargetRevision != currentTargetRevision
            || now >= nextIdleFallbackRepairTime;
    }
}
