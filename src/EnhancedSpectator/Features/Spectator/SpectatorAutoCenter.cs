using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Render-only look rig. Idle tracking never moves the ghost, voice or orbital station.</summary>
internal sealed class SpectatorAutoCenter
{
    internal const float IdleSeconds = 1.2f;
    private bool _ready;
    private float _idle;
    private Quaternion _rotation;
    internal static bool ShouldHideRemoteModel(bool enabled, bool viewerAlive, bool senderCentering)
        => enabled && viewerAlive && senderCentering;

    internal bool IsCentering { get; private set; }
    internal void Clear() { _ready = false; _idle = 0; IsCentering = false; }
    internal Quaternion Update(Quaternion baseRotation, Vector3 direction, Vector2 mouse, float dt, bool enabled, bool blocked, int speed, bool baseIncludesMouse = false)
    {
        IsCentering = false;
        bool initial = !_ready;
        if (!_ready || !enabled) { _rotation = baseRotation; _ready = true; _idle = 0; }
        if (!enabled) return baseRotation;
        if (blocked) { _idle = 0; return _rotation; }
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
            float ramp = Mathf.SmoothStep(0, 1, (_idle - IdleSeconds) / .4f);
            float t = 1 - Mathf.Exp(-dt * ramp / (.32f * SpectatorFollowStabilizer.ResponseScale(speed)));
            _rotation = Quaternion.Slerp(_rotation, Quaternion.LookRotation(direction, Vector3.up), t);
        }
        return _rotation;
    }
}
