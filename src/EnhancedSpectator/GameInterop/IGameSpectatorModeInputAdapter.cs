namespace EnhancedSpectator.GameInterop;

// Split-screen pointer capture blocks camera motion while view shortcuts remain usable.
internal interface IGameSpectatorModeInputAdapter
{
    bool IsViewModeInputBlocked();
}
