namespace EnhancedSpectator.Features.Spectator;

/// <summary>Local pointer ownership, separate from menu/chat focus and automatic camera motion.</summary>
public static class SpectatorPointerCapture
{
    /// <summary>Whether the viewer-list pointer currently consumes local camera interaction.</summary>
    public static bool IsActive { get; internal set; }
}
