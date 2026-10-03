using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Missing geometry is recoverable. Only the controller decides when the mode is no longer eligible.</summary>
internal sealed class MonitorCameraDirector
{
    internal const float GraceSeconds = 1f;
    internal const int ProbeBudget = 4;
    private bool _hasShot, _hasPose, _hasRoom, _temporary, _searching;
    private bool _nearMainEntrance;
    private int _roomId, _pendingRoom, _style, _candidateIndex;
    private float _pendingSince, _blockedSeconds, _nextProbe, _lastRoomTime, _shotSince, _nextRecovery;
    private float _bestScore;
    private bool _hasFocus;
    private Vector3 _lastFocus, _focusVelocity;
    private readonly MonitorSubjectPacing _pacing=new MonitorSubjectPacing();
    private readonly MonitorCarrierCamera _carrier=new MonitorCarrierCamera();
    private bool _carrierHeld;
    internal bool EnteringCarrier => _carrierHeld && _carrier.Entering;
    internal bool LeavingCarrier => _leavingCarrier;
    internal bool InCabin => _carrierHeld && _carrier.InCabin;
    internal bool CabinLensReady => _carrierHeld && _carrier.CabinLensReady;
    private bool _carrierRoof,_exitRoof,_nearElevator,_approachCrossing,_carrierNoCollision,_exitNoCollision;
    private Bounds _carrierBounds;
    private Vector3 _approachDirection,_exitEdge;
    private Vector3 _approachCenter;
    private bool _approachHandled,_localStation;
    private float _resumeBlend;
    private Vector3 _resumeVelocity,_rideVelocity;
    private bool _leavingCarrier;
    private float _exitElapsed,_exitGrounded;
    private Vector3 _carrierFocus,_exitFocus,_exitStart,_exitVelocity,_exitMotion;
    private readonly MonitorTravellingDirector _travelling = new MonitorTravellingDirector();
    private readonly MonitorCameraAgent _agent = new MonitorCameraAgent();
    private bool _agentActive;
    internal bool CanTransferTarget => _agentActive && !_carrierHeld && !_leavingCarrier;

    internal void BeginTargetTransfer(Vector3 position,Quaternion rotation,Vector3 focus)
    {
        // Vanilla holds the last camera still before its automatic death switch.
        Vector3 velocity=Vector3.zero;
        _agent.Seed(position,rotation,velocity,focus);
        _agent.BeginSubjectTransition(focus);
        _agentActive=true; _position=position; _rotation=rotation;
        _style=2; _hasShot=_hasPose=true; _transitioning=false; TransitionOpacity=0;
    }
    private bool _veronicaPreparing, _veronicaAdvancing;
    private float _veronicaRetry, _recedingTime, _unreadableTime;
    private bool _readablePicture;
    private float _stationContactTime;

    internal bool IsTravelling => !_carrierHeld && (_agentActive ? _agent.IsTravelling : MonitorCameraStyles.IsContinuous(_style) ? _travelling.IsTravelling : _veronicaAdvancing);
    private bool _transitioning, _fadeTransition, _transitionSwapped;
    private float _transitionTime, _transitionDuration;
    private Vector3 _railVelocity;
    private float _settleUntil;
    internal float TransitionOpacity { get; private set; }
    internal int ShotChanges { get; private set; }
    private MonitorRoom _room, _shotRoom, _searchRoom;
    private Vector3 _station, _stationFocus, _position, _best;
    private Quaternion _rotation;
    internal MonitorTrackingState State { get; private set; }
    internal bool DiagnosticsEnabled { get; set; }
    internal string Describe(float now) => _agentActive && !_carrierHeld && !_leavingCarrier ? _agent.Describe(now) : _carrierHeld ? $"carrier style={_style},entering={_carrier.Entering},relative-frame=True,changes={ShotChanges},{_carrier.Describe()}" : _leavingCarrier ? $"carrier-exit style={_style},roof={_exitRoof},elapsed={_exitElapsed:F2},grounded={_exitGrounded:F2},velocity={_exitMotion:F2}" : (MonitorCameraStyles.IsContinuous(_style) || _veronicaPreparing || _veronicaAdvancing) ? $"style={_style},"+_travelling.Describe(now)
        : $"station carrierExit={_leavingCarrier},resuming={_resumeBlend>0},style={_style},room={_roomId},pending={_pendingRoom},entrance={_nearElevator}/{_approachHandled},localStation={_localStation},search={_searching}:{_candidateIndex},occluded={_blockedSeconds:F2},unreadable={_unreadableTime:F2},receding={_recedingTime:F2},transition={_transitioning},fade={_fadeTransition},shotAge={now-_shotSince:F2},changes={ShotChanges}";

