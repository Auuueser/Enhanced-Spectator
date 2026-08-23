using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Pure normalization rules that map arbitrary prefab renderer bounds onto a spectator world pose.
/// </summary>
public static class FearVisualNormalizationRules
{
    /// <summary>Returns the content translation that places its combined bounds center at the pose origin.</summary>
    public static Vector3 ResolveContentOffset(Vector3 combinedBoundsCenter)
    {
        return -combinedBoundsCenter;
    }

    /// <summary>Returns the world translation that aligns current renderer bounds with the representation pose.</summary>
    public static Vector3 ResolveRuntimeWorldOffset(Vector3 representationPosition, Vector3 currentBoundsCenter)
    {
        return representationPosition - currentBoundsCenter;
    }

    /// <summary>Returns a uniform scale that normalizes the combined visible height.</summary>
    public static float ResolveUniformScale(float targetHeight, float sourceHeight)
    {
        return Mathf.Max(0.05f, targetHeight) / Mathf.Max(0.01f, sourceHeight);
    }

    /// <summary>Resolves the final original-size or normalized-size scale with a global multiplier.</summary>
    public static float ResolveFinalScale(
        bool useOriginalScale,
        float targetHeight,
        float sourceHeight,
        float scaleMultiplier)
    {
        float multiplier = Mathf.Max(0.01f, scaleMultiplier);
        return useOriginalScale
            ? multiplier
            : ResolveUniformScale(targetHeight, sourceHeight) * multiplier;
    }
}
