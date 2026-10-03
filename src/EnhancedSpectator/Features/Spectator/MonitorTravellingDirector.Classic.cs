using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

internal sealed partial class MonitorTravellingDirector
{
    private bool _classicObservedRoute,_classicGrounded;
    private float _classicNextRoute,_classicSlopeUntil,_classicPolicyBlend,_classicRouteArc,_classicBlendAcceleration=160f;
    private Vector3 _classicFacingGoal;
    private float _classicFacingUntil,_classicFacingCooldown,_classicFacingSide=1f;
    private bool _classicHasElevation,_classicHasGroundFocus,_classicJumping;
    private float _classicFocusY,_classicGroundFocusY,_classicStandOff=1.5f;
    private bool _classicExtreme,_classicSightRecovery;
    private Vector3 _classicStopAnchor;
    private float _classicStoppedFor;
    internal const float ClassicExtremeDistance=70f,ClassicExtremeSpeed=240f,ClassicStopDelay=2f;
    private bool ClassicReferencePending => !HasRoute && (_contactTime>.1f || _permissiveRoute);

    private void ResumeClassicRoute(Vector3 focus)
    {
        Vector3 position=_position,velocity=_motionVelocity; Quaternion rotation=_rotation;
        Clear(); Seed(position,rotation,false,default,focus); _routePolicy=true;
        _motionVelocity=_handoffVelocity=velocity; _speed=velocity.magnitude;
        _travelDirection=velocity.normalized; _handoffRemaining=_speed/160f+.05f;
        _needsRoute=true;
    }

    private bool UpdateClassicExtreme(Vector3 focus,float now,float dt)
    {
        if(!_ready || !_classicExtreme && (focus-_position).sqrMagnitude<ClassicExtremeDistance*ClassicExtremeDistance) return false;
        if(!_classicExtreme) { ResumeClassicRoute(focus); _classicExtreme=true; }
        Vector3 goal=focus-(focus-_position).normalized*4.5f+Vector3.up*1.2f;
        float left=dt;
        while(left>.00001f)
        {
            float step=Mathf.Min(left,1f/120f); left-=step;
            Vector3 delta=goal-_position;
            float speed=Mathf.Min(ClassicExtremeSpeed,Mathf.Sqrt(960f*Mathf.Max(0,delta.magnitude-1f)));
            _motionVelocity=Vector3.MoveTowards(_motionVelocity,delta.normalized*speed,480f*step);
            _position+=_motionVelocity*step;
        }
        _speed=_motionVelocity.magnitude; _travelDirection=_motionVelocity.normalized;
        _lastMoveTime=now; State=MonitorTrackingState.Tracking;
        if(DiagnosticsEnabled) _lastMotion="extreme-direct";
        Aim(focus,dt);
        // Hysteresis and a braking envelope bring speed below the ordinary limit
        // before restarting the route. Neither transition consults geometry.
        if((focus-_position).sqrMagnitude<64f && _speed<PursuitSpeedLimit) ResumeClassicRoute(focus);
        return true;
    }

    private void SampleClassicStop(Vector3 focus,float dt)
    {
        if(!_hasDropFocus || (focus-_classicStopAnchor).sqrMagnitude>.04f)
        { _classicStopAnchor=focus; _classicStoppedFor=0; }
        else _classicStoppedFor+=dt;
        if(_classicSightRecovery && _classicStoppedFor<ClassicStopDelay)
        { _classicSightRecovery=false; _searching=false; }
    }

