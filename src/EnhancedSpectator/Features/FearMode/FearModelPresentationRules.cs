using System;
using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Per-model inclusion and bind-pose orientation rules confirmed through runtime feedback.
/// </summary>
public static class FearModelPresentationRules
{
    /// <summary>Stable selector alias for the bloom enemy nested under Cadaver Growth.</summary>
    public const string CadaverBloomModelKey = "Cadaver Bloom";

    /// <summary>Stable selector alias for the pre-transformation Maneater visual.</summary>
    public const string ManeaterBabyModelKey = "Maneater (Baby)";

    /// <summary>Stable selector alias for the post-transformation Maneater visual.</summary>
    public const string ManeaterAdultModelKey = "Maneater (Adult)";

    /// <summary>Gets whether the model should be omitted from the selector.</summary>
    public static bool IsExcludedModel(string? modelKey)
    {
        return EqualsKey(modelKey, "Cadaver growths")
            || EqualsKey(modelKey, "Cadaver Growth")
            || EqualsKey(modelKey, "CadaverGrowth")
            || EqualsKey(modelKey, "Docile Locust Bees")
            || EqualsKey(modelKey, "Red Locust Bees");
    }

    /// <summary>Returns the model-space Euler correction for a known V81 bind pose.</summary>
    public static Vector3 ResolveCorrectionEuler(string? modelKey)
    {
        if (modelKey != null && modelKey.StartsWith("item:", StringComparison.Ordinal)) return FearItemPoseRules.WorldEuler(modelKey);
        if (modelKey == FearModelIdentityRules.Dropship) return new Vector3(-90f, 0f, 0f);
        if (EqualsKey(modelKey, "Baboon hawk")
            || EqualsKey(modelKey, "Bunker Spider")
            || EqualsKey(modelKey, "Crawler")
            || EqualsKey(modelKey, "Nutcracker")
            || EqualsKey(modelKey, "RadMech"))
        {
            return new Vector3(0f, 180f, 0f);
        }

        if (EqualsKey(modelKey, "Hoarding bug")
            || EqualsKey(modelKey, "Lasso")
            || EqualsKey(modelKey, ManeaterBabyModelKey)
            || EqualsKey(modelKey, ManeaterAdultModelKey))
        {
            return new Vector3(0f, 90f, 0f);
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Gets whether the serialized prefab root pose must be normalized for this renderer-only bind pose.
    /// </summary>
    public static bool ShouldNormalizeSourceRootPose(string? modelKey)
    {
        return EqualsKey(modelKey, "Masked") || EqualsKey(modelKey, "Spring");
    }

    /// <summary>
    /// Gets whether the cloned visual needs a final renderer-bounds correction after its first world pose.
    /// </summary>
    public static bool ShouldRecenterFromRuntimeBounds(string? modelKey)
    {
        return EqualsKey(modelKey, ManeaterAdultModelKey);
    }

    /// <summary>
    /// Keeps monster models upright, inherits spectator yaw, and applies the per-model bind-pose correction.
    /// </summary>
    public static Quaternion ResolveWorldRotation(Quaternion spectatorLookRotation, string? modelKey)
    {
        if (modelKey != null && modelKey.StartsWith("item:", StringComparison.Ordinal))
            return FearItemPoseRules.WorldRotation(spectatorLookRotation.eulerAngles.y, modelKey);
        Vector3 correction = ResolveCorrectionEuler(modelKey);
        return Quaternion.Euler(
            correction.x,
            spectatorLookRotation.eulerAngles.y + correction.y,
            correction.z);
    }

    private static bool EqualsKey(string? value, string expected)
    {
        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }
}
