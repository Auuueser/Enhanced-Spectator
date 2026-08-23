using System.Collections.Generic;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Stores the newest validated selection for each origin player.
/// </summary>
public sealed class FearModeSelectionRegistry
{
    private readonly Dictionary<ulong, FearModeSelectionState> _states =
        new Dictionary<ulong, FearModeSelectionState>();
    private int _revision;

    /// <summary>Gets the local registry revision.</summary>
    public int Revision => _revision;

    /// <summary>Stores a strictly newer per-origin selection.</summary>
    public bool Update(FearModeSelectionState state)
    {
        if (_states.TryGetValue(state.ClientId, out FearModeSelectionState existing)
            && state.Revision <= existing.Revision)
        {
            return false;
        }

        _states[state.ClientId] = state;
        _revision++;
        return true;
    }

    /// <summary>Attempts to get the current selection for one origin.</summary>
    public bool TryGet(ulong clientId, out FearModeSelectionState state)
    {
        return _states.TryGetValue(clientId, out state!);
    }

    /// <summary>Removes one origin selection.</summary>
    public bool Remove(ulong clientId)
    {
        if (!_states.Remove(clientId))
        {
            return false;
        }

        _revision++;
        return true;
    }

    /// <summary>Clears all selections.</summary>
    public void Clear()
    {
        if (_states.Count == 0)
        {
            return;
        }

        _states.Clear();
        _revision++;
    }

    /// <summary>Copies current selections into caller-owned storage.</summary>
    public void CopyTo(List<FearModeSelectionState> destination)
    {
        destination.Clear();
        foreach (FearModeSelectionState state in _states.Values)
        {
            destination.Add(state);
        }
    }
}
