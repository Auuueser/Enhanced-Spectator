using System;

namespace EnhancedSpectator.GameInterop;

/// <summary>Local spectator HUD layout compatibility boundary.</summary>
public interface IGameSpectatorHudAdapter : IDisposable
{
    /// <summary>Updates or restores the locally borrowed spectator name layout.</summary>
    void UpdateLayout();
}
