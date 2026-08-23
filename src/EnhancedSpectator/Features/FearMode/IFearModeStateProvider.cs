using System.Collections.Generic;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Exposes validated fear-mode session and player-selection state to visuals and UI.
/// </summary>
public interface IFearModeStateProvider
{
    /// <summary>Gets the host-authoritative session gate.</summary>
    bool IsSessionEnabled { get; }

    /// <summary>Attempts to get one validated player selection.</summary>
    bool TryGetSelection(ulong clientId, out FearModeSelectionState selection);

    /// <summary>Copies all validated selections into caller-owned storage.</summary>
    void CopySelectionsTo(List<FearModeSelectionState> destination);
}
