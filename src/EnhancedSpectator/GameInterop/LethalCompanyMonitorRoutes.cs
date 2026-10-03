using System.Collections.Generic;
using DunGen;
using UnityEngine;
using UnityEngine.AI;

namespace EnhancedSpectator.GameInterop;

public sealed partial class LethalCompanySpectatorAdapter : IGameMonitorRouteAdapter, IGameMonitorDiagnosticsAdapter, IGameMonitorMotionAdapter, IGameMonitorWallTransitAdapter
{
    private Dungeon? _routeDungeon;
    private int _routeTileCount, _routeFrame=-1;
    private readonly List<Tile> _routeTiles=new List<Tile>();
    private readonly Dictionary<int,List<MonitorPortal>> _routePortals=new Dictionary<int,List<MonitorPortal>>();
    private readonly MonitorDoorGeometry _routeDoors=new MonitorDoorGeometry();
    private readonly NavMeshPath _routeNav=new NavMeshPath();
    private readonly Vector3[] _routeCorners=new Vector3[128];
    private string _routeNavOutcome="not-requested";
    string IGameMonitorDiagnosticsAdapter.DescribeMonitorGeometry(Vector3 cameraPosition)
    {
        EnsureMonitorRoutes();
        string lastProbe=_routeDoors.DescribeLastBlock();
        bool cameraClear=_routeDoors.ClearPath(cameraPosition,cameraPosition,MonitorWorldMask,true);
        bool hasRoom=((IGameMonitorRouteAdapter)this).TryGetRouteRoom(cameraPosition,out var room);
        return $"tiles={_routeTiles.Count},openings={_routeDoors.OpeningCount},cameraClear={cameraClear},cameraRoom={(hasRoom?room.Id:0)},lastPlacement={_monitorPlacementReason},nav={_routeNavOutcome},lastBlockedProbe=[{lastProbe}],cameraBlock=[{(cameraClear?"none":_routeDoors.DescribeLastBlock())}]";
    }
    private void ClearMonitorRoutes()
    { ClearMonitorAgentRoutes(); _routeDungeon=null; _routeTileCount=0; _routeTiles.Clear(); _routePortals.Clear(); _routeDoors.Clear(); _routeFrame=-1; _routeNavOutcome="not-requested"; }
    private void EnsureMonitorRoutes()
    {
        var runtime=RoundManager.Instance!=null ? RoundManager.Instance.dungeonGenerator : null;
        var dungeon=runtime!=null ? runtime.Generator.CurrentDungeon : null;
        if(dungeon==_routeDungeon && (dungeon==null || dungeon.AllTiles.Count==_routeTileCount)) return;
        ClearMonitorRoutes(); _routeDungeon=dungeon;
        if(dungeon==null) return;
        _routeTileCount=dungeon.AllTiles.Count;
        foreach(var tile in dungeon.AllTiles)
        {
            if(tile==null || !tile.HasValidBounds) continue;
            _routeTiles.Add(tile); var portals=new List<MonitorPortal>(); _routePortals[tile.GetInstanceID()]=portals;
            foreach(var door in tile.UsedDoorways)
            {
                if(door==null || door.ConnectedDoorway==null || door.ConnectedDoorway.Tile==null) continue;
                Vector2 size=door.Socket.Size;
                Vector3 center=door.transform.TransformPoint(new Vector3(0,Mathf.Clamp(size.y*.55f,.7f,1.6f),0));
                Vector3 facing=door.transform.forward;
                portals.Add(new MonitorPortal(tile.GetInstanceID(),door.ConnectedDoorway.Tile.GetInstanceID(),center-facing*.8f,center,center+facing*.8f));
                _routeDoors.AddOpening(door.transform.worldToLocalMatrix,size);
                _agentGeometry.AddOpening(door.transform.worldToLocalMatrix,size);
            }
        }
    }
    bool IGameMonitorRouteAdapter.TryGetRouteRoom(Vector3 position,out MonitorRoom room)
    {
        EnsureMonitorRoutes(); room=default;
        Tile? found=SpectatorInteriorVisibility.SpecialEntrance(_routeDungeon,position); float best=float.PositiveInfinity;
        // Prefer the floor's owning tile so vertically stacked bounds cannot create a floor shortcut.
        if(found==null && Physics.Raycast(position,Vector3.down,out var hit,12f,MonitorWorldMask,QueryTriggerInteraction.Ignore))
        {
            var tile=hit.collider.GetComponentInParent<Tile>();
            if(tile!=null && tile.Dungeon==_routeDungeon && tile.HasValidBounds) found=tile;
        }
        if(found==null) foreach(var tile in _routeTiles)
        {
            if(tile==null || !tile.gameObject.activeInHierarchy) continue;
            var bounds=tile.Placement.LocalBounds; bounds.Expand(.2f);
            if(!bounds.Contains(tile.transform.InverseTransformPoint(position))) continue;
            float volume=bounds.size.x*bounds.size.y*bounds.size.z;
            if(volume<best) { best=volume; found=tile; }
        }
        if(found==null) return false;
        room=new MonitorRoom(found.GetInstanceID(),found.Placement.LocalBounds,found.transform.localToWorldMatrix,found.transform.worldToLocalMatrix);
        return true;
    }
    void IGameMonitorRouteAdapter.GetMonitorPortals(int roomId,List<MonitorPortal> result)
    { EnsureMonitorRoutes(); result.Clear(); if(_routePortals.TryGetValue(roomId,out var list)) result.AddRange(list); }
    bool IGameMonitorRouteAdapter.IsMonitorTravelClear(Vector3 from,Vector3 to)
    { EnsureMonitorRoutes(); return _routeDoors.ClearPath(from,to,MonitorWorldMask); }
    bool IGameMonitorWallTransitAdapter.IsMonitorWallTransitClear(Vector3 from,Vector3 to)
    { EnsureMonitorRoutes(); return _routeDoors.ClearWallTransit(from,to,MonitorWorldMask); }
    bool IGameMonitorMotionAdapter.TryMoveMonitorCamera(Vector3 from,Vector3 requested,out Vector3 position,out bool sliding)
        => _routeDoors.TryMove(from,requested,MonitorWorldMask,out position,out sliding);
    bool IGameMonitorRouteAdapter.TryGetMonitorNavRoute(Vector3 from,Vector3 to,List<Vector3> points)
    {
        points.Clear();
        if(_routeFrame==Time.frameCount) { _routeNavOutcome="frame-budget"; return false; }
        _routeFrame=Time.frameCount;
        if(!Physics.Raycast(from,Vector3.down,out var floorA,12f,MonitorWorldMask,QueryTriggerInteraction.Ignore)
            || !Physics.Raycast(to,Vector3.down,out var floorB,12f,MonitorWorldMask,QueryTriggerInteraction.Ignore)) { _routeNavOutcome="floor-missing"; return false; }
        if(!NavMesh.SamplePosition(floorA.point,out var a,.8f,NavMesh.AllAreas)
            || !NavMesh.SamplePosition(floorB.point,out var b,.8f,NavMesh.AllAreas)
            || Mathf.Abs(a.position.y-floorA.point.y)>.5f || Mathf.Abs(b.position.y-floorB.point.y)>.5f) { _routeNavOutcome="sample-or-floor-mismatch"; return false; }
        if(!NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,_routeNav) || _routeNav.status!=NavMeshPathStatus.PathComplete) { _routeNavOutcome="no-complete-nav-path"; return false; }
        int count=_routeNav.GetCornersNonAlloc(_routeCorners);
        if(count<2 || count>=_routeCorners.Length) { _routeNavOutcome="corner-count-invalid"; return false; }
        points.Add(from);
        for(int i=1;i<count-1;i++) points.Add(_routeCorners[i]+Vector3.up*1.3f);
        points.Add(to);
        _routeNavOutcome="candidate-awaiting-physics-validation";
        // Navigation supplies candidates only. The planner validates every lifted segment and endpoint.
        return true;
    }
}
