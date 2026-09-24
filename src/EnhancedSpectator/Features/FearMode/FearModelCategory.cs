using System;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>Categories available in the retained model catalog.</summary>
public enum FearModelCategory
{
    /// <summary>Every available model.</summary>
    All,
    /// <summary>Enemy visuals and the default ghost.</summary>
    Monsters,
    /// <summary>Original scrap props.</summary>
    Scrap,
    /// <summary>Original equipment and non-scrap props.</summary>
    Items,
    /// <summary>Miniature ship and delivery rocket.</summary>
    Miniatures
}

/// <summary>Stable expanded-catalog identity and scale rules.</summary>
public static class FearModelIdentityRules
{
    /// <summary>Miniature ship key.</summary>
    public const string Ship = "mini:ship";
    /// <summary>Miniature delivery rocket key.</summary>
    public const string Dropship = "mini:dropship";
    /// <summary>Canonical weed-enemy selector key.</summary>
    public const string BushWolf = "BushWolf";
    /// <summary>Whether this key requires the 0.3.2 catalog extension.</summary>
    public static bool IsExpanded(string key) => key.StartsWith("item:", StringComparison.Ordinal)
        || key.StartsWith("mini:", StringComparison.Ordinal) || key == BushWolf;
    /// <summary>Preserves legacy keys; substitutes the default head for an older receiver.</summary>
    public static string KeyForPeer(string key, bool supportsExpanded) =>
        supportsExpanded || !IsExpanded(key) ? key : FearModeRules.DefaultModelKey;
    /// <summary>Never plays an expanded model's sound for a receiver showing its fallback head.</summary>
    public static bool CanPlayForPeer(string key, bool supportsExpanded) => supportsExpanded || !IsExpanded(key);
    /// <summary>Resolves category without depending on a translated label.</summary>
    public static FearModelCategory Category(string key)
    {
        if (key == Ship || key == Dropship) return FearModelCategory.Miniatures;
        if (key.StartsWith("item:", StringComparison.Ordinal) && OriginalItemCatalog.Items.TryGetValue(key.Substring(5), out var item))
            return item.Scrap ? FearModelCategory.Scrap : FearModelCategory.Items;
        return FearModelCategory.Monsters;
    }
    /// <summary>Fits a miniature's longest side to two metres before applying the user multiplier.</summary>
    public static float MiniatureScale(float x, float y, float z, float multiplier)
    {
        float size = Math.Max(x, Math.Max(y, z));
        return size > 0.0001f && !float.IsNaN(size) && !float.IsInfinity(size)
            ? 2f / size * Math.Max(0.01f, multiplier) : 1f;
    }
}
