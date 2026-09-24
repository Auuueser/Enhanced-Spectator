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
}
