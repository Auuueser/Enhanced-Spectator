using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Reads the moving cabin's structural boxes, never the shaft-sized elevatorBounds trigger.</summary>
internal sealed class MonitorElevatorGeometry
{
    private Transform? _frame;
    private BoxCollider[] _boxes=System.Array.Empty<BoxCollider>();
    private Bounds _localCabin,_localRoof;
    private bool _valid;
    internal bool Resolve(Transform frame,out Bounds cabin,out Bounds roof)
    {
        if(_frame!=frame)
        {
            _frame=frame; _boxes=frame.GetComponents<BoxCollider>(); _valid=false;
            BoxCollider? floor=null,ceiling=null;
            foreach(var box in _boxes)
            {
                if(box==null || box.isTrigger || !box.enabled || box.size.y>Mathf.Min(box.size.x,box.size.z)*.3f) continue;
                if(floor==null || box.center.y<floor.center.y) floor=box;
                if(ceiling==null || box.center.y>ceiling.center.y) ceiling=box;
            }
            if(floor!=null && ceiling!=null && floor!=ceiling)
            {
                Vector3 min=floor.center-floor.size*.5f,max=floor.center+floor.size*.5f;
                min.y=max.y; max.y=ceiling.center.y-ceiling.size.y*.5f;
                foreach(var box in _boxes)
                {
                    if(box==null || box.isTrigger || !box.enabled || box.size.y<(max.y-min.y)*.6f) continue;
                    if(box.size.x<box.size.z*.2f)
                    { if(box.center.x>floor.center.x) max.x=Mathf.Min(max.x,box.center.x-box.size.x*.5f); else min.x=Mathf.Max(min.x,box.center.x+box.size.x*.5f); }
                    if(box.size.z<box.size.x*.2f)
                    { if(box.center.z>floor.center.z) max.z=Mathf.Min(max.z,box.center.z-box.size.z*.5f); else min.z=Mathf.Max(min.z,box.center.z+box.size.z*.5f); }
                }
                _localCabin=new Bounds(); _localCabin.SetMinMax(min,max);
                _localRoof=new Bounds(ceiling.center,ceiling.size);
                _valid=_localCabin.size.x>.6f && _localCabin.size.z>.6f && _localCabin.size.y>1f;
            }
        }
        cabin=roof=default;
        if(!_valid) return false;
        cabin=WorldBounds(frame,_localCabin); roof=WorldBounds(frame,_localRoof); return true;
    }
    internal bool Contains(Transform frame,Vector3 feet,bool roof)
    {
        var p=frame.InverseTransformPoint(feet); var area=roof ? _localRoof : _localCabin;
        return p.x>=area.min.x && p.x<=area.max.x && p.z>=area.min.z && p.z<=area.max.z
            && p.y>=area.min.y-.35f && p.y<=area.max.y+(roof?1f:0);
    }
    internal bool TrySupport(Transform frame,Vector3 feet,bool sampled,MonitorSupport sample,out MonitorSupport support)
    {
        support=sample;
        if(!Resolve(frame,out var cabin,out var roof)) return false;
        bool onRoof=feet.y>=roof.min.y-.1f;
        if(!Contains(frame,feet,onRoof) || (onRoof && (!sampled || Mathf.Abs(sample.Point.y-roof.max.y)>=.65f))) return false;
        support=new MonitorSupport(frame,new Vector3(feet.x,onRoof?roof.max.y:cabin.min.y,feet.z),
            onRoof?roof:cabin,true,onRoof?float.NaN:cabin.max.y,onRoof?_localRoof:_localCabin);
        return true;
    }
    private static Bounds WorldBounds(Transform frame,Bounds local)
    {
        var result=new Bounds(frame.TransformPoint(local.center),Vector3.zero);
        for(int i=0;i<8;i++) result.Encapsulate(frame.TransformPoint(local.center+Vector3.Scale(local.extents,
            new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
        return result;
    }
}