    internal void SeedTravelling(IGameMonitorCameraAdapter world,Vector3 position,Quaternion rotation,int style=2)
    {
        if(_hasShot || !world.IsMonitorTargetIndoors || !world.IsMonitorPathClear(position,position)) return;
        if(world is IGameMonitorRouteAdapter routes && !routes.TryGetRouteRoom(position,out _)) return;
        _position=position; _rotation=rotation; _hasShot=_hasPose=true;
        _travelling.Clear(); _travelling.Seed(position,rotation); _style=MonitorCameraStyles.IsContinuous(style) ? style : 2;
        if (_style==2 && world is IGameMonitorAgentAdapter) { _agent.Seed(position,rotation); _agentActive=true; }
    }

    internal void Clear()
    {
        _veronicaPreparing=_veronicaAdvancing=false; _veronicaRetry=_recedingTime=_unreadableTime=0;
        _stationContactTime=0;
        _travelling.Clear(); _pacing.Clear(); _carrier.Clear(); _carrierHeld=false;
        _agent.Clear(); _agentActive=false;
        _leavingCarrier=false; _exitElapsed=_exitGrounded=0; _exitVelocity=_exitMotion=Vector3.zero;
        _carrierRoof=_exitRoof=_nearElevator=_approachCrossing=false; _resumeBlend=0; _exitEdge=Vector3.zero;
        _resumeVelocity=_rideVelocity=Vector3.zero;
        _carrierNoCollision=_exitNoCollision=false;
        _approachHandled=_localStation=false;
        _hasShot = _hasPose = _hasRoom = _temporary = _searching = false;
        _nearMainEntrance=false;
        _roomId = _pendingRoom = _candidateIndex = 0;
        _blockedSeconds = _nextProbe = _pendingSince = _nextRecovery = 0;
        _transitioning=false; TransitionOpacity=0;
        _settleUntil=0; _railVelocity=Vector3.zero;
        _hasFocus=false; _focusVelocity=Vector3.zero; ShotChanges=0;
        State = MonitorTrackingState.Searching;
    }