    private Vector3 ClassicCompositionFocus(Vector3 actual,bool grounded,float dt)
    {
        if(!_classicHasElevation) { _classicFocusY=actual.y; _classicHasElevation=true; }
        if(grounded)
        {
            _classicJumping=false;
            _classicGroundFocusY=actual.y; _classicHasGroundFocus=true;
            float error=actual.y-_classicFocusY;
            float speed=_hasFocus && dt>0 ? Vector3.ProjectOnPlane(actual-_lastFocus,Vector3.up).magnitude/dt : 0;
            if(_dropFollowing || speed>12f || Mathf.Abs(error)>1f) _classicFocusY=actual.y;
            // Remote feet can still count as grounded during the first .25m of a hop.
            else if(Mathf.Abs(error)>.3f)
                _classicFocusY=Mathf.Lerp(_classicFocusY,actual.y-Mathf.Sign(error)*.12f,1-Mathf.Exp(-dt/.15f));
        }
        else
        {
            if(_classicHasGroundFocus && actual.y>_classicGroundFocusY+.15f) _classicJumping=true;
            if(!_classicHasGroundFocus || actual.y<_classicGroundFocusY-.2f) _classicFocusY=actual.y;
        }
        actual.y=_classicFocusY;
        return actual;
    }

    private void SelectClassicFraming(IGameMonitorCameraAdapter adapter,IGameMonitorRouteAdapter? world,Vector3 focus)
    {
        Vector3 side=Vector3.ProjectOnPlane(_position-focus,Vector3.up).normalized;
        Vector3 lateral=Vector3.Cross(Vector3.up,side)*2.5f;
        bool narrow=lateral.sqrMagnitude>1f && !ClearPath(adapter,world,focus,focus+lateral)
            && !ClearPath(adapter,world,focus,focus-lateral)
            && (ClearPath(adapter,world,focus,focus+side*4.5f)
                || ClearPath(adapter,world,focus,focus-side*4.5f));
        if(narrow && _classicStandOff<3f && (_position-focus).sqrMagnitude<9f) _needsRoute=true;
        _classicStandOff=narrow ? 3f : 1.5f;
    }

    private bool UpdateClassicFacingPass(IGameMonitorCameraAdapter adapter,Vector3 focus,float now,float dt)
    {
        if(!_ready || _singleMove || _previewPush || _aimGrace>0 || _dropFollowing || _classicObservedRoute) return false;
        if(_classicFacingUntil>0 && now>=_classicFacingUntil)
        {
            _classicFacingUntil=0;
            _handoffVelocity=_motionVelocity; _handoffRemaining=_speed/160f+.05f;
        }
        Vector3 velocity=Vector3.ProjectOnPlane(_focusVelocity,Vector3.up);
        float speed=velocity.magnitude;
        if(now>=_classicFacingCooldown && speed>1.5f && speed<12f
            && Mathf.Abs(_focusVelocity.y)<.5f && Mathf.Abs(focus.y-_position.y)<3f)
        {
            Vector3 offset=Vector3.ProjectOnPlane(_position-focus,Vector3.up);
            float time=Vector3.Dot(offset,velocity)/(speed*speed);
            Vector3 closest=offset-velocity*time;
            if(time>.05f && time<1f && closest.sqrMagnitude<2.25f)
            {
                Vector3 side=Vector3.Cross(Vector3.up,velocity/speed);
                float signed=Vector3.Dot(closest,side);
                if(Mathf.Abs(signed)>.15f) _classicFacingSide=Mathf.Sign(signed);
                Vector3 goal=_position+side*_classicFacingSide*(2.2f-Mathf.Abs(signed));
                var world=adapter as IGameMonitorRouteAdapter;
                _classicFacingCooldown=now+.15f;
                if(ClearPath(adapter,world,_position,goal) && adapter.IsMonitorSightClear(goal,focus))
                {
                    // Acquire one lateral station before the subject crosses the lens.
                    // Do not orbit or chase a continuously moving offset around B.
                    _classicFacingGoal=goal; _classicFacingUntil=now+1.6f;
                    _classicFacingCooldown=now+.5f;
                    _route.Clear(); _cursor=_published=0; _planner.Clear(); _searching=false;
                    _brakingForReturn=false;
                }
            }
        }
        if(now>=_classicFacingUntil) return false;
        if(DiagnosticsEnabled) _lastMotion="head-on-station";
        float left=dt;
        while(left>.00001f)
        {
            float step=Mathf.Min(left,1f/120f); left-=step;
            Vector3 delta=_classicFacingGoal-_position;
            Vector3 wanted=Vector3.ClampMagnitude(delta*6f,5f);
            _motionVelocity=Vector3.MoveTowards(_motionVelocity,wanted,160f*step);
            _position+=_motionVelocity*step;
        }
        _speed=_motionVelocity.magnitude; _speedVelocity=_verticalCarrySpeed=0;
        if(_speed>.001f) _travelDirection=_motionVelocity/_speed;
        _station=_goal=_position; _goalFocus=focus;
        _needsRoute=false; _nextPlan=now+.25f;
        _stagnantTime=_blockedTime=0; _progressPosition=_position; _lastMoveTime=now;
        State=_speed>.08f ? MonitorTrackingState.Tracking : MonitorTrackingState.Holding;
        Aim(focus,dt);
        return true;
    }

