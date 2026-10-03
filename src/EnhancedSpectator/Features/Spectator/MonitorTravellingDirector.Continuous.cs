using System.Collections.Generic;
using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

internal sealed partial class MonitorTravellingDirector
{
    // Continuous cameras follow a live pose, not a sequence of stopping destinations.
    // References can suggest framing, but never authorize translation or consume the
    // speed needed to catch the subject. NavMesh suggestions never supply its height.
    private const int PursuitTrailCapacity=256;
    private const float PursuitAcceleration=160f, PursuitVerticalAcceleration=100f;
    private struct PursuitSample { internal Vector3 Focus; internal float Time, Arc; }
    private readonly PursuitSample[] _pursuitTrail=new PursuitSample[PursuitTrailCapacity];
    private readonly List<Vector3> _pursuitNavigation=new List<Vector3>(128);
    private int _pursuitTrailStart,_pursuitTrailCount;
    private bool _pursuitHasSample,_pursuitFalling,_pursuitHasFraming;
    private Vector3 _pursuitFocus,_pursuitGoal,_pursuitSide,_pursuitNavFocus,_pursuitNavVelocity,_pursuitEntrancePoint,_pursuitEntranceAxis;
    private float _pursuitArc,_pursuitHeightBias,_pursuitRadius=4.5f,_pursuitNextNavigation,_pursuitNavigationTime,_pursuitFallEvidence,_pursuitLandingRemaining,_pursuitObservedSpeed,_pursuitNextEntrance;
    private string _pursuitReference="none";
    private float _pursuitStandOff=4.5f,_pursuitEyeHeight=1.2f;
    private bool _pursuitHasGround;
    private Vector3 _pursuitGroundProbe,_pursuitDropAxis,_pursuitTrailHeading;
    private float _pursuitGroundHeight,_pursuitNextGround,_pursuitOccludedFor,_pursuitPlanarSpeed,_pursuitObservedPlanarSpeed;

    private void ClearContinuousPursuit()
    {
        _pursuitTrailStart=_pursuitTrailCount=0; _pursuitHasSample=_pursuitFalling=_pursuitHasFraming=false;
        _pursuitFocus=_pursuitGoal=_pursuitSide=_pursuitNavFocus=_pursuitNavVelocity=Vector3.zero;
        _pursuitEntrancePoint=_pursuitEntranceAxis=Vector3.zero; _pursuitNextEntrance=0;
        _pursuitArc=_pursuitHeightBias=_pursuitNextNavigation=_pursuitNavigationTime=_pursuitFallEvidence=_pursuitLandingRemaining=_pursuitObservedSpeed=0;
        _pursuitRadius=4.5f; _pursuitNavigation.Clear(); _pursuitReference="none";
        _pursuitStandOff=4.5f; _pursuitEyeHeight=1.2f;
        _pursuitHasGround=false; _pursuitGroundProbe=_pursuitDropAxis=_pursuitTrailHeading=Vector3.zero;
        _pursuitGroundHeight=_pursuitNextGround=_pursuitOccludedFor=_pursuitPlanarSpeed=_pursuitObservedPlanarSpeed=0;
        _rearPursuit=_rearHasBearing=false; _rearBearing=Vector3.zero; _rearGroundRate=_rearGroundSlopeRate=0;
    }

