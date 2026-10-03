using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

internal sealed partial class MonitorTravellingDirector
{
    private bool _seedUnchecked,_dropFollowing,_hasDropFocus,_dropJoinRequired;
    private float _dropEvidence,_dropSettled;
    private Vector3 _dropOffset,_dropMotion,_lastDropFocus,_dropFocusVelocity;
    internal const float PursuitSpeedLimit=72f;

    private static bool IndoorArrival(IGameMonitorCameraAdapter adapter,Vector3 focus,out Vector3 point,out Vector3 inward)
    {
        point=inward=default;
        bool found=adapter is IGameMonitorIndoorEntranceAdapter arrival
            ? arrival.TryGetMonitorIndoorEntrance(out point,out inward)
            : adapter is IGameMonitorEntranceAdapter entrance && entrance.TryGetMonitorMainEntrance(out point,out inward);
        Vector3 offset=Vector3.ProjectOnPlane(focus-point,Vector3.up);
        inward=Vector3.ProjectOnPlane(inward,Vector3.up).normalized;
        if(!found || offset.sqrMagnitude>64f || inward.sqrMagnitude<.5f) return false;
        // Door assets may face either way. The arrived subject establishes the interior side.
        if(Vector3.Dot(offset,inward)<0) inward=-inward;
        return true;
    }
    private static bool InitialStationAllowed(IGameMonitorCameraAdapter adapter,IGameMonitorRouteAdapter? world,
        Vector3 focus,Vector3 candidate,bool roomReady,MonitorRoom room)
    {
        if(IndoorArrival(adapter,focus,out var door,out var inward)
            && Vector3.Dot(candidate-door,inward)<.3f) return false;
        if(world!=null && roomReady && (!room.Bounds.Contains(room.WorldToLocal.MultiplyPoint3x4(candidate))
            || !world.TryGetRouteRoom(candidate,out var owner) || owner.Id!=room.Id)) return false;
        return true;
    }
    private void ValidateArrivalSeed(IGameMonitorCameraAdapter adapter,Vector3 focus)
    {
        if(!_seedUnchecked || _singleMove) return;
        _seedUnchecked=false;
        // A seed can legitimately belong to an earlier connected room. Only a nearby
        // actual entrance authorizes discarding it before the first rendered indoor shot.
        if(!IndoorArrival(adapter,focus,out var door,out var inward)
            || Vector3.Dot(_position-door,inward)>=.3f) return;
        _ready=false; _route.Clear(); _planner.Clear(); _cursor=_published=0;
        _searching=false; _needsRoute=true; _nextPlan=0;
        _speed=_speedVelocity=0; _travelDirection=Vector3.zero;
        if(DiagnosticsEnabled) _lastMotion="indoor-arrival-reacquire";
    }
    private bool UpdateObservedDrop(IGameMonitorCameraAdapter adapter,Vector3 focus,float now,float dt,int tier,float catchUpDistance,bool grounded)
    {
        // Follow smoothing can replace a nearby floor height before the subject lands.
        // Fall evidence and landing must come from the actual subject, not that filter.
        Vector3 previous=_hasDropFocus ? _lastDropFocus : focus,observed=focus-previous;
        _lastDropFocus=focus; _hasDropFocus=true;
        if(dt>.0001f) _dropFocusVelocity=Vector3.Lerp(_dropFocusVelocity,
            Vector3.ClampMagnitude(observed/dt,PursuitSpeedLimit),1-Mathf.Exp(-dt/.06f));
        return FollowObservedDrop(adapter,focus,previous,observed,now,dt,tier,catchUpDistance,grounded);
    }
    private bool FollowObservedDrop(IGameMonitorCameraAdapter adapter,Vector3 focus,Vector3 previousFocus,
        Vector3 observed,float now,float dt,int tier,float catchUpDistance,bool grounded)
    {
        if(_singleMove || !_ready) return false;
        if(dt<=.0001f) return _dropFollowing;
        // Route travelling must enter the tested drop channel at the same early
        // airborne evidence as tracking, before the original support band is lost.
        bool descent=observed.y/dt< (_routePolicy ? -1.5f : -4.5f)
            && observed.magnitude<=Mathf.Max(2f,96f*dt) && _dropFocusVelocity.y< (_routePolicy ? -1f : -3f);
        bool belowTakeoff=!_classicJumping || focus.y<_classicGroundFocusY-.2f;
        // A jump over a ledge loses its takeoff floor before crossing that height.
        // Only sample that local support band; an ordinary hop keeps its floor.
        if(!belowTakeoff && descent && !grounded && _pursuitHasGround && adapter is IGameMonitorGroundReferenceAdapter support)
            belowTakeoff=!support.TryGetMonitorGroundReference(focus,_classicGroundFocusY-1.05f,out _);
        bool falling=descent && !grounded && belowTakeoff;
        _dropEvidence=falling ? _dropEvidence+dt : Mathf.Max(0,_dropEvidence-dt*2);
        if(!_dropFollowing && _dropEvidence>=(_routePolicy ? .045f : .075f))
        {
            // A recorded jump supersedes the old ground itinerary. Preserve the current
            // pose/velocity, then follow the real descent; no timeout invents a floor chord.
            _dropFollowing=true; _dropSettled=0;
            _dropJoinRequired=_position.y>=focus.y-.3f;
            _dropOffset=Vector3.up*Mathf.Clamp((_position-previousFocus).y,1.2f,2f);
            _dropMotion=_motionVelocity;
            if(_routePolicy)
            {
                // Preserve the recently probed support while replacing stair samples
                // with the airborne channel. The player may already be below its band;
                // the next probe still confirms or invalidates the actual upper slab.
                bool hasGround=_pursuitHasGround;
                float groundHeight=_pursuitGroundHeight; Vector3 groundProbe=_pursuitGroundProbe;
                ClearContinuousPursuit();
                _pursuitHasGround=hasGround; _pursuitGroundHeight=groundHeight; _pursuitGroundProbe=groundProbe;
            }
            _planner.Clear(); _route.Clear(); _cursor=_published=0;
            _classicFacingUntil=0; _classicObservedRoute=false;
            _searching=false; _needsRoute=true; _planRetainsLeg=false;
            _brakingForReturn=false; _returningTime=0; _aimGrace=Mathf.Max(_aimGrace,.5f);
        }
        if(!_dropFollowing) return false;
        _dropSettled=observed.y/dt>=-1f ? _dropSettled+dt : 0;
        if(_routePolicy) return UpdateTravellingDrop(adapter,focus,previousFocus,now,dt,tier,catchUpDistance,grounded);
        Vector3 actualGoal=focus+_dropOffset,desired=actualGoal,springVelocity=_dropMotion;
        // Below a falling subject, begin the descent shortly before it passes this
        // height. Use a short real-motion prediction, bounded relative to the
        // subject's current pose, and remove it as soon as landing is observed.
        if(!_dropJoinRequired && !grounded) desired.y=Mathf.Max(focus.y-.5f,desired.y+Mathf.Min(0,observed.y/dt)*.12f);
        var world=adapter as IGameMonitorRouteAdapter;
        // Move toward the observed descent before crossing a neighbouring floor.
        // Once aligned with that real trajectory, a prop/reference hit cannot park
        // the camera above the jump or resurrect the superseded stair itinerary.
        bool holdHeight=_dropJoinRequired && world!=null && Vector3.ProjectOnPlane(_position-focus,Vector3.up).sqrMagnitude>.49f
            && !world.IsMonitorTravelClear(_position,desired);
        if(holdHeight) desired.y=Mathf.Max(_position.y,desired.y);
        // A camera still climbing below the player waits for the real descent to
        // reach its height; it must not first accelerate upstairs after the jump.
        if(_position.y<desired.y && _dropFocusVelocity.y< -1f) desired.y=_position.y;
        Vector3 eased=Vector3.SmoothDamp(_position,desired,ref springVelocity,.14f,PursuitSpeedLimit,dt);
        Vector3 wanted=(eased-_position)/dt,remaining=desired-_position;
        // The acceleration limit also needs braking room. A spring command alone
        // can reverse too late after a long fall and carry the camera below landing.
        wanted.x=DropAxisSpeed(wanted.x,remaining.x);
        wanted.y=DropAxisSpeed(wanted.y,remaining.y);
        wanted.z=DropAxisSpeed(wanted.z,remaining.z);
        _dropMotion=Vector3.MoveTowards(_dropMotion,Vector3.ClampMagnitude(wanted,PursuitSpeedLimit),100f*dt);
        _position+=_dropMotion*dt; _station=_position;
        _motionVelocity=_dropMotion;
        _speed=_dropMotion.magnitude; _speedVelocity=0;
        if(_speed>.001f) _travelDirection=_dropMotion/_speed;
        _stagnantTime=0; _progressPosition=_position; _lastMoveTime=now;
        State=MonitorTrackingState.Tracking;
        if(DiagnosticsEnabled) _lastMotion="observed-drop-follow";
        Aim(focus,dt);
        if(_dropSettled>.25f && !holdHeight && Mathf.Abs(_position.y-actualGoal.y)<.35f && Mathf.Abs(_dropMotion.y)<.5f)
        {
            _dropFollowing=false; _dropEvidence=_dropSettled=0;
            _nextPlan=now; _needsRoute=true; _goalFocus=focus;
            _handoffRemaining=.4f; _handoffVelocity=_dropMotion;
        }
        return true;
    }
    private static float DropAxisSpeed(float speed,float distance)
    {
        if(Mathf.Abs(distance)<.001f) return 0;
        if(speed*distance<=0) return speed;
        return Mathf.Sign(speed)*Mathf.Min(Mathf.Abs(speed),Mathf.Sqrt(160f*Mathf.Abs(distance)));
    }
    private bool RefreshMovingEndpoint(IGameMonitorRouteAdapter? world,Vector3 focus,bool roomReady,MonitorRoom room,bool sight)
    {
        if(_singleMove || world==null || !HasRoute || _cursor+1!=_route.Count || _searching
            || _planner.Status==MonitorRouteStatus.Computing || !sight || !roomReady || room.Id!=_goalRoom
            || Mathf.Abs(_position.y-focus.y)>2.5f
            || Vector3.Dot(_focusVelocity,(focus-_position).normalized)<.65f) return false;
        Vector3 side=Vector3.ProjectOnPlane(_position-focus,Vector3.up);
        if(side.sqrMagnitude<2.25f) return false;
        Vector3 goal=focus+side.normalized*_classicStandOff+Vector3.up*1.2f;
        if(!world.IsMonitorTravelClear(_position,goal) || !world.IsMonitorTravelClear(goal,focus)) return false;
        // Keep the current route and velocity. Only its verified final observation
        // point follows B; door/stair waypoints remain ordered and unchanged.
        _route[_cursor]=_goal=goal; _goalFocus=focus;
        return true;
    }
    private float BoundByUpcomingBends(float speed,float maxSpeed,float acceleration,float catchUp)
    {
        float distance=(_route[_cursor]-_position).magnitude;
        for(int i=_cursor;i+1<_route.Count && i<_cursor+7 && distance<40f;i++)
        {
            Vector3 incoming=_route[i]-(i==_cursor ? _segmentStart : _route[i-1]),outgoing=_route[i+1]-_route[i];
            if(incoming.sqrMagnitude>.0025f && outgoing.sqrMagnitude>.0025f
                && Mathf.Abs(incoming.normalized.y)<.35f && Mathf.Abs(outgoing.normalized.y)<.35f)
            {
                float alignment=Vector3.Dot(incoming.normalized,outgoing.normalized);
                if(alignment<.8f)
                {
                    float spacious=Mathf.InverseLerp(6f,12f,Mathf.Max(incoming.magnitude,outgoing.magnitude));
                    float through=alignment>-.5f
                        ? Mathf.Lerp(Mathf.Min(5f,maxSpeed*.6f),Mathf.Min(9f,maxSpeed*.8f),catchUp*spacious) : 0;
                    speed=Mathf.Min(speed,Mathf.Sqrt(through*through+2*acceleration*distance));
                }
            }
            distance+=outgoing.magnitude;
        }
        return speed;
    }
}