    internal bool TryUpdate(IGameMonitorCameraAdapter world, Vector3 focus, float now, float dt,
        out Vector3 position, out Quaternion rotation, int style = 0, int followSpeed = 1,float travellingCatchUpDistance=18f)
    {
        position = default; rotation = Quaternion.identity;
        dt=Mathf.Clamp(dt,0,.1f);
        TransitionOpacity=Mathf.Max(0,TransitionOpacity-dt/.22f);
        style = MonitorCameraStyles.Valid(style);
        if(_carrier.TryUpdate(world,focus,dt,style,_hasShot,_position,_rotation,out var ridePosition,out var rideRotation))
        {
            if(dt>.0001f && _carrierHeld) _rideVelocity=Vector3.ClampMagnitude((ridePosition-_position)/dt,10f);
            else if(!_carrierHeld) _rideVelocity=Vector3.zero;
            _veronicaPreparing=_veronicaAdvancing=false;
            _carrierHeld=true; _hasShot=_hasPose=true; _position=position=ridePosition; _rotation=rotation=rideRotation;
            _carrierFocus=focus; _leavingCarrier=false;
            _carrierRoof=_carrier.OnRoof; _carrierBounds=_carrier.Footprint;
            _carrierNoCollision=_carrier.IgnoresCollisions;
            _resumeBlend=0;
            _blockedSeconds=_unreadableTime=_recedingTime=_stationContactTime=0;
            _style=style; _transitioning=false; TransitionOpacity=_carrier.TransitionOpacity; _searching=false;
            State=MonitorTrackingState.Tracking; return true;
        }
        if(_carrierHeld)
        {
            // Detach from the lift, retaining the current side and pose through the jump/landing.
            // Do not immediately mark this as a temporary station and search for an unrelated corner.
            _carrierHeld=false; _leavingCarrier=true; _exitElapsed=_exitGrounded=0;
            _exitStart=_position; _exitFocus=_carrierFocus; _exitVelocity=_exitMotion=_rideVelocity;
            _exitRoof=_carrierRoof; _exitEdge=Vector3.zero;
            _exitNoCollision=_carrierNoCollision;
            _searching=_transitioning=false; TransitionOpacity=0;
        }
        if(_leavingCarrier)
        {
            if(UpdateCarrierExit(world,focus,dt))
            {
                position=_position; rotation=_rotation; State=MonitorTrackingState.Tracking; return true;
            }
            _leavingCarrier=false; _travelling.Clear(); _travelling.Seed(_position,_rotation,true,_exitMotion,focus);
            if (style==2 && world is IGameMonitorAgentAdapter) { _agent.Seed(_position,_rotation,_exitMotion,focus); _agentActive=true; }
            _station=_position; _stationFocus=focus; _hasFocus=false;
            _temporary=!world.TryGetMonitorRoom(out _shotRoom);
            if(!_temporary) { _roomId=_shotRoom.Id; _room=_shotRoom; _hasRoom=true; _lastRoomTime=now; }
            _pacing.Clear(); _nextProbe=now+.7f; _settleUntil=now+1.2f; _shotSince=now;
            _blockedSeconds=_unreadableTime=_recedingTime=_stationContactTime=0;
            _railVelocity=Vector3.zero; _resumeVelocity=Vector3.ProjectOnPlane(_exitMotion,Vector3.up); _resumeBlend=1.2f;
            // UpdateCarrierExit already moved this frame; normal ownership begins next frame.
            position=_position; rotation=_rotation; State=MonitorTrackingState.Tracking; return true;
        }
        if (MonitorCameraStyles.IsContinuous(style))
        {
            _resumeBlend=0; _resumeVelocity=Vector3.zero;
            if (style==2 && world is IGameMonitorAgentAdapter)
            {
                if (!_agentActive)
                {
                    _agent.Clear(); if (_hasPose) _agent.Seed(_position,_rotation,_style==3 ? _travelling.Velocity : Vector3.zero,focus);
                    _agentActive=true;
                }
                _agent.DiagnosticsEnabled=DiagnosticsEnabled;
                _veronicaPreparing=_veronicaAdvancing=false;
                _style=style; _transitioning=false; TransitionOpacity=0;
                bool updated=_agent.TryUpdate(world,focus,now,dt,followSpeed,out position,out rotation,travellingCatchUpDistance);
                _hasShot=updated; _hasPose|=updated; _position=position; _rotation=rotation;
                State=_agent.State; ShotChanges=_agent.ShotChanges;
                return updated;
            }
            if (_agentActive)
            {
                _travelling.Clear(); _travelling.Seed(_position,_rotation,false,_agent.Velocity,focus,true);
                _agent.Clear(); _agentActive=false;
            }
            _travelling.DiagnosticsEnabled=DiagnosticsEnabled;
            if(!MonitorCameraStyles.IsContinuous(_style)) { _travelling.Clear(); if(_hasShot) _travelling.Seed(_position,_rotation); }
            else if(_style!=style) _travelling.ChangeTravellingPolicy(style==2,focus);
            _veronicaPreparing=_veronicaAdvancing=false;
            _style=style; _transitioning=false; TransitionOpacity=0;
            bool tracking=style==2
                ? _travelling.TryUpdateTravelling(world,focus,now,dt,followSpeed,out position,out rotation,travellingCatchUpDistance)
                : _travelling.TryUpdate(world,focus,now,dt,followSpeed,out position,out rotation,travellingCatchUpDistance);
            _hasShot=tracking; _hasPose|=tracking; _position=position; _rotation=rotation;
            State=_travelling.State; ShotChanges=_travelling.ShotChanges;
            return tracking;
        }
        bool styleChanged=_style!=style;
        if (_agentActive) { _agent.Clear(); _agentActive=false; }
        if (styleChanged)
        {
            // Retain a safe picture while the new style acquires a station, rather than flashing the eye fallback.
            _veronicaPreparing=_veronicaAdvancing=false; _travelling.Clear();
            _pacing.Clear();
            _style = style; _temporary = _hasShot; _searching = false; _nextProbe = 0;
            _transitioning=false;
        }
        if (!world.IsMonitorTargetIndoors) { Clear(); return false; }
        if(style==1 && (_veronicaPreparing || _veronicaAdvancing))
        {
            _travelling.DiagnosticsEnabled=DiagnosticsEnabled;
            _travelling.TryUpdate(world,focus,now,dt,followSpeed,out var moved,out var aimed);
            _position=position=moved; _rotation=rotation=aimed; TransitionOpacity=0;
            if(_veronicaPreparing)
            {
                if(_travelling.Preparation==MonitorPushPreparation.Ready)
                { _travelling.CommitPush(); _veronicaPreparing=false; _veronicaAdvancing=true; }
                else if(_travelling.Preparation==MonitorPushPreparation.Rejected)
                { _veronicaPreparing=false; _veronicaRetry=now+2f; _nextProbe=now; }
                else { State=MonitorTrackingState.Holding; return true; }
                if(_veronicaAdvancing) { State=MonitorTrackingState.Tracking; return true; }
            }
            else
            {
                if(_travelling.PushSettled)
                {
                    _veronicaAdvancing=false; _station=_position; _stationFocus=focus; _localStation=true;
                    _stationContactTime=0;
                    _temporary=false; _hasShot=_hasPose=true; _blockedSeconds=0; _shotSince=now;
                    _lastFocus=focus; _hasFocus=true; _focusVelocity=Vector3.zero;
                    _settleUntil=now+2f; _veronicaRetry=now+2f; _nextProbe=now+2f;
                }
                State=_travelling.State; return true;
            }
        }
        Bounds approach=default;
        _nearElevator=world is IGameMonitorEntranceAdapter entrance && entrance.TryGetMonitorElevatorApproach(out approach);
        Vector3 entryPoint=default,entryAxis=default;
        _nearMainEntrance=world is IGameMonitorEntranceAdapter doors && doors.TryGetMonitorMainEntrance(out entryPoint,out entryAxis);
        _approachCrossing=false;
        _approachDirection=Vector3.zero;
        if(!_nearElevator || (approach.center-_approachCenter).sqrMagnitude>9f) _approachHandled=false;
        if(_nearElevator) _approachCenter=approach.center;
        if(_nearElevator)
        { _approachDirection=approach.center-focus; _approachDirection.y=0; _approachDirection.Normalize(); }
        if(_nearMainEntrance && !_nearElevator)
        { _approachDirection=focus-entryPoint; _approachDirection.y=0; if(_approachDirection.sqrMagnitude<.36f) _approachDirection=Vector3.ProjectOnPlane(entryAxis,Vector3.up); _approachDirection.Normalize(); }
        _pacing.Sample(focus,dt);
        bool freshRoom = world.TryGetMonitorRoom(out var room);
        if (freshRoom) { _room = room; _hasRoom = true; _lastRoomTime = now; }
        bool usableRoom = _hasRoom && (freshRoom || now - _lastRoomTime <= GraceSeconds);
        room = _room;
        // Transport a retained station continuously with sustained vertical travel (e.g. lifts).
        // Horizontal room corners remain anchored. Height alone must never trigger repeated cuts.
        if (_hasShot && _hasFocus && !_transitioning)
        {
            float dy=focus.y-_lastFocus.y;
            if(Mathf.Abs(dy)<2f)
            {
                Vector3 shift=Vector3.up*dy;
                _station+=shift; _stationFocus+=shift;
                Vector3 transported=_position+shift;
                if(style==1 || _resumeBlend>0 || (world.IsMonitorPathClear(_position,transported) && world.IsMonitorSightClear(transported,focus))) _position=transported;
            }
        }
        if (_hasFocus && dt > .0001f)
        {
            Vector3 velocity=Vector3.ClampMagnitude((focus-_lastFocus)/dt,12f); velocity.y=0;
            _focusVelocity=Vector3.Lerp(_focusVelocity,velocity,MonitorCameraRules.Blend(dt,.12f));
        }
        _lastFocus=focus; _hasFocus=true;
        if(!_hasShot && _hasPose && world.IsMonitorPathClear(_position,_position)
            && world.IsMonitorSightClear(_position,focus)
            && (_position-focus).sqrMagnitude<=272.25f)
        {
            // Recover the SAME picture before searching for a replacement. Otherwise every
            // door opening after a brief loss creates another entry/cut even for a still player.
            _hasShot=true; _temporary=false; _searching=false; _blockedSeconds=0;
            _station=_position; _stationFocus=focus; _localStation=true;
            _nextProbe=Mathf.Max(_nextProbe,now+.5f);
        }
        bool advancing=_resumeBlend>0;
        bool stationClear=advancing || world.IsMonitorPathClear(_position,_position);
        _stationContactTime=Mathf.Clamp(_stationContactTime+(stationClear ? -dt : dt),0,.3f);
        bool safe = _hasShot && _stationContactTime<.25f;
        if (!safe && !_transitioning) { if (_hasShot) TransitionOpacity=1; _hasShot=false; _transitioning=false; }
        var framing=Frame(world,focus);
        bool clear = _hasShot && framing.Visible;
        _readablePicture=_hasShot && framing.Readable;
        _unreadableTime=_readablePicture ? 0 : _unreadableTime+dt;
        Vector3 radial=focus-_position; radial.y=0;
        _recedingTime=clear && Vector3.Dot(_focusVelocity,radial.normalized)>.6f ? _recedingTime+dt : 0;
        _blockedSeconds = clear ? 0 : _blockedSeconds + dt;

        // A successfully placed fallback is a usable local station even without room metadata.
        // Do not keep it disposable until the player reaches a tile: crossing its 0.6m height
        // offset used to invalidate it and repeatedly cut to another identical fallback.
        if(_temporary && _localStation && safe && clear && !_transitioning && !styleChanged)
        {
            _temporary=false; _station=_position; _stationFocus=focus;
            if(usableRoom) { _shotRoom=room; _roomId=room.Id; }
            _nextProbe=Mathf.Max(_nextProbe,now+.7f);
        }

        bool searched = false;
        if (usableRoom)
        {
            if (_pendingRoom != room.Id) { _pendingRoom = room.Id; _pendingSince = now; }
            bool confirmedRoom = _roomId != room.Id && now - _pendingSince >= MonitorCameraRules.RoomDelay;
            // Tile identity is bookkeeping, not a cut trigger while the existing picture remains usable.
            bool changedRoom = confirmedRoom && (!clear || (_position-focus).magnitude > 12f);
            if (confirmedRoom && clear && !changedRoom && !_transitioning && !_temporary)
            { _roomId=room.Id; _shotRoom=room; _station=_position; _stationFocus=focus; }
            // A predicted loss or tile/zone boundary is not an actual loss of the picture.
            // Keep a generous usable envelope; only persistent occlusion or framing exhaustion
            // requests a new station. The camera continues aiming/railing during this grace.
            float shotDistance=(_position-focus).magnitude;
            float holdDistance=style==0 ? 15f : 16.5f;
            // Proximity is a tracking/aiming problem, not an automatic edit. A visible subject
            // may pass either side of (or directly underneath) a safe station without a cut.
            bool usablePicture=_readablePicture;
            if(_temporary && (_localStation || _nearElevator) && safe && usablePicture && !_transitioning)
            {
                _temporary=false; _station=_position; _stationFocus=focus; _shotRoom=room; _roomId=room.Id;
                _localStation=true;
                _nextProbe=Mathf.Max(_nextProbe,now+.7f);
            }
            Vector3 facing=_position-focus; facing.y=0;
            // Passing a frontal station is not a loss of the picture. Keep aiming/sliding
            // while it remains readable instead of bypassing the normal hold with a covered cut.
            _approachCrossing=_nearElevator && !_approachHandled && !usablePicture && clear && facing.magnitude<3f
                && Vector3.Dot(facing.normalized,_approachDirection)>.65f && Vector3.Dot(_focusVelocity,_approachDirection)>.4f;
            bool lostSubject=!clear && _blockedSeconds>=.45f;
            bool needsShot = _temporary || !_hasShot ||
                (!usablePicture && (lostSubject || _unreadableTime>=.65f));
            bool proactive=style==1 && usablePicture && _recedingTime>=.5f && shotDistance>7f;
            // Allow the completed shot to settle through brief door-post/prop occlusion. A genuinely
            // lost subject still forces recovery; a cooldown must never hide persistent obstruction.
            if(_hasShot && !_temporary && now<_settleUntil && !lostSubject
                && (_position-focus).magnitude<MonitorCameraRules.MaximumDistance) needsShot=false;
            if (_searching && _searchRoom.Id != room.Id) _searching = false;
            if (!needsShot) _searching = false;
            if(style==1 && (needsShot || proactive) && _hasShot && !_temporary && !_transitioning && !_searching
                && now>=_veronicaRetry && now>=_settleUntil)
            {
                _travelling.PreparePush(_position,_rotation,focus); _veronicaPreparing=true;
                position=_position; rotation=_rotation; AimAtPlayer(focus,dt,followSpeed); rotation=_rotation;
                State=MonitorTrackingState.Holding; return true;
            }
            if (needsShot && (!_hasShot || _temporary || lostSubject || now-_shotSince>=3f) && !_transitioning && !_searching && now >= _nextProbe && (!_hasShot || !clear || now-_shotSince>=3.5f || _temporary || _approachCrossing || shotDistance>holdDistance))
            {
                _searching = true; _candidateIndex = 0; _bestScore = float.NegativeInfinity; _searchRoom = room;
            }
            if (_searching) { Search(world, focus, now); searched = true; }
        }
        else _searching = false;

        // Bridge brief occlusion/room gaps with a safe old station, never a wall-embedded camera.
        clear = _hasShot && Frame(world,focus).Visible;
        if (!_transitioning && _hasShot && !clear && _blockedSeconds > GraceSeconds+3f) _hasShot = false;
        // Distance alone never invalidates a still-readable picture.
        if (!_hasShot && !_searching && !searched && now >= _nextRecovery)
        {
            _nextRecovery = now + MonitorCameraRules.ProbeInterval;
            for (int i = 0; i < ProbeBudget; i++)
            {
                // Narrow platforms/roofs may have no floor beneath the normal four-metre station.
                Vector3 offset = i switch { 0 => new Vector3(1.8f, .6f, 0), 1 => new Vector3(-1.8f, .6f, 0),
                    2 => new Vector3(0, .6f, 1.8f), _ => new Vector3(0, .6f, -1.8f) };
                if (!world.TryPlaceMonitorCamera(focus, focus + offset, out var p)) continue;
                SetShot(world, p, focus, now, room, temporary: true); break;
            }
        }
        if (!_hasShot)
        {
            State=MonitorTrackingState.Recovering;
            if(!_hasPose || !world.IsMonitorPathClear(_position,_position)) return false;
            // Once acquired, a room view remains the presentation owner. A temporarily closed
            // door or failed probe must not alternate it with the player's eye camera each scan.
            TransitionOpacity=0; AimAtPlayer(focus,dt,followSpeed);
            position=_position; rotation=_rotation; return true;
        }

        Vector3 desired=_station;
        if (_transitioning)
        {
            _transitionTime += dt;
            if (_fadeTransition)
            {
                AimAtPlayer(focus,dt,followSpeed);
                // The committed landing is immutable during this transition.
                float outTime=_transitionDuration*.4f;
                TransitionOpacity=_transitionTime<outTime ? Mathf.SmoothStep(0,1,_transitionTime/outTime)
                    : 1-Mathf.SmoothStep(0,1,(_transitionTime-outTime)/(_transitionDuration-outTime));
                if (!_transitionSwapped && _transitionTime>=outTime)
                {
                    // A rail evaluated in the dungeon tile can push an entrance shot back into
                    // the same frontal close-up. A transition has one fixed destination.
                    Vector3 destination=_station;
                    if(AcceptStation(world,destination,focus) && world.IsMonitorSightClear(destination,focus))
                    {
                        _position=_station=destination; _stationFocus=focus;
                        _stationContactTime=0;
                        if(usableRoom) { _shotRoom=room; _roomId=room.Id; }
                        _rotation=Quaternion.LookRotation(focus-destination,Vector3.up);
                        _blockedSeconds=0;
                    }
                    else
                    {
                        // Failed replacement is not another transition request. Keep the old
                        // usable pose and rebuild only after actual loss of framing/visibility.
                        _station=_position; _stationFocus=focus; _localStation=true;
                        _temporary=!world.IsMonitorSightClear(_position,focus);
                    }
                    // Swap exactly once, fully covered. Never reposition during the reveal half.
                    TransitionOpacity=1; _transitionSwapped=true; _transitionTime=outTime;
                }
            }
            if (_transitionTime>=_transitionDuration)
            {
                _transitioning=false; TransitionOpacity=0; _blockedSeconds=0;
                if(!_fadeTransition) { _station=_position; _stationFocus=focus; }
                _shotSince=now; _settleUntil=now+.7f; _nextProbe=Mathf.Max(_nextProbe,now+.2f);
            }
            position=_position; rotation=_rotation; State=MonitorTrackingState.Tracking;
            return true;
        }
        Vector3 step;
        bool permissiveMotion=_resumeBlend>0;
        if(_resumeBlend>0)
        {
            float previous=_resumeBlend/1.2f;
            _resumeBlend=Mathf.Max(0,_resumeBlend-dt);
            float remaining=_resumeBlend/1.2f;
            // Integrate a smooth braking envelope. Pulling toward the handoff origin
            // with inherited velocity first overshot it, then visibly reversed direction.
            float integral=1.2f*((previous*previous*previous-.5f*previous*previous*previous*previous)
                -(remaining*remaining*remaining-.5f*remaining*remaining*remaining*remaining));
            step=_position+_resumeVelocity*integral; _station=step;
        }
        else step=Vector3.Lerp(_position,desired,MonitorCameraRules.Blend(dt,.55f*SpectatorFollowStabilizer.ResponseScale(followSpeed)));
        if (permissiveMotion || (world.IsMonitorPathClear(_position, step) && world.IsMonitorSightClear(step, focus))) _position = step;
        else { _railVelocity=Vector3.zero; }
        AimAtPlayer(focus,dt,followSpeed);
        State = _temporary ? MonitorTrackingState.Recovering
            : !freshRoom || !clear ? MonitorTrackingState.Holding : MonitorTrackingState.Tracking;
        position = _position; rotation = _rotation;
        return true;
    }