    private bool UpdateContinuousPursuit(IGameMonitorCameraAdapter adapter,Vector3 suppliedFocus,float now,float dt,
        int tier,float catchUpDistance,bool followBehind,out Vector3 position,out Quaternion rotation)
    {
        position=_position; rotation=_rotation;
        Vector3 focus=suppliedFocus;
        if(adapter is IGameMonitorSubjectMotionAdapter motion && motion.TryGetMonitorSubjectFocus(out var actual) && Finite(actual)) focus=actual;
        if(!Finite(focus)) return _ready;
        dt=float.IsNaN(dt) ? 0 : Mathf.Clamp(dt,0,.1f);
        ValidateArrivalSeed(adapter,focus);
        SamplePursuit(focus,now,dt);
        if(followBehind!=_rearPursuit) _rearHasBearing=false;
        _rearPursuit=followBehind;
        if(followBehind) UpdateRearBearing(adapter,dt);
        bool roomReady=adapter.TryGetMonitorRoom(out var room);
        if(!_ready) AcquirePursuitPose(adapter,focus,roomReady,room,now);
        if(!_ready) return false;
        if(dt<=.0001f) { position=_position; rotation=_rotation; return true; }

        bool sight=adapter.IsMonitorSightClear(_position,focus);
        _pursuitOccludedFor=sight ? 0 : _pursuitOccludedFor+dt;
        bool recoveringSight=_pursuitOccludedFor>.25f;
        float subjectDistance=(focus-_position).magnitude;
        Vector3 flatOffset=Vector3.ProjectOnPlane(_position-focus,Vector3.up);
        if(!_pursuitHasFraming)
        {
            _pursuitHasFraming=true;
            // A readable seed (especially a carrier exit) keeps its composition.
            // Braking reserve may grow with speed without imposing a new slow orbit.
            _pursuitStandOff=Mathf.Clamp(flatOffset.magnitude,_handoffRemaining>0 ? 1.2f : 2.5f,4.5f);
            float elevation=_position.y-focus.y;
            if(elevation>=.6f && elevation<=2.2f && elevation<flatOffset.magnitude*.85f)
                _pursuitEyeHeight=Mathf.Clamp(elevation*_pursuitStandOff/Mathf.Max(.6f,flatOffset.magnitude),.9f,2.2f);
            _pursuitRadius=_pursuitStandOff;
        }
        if(followBehind) _pursuitSide=-_rearBearing;
        else if(flatOffset.sqrMagnitude>.25f) _pursuitSide=flatOffset.normalized;
        if(_pursuitSide.sqrMagnitude<.5f)
        {
            _pursuitSide=Vector3.ProjectOnPlane(-(_rotation*Vector3.forward),Vector3.up).normalized;
            if(_pursuitSide.sqrMagnitude<.5f) _pursuitSide=Vector3.back;
        }
        float stoppingReserve=Mathf.Min(16f,_pursuitPlanarSpeed*_pursuitPlanarSpeed/(2f*PursuitAcceleration));
        float framingStandOff=_pursuitStandOff;
        if(followBehind && !_pursuitFalling)
        {
            float grade=Mathf.Abs(_focusVelocity.y)/Mathf.Max(1f,_pursuitPlanarSpeed);
            framingStandOff=Mathf.Lerp(framingStandOff,Mathf.Min(1.5f,framingStandOff),Mathf.InverseLerp(.2f,.6f,grade));
        }
        _pursuitRadius=Mathf.MoveTowards(_pursuitRadius,(!recoveringSight ? framingStandOff : Mathf.Min(1.5f,framingStandOff))+stoppingReserve,dt*16f);
        // Recover visibility by closing toward the actual player. Restoring sight must
        // not push a stopped camera back out through the wall it just left.
        float radius=_pursuitObservedSpeed>.65f ? _pursuitRadius
            : Mathf.Min(_pursuitRadius,Mathf.Max(!recoveringSight ? 2.5f : 1.2f,flatOffset.magnitude));
        _pursuitGoal=focus+_pursuitSide*radius+Vector3.up*_pursuitEyeHeight;
        if(now>=_pursuitNextEntrance)
        {
            _pursuitNextEntrance=now+.35f;
            if(!IndoorArrival(adapter,focus,out _pursuitEntrancePoint,out _pursuitEntranceAxis)) _pursuitEntranceAxis=Vector3.zero;
        }
        bool entranceFraming=_pursuitEntranceAxis.sqrMagnitude>.5f
            && Vector3.ProjectOnPlane(focus-_pursuitEntrancePoint,Vector3.up).sqrMagnitude<64f;
        if(entranceFraming)
        {
            // The braking reserve is a composition choice, not a reason to back
            // outside the entrance the subject just used. Keep the ideal pose inside;
            // actual translation still uses the same continuous velocity integrator.
            float inside=Vector3.Dot(_pursuitGoal-_pursuitEntrancePoint,_pursuitEntranceAxis);
            if(inside<.4f) _pursuitGoal+=_pursuitEntranceAxis*(.4f-inside);
        }

        bool grounded=false;
        if((Mathf.Abs(_focusVelocity.y)>.35f || Mathf.Abs(_pursuitHeightBias)>.05f || _pursuitFalling) && adapter is IGameMonitorTraversalAdapter traversal)
            grounded=traversal.IsMonitorSubjectGrounded;
        bool dropping=_focusVelocity.y< -1.5f && !grounded;
        bool wasFalling=_pursuitFalling;
        _pursuitLandingRemaining=_pursuitFalling && grounded ? .35f : Mathf.Max(0,_pursuitLandingRemaining-dt);
        _pursuitFallEvidence=dropping ? _pursuitFallEvidence+dt : 0;
        _pursuitFalling=_pursuitFallEvidence>=.045f || _pursuitFalling && !grounded && _focusVelocity.y< -.5f;
        if(_pursuitFalling && !wasFalling) _pursuitDropAxis=Vector3.ProjectOnPlane(focus-_position,Vector3.up).normalized;
        if(_pursuitFalling && !wasFalling || wasFalling && grounded)
        {
            // Airborne travel is not stair terrain. Do not revive its height when
            // the subject stops on the lower floor after landing.
            _pursuitTrailStart=_pursuitTrailCount=0; _pursuitArc=_pursuitHeightBias=0;
            _pursuitTrailHeading=Vector3.zero; _pursuitNavigation.Clear();
        }
        SamplePursuitGround(adapter,now,Mathf.Abs(_focusVelocity.y)>.35f || _motionVelocity.y<-.35f || _pursuitFalling);
        // A valid floor at a stale high seed must not hold a camera on the wrong
        // storey. Only a real observed drop can retain such a distant upper reference.
        if(_pursuitHasGround && !_pursuitFalling && _pursuitGroundHeight>focus.y+6f) _pursuitHasGround=false;

        _pursuitReference="live";
        Vector3 hint=default;
        Vector3 trailGoal; float trailAge;
        bool hasHint=followBehind ? TryRearTrailGoal(focus,now,radius,out trailGoal,out trailAge)
            : TryPursuitTrailGoal(focus,now,radius,out trailGoal,out trailAge);
        bool rearTrail=followBehind && hasHint;
        float heightBias=0;
        if(hasHint)
        {
            hint=trailGoal; _pursuitReference="observed-trail";
            if(followBehind)
            {
                // The old travelling shot trails the person's path through a bend.
                // A short lived rear rig target provides that feel without replaying
                // route points or granting navigation ownership of actual motion.
                _pursuitReference="rear-trail";
            }
            // Only a recent real trajectory can bias stair elevation. NavMesh's floor
            // projection is deliberately excluded, and a fall supersedes old stair Y.
            if(!_pursuitFalling && grounded && trailAge<=(followBehind ? 2.5f : .45f))
                heightBias=Mathf.Clamp(trailGoal.y-focus.y,-1.5f,4f);
        }
        else if(!_pursuitFalling && TryPursuitNavigation(adapter,focus,now,sight,subjectDistance,out var navigationGoal))
        { hint=navigationGoal; hasHint=true; _pursuitReference="navigation"; }
        if(entranceFraming && hasHint && Vector3.Dot(hint-_pursuitEntrancePoint,_pursuitEntranceAxis)<.4f) hasHint=false;
        if(rearTrail && hasHint) { _pursuitGoal.x=trailGoal.x; _pursuitGoal.z=trailGoal.z; }
        _pursuitHeightBias=Mathf.Lerp(_pursuitHeightBias,heightBias,1-Mathf.Exp(-dt/(_pursuitFalling?.04f:.12f)));
        _pursuitGoal.y+=_pursuitHeightBias;
        if(_pursuitFalling)
        {
            // Follow the real drop channel, rather than preserving a neighbouring
            // balcony offset. A camera below the jumper does not first chase upstairs.
            _pursuitGoal.x=focus.x; _pursuitGoal.z=focus.z;
            // While the camera is still above the observed ledge, aim beyond the
            // subject's drop line instead of asymptotically approaching its edge.
            if(_pursuitHasGround && _position.y>focus.y+.4f) _pursuitGoal+=_pursuitDropAxis*1.25f;
            if(_position.y<_pursuitGoal.y) _pursuitGoal.y=_position.y;
            hasHint=false; _pursuitReference="observed-fall";
        }
        bool rearTerrain=followBehind && grounded && !_pursuitFalling;
        float rearGround=0;
        bool rearObservedGround=rearTerrain && TryRearGroundHeight(now,out rearGround);
        if(rearObservedGround && _pursuitHasGround && Mathf.Abs(_pursuitGroundHeight-rearGround)>.66f) _pursuitHasGround=false;
        // Footfalls are discrete, while their local slope is continuous. Retain a
        // small frame-time reserve at step/landing transitions before integration.
        float rearBrakeMargin=.65f+(rearObservedGround ? Mathf.Min(.2f,dt*Mathf.Abs(_rearGroundRate)*.25f) : 0);
        // A real grounded visit remains a soft height reference even when optional
        // camera geometry probes refuse it. No reference may park horizontal motion.
        if(rearObservedGround) _pursuitGoal.y=Mathf.Max(_pursuitGoal.y,rearGround+.85f);
        if(_pursuitHasGround) _pursuitGoal.y=Mathf.Max(_pursuitGoal.y,_pursuitGroundHeight+(rearTerrain ? .85f : .55f));

        float threshold=float.IsNaN(catchUpDistance) ? 18f : Mathf.Clamp(catchUpDistance,6f,60f);
        float baseSpeed=12f*.6f/SpectatorFollowStabilizer.ResponseScale(tier);
        float speedCap=Mathf.Max(baseSpeed,_targetSpeed+2f);
        speedCap=Mathf.Max(speedCap,baseSpeed+Mathf.Max(0,_targetSpeed-9f)*1.6f*Mathf.InverseLerp(5f,16f,subjectDistance));
        speedCap=Mathf.Lerp(speedCap,Mathf.Max(speedCap,_targetSpeed+Mathf.Max(0,subjectDistance-threshold)/1.5f),
            Mathf.InverseLerp(threshold,threshold+12f,subjectDistance));
        bool rearFast=followBehind && _pursuitObservedPlanarSpeed>10f;
        if(rearFast) speedCap=Mathf.Max(speedCap,_pursuitObservedPlanarSpeed+2f);
        speedCap=Mathf.Min(PursuitSpeedLimit,speedCap);
        float acceleration=_pursuitFalling || _pursuitLandingRemaining>0 || rearFast ? PursuitAcceleration
            : Mathf.Lerp(80f,PursuitAcceleration,Mathf.Max(Mathf.InverseLerp(threshold,threshold+12f,subjectDistance),
                Mathf.InverseLerp(10f,18f,_targetSpeed)));
        Vector3 subjectVelocity=_focusVelocity;
        // Fast travel can start before the ordinary filtered response catches up.
        // Feed forward its measured planar speed; actual acceleration/turn limits
        // still apply, and vertical step noise never becomes a horizontal boost.
        if(rearFast)
        {
            Vector3 planar=_pursuitTrailHeading*_pursuitObservedPlanarSpeed;
            subjectVelocity.x=planar.x; subjectVelocity.z=planar.z;
        }
        if(_pursuitFalling && _position.y<focus.y+.8f) subjectVelocity.y=0;

        // Substeps bound braking error at low frame rates. Vertical motion is scalar:
        // a 180-degree horizontal turn can never interpolate through a downward axis.
        float left=dt;
        while(left>.00001f)
        {
            float step=Mathf.Min(left,1f/120f); left-=step;
            Vector3 error=_pursuitGoal-_position;
            float closingGain=_pursuitFalling && _pursuitHasGround ? 8f : 4f;
            Vector3 correction=Vector3.ProjectOnPlane(error,Vector3.up)*closingGain;
            float flatError=Vector3.ProjectOnPlane(error,Vector3.up).magnitude;
            correction=Vector3.ClampMagnitude(correction,Mathf.Sqrt(160f*flatError));
            correction.y=Mathf.Sign(error.y)*Mathf.Min(Mathf.Abs(error.y)*10f,Mathf.Sqrt(160f*Mathf.Abs(error.y)));
            Vector3 wanted=Vector3.ClampMagnitude(subjectVelocity+correction,speedCap);
            // Braking against the subject's real ground band is a scalar trajectory
            // reference, not collision clipping. It cannot create a wall slide or wait.
            float lower=focus.y-.4f;
            if(rearObservedGround) lower=Mathf.Max(lower,rearGround+rearBrakeMargin);
            if(_pursuitHasGround) lower=Mathf.Max(lower,_pursuitGroundHeight+(rearTerrain ? rearBrakeMargin : .35f));
            float downward=Mathf.Sqrt(180f*Mathf.Max(0,_position.y-lower));
            wanted.y=Mathf.Max(wanted.y,(rearObservedGround ? _rearGroundRate : 0)-downward);
            if(_pursuitFalling || _pursuitLandingRemaining>0)
                wanted.y=Mathf.Max(wanted.y,-Mathf.Sqrt(160f*Mathf.Max(0,_position.y-_pursuitGoal.y)));
            if(_pursuitFalling && _position.y<focus.y+.8f) wanted.y=Mathf.Min(0,wanted.y);
            Vector3 flatWanted=Vector3.ProjectOnPlane(wanted,Vector3.up);
            if(hasHint && subjectDistance>3f)
            {
                Vector3 radial=Vector3.ProjectOnPlane(focus-_position,Vector3.up).normalized;
                Vector3 lateral=Vector3.ProjectOnPlane(hint-_pursuitGoal,Vector3.up);
                lateral-=radial*Vector3.Dot(lateral,radial);
                float weight=1-Mathf.InverseLerp(threshold,threshold+12f,subjectDistance);
                Vector3 advised=flatWanted+Vector3.ClampMagnitude(lateral*2f,4f)*weight;
                float closing=Vector3.Dot(flatWanted,radial);
                Vector3 baseRadial=radial*closing;
                float budget=Mathf.Sqrt(Mathf.Max(0,speedCap*speedCap-wanted.y*wanted.y-closing*closing));
                flatWanted=baseRadial+Vector3.ClampMagnitude(advised-baseRadial,budget);
            }
            Vector3 velocityError=flatWanted+Vector3.up*wanted.y-_motionVelocity;
            float verticalShare=velocityError.sqrMagnitude>.000001f ? Mathf.Abs(velocityError.y)/velocityError.magnitude : 0;
            // Share the acceleration by the required velocity change. Giving Y the
            // entire budget first made an ordinary diagonal start dive, then turn.
            float nextY=Mathf.MoveTowards(_motionVelocity.y,wanted.y,Mathf.Min(PursuitVerticalAcceleration,acceleration*verticalShare)*step);
            float usedY=(nextY-_motionVelocity.y)/step;
            float horizontalAcceleration=Mathf.Sqrt(Mathf.Max(0,acceleration*acceleration-usedY*usedY));
            Vector3 currentFlat=Vector3.ProjectOnPlane(_motionVelocity,Vector3.up);
            Vector3 nextFlat=Vector3.MoveTowards(currentFlat,flatWanted,horizontalAcceleration*step);
            // At walking speed a bounded acceleration can still swing the heading
            // sharply. Limit only the planar turn; height and catch-up remain live.
            // Reducing this angle preserves the existing vector acceleration bound.
            if(currentFlat.sqrMagnitude>.25f && nextFlat.sqrMagnitude>.25f)
                nextFlat=Vector3.RotateTowards(currentFlat,nextFlat,12f*step,0).normalized*nextFlat.magnitude;
            // The split axes must also obey the ordinary speed envelope. When the
            // requested cap falls, retaining the prior speed here lets the bounded
            // acceleration brake it instead of an instantaneous velocity truncation.
            float actualCap=Mathf.Min(PursuitSpeedLimit,Mathf.Max(speedCap,_motionVelocity.magnitude));
            _motionVelocity=Vector3.ClampMagnitude(nextFlat+Vector3.up*nextY,actualCap);
            _position+=_motionVelocity*step;
        }
        _speed=_motionVelocity.magnitude; _speedVelocity=0;
        if(_speed>.001f) _travelDirection=_motionVelocity/_speed;
        _station=_position; _goal=_pursuitGoal; _goalFocus=focus; _goalRoom=roomReady ? room.Id : 0;
        _diagnosticRoomReady=roomReady; _diagnosticRoom=_goalRoom; _diagnosticSight=sight; _diagnosticNeed=false;
        _stagnantTime=0; _blockedTime=0; _handoffRemaining=Mathf.Max(0,_handoffRemaining-dt);
        if((_position-position).sqrMagnitude>.000001f) _lastMoveTime=now;
        if(DiagnosticsEnabled) _lastMotion="continuous-"+_pursuitReference;
        State=_speed>.08f || _targetSpeed>.35f ? MonitorTrackingState.Tracking : MonitorTrackingState.Holding;
        Aim(focus,dt); position=_position; rotation=_rotation; return true;
    }

