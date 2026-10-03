using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Manually selected single-subject prototypes. No invented threat/death/voice events.</summary>
internal sealed class DirectorSampleCamera
{
    private readonly MonitorCameraDirector _room = new MonitorCameraDirector();
    private bool _started, _hasPose, _falling, _landed, _hasCompanion;
    private int _sample;
    private float _start, _lastY, _peakY, _stillTime, _companionProbe;
    private Vector3 _back, _right, _position, _companion;
    private Quaternion _rotation;
    internal bool Wide { get; private set; }
    internal float FocusDistance { get; private set; } = 5;
    internal string Stage { get; private set; } = "waiting";
    internal void Clear() { _started = _hasPose = _falling = _landed = _hasCompanion = false; _room.Clear(); _stillTime=0; _companionProbe=0; }

    internal bool TryUpdate(IGameDirectorCameraAdapter world, Vector3 eyes, Vector3 forward, int sample,
        float now, float dt, out Vector3 position, out Quaternion rotation, out float fov)
    {
        position=eyes; rotation=Quaternion.identity; fov=60;
        sample=DirectorSampleRules.Valid(sample);
        if (_started && _sample!=sample) Clear();
        if (!_started)
        {
            _started=true; _sample=sample; _start=now; _lastY=_peakY=eyes.y;
            forward.y=0; if(forward.sqrMagnitude<.01f) forward=Vector3.forward;
            _back=-forward.normalized; _right=Vector3.Cross(Vector3.up,-_back);
        }
        float time=now-_start;
        float vy=dt>.0001f ? (eyes.y-_lastY)/dt : 0;
        _lastY=eyes.y; _peakY=Mathf.Max(_peakY,eyes.y);
        if (!_falling && DirectorSampleRules.FallStarted(_peakY-eyes.y,vy)) _falling=true;
        if (_falling) { _stillTime=Mathf.Abs(vy)<.5f ? _stillTime+dt : 0; if(_stillTime>.2f) _landed=true; }
        Vector3 focus=eyes-Vector3.up*.3f;
        Vector3 candidate=focus+_back*5+_right*2+Vector3.up*1.2f;
        bool cut=false, roomShot=false; Wide=sample!=0;
        Stage="establish";
        if(sample==0 || sample==1 && !_falling)
        {
            if (world.IsMonitorTargetIndoors && _room.TryUpdate(world,focus,now,dt,out var p,out var q))
            { candidate=p; roomShot=true; }
            Stage=sample==0 ? "suspense" : "awaiting-fall";
        }
        if(sample==1 && _falling)
        {
            candidate=focus+_back*4+_right*3+Vector3.up*.8f;
            Stage=_landed ? "landing-hold" : "fall-handoff";
            cut=true;
        }
        if(sample==2)
        {
            if(now>=_companionProbe) { _companionProbe=now+.25f; _hasCompanion=world.TryGetDirectorCompanion(focus,out _companion); }
            bool reaction=_hasCompanion && time>=4 && time<6;
            Wide=!reaction;
            if (_hasCompanion && !reaction) focus=(focus+_companion)*.5f;
            candidate=focus+_back*(reaction?3.4f:6)+_right*(reaction?1.5f:2)+Vector3.up*.5f;
            Stage=!_hasCompanion ? "single-subject" : reaction ? "reaction-demo" : "two-shot";
        }
        if(sample==3)
        {
            candidate=focus+(_back+_right*.35f).normalized*(4+3*DirectorSampleRules.Ease(time,1.6f));
            Stage=time<1.6f ? "dolly-zoom" : "hold";
            Wide=false;
        }
        if(!world.TryPlaceDirectorCamera(focus,candidate,out var safe))
        {
            if(!_hasPose || !world.IsMonitorPathClear(_position,_position) || !world.IsMonitorSightClear(_position,focus))
            { Stage="recovering"; return false; }
            safe=_position;
        }
        if(sample==2 && _hasCompanion && Wide
            && (!world.IsMonitorSightClear(safe,_companion) || !world.IsMonitorSightClear(safe,eyes-Vector3.up*.3f)))
        {
            // A visible midpoint does not prove both people are visible. Degrade to the primary subject.
            focus=eyes-Vector3.up*.3f; Stage="pair-occluded";
            if(!world.TryPlaceDirectorCamera(focus,focus+_back*5+_right*2+Vector3.up,out safe))
            { Stage="recovering"; return false; }
        }
        // Preserve a camera side fixed at shot entry; cuts never interpolate through geometry.
        if(!_hasPose || roomShot || !world.IsMonitorPathClear(_position,safe)) _position=safe;
        else
        {
            Vector3 step=Vector3.Lerp(_position,safe,MonitorCameraRules.Blend(dt,cut?.25f:.5f));
            if(world.IsMonitorPathClear(_position,step)&&world.IsMonitorSightClear(step,focus)) _position=step;
            else _position=safe;
        }
        Quaternion look=Quaternion.LookRotation(focus-_position,Vector3.up);
        _rotation=!_hasPose ? look : Quaternion.Slerp(_rotation,look,MonitorCameraRules.Blend(dt,.15f));
        _hasPose=true;
        FocusDistance=(focus-_position).magnitude;
        fov=DirectorSampleRules.Fov(sample,time,FocusDistance,Wide);
        position=_position; rotation=_rotation;
        return true;
    }
}