    private void SampleClassicTrajectory(Vector3 focus,float now,float dt,bool grounded)
    {
        Vector3 velocity=_focusVelocity; float speed=_targetSpeed;
        SamplePursuit(focus,now,dt);
        _focusVelocity=velocity; _targetSpeed=speed;
        // A teleport resets the trail's arc. A visible return supersedes the old
        // outbound itinerary, using the same live rejoin policy as geometry routes.
        if(_pursuitArc<_classicRouteArc || _returningTime>.15f) _classicObservedRoute=false;
        bool pendingReference=ClassicReferencePending;
        bool useful=Mathf.Abs(velocity.y)>.35f || _pursuitObservedPlanarSpeed>12f || _classicObservedRoute || pendingReference;
        _classicGrounded=useful && grounded;
        if(_classicGrounded && (Mathf.Abs(velocity.y)>.5f || pendingReference)) _classicSlopeUntil=now+2.5f;
    }

    private bool UpdateClassicTrajectory(IGameMonitorCameraAdapter adapter,Vector3 focus,float now,bool roomReady,float catchUpDistance)
    {
        bool fast=_pursuitObservedPlanarSpeed>12f;
        if(!_classicGrounded && !fast && !_classicObservedRoute || _pursuitTrailCount<2 || !_ready || _returningTime>.15f) return false;
        bool moving=_pursuitObservedSpeed>.65f;
        if(!moving && _classicObservedRoute) return true;
        if((!moving || _pursuitObservedPlanarSpeed<=12f && now>_classicSlopeUntil) && !ClassicReferencePending)
        { _classicObservedRoute=false; return false; }
        if(now<_classicNextRoute && _classicObservedRoute && _cursor<_route.Count) return true;
        _classicNextRoute=now+.1f;
        if(_classicObservedRoute && _cursor<_route.Count && _route.Count<PursuitTrailCapacity)
        {
            // Advance one committed itinerary. Re-projecting to the nearest segment
            // at every refresh can pick a parallel passage or restart a stair bend.
            // Bound its history by the same capacity as the source trail.
            if(_cursor>0) { _segmentStart=_route[_cursor-1]; _route.RemoveRange(0,_cursor); _cursor=0; }
            for(int i=0;i<_pursuitTrailCount;i++)
                if(PursuitTrail(i).Arc>_classicRouteArc) _route.Add(PursuitTrail(i).Focus+Vector3.up*1.2f);
            _classicRouteArc=_pursuitArc; _goal=_route[_route.Count-1]; _goalFocus=focus;
            if(roomReady && adapter.TryGetMonitorRoom(out var owner)) _goalRoom=owner.Id;
            return true;
        }
        float nearest=float.PositiveInfinity; int join=-1; Vector3 joinPoint=default;
        for(int i=_pursuitTrailCount-2;i>=0;i--)
        {
            var sample=PursuitTrail(i); var next=PursuitTrail(i+1);
            // An unfinished stair/corner connection still needs its older visited
            // prefix. The bounded trail belongs to this traversal, not a 2.5s timer.
            Vector3 leg=next.Focus-sample.Focus;
            if(leg.sqrMagnitude<.001f) continue;
            Vector3 start=sample.Focus+Vector3.up*1.2f;
            float t=Vector3.Dot(_position-start,leg)/leg.sqrMagnitude;
            if(t>1f) continue;
            Vector3 point=Vector3.Lerp(sample.Focus,next.Focus,Mathf.Clamp01(t))+Vector3.up*1.2f;
            if(Mathf.Abs(point.y-_position.y)>4f) continue;
            float gap=(point-_position).sqrMagnitude;
            if(gap<nearest) { nearest=gap; join=t<0 ? i : i+1; joinPoint=point; }
        }
        bool distantFallback=(join<0 || nearest>100f) && fast && !roomReady
            && !IsTravelling && !_searching && _planner.Status!=MonitorRouteStatus.Computing
            && Mathf.Abs(focus.y+1.2f-_position.y)<=.35f
            && Vector3.ProjectOnPlane(focus-_position,Vector3.up).magnitude>Mathf.Clamp(catchUpDistance,6f,60f);
        if((join<0 || nearest>100f) && !distantFallback) return false;
        // Room lookup and geometry availability are independent. A missing target
        // tile must not authorize joining its trail through a floor or wall.
        if(!distantFallback && adapter is IGameMonitorRouteAdapter world
            && !world.IsMonitorTravelClear(_position,joinPoint)
            && (nearest>2.25f || Mathf.Abs(joinPoint.y-_position.y)>.75f))
        {
            // Retain the ordered prefix and velocity while geometry planning repairs
            // the connection; discarding it strands a rig already approaching a bend.
            _classicObservedRoute=false;
            _needsRoute=true;
            return false;
        }
        // A local join in the same recorded height band can leave a trim/hull
        // contact without waiting for geometry search. Remote or cross-storey
        // joins still need a clear connection; the visited prefix stays ordered.
        _route.Clear(); _cursor=_published=0; _planner.Clear(); _searching=false;
        _classicFacingUntil=0;
        _segmentStart=_position;
        if(distantFallback) _route.Add(focus-_pursuitTrailHeading*2.5f+Vector3.up*1.2f);
        else if((joinPoint-_position).sqrMagnitude>=.09f) _route.Add(joinPoint);
        for(int i=distantFallback ? _pursuitTrailCount : join;i<_pursuitTrailCount;i++)
        {
            Vector3 point=PursuitTrail(i).Focus+Vector3.up*1.2f;
            if(_route.Count==0 && (point-_position).sqrMagnitude<.09f) continue;
            _route.Add(point);
        }
        if(_route.Count==0) return false;
        _goal=_route[_route.Count-1]; _goalFocus=focus;
        if(adapter.TryGetMonitorRoom(out var room)) _goalRoom=room.Id;
        _needsRoute=false; _classicObservedRoute=true; _classicRouteArc=_pursuitArc;
        return true;
    }

