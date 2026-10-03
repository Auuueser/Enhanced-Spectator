namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Local enhanced spectator camera mode.
/// </summary>
public enum SpectatorCameraMode
{
    /// <summary>
    /// Enhanced free-flying spectator camera.
    /// </summary>
    Freecam,

    /// <summary>
    /// Camera follows the local logical ghost/avatar from third person.
    /// </summary>
    ThirdPerson,

    /// <summary>Follow the currently watched living player's eyes.</summary>
    FirstPerson,

    /// <summary>Continuously orbit the watched living player.</summary>
    Cinematic,

    /// <summary>Room-corner tracking camera, available only inside the facility.</summary>
    Monitor,

    /// <summary>Manually selected cinematography samples.</summary>
    Director,
}

/// <summary>Explicit selection survives an indoor-only mode's temporary outdoor fallback.</summary>
internal sealed class MonitorModePreference
{
    private bool _manualMonitor, _manualSelection, _outsideEnabled;
    private SpectatorCameraMode _outsideMode;
    internal bool Automatic { get; private set; }
    internal bool Selected => Automatic || _manualMonitor;
    internal void Select(SpectatorCameraMode mode)
    { _manualSelection=true; _manualMonitor=mode==SpectatorCameraMode.Monitor; Automatic=false; }
    internal void SelectStyle(SpectatorCameraMode current) => Select(Selected ? SpectatorCameraMode.Monitor : current);
    internal void Clear() { _manualSelection=_manualMonitor=Automatic=false; }
    internal SpectatorCameraMode Resolve(SpectatorCameraMode current,bool enabled,bool indoors) =>
        _manualMonitor && enabled ? (indoors ? SpectatorCameraMode.Monitor : SpectatorCameraMode.Freecam) : current;
    internal SpectatorCameraMode ResolveAutomatic(SpectatorCameraMode current,bool enabled,bool indoors,bool allowed,out bool desiredEnabled)
    {
        desiredEnabled=enabled;
        if(Automatic && (!allowed || !indoors))
        { Automatic=false; desiredEnabled=_outsideEnabled; return _outsideMode; }
        if(!_manualSelection && allowed && indoors)
        {
            if(!Automatic) { _outsideMode=current; _outsideEnabled=enabled; Automatic=true; }
            desiredEnabled=true; return SpectatorCameraMode.Monitor;
        }
        return current;
    }
}
