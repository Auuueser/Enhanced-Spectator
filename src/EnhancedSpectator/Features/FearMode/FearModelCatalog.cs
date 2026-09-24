using System;
using System.Collections.Generic;
using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Caches bounded runtime model keys resolved through GameInterop.
/// </summary>
public sealed class FearModelCatalog
{
    private readonly IGameFearModeAdapter _adapter;
    private readonly List<string> _modelKeys = new List<string>();

    /// <summary>Creates a catalog backed by the confirmed game adapter.</summary>
    public FearModelCatalog(IGameFearModeAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    /// <summary>Gets the current ordered model keys, including Default.</summary>
    public IReadOnlyList<string> ModelKeys => _modelKeys;

    /// <summary>Refreshes keys from confirmed original visual sources.</summary>
    public void Refresh()
    {
        _adapter.CopyAvailableModelKeysTo(_modelKeys);
        for (int index = _modelKeys.Count - 1; index >= 0; index--)
        {
            if (!FearModeRules.IsValidModelKey(_modelKeys[index])
                || FearModelPresentationRules.IsExcludedModel(_modelKeys[index])
                || string.Equals(_modelKeys[index], FearModeRules.DefaultModelKey, StringComparison.Ordinal))
            {
                _modelKeys.RemoveAt(index);
            }
        }

        _modelKeys.Sort(StringComparer.Ordinal);
        _modelKeys.Insert(0, FearModeRules.DefaultModelKey);
    }

    /// <summary>Gets whether the key exists in the refreshed catalog.</summary>
    public bool IsAllowed(string modelKey)
    {
        if (!FearModeRules.IsValidModelKey(modelKey))
        {
            return false;
        }

        for (int index = 0; index < _modelKeys.Count; index++)
        {
            if (string.Equals(_modelKeys[index], modelKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Attempts to resolve the source prefab transform without instantiating it.</summary>
    public bool TryGetVisualSource(string modelKey, out FearVisualSource? source)
    {
        if (string.Equals(modelKey, FearModeRules.DefaultModelKey, StringComparison.Ordinal)
            || !IsAllowed(modelKey))
        {
            source = null;
            return false;
        }

        return _adapter.TryGetVisualSource(modelKey, out source) && source != null;
    }
}

/// <summary>Pure retry cadence for catalogs that may initialize before level definitions are ready.</summary>
public static class FearModelCatalogRefreshRules
{
    /// <summary>Gets the next refresh delay, retrying an incomplete Default-only catalog promptly.</summary>
    public static int ResolveDelayFrames(int modelCount)
    {
        return modelCount <= 1 ? 30 : 300;
    }
}