    private void UpdateClassicRouteMotion(Vector3 focus,float now,float dt)
    {
        if(DiagnosticsEnabled) _lastMotion="observed-route-transit";
        bool moving=_pursuitObservedSpeed>.65f;
        float acceleration=_classicPolicyBlend>0 ? _classicBlendAcceleration : 160f;
        float left=dt;
        while(left>.00001f)
        {
            float step=Mathf.Min(left,1f/120f); left-=step;
            // Preview along the visited polyline, not a camera offset around B.
            // Interior samples keep progress; only the final shot may settle.
            while(_cursor<_route.Count-1 && Vector3.Dot(_position-_route[_cursor],
                _route[_cursor+1]-_route[_cursor])>=0) _cursor++;
            Vector3 guide=_position;
            float reach=Mathf.Max(1.5f,Vector3.ProjectOnPlane(_motionVelocity,Vector3.up).magnitude*.12f);
            for(int i=_cursor;i<_route.Count && reach>0;i++)
            {
                Vector3 leg=_route[i]-guide;
                float length=Vector3.ProjectOnPlane(leg,Vector3.up).magnitude;
                if(length<.001f) { guide=_route[i]; continue; }
                float advance=Mathf.Min(reach,length);
                guide+=leg*(advance/length); reach-=advance;
            }
            Vector3 forward=Vector3.ProjectOnPlane(guide-_position,Vector3.up).normalized;
            float gap=Vector3.ProjectOnPlane(focus-_position,Vector3.up).magnitude;
            // Descending stairs need a closer composition, while the measured
            // speed's full stopping distance remains reserved independently.
            float grade=-_focusVelocity.y/Mathf.Max(1f,_pursuitObservedPlanarSpeed);
            float framing=Mathf.Lerp(2.5f,1.5f,Mathf.InverseLerp(.2f,.6f,grade));
            float reserve=framing+_pursuitObservedPlanarSpeed*_pursuitObservedPlanarSpeed/320f;
            float remainingRoute=0; Vector3 previous=_position;
            for(int i=_cursor;i<_route.Count && remainingRoute<40f;i++)
            { remainingRoute+=Vector3.ProjectOnPlane(_route[i]-previous,Vector3.up).magnitude; previous=_route[i]; }
            float speed=moving ? Mathf.Min(PursuitSpeedLimit,_pursuitObservedPlanarSpeed+Mathf.Max(0,gap-reserve)*4f)
                : Mathf.Min(PursuitSpeedLimit,Mathf.Max(0,remainingRoute-framing)*4f);
            Vector3 wanted=forward*speed;
            wanted.y=moving || speed>.08f ? Mathf.Clamp((guide.y-_position.y)*10f,-72f,72f) : 0;
            if((moving || speed>.08f) && TryRearGroundHeight(now,out float floor))
            {
                // Preview the visited slope at the same route lookahead used by XZ.
                // A step under the current rig is too late to begin climbing at speed.
                float elevation=Mathf.Clamp(guide.y,floor+.85f,floor+(_focusVelocity.y<-.5f ? .85f : 4f));
                wanted.y=_rearGroundSlopeRate+(elevation-_position.y)*10f;
                if(DiagnosticsEnabled) _lastMotion=$"observed-route-transit:floor={floor:F2},rate={_rearGroundSlopeRate:F2}/{_rearGroundRate:F2},elevation={elevation:F2},velocity={_motionVelocity},guide={guide}";
            }
            // The support band belongs to this visited route segment. A target
            // already upstairs must not forbid finishing the lower stair prefix.
            wanted=ClassicTerrainCommand(wanted*step,guide-Vector3.up*1.2f,now,step)/step;
            wanted=Vector3.ClampMagnitude(wanted,PursuitSpeedLimit);
            // Height braking owns its bounded scalar budget; XZ receives what is
            // left of the vector budget, so a floor reference never clips the pose.
            float nextY=Mathf.MoveTowards(_motionVelocity.y,wanted.y,Mathf.Min(100f,acceleration)*step);
            float usedY=(nextY-_motionVelocity.y)/step;
            Vector3 flat=Vector3.MoveTowards(Vector3.ProjectOnPlane(_motionVelocity,Vector3.up),
                Vector3.ProjectOnPlane(wanted,Vector3.up),Mathf.Sqrt(Mathf.Max(0,acceleration*acceleration-usedY*usedY))*step);
            _motionVelocity=Vector3.ClampMagnitude(flat+Vector3.up*nextY,PursuitSpeedLimit);
            _position+=_motionVelocity*step;
        }
        _speed=_motionVelocity.magnitude; _speedVelocity=_verticalCarrySpeed=0; _station=_position;
        if(_speed>.001f) _travelDirection=_motionVelocity/_speed;
        if(!moving && _speed<.08f)
        {
            _route.Clear(); _cursor=_published=0; _classicObservedRoute=false;
            _station=_goal=_position; _goalFocus=focus; _settledBySubject=true;
            _needsRoute=false; _stationSince=now;
        }
        _stagnantTime=_blockedTime=0; _progressPosition=_position; _lastMoveTime=now;
        State=_speed>.08f ? MonitorTrackingState.Tracking : MonitorTrackingState.Holding;
        Aim(focus,dt);
    }

