using System;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>Bounded owner scale and smooth watched-body distance visibility.</summary>
public static class FearModelAppearanceRules
{
    /// <summary>Local viewer preference; network role and model owner never change this decision.</summary>
    public static bool ShouldFade(bool enabled, bool viewerDead, bool fadeWhileSpectating) =>
        enabled && (!viewerDead || fadeWhileSpectating);

    /// <summary>Full opacity uses the untouched native render path, including its scene depth.</summary>
    public static bool NeedsComposite(float opacity) => !float.IsNaN(opacity) && opacity >= 0f && opacity < 1f;

    /// <summary>Rejects invalid scale payloads and caps the permitted world multiplier.</summary>
    public static float ClampScale(float scale) => float.IsNaN(scale) || float.IsInfinity(scale)
        ? 1f : Math.Max(0.1f, Math.Min(5f, scale));

    /// <summary>Quintic opacity between model and watched-body envelopes; configurable outer distance, 0.15 m inner distance.</summary>
    public static float Opacity(float surfaceDistance, bool enabled, float radius = 2.5f)
    {
        if (!enabled || float.IsNaN(surfaceDistance)) return 1f;
        if (float.IsNaN(radius) || float.IsInfinity(radius)) radius = 2.5f;
        radius = Math.Max(.5f, Math.Min(10f, radius));
        float t = Math.Max(0f, Math.Min(1f, (surfaceDistance - 0.15f) / (radius - .15f)));
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    /// <summary>Frame-rate-independent easing without alpha overshoot or hard cuts on target changes.</summary>
    public static float SmoothOpacity(float current, float desired, float elapsed)
    {
        if (float.IsNaN(current) || float.IsInfinity(current)) current = 1f;
        if (float.IsNaN(desired) || float.IsInfinity(desired)) desired = 1f;
        current = Math.Max(0, Math.Min(1, current)); desired = Math.Max(0, Math.Min(1, desired));
        if (float.IsNaN(elapsed) || elapsed <= 0) return current;
        return current + (desired - current) * (1f - (float)Math.Exp(-Math.Min(.1f, elapsed) / .2f));
    }
}
