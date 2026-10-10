using EnhancedSpectator.Features.Spectator;

namespace EnhancedSpectator.GameInterop;

public sealed partial class LethalCompanySpectatorAdapter
{
    /// <inheritdoc />
    // Menu/chat/focus and our pointer ownership block all camera controls. A foreign visible cursor only
    // suppresses look, in the input reader; it must not disable keyboard modes or movement.
    public bool IsCameraInputBlocked()
        => _boundView || SpectatorPointerCapture.IsActive || LethalCompanySplitScreenUiPresentation.ReportOwned || IsUiInputBlocked();

    bool IGameSpectatorModeInputAdapter.IsViewModeInputBlocked() => LethalCompanySplitScreenUiPresentation.ReportOwned || IsUiInputBlocked();
}