    private Vector3 ClassicTerrainCommand(Vector3 move,Vector3 focus,float now,float dt)
    {
        if(!_classicGrounded || dt<=.0001f || !TryRearGroundHeight(now,out float floor)) return move;
        // Real footfalls are discrete; keep hull and interpolation reserve before
        // the predicted slope can descend, including the first and final tread.
        float lower=floor+.8f+Mathf.Min(.2f,dt*Mathf.Abs(_rearGroundRate)*.25f);
        float down=Mathf.Sqrt(180f*Mathf.Max(0,_position.y-lower));
        float wanted=Mathf.Max(move.y/dt,_rearGroundRate-down);
        // The real subject's lower band also previews a landing. A descending
        // slope rate must not carry its vertical momentum through that flat floor.
        wanted=Mathf.Max(wanted,-Mathf.Sqrt(180f*Mathf.Max(0,_position.y-(focus.y-.4f))));
        move.y=wanted*dt;
        return move;
    }

    internal void ChangeTravellingPolicy(bool route,Vector3 focus)
    {
        Vector3 position=_position,velocity=_motionVelocity,subjectVelocity=_focusVelocity;
        Quaternion rotation=_rotation;
        float targetSpeed=_targetSpeed;
        Clear(); Seed(position,rotation,false,default,focus);
        _seedUnchecked=false; _routePolicy=route;
        _motionVelocity=_handoffVelocity=velocity;
        _speed=velocity.magnitude; _travelDirection=_speed>.001f ? velocity/_speed : Vector3.zero;
        _focusVelocity=subjectVelocity; _targetSpeed=targetSpeed;
        // A switch keeps physical momentum while the route is prepared. This is
        // independent of the cabin's intentionally limited exit seed velocity.
        _handoffRemaining=route ? _speed/160f+.05f : 0;
        _classicPolicyBlend=route ? .5f : 0;
        _needsRoute=route;
    }

