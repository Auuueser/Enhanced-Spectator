using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Bounded route search, only when acquiring a station. No per-frame allocations or global pathfinding.</summary>
internal sealed class MonitorTravelPath
{
    private Vector3 _from, _control, _to;
    internal float Length { get; private set; }
    internal bool Curved { get; private set; }
    internal Vector3 Point(float t) { float u=1-t; return u*u*_from+2*u*t*_control+t*t*_to; }
    internal bool Plan(IGameMonitorCameraAdapter world, Vector3 from, Vector3 to, Vector3 focus, bool allowCurve)
    {
        _from=from; _to=to; _control=(from+to)*.5f; Curved=false;
        if (Validate(world,focus)) return true;
        if (!allowCurve) return false;
        Vector3 axis=to-from; axis.y=0;
        Vector3 side=axis.sqrMagnitude>.01f ? Vector3.Cross(Vector3.up,axis.normalized) : Vector3.right;
        for(int i=0;i<4;i++)
        {
            float width=i<2 ? 2.5f : 4.5f;
            _control=(from+to)*.5f+side*(i%2==0 ? width : -width);
            if (Validate(world,focus)) { Curved=true; return true; }
        }
        return false;
    }
    // A physical route is not automatically a comfortable push. Decide before showing any movement.
    internal bool SuitablePush(IGameMonitorCameraAdapter world,Vector3 focus,Vector3 predicted)
    {
        if(Length>10f || Length>Mathf.Max(1f,(_to-_from).magnitude)*1.3f) return false;
        Vector3 origin=_from-focus; origin.y=0;
        for(int i=0;i<=8;i++)
        {
            Vector3 point=Point(i/8f), offset=point-focus;
            Vector3 planar=offset; planar.y=0;
            if(planar.magnitude<2.5f || Mathf.Abs(offset.y)>planar.magnitude*.85f
                || Vector3.Angle(origin,planar)>65f || !world.IsMonitorSightClear(point,predicted)) return false;
        }
        return true;
    }

    private bool Validate(IGameMonitorCameraAdapter world, Vector3 focus)
    {
        Vector3 previous=_from; Length=0;
        for(int i=1;i<=8;i++)
        {
            Vector3 point=Point(i/8f);
            if (!world.IsMonitorPathClear(previous,point) || !world.IsMonitorSightClear(point,focus)
                || DistanceToSegment(focus,previous,point)<.9f) return false;
            Length+=(point-previous).magnitude; previous=point;
        }
        return true;
    }
    internal static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 delta=b-a;
        float t=delta.sqrMagnitude>.0001f ? Mathf.Clamp01(Vector3.Dot(point-a,delta)/delta.sqrMagnitude) : 0;
        return (point-(a+delta*t)).magnitude;
    }
}
