using System;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>Prevents changed item clip indices from being interpreted by the previous catalog.</summary>
public static class FearItemSoundCompatibility
{
    /// <summary>Functional-only item sound ordering.</summary>
    public const int Revision = 3;
    /// <summary>Changed original item/rocket lists require matching ordered fingerprints.</summary>
    public static bool UsesCatalogFingerprint(string modelKey) => modelKey.StartsWith("item:", StringComparison.Ordinal)
        || modelKey == FearModelIdentityRules.Dropship;
    /// <summary>Existing enemy/ship catalogs retain compatibility; item sounds require the exact revision.</summary>
    public static bool CanExchange(string modelKey, int peerRevision) =>
        !UsesCatalogFingerprint(modelKey) || peerRevision == Revision;
}
