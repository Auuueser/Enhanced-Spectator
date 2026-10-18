using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Whether the spectator has left the camera alone for the idle wait, in any camera and whatever their recentring
/// option. Published as idle: other players hide an idle spectator's model (by their own option), translucent while
/// it speaks. Only deliberate operation counts: a click, a wheel step, moving the free or third-person camera with its
/// keys, or the view turned by more than half a degree within about a quarter second. A hand resting on the mouse
/// (sensor jitter, tremor) leaves the spectator idle.
/// </summary>
internal sealed class SpectatorIdle
{
    internal const float Seconds = 3f, TurnDegrees = .5f, TurnWindow = .25f;
    private float _seconds, _turn;
    internal bool IsIdle => _seconds >= Seconds;

    /// <summary>Returns whether this frame operated the camera.</summary>
    internal bool Update(bool spectating, bool click, Vector2 lookDegrees, float deltaTime)
    {
        // Turning drains at half a degree per quarter second: slower drift never adds up to an operation.
        _turn = Mathf.Max(0, _turn - deltaTime * TurnDegrees / TurnWindow) + lookDegrees.magnitude;
        bool operated = click || _turn > TurnDegrees;
        if (operated) _turn = 0;
        _seconds = !spectating || operated ? 0 : _seconds + deltaTime;
        return operated;
    }
}
