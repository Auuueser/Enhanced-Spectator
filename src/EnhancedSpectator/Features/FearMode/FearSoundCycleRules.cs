using System;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Pure rules for sequential fear-sound playback.
/// </summary>
public static class FearSoundCycleRules
{
    /// <summary>Gets whether a completed local clip should advance while the initiating hold continues.</summary>
    public static bool ShouldAdvance(bool autoCycleArmed, bool keyHeld, bool selectionUnchanged)
    {
        return autoCycleArmed && keyHeld && selectionUnchanged;
    }

    /// <summary>Gets the next bounded clip index, wrapping at the end of the catalog.</summary>
    public static int ResolveNextIndex(int currentIndex, int clipCount)
    {
        if (clipCount <= 0)
        {
            return -1;
        }

        return (Math.Max(-1, currentIndex) + 1) % clipCount;
    }
}
