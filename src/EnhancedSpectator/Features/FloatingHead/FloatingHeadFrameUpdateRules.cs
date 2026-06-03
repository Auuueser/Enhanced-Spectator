namespace EnhancedSpectator.Features.FloatingHead;

/// <summary>
/// Pure frame-level scheduling rules for floating-head visual updates.
/// </summary>
public static class FloatingHeadFrameUpdateRules
{
    /// <summary>
    /// Returns whether the current render callback must run the expensive full visual update.
    /// </summary>
    public static bool ShouldRunFullVisualUpdate(
        int currentFrame,
        int lastFullUpdateFrame,
        bool hasCachedVisualState)
    {
        return !hasCachedVisualState || lastFullUpdateFrame != currentFrame;
    }
}