    private void SamplePursuitGround(IGameMonitorCameraAdapter adapter,float now,bool useful)
    {
        if(!useful || adapter is not IGameMonitorGroundReferenceAdapter ground) { _pursuitHasGround=false; return; }
        if(now<_pursuitNextGround) return;
        _pursuitNextGround=now+.1f;
        float expected=_pursuitHasGround && Vector3.ProjectOnPlane(_position-_pursuitGroundProbe,Vector3.up).sqrMagnitude<9f
            ? _pursuitGroundHeight : _position.y-1.05f-_pursuitEyeHeight;
        if((_rearPursuit || _routePolicy) && TryRearGroundHeight(now,out float observedGround)) expected=observedGround;
        _pursuitHasGround=ground.TryGetMonitorGroundReference(_position,expected,out float height)
            && !float.IsNaN(height) && !float.IsInfinity(height) && Mathf.Abs(height-expected)<=.66f;
        if(_pursuitHasGround) { _pursuitGroundHeight=height; _pursuitGroundProbe=_position; }
    }

    private void AcquirePursuitPose(IGameMonitorCameraAdapter adapter,Vector3 focus,bool roomReady,MonitorRoom room,float now)
    {
        var world=adapter as IGameMonitorRouteAdapter;
        for(int i=0;i<4;i++)
        {
            float angle=_rearPursuit ? (i==0 ? 0 : i==1 ? -25f : i==2 ? 25f : 50f) : i*90f;
            Vector3 side=_rearPursuit ? -_rearBearing : Vector3.back;
            Vector3 candidate=focus+Quaternion.Euler(0,angle,0)*side*4.5f+Vector3.up*1.2f;
            if(!adapter.TryPlaceMonitorCamera(focus,candidate,out var placed) || !Finite(placed)
                || !InitialStationAllowed(adapter,world,focus,placed,roomReady,room)) continue;
            Seed(placed,MonitorCameraAim.Target(_rotation,focus-placed)); _seedUnchecked=false;
            _stationSince=now; ShotChanges++; return;
        }
        // Missing room data or every rejected reference probe must not strand the
        // initial camera outside the facility. The actual indoor subject is valid.
        Vector3 fallback=focus+Vector3.up*.9f;
        Seed(fallback,MonitorCameraAim.Target(_rotation,focus-fallback)); _seedUnchecked=false;
        _stationSince=now; ShotChanges++;
    }

