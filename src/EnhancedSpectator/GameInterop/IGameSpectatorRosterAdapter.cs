using System;
using System.Collections.Generic;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Networking;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Game state and retained UI boundary for the spectator roster.</summary>
public interface IGameSpectatorRosterAdapter : IDisposable
{
    /// <summary>Whether the retained view still belongs to the current native HUD and canvas.</summary>
    bool IsViewReady { get; }
    /// <summary>Reads local spectator state only while the spectator HUD is applicable.</summary>
    bool TryGetLocalTarget(out SpectatorTargetState target);
    /// <summary>Reads the configured panel shortcut through the game's input abstraction.</summary>
    bool IsTogglePressed(KeyCode key);
    /// <summary>Updates the transient pointer and hover popup every frame, without rebuilding the roster.</summary>
    void UpdateInteraction(KeyCode cursorKey);
    /// <summary>Copies authoritative connected player identities, including vanilla clients.</summary>
    void CopyPlayersTo(List<SpectatorRosterPlayer> players);
    /// <summary>Updates the retained local overlay.</summary>
    void Present(SpectatorRoster roster, SpectatorTargetState local, bool chinese);
    /// <summary>Hides or reveals owned UI without rebuilding it.</summary>
    void SetVisible(bool visible);
}
