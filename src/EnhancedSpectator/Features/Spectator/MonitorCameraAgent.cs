using System.Collections.Generic;
using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>The photographer owns route decisions and position; the private rig owns aim only.</summary>
internal sealed class MonitorCameraAgent
{
    internal const int QueryBudget = 64;
    internal const float ExtremeDistance = 70f;
    private const float FollowingGap = 5f;
    // A faster subject is framed wider (a sprint about 8.5m, a walk 6.6m), which
    // also leaves the room to ease to rest when it stops suddenly.
    private const float SpeedGap = .35f;
    // A crouching subject is filmed closer. The posture part of the framing
    // eases out within about FramingOut seconds when the subject stands and back
    // in over FramingIn when it crouches, so the shot breathes. The speed part
    // stays immediate: it is the room to ease to rest when the subject stops.
    private const float CrouchGap = 3f, FramingOut = .6f, FramingIn = 1f;
    // Gears follow the subject's pace between a run and a sprint; walking and
    // ordinary running keep the comfortable first gear.
    private const float RunPace = 7f, SprintPace = 10f;
    // The camera rides RaisedHeight above the subject's eye line where a probe
    // finds headroom, and NormalHeight under a low ceiling (caves, ducts, vents).
    private const float NormalHeight = .55f, RaisedHeight = .8f, HeadroomMargin = .2f, HeightEase = .6f;
    // The observed leash reaches almost to the subject; its end never paces ordinary following.
    private const float RouteLead = .4f;
    // Footfalls within this corridor of a straight run share one rounded route segment.
    // The corridor narrows with the run's length, and a newly turning step ends
    // the run, so walked bends keep their footfalls instead of cutting the corner.
    // Steps, rubble and dips inside the corridor are flattened: a footfall may lie
    // up to MergeDip below the chord, but only MergeHeadroom above it. A footfall
    // rides its eye height above the walked surface, so the chord still clears a
    // stair nosing, a ledge or a rock by most of a metre.
    private const float MergeTolerance = .3f, MergeSlope = .04f, MergeTurn = 5f, MergeDip = .8f, MergeHeadroom = .4f;
    // A footfall more than this below its real supported height is a lagging climb.
    private const float ClimbLag = .15f;
    // An ordinary jump lands within LongAir. Staying up longer, such as walking
    // a handrail or a ledge that is not walkable ground, is a new level.
    private const float LongAir = .9f;
    // A steering cut may pass this little under a route point, so the camera
    // does not dip under the top edge of a stair or slope while pursuing.
    private const float GuideHeadroom = .2f;
    // Across open floor a clear sweep may cut the subject's detour, within its
    // passage and without a sharp turn; a wall corner blocks the sweep.
    private const float ShortcutReach = 2f, ShortcutTurn = 30f;
    // Consecutive footfalls closer than this were walked by the subject: they are
    // the route as they stand. Only a longer jump (a teleport) needs a connector.
    private const float StepReach = 1.5f;
    // Comfortable sideways acceleration while steering through a bend; vertical
    // bends (stairs, landings, rubble, drops) count at VerticalBendWeight.
    private const float LateralAcceleration = 12f, VerticalBendWeight = .35f;
    // The camera picks its own way: every ShortcutInterval it looks up to
    // ShortcutRange ahead along the route for a clear straight cut that saves distance.
    private const float ShortcutInterval = .2f, ShortcutRange = 12f;
    // A camera at rest turns to its guide this fast; in motion it turns along
    // the pursuit arc instead, so heading never snaps.
    // A footfall within ArriveRadius is reached; one within LeaveRadius that the
    // camera is already moving away from has been passed.
    private const float RestTurn = 6f, ArriveRadius = .6f, LeaveRadius = 3f;
    // Floor expected within this distance below a camera riding its eye height.
    private const float HoverReach = 2.6f;
    // A camera far behind may corner and brake this much harder than comfortable
    // following: lateral 12 -> 60 m/s^2, close to the previous engine's bends.
    private const float UrgentCornering = 5f;
    // A bend revealed only as the route grows lowers the speed ceiling at once;
    // it is met with a firm but finite brake rather than a one-frame speed drop.
    private const float EmergencyBrake = 80f;
    // Comfortable deceleration for route-end and bend envelopes.
    private const float EnvelopeBraking = 7f;
    private readonly struct Footprint
    {
        internal readonly Vector3 Position;
        internal readonly float Arc;
        internal Footprint(Vector3 position, float arc) { Position = position; Arc = arc; }
    }
    private readonly struct MotionLimits
    {
        internal readonly float Accelerate, Brake, Jerk;
        internal MotionLimits(float accelerate, float brake, float jerk) { Accelerate = accelerate; Brake = brake; Jerk = jerk; }
    }
    private readonly List<Footprint> _trail = new List<Footprint>(512);
    private readonly List<Vector3> _route = new List<Vector3>(512);
    private readonly List<float> _routeArcs = new List<float>(512);
    private readonly List<Vector3> _planned = new List<Vector3>(128);
    private readonly List<Vector3> _mergeSpan = new List<Vector3>(24);
    private readonly MonitorAgentRoutePlanner _planner = new MonitorAgentRoutePlanner();
    private readonly MonitorAgentCameraRig _rig = new MonitorAgentCameraRig();
    private enum StationMotion { None, Braking, Finding, Planning, Travelling }
    private bool _ready, _moving, _extreme, _hasFocus, _pending, _seedPending, _sightBlocked, _subjectTransfer;
    private bool _tailFromTrail, _hasSubjectSample;
    private StationMotion _stationMotion;
    private int _stationCandidate;
    private float _stationArc, _pendingSince, _stationSearchStarted;
    private bool _referenceTravel;
    private int _cursor = 1, _revision, _queries;
    private float _speed, _accel, _nextShortcut, _turnSign = 1, _height, _routeHeight, _cameraHeight=.55f, _queuedArc = -FollowingGap, _tailArc=-FollowingGap;
    private float _pendingArc, _stillTime, _occludedTime, _stalledTime, _nextSight, _progressWindowArc;
    private float _subjectArc, _subjectSpeed, _followError, _nextRebase, _airTime, _framingGap = FollowingGap, _heightTarget = NormalHeight;
    private Vector3 _focus, _lastSubject, _direction, _level = Vector3.forward, _heading = Vector3.forward;
    private Quaternion _rotation = Quaternion.identity;
    internal Vector3 Position { get; private set; }
    internal Vector3 Velocity { get; private set; }
    internal float RouteProgress { get; private set; }
    internal MonitorTrackingState State { get; private set; }
    internal int ShotChanges { get; private set; }
    internal bool DiagnosticsEnabled { get; set; }
    internal bool IsTravelling => _moving || _extreme || _stationMotion!=StationMotion.None;
    internal bool IsExtremeCatchUp => _extreme;
    internal string Describe(float now) => $"agent phase={State},station={_stationMotion},transfer={_subjectTransfer},extreme={_extreme},gap={(Position-_focus).magnitude:F2},speed={_speed:F2},accel={_accel:F2},error={_followError:F2},subject={_subjectSpeed:F2},arc={RouteProgress:F2},route={_cursor}/{_route.Count},trail={_trail.Count},queued={_queuedArc:F2},stalled={_stalledTime:F2},stationary={_stillTime:F2},occluded={_occludedTime:F2},queries={_queries}/{QueryBudget},cuts={ShotChanges},{_planner.Describe()}";