    private bool UpdateCarrierExit(IGameMonitorCameraAdapter world,Vector3 focus,float dt)
    {
        _exitElapsed+=dt;
        if(!world.IsMonitorTargetIndoors || (focus-_exitFocus).sqrMagnitude>400f) return false;
        bool supported=world is IGameMonitorSupportAdapter support && support.TryGetMonitorSupport(out _);
        // A single missing foot sample must not restart the landing blend on uneven floors.
        _exitGrounded=supported ? _exitGrounded+dt : Mathf.Max(0,_exitGrounded-dt*2f);
        Vector3 desired=_exitStart+(focus-_exitFocus);
        if(MonitorCameraStyles.IsContinuous(_style) && _exitRoof && _exitNoCollision)
        {
            // Keep the overhead fall, then descend with the player into a normal framing
            // height BEFORE handing the camera to the indoor route planner. A high seed above
            // the next tunnel ceiling cannot connect to its walking navigation or portal.
            float landing=Mathf.SmoothStep(0,1,Mathf.Clamp01((_exitGrounded-.15f)/.55f));
            desired.y=focus.y+Mathf.Lerp(_exitStart.y-_exitFocus.y,1.2f,landing);
        }
        bool clearingRoof=false;
        if(!_exitNoCollision && _exitRoof && _position.y>_carrierBounds.max.y-.2f)
        {
            if(_exitEdge==Vector3.zero)
            {
                Vector3 outside=focus-_carrierBounds.ClosestPoint(focus); outside.y=0;
                if(outside.sqrMagnitude>.01f) _exitEdge=Mathf.Abs(outside.x)>=Mathf.Abs(outside.z)
                    ? Vector3.right*Mathf.Sign(outside.x) : Vector3.forward*Mathf.Sign(outside.z);
            }
            if(_exitEdge!=Vector3.zero)
            {
                bool x=_exitEdge.x!=0; float sign=x?_exitEdge.x:_exitEdge.z;
                float boundary=(sign>0 ? (x?_carrierBounds.max.x:_carrierBounds.max.z) : (x?_carrierBounds.min.x:_carrierBounds.min.z))+sign*.65f;
                float current=x?_position.x:_position.z;
                if(x) desired.x=sign>0 ? Mathf.Max(desired.x,boundary) : Mathf.Min(desired.x,boundary);
                else desired.z=sign>0 ? Mathf.Max(desired.z,boundary) : Mathf.Min(desired.z,boundary);
                // Cross the roof edge at the existing height before beginning descent.
                if(sign*(current-boundary)<-.05f) { desired.y=_position.y; clearingRoof=true; }
            }
        }
        Vector3 previous=_position;
        Vector3 step=Vector3.SmoothDamp(_position,desired,ref _exitVelocity,.25f,10f,dt);
        if(_exitNoCollision || world.IsMonitorPathClear(_position,step)) _position=step;
        else if(world is IGameMonitorMotionAdapter motion && motion.TryMoveMonitorCamera(_position,step,out var safe,out _)) _position=safe;
        else _exitVelocity=Vector3.zero;
        // Transfer the motion actually presented to the viewer, rather than the spring's
        // internal end-of-step derivative (or an unapplied collision-recovery velocity).
        if(dt>.0001f) _exitMotion=Vector3.ClampMagnitude((_position-previous)/dt,10f);
        Vector3 look=focus-_position;
        if(look.sqrMagnitude>.001f)
        {
            var aim=MonitorCameraAim.Target(_rotation,look);
            var eased=Quaternion.Slerp(_rotation,aim,1-Mathf.Exp(-dt/.16f));
            _rotation=Quaternion.RotateTowards(_rotation,eased,180f*dt);
        }
        // Long falls remain a continuous shot. Once grounded, leave time to settle before
        // allowing normal room selection; prolonged invalid support still has a bounded recovery.
        // A running subject leaves a permanent horizontal spring offset. Requiring that
        // offset to vanish held the overhead rig for the entire timeout. Landing stability,
        // rather than a stationary player, permits a velocity-preserving handoff.
        bool settled=!clearingRoof && _exitGrounded>=.35f
            && Mathf.Abs(_position.y-desired.y)<.45f && Mathf.Abs(_exitVelocity.y)<2f;
        return _exitElapsed<1.2f || (!settled && _exitElapsed<4f);
    }

