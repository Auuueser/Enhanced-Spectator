using System.Collections.Generic;
using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

internal enum MonitorPushPreparation { None, Planning, Ready, Rejected }

/// <summary>Route travelling, continuous tracking and preflighted pushes share pose ownership.</summary>
internal sealed partial class MonitorTravellingDirector
{
    private bool _previewPush, _singleMove, _routePolicy;
    internal MonitorPushPreparation Preparation { get; private set; }
    internal bool PushSettled => _singleMove && !_previewPush && !_searching && !IsTravelling && _landingStable>=.35f && _contactTime<.25f && _planner.Status!=MonitorRouteStatus.Computing;
    internal void PreparePush(Vector3 position,Quaternion rotation,Vector3 focus)
    {
        Clear(); Seed(position,rotation,false,default,focus);
        _previewPush=_singleMove=_needsRoute=true; Preparation=MonitorPushPreparation.Planning;
    }
    internal void CommitPush() { _previewPush=false; Preparation=MonitorPushPreparation.None; _nextPlan=0; }
    private bool ComfortablePush(Vector3 focus)
    {
        Vector3 previous=_position,origin=_position-focus; origin.y=0;
        float length=0;
        for(int i=_cursor;i<_route.Count;i++)
        {
            Vector3 next=_route[i]; length+=(next-previous).magnitude;
            for(int j=0;j<=8;j++)
            {
                Vector3 offset=Vector3.Lerp(previous,next,j/8f)-focus,flat=offset; flat.y=0;
                if(flat.magnitude<2.5f || Mathf.Abs(offset.y)>flat.magnitude*.85f || Vector3.Angle(origin,flat)>65f) return false;
            }
            previous=next;
        }
        return length>=1.75f && length<=10f;
    }
    private readonly MonitorSubjectPacing _pacing=new MonitorSubjectPacing();
    private readonly MonitorRoutePlanner _planner=new MonitorRoutePlanner();
    private readonly List<MonitorPortal> _nearbyPortals=new List<MonitorPortal>();
    private readonly List<Vector3> _route=new List<Vector3>(256);
    private readonly List<Vector3> _subjectTrail=new List<Vector3>(512);
    private Vector3 _planAnchor;
    private bool _planRetainsLeg;
    private Vector3 _progressPosition;
    private float _stagnantTime;
    private bool _permissiveRoute;
    private Vector3 _fallbackGoal;
    private Vector3 _segmentStart;
    private int _recoveries;
    private bool _ready,_hasFocus,_searching,_needsRoute,_settledBySubject;
    private Vector3 _position,_station,_lastFocus,_focusVelocity,_goalFocus,_best,_goal,_travelDirection;
    private Quaternion _rotation=Quaternion.identity;
    private float _nextPlan,_bestScore,_blockedTime,_stationSince,_speed,_speedVelocity;
    private int _goalRoom,_probe,_cursor,_published;
    private MonitorRoom _searchRoom;
    private Vector3 _searchFocus;
    private bool _diagnosticRoomReady,_diagnosticSight,_diagnosticNeed;
    private int _diagnosticRoom;
    private float _planStarted,_lastMoveTime;
    private float _aimGrace, _nextGeometryRecovery;
    private float _contactTime,_landingStable,_nextLandingRepair;
    private Vector3 _handoffVelocity,_motionVelocity;
    private float _handoffRemaining;
    private float _verticalCarrySpeed;
    private string _lastPlan="none";
    private string _lastMotion="none";
    private bool _lastWaiting;
    private float _targetSpeed,_nextLiveJoin,_returningTime,_turnSign=1;
    private bool _brakingForReturn;
    internal bool DiagnosticsEnabled { get; set; }
    internal string Describe(float now) => $"travel push={_singleMove}/{Preparation},room={_diagnosticRoom}/{_diagnosticRoomReady},sight={_diagnosticSight},need={_diagnosticNeed},search={_searching}:{_probe},route={_cursor}/{_route.Count},published={_published},speed={_speed:F2},subjectPlanar={_pursuitObservedPlanarSpeed:F2},geometryPassage={_permissiveRoute},waiting={_pacing.Waiting},contact={_contactTime:F2},landing={_landingStable:F2},motion={_lastMotion},blocked={_blockedTime:F2},stagnant={_stagnantTime:F2},recoveries={_recoveries},sinceMove={Mathf.Max(0,now-_lastMoveTime):F2},planAge={(_planStarted>0?now-_planStarted:0):F2},goalRoom={_goalRoom},goal={_goal:F2},goalFocus={_goalFocus:F2},plan=[{_planner.Describe()}],lastPlan=[{_lastPlan}]";
    private bool HasRoute => _cursor<_route.Count;
    internal bool IsTravelling => _singleMove || _routePolicy ? _classicExtreme || HasRoute || (_dropFollowing || _classicFacingUntil>0) && _speed>.08f : _ready && _speed>.08f;
    internal string DescribeLeg() => _singleMove || _routePolicy
        ? $"position={_position:F3},heading={_travelDirection:F3},segment={_segmentStart:F3},next={(_cursor<_route.Count ? _route[_cursor] : _position):F3}"
        : $"position={_position:F3},velocity={_motionVelocity:F3},subject={_pursuitFocus:F3},goal={_pursuitGoal:F3},reference={_pursuitReference},heightBias={_pursuitHeightBias:F3}";
    internal MonitorTrackingState State { get; private set; }
    internal int ShotChanges { get; private set; }
    internal void Clear()
    {
        _previewPush=_singleMove=_routePolicy=false; Preparation=MonitorPushPreparation.None;
        _pacing.Clear(); _permissiveRoute=false; _aimGrace=_handoffRemaining=_verticalCarrySpeed=0; _handoffVelocity=_motionVelocity=Vector3.zero;
        _ready=_hasFocus=_searching=_needsRoute=_settledBySubject=false; _nextPlan=_blockedTime=0; _probe=_cursor=0;
        _route.Clear(); _planner.Clear(); _speed=0; _published=0; _focusVelocity=Vector3.zero; ShotChanges=0;
        _subjectTrail.Clear(); _planRetainsLeg=false;
        State=MonitorTrackingState.Searching;
        _lastMoveTime=_planStarted=0; _lastPlan="none";
        _lastMotion="none"; _lastWaiting=false;
        _stagnantTime=0; _recoveries=0;
        _travelDirection=Vector3.zero; _speedVelocity=0;
        _contactTime=_landingStable=_nextLandingRepair=0;
        _targetSpeed=_nextLiveJoin=_returningTime=0;
        _turnSign=1;
        _brakingForReturn=false;
        _classicObservedRoute=_classicGrounded=false; _classicNextRoute=_classicSlopeUntil=_classicPolicyBlend=_classicRouteArc=0;
        _classicBlendAcceleration=160f;
        _classicFacingGoal=Vector3.zero; _classicFacingUntil=_classicFacingCooldown=0; _classicFacingSide=1f;
        _classicHasElevation=_classicHasGroundFocus=_classicJumping=false; _classicFocusY=_classicGroundFocusY=0; _classicStandOff=1.5f;
        _classicExtreme=_classicSightRecovery=false; _classicStopAnchor=Vector3.zero; _classicStoppedFor=0;
        _seedUnchecked=_dropFollowing=_hasDropFocus=_dropJoinRequired=false; _dropEvidence=_dropSettled=0;
        _dropOffset=_dropMotion=_dropFocusVelocity=Vector3.zero;
        ClearContinuousPursuit();
    }
    internal Vector3 Velocity => _motionVelocity;
    internal void Seed(Vector3 position,Quaternion rotation,bool softenAim=false,Vector3 velocity=default,Vector3? focus=null,bool preserveVelocity=false)
    {
        _position=_station=_progressPosition=position; _rotation=rotation; _ready=true; _stagnantTime=0;
        _aimGrace=softenAim?1.2f:0;
        _handoffRemaining=softenAim?1.2f:0; _handoffVelocity=preserveVelocity ? velocity : Vector3.ClampMagnitude(velocity,10f);
        _motionVelocity=_handoffVelocity;
        _verticalCarrySpeed=0;
        // Cabin departure keeps its existing gentle acceleration envelope before
        // normal route pursuit takes over; manual style switches have a separate blend.
        _classicPolicyBlend=softenAim ? 1.5f : 0; _classicBlendAcceleration=softenAim ? 80f : 160f;
        _seedUnchecked=true;
        _speed=_handoffVelocity.magnitude; _travelDirection=_speed>.01f?_handoffVelocity/_speed:Vector3.zero;
        if(focus.HasValue) { _lastFocus=focus.Value; _hasFocus=true; }
    }
    internal bool TryUpdate(IGameMonitorCameraAdapter adapter,Vector3 focus,float now,float dt,int tier,out Vector3 position,out Quaternion rotation,float catchUpDistance=18f,bool followBehind=false)
    {
        _routePolicy=_singleMove || _previewPush;
        if(_routePolicy) return UpdateRouteTravelling(adapter,focus,now,dt,tier,out position,out rotation,catchUpDistance);
        if(!adapter.IsMonitorTargetIndoors) { Clear(); position=_position; rotation=_rotation; return false; }
        return UpdateContinuousPursuit(adapter,focus,now,dt,tier,catchUpDistance,followBehind,out position,out rotation);
    }
    internal bool TryUpdateTravelling(IGameMonitorCameraAdapter adapter,Vector3 focus,float now,float dt,int tier,out Vector3 position,out Quaternion rotation,float catchUpDistance=18f)
    {
        _routePolicy=true;
        return UpdateRouteTravelling(adapter,focus,now,dt,tier,out position,out rotation,catchUpDistance);
    }
    private bool UpdateRouteTravelling(IGameMonitorCameraAdapter adapter,Vector3 focus,float now,float dt,int tier,out Vector3 position,out Quaternion rotation,float catchUpDistance)
    {
        if(!_singleMove && !_previewPush && adapter is IGameMonitorSubjectMotionAdapter motion
            && motion.TryGetMonitorSubjectFocus(out var actual) && Finite(actual)) focus=actual;
        position=_position; rotation=_rotation;
        if(_lastMoveTime<=0) _lastMoveTime=now;
        if(!adapter.IsMonitorTargetIndoors) { Clear(); return false; }
        var world=adapter as IGameMonitorRouteAdapter;
        if(_previewPush && world==null) { Preparation=MonitorPushPreparation.Rejected; return _ready; }
        dt=Mathf.Clamp(dt,0,.1f);
        // A paused frame preserves the current motion owner and pose. Preflight
        // planning remains independent of simulation time.
        if(dt<=0 && _hasDropFocus && !_singleMove && !_previewPush) return _ready;
        Vector3 rawFocus=focus;
        if(!_singleMove && !_previewPush && UpdateClassicExtreme(rawFocus,now,dt))
        { position=_position; rotation=_rotation; return true; }
        bool grounded=!_singleMove && !_previewPush && adapter is IGameMonitorTraversalAdapter feet && feet.IsMonitorSubjectGrounded;
        if(!_singleMove && !_previewPush)
        { focus=ClassicCompositionFocus(rawFocus,grounded,dt); SampleClassicStop(rawFocus,dt); }
        _classicPolicyBlend=Mathf.Max(0,_classicPolicyBlend-dt);
        if(_subjectTrail.Count>0 && (focus-_subjectTrail[_subjectTrail.Count-1]).sqrMagnitude>64f) _subjectTrail.Clear();
        if(_subjectTrail.Count==0 || (focus-_subjectTrail[_subjectTrail.Count-1]).sqrMagnitude>=.16f)
        { if(_subjectTrail.Count==512) _subjectTrail.RemoveAt(0); _subjectTrail.Add(focus); }
        _handoffRemaining=Mathf.Max(0,_handoffRemaining-dt);
        bool roomReady=adapter.TryGetMonitorRoom(out var room);
        ValidateArrivalSeed(adapter,focus);
        if(!_ready && !_singleMove && !_previewPush) AcquirePursuitPose(adapter,focus,roomReady,room,now);
        _pacing.Sample(focus,dt);
        if(_hasFocus && dt>.0001f)
        {
            Vector3 travel=focus-_lastFocus;
            // Scalar speed survives a corner or reversal; vector cancellation must not
            // masquerade as the runner stopping. Teleports are not pursuit acceleration.
            float sampleLimit=_singleMove ? 32f : PursuitSpeedLimit;
            float sampled=travel.magnitude>(_singleMove ? 2f : Mathf.Max(2f,96f*dt)) ? 0 : Mathf.Min(sampleLimit,travel.magnitude/dt);
            _targetSpeed=Mathf.Lerp(_targetSpeed,sampled,1-Mathf.Exp(-dt/(sampled>_targetSpeed?.12f:.6f)));
            _focusVelocity=Vector3.Lerp(_focusVelocity,Vector3.ClampMagnitude(travel/dt,sampleLimit),1-Mathf.Exp(-dt/.12f));
        }
        _lastFocus=focus; _hasFocus=true;
        bool rawElevationMoving=dt>0 && _hasDropFocus && Mathf.Abs(rawFocus.y-_lastDropFocus.y)/dt>.35f;
        if(!_singleMove && !_previewPush) SamplePursuitGround(adapter,now,rawElevationMoving || _dropFollowing);
        if(UpdateObservedDrop(adapter,rawFocus,now,dt,tier,catchUpDistance,grounded))
        { position=_position; rotation=_rotation; return true; }
        bool sight=_ready && adapter.IsMonitorSightClear(_position,focus);
        Vector3 planarFrame=_position-focus; planarFrame.y=0;
        bool readableHeight=Mathf.Abs(_position.y-focus.y)<=planarFrame.magnitude*.85f;
        bool recoverSight=!_singleMove && !_previewPush && _classicStoppedFor>=ClassicStopDelay && !sight;
        bool startSightRecovery=recoverSight && !_classicSightRecovery;
        if(startSightRecovery)
        {
            _classicSightRecovery=true; _classicObservedRoute=false; _classicFacingUntil=0; _searching=false; _planner.Clear(); _published=0;
            _needsRoute=true; _nextPlan=now;
        }
        // Complete the visible station, rather than parking at the first fleeting
        // line of sight at a corner. A moving subject cancels recovery in its sampler.
        if(sight && !_searching && !IsTravelling) _classicSightRecovery=false;
        bool readableReturn=sight && (_singleMove || readableHeight) && Vector3.Dot(_focusVelocity,(focus-_position).normalized)<-2f;
        if(readableReturn && !_singleMove) readableReturn=ClearPath(adapter,world,_position,focus);
        _returningTime=readableReturn
            ? _returningTime+dt : Mathf.Max(0,_returningTime-dt*2);
        // Motion observation continues during recovery, independently of which
        // policy owns translation. Resuming must not turn skipped samples into speed.
        if(!_singleMove && !_previewPush) SampleClassicTrajectory(grounded ? rawFocus : focus,now,dt,grounded);
        bool observedRoute=!_singleMove && !_previewPush && !_classicSightRecovery
            && UpdateClassicTrajectory(adapter,grounded ? rawFocus : focus,now,roomReady,catchUpDistance);
        if(observedRoute)
        {
            UpdateClassicRouteMotion(focus,now,dt);
            position=_position; rotation=_rotation; return true;
        }
        if(!_classicSightRecovery && UpdateClassicFacingPass(adapter,focus,now,dt))
        { position=_position; rotation=_rotation; return true; }
        Vector3 predicted=focus+Vector3.ClampMagnitude(_focusVelocity*.25f,1f);
        bool futureSight=_ready && adapter.IsMonitorSightClear(_position,predicted);
        if(!_singleMove && !_previewPush && _ready && world!=null && HasRoute && sight
            && _returningTime>.15f && now>=_nextLiveJoin && (focus-_goalFocus).sqrMagnitude>2.25f
            && (focus-_position).sqrMagnitude<196f)
        {
            Vector3 toward=focus-_position,leg=_route[_cursor]-_position;
            float remaining=leg.magnitude;
            for(int i=_cursor+1;i<_route.Count && remaining<30;i++) remaining+=(_route[i]-_route[i-1]).magnitude;
            if(Vector3.Dot(leg,toward)<0 || remaining>toward.magnitude+3f)
            {
                _nextLiveJoin=now+.2f;
                Vector3 side=_position-focus; side.y=0;
                Vector3 goal=focus+side.normalized*_classicStandOff+Vector3.up*1.2f;
                if(side.magnitude>2.5f && Mathf.Abs(toward.y)<2.5f
                    && world.IsMonitorTravelClear(_position,goal) && world.IsMonitorTravelClear(goal,focus))
                {
                    // A readable returning player supersedes its historical itinerary.
                    // Rebase on the current pose, keeping speed/heading and stopping rules.
                    JoinLiveTarget(goal,focus,roomReady?room.Id:0,now);
                }
            }
        }
        bool liveEndpoint=RefreshMovingEndpoint(world,focus,roomReady,room,sight);
        if(!_singleMove && !_previewPush && !IsTravelling && !_searching && now>=_nextPlan
            && Vector3.ProjectOnPlane(_position-focus,Vector3.up).sqrMagnitude<6.25f)
            SelectClassicFraming(adapter,world,focus);
        bool needs=_needsRoute || !_ready || !sight || !futureSight || (_position-predicted).sqrMagnitude>(_settledBySubject ? 225f : 49f)
            || !_singleMove && !readableHeight
            || (_position-focus).sqrMagnitude<2.25f
            || (_settledBySubject && (focus-_goalFocus).sqrMagnitude>1f);
        if(HasRoute) needs=startSightRecovery || (roomReady && room.Id!=_goalRoom) || (focus-_goalFocus).sqrMagnitude>2.25f
            || (_planner.Status==MonitorRouteStatus.Idle && !_searching && _published==0
                && (_route[_route.Count-1]-_goal).sqrMagnitude>.04f);
        _diagnosticRoomReady=roomReady; _diagnosticRoom=roomReady?room.Id:0; _diagnosticSight=sight; _diagnosticNeed=needs;
        if(!_previewPush && _planner.Status==MonitorRouteStatus.Computing && _published>1 && now-_planStarted>1.2f
            && (focus-_goalFocus).sqrMagnitude>9f)
        {
            // Retarget only after a usable prefix has been published. Restarting an expensive
            // first leg every 1.2 seconds also reset its geometry-recovery deadline, so a running
            // subject could starve both search and recovery indefinitely.
            _planner.Clear(); _nextPlan=now; _needsRoute=needs=true;
        }
        if((!_previewPush || Preparation==MonitorPushPreparation.Planning) && !_searching && _planner.Status!=MonitorRouteStatus.Computing && now>=_nextPlan
            && needs && (!_ready || !sight || IsTravelling || (_position-predicted).sqrMagnitude>121f || now-_stationSince>.6f))
        {
            if(!_singleMove && !_previewPush) SelectClassicFraming(adapter,world,focus);
            _searchRoom=room; _searchFocus=_classicSightRecovery ? rawFocus : focus;
            if(!_classicSightRecovery && world!=null && world.TryGetRouteRoom(predicted,out var ahead) && world.IsMonitorTravelClear(focus,predicted))
            { _searchRoom=ahead; _searchFocus=predicted; }
            _searching=true; _probe=0; _bestScore=float.NegativeInfinity;
        }
        if(_searching)
        {
            for(int budget=0;budget<4;budget++)
            {
                int index=_probe++;
                Vector3 candidate=_searchFocus+Quaternion.Euler(0,(index%8)*45f,0)*Vector3.back*4.5f
                    +Vector3.up*(index/8==1 ? .6f : index/8==2 ? 1.8f : 1.2f);
                if(!_ready && index>=16)
                    candidate=_searchFocus+Quaternion.Euler(0,(index%8)*45f,0)*Vector3.back*1.8f+Vector3.up*.6f;
                if(_classicSightRecovery)
                    candidate=_searchFocus+Quaternion.Euler(0,(index%8)*45f,0)*Vector3.back*3f
                        +Vector3.up*(index/8==1 ? .6f : index/8==2 ? 1.8f : 1.2f);
                else if(_ready)
                {
                    // B is the live subject, not a room corner. Nearby alternatives only resolve
                    // obstructed placement; the camera stops before B at a readable framing distance.
                    Vector3 side=_position-focus; side.y=0;
                    if(side.sqrMagnitude<.1f) { side=-(_rotation*Vector3.forward); side.y=0; }
                    if(side.sqrMagnitude<.1f) side=Vector3.back;
                    int slot=index%8;
                    float angle=slot==0 ? 0 : ((slot+1)/2)*25f*(slot%2==0 ? -1 : 1);
                    float radius=_classicStandOff>1.5f ? _classicStandOff
                        : _singleMove || (_position-focus).sqrMagnitude<9 || Mathf.Abs(_position.y-focus.y)>side.magnitude*.85f ? 4.5f : _classicStandOff;
                    candidate=_searchFocus+Quaternion.Euler(0,angle,0)*side.normalized*radius
                        +Vector3.up*(index/8==1 ? .6f : index/8==2 ? 1.8f : 1.2f);
                }
                if(index==0) _fallbackGoal=_ready ? candidate : _searchFocus+Vector3.up*.6f;
                if(adapter.TryPlaceMonitorCamera(_searchFocus,candidate,out candidate) && (world==null || world.IsMonitorTravelClear(candidate,candidate))
                    && (!_classicSightRecovery || adapter.IsMonitorSightClear(candidate,rawFocus)
                        && Vector3.ProjectOnPlane(candidate-rawFocus,Vector3.up).sqrMagnitude>6.25f)
                    && (_ready || InitialStationAllowed(adapter,world,_searchFocus,candidate,roomReady,_searchRoom)))
                {
                    float score=MonitorCameraRules.Score(candidate,_searchFocus,_position,_ready,index%8);
                    if(_ready) score=5f-Mathf.Abs((candidate-_searchFocus).magnitude-(_classicStandOff>1.5f ? 3.25f : 2f))
                        -ContinuityCost(_position,candidate,focus)*.65f-(candidate-_position).magnitude*.02f;
                    if(score>_bestScore) { _bestScore=score; _best=candidate; }
                }
                if(_probe%8!=0 || (_probe<24 && float.IsNegativeInfinity(_bestScore))) continue;
                _searching=false; _nextPlan=now+.25f;
                if(_classicSightRecovery && float.IsNegativeInfinity(_bestScore))
                { _nextPlan=now+.5f; break; }
                if(_previewPush && float.IsNegativeInfinity(_bestScore))
                { Preparation=MonitorPushPreparation.Rejected; _nextPlan=float.PositiveInfinity; break; }
                if(float.IsNegativeInfinity(_bestScore)) _best=_fallbackGoal;
                _goal=_best; _goalFocus=_searchFocus; _goalRoom=_searchRoom.Id;
                if(!_ready)
                {
                    Seed(_goal,Quaternion.LookRotation(focus-_goal,Vector3.up)); _stationSince=now; ShotChanges++;
                }
                else if(world!=null) BeginPlan(world,now);
                else { _route.Clear(); _route.Add(_goal); _cursor=0; _segmentStart=_position; }
                break;
            }
        }
        if(world!=null && _planner.Status==MonitorRouteStatus.Computing) _planner.Tick(world);
        if(_previewPush && (_planner.Status==MonitorRouteStatus.Blocked
            || (_planner.Status==MonitorRouteStatus.Computing && now-_planStarted>=.6f)))
        { Preparation=MonitorPushPreparation.Rejected; _planner.Clear(); _searching=false; _nextPlan=float.PositiveInfinity; }
        if(!_previewPush && (_planner.Status==MonitorRouteStatus.Blocked
            || (_planner.Status==MonitorRouteStatus.Computing && now>=_nextGeometryRecovery)))
        { _planner.AllowGeometryPassage(world!,_subjectTrail); _permissiveRoute=true; _recoveries++; _nextGeometryRecovery=now+1.2f; }
        if(world!=null && _planner.Points.Count>Mathf.Max(1,_published))
        {
            if(_published>0)
            {
                // Preserve the segment already in flight while the planner appends its suffix.
                for(int i=_published;i<_planner.Points.Count;i++) _route.Add(_planner.Points[i]);
                _published=_planner.Points.Count;
            }
            else
            {
                // Join a still-valid suffix from the moving camera; never rewind to the planning snapshot.
                bool retainLeg=_planRetainsLeg && HasRoute && (_route[_cursor]-_planAnchor).sqrMagnitude<.0025f;
                Vector3 anchor=retainLeg ? _route[_cursor] : _position;
                int join=-1,first=1;
                // Locate a consumed prefix with bounded arithmetic, not one physics query per point.
                while(first<64 && first+1<_planner.Points.Count && Vector3.Dot(anchor-_planner.Points[first],
                    _planner.Points[first]-_planner.Points[first-1])>=0
                    && (anchor-_planner.Points[first+1]).sqrMagnitude<=(anchor-_planner.Points[first]).sqrMagnitude) first++;
                for(int i=first;i<Mathf.Min(first+12,_planner.Points.Count);i++)
                    if((!retainLeg || (_planner.Points[i]-anchor).sqrMagnitude>.04f
                        && Vector3.Dot(_planner.Points[i]-anchor,anchor-_position)>=-.01f)
                        && world.IsMonitorTravelClear(anchor,_planner.Points[i]))
                    { join=i; break; }
                // A permitted route is not permission to jump to a later arbitrary waypoint.
                if(join<0 && _permissiveRoute && (anchor-_planner.Points[0]).sqrMagnitude<.04f) join=1;
                if(join>=0)
                {
                    _route.Clear(); if(retainLeg) _route.Add(anchor);
                    for(int i=join;i<_planner.Points.Count;i++) _route.Add(_planner.Points[i]);
                    // Installing a new route is not evidence that the camera actually moved.
                    _cursor=0; if(!retainLeg) _segmentStart=_position;
                    ShotChanges++; _published=_planner.Points.Count;
                    if(DiagnosticsEnabled) _lastMotion=retainLeg ? "route-suffix-preserved-leg" : "route-joined-current-pose";
                }
            }
        }
        if(_previewPush && _planner.Status==MonitorRouteStatus.Ready)
        {
            Preparation=ComfortablePush(focus) && ComfortablePush(predicted) ? MonitorPushPreparation.Ready : MonitorPushPreparation.Rejected;
            _nextPlan=float.PositiveInfinity;
        }
        if(_planner.Status==MonitorRouteStatus.Ready || _planner.Status==MonitorRouteStatus.Blocked)
        {
            if(DiagnosticsEnabled) _lastPlan=_planner.Describe()+$",joined={_published>0},age={now-_planStarted:F2}";
            _planner.Clear();
        }
        if(!_ready) return false;
        if(_previewPush) { Aim(focus,dt); position=_position; rotation=_rotation; return true; }
        // Do not park in a portal or in the previous room merely because a doorway reveals the player.
        bool waitingRoom=roomReady && (world==null ||
            (world.TryGetRouteRoom(_position,out var cameraRoom) && cameraRoom.Id==room.Id));
        if(waitingRoom && world!=null)
        {
            // Exclude actual door apertures, not every wall-side station in the room bounds.
            world.GetMonitorPortals(room.Id,_nearbyPortals);
            foreach(var portal in _nearbyPortals)
            {
                Vector3 offset=_position-portal.Center; offset.y=0;
                if(offset.sqrMagnitude<1.44f) { waitingRoom=false; break; }
            }
        }
        bool clearLanding=ClearPath(adapter,world,_position,_position);
        // Integrate contact evidence. A grazing overlap must not toggle waiting/acceleration each frame.
        _contactTime=Mathf.Clamp(_contactTime+(clearLanding ? -dt : dt),0,.3f);
        bool embedded=_contactTime>=.25f;
        float subjectDistance=(focus-_position).magnitude;
        // Seeing B through a slit or across a stairwell does not complete the
        // connected camera route. Only a readable frame may apply shot pacing.
        bool routeTransit=!_singleMove && HasRoute && _cursor+1<_route.Count
            && !(sight && readableHeight && waitingRoom && ClearPath(adapter,world,_position,focus));
        bool paceVisible=sight && (_singleMove || readableHeight && !routeTransit);
        float pace=_pacing.Advance(subjectDistance,paceVisible,waitingRoom && !embedded,dt);
        if(DiagnosticsEnabled && _lastWaiting!=_pacing.Waiting) _lastMotion=_pacing.Waiting ? "subject-wait" : "subject-resume";
        _lastWaiting=_pacing.Waiting;
        if(embedded && now>=_nextLandingRepair && (!HasRoute || (_route[_route.Count-1]-_position).sqrMagnitude<.36f))
        {
            _nextLandingRepair=now+.25f;
            // Continue the final leg out of an exceptional overlap, rather than handing an invalid
            // station back to the cut director. At most four endpoint queries per repair tick.
            Vector3 heading=HasRoute ? (_route[_route.Count-1]-_segmentStart).normalized : _travelDirection;
            bool repaired=false;
            for(int i=1;i<=4 && heading.sqrMagnitude>.5f;i++)
            {
                Vector3 candidate=_position+heading*(i*.3f),offset=candidate-focus; offset.y=0;
                if(offset.magnitude<2.5f || ContinuityCost(_position,candidate,focus)>.5f || !ClearPath(adapter,world,candidate,candidate)) continue;
                if(HasRoute) _route[_route.Count-1]=candidate;
                else { _route.Clear(); _route.Add(candidate); _cursor=0; _segmentStart=_position; }
                _goal=candidate; _landingStable=0; repaired=true; break;
            }
            if(!repaired) { _needsRoute=true; _nextPlan=Mathf.Min(_nextPlan,now); }
            if(DiagnosticsEnabled) _lastMotion=repaired ? "landing-extend-current-leg" : "landing-replan-preserve-pose";
        }
        if(HasRoute)
        {
            _verticalCarrySpeed=0;
            // Once a retained route intersects reference geometry, that geometry must
            // not turn its small transit samples into physical braking destinations.
            // Relax only a local camera-width corridor along the already ordered route.
            bool referenceContact=!_singleMove && (!clearLanding || _contactTime>0);
            // A doorway's small camera-height adjustment is not a staircase. Keep the
            // proven narrow level-turn behavior until the route actually changes floors.
            bool stairRoute=Mathf.Abs(_goal.y-_planAnchor.y)>1.5f;
            // Prefer volume-checked shortcuts. During reference contact only a small,
            // almost-straight corridor may bypass the probe, never the room itinerary.
            for(int i=Mathf.Min(_cursor+6,_route.Count-1);i>_cursor;i--)
                if((CollinearSuffix(i) || LocalTransitSuffix(i) || stairRoute && StairTransitSuffix(i))
                    && (ClearPath(adapter,world,_position,_route[i])
                        || referenceContact && ReferenceTransitChord(_route[i],i)))
                { _segmentStart=_position; _cursor=i; break; }
            Vector3 point=_route[_cursor], guide=point;
            float remainingRoute=(point-_position).magnitude;
            for(int i=_cursor+1;i<_route.Count && remainingRoute<40f;i++) remainingRoute+=(_route[i]-_route[i-1]).magnitude;
            float catchUp=Mathf.InverseLerp(7f,14f,Mathf.Max(subjectDistance,sight ? 0 : remainingRoute+4.5f));
            bool anticipating=false;
            if(_cursor+1<_route.Count)
            {
                Vector3 outgoing=_route[_cursor+1]-point;
                float lookAhead=Mathf.Lerp(.8f,2.4f,catchUp);
                float reach=lookAhead-(point-_position).magnitude;
                // Preview continuous stairs only after the same camera-hull check as a level bend.
                // Large vertical drops and opposing grades retain their stopping envelope.
                if((Mathf.Abs((point-_position).normalized.y)>.35f || Mathf.Abs(outgoing.normalized.y)>.35f)
                    && !(stairRoute && ContinuousGrade(point-_segmentStart,outgoing))
                    && !(referenceContact && ReferenceTransitGrade(point-_position,outgoing))) reach=0;
                // Advance the steering target along the ordered route BEFORE the bend.
                // Normal bends are hull-checked; contact recovery stays in the narrow
                // corridor of an existing almost-straight segment.
                for(int attempt=0;attempt<3 && reach>.08f;attempt++,reach*=.5f)
                {
                    Vector3 candidate=point;
                    float remaining=reach;
                    int last=_cursor;
                    for(int i=_cursor+1;i<Mathf.Min(_route.Count,_cursor+7) && remaining>0;i++)
                    {
                        Vector3 leg=_route[i]-candidate;
                        if(Mathf.Abs(leg.normalized.y)>.35f
                            && !(stairRoute && ContinuousGrade(candidate-_position,leg))
                            && !(referenceContact && ReferenceTransitGrade(candidate-_position,leg))) break;
                        float advance=Mathf.Min(remaining,leg.magnitude);
                        candidate+=leg.normalized*advance; remaining-=advance; last=i;
                    }
                    if((candidate-point).sqrMagnitude<.0064f) continue;
                    if(ClearPath(adapter,world,_position,candidate)
                        || referenceContact && ReferenceTransitChord(candidate,last))
                    { guide=candidate; anticipating=true; break; }
                }
            }
            Vector3 delta=guide-_position;
            float distance=delta.magnitude;
            float maxSpeed=12f*.6f/SpectatorFollowStabilizer.ResponseScale(tier), accel=40f;
            // Keep the existing tiers for ordinary players. Fast runners get bounded,
            // distance-dependent headroom so a fixed cap cannot leave the camera behind.
            float fastRunner=Mathf.Max(0,_targetSpeed-9f);
            // Short packed bends retain their proven approach speed. Applying the full
            // pursuit boost there overshoots the next guide and creates a return detour.
            float routeHeadroom=_cursor+1<_route.Count
                ? Mathf.InverseLerp(6f,12f,(point-_segmentStart).magnitude) : 1f;
            maxSpeed=Mathf.Min(PursuitSpeedLimit,maxSpeed+fastRunner*1.6f*routeHeadroom*Mathf.InverseLerp(5f,16f,subjectDistance));
            // Long open legs need a closing margin beyond the runner's own speed.
            // Keep ordinary movement and dense bends on their established envelope.
            if(!_singleMove)
            {
                float threshold=Mathf.Clamp(catchUpDistance,6f,60f);
                float excess=Mathf.Max(0,subjectDistance-threshold);
                float pursuit=Mathf.Max(maxSpeed,_targetSpeed+excess/1.5f);
                maxSpeed=Mathf.Lerp(maxSpeed,Mathf.Min(PursuitSpeedLimit,pursuit),
                    Mathf.InverseLerp(threshold,threshold+12f,subjectDistance)*routeHeadroom);
            }
            else maxSpeed=Mathf.Min(32f,maxSpeed);
            // A close subject in a small room does not need a full-speed rush between nearby stations.
            maxSpeed*=Mathf.Lerp(.4f,1f,Mathf.InverseLerp(2f,9f,(focus-_position).magnitude));
            // A moving observation point is not a stationary stop at its old sample.
            float endSpeed=liveEndpoint ? Mathf.Min(maxSpeed,Mathf.Max(0,Vector3.Dot(_focusVelocity,delta.normalized))) : 0;
            bool flowTurn=false;
            float transitSpeed=Mathf.Min(5f,maxSpeed*.6f);
            if(_cursor+1<_route.Count)
            {
                Vector3 next=(_route[_cursor+1]-point).normalized;
                Vector3 approach=(point-_segmentStart).normalized;
                flowTurn=Mathf.Abs(approach.y)<.35f && Mathf.Abs(next.y)<.35f
                    && Vector3.Dot(approach,next)>-.5f;
                flowTurn|=stairRoute && anticipating && ContinuousGrade(point-_segmentStart,_route[_cursor+1]-point);
                flowTurn|=referenceContact && anticipating && ReferenceTransitGrade(point-_segmentStart,_route[_cursor+1]-point);
                float alignment=Vector3.Dot(delta.normalized,next);
                endSpeed=alignment<.7f ? (alignment>.15f ? 1.2f : 0) : maxSpeed*Mathf.Pow(Mathf.Clamp01((alignment+1)*.5f),4f)*.8f;
                if(anticipating) endSpeed=Mathf.Max(endSpeed,Mathf.Min(4f,maxSpeed*.5f));
                // A normal transit bend is not a stop. Use its route geometry rather than
                // the shrinking guide vector to retain speed throughout the turn.
                if(flowTurn && anticipating)
                {
                    // Door entry/center/exit samples can shorten a leg without tightening
                    // the actual corner. Retain more flow on a verified steering chord
                    // between spacious bends, while packed hairpins keep their old cap.
                    float spacing=Mathf.Max((point-_segmentStart).magnitude,(_route[_cursor+1]-point).magnitude);
                    float spacious=Mathf.InverseLerp(6f,12f,spacing);
                    transitSpeed=Mathf.Lerp(transitSpeed,Mathf.Min(9f,maxSpeed*.8f),catchUp*spacious);
                }
                if(flowTurn) endSpeed=Mathf.Max(endSpeed,transitSpeed);
            }
            float speed=Mathf.Min(maxSpeed,Mathf.Sqrt(endSpeed*endSpeed+2*accel*Mathf.Max(0,distance-.002f)));
            if(!_singleMove) speed=BoundByUpcomingBends(speed,maxSpeed,accel,catchUp);
            if(_cursor+1<_route.Count) speed=Mathf.Min(speed,endSpeed+distance*2.5f);
            if(_cursor+1==_route.Count) speed=Mathf.Min(speed,distance*7f);
            // Brake before sharp route bends; heading easing must not carry a fast camera
            // metres beyond a door or below a stair waypoint before it can turn back.
            if(_travelDirection.sqrMagnitude>.5f && distance>.001f && Vector3.Dot(_travelDirection,delta/distance)<.8f)
            {
                float turning=Mathf.Max(.3f,distance/.25f)*Mathf.Lerp(.15f,1f,Mathf.Clamp01(Vector3.Dot(_travelDirection,delta/distance)));
                if(flowTurn) turning=Mathf.Max(turning,transitSpeed);
                speed=Mathf.Min(speed,turning);
            }
            Vector3 framingOffset=_position-focus; framingOffset.y=0;
            // Use ordered path distance while occluded. A player just across a wall
            // is close in space but not yet framed; radial distance cannot park the rig
            // before its mandatory corner. Visible shots retain their usual pacing.
            Vector3 radial=(focus-_position).normalized;
            float closing=distance>.001f ? Vector3.Dot(delta/distance,radial) : 0;
            float outward=Mathf.Max(0,Vector3.Dot(_focusVelocity,radial));
            float frameDistance=readableHeight ? framingOffset.magnitude : subjectDistance;
            float reserve=sight && !embedded ? 4.5f : 3f;
            float approachDistance=_singleMove || paceVisible ? frameDistance : Mathf.Max(frameDistance,remainingRoute+reserve);
            if(closing>.2f && !_classicSightRecovery)
            {
                float follow=outward+Mathf.Max(0,(approachDistance-reserve-_speed*.14f)*3f);
                speed=Mathf.Min(speed,follow/closing);
            }
            // Close framing can require an outward leg. Distance pacing may brake
            // an approach, but must not park that leg directly above the live subject.
            bool openingFrame=_classicSightRecovery || !_singleMove && frameDistance<Mathf.Max(2.5f,_classicStandOff) && closing<.2f;
            speed*=Mathf.Lerp(openingFrame ? 1f : pace,1f,_pacing.Waiting ? 0 : catchUp);
            if(_pacing.Engaged && !openingFrame)
                speed=Mathf.Min(speed,_pacing.Waiting ? 0 : _pacing.Speed*1.15f+Mathf.Max(0,subjectDistance-7f)*1.2f);
            if(!_singleMove && _pacing.Waiting && subjectDistance>7f)
                speed=Mathf.Max(speed,Mathf.Min(maxSpeed,(subjectDistance-6.5f)*3f));
            Vector3 forward=distance>.0001f ? delta/distance : Vector3.zero;
            if(sight && (_returningTime>.15f || (subjectDistance<9f && _focusVelocity.magnitude>2f
                    && Vector3.Dot(_travelDirection,_focusVelocity.normalized)<-.7f))
                && Mathf.Abs(forward.y)<.35f && Mathf.Abs(_travelDirection.y)<.35f
                && (Vector3.Dot(_travelDirection,forward)<-.5f
                    || (subjectDistance<9f && Vector3.Dot(_travelDirection,_focusVelocity.normalized)<-.7f)))
                _brakingForReturn=true;
            if(!_singleMove) _brakingForReturn&=paceVisible;
            if(_brakingForReturn) speed=0;
            // Carry speed and heading across route replacement; ease acceleration as well as position.
            // The response stays short enough to catch a running subject without a delayed departure.
            float speedResponse=Mathf.Lerp(.22f,.3f,Mathf.Clamp01(_handoffRemaining/1.2f));
            // Catch up promptly on long open legs; keep the bend/stopping envelopes above.
            speedResponse=Mathf.Lerp(speedResponse,.12f,catchUp);
            if(_brakingForReturn) speedResponse=.08f;
            _speed=Mathf.SmoothDamp(_speed,speed,ref _speedVelocity,speedResponse,80f,dt);
            // Do not let the speed filter carry momentum past the physical stopping envelope.
            // The final exponential approach stays continuous instead of truncating a fast last step.
            float safeSpeed=Mathf.Sqrt(endSpeed*endSpeed+2*accel*distance);
            if(_cursor+1<_route.Count) safeSpeed=Mathf.Min(safeSpeed,endSpeed+distance*4f);
            if(_cursor+1==_route.Count) safeSpeed=Mathf.Min(safeSpeed,distance*8f);
            if(_speed>safeSpeed) { _speed=safeSpeed; _speedVelocity=Mathf.Min(0,_speedVelocity); }
            bool stillApproaching=Vector3.Dot(_focusVelocity,(focus-_position).normalized)<-.5f;
            if(_brakingForReturn && _speed<.2f && !stillApproaching)
            {
                // Refresh the station after the subject passes; the old station may
                // still be on its outbound side, causing an unnecessary camera orbit.
                Vector3 side=Vector3.ProjectOnPlane(_position-focus,Vector3.up);
                Vector3 liveGoal=focus+side.normalized*_classicStandOff+Vector3.up*1.2f;
                if(world!=null && side.sqrMagnitude>2.25f
                    && world.IsMonitorTravelClear(_position,liveGoal)
                    && world.IsMonitorTravelClear(liveGoal,focus))
                {
                    JoinLiveTarget(liveGoal,focus,roomReady?room.Id:0,now);
                    point=liveGoal; distance=(point-_position).magnitude;
                    forward=(point-_position).normalized; anticipating=false;
                }
                _brakingForReturn=false; _speed=_speedVelocity=0; _travelDirection=forward;
            }
            // Ordinary travelling already integrates a bounded velocity below. Easing
            // a separate heading first makes a passed bend pull the rig back around
            // its marker, especially when a stair grade changes. Steer the velocity
            // toward the ordered guide directly; picture aiming remains independent.
            if(!_singleMove || _travelDirection.sqrMagnitude<.5f) _travelDirection=forward;
            else if(!_brakingForReturn)
            {
                // Short transit markers must not retain a long heading lag: that lag pushes
                // the camera past the corner, then the behind-camera guide triggers braking.
                bool levelTransit=_cursor+1<_route.Count && (Mathf.Abs(forward.y)<.35f || stairRoute && anticipating);
                float response=levelTransit ? Mathf.Lerp(.055f,.18f,Mathf.Clamp01(Vector3.Dot(_travelDirection,forward))) : .18f;
                float blend=1-Mathf.Exp(-dt/response);
                Vector3 eased;
                if(Mathf.Abs(_travelDirection.y)<.35f && Mathf.Abs(forward.y)<.35f)
                {
                    // Antipodal 3D slerp can turn a horizontal reversal through the floor.
                    // Retain turn handedness near 180 degrees instead of alternating sides.
                    Vector3 a=Vector3.ProjectOnPlane(_travelDirection,Vector3.up).normalized;
                    Vector3 b=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
                    float yaw=Vector3.SignedAngle(a,b,Vector3.up);
                    if(Mathf.Abs(yaw)>150) yaw=Mathf.Abs(yaw)*_turnSign;
                    else if(Mathf.Abs(yaw)>10) _turnSign=Mathf.Sign(yaw);
                    eased=Quaternion.AngleAxis(yaw*blend,Vector3.up)*a;
                    float elevation=Mathf.Lerp(_travelDirection.y,forward.y,blend);
                    eased=(eased*Mathf.Sqrt(1-elevation*elevation)+Vector3.up*elevation).normalized;
                }
                else eased=Vector3.Slerp(_travelDirection,forward,blend);
                _travelDirection=Vector3.RotateTowards(_travelDirection,eased,(levelTransit?540f:240f)*Mathf.Deg2Rad*dt,0).normalized;
            }
            float stepLength=Mathf.Min(_speed*dt,distance);
            if(sight && _pacing.Engaged)
            {
                Vector3 offset=_position-focus; offset.y=0;
                // Climbing along a staircase is translation, not an orbit around the subject.
                Vector3 planarDirection=stairRoute || Mathf.Abs(_focusVelocity.y)>.5f
                    ? Vector3.ProjectOnPlane(_travelDirection,Vector3.up) : _travelDirection;
                Vector3 lateral=planarDirection-Vector3.Project(planarDirection,offset.normalized);
                // Limit camera-induced orbit speed; do not zoom overhead simply to finish a route.
                float angularSpeed=(30f+Mathf.Min(25f,_pacing.Speed*8f))*Mathf.Deg2Rad;
                if(lateral.magnitude>.01f) stepLength=Mathf.Min(stepLength,angularSpeed*Mathf.Max(1,offset.magnitude)*dt/lateral.magnitude);
            }
            Vector3 move=_travelDirection*stepLength;
            if(!_singleMove) move=ClassicTerrainCommand(move,focus,now,dt);
            if(!_singleMove && dt>.0001f)
            {
                // Planner publication and marker simplification can change a braking
                // envelope in one frame. Apply that command through continuous velocity,
                // rather than turning it into a sudden physical slowdown or heading snap.
                Vector3 command=move/dt;
                if(_classicPolicyBlend>0)
                {
                    move=Vector3.zero; float remaining=dt;
                    while(remaining>.00001f)
                    {
                        float step=Mathf.Min(remaining,1f/120f); remaining-=step;
                        _motionVelocity=Vector3.MoveTowards(_motionVelocity,command,_classicBlendAcceleration*step);
                        move+=_motionVelocity*step;
                    }
                }
                else { _motionVelocity=Vector3.MoveTowards(_motionVelocity,command,160f*dt); move=_motionVelocity*dt; }
                _speed=_motionVelocity.magnitude;
                if(_speed>.001f) _travelDirection=_motionVelocity/_speed;
            }
            else _motionVelocity=dt>.0001f ? move/dt : Vector3.zero;
            // Collision belongs to route preference, never to the actual camera translation.
            // Retain the eased heading through a door, wall or moving obstacle instead of clipping
            // the step, snapping its direction or repeatedly restarting the same route.
            _position+=move; _blockedTime=0;
            // Cross a waypoint's incoming plane, not a tiny sphere around its exact coordinate.
            // An eased turn may pass beside the marker; chasing it afterward would orbit forever.
            Vector3 incoming=(point-_segmentStart).normalized;
            bool passed=_cursor+1<_route.Count && Vector3.Dot(_position-point,incoming)>=0
                && Vector3.ProjectOnPlane(_position-point,incoming).magnitude<.45f;
            if(anticipating && _cursor+1<_route.Count)
            {
                Vector3 outgoing=_route[_cursor+1]-point;
                passed|=Vector3.Dot(_position-point,outgoing)>.01f
                    && MonitorTravelPath.DistanceToSegment(_position,point,_route[_cursor+1])<.25f;
            }
            float arrival=_cursor+1==_route.Count ? .008f : Mathf.Clamp(_speed*.045f,.05f,.22f);
            if(passed || (point-_position).sqrMagnitude<arrival*arrival)
            {
                _segmentStart=point; _cursor++;
                if(!HasRoute)
                {
                    _handoffRemaining=0; _handoffVelocity=Vector3.zero;
                    _speed=_speedVelocity=0; _station=_position; _stationSince=now;
                    _needsRoute=false;
                }
            }
            if((_position-_progressPosition).sqrMagnitude>.0144f)
            { _progressPosition=_position; _stagnantTime=0; }
            else if((pace<.15f || (sight && waitingRoom && (focus-_position).magnitude<5.5f)) && _blockedTime==0) { _stagnantTime=0; _progressPosition=_position; }
            else _stagnantTime+=dt;

        }
        else if(_handoffRemaining>0 && _handoffVelocity.sqrMagnitude>.0001f)
        {
            // Planning is asynchronous. Retain a gently decaying exit velocity while it prepares
            // the first route instead of freezing above the player and restarting from rest.
            if(_routePolicy && !_singleMove)
            {
                float remaining=dt;
                while(remaining>.00001f)
                {
                    float step=Mathf.Min(remaining,1f/120f); remaining-=step;
                    float acceleration=_classicPolicyBlend>0 ? _classicBlendAcceleration : 160f;
                    _handoffVelocity=Vector3.MoveTowards(_handoffVelocity,Vector3.zero,acceleration*step);
                    _position+=_handoffVelocity*step;
                }
            }
            else { _handoffVelocity*=Mathf.Exp(-dt/.3f); _position+=_handoffVelocity*dt; }
            _station=_position;
            _motionVelocity=_handoffVelocity;
            _speed=_handoffVelocity.magnitude; _travelDirection=_handoffVelocity.normalized;
            _stagnantTime=0; _progressPosition=_position;
        }
        else
        {
            // Retained framing follows elevation continuously. Preparing a route must not
            // abruptly discard the upward/downward motion, nor restart its first leg from rest.
            float carry=_ready && !_searching && _planner.Status!=MonitorRouteStatus.Computing ? _focusVelocity.y : 0;
            _verticalCarrySpeed=Mathf.MoveTowards(_verticalCarrySpeed,carry,30f*dt);
            Vector3 idleMove=Vector3.up*(_verticalCarrySpeed*dt);
            if(!_singleMove) idleMove=ClassicTerrainCommand(idleMove,focus,now,dt);
            _position+=idleMove; _station=_position;
            _motionVelocity=dt>.0001f ? idleMove/dt : Vector3.zero;
            _speed=Mathf.Abs(_verticalCarrySpeed); _speedVelocity=0;
            if(_speed>.01f) _travelDirection=Vector3.up*Mathf.Sign(_verticalCarrySpeed);
            _stagnantTime=0; _progressPosition=_position;
        }
        Vector3 frameOffset=_position-focus; frameOffset.y=0;
        bool framed=readableHeight && frameOffset.magnitude>=2.5f && frameOffset.magnitude<=4.6f;
        if((_pacing.Waiting || framed) && _speed<.08f && waitingRoom && paceVisible && !embedded && clearLanding && HasRoute)
        {
            // A stopped player ends the old move here. A later walk acquires a fresh, same-side goal.
            _route.Clear(); _planner.Clear(); _cursor=_published=0; _searching=_needsRoute=false;
            _station=_goal=_position; _goalFocus=focus; _goalRoom=room.Id; _stationSince=now;
            _settledBySubject=true; _speed=_speedVelocity=_stagnantTime=_blockedTime=0;
            _handoffRemaining=0; _handoffVelocity=Vector3.zero;
            _travelDirection=Vector3.zero; _progressPosition=_position;
        }
        _landingStable=!IsTravelling && !_searching && !embedded ? _landingStable+dt : 0;
        Aim(focus,dt);
        State=_pacing.Waiting || _blockedTime>.1f ? MonitorTrackingState.Holding
            : IsTravelling || sight ? MonitorTrackingState.Tracking : MonitorTrackingState.Holding;
        if((_position-position).sqrMagnitude>.000001f) _lastMoveTime=now;
        position=_position; rotation=_rotation; return true;
    }
    private void Aim(Vector3 focus,float dt)
    {
        Vector3 look=focus-_position;
        if(look.sqrMagnitude>.001f)
        {
            if(_routePolicy && !_singleMove && !_dropFollowing && _aimGrace<=0)
            {
                _rotation=MonitorCameraAim.Upright(_rotation,look,dt,.075f,300f);
                return;
            }
            Quaternion aim=MonitorCameraAim.Target(_rotation,look);
            // A held route station can let the subject pass underneath the lens.
            // Target already stabilizes the up axis; ordinary route framing still needs
            // its normal response here. Keep the softer carrier/push handoff intact.
            if(_aimGrace>0 || (!_routePolicy && Mathf.Abs(look.normalized.y)>.94f))
            {
                _aimGrace=Mathf.Max(0,_aimGrace-dt);
                _rotation=Quaternion.RotateTowards(_rotation,Quaternion.Slerp(_rotation,aim,1-Mathf.Exp(-dt/.16f)),180f*dt);
                return;
            }
            var eased=Quaternion.Slerp(_rotation,aim,1-Mathf.Exp(-dt/.075f));
            // Preserve readable framing during ordinary route turns, but cap the entire update:
            // the framing correction must never bypass the angular speed limit after a handoff.
            float error=Quaternion.Angle(eased,aim);
            if(error>8) eased=Quaternion.RotateTowards(eased,aim,error-8);
            if(_routePolicy)
            {
                // At an overhead crossing, restoring the horizon can otherwise
                // spend the entire angular budget on roll. Frame the subject first,
                // then restore the horizon with the remaining bounded rotation.
                Quaternion swing=Quaternion.FromToRotation(_rotation*Vector3.forward,eased*Vector3.forward);
                float budget=300f*dt, used=Mathf.Min(budget,Quaternion.Angle(Quaternion.identity,swing));
                _rotation=Quaternion.RotateTowards(Quaternion.identity,swing,used)*_rotation;
                _rotation=Quaternion.RotateTowards(_rotation,eased,Mathf.Max(0,budget-used));
                return;
            }
            _rotation=Quaternion.RotateTowards(_rotation,eased,300f*dt);
        }
    }

