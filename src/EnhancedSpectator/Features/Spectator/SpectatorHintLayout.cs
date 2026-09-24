using System;

namespace EnhancedSpectator.Features.Spectator;

internal static class SpectatorHintLayout
{
    // Size to measured localized text; fit both columns together instead of stretching them apart.
    internal static (float Label, float Keys, float Gap, float Width, float Scale) Fit(float label, float keys, float gap, float maximum)
    {
        label = Math.Max(1f, label); keys = Math.Max(1f, keys); gap = Math.Max(1f, gap);
        float desired = label + keys + gap;
        float scale = Math.Min(1f, Math.Max(1f, maximum) / desired);
        return (label * scale, keys * scale, gap * scale, desired * scale, scale);
    }
}