    private bool UpdateTravellingDrop(IGameMonitorCameraAdapter adapter,Vector3 focus,Vector3 previous,
        float now,float dt,int tier,float catchUpDistance,bool grounded)
    {
        if(!_pursuitHasSample)
        {
            _pursuitFocus=previous; _pursuitHasSample=true;
            _pursuitFalling=true; _pursuitFallEvidence=.075f;
            _pursuitDropAxis=Vector3.ProjectOnPlane(focus-_position,Vector3.up).normalized;
            _subjectTrail.Clear();
        }
        // Real airborne motion temporarily supersedes a ground route. The tested
        // drop channel leaves the actual upper slab before descending; it does not
        // treat the stair path or its navigation height as a landing prediction.
        bool result=UpdateContinuousPursuit(adapter,focus,now,dt,tier,catchUpDistance,false,out _,out _);
        if(grounded && !_pursuitFalling && _dropSettled>.35f && Mathf.Abs(_motionVelocity.y)<.5f)
        {
            _dropFollowing=false; _dropEvidence=_dropSettled=0;
            ClearContinuousPursuit();
            _classicObservedRoute=_classicGrounded=false; _classicSlopeUntil=_verticalCarrySpeed=0;
            _subjectTrail.Clear(); _goalFocus=_lastFocus=focus; _hasFocus=true;
            _needsRoute=true; _nextPlan=now;
            _handoffVelocity=_motionVelocity; _handoffRemaining=_speed/160f+.05f;
        }
        return result;
    }
}