    internal void Seed(Vector3 position, Quaternion rotation, Vector3 velocity = default, Vector3? focus = null)
    {
        Clear();
        _ready = true; Position = position; _rotation = rotation; Velocity = velocity; _speed = velocity.magnitude;
        if (focus.HasValue) InitializeFocus(focus.Value);
        _rig.Seed(position, rotation);
        _seedPending=true;
    }

    internal void Clear()
    {
        _rig.Dispose(); _planner.Clear();
        _trail.Clear(); _route.Clear(); _routeArcs.Clear(); _planned.Clear(); _mergeSpan.Clear();
        _ready = _moving = _extreme = _hasFocus = _pending = _seedPending = false;
        _tailFromTrail = _hasSubjectSample = false;
        _cursor = 1; _speed = _accel = _nextShortcut = 0; _turnSign = 1; _direction = Vector3.zero; _level = Vector3.forward;
        _queuedArc = _tailArc = -FollowingGap; _stillTime = _occludedTime = _stalledTime = _nextSight = 0;
        _subjectArc = _subjectSpeed = _followError = _nextRebase = _airTime = 0; _framingGap = FollowingGap;
        RouteProgress = _progressWindowArc = 0; ShotChanges = 0; Velocity = Vector3.zero;
        State = MonitorTrackingState.Searching;
        _stationMotion=StationMotion.None; _subjectTransfer=_sightBlocked=false; _stationCandidate=0;
        _referenceTravel=false;
    }

    internal void BeginSubjectTransition(Vector3 focus)
    {
        _trail.Clear(); InitializeFocus(focus);
        _stillTime=_occludedTime=0; _sightBlocked=false; _seedPending=false;
        BeginStationMotion(true);
    }

    private void BeginStationMotion(bool subjectTransfer=false)
    {
        // Changing the shot goal cannot restart an already exhausted search for
        // this subject. A different subject receives its own planning budget.
        _stationSearchStarted=!subjectTransfer && _pending ? _pendingSince : float.NaN;
        _planner.Clear(); _pending=false;
        _stationMotion=StationMotion.Braking; _stationCandidate=0;
        _subjectTransfer=subjectTransfer;
    }

    internal bool TryUpdate(IGameMonitorCameraAdapter adapter, Vector3 focus, float now, float dt, int tier,
        out Vector3 position, out Quaternion rotation, float catchUpDistance = 18f)
    {
        position = Position; rotation = _rotation;
        if (!adapter.IsMonitorTargetIndoors || !(adapter is IGameMonitorAgentAdapter world)) return false;
        dt = Mathf.Clamp(dt, 0, .1f);
        // The subject's real position, not the spectator's smoothed follow anchor:
        // that anchor lags a running climb and would lay the route along the steps.
        if (adapter is IGameMonitorSubjectMotionAdapter motion && motion.TryGetMonitorSubjectFocus(out var actual)) focus = actual;
        if (!_hasFocus) InitializeFocus(focus);
        if (!_ready)
        {
            if (adapter is IGameMonitorSubjectHeadingAdapter heading && heading.TryGetMonitorSubjectHeading(out var forward))
                _heading = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            world.BeginMonitorAgentFrame(now, QueryBudget);
            Vector3 station = FindStation(world, focus, out bool found);
            if (!found) return false;
            Position = station;
            _cameraHeight=_heightTarget=Mathf.Clamp(station.y-_height,.25f,.85f);
            _rotation = Quaternion.LookRotation(focus-Position, Vector3.up);
            _rig.Seed(Position, _rotation); _ready = true;
        }
        world.BeginMonitorAgentFrame(now, QueryBudget);
        float gap = (focus-Position).magnitude;
        // Only an exceptional distance changes shots. An admitted nearby death
        // transfer owns its whole journey, even if its starting camera is distant.
        if (gap >= ExtremeDistance && !_subjectTransfer)
        {
            _extreme=true;
            Vector3 station=FindStation(world,focus,out bool found);
            if (found)
            {
                Position=station; Velocity=Vector3.zero; _speed=_accel=0; ShotChanges++;
                _cameraHeight=_heightTarget=Mathf.Clamp(station.y-focus.y,.25f,.85f);
                ResetRoute(focus); _stationMotion=StationMotion.None; _seedPending=false;
                _stillTime=_occludedTime=0; _sightBlocked=false; _moving=false; _extreme=false;
                _rotation=Quaternion.LookRotation(focus-Position,Vector3.up);
                _rig.Seed(Position,_rotation);
            }
            _queries=world.MonitorAgentQueries; State=found ? MonitorTrackingState.Holding : MonitorTrackingState.Recovering;
            _focus=focus; position=Position; rotation=_rotation; return true;
        }
        _extreme=false;
        if (_seedPending)
        {
            var place=world.ProbeMonitorAgentTravel(Position,Position);
            var forwardSight=world.ProbeMonitorAgentSight(Position,focus);
            var reverseSight=world.ProbeMonitorAgentSight(focus,Position);
            bool outside=world.TryGetMonitorAgentRoom(focus,out _)
                && !world.TryGetMonitorAgentRoom(Position,out _);
            bool insideBackface=forwardSight.Passable && reverseSight.Status==MonitorAgentProbeStatus.Blocked;
            if (outside || insideBackface || place.Status==MonitorAgentProbeStatus.Blocked)
            {
                BeginStationMotion(); _seedPending=false;
            }
            else if (place.Status!=MonitorAgentProbeStatus.Deferred) _seedPending=false;
        }
        if (_revision != world.MonitorAgentWorldRevision)
        {
            _revision = world.MonitorAgentWorldRevision;
            ResetRoute(new Vector3(focus.x,_height,focus.z));
            if (_stationMotion!=StationMotion.None)
            {
                _stationMotion=StationMotion.Braking; _stationCandidate=0;
                _stationSearchStarted=float.NaN;
            }
        }
        ObserveSubject(adapter, focus, dt);
        _cameraHeight+=(_heightTarget-_cameraHeight)*(1-Mathf.Exp(-dt/HeightEase));
        gap=(new Vector3(focus.x,_height,focus.z)-Position).magnitude;
        Vector3 subject=new Vector3(focus.x, _routeHeight+_cameraHeight, focus.z);
        RecordTrail(world,subject);
        ObserveProgress(subject,dt);
        if (now >= _nextSight)
        {
            var sight=world.ProbeMonitorAgentSight(Position,focus);
            if (sight.Status!=MonitorAgentProbeStatus.Deferred)
            {
                _sightBlocked=!sight.Passable; _nextSight=now+.1f;
                if (!_sightBlocked) _occludedTime=0;
            }
        }
        _occludedTime=_sightBlocked ? _occludedTime+dt : 0;
        if (_stationMotion==StationMotion.None && _stillTime>=1f && _occludedTime>=1f)
            BeginStationMotion();

        Vector3 before=Position;
        if (_stationMotion!=StationMotion.None)
            UpdateStationMotion(adapter,world,focus,now,dt,catchUpDistance);
        else
            Follow(adapter,world,now,dt,tier,catchUpDistance,gap);
        if (dt>0) Velocity=(Position-before)/dt;
        _rotation=_rig.Evaluate(Position,new Vector3(focus.x,_height,focus.z),_rotation,dt);
        _queries=world.MonitorAgentQueries;
        State=_stationMotion!=StationMotion.None || _pending || _stalledTime>.05f
            ? MonitorTrackingState.Recovering : _moving ? MonitorTrackingState.Tracking : MonitorTrackingState.Holding;
        _focus=focus; position=Position; rotation=_rotation; return true;
    }

