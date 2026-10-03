using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Camera-only rig: manual input and network/voice poses never pass through this filter.</summary>
internal sealed class SpectatorFollowStabilizer
{
    private bool _ready;
    private Vector3 _position, _velocity, _lastInput;
    internal void Clear() { _ready = false; _velocity = Vector3.zero; }
    internal void Transport(Vector3 delta)
    { if (_ready) { _position+=delta; _lastInput+=delta; } }
    internal Vector3 Update(Vector3 target, float dt, bool enabled, int speed = 1)
    {
        if (!_ready || !enabled || (target-_lastInput).sqrMagnitude > 64f)
        { _position=target; _velocity=Vector3.zero; _ready=true; }
        float fallSpeed = dt > .0001f ? Mathf.Max(0, (_lastInput.y-target.y)/dt) : 0;
        _lastInput=target;
        if (!enabled || dt<=0) return _position;
        dt=Mathf.Min(dt,.1f);
        float response = ResponseScale(speed);
        _position.x=Axis(_position.x,target.x,ref _velocity.x,dt,.16f*response);
        _position.z=Axis(_position.z,target.z,ref _velocity.z,dt,.16f*response);
        // Suppress upward jump/bob, but do not leave the camera upstairs during a fall.
        float verticalResponse = Mathf.Lerp(.65f, .075f, target.y < _position.y ? Mathf.Clamp01(fallSpeed / 4f) : 0);
        _position.y=Axis(_position.y,target.y,ref _velocity.y,dt,verticalResponse*response);
        return _position;
    }
    internal static float ResponseScale(int speed) => speed == 0 ? 1f : speed == 2 ? .35f : .6f;
    // Exact critically damped spring for a constant target over this frame. No overshooting lerp chain.
    private static float Axis(float value,float target,ref float velocity,float dt,float response)
    {
        float omega=2f/response, offset=value-target, term=(velocity+omega*offset)*dt;
        float decay=Mathf.Exp(-omega*dt);
        velocity=(velocity-omega*term)*decay;
        return target+(offset+term)*decay;
    }
}
