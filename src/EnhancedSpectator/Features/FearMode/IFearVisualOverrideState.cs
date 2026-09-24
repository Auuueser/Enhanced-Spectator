using System.Collections.Generic;
using System;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Reports which player ids currently have a rendered fear visual replacing the default head.
/// </summary>
public interface IFearVisualOverrideState
{
    /// <summary>Gets the validated owner scale for the default-head fallback.</summary>
    float GetOwnerScale(ulong clientId);
    /// <summary>Gets whether the specified player's default head should be suppressed.</summary>
    bool IsFearVisualActive(ulong clientId);

    /// <summary>Attempts to get the rendered model height above its representation origin.</summary>
    bool TryGetWorldTopOffset(ulong clientId, out float topOffset);
}

/// <summary>
/// Mutable per-frame override registry shared with default visual services.
/// </summary>
public sealed class FearVisualOverrideRegistry : IFearVisualOverrideState
{
    private readonly Dictionary<ulong, float> _activeClientOffsets = new Dictionary<ulong, float>();
    private Func<ulong, float>? _ownerScale;

    /// <summary>Binds the current authoritative selection lookup without storing stale sizes.</summary>
    public void BindOwnerScale(Func<ulong, float> lookup) => _ownerScale = lookup;
    /// <inheritdoc />
    public float GetOwnerScale(ulong clientId) => FearModelAppearanceRules.ClampScale(_ownerScale?.Invoke(clientId) ?? 1f);

    /// <inheritdoc />
    public bool IsFearVisualActive(ulong clientId)
    {
        return _activeClientOffsets.ContainsKey(clientId);
    }

    /// <inheritdoc />
    public bool TryGetWorldTopOffset(ulong clientId, out float topOffset)
    {
        return _activeClientOffsets.TryGetValue(clientId, out topOffset);
    }

    /// <summary>Marks or unmarks one player override.</summary>
    public void SetActive(ulong clientId, bool active, float topOffset = 0f)
    {
        if (active)
        {
            _activeClientOffsets[clientId] = topOffset;
        }
        else
        {
            _activeClientOffsets.Remove(clientId);
        }
    }

    /// <summary>Clears all active overrides.</summary>
    public void Clear()
    {
        _activeClientOffsets.Clear();
    }
}