    private void Follow(IGameMonitorCameraAdapter adapter, IGameMonitorAgentAdapter world, float now, float dt, int tier, float catchUp, float gap)
    {
        world.SetMonitorAgentQueryLimit(QueryBudget-16);
        if (_pending)
        {
            _planner.Tick(adapter,now);
            if (_planner.Status==MonitorAgentRouteStatus.Complete
                || _planner.Status==MonitorAgentRouteStatus.Unavailable || now-_pendingSince>=1f)
                CommitPlan(_pendingArc);
        }
        bool crouching=adapter is IGameMonitorSubjectPostureAdapter posture && posture.IsMonitorSubjectCrouching;
        float stance=crouching ? CrouchGap : FollowingGap;
        _framingGap+=(stance-_framingGap)*(1-Mathf.Exp(-dt/(stance>_framingGap ? FramingOut : FramingIn)));
        float desiredGap=_framingGap+SpeedGap*_subjectSpeed;
        // A visible subject nearby is framed by straight distance, but not one on
        // another storey: looking down through an opening is no framing, so a
        // large height difference follows the route down (or up) instead.
        float storey=Mathf.InverseLerp(1.5f,3f,Mathf.Abs(_height-(Position.y-_cameraHeight)));
        if (!_pending)
        {
            RebaseNearSubject(world,gap,now);
            // A camera at rest within its framing distance has nothing to follow
            // yet; it never routes toward a subject that has not walked away.
            if (_cursor<_route.Count || gap>FollowingGap || storey>0) ExtendRoute(adapter,world,now);
        }
        world.SetMonitorAgentQueryLimit(QueryBudget);

        // One continuous speed law: match the subject's own pace and close the
        // remaining distance error. No hold/burst thresholds and no hard settle.
        float distance=PathGap();
        if (!_sightBlocked) distance=Mathf.Lerp(Mathf.Min(distance,gap*1.25f),distance,storey);
        _followError=distance-desiredGap;
        // The camera never parks hanging in the air above a drop: while the route
        // leads down and no floor lies within reach below it (an opening, a
        // ledge), it keeps following until it rides above the floor below.
        // Stairs and slopes stay within reach, so they are unaffected.
        if (_followError<1f && _cursor<_route.Count && _route[_route.Count-1].y<Position.y-.5f
            && world.ProbeMonitorAgentTravel(Position,Position+Vector3.down*HoverReach).Passable)
            _followError=1f;
        // Far in a straight line or far behind along the route: both are a catch-up.
        float far=Mathf.Max(Far(gap,catchUp),Mathf.Clamp01((_followError-3f)/6f));
        // A faster subject raises the pursuit's own pace: its speed ceiling,
        // its acceleration and how late it eases into the framing distance.
        float pace=_subjectSpeed;
        // Gear: from a walk to a sprint the camera may accelerate, brake and corner
        // harder, so a fast subject through many corners does not leave it in its
        // walking gear. It shifts continuously with the filtered pace.
        float gear=.5f*Mathf.InverseLerp(RunPace,SprintPace,pace);
        float target=_subjectSpeed+Approach(_followError,Tier(tier,1.6f,2f,2.5f),Mathf.Lerp(Mathf.Max(Tier(tier,3f,4f,5f),.5f*pace),9f,far));
        target=Mathf.Clamp(target,0,Mathf.Lerp(Mathf.Max(Tier(tier,10f,14f,18f),1.5f*pace),40f,far));
        // A camera that is already too close and still moving brakes firmly.
        float close=Mathf.Clamp01(-_followError/1.5f);
        float drive=Mathf.Max(far,gear);
        var limits=new MotionLimits(Mathf.Lerp(Mathf.Max(Tier(tier,6f,8f,10f),pace),20f,drive),
            Mathf.Max(Mathf.Lerp(10f,16f,drive),10f+15f*close),Mathf.Max(Mathf.Lerp(40f,120f,drive),40f+200f*close));
        // A seeded hand-off velocity with no observed route yet coasts out gently.
        if (_cursor>=_route.Count) { if (_speed>0) CoastToStop(dt); }
        // Falling behind along a winding route is urgent even while the straight gap is short.
        else MoveAlongRoute(world,now,dt,target,limits,true,Mathf.Max(drive,Mathf.Clamp01((_followError-2f)/6f)));
        _moving=_speed>.01f || (_followError>.05f && _cursor<_route.Count);
        bool wantsProgress=_followError>1f && (_pending || _cursor<_route.Count || HasEligibleFootprint());
        if (wantsProgress) _stalledTime+=dt;
        if (!wantsProgress || RouteProgress-_progressWindowArc>=.15f)
        { _stalledTime=0; _progressWindowArc=RouteProgress; }
    }

