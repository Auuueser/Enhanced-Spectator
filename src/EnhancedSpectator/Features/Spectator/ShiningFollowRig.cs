using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Fast critically damped pursuit on an orbit, never a chord through the subject.</summary>
internal sealed class ShiningFollowRig
{
    private bool _ready;
    private float _yaw, _yawVelocity, _radius, _radiusVelocity, _height, _heightVelocity;
    internal void Clear() { _ready=false; _yawVelocity=_radiusVelocity=_heightVelocity=0; }
    internal Vector3 Update(Vector3 body, Vector3 forward, Vector3 previous, float distance, float height, float dt, int tier)
    {
        float targetYaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg+180f;
        if (!_ready)
        {
            Vector3 offset=previous-body; offset.y=0;
            _yaw=offset.sqrMagnitude>.01f ? Mathf.Atan2(offset.x,offset.z)*Mathf.Rad2Deg : targetYaw;
            _radius=Mathf.Max(1.5f,offset.magnitude); _height=previous.y-body.y; _ready=true;
        }
        targetYaw=_yaw+Mathf.DeltaAngle(_yaw,targetYaw);
        float response=.13f*SpectatorFollowStabilizer.ResponseScale(tier)/.6f;
        dt=Mathf.Clamp(dt,0,.1f);
        _yaw=Spring(_yaw,targetYaw,ref _yawVelocity,dt,response);
        _radius=Spring(_radius,distance,ref _radiusVelocity,dt,response);
        _height=Spring(_height,height,ref _heightVelocity,dt,response);
        return body+Quaternion.Euler(0,_yaw,0)*Vector3.forward*Mathf.Max(1.5f,_radius)+Vector3.up*_height;
    }
    private static float Spring(float value,float goal,ref float velocity,float dt,float response)
    {
        float omega=2f/response, offset=value-goal, term=(velocity+omega*offset)*dt, decay=Mathf.Exp(-omega*dt);
        velocity=(velocity-omega*term)*decay;
        return goal+(offset+term)*decay;
    }
}
