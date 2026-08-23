using System.Collections.Generic;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Reports which player ids currently have a rendered fear visual replacing the default head.
/// </summary>
public interface IFearVisualOverrideState
{
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