    private void UpdateStationMotion(IGameMonitorCameraAdapter adapter,IGameMonitorAgentAdapter world,
        Vector3 focus,float now,float dt,float catchUp)
    {
        if (_stationMotion==StationMotion.Braking)
        {
            if (_cursor<_route.Count && _speed>0) MoveAlongRoute(world,now,dt,0,new MotionLimits(0,14f,80f),false,0);
            else CoastToStop(dt);
            // The proportional brake only approaches zero; end its imperceptible tail.
            _moving=_speed>.05f;
            if (_moving) return;
            _speed=_accel=0;
            ClearRoute(); _stationMotion=StationMotion.Finding;
        }
        if (_stationMotion==StationMotion.Finding)
        {
            Vector3 station=FindStation(world,focus,out bool found);
            if (!found) return;
            _stationArc=_trail[_trail.Count-1].Arc;
            _planner.Request(adapter,Position,station,now);
            _pendingSince=float.IsNaN(_stationSearchStarted) ? now : _stationSearchStarted;
            _stationMotion=StationMotion.Planning;
        }
        if (_stationMotion==StationMotion.Planning)
        {
            world.SetMonitorAgentQueryLimit(QueryBudget-16);
            _planner.Tick(adapter,now);
            world.SetMonitorAgentQueryLimit(QueryBudget);
            // The bounded search needs its full second through corners and stairs;
            // an earlier advisory reference would cut straight through the walls.
            if (_planner.Status!=MonitorAgentRouteStatus.Complete
                && _planner.Status!=MonitorAgentRouteStatus.Unavailable && now-_pendingSince<1f) return;
            Append(Position); CommitPlan(float.NaN);
            _stationMotion=StationMotion.Travelling;
        }
        if (_stationMotion==StationMotion.Travelling)
        {
            float remaining=RemainingRoute();
            float far=Mathf.SmoothStep(0,1,Mathf.InverseLerp(catchUp*.75f,catchUp*1.25f,remaining));
            // Finite-time arrival at a constant comfortable deceleration, with a
            // small final creep so the journey actually completes.
            float target=Mathf.Min(Mathf.Max(Mathf.Sqrt(2f*Mathf.Lerp(5f,EnvelopeBraking,far)*remaining),.15f),Mathf.Lerp(12f,40f,far));
            MoveAlongRoute(world,now,dt,target,new MotionLimits(Mathf.Lerp(8f,20f,far),14f,Mathf.Lerp(60f,120f,far)),false,far);
            _moving=_cursor<_route.Count;
            if (_moving) return;
            ClearRoute(); _stationMotion=StationMotion.None; _subjectTransfer=false;
            _referenceTravel=false;
            // Continue from the subject's newest footfalls. Never walk back along
            // the old trail behind the new station, nor replay the old subject.
            _queuedArc=_tailArc=_stationArc;
            _stillTime=_occludedTime=_stalledTime=0; _sightBlocked=false; _nextSight=now;
        }
    }

    private void CommitPlan(float trailArc)
    {
        _planned.Clear();
        if (_planner.Status==MonitorAgentRouteStatus.Complete) _planner.CopyPath(_planned);
        else { _planner.CopyReferencePath(_planned); _referenceTravel=true; }
        // Geometry guides the route, but never vetoes progression indefinitely.
        // A failed leg preserves its certified prefix and ordered portal anchors.
        for (int i=1; i<_planned.Count; i++) Append(_planned[i],i==_planned.Count-1 ? trailArc : float.NaN);
        if (!float.IsNaN(trailArc)) _queuedArc=trailArc;
        _pending=false; _planner.Clear();
    }

    private void CoastToStop(float dt)
    {
        _speed=Mathf.MoveTowards(_speed,0,30f*dt); _accel=0;
        Position+=Velocity.normalized*_speed*dt;
    }

    private void InitializeFocus(Vector3 focus)
    {
        _hasFocus = true; _focus = focus; _height = _routeHeight = focus.y;
        _cameraHeight=_heightTarget=_ready ? Mathf.Clamp(Position.y-focus.y,.25f,.85f) : NormalHeight;
        Vector3 direction = Vector3.ProjectOnPlane(focus-Position, Vector3.up);
        if (direction.sqrMagnitude > .1f) _heading = direction.normalized;
        _trail.Add(new Footprint(focus+Vector3.up*_cameraHeight, 0));
        _subjectArc=0; _hasSubjectSample=false;
    }

    private void ObserveSubject(IGameMonitorCameraAdapter adapter, Vector3 focus, float dt)
    {
        Vector3 horizontal = Vector3.ProjectOnPlane(focus-_focus, Vector3.up);
        _stillTime = horizontal.magnitude < .06f*dt && Mathf.Abs(focus.y-_focus.y)<.12f*dt ? _stillTime+dt : 0;
        if (horizontal.magnitude > .5f*dt) _heading = horizontal.normalized;
        bool grounded = !(adapter is IGameMonitorTraversalAdapter traversal) || traversal.IsMonitorSubjectGrounded;
        _airTime = grounded ? 0 : _airTime+dt;
        bool supported = grounded || _airTime>LongAir;
        // Supported height is continuous; quantized height creates artificial bends.
        // Ordinary airborne jumps still remain outside the navigation trail.
        if (supported || focus.y<_routeHeight-.25f)
            _routeHeight=focus.y;
        float error = focus.y-_height;
        // Ordinary jumps and small ground noise do not become route waypoints.
        if (supported && Mathf.Abs(error)>.28f)
            _height += (error-Mathf.Sign(error)*.12f)*(1-Mathf.Exp(-dt/.12f));
        else if (!supported && error<-.25f)
            _height += (error+.12f)*(1-Mathf.Exp(-dt/.10f));
    }

