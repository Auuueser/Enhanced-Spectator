namespace EnhancedSpectator.Features.FloatingHead;

/// <summary>
/// Pure invalidation rules for cached floating-head name tag text.
/// </summary>
public static class NameTagTextCacheRules
{
    /// <summary>
    /// Gets whether cached name tag text should be cleared before formatting.
    /// </summary>
    public static bool ShouldClear(
        int lastIdentityRevision,
        int currentIdentityRevision,
        bool lastUseGamePlayerNames,
        bool currentUseGamePlayerNames,
        bool lastUseFallbackIds,
        bool currentUseFallbackIds)
    {
        return lastIdentityRevision != currentIdentityRevision
            || lastUseGamePlayerNames != currentUseGamePlayerNames
            || lastUseFallbackIds != currentUseFallbackIds;
    }
}