    private void SamplePursuit(Vector3 focus,float now,float dt)
    {
        Vector3 displacement=_pursuitHasSample ? focus-_pursuitFocus : Vector3.zero;
        bool teleport=_pursuitHasSample && displacement.magnitude>Mathf.Max(2f,(PursuitSpeedLimit+4f)*dt);
        if(teleport)
        {
            _pursuitTrailStart=_pursuitTrailCount=0; _pursuitArc=0; _pursuitHeightBias=0;
            _pursuitNextEntrance=0;
            _pursuitHasGround=false; _pursuitNextGround=_pursuitPlanarSpeed=_pursuitObservedPlanarSpeed=0; _pursuitTrailHeading=Vector3.zero;
            _rearHasBearing=false;
            _pursuitNavigation.Clear(); _focusVelocity=Vector3.zero; _targetSpeed=_pursuitObservedSpeed=0;
        }
        else if(_pursuitHasSample && dt>.0001f)
        {
            Vector3 observed=Vector3.ClampMagnitude(displacement/dt,PursuitSpeedLimit);
            _pursuitObservedSpeed=observed.magnitude;
            _focusVelocity=Vector3.Lerp(_focusVelocity,observed,1-Mathf.Exp(-dt/.045f));
            _targetSpeed=Mathf.Lerp(_targetSpeed,observed.magnitude,1-Mathf.Exp(-dt/(observed.magnitude>_targetSpeed?.08f:.18f)));
            Vector3 planar=Vector3.ProjectOnPlane(observed,Vector3.up);
            _pursuitObservedPlanarSpeed=planar.magnitude;
            _pursuitPlanarSpeed=Mathf.Lerp(_pursuitPlanarSpeed,planar.magnitude,1-Mathf.Exp(-dt/(planar.magnitude>_pursuitPlanarSpeed?.08f:.18f)));
            if(planar.sqrMagnitude>.25f) _pursuitTrailHeading=planar.normalized;
        }
        _pursuitFocus=focus; _pursuitHasSample=true;
        if(_pursuitTrailCount>0 && (focus-PursuitTrail(_pursuitTrailCount-1).Focus).sqrMagnitude<.0144f) return;
        if(_pursuitTrailCount>0) _pursuitArc+=(focus-PursuitTrail(_pursuitTrailCount-1).Focus).magnitude;
        if(_pursuitTrailCount==PursuitTrailCapacity) { _pursuitTrailStart=(_pursuitTrailStart+1)%PursuitTrailCapacity; _pursuitTrailCount--; }
        int index=(_pursuitTrailStart+_pursuitTrailCount++)%PursuitTrailCapacity;
        _pursuitTrail[index]=new PursuitSample { Focus=focus,Time=now,Arc=_pursuitArc };
    }
    private PursuitSample PursuitTrail(int index) => _pursuitTrail[(_pursuitTrailStart+index)%PursuitTrailCapacity];