    private MonitorFraming Frame(IGameMonitorCameraAdapter world,Vector3 focus)
    {
        if(world is IGameMonitorFramingAdapter framing) return framing.EvaluateMonitorFraming(_position,_rotation);
        bool visible=world.IsMonitorSightClear(_position,focus);
        return new MonitorFraming(visible,visible && (_position-focus).magnitude<30f);
    }

    private void AimAtPlayer(Vector3 focus,float dt,int speed)
    {
        Vector3 look=focus+Vector3.ClampMagnitude(_focusVelocity*.04f,.25f)-_position;
        if (look.sqrMagnitude<.001f) return;
        Quaternion target=MonitorCameraAim.Target(_rotation,look);
        if(_resumeBlend>0 || Mathf.Abs(look.normalized.y)>.94f)
        { _rotation=Quaternion.RotateTowards(_rotation,Quaternion.Slerp(_rotation,target,MonitorCameraRules.Blend(dt,.16f)),180f*dt); return; }
        _rotation=Quaternion.Slerp(_rotation,target,MonitorCameraRules.Blend(dt,.07f*SpectatorFollowStabilizer.ResponseScale(speed)));
        // Keep running players in frame even during large direction changes; easing position cannot delay aim.
        float error=Quaternion.Angle(_rotation,target);
        if (error>12f) _rotation=Quaternion.RotateTowards(_rotation,target,error-12f);
    }