    // Continuous subject progress along its own trail, and its filtered pace.
    private void ObserveProgress(Vector3 subject, float dt)
    {
        Footprint last=_trail[_trail.Count-1];
        _subjectArc=last.Arc+(subject-last.Position).magnitude;
        if (dt>0 && _hasSubjectSample)
        {
            float rate=Mathf.Min((subject-_lastSubject).magnitude/dt,20f);
            _subjectSpeed+=(rate-_subjectSpeed)*(1-Mathf.Exp(-dt/.12f));
        }
        _lastSubject=subject; _hasSubjectSample=true;
    }

    private void RecordTrail(IGameMonitorAgentAdapter world,Vector3 point)
    {
        Footprint last = _trail[_trail.Count-1];
        float distance = (point-last.Position).magnitude;
        if (distance < .4f) return;
        // Ride higher only where a camera-sized sweep finds headroom above the eye line.
        Vector3 eye=new Vector3(point.x,_routeHeight+NormalHeight,point.z);
        _heightTarget=world.ProbeMonitorAgentTravel(eye,eye+Vector3.up*(RaisedHeight-NormalHeight+HeadroomMargin)).Passable
            ? RaisedHeight : NormalHeight;
        // Prefer comfortable filtered height only where the supported-to-filtered
        // hull is clear. A landing slab cannot pull the footprint into or below it.
        // The filter lags a running climb by over half a metre, which would lay
        // the route along the steps; a climbing footfall keeps its real height.
        Vector3 preferred=new Vector3(point.x,_height+_cameraHeight,point.z);
        if (preferred.y>=point.y-ClimbLag && world.ProbeMonitorAgentTravel(point,preferred).Passable) point=preferred;
        distance=(point-last.Position).magnitude;
        if (distance<.4f) return;
        if (_trail.Count >= 512)
        {
            int passed = 0;
            while (passed+1<_trail.Count && _trail[passed+1].Arc<_queuedArc) passed++;
            if (passed>0) _trail.RemoveRange(0, passed);
            else return; // Retain the ordered route rather than dropping an unvisited corner.
        }
        _trail.Add(new Footprint(point, last.Arc+distance));
    }

    private float Eligible() => Mathf.Max(_trail[0].Arc,_subjectArc-RouteLead);

    private bool HasEligibleFootprint()
    {
        float eligible=Eligible();
        for (int i=_trail.Count-1; i>=0 && _trail[i].Arc>_queuedArc+.01f; i--)
            if (_trail[i].Arc<=eligible) return true;
        return false;
    }

    private void ExtendRoute(IGameMonitorCameraAdapter adapter, IGameMonitorAgentAdapter world, float now)
    {
        if (_route.Count == 0) { Append(Position); _cursor=1; }
        float eligible=Eligible();
        if (_cursor>=_route.Count)
        {
            // Progress through nearby already-observed footfalls, never project onto a distant floor or room.
            for (int i=0; i<_trail.Count && _trail[i].Arc<=eligible; i++)
                if (_trail[i].Arc>_queuedArc && (Position-_trail[i].Position).sqrMagnitude<.64f)
                    _queuedArc=_tailArc=_trail[i].Arc;
        }
        for (int n=0; n<8; n++)
        {
            int index = 0;
            while (index<_trail.Count && _trail[index].Arc<=_queuedArc+.01f) index++;
            if (index>=_trail.Count || _trail[index].Arc>eligible) return;
            Footprint target = _trail[index];
            if (TryMergeTail(world,target))
            {
                _queuedArc=target.Arc;
                continue;
            }
            Vector3 start = _route[_route.Count-1];
            // Colliders are only a reference: the subject's own step is never vetoed,
            // so stairs, rubble, drops and gaps it crossed are followed directly.
            if ((target.Position-start).sqrMagnitude<=StepReach*StepReach)
            {
                _referenceTravel=false;
                Append(target.Position,target.Arc); _queuedArc=target.Arc;
                _tailFromTrail=true;
                continue;
            }
            var probe = world.ProbeMonitorAgentTravel(start, target.Position);
            if (probe.Status==MonitorAgentProbeStatus.Deferred && !_referenceTravel && _stalledTime<1f) return;
            if (probe.Passable || probe.Status==MonitorAgentProbeStatus.Deferred)
            {
                _referenceTravel=!probe.Passable;
                Append(target.Position,target.Arc); _queuedArc=target.Arc;
                _tailFromTrail=true;
            }
            else
            {
                _pendingArc=target.Arc; _pending=true; _pendingSince=now;
                _planner.Request(adapter, start, target.Position, now); return;
            }
        }
    }

    // Footfalls are sampled every 0.4m and carry every sway, step and rock of
    // the subject's walk. Extend the unreached tail vertex instead of adding a
    // new kink while the run hugs one straight chord, or while a clear sweep
    // cuts across open floor: the camera need not retrace every footfall.
    private bool TryMergeTail(IGameMonitorAgentAdapter world, Footprint footprint)
    {
        int last=_route.Count-1;
        // The vertex after the current segment shapes its prepared end tangent; keep it.
        if (!_tailFromTrail || _referenceTravel || last<=_cursor+1 || _mergeSpan.Count>=24) return false;
        Vector3 anchor=_route[last-1], tail=_route[last], point=footprint.Position;
        if (Vector3.Dot(tail-anchor,point-tail)<=0) return false;
        // Turns are judged on the floor plan; a climb is not a bend.
        float leave=Vector3.Angle(Flat(anchor-_route[last-2]),Flat(point-anchor));
        float tolerance=Mathf.Min(MergeTolerance,MergeSlope*Flat(point-anchor).magnitude);
        // Neither end of a hugging run may hide a turn: it leaves its anchor along
        // the incoming route and its newest step is still straight.
        bool hugs=leave<=MergeTurn && Vector3.Angle(Flat(point-tail),Flat(point-anchor))<=MergeTurn
            && Beside(tail,anchor,point,tolerance,MergeHeadroom);
        for (int i=0; hugs && i<_mergeSpan.Count; i++) hugs=Beside(_mergeSpan[i],anchor,point,tolerance,MergeHeadroom);
        if (!hugs)
        {
            if (leave>ShortcutTurn || !Beside(tail,anchor,point,ShortcutReach,MergeHeadroom)) return false;
            for (int i=0; i<_mergeSpan.Count; i++)
                if (!Beside(_mergeSpan[i],anchor,point,ShortcutReach,MergeHeadroom)) return false;
            if (!world.ProbeMonitorAgentTravel(anchor,point).Passable) return false;
        }
        _mergeSpan.Add(tail);
        _route[last]=point; _routeArcs[last]=footprint.Arc; _tailArc=footprint.Arc;
        return true;
    }

