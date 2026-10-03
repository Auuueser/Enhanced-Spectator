using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnhancedSpectator.GameInterop;

/// <summary>Only camera queries ignore audited V81 door leaves/frames. Never mutates scene colliders.</summary>
internal sealed class MonitorDoorGeometry
{
    internal static bool OwnsScene(Scene scene) => scene.name.StartsWith("EnhancedSpectator camera query ",System.StringComparison.Ordinal);
    private readonly RaycastHit[] _hits=new RaycastHit[64];
    private readonly Collider[] _overlaps=new Collider[64];
    private readonly Dictionary<int,bool> _doorCache=new Dictionary<int,bool>();
    private readonly List<Opening> _openings=new List<Opening>();
    private SphereCollider? _probeShape;
    private Scene _probeScene;
    private Vector3 _blockNormal;
    private bool _wallTransit;
    private float _radius=MonitorCameraGeometry.Clearance;
    private Vector3 _probeFrom,_probeTo,_blockedFrom,_blockedTo;
    private Collider? _lastBlocker;
    private string _blockReason="none";
    private int _blockFrame=-1;
    internal int OpeningCount => _openings.Count;
    private readonly struct Opening
    {
        internal readonly Matrix4x4 WorldToLocal;
        internal readonly Vector2 Size;
        internal Opening(Matrix4x4 matrix,Vector2 size) { WorldToLocal=matrix; Size=size; }
        internal bool Contains(Vector3 point)
        {
            Vector3 p=WorldToLocal.MultiplyPoint3x4(point);
            return Mathf.Abs(p.x)<=Size.x*.5f+.35f && p.y>=.25f && p.y<=Size.y+.35f && Mathf.Abs(p.z)<=.45f;
        }
    }
    internal void Clear()
    {
        _doorCache.Clear(); _openings.Clear(); _lastBlocker=null; _blockReason="none"; _blockFrame=-1;
        if(_probeShape!=null)
        {
            if(Application.isPlaying) Object.Destroy(_probeShape.gameObject);
            else Object.DestroyImmediate(_probeShape.gameObject);
        }
        _probeShape=null;
        if(_probeScene.IsValid() && _probeScene.isLoaded) SceneManager.UnloadSceneAsync(_probeScene);
        _probeScene=default;
    }
    private SphereCollider ProbeShape
    {
        get
        {
            if(_probeShape==null)
            {
                var go=new GameObject("EnhancedSpectator camera query") { hideFlags=HideFlags.HideAndDontSave };
                // Disabled colliders have no usable PhysX shape in this Unity version. Keep the
                // enabled probe in an isolated physics scene that is never simulated instead.
                _probeScene=SceneManager.CreateScene("EnhancedSpectator camera query "+go.GetInstanceID(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                SceneManager.MoveGameObjectToScene(go,_probeScene);
                _probeShape=go.AddComponent<SphereCollider>();
                _probeShape.isTrigger=true;
                _probeShape.radius=MonitorCameraGeometry.Clearance;
            }
            return _probeShape;
        }
    }
    private bool Contact(Collider c,Vector3 point,out Vector3 contact,out Vector3 normal)
    {
        // ClosestPoint is unsupported for non-convex room meshes. Narrow-phase penetration accepts
        // any scene collider when the other shape is a sphere, including concave floor/wall meshes.
        if(Mathf.Abs(ProbeShape.radius-_radius)>.000001f) ProbeShape.radius=_radius;
        bool overlaps=Physics.ComputePenetration(ProbeShape,point,Quaternion.identity,c,c.transform.position,
            c.transform.rotation,out normal,out float depth);
        contact=point-normal*_radius;
        return overlaps && depth>.00001f;
    }
    internal void AddOpening(Matrix4x4 matrix,Vector2 size) => _openings.Add(new Opening(matrix,size));
    internal bool ClearWallTransit(Vector3 from,Vector3 to,int mask)
    {
        _wallTransit=true;
        try { return ClearPath(from,to,mask,true); }
        finally { _wallTransit=false; }
    }
    internal bool ClearPath(Vector3 from,Vector3 to,int mask,bool moving=false)
    {
        // Planning leaves 4 cm of tolerance around the real camera volume. It exceeds the maximum
        // grazing error of the 20 cm subdivision on a sphere, avoiding endpoint-dependent stalls.
        _radius=MonitorCameraGeometry.Clearance+(moving?0:.04f);
        _blockNormal=Vector3.zero;
        _probeFrom=from; _probeTo=to;
        if (!ClearPoint(from,mask) || !ClearPoint(to,mask)) return false;
        Vector3 delta=to-from;
        if(delta.sqrMagnitude<.000001f) return true;
        if(!SweepClear(from,to,mask,out bool uncertain)) return false;
        if(!uncertain && delta.magnitude>.2f)
        {
            int count=Physics.OverlapCapsuleNonAlloc(from,to,_radius,_overlaps,mask,QueryTriggerInteraction.Ignore);
            if(count==_overlaps.Length) return Blocked("capsule-buffer-full",null);
            for(int i=0;i<count;i++) if(_overlaps[i] is MeshCollider mesh && !mesh.convex)
            { uncertain=true; break; }
        }
        if(!uncertain || delta.magnitude<=.2f) return true;
        // A sweep returns only the first hit for each mesh. Ignoring an initial broad-phase overlap
        // or a shared doorway contact can hide a later wall of that SAME concave mesh. Recast short,
        // overlapping volumes and narrow-phase every endpoint before accepting the whole segment.
        int steps=Mathf.CeilToInt(delta.magnitude/.2f);
        if(steps>256) return Blocked("segment-budget",null);
        Vector3 previous=from;
        for(int i=1;i<=steps;i++)
        {
            Vector3 next=Vector3.Lerp(from,to,(float)i/steps);
            if(!ClearPoint(next,mask) || !SweepClear(previous,next,mask,out _)) return false;
            previous=next;
        }
        return true;
    }
    private bool SweepClear(Vector3 from,Vector3 to,int mask,out bool uncertain)
    {
        uncertain=false;
        Vector3 delta=to-from;
        int count=Physics.SphereCastNonAlloc(from,_radius,delta.normalized,_hits,delta.magnitude,mask,QueryTriggerInteraction.Ignore);
        // Saturation is not proof of a clear route; keep the camera on the safe side.
        if(count==_hits.Length) return Blocked("cast-buffer-full",null);
        for(int i=0;i<count;i++)
        {
            var hit=_hits[i];
            if(hit.distance<=.0001f)
            {
                uncertain=true;
                // PhysX sweeps can report a zero-distance hit on a concave mesh's broad-phase volume.
                // Verify the actual triangles, and also a tiny forward step for an inward tangent hit.
                Vector3 probe=from+delta.normalized*Mathf.Min(.005f,delta.magnitude);
                if(Contact(hit.collider,probe,out var contact,out var normal)
                    && !Ignored(hit.collider,contact,normal))
                { _blockNormal=normal; return Blocked("cast-start-overlap",hit.collider); }
            }
            else if(!Ignored(hit.collider,hit.point,hit.normal))
            { _blockNormal=hit.normal; return Blocked("cast-hit",hit.collider); }
            else if(!IsDoor(hit.collider)) uncertain=true;
        }
        // Concave/single-sided triangles can yield an initial-overlap sweep without a later surface
        // hit. A center ray supplies that surface crossing even for very thin walls and floors.
        count=Physics.RaycastNonAlloc(from,delta.normalized,_hits,delta.magnitude,mask,QueryTriggerInteraction.Ignore);
        if(count==_hits.Length) return Blocked("ray-buffer-full",null);
        for(int i=0;i<count;i++)
        {
            var hit=_hits[i];
            if(!Ignored(hit.collider,hit.point,hit.normal))
            { _blockNormal=hit.normal; return Blocked("ray-surface",hit.collider); }
            if(!IsDoor(hit.collider)) uncertain=true;
        }
        return true;
    }
    private bool ClearPoint(Vector3 point,int mask)
    {
        int count=Physics.OverlapSphereNonAlloc(point,_radius,_overlaps,mask,QueryTriggerInteraction.Ignore);
        if(count==_overlaps.Length) return Blocked("overlap-buffer-full",null);
        for(int i=0;i<count;i++)
        {
            Collider c=_overlaps[i];
            if(Contact(c,point,out var contact,out var normal) && !Ignored(c,contact,normal))
            { _blockNormal=normal; return Blocked("endpoint-overlap",c); }
        }
        return true;
    }
    private bool Blocked(string reason,Collider? collider)
    {
        _blockReason=reason; _lastBlocker=collider; _blockFrame=Time.frameCount;
        _blockedFrom=_probeFrom; _blockedTo=_probeTo; return false;
    }
    internal bool TryMove(Vector3 from,Vector3 requested,int mask,out Vector3 position,out bool sliding)
    {
        if(ClearPath(from,requested,mask,true)) { position=requested; sliding=false; return true; }
        sliding=true;
        return TrySlide(from,requested,mask,out position);
    }
    internal bool TrySlide(Vector3 from,Vector3 requested,int mask,out Vector3 position)
    {
        position=from;
        // Called only after a rejected step. Keep this local correction bounded, and validate the
        // complete replacement segment with exactly the same probe used by route planning.
        Vector3 normal=_blockNormal, move=requested-from;
        if(move.sqrMagnitude<.000001f || normal.sqrMagnitude<.5f) return false;
        // Retain a small outward component so a valid slide cannot leave the real camera trapped
        // inside the planner's extra clearance. This is a checked move, never a position snap.
        Vector3 slide=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(move,normal)+normal*.05f,move.magnitude);
        if(slide.sqrMagnitude>move.sqrMagnitude*.01f && ClearPath(from,from+slide,mask,true))
        { position=from+slide; return true; }
        // A small safe prefix avoids stopping a full frame short of an obstruction. No depenetration
        // teleport: if the origin is genuinely embedded, hold it and let replanning report the fault.
        float low=0,high=1;
        for(int i=0;i<4;i++)
        {
            float middle=(low+high)*.5f;
            if(ClearPath(from,from+move*middle,mask,true)) low=middle; else high=middle;
        }
        if(low<.0625f) return false;
        position=from+move*low; return true;
    }
    internal string DescribeLastBlock()
    {
        string path="none";
        if(_lastBlocker!=null)
        {
            path=_lastBlocker.name;
            for(var parent=_lastBlocker.transform.parent;parent!=null && path.Length<300;parent=parent.parent) path=parent.name+"/"+path;
            path+=$",type={_lastBlocker.GetType().Name},layer={_lastBlocker.gameObject.layer},bounds={_lastBlocker.bounds}";
        }
        return $"{_blockReason},frame={_blockFrame},from={_blockedFrom:F2},to={_blockedTo:F2},collider={path}";
    }
    private bool Ignored(Collider c,Vector3 hit,Vector3 normal)
    {
        if(IsDoor(c)) return true;
        if(_wallTransit && normal.sqrMagnitude>.5f && Mathf.Abs(normal.y)<.3f) return true;
        // A frame may share a wall collider. Waive only vertical contact inside a real connected aperture.
        // Floors/ceilings keep blocking, even at a threshold.
        if(Mathf.Abs(normal.y)>.55f) return false;
        for(int i=0;i<_openings.Count;i++) if(_openings[i].Contains(hit)) return true;
        return false;
    }
    private bool IsDoor(Collider c)
    {
        int id=c.GetInstanceID();
        if(!_doorCache.TryGetValue(id,out bool door)) { door=IsAuditedDoor(c.transform); _doorCache[id]=door; }
        return door;
    }
    internal static bool IsAuditedDoor(Transform leaf)
    {
        string path="";
        for(Transform? node=leaf;node!=null;node=node.parent)
        {
            string name=node.name.Replace("(Clone)","").Trim();
            if(IsAuditedPath(name,path)) return true;
            path=path.Length==0 ? name : name+"/"+path;
            if(path.Length>300) break;
        }
        return false;
    }
    internal static bool IsAuditedPath(string root,string path)
    {
        switch(root)
        {
            case "OfficeDoor": case "NormalDoor": return path=="DoorMesh/Cube";
            case "SteelDoorMapModel": case "FancyDoorMapModel": case "FancyDoorMapModelGlass": case "FancyDoorMapModelDarkWoodFrame":
                return path=="SteelDoor (1)/DoorMesh/Cube";
            case "YellowMineDoor":
                return path=="MineDoorFrame" || path=="MineDoorFrame/LOSBlocker" || path=="MineDoorMesh/Cube" || path=="MineDoorMesh/LOSBlocker";
            case "BathroomShowerDoor": return path=="ShowerDoorContainer/BathroomShowerDoor/Cube";
            case "GlassDoubleDoorsMapModel": return path=="SteelDoorAnim/DoorMesh/DoorLeftTrigger" || path=="SteelDoorAnim/DoorMesh (1)/DoorRightTrigger";
            case "BigDoor": return path=="BigDoorLeft" || path=="BigDoorRight" || path=="Cube" || path=="Cube.001/Colliders/Cube"
                || path=="Cube.001/Colliders/Cube (1)" || path=="Cube.001/Colliders/Cube (2)" || path=="Cube.001/Colliders/Cube (3)"
                || path=="Cube.001/Colliders/Cube (4)" || path=="Cube.001/Colliders/Cube (5)";
            default: return false;
        }
    }
}