    private void Search(IGameMonitorCameraAdapter world, Vector3 focus, float now)
    {
        Vector3 localFocus = _searchRoom.WorldToLocal.MultiplyPoint3x4(focus);
        for (int budget = 0; budget < ProbeBudget; budget++)
        {
            int i = _candidateIndex++;
            Vector3 candidate = _searchRoom.LocalToWorld.MultiplyPoint3x4(
                MonitorCameraStyles.Candidate(_searchRoom.Bounds, localFocus, i, _style));
            if(_approachCrossing || (_nearMainEntrance && !_hasPose))
            {
                // Search beside/behind the approach, rather than repeatedly shrinking a distant
                // room-corner candidate into the same frontal close-up at the elevator doorway.
                Vector3 side=Vector3.Cross(Vector3.up,_approachDirection);
                candidate=focus+side*((i&1)==0?-1:1)*(i%8<4?2.2f:3.2f)
                    -_approachDirection*((i&2)==0?1.2f:2.4f)+Vector3.up*(i<8?1.3f:.7f);
            }
            if (world.TryPlaceMonitorCamera(focus, candidate, out candidate) && AcceptStation(world,candidate,focus))
            {
                float score = MonitorCameraRules.Score(candidate, focus, _position, _hasShot, i % 8);
                score+=EntranceCompositionScore(candidate,focus);
                Vector3 predicted=focus+Vector3.ClampMagnitude(_focusVelocity*.55f,2.5f);
                if (world.IsMonitorSightClear(candidate,predicted)) score+=1.4f;
                else score-=1.2f;
                if (score > _bestScore) { _bestScore = score; _best = candidate; }
            }
            // Try normal stations first; other heights only when those fail.
            if (_candidateIndex % 8 != 0 || (_candidateIndex < 24 && float.IsNegativeInfinity(_bestScore))) continue;
            _searching = false; _nextProbe = now + MonitorCameraRules.ProbeInterval;
            // Revalidate because the target may have moved during this budgeted search.
            if (!float.IsNegativeInfinity(_bestScore) && world.TryPlaceMonitorCamera(focus, _best, out var chosen))
                SetShot(world, chosen, focus, now, _searchRoom, temporary: false);
            return;
        }
    }