    // A subject that came back past a waiting camera must not drag it around
    // the abandoned loop. Resume from a nearby newer footfall instead.
    private void RebaseNearSubject(IGameMonitorAgentAdapter world, float gap, float now)
    {
        // Only when the camera is about to set off after the subject has passed:
        // an approaching subject keeps the waiting camera still.
        if (_speed>.05f || _sightBlocked || _referenceTravel || now<_nextRebase || PathGap()<gap*1.5f+3f) return;
        _nextRebase=now+.25f;
        float ahead=_queuedArc;
        for (int i=_cursor; i<_routeArcs.Count; i++)
            if (!float.IsNaN(_routeArcs[i])) { ahead=_routeArcs[i]; break; }
        float eligible=Eligible();
        int probes=0;
        for (int i=_trail.Count-1; i>=0 && probes<3; i--)
        {
            Footprint footprint=_trail[i];
            if (footprint.Arc<=ahead+4f) return;
            if (footprint.Arc>eligible || (footprint.Position-Position).sqrMagnitude>16f) continue;
            var probe=world.ProbeMonitorAgentTravel(Position,footprint.Position); probes++;
            if (probe.Status==MonitorAgentProbeStatus.Deferred) return;
            if (!probe.Passable) continue;
            ClearRoute(); Append(Position); _cursor=1;
            Append(footprint.Position,footprint.Arc); _queuedArc=footprint.Arc; _tailFromTrail=true;
            return;
        }
    }

    private void Append(Vector3 point, float trailArc=float.NaN)
    {
        if (!float.IsNaN(trailArc)) _tailArc=trailArc;
        _tailFromTrail=false;
        if (_route.Count>0 && (point-_route[_route.Count-1]).sqrMagnitude<=.0025f) return;
        _route.Add(point); _routeArcs.Add(trailArc); _mergeSpan.Clear();
    }

    // Steering, not rails: the camera heads for a guide a few metres ahead along
    // its route and turns along the pursuit arc toward it, so bends become wide
    // continuous curves and speed is only reduced for the turn actually ahead.
    private void MoveAlongRoute(IGameMonitorAgentAdapter world, float now, float dt, float target, MotionLimits limits, bool following, float urgency)
    {
        AdvanceCursor();
        if (_cursor>=_route.Count) { _speed=_accel=0; return; }
        if (following) TakeShortcut(world,now);
        float strength=Mathf.Lerp(1f,UrgentCornering,urgency);
        float lateral=LateralAcceleration*strength, braking=EnvelopeBraking*strength;
        float look=Mathf.Clamp(.8f+.3f*_speed,1f,5f);
        Vector3 guide=Guide(world,look), toward=guide-Position;
        float distance=toward.magnitude;
        Vector3 desired=distance>.0001f ? toward/distance : _direction;
        // At rest there is no velocity to keep continuous: face the guide directly.
        if (_speed<.05f || _direction.sqrMagnitude<.5f) { _direction=desired; KeepLevel(desired); }
        float bend=Mathf.Deg2Rad*Vector3.Angle(Weighted(_direction),Weighted(desired));
        // Pure pursuit: the arc tangent to the heading through the guide.
        float curvature=2f*Mathf.Sin(Mathf.Min(bend,Mathf.PI*.5f))/Mathf.Max(distance,.3f);
        // The guide may jump when the route is merged or cut short; the turn now
        // ahead only lowers the target, while braking for bends further along the
        // route and for the route's end stays a firm ceiling.
        float bendSpeed=bend>Mathf.PI*.5f ? 1.5f : curvature>.0001f ? Mathf.Sqrt(lateral/curvature) : 240f;
        // While the leash keeps extending just behind a walking subject, its end
        // moves with the subject; braking for it as a fixed wall creates judder.
        float carried=following && !_pending && _tailFromTrail && _subjectArc-_tailArc<RouteLead+1.2f ? .8f*_subjectSpeed : 0;
        float ceiling=Mathf.Min(BendEnvelope(lateral,braking,look),carried+Mathf.Sqrt(2f*EnvelopeBraking*RemainingRoute()));
        Accelerate(Mathf.Min(target,Mathf.Min(bendSpeed,ceiling)),ceiling,limits,dt);
        // Turn along the pursuit arc, never harder than the comfortable sideways acceleration.
        Turn(desired,(Mathf.Min(_speed*curvature,lateral/Mathf.Max(_speed,.5f))+RestTurn/(1f+_speed))*dt);
        float step=_speed*dt;
        Position+=_direction*step; RouteProgress+=step;
        // Running off the route's end leaves the remaining speed to coast out.
        AdvanceCursor();
    }

    // The guide lies `look` metres ahead along the route from the camera. It may
    // cut a bend where the cut stays beside the walked route or a sweep shows it
    // clear; otherwise it is drawn back toward the route so walls are not cut.
    private Vector3 Guide(IGameMonitorAgentAdapter world, float look)
    {
        for (int attempt=0; attempt<3; attempt++, look*=.5f)
        {
            Vector3 from=Position, guide=Position;
            float left=look;
            int index=_cursor;
            for (; index<_route.Count; index++)
            {
                float leg=(_route[index]-from).magnitude;
                if (leg>=left) { guide=from+(_route[index]-from)*(left/Mathf.Max(leg,.0001f)); break; }
                left-=leg; from=guide=_route[index];
            }
            if (index==_cursor) return guide;
            bool beside=true;
            for (int i=_cursor; beside && i<index && i<_route.Count; i++) beside=Beside(_route[i],Position,guide,MergeTolerance,GuideHeadroom);
            if (beside || world.ProbeMonitorAgentTravel(Position,guide).Passable) return guide;
        }
        return _route[_cursor];
    }

