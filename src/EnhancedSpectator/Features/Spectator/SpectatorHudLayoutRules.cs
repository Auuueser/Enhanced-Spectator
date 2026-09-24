using System;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Canvas-space spectator name placement below an existing clock.</summary>
public static class SpectatorHudLayoutRules
{
    /// <summary>Computes an absolute, non-accumulating shift from the original name rectangle.</summary>
    public static float DownwardOffset(float nameTop, float clockBottom, float gap = 8f) =>
        Math.Min(0f, clockBottom - gap - nameTop);

    /// <summary>Fits the label into the vertical space between the clock and canvas bottom.</summary>
    public static float AvailableHeight(float clockBottom, float canvasBottom, float desiredHeight, float gap = 8f) =>
        Math.Max(0f, Math.Min(desiredHeight, clockBottom - canvasBottom - gap - 2f));
}
