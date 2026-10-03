using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

internal sealed partial class MonitorTravellingDirector
{
    private bool _rearPursuit,_rearHasBearing;
    private Vector3 _rearBearing;
    private float _rearGroundRate,_rearGroundSlopeRate;

    private void UpdateRearBearing(IGameMonitorCameraAdapter adapter,float dt)
    {
        if(!_rearHasBearing)
        {
            Vector3 heading=Vector3.ProjectOnPlane(_rotation*Vector3.forward,Vector3.up);
            if(adapter is IGameMonitorSubjectHeadingAdapter subject
                && subject.TryGetMonitorSubjectHeading(out var actual) && Finite(actual)
                && Vector3.ProjectOnPlane(actual,Vector3.up).sqrMagnitude>.001f)
                heading=Vector3.ProjectOnPlane(actual,Vector3.up);
            _rearBearing=heading.sqrMagnitude>.001f ? heading.normalized : Vector3.forward;
            _rearHasBearing=true;
        }
        // Like a trailing camera operator, preserve the established shot when the
        // person stops or only looks around. Follow real travel, not every eye yaw.
        Vector3 travel=Vector3.ProjectOnPlane(_focusVelocity,Vector3.up);
        if(_pursuitObservedSpeed<=.65f || travel.sqrMagnitude<.64f) return;
        float turn=4f+2f*Mathf.InverseLerp(6f,24f,_pursuitPlanarSpeed);
        _rearBearing=Vector3.RotateTowards(_rearBearing,travel.normalized,turn*dt,0).normalized;
    }

    private bool TryRearTrailGoal(Vector3 focus,float now,float radius,out Vector3 goal,out float age)
    {
        goal=default; age=0;
        if(_pursuitTrailCount<2 || _pursuitFalling) return false;
        bool paused=_pursuitObservedSpeed<=.65f;
        Vector3 heading=paused ? _rearBearing : _pursuitTrailHeading;
        if(heading.sqrMagnitude<.5f) return false;
        float arc=_pursuitArc-radius;
        float maxAge=Mathf.Clamp(radius/Mathf.Max(.8f,_pursuitPlanarSpeed)+.15f,.6f,2.5f);
        float referenceTime=paused ? PursuitTrail(_pursuitTrailCount-1).Time : now;
        for(int i=_pursuitTrailCount-2;i>=0;i--)
        {
            var a=PursuitTrail(i); var b=PursuitTrail(i+1);
            if(a.Arc>arc) continue;
            if(b.Arc<arc || referenceTime-a.Time>maxAge) return false;
            float t=Mathf.InverseLerp(a.Arc,b.Arc,arc);
            goal=Vector3.Lerp(a.Focus,b.Focus,t);
            age=referenceTime-Mathf.Lerp(a.Time,b.Time,t);
            // Sub-sample motion is still part of the live stop. Without this
            // remainder, the last 12cm quantization could make the rear goal recede
            // continuously while the radius is clamped to the held camera distance.
            if(paused) goal+=focus-PursuitTrail(_pursuitTrailCount-1).Focus;
            Vector3 offset=Vector3.ProjectOnPlane(goal-focus,Vector3.up);
            // Returns and tight folds must not send the rig along the player's old
            // outward path. Rejoin the live rear target instead of completing a lap.
            return offset.sqrMagnitude<=radius*radius+1f && Vector3.Dot(offset,heading)<=.25f;
        }
        return false;
    }

    private bool TryRearGroundHeight(float now,out float height)
    {
        height=0;
        _rearGroundRate=_rearGroundSlopeRate=0;
        if(_pursuitFalling || _pursuitTrailCount<2) return false;
        // The camera can lag behind its ideal rear rig during acceleration. Its
        // local floor belongs to the actual position, not the newer rig endpoint.
        // Only recent grounded visits in the camera's height band may suggest a
        // local floor. Optional physical probes can confirm this soft reference.
        bool paused=_pursuitObservedSpeed<=.65f;
        float referenceTime=paused ? PursuitTrail(_pursuitTrailCount-1).Time : now;
        // The initial rig may stand just behind the first recorded foot sample.
        // Retain that nearby same-height seed reference until the live trail reaches
        // it, rather than starting descent before its own horizontal rejoin.
        float nearest=Mathf.Max(2f,_pursuitStandOff+.5f);
        nearest*=nearest;
        bool found=false;
        int closest=-1;
        Vector3 travel=paused ? _rearBearing : _pursuitTrailHeading;
        for(int i=_pursuitTrailCount-2;i>=0;i--)
        {
            var a=PursuitTrail(i); var b=PursuitTrail(i+1);
            if(referenceTime-a.Time>2.5f) break;
            Vector3 leg=Vector3.ProjectOnPlane(b.Focus-a.Focus,Vector3.up);
            if(leg.sqrMagnitude<.0001f || Vector3.Dot(leg.normalized,travel)<-.1f) continue;
            float t=Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(_position-a.Focus,Vector3.up),leg)/leg.sqrMagnitude);
            Vector3 visit=Vector3.Lerp(a.Focus,b.Focus,t);
            float candidate=visit.y-1.05f;
            if(candidate>_position.y+.3f || _position.y-candidate>4f) continue;
            float gap=Vector3.ProjectOnPlane(visit-_position,Vector3.up).sqrMagnitude;
            if(gap>=nearest) continue;
            nearest=gap; height=candidate; found=true; closest=i;
        }
        if(found)
        {
            var a=PursuitTrail(closest); var b=PursuitTrail(closest+1);
            Vector3 leg=Vector3.ProjectOnPlane(b.Focus-a.Focus,Vector3.up);
            // The local support level descends as the camera advances on a slope.
            // Brake relative to that moving reference, rather than treating every
            // stair sample as a stationary floor requiring a full vertical stop.
            {
                // Smooth discrete footfalls using the nearby real visits. The
                // Incoming real visits can anticipate a rising slope; descent
                // braking changes only once the camera joins that support segment.
                // Use travelled metres rather than a fixed number of frames: the
                // same stair transition must have the same preview at 30 and 144 FPS.
                int first=closest,last=closest+1;
                float behind=0,ahead=0;
                while(first>0 && behind<.6f)
                { behind+=Vector3.ProjectOnPlane(PursuitTrail(first).Focus-PursuitTrail(first-1).Focus,Vector3.up).magnitude; first--; }
                while(last+1<_pursuitTrailCount && ahead<3.6f)
                { ahead+=Vector3.ProjectOnPlane(PursuitTrail(last+1).Focus-PursuitTrail(last).Focus,Vector3.up).magnitude; last++; }
                Vector3 span=PursuitTrail(last).Focus-PursuitTrail(first).Focus;
                Vector3 flat=Vector3.ProjectOnPlane(span,Vector3.up);
                if(flat.sqrMagnitude>.04f)
                {
                    _rearGroundSlopeRate=Mathf.Clamp(Vector3.Dot(Vector3.ProjectOnPlane(_motionVelocity,Vector3.up),flat)
                        *span.y/flat.sqrMagnitude,-72f,72f);
                    if(Vector3.Dot(Vector3.ProjectOnPlane(_position-a.Focus,Vector3.up),leg)>=0
                        && PursuitTrail(last).Focus.y<a.Focus.y-.025f)
                        _rearGroundRate=Mathf.Min(0,_rearGroundSlopeRate);
                }
            }
        }
        return found;
    }
}
