using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

internal enum MonitorCarrierPhase { None, Entering, Riding, ChangingRegion }

/// <summary>Retains one carrier-relative composition inside a moving cabin or above its roof.</summary>
internal sealed class MonitorCarrierCamera
{
    private Transform? _surface;
    private Vector3 _sampleLocal,_sampleWorld,_cameraLocal,_stationLocal,_focusLocal,_velocity;
    private Bounds _bounds;
    private Bounds? _localBounds;
    private float _missing,_ceilingY,_aimBlend,_turnSpeed;
    private bool _riding,_hasCamera,_entering,_roof,_hasLayout;
    private Vector3 _aimLocal,_aimVelocity;
    private float _entryBlend;
    private bool _known,_unobstructed;
    internal bool IgnoresCollisions => _unobstructed;
    private float _regionTime;
    private Vector3 _supportOffset;
    private bool _pendingRoof;
    internal int Acquisitions { get; private set; }
    internal MonitorCarrierPhase Phase { get; private set; }
    private string _reason="none";
    internal float TransitionOpacity => 0;
    internal bool Active => _riding;
    internal bool InCabin => _riding && _hasLayout && !_roof;
    internal bool CabinLensReady => InCabin && _hasCamera && !_entering;
    internal bool OnRoof => _roof;
    internal Bounds Footprint => _bounds;
    internal bool Entering => _entering || !_hasCamera;
    internal string Describe() => $"phase={Phase},acquisitions={Acquisitions},reason={_reason},known={_known},roof={_roof},continuous=True,missing={_missing:F2},frame={(_surface!=null ? _surface.name : "none")}";
    internal void Clear()
    { Phase=MonitorCarrierPhase.None; _regionTime=0; _surface=null; _localBounds=null; _riding=_hasCamera=_entering=_hasLayout=_known=_unobstructed=false; _missing=_aimBlend=_turnSpeed=0; _velocity=Vector3.zero; }

