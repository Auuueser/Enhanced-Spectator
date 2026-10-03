using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

internal static class MonitorCameraAim
{
    // Pan and tilt an upright rig instead of transporting its up vector across
    // the subject. Quaternion interpolation around an overhead crossing can
    // preserve a 180-degree bank even while the lens still aims correctly.
    internal static Quaternion Upright(Quaternion previous,Vector3 look,float dt,float response,float rate)
    {
        if(dt<=0 || look.sqrMagnitude<.000001f) return previous;
        Vector3 forward=previous*Vector3.forward;
        float yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
        float pitch=-Mathf.Asin(Mathf.Clamp(forward.y,-1f,1f))*Mathf.Rad2Deg;
        Quaternion level=Quaternion.Euler(pitch,yaw,0);
        float roll=Vector3.SignedAngle(level*Vector3.up,previous*Vector3.up,forward);
        Vector3 direction=look.normalized;
        float flat=Mathf.Sqrt(direction.x*direction.x+direction.z*direction.z);
        float targetYaw=flat>.0001f ? Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg : yaw;
        float targetPitch=Mathf.Clamp(-Mathf.Atan2(direction.y,flat)*Mathf.Rad2Deg,-85f,85f);
        float pan=Mathf.DeltaAngle(yaw,targetYaw),tilt=targetPitch-pitch;
        float error=Mathf.Max(Mathf.Abs(pan),Mathf.Abs(tilt),Mathf.Abs(roll));
        float blend=Mathf.Max(1-Mathf.Exp(-dt/response),error>4f ? 1-4f/error : 0);
        Quaternion Pose(float t) => Quaternion.Euler(pitch+tilt*t,yaw+pan*t,roll*(1-t));
        float budget=rate*dt;
        Quaternion result=Pose(blend);
        if(Quaternion.Angle(previous,result)>budget)
        {
            float low=0,high=blend;
            for(int i=0;i<14;i++)
            {
                float middle=(low+high)*.5f;
                if(Quaternion.Angle(previous,Pose(middle))>budget) high=middle;
                else low=middle;
            }
            result=Pose(low);
        }
        return result;
    }

    // At the vertical pole world-up cannot define a unique horizon. Transport the previous
    // camera basis instead, restoring level framing gradually once the subject moves away.
    internal static Quaternion Target(Quaternion previous,Vector3 look)
    {
        if(look.sqrMagnitude<.000001f) return previous;
        Vector3 direction=look.normalized;
        Quaternion carried=Quaternion.FromToRotation(previous*Vector3.forward,direction)*previous;
        carried=Quaternion.LookRotation(direction,carried*Vector3.up);
        float horizon=Mathf.InverseLerp(.997f,.965f,Mathf.Abs(Vector3.Dot(direction,Vector3.up)));
        if(horizon<=0) return carried;
        return Quaternion.Slerp(carried,Quaternion.LookRotation(direction,Vector3.up),Mathf.SmoothStep(0,1,horizon));
    }
}
