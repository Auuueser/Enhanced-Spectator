namespace EnhancedSpectator.Features.Spectator;

/// <summary>Transient HUD request; menus temporarily reveal UI and life boundaries clear it.</summary>
internal sealed class SpectatorHudVisibilityState
{
    internal bool Requested { get; private set; }
    internal bool Effective { get; private set; }
    internal void Update(bool spectating, bool inputBlocked, bool toggle)
    {
        if (!spectating) Requested = false;
        else if (!inputBlocked && toggle) Requested = !Requested;
        Effective = spectating && !inputBlocked && Requested;
    }
    internal void SetRequested(bool value) => Requested = value;
}