    // Brake ahead of the route's own bends: a bend of angle a, steered with
    // look-ahead L, needs curvature about 2 sin(a/2)/L. Reversals are taken slowly.
    private float BendEnvelope(float lateral, float braking, float look)
    {
        // Bend angles come from the route's own legs: the camera's small offset
        // inside a pursuit arc must not inflate the next bend as it nears it.
        float limit=240f, distance=(_route[_cursor]-Position).magnitude;
        Vector3 incoming=_route[_cursor]-_route[_cursor-1];
        for (int i=_cursor; i+1<_route.Count && distance<30f; i++)
        {
            Vector3 outgoing=_route[i+1]-_route[i];
            float angle=Vector3.Angle(Weighted(incoming),Weighted(outgoing));
            if (angle>3f && incoming.sqrMagnitude>.0001f && outgoing.sqrMagnitude>.0001f)
            {
                float speed=angle>150f ? 1.5f : Mathf.Sqrt(lateral*look/(2f*Mathf.Sin(.5f*Mathf.Deg2Rad*angle)));
                limit=Mathf.Min(limit,Mathf.Sqrt(speed*speed+2f*braking*distance));
            }
            distance+=outgoing.magnitude; incoming=outgoing;
        }
        return limit;
    }

    // Turn the heading toward the guide: yaw by at most `step` radians about the
    // vertical, with a stable handedness so a reversal never pitches through the
    // floor, and pitch with the cheaper vertical budget that the bend speed
    // already assumes. A guide straight below is reached by pitching down, never
    // by circling above it.
    private void Turn(Vector3 desired, float step)
    {
        KeepLevel(_direction);
        Vector3 to=Flat(desired);
        if (to.sqrMagnitude>.0004f)
        {
            float yaw=Vector3.SignedAngle(_level,to,Vector3.up);
            if (Mathf.Abs(yaw)>170f) yaw=Mathf.Abs(yaw)*_turnSign;
            else if (Mathf.Abs(yaw)>10f) _turnSign=Mathf.Sign(yaw);
            float degrees=step*Mathf.Rad2Deg;
            _level=Quaternion.AngleAxis(Mathf.Clamp(yaw,-degrees,degrees),Vector3.up)*_level;
        }
        float pitch=Mathf.MoveTowards(Mathf.Asin(Mathf.Clamp(_direction.y,-1,1)),Mathf.Asin(Mathf.Clamp(desired.y,-1,1)),step/VerticalBendWeight);
        _direction=_level*Mathf.Cos(pitch)+Vector3.up*Mathf.Sin(pitch);
    }

    // The level heading survives a vertical stretch of travel.
    private void KeepLevel(Vector3 direction)
    {
        Vector3 flat=Flat(direction);
        if (flat.sqrMagnitude>.0004f) _level=flat.normalized;
    }

    // Follow the camera's own projection onto the route, never backwards: a
    // pursuit arc cuts inside a bend and may never cross the bend's own plane.
    // A steering camera passes beside the subject's footfalls rather than onto
    // them: an intermediate footfall within ArriveRadius on the floor plan (and at
    // its height) is reached, or a tight orbit beside an unreachable corner would
    // hold the guide in place. Planned corners hug walls and the route's end is a
    // stop, so those are still reached exactly.
    private void AdvanceCursor()
    {
        while (_cursor<_route.Count)
        {
            Vector3 start=_route[_cursor-1], end=_route[_cursor];
            Vector3 offset=end-Position;
            // A footfall the camera has already turned away from is behind it.
            // Returning to touch it after the subject doubled back past the camera
            // is what made it circle in place: turn away, turn back, repeat.
            bool beside=_cursor+1<_route.Count && !float.IsNaN(_routeArcs[_cursor])
                ? Mathf.Abs(offset.y)<=1f && (Flat(offset).sqrMagnitude<=ArriveRadius*ArriveRadius
                    || _speed>.05f && offset.sqrMagnitude<=LeaveRadius*LeaveRadius && Vector3.Dot(Flat(_direction),Flat(offset))<0)
                : offset.sqrMagnitude<=.0025f;
            bool reached=beside || Vector3.Dot(Position-end,end-start)>=0;
            bool onNext=_cursor+1<_route.Count && Vector3.Dot(Position-end,_route[_cursor+1]-end)>0
                && SegmentDistance(Position,end,_route[_cursor+1])<SegmentDistance(Position,start,end);
            if (!reached && !onNext) break;
            _cursor++;
        }
        if (_cursor>=64)
        {
            int drop=_cursor-2;
            _route.RemoveRange(0,drop); _routeArcs.RemoveRange(0,drop); _cursor=2;
        }
    }

    // The camera chooses its own way: a clear straight cut to a route point
    // ahead replaces the subject's detour, loop or doubled-back stair run.
    private void TakeShortcut(IGameMonitorAgentAdapter world, float now)
    {
        if (now<_nextShortcut || _cursor+1>=_route.Count) return;
        _nextShortcut=now+ShortcutInterval;
        float along=(_route[_cursor]-Position).magnitude;
        // Sweeps are spread from far to near: in a cave the farthest cuts are often
        // grazed by rubble while a nearer one is clear.
        int first=Mathf.Min(_route.Count-1,_cursor+32), stride=Mathf.Max(1,(first-_cursor)/4), probes=0;
        for (int i=first; i>_cursor; i--)
        {
            float straight=(_route[i]-Position).magnitude;
            if (straight>ShortcutRange) continue;
            float routed=along;
            for (int j=_cursor+1; j<=i; j++) routed+=(_route[j]-_route[j-1]).magnitude;
            if (routed<straight*1.3f+2f) continue;
            // A footfall the subject just ran past beside the camera needs no sweep:
            // what the subject walked is the route.
            if (straight>StepReach || float.IsNaN(_routeArcs[i]))
            {
                if (probes>=4 || (first-i)%stride!=0) continue;
                probes++;
                if (!world.ProbeMonitorAgentTravel(Position,_route[i]).Passable) continue;
            }
            _route[i-1]=Position; _cursor=i; _mergeSpan.Clear();
            return;
        }
    }

    private static Vector3 Weighted(Vector3 value) => new Vector3(value.x,value.y*VerticalBendWeight,value.z);

