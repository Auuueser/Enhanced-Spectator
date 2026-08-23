using System;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>Resolves name-tag clearance above optional fear visuals.</summary>
public static class FearNameTagPlacementRules
{
    /// <summary>Returns the configured base offset or model top plus that clearance.</summary>
    public static float ResolveHeightOffset(float configuredOffset, bool hasFearTop, float fearTopOffset)
    {
        float clearance = Math.Max(0f, configuredOffset);
        return hasFearTop ? Math.Max(clearance, Math.Max(0f, fearTopOffset) + clearance) : clearance;
    }
}