    private bool TryPursuitTrailGoal(Vector3 focus,float now,float radius,out Vector3 goal,out float age)
    {
        goal=default; age=0;
        bool paused=_pursuitObservedSpeed<=.65f;
        Vector3 heading=paused ? _pursuitTrailHeading : Vector3.ProjectOnPlane(_focusVelocity,Vector3.up).normalized;
        if(_pursuitTrailCount<2 || heading.sqrMagnitude<.5f || Vector3.Dot(_pursuitSide,heading)>.25f) return false;
        // Select by ordered arc/time, never by XZ nearest point: a folded staircase or
        // a return through an old room cannot select an earlier visit on another floor.
        float arc=_pursuitArc-(paused ? radius : Mathf.Min(radius,Mathf.Max(.8f,_targetSpeed*.35f)));
        // A player who stops on a staircase still supplies the same local terrain
        // evidence. Keep that visit's height reference while its position is held;
        // moving/teleporting immediately returns to ordinary time-limited history.
        float referenceTime=paused ? PursuitTrail(_pursuitTrailCount-1).Time : now;
        for(int i=_pursuitTrailCount-2;i>=0;i--)
        {
            var a=PursuitTrail(i); var b=PursuitTrail(i+1);
            if(a.Arc>arc) continue;
            if(b.Arc<arc || referenceTime-a.Time>.6f) return false;
            float t=Mathf.InverseLerp(a.Arc,b.Arc,arc);
            goal=Vector3.Lerp(a.Focus,b.Focus,t); age=referenceTime-Mathf.Lerp(a.Time,b.Time,t);
            // The same live braking reserve selects this arc. A fixed eight-metre
            // cutoff discarded valid stair history just when fast descent needed it.
            return (goal-focus).sqrMagnitude<=radius*radius+1f;
        }
        return false;
    }