    private void SetShot(IGameMonitorCameraAdapter world, Vector3 position, Vector3 focus, float now, MonitorRoom room, bool temporary)
    {
        if(!AcceptStation(world,position,focus)) { _nextProbe=now+.5f; return; }
        bool previous=_hasPose;
        bool usablePrevious=_hasShot && world.IsMonitorPathClear(_position,_position);
        if(previous && _approachCrossing && !temporary)
        {
            Vector3 offset=position-focus; offset.y=0;
            if((position-_position).sqrMagnitude<.25f || offset.magnitude<1.35f
                || Vector3.Dot(offset.normalized,_approachDirection)>.35f)
            { _nextProbe=now+.5f; return; }
        }
        if (usablePrevious && !temporary && !_approachCrossing && Frame(world,focus).Readable
            && (position-_position).sqrMagnitude<3.0625f)
        {
            _roomId=room.Id; _shotRoom=room; _station=_position; _stationFocus=focus;
            _temporary=false; _nextProbe=now+1f; _railVelocity=Vector3.zero;
            return;
        }
        if(usablePrevious && !_temporary && !temporary && !_approachCrossing && (_position-focus).magnitude<12f
            && world.IsMonitorSightClear(_position,focus)
            && world.IsMonitorSightClear(_position,focus+Vector3.ClampMagnitude(_focusVelocity*.4f,2f)))
        {
            float currentScore=MonitorCameraRules.Score(_position,focus,_position,true,4)+.8f;
            if(_bestScore<currentScore+.6f) { _roomId=room.Id; _nextProbe=now+1f; return; }
        }
        _railVelocity=Vector3.zero;
        _transitionTime=0; _transitionSwapped=false;
        _transitioning=previous && (position-_position).sqrMagnitude>.01f;
        if (usablePrevious && !_temporary && !_approachCrossing && Frame(world,focus).Readable)
        { _transitioning=false; _nextProbe=now+1f; return; }
        _resumeBlend=0; _resumeVelocity=Vector3.zero;
        _fadeTransition=previous;
        _transitionDuration=.4f;
        _station=position;
        _localStation=temporary || _nearElevator;
        if(_approachCrossing) _approachHandled=true;
        if (!previous)
        {
            if (_hasFocus && ShotChanges>0) TransitionOpacity=1;
            _position=position; _rotation=Quaternion.LookRotation(focus-position,Vector3.up);
            _stationContactTime=0;
        }
        _stationFocus = focus; _roomId = room.Id; _shotRoom = room;
        ShotChanges++;
        _nextProbe=now+(temporary ? 1.0f : .75f);
        _hasShot = _hasPose = true; _temporary = temporary; _blockedSeconds = 0; _shotSince = now;
    }
    private bool AcceptStation(IGameMonitorCameraAdapter world,Vector3 candidate,Vector3 focus)
    {
        if(!world.IsMonitorPathClear(candidate,candidate)) return false;
        if(!_approachCrossing) return true;
        Vector3 offset=candidate-focus; offset.y=0;
        return offset.magnitude>=1.35f && Vector3.Dot(offset.normalized,_approachDirection)<=.35f;
    }
    private float EntranceCompositionScore(Vector3 candidate,Vector3 focus)
    {
        if(!_nearElevator && !_nearMainEntrance) return 0;
        Vector3 offset=candidate-focus; offset.y=0;
        float ahead=Vector3.Dot(offset.normalized,_approachDirection);
        if(_nearMainEntrance) ahead=Mathf.Abs(ahead);
        return ahead>.35f && offset.magnitude<7f ? -6f : 0;
    }
}
