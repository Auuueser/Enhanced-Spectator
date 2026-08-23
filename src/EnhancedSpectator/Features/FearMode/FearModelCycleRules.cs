namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Pure wraparound selection rules shared by the fear-model menu and hotkeys.
/// </summary>
public static class FearModelCycleRules
{
    /// <summary>Returns the wrapped model index after moving in the requested direction.</summary>
    public static int ResolveNextIndex(int currentIndex, int modelCount, int direction)
    {
        if (modelCount <= 0)
        {
            return -1;
        }

        int normalizedCurrent = currentIndex >= 0 && currentIndex < modelCount ? currentIndex : 0;
        int normalizedDirection = direction < 0 ? -1 : 1;
        return (normalizedCurrent + normalizedDirection + modelCount) % modelCount;
    }
}