    private bool CollinearSuffix(int end)
    {
        Vector3 previous=_position;
        for(int i=_cursor;i<end;i++)
        {
            Vector3 a=_route[i]-previous,b=_route[i+1]-_route[i];
            if(a.sqrMagnitude>.0025f && b.sqrMagnitude>.0025f && Vector3.Angle(a,b)>5f) return false;
            previous=_route[i];
        }
        return true;
    }
    private bool ReferenceTransitChord(Vector3 to,int end)
    {
        Vector3 chord=to-_position;
        if(chord.sqrMagnitude<.0064f || Mathf.Abs(chord.normalized.y)>.35f) return false;
        Vector3 direction=Vector3.ProjectOnPlane(chord,Vector3.up).normalized;
        Vector3 previous=_position;
        float length=0;
        for(int i=_cursor;i<=end;i++)
        {
            Vector3 point=i==end ? to : _route[i],leg=point-previous;
            length+=leg.magnitude;
            // This is a trim/contact exception, never a new route across a room,
            // another floor, or a reversal around a tight hairpin. No extra queries.
            if(length>2.4f || Mathf.Abs(point.y-_position.y)>.35f
                || Vector3.Dot(leg,chord)<-.001f
                || Vector3.Dot(Vector3.ProjectOnPlane(leg,Vector3.up).normalized,direction)<.9f
                || MonitorTravelPath.DistanceToSegment(point,_position,to)>MonitorCameraGeometry.Clearance) return false;
            previous=point;
        }
        return true;
    }
    private static bool ReferenceTransitGrade(Vector3 incoming,Vector3 outgoing)
    {
        if(Mathf.Abs(incoming.y)>.35f || Mathf.Abs(outgoing.y)>.35f) return false;
        Vector3 a=Vector3.ProjectOnPlane(incoming,Vector3.up),b=Vector3.ProjectOnPlane(outgoing,Vector3.up);
        return a.sqrMagnitude>.0001f && b.sqrMagnitude>.0001f
            && Vector3.Dot(a.normalized,b.normalized)>-.5f;
    }
    private bool LocalTransitSuffix(int end)
    {
        // Grid and navigation output contains short wall-side zigzags. They are transit
        // samples, not independent braking destinations. Simplify a local, almost-level
        // section only if the caller verifies the whole camera-sized chord through it.
        Vector3 previous=_position;
        float length=0;
        for(int i=_cursor;i<=end;i++)
        {
            Vector3 leg=_route[i]-previous; length+=leg.magnitude;
            if(length>5f || Mathf.Abs(_route[i].y-_position.y)>1f) return false;
            previous=_route[i];
        }
        // Local vertical grid noise may be removed too, but not a real steep descent.
        return Mathf.Abs((_route[end]-_position).normalized.y)<=.35f;
    }
    private static bool ContinuousGrade(Vector3 incoming,Vector3 outgoing)
    {
        if(incoming.sqrMagnitude<.0025f || outgoing.sqrMagnitude<.0025f) return false;
        if(incoming.y*outgoing.y<-.02f) return false;
        // A short grid riser belongs to its flight; a free vertical fall does not.
        if(Mathf.Abs(incoming.normalized.y)>.85f && incoming.magnitude>.9f
            || Mathf.Abs(outgoing.normalized.y)>.85f && outgoing.magnitude>.9f) return false;
        Vector3 a=Vector3.ProjectOnPlane(incoming,Vector3.up),b=Vector3.ProjectOnPlane(outgoing,Vector3.up);
        return a.sqrMagnitude<.01f || b.sqrMagnitude<.01f || Vector3.Dot(a.normalized,b.normalized)>-.5f;
    }
    private bool StairTransitSuffix(int end)
    {
        Vector3 chord=_route[end]-_position,flat=Vector3.ProjectOnPlane(chord,Vector3.up);
        if(Mathf.Abs(chord.y)<.3f || Mathf.Abs(chord.normalized.y)>.85f || flat.sqrMagnitude<.1f) return false;
        float length=0; Vector3 previous=_position;
        for(int i=_cursor;i<=end;i++)
        {
            Vector3 leg=_route[i]-previous; length+=leg.magnitude;
            if(length>6.5f || leg.y*chord.y<-.02f) return false;
            Vector3 planar=Vector3.ProjectOnPlane(leg,Vector3.up);
            if(planar.sqrMagnitude>.01f && Vector3.Dot(planar.normalized,flat.normalized)<-.2f) return false;
            previous=_route[i];
        }
        // The caller still validates the entire lifted chord. No floor/rail shortcut is authorized here.
        return true;
    }
    private void BeginPlan(IGameMonitorRouteAdapter world,float now)
    {
        _settledBySubject=false; _needsRoute=true; _published=0; _planStarted=now; _nextGeometryRecovery=now+1.2f; _permissiveRoute=false;
        // Plan the suffix FROM the retained waypoint, not from an earlier camera snapshot.
        // Otherwise prepending that waypoint can replay the approach to the same door.
        _classicFacingUntil=0; _classicObservedRoute=false;
        _planRetainsLeg=HasRoute && _cursor+1<_route.Count
            && !(_returningTime>.15f && Vector3.Dot(_route[_cursor]-_position,_goal-_position)<0);
        _planAnchor=_planRetainsLeg ? _route[_cursor] : _position;
        _planner.Begin(world,_planAnchor,_goal);
    }
    private void JoinLiveTarget(Vector3 goal,Vector3 focus,int room,float now)
    {
        _planner.Clear(); _route.Clear(); _route.Add(goal); _cursor=_published=0;
        _segmentStart=_position; _goal=goal; _goalFocus=focus; _goalRoom=room;
        _searching=_needsRoute=_settledBySubject=_planRetainsLeg=false; _permissiveRoute=false;
        _nextPlan=now+.2f;
        if(DiagnosticsEnabled) _lastMotion="live-target-rejoin";
    }
    internal static float ContinuityCost(Vector3 previous,Vector3 candidate,Vector3 focus)
    {
        Vector3 a=previous-focus,b=candidate-focus;
        float height=b.y; a.y=b.y=0;
        return (a.sqrMagnitude>.1f && b.sqrMagnitude>.1f ? Vector3.Angle(a,b)/45f : 0)
            +Mathf.Max(0,Mathf.Abs(height)-b.magnitude*.7f)*2f;
    }
    private static bool ClearPath(IGameMonitorCameraAdapter adapter,IGameMonitorRouteAdapter? world,Vector3 from,Vector3 to)
        => world!=null ? world.IsMonitorTravelClear(from,to) : adapter.IsMonitorPathClear(from,to);
}