    internal bool TryUpdate(IGameMonitorCameraAdapter world,Vector3 focus,float dt,int style,
        bool hasShot,Vector3 previous,Quaternion previousRotation,out Vector3 position,out Quaternion rotation)
    {
        position=previous; rotation=previousRotation;
        if(!world.IsMonitorTargetIndoors || !(world is IGameMonitorSupportAdapter adapter)) { Clear(); return false; }
        bool supported=adapter.TryGetMonitorSupport(out var support);
        // The canonical elevator and local body position survive contact/door collider churn.
        if(_known && _riding && _surface!=null && (!supported || !support.KnownElevator)
            && RetainSupport(focus,out var retained))
        { support=retained; supported=true; _reason="local-region-retained"; }
        if(supported)
        {
            if(support.Surface!=_surface)
            {
                Clear(); _surface=support.Surface; _reason="new-carrier";
                _sampleLocal=_surface.InverseTransformPoint(support.Point); _sampleWorld=support.Point;
                _bounds=support.Bounds; _riding=support.KnownElevator;
                if(!_riding) return false;
            }
            _missing=0;
            _known=support.KnownElevator;
            _unobstructed|=_known;
            // Only confirmed elevator geometry can change cabin/roof classification.
            // An overhead ray on an ordinary floor is not evidence of an elevator.
            bool change=_known && _riding && _hasLayout && _roof!=support.OnRoof;
            if(change)
            {
                if(_pendingRoof!=support.OnRoof) { _pendingRoof=support.OnRoof; _regionTime=0; }
                _regionTime+=dt;
                if(_localBounds.HasValue)
                {
                    float localY=_surface.InverseTransformPoint(support.Point).y;
                    float margin=.2f/Mathf.Max(.01f,_surface.lossyScale.y);
                    bool crossed=_roof ? localY<_localBounds.Value.min.y-margin : localY>_localBounds.Value.max.y+margin;
                    if(crossed) _regionTime=.25f;
                }
                // Require stable classification; keep the established geometry until confirmation.
                if(_regionTime<.25f) support=new MonitorSupport(_surface,support.Point,_bounds,_known,
                    _roof ? float.NaN : _ceilingY,_localBounds);
                else
                {
                    _hasCamera=false; _regionTime=0;
                    Phase=MonitorCarrierPhase.ChangingRegion; _reason="confirmed-region-change";
                }
            }
            else _regionTime=0;
            _bounds=support.Bounds;
            var bodyBounds=_bounds;
            if(_known && !support.OnRoof && bodyBounds.size.y<1f)
                bodyBounds.SetMinMax(new Vector3(bodyBounds.min.x,support.Point.y,bodyBounds.min.z),
                    new Vector3(bodyBounds.max.x,support.CeilingY,bodyBounds.max.z));
            _localBounds=support.LocalBounds ?? (_known ? LocalBounds(_surface,bodyBounds) : (Bounds?)null);
            if(!_hasLayout || !_riding || _known)
            {
                _roof=support.OnRoof; _ceilingY=support.CeilingY; _hasLayout=true;
                _supportOffset=focus-support.Point;
            }
            _riding|=support.KnownElevator;
        }
        else if(!_riding || (_missing+=dt)>.3f || _surface==null) { Clear(); return false; }
        if(_surface==null) { Clear(); return false; }
        Vector3 transported=_surface.TransformPoint(_sampleLocal),delta=transported-_sampleWorld;
        _sampleWorld=transported;
        // A discontinuous carrier teleport is a fresh acquisition, not a giant camera step.
        if(delta.sqrMagnitude>9f) { Clear(); return false; }
        if(Mathf.Abs(delta.y)>Mathf.Max(.0001f,dt*.1f)) _riding=true;
        if(!_riding) return false;
        if(!supported) _bounds.center+=delta;
        if(!supported && !_roof) _ceilingY+=delta.y;
        // Frame the upper torso from the cabin's high inside corner; the scoped wide lens
        // keeps the surrounding cabin and the whole player readable at this steeper angle.
        Vector3 aimFocus=focus+(_roof ? Vector3.zero : Vector3.up*.35f);

        // Once acquired, every carrier uses the original continuous corner/roof rig.
        // Occluding columns and doors cannot select a replacement shot, regardless of style.
        _unobstructed=true;
        return UpdateUnobstructed(focus,aimFocus,delta,dt,hasShot,previous,previousRotation,out position,out rotation);
    }
    private static Bounds LocalBounds(Transform frame,Bounds bounds)
    {
        var result=new Bounds(frame.InverseTransformPoint(bounds.center),Vector3.zero);
        for(int i=0;i<8;i++) result.Encapsulate(frame.InverseTransformPoint(bounds.center+Vector3.Scale(bounds.extents,
            new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
        return result;
    }
    private bool RetainSupport(Vector3 focus,out MonitorSupport support)
    {
        support=default;
        if(_surface==null || !_localBounds.HasValue) return false;
        var area=_localBounds.Value;
        Vector3 feet=_surface.InverseTransformPoint(focus-_supportOffset);
        if(feet.x<area.min.x-.35f || feet.x>area.max.x+.35f
            || feet.z<area.min.z-.35f || feet.z>area.max.z+.35f) return false;
        if(feet.y<area.min.y-.25f || feet.y>area.max.y+(_roof?.65f:.15f)) return false;
        var bounds=new Bounds(_surface.TransformPoint(area.center),Vector3.zero);
        for(int i=0;i<8;i++) bounds.Encapsulate(_surface.TransformPoint(area.center+Vector3.Scale(area.extents,
            new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
        support=new MonitorSupport(_surface,focus-_supportOffset,bounds,true,_roof?float.NaN:bounds.max.y,area);
        return true;
    }

    private bool UpdateUnobstructed(Vector3 focus,Vector3 aimFocus,Vector3 delta,float dt,bool hasShot,
        Vector3 previous,Quaternion previousRotation,out Vector3 position,out Quaternion rotation)
    {
        // A confirmed elevator owns its camera for the whole ride. Props, door panels, cables,
        // shaft beams and sight occlusion cannot invalidate the pose or restart the entry.
        Vector3 moved=_hasCamera ? _surface!.TransformPoint(_cameraLocal) : previous+delta;
        if(!_hasCamera)
        {
            bool changing=Phase==MonitorCarrierPhase.ChangingRegion;
            Acquisitions++; if(!changing) Phase=MonitorCarrierPhase.Entering;
            Vector3 goal=new Vector3(_bounds.min.x,_ceilingY-.45f,_bounds.min.z);
            if(_localBounds.HasValue && !_roof)
            {
                var area=_localBounds.Value;
                goal=_surface!.TransformPoint(new Vector3(area.min.x,area.max.y-.45f/_surface.lossyScale.y,area.min.z));
            }
            if(_roof) goal=focus+Vector3.up*2.8f+new Vector3(-1,0,-1).normalized*1.1f;
            goal=ClampToFootprint(goal);
            _stationLocal=_surface!.InverseTransformPoint(goal); _focusLocal=_surface.InverseTransformPoint(focus);
            _aimLocal=_surface.InverseTransformPoint(aimFocus); if(!changing) { _aimVelocity=Vector3.zero; _velocity=Vector3.zero; } _entryBlend=hasShot?1:0; _entering=hasShot; _hasCamera=true; _aimBlend=1.2f;
            if(!hasShot) moved=goal;
        }
        Vector3 slide=_surface!.InverseTransformPoint(focus)-_focusLocal; slide.y=0;
        Vector3 desired=ClampToFootprint(_surface.TransformPoint(_stationLocal+(_roof?slide:Vector3.ClampMagnitude(slide*.25f,.25f))));
        _entryBlend=Mathf.Lerp(_entryBlend,_entering?1:0,1-Mathf.Exp(-dt/.3f));
        moved=Vector3.SmoothDamp(moved,desired,ref _velocity,Mathf.Lerp(.35f,.5f,_entryBlend),Mathf.Lerp(2f,4f,_entryBlend),dt);
        if(_entering && (moved-desired).sqrMagnitude<.0004f) { _entering=false; Phase=MonitorCarrierPhase.Riding; }
        _cameraLocal=_surface.InverseTransformPoint(moved); _aimBlend=Mathf.Max(0,_aimBlend-dt);
        position=moved;
        // Filter the subject in the carrier frame: footsteps/network samples are damped, while
        // elevator translation is applied equally to camera and aim, with no vertical catch-up lag.
        _aimLocal=Vector3.SmoothDamp(_aimLocal,_surface.InverseTransformPoint(aimFocus),ref _aimVelocity,.12f,20f,dt);
        Vector3 stableAim=_surface.TransformPoint(_aimLocal);
        rotation=hasShot ? Aim(previousRotation,stableAim-moved,dt,_entering || _aimBlend>0)
            : Quaternion.LookRotation(aimFocus-moved,Vector3.up);
        return true;
    }
    private Quaternion Aim(Quaternion previous,Vector3 look,float dt,bool entering)
    {
        if(look.sqrMagnitude<.001f) return previous;
        Quaternion desired=MonitorCameraAim.Target(previous,look);
        float angle=Quaternion.Angle(previous,desired);
        // Ease angular speed too: a distant/reversed incoming view must not start at full speed,
        // and the entry-to-tracking handoff must not abruptly double its response.
        float speed=Mathf.Min(entering ? 110f : 210f,angle/(entering ? .24f : .1f));
        _turnSpeed=Mathf.MoveTowards(_turnSpeed,speed,240f*dt);
        return Quaternion.RotateTowards(previous,desired,Mathf.Min(angle,_turnSpeed*dt));
    }
    private static float ClampAxis(float value,float min,float max) =>
        max-min<.9f ? (min+max)*.5f : Mathf.Clamp(value,min+.4f,max-.4f);
    private Vector3 ClampToFootprint(Vector3 p)
    {
        if(_localBounds.HasValue && _surface!=null)
        {
            var area=_localBounds.Value; var scale=_surface.lossyScale; p=_surface.InverseTransformPoint(p);
            p.x=Mathf.Clamp(p.x,area.min.x+.4f/scale.x,area.max.x-.4f/scale.x);
            p.z=Mathf.Clamp(p.z,area.min.z+.4f/scale.z,area.max.z-.4f/scale.z);
            return _surface.TransformPoint(p);
        }
        p.x=ClampAxis(p.x,_bounds.min.x,_bounds.max.x); p.z=ClampAxis(p.z,_bounds.min.z,_bounds.max.z); return p;
    }
}