    // Jerk-limited speed tracking: acceleration itself ramps, so starts, stops
    // and corrections ease in and out rather than snapping.
    private void Accelerate(float target, float ceiling, MotionLimits limits, float dt)
    {
        if (dt<=0) return;
        float wanted=Mathf.Clamp((target-_speed)/.25f,-limits.Brake,limits.Accelerate);
        _accel=Mathf.MoveTowards(_accel,wanted,limits.Jerk*dt);
        float next=_speed+_accel*dt;
        if ((_accel>0 && _speed<=target && next>target) || (_accel<0 && _speed>=target && next<target)) next=target;
        if (next>ceiling) { next=Mathf.Min(next,Mathf.Max(ceiling,_speed-EmergencyBrake*dt)); _accel=Mathf.Min(_accel,0); }
        if (next<=0) { next=0; _accel=Mathf.Max(_accel,0); }
        _speed=next;
    }

    private static float Approach(float error, float gain, float arrival)
        => error<=0 ? gain*error : Mathf.Min(gain*error,Mathf.Sqrt(2f*arrival*error));

    private static float Tier(int tier, float slow, float normal, float fast) => tier<=0 ? slow : tier==1 ? normal : fast;

    private static float Far(float gap, float catchUp) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(catchUp*.75f,catchUp*1.25f,gap));

    private Vector3 FindStation(IGameMonitorAgentAdapter world, Vector3 focus, out bool found)
    {
        // Full ring, ordered from behind through the sides to the front. Keep
        // the cursor when a query is deferred so a dense scene cannot starve later candidates.
        for (; _stationCandidate<48; _stationCandidate++)
        {
            int directionIndex=_stationCandidate%8, band=_stationCandidate/8;
            float angle=directionIndex==0 ? 0 : directionIndex==7 ? 180
                : ((directionIndex+1)/2)*45f*(directionIndex%2==1 ? 1 : -1);
            Vector3 direction=Quaternion.AngleAxis(angle,Vector3.up)*(-_heading);
            float distance=band<2 ? 5f : band<4 ? 3.5f : 1.6f;
            Vector3 candidate=focus+direction*distance+Vector3.up*(band%2==0 ? .55f : .25f);
            var place=world.ProbeMonitorAgentTravel(candidate,candidate);
            if (place.Status==MonitorAgentProbeStatus.Deferred) break;
            if (!place.Passable) continue;
            if (world.TryGetMonitorAgentRoom(focus,out var subjectRoom)
                && (!world.TryGetMonitorAgentRoom(candidate,out var cameraRoom) || subjectRoom.Id!=cameraRoom.Id)) continue;
            var sight=world.ProbeMonitorAgentSight(candidate,focus);
            if (sight.Status==MonitorAgentProbeStatus.Deferred) break;
            if (!sight.Passable) continue;
            var reverse=world.ProbeMonitorAgentSight(focus,candidate);
            if (reverse.Status==MonitorAgentProbeStatus.Deferred) break;
            if (!reverse.Passable) continue;
            _stationCandidate=0; found=true; return candidate;
        }
        if (_stationCandidate>=48)
        {
            // A complete unsuccessful ring is different from a deferred query.
            // Geometry is advisory: a subject phased into solid geometry must
            // not keep either a 70m cut or a continuous transfer waiting forever.
            _stationCandidate=0; found=true;
            return focus-_heading*1.6f+Vector3.up*.25f;
        }
        found=false; return Position;
    }

    private void ClearRoute()
    {
        _planner.Clear(); _route.Clear(); _routeArcs.Clear(); _planned.Clear(); _mergeSpan.Clear();
        _cursor=1; _pending=_moving=_tailFromTrail=false;
        _progressWindowArc=RouteProgress;
    }

    private void ResetRoute(Vector3 focus)
    {
        ClearRoute(); _trail.Clear(); _queuedArc=_tailArc=-FollowingGap; _referenceTravel=false;
        _height=_routeHeight=focus.y; _trail.Add(new Footprint(focus+Vector3.up*_cameraHeight,0));
        _focus=focus; _hasFocus=true; _subjectArc=0; _hasSubjectSample=false;
    }

    private Vector3 NextFootprint(Vector3 end)
    {
        for (int i=0; i<_trail.Count; i++)
            if (_trail[i].Arc>_tailArc+.01f) return _trail[i].Position;
        return end;
    }

    // Distance the camera still has to travel along its route and the
    // subject's observed trail before it reaches the subject.
    private float PathGap()
    {
        Vector3 end=_cursor<_route.Count ? _route[_route.Count-1] : Position;
        float result=RemainingRoute();
        for (int i=0; i<_trail.Count; i++)
            if (_trail[i].Arc>_tailArc+.01f)
                return result+(_trail[i].Position-end).magnitude+Mathf.Max(0,_subjectArc-_trail[i].Arc);
        return result+Mathf.Max(0,_subjectArc-_tailArc);
    }

    private static Vector3 Flat(Vector3 value) => new Vector3(value.x,0,value.z);

    // A footfall lies beside a chord: within `sideways` on the floor plan, and
    // the chord neither dips under the walked surface nor floats far above it.
    private static bool Beside(Vector3 point, Vector3 a, Vector3 b, float sideways, float headroom)
    {
        Vector3 ab=Flat(b-a); float length=ab.sqrMagnitude;
        float t=length>.000001f ? Mathf.Clamp01(Vector3.Dot(Flat(point-a),ab)/length) : 0;
        Vector3 closest=Vector3.Lerp(a,b,t);
        float rise=point.y-closest.y;
        return Flat(point-closest).magnitude<=sideways && rise<=headroom && -rise<=MergeDip;
    }

    private float RemainingRoute()
    {
        if (_cursor>=_route.Count) return 0;
        // Measured from where the camera actually is, so the arrival envelope
        // cannot reach zero short of the route's end.
        float result=(_route[_cursor]-Position).magnitude;
        for (int i=_cursor+1; i<_route.Count; i++) result+=(_route[i]-_route[i-1]).magnitude;
        return result;
    }

    private static float SegmentDistance(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab=b-a; float length=ab.sqrMagnitude;
        float t=length>.000001f ? Mathf.Clamp01(Vector3.Dot(point-a,ab)/length) : 0;
        return (a+ab*t-point).magnitude;
    }
}