    private bool TryPursuitNavigation(IGameMonitorCameraAdapter adapter,Vector3 focus,float now,bool sight,float distance,out Vector3 goal)
    {
        goal=default;
        if(sight || distance<6f || adapter is not IGameMonitorRouteAdapter world) return false;
        if(now>=_pursuitNextNavigation)
        {
            _pursuitNextNavigation=now+.35f; _pursuitNavigation.Clear();
            _pursuitNavFocus=focus; _pursuitNavVelocity=_focusVelocity; _pursuitNavigationTime=now;
            if(!world.TryGetMonitorNavRoute(_position,_pursuitGoal,_pursuitNavigation)
                || !ValidPursuitNavigation(focus)) _pursuitNavigation.Clear();
        }
        if(_pursuitNavigation.Count<2 || now-_pursuitNavigationTime>.55f || (focus-_pursuitNavFocus).sqrMagnitude>4f
            || Vector3.Dot(_pursuitNavVelocity,_focusVelocity)<-1f) return false;
        Vector3 radial=Vector3.ProjectOnPlane(focus-_position,Vector3.up).normalized;
        float nearest=float.PositiveInfinity;
        for(int i=1;i<_pursuitNavigation.Count;i++)
        {
            Vector3 a=_pursuitNavigation[i-1],b=_pursuitNavigation[i],leg=b-a;
            float t=leg.sqrMagnitude>.0001f ? Mathf.Clamp01(Vector3.Dot(_position-a,leg)/leg.sqrMagnitude) : 1;
            Vector3 point=Vector3.Lerp(a,b,t),flat=Vector3.ProjectOnPlane(b-_position,Vector3.up);
            float gap=(point-_position).sqrMagnitude;
            if(gap>nearest || flat.sqrMagnitude<.01f || Vector3.Dot(flat.normalized,radial)<.5f) continue;
            nearest=gap; goal=point+leg.normalized*Mathf.Min(2.5f,(b-point).magnitude);
        }
        return nearest<25f;
    }
    private bool ValidPursuitNavigation(Vector3 focus)
    {
        if(_pursuitNavigation.Count<2 || _pursuitNavigation.Count>128 || !Finite(_pursuitNavigation[0])
            || (_pursuitNavigation[0]-_position).sqrMagnitude>4f
            || (_pursuitNavigation[_pursuitNavigation.Count-1]-_pursuitGoal).sqrMagnitude>4f) return false;
        float length=0,distance=(focus-_position).magnitude;
        for(int i=1;i<_pursuitNavigation.Count;i++)
        {
            Vector3 point=_pursuitNavigation[i];
            if(!Finite(point) || MonitorTravelPath.DistanceToSegment(point,_position,focus)>Mathf.Min(6f,2f+distance*.15f)) return false;
            length+=(point-_pursuitNavigation[i-1]).magnitude;
        }
        return length<=distance*1.35f+8f;
    }
    private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
        && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
}
