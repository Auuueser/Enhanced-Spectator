using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Idle look rig and anchor-relative head-height easing for free/third-person ghosts.</summary>
internal sealed class SpectatorAutoCenter
{
    internal const float IdleSeconds = 3f;
    internal const float HeadHeight = 1.1f;
    // The centred camera's height: above the teammate's head, looking down at it.
    internal const float CameraHeight = 2f;
    private bool _ready;
    private float _idle;
    private Quaternion _rotation;
    // Living and dead viewers alike: many idle spectators would otherwise crowd every view.
    internal static bool ShouldHideRemoteModel(bool enabled, bool senderCentering) => enabled && senderCentering;

    /// <summary>
    /// Whether a spectator counts as centred (hidden from others). A spectator who has just died has not touched the
    /// camera: centred from the start. In split-screen, enlarging another view or passing through the tiles restarts
    /// the camera and its idle wait, not the player's idleness. Either way it holds until the player moves the camera
    /// (mouse look, wheel or a click on the view), so the model does not show by itself.
    /// </summary>
    internal static bool HoldCentering(bool held, bool centering, bool holds, bool cameraMoved)
        => centering || held && holds && !cameraMoved;

    internal bool IsCentering { get; private set; }
    // How far the centering has eased in (0 at the idle boundary, 1 after 0.4 s), shared by the height ease.
    private float _ramp;
    internal void Clear() { _ready = false; _idle = 0; IsCentering = false; _keepIdle = false; }
    /// <summary>The player operated the camera without looking around (the wheel): the idle wait starts over.</summary>
    internal void Touch() { _idle = 0; IsCentering = false; }
    /// <summary>
    /// A new base view (the teammate teleported through an entrance, the follow rig restarted) without any player
    /// input: the rotation re-seeds, but the idle time carries on, so a centred ghost stays centred (and hidden)
    /// instead of reappearing until the idle wait runs out again.
    /// </summary>
    internal void Rebase() { _ready = false; _keepIdle = true; }
    private bool _keepIdle;
    /// <summary>
    /// Only looking around resets the idle time. A menu, chat, another window or a free cursor zero
    /// <paramref name="mouse"/> at the caller: the player is not operating the camera then, so it recentres as usual.
    /// </summary>
    internal Quaternion Update(Quaternion baseRotation, Vector3 direction, Vector2 mouse, float dt, bool enabled, int speed, bool baseIncludesMouse = false)
    {
        IsCentering = false;
        bool initial = !_ready;
        if (!_ready || !enabled) { _rotation = baseRotation; _ready = true; if (!(_keepIdle && enabled)) _idle = 0; _keepIdle = false; }
        if (!enabled) return baseRotation;
        dt = Mathf.Clamp(dt, 0, .1f);
        if (mouse.sqrMagnitude > .0001f)
        {
            _idle = 0;
            if (initial && baseIncludesMouse) return _rotation;
            var angles = _rotation.eulerAngles;
            float pitch = Mathf.DeltaAngle(0, angles.x);
            _rotation = Quaternion.Euler(Mathf.Clamp(pitch - mouse.y, -85, 85), angles.y + mouse.x, 0);
        }
        else _idle += dt;
        if (_idle > IdleSeconds && direction.sqrMagnitude > .01f)
        {
            IsCentering = true;
            float ramp = _ramp = Mathf.SmoothStep(0, 1, (_idle - IdleSeconds) / .4f);
            float t = 1 - Mathf.Exp(-dt * ramp / (.32f * SpectatorFollowStabilizer.ResponseScale(speed)));
            _rotation = Quaternion.Slerp(_rotation, Quaternion.LookRotation(direction, Vector3.up), t);
        }
        return _rotation;
    }

    /// <summary>
    /// Eases the anchor-relative offset up to <see cref="CameraHeight"/>, above the head the view turns to, so the
    /// centred view looks down at the teammate. The rendered camera receives the follow rig's translation separately;
    /// including that translation here would apply it twice during jumps and stairs.
    /// </summary>
    internal float EaseHeight(float current, float dt, int speed)
    {
        if (!IsCentering) return current;
        return Mathf.Lerp(current, CameraHeight, Ease(dt, speed));
    }

    /// <summary>
    /// While centering, a camera closer than <paramref name="distance"/> (the player's spectate distance) backs off to
    /// it horizontally, keeping its direction from the teammate (<paramref name="away"/> when it is right above them);
    /// a farther camera stays where it is.
    /// </summary>
    internal Vector3 EaseOutward(Vector3 offset, float distance, Vector3 away, float dt, int speed)
    {
        if (!IsCentering) return offset;
        var flat = new Vector3(offset.x, 0, offset.z);
        float wanted = Mathf.Sqrt(Mathf.Max(0, distance * distance - offset.y * offset.y));
        if (flat.magnitude >= wanted) return offset;
        away.y = 0;
        Vector3 direction = flat.sqrMagnitude > .0001f ? flat.normalized : away.sqrMagnitude > .0001f ? away.normalized : Vector3.back;
        return direction * Mathf.Lerp(flat.magnitude, wanted, Ease(dt, speed)) + Vector3.up * offset.y;
    }

    private float Ease(float dt, int speed)
        => 1 - Mathf.Exp(-Mathf.Clamp(dt, 0, .1f) * _ramp / (.5f * SpectatorFollowStabilizer.ResponseScale(speed)));
}
