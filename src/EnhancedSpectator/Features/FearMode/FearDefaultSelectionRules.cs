namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Pure scheduling rules for establishing an implicit Default selection for a newly dead player.
/// </summary>
public static class FearDefaultSelectionRules
{
    /// <summary>Gets whether the local client should submit the missing Default selection now.</summary>
    public static bool ShouldRequestDefault(
        bool sessionEnabled,
        bool isLocalDead,
        bool hasSelection,
        int currentFrame,
        int nextRetryFrame)
    {
        return sessionEnabled
            && isLocalDead
            && !hasSelection
            && currentFrame >= nextRetryFrame;
    }
}
