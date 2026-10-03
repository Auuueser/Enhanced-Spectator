using System.Collections.Generic;
using System.Diagnostics;
using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

internal enum MonitorRouteStatus { Idle, Computing, Ready, Blocked }

/// <summary>Streams validated portal legs while a bounded weighted spatial search resolves later obstacles.</summary>
internal sealed class MonitorRoutePlanner
{
    internal const int QueryBudget=32, NodeLimit=4096;
    internal MonitorRouteStatus Status { get; private set; }
    internal readonly List<Vector3> Points=new List<Vector3>(128);
    private readonly List<MonitorPortal> _portals=new List<MonitorPortal>();
    private readonly List<int> _rooms=new List<int>();
    private readonly Dictionary<int,MonitorPortal> _parents=new Dictionary<int,MonitorPortal>();
    private readonly List<MonitorPortal> _itinerary=new List<MonitorPortal>();
    private readonly List<Vector3> _goals=new List<Vector3>(), _nav=new List<Vector3>();
    private readonly Dictionary<Vector3Int,int> _visited=new Dictionary<Vector3Int,int>();
    private readonly List<Node> _nodes=new List<Node>(NodeLimit);
    private readonly List<int> _heap=new List<int>(NodeLimit), _reverse=new List<int>();
    private readonly Stopwatch _watch=new Stopwatch();
    private struct Node { internal Vector3Int Cell; internal int Parent; internal float Cost, Priority; internal bool Closed; }
    private int _portalCursor,_portalAttempt;
    private int _roomCursor,_targetRoom,_startRoom,_leg,_phase,_navCursor,_expanding=-1,_neighbor;
    private Vector3 _from,_to,_start,_goal;
    private MonitorRoom _bounds;
    private bool _hasBounds,_flatSearch,_navUsable;
    private int _ticks,_totalQueries;
    private string _reason="none";
    private const float Cell=.65f;
    internal void Clear() { Status=MonitorRouteStatus.Idle; Points.Clear(); _goals.Clear(); _nav.Clear(); _heap.Clear(); _nodes.Clear(); }
    internal void Begin(IGameMonitorRouteAdapter world,Vector3 from,Vector3 to)
    {
        Clear(); _from=from; _to=to; _phase=0; _roomCursor=0; _leg=0;
        _ticks=_totalQueries=0; _portalCursor=_portalAttempt=0; _reason="planning";
        _rooms.Clear(); _parents.Clear(); _itinerary.Clear();
        if(!world.TryGetRouteRoom(from,out var a) || !world.TryGetRouteRoom(to,out var b))
        {
            // Missing room metadata is recoverable, but never authorizes a wall shortcut.
            if(world.IsMonitorTravelClear(from,to)) { Points.Add(from); Points.Add(to); Status=MonitorRouteStatus.Ready; }
            else { Status=MonitorRouteStatus.Blocked; _reason="room-metadata-missing-and-direct-blocked"; }
            return;
        }
        _startRoom=a.Id; _targetRoom=b.Id; _rooms.Add(a.Id); _parents[a.Id]=default;
        Status=MonitorRouteStatus.Computing;
    }
    internal void Tick(IGameMonitorRouteAdapter world)
    {
        if(Status!=MonitorRouteStatus.Computing) return;
        _ticks++; _watch.Restart(); int queries=0, work=0;
        while(Status==MonitorRouteStatus.Computing && queries<QueryBudget-1 && work++<128 && _watch.Elapsed.TotalMilliseconds<1)
        {
            if(_phase==0)
            {
                if(_roomCursor>=_rooms.Count) { Status=MonitorRouteStatus.Blocked; _reason="disconnected-room-graph"; break; }
                int id=_rooms[_roomCursor++];
                if(id==_targetRoom)
                {
                    while(id!=_startRoom) { var portal=_parents[id]; _itinerary.Add(portal); id=portal.From; }
                    _itinerary.Reverse();
                    Points.Add(_from); _phase=4; continue;
                }
                world.GetMonitorPortals(id,_portals);
                foreach(var portal in _portals) if(!_parents.ContainsKey(portal.To)) { _parents[portal.To]=portal; _rooms.Add(portal.To); }
                if(_rooms.Count>1024) { Status=MonitorRouteStatus.Blocked; _reason="room-limit"; }
                continue;
            }
            if(_phase==4)
            {
                if(_portalCursor>=_itinerary.Count) { _goals.Add(_to); _phase=1; continue; }
                // Door anchors can overlap a stair lip, trim or movable prop. Resolve a nearby
                // full crossing, not just an empty center. Every variant uses the same solid-world rules.
                if(queries>QueryBudget-5) break;
                var portal=_itinerary[_portalCursor];
                Vector3 side=Vector3.Cross(Vector3.up,(portal.Exit-portal.Entry).normalized);
                int attempt=_portalAttempt++;
                float lift=attempt==0 ? 0 : attempt<=2 ? (attempt==1 ? .4f : -.4f)
                    : attempt<=4 ? (attempt==3 ? .8f : -.8f) : (attempt-5)/2*.4f;
                float lateral=attempt<5 ? 0 : (attempt%2==1 ? .35f : -.35f);
                Vector3 offset=Vector3.up*lift+side*lateral;
                Vector3 entry=portal.Entry+offset,center=portal.Center+offset,exit=portal.Exit+offset;
                queries+=5;
                if(world.IsMonitorTravelClear(entry,entry) && world.IsMonitorTravelClear(center,center)
                    && world.IsMonitorTravelClear(exit,exit) && world.IsMonitorTravelClear(entry,center)
                    && world.IsMonitorTravelClear(center,exit))
                {
                    _goals.Add(entry); _goals.Add(center); _goals.Add(exit);
                    _portalCursor++; _portalAttempt=0;
                }
                else if(_portalAttempt>=11) { Status=MonitorRouteStatus.Blocked; _reason="portal-crossing-obstructed"; }
                continue;
            }
            if(_phase==1)
            {
                if(_leg>=_goals.Count) { Status=MonitorRouteStatus.Ready; _reason="complete"; break; }
                _start=Points[Points.Count-1]; _goal=_goals[_leg]; _nav.Clear(); _navUsable=false; queries+=2;
                // Searching thousands of nodes cannot reach an occupied endpoint.
                if(!world.IsMonitorTravelClear(_goal,_goal))
                { Status=MonitorRouteStatus.Blocked; _reason="occupied-leg-endpoint"; break; }
                if(world.IsMonitorTravelClear(_start,_goal)) { Points.Add(_goal); _leg++; continue; }
                _hasBounds=world.TryGetRouteRoom(_start,out _bounds);
                _navCursor=1;
                if(world.TryGetMonitorNavRoute(_start,_goal,_nav) && ValidNavigation()) { _navUsable=true; _phase=2; }
                else StartGrid();
                // Only one navigation request per planner frame, even when the adapter has no frame clock.
                break;
            }
            if(_phase==2)
            {
                queries++;
                if(!world.IsMonitorTravelClear(_nav[_navCursor-1],_nav[_navCursor])) { StartGrid(); continue; }
                if(++_navCursor==_nav.Count)
                { for(int i=1;i<_nav.Count;i++) Points.Add(_nav[i]); _leg++; _phase=1; }
                continue;
            }
            if(_phase==3)
            {
                if(_expanding<0)
                {
                    if(_heap.Count==0)
                    {
                        if(_flatSearch) { StartGrid(false); continue; }
                        Status=MonitorRouteStatus.Blocked; _reason="spatial-search-exhausted"; break;
                    }
                    int index=Pop(); var node=_nodes[index]; if(node.Closed) continue;
                    node.Closed=true; _nodes[index]=node; Vector3 p=Position(node.Cell); queries++;
                    if(world.IsMonitorTravelClear(p,_goal))
                    {
                        _reverse.Clear();
                        for(int n=index;n>0;n=_nodes[n].Parent) _reverse.Add(n);
                        for(int n=_reverse.Count-1;n>=0;n--) Points.Add(Position(_nodes[_reverse[n]].Cell));
                        Points.Add(_goal); _leg++; _phase=1; continue;
                    }
                    _expanding=index; _neighbor=0;
                    continue;
                }
                Node current=_nodes[_expanding];
                int direction=_neighbor++; int x=direction%3-1, y=(direction/3)%3-1, z=direction/9-1;
                if(_neighbor==27) { _expanding=-1; }
                if(x==0&&y==0&&z==0) continue;
                if(_flatSearch && y!=0) continue;
                Vector3Int cell=current.Cell+new Vector3Int(x,y,z); Vector3 point=Position(cell);
                if(!InBounds(point) || _visited.ContainsKey(cell)) continue;
                queries++;
                if(!world.IsMonitorTravelClear(Position(current.Cell),point)) continue;
                if(_flatSearch && _nodes.Count>=256) { StartGrid(false); continue; }
                if(_nodes.Count>=NodeLimit) { Status=MonitorRouteStatus.Blocked; _reason="spatial-node-limit"; break; }
                // Routes need to be safe and responsive, not globally shortest. A weighted goal heuristic
                // avoids spending seconds filling a 3D volume along an unpassable wall. Prefer level travel
                // when it is available, while keeping vertical neighbors for stairs and floor openings.
                float cost=current.Cost+new Vector3(x,y,z).magnitude*Cell+Mathf.Abs(y)*Cell*.35f;
                var next=new Node { Cell=cell,Parent=_visited[current.Cell],Cost=cost,Priority=cost+2f*(point-_goal).magnitude };
                int ni=_nodes.Count; _nodes.Add(next); _visited[cell]=ni; Push(ni);
            }
        }
        _watch.Stop(); _totalQueries+=queries;
        if(Points.Count>4096) { Status=MonitorRouteStatus.Blocked; _reason="waypoint-limit"; }
    }
    internal string Describe() => Status==MonitorRouteStatus.Idle ? "Idle"
        : $"{Status}/{_reason},phase={_phase},flat={_flatSearch},leg={_leg}/{_goals.Count},nodes={_nodes.Count},open={_heap.Count},ticks={_ticks},queries={_totalQueries},rooms={_startRoom}->{_targetRoom},from={_from:F2},to={_to:F2},legGoal={_goal:F2}";
    // Recover only the current leg, then resume ordinary planning for the remaining itinerary.
    // A blocked trim/door must not authorize one direct chord through every subsequent room.
    internal void AllowGeometryPassage(IGameMonitorRouteAdapter world,IReadOnlyList<Vector3>? subjectTrail=null)
    {
        if(Status==MonitorRouteStatus.Idle || Status==MonitorRouteStatus.Ready) return;
        if(Points.Count==0) Points.Add(_from);
        if(_phase==4 && _portalCursor<_itinerary.Count)
        {
            var portal=_itinerary[_portalCursor++];
            _goals.Add(portal.Entry); _goals.Add(portal.Center); _goals.Add(portal.Exit);
            _portalAttempt=0; Status=MonitorRouteStatus.Computing;
            _reason="portal-local-geometry-passage"; return;
        }
        if(_phase>=1 && _phase<=3 && _leg<_goals.Count && _navUsable)
        {
            // Navigation is only eligible after endpoint, height-band and cycle validation.
            // A camera hull touching trim may then follow the same walked route without clipping.
            for(int i=1;i<_nav.Count;i++) Points.Add(_nav[i]);
            _leg++; _phase=1; Status=MonitorRouteStatus.Computing;
            _reason="validated-navigation-geometry-passage"; return;
        }
        if(_phase>=1 && _phase<=3 && _leg<_itinerary.Count*3 && _leg%3!=0)
        {
            // Entry-to-center-to-exit is a verified connected doorway, even when its frame
            // or closed leaf occupies the camera hull. This exception cannot span a room.
            Points.Add(_goal); _leg++; _phase=1; Status=MonitorRouteStatus.Computing;
            _reason="confirmed-doorway-segment-passage"; return;
        }
        if(subjectTrail!=null && TrySubjectTrail(world,subjectTrail))
        { Status=MonitorRouteStatus.Ready; _reason="recorded-subject-trail-recovery"; return; }
        // Keep searching and keep every published segment. Time alone must never invent a
        // chord through a wall/floor, or append the tail of a rejected navigation candidate.
        _reason="waiting-for-connected-route";
    }
    private bool ValidNavigation()
    {
        if(_nav.Count<2 || (_nav[0]-_start).sqrMagnitude>.04f || (_nav[_nav.Count-1]-_goal).sqrMagnitude>.04f) return false;
        // Nav corners are lifted from the walking surface by 1.3 m, while a normal
        // camera endpoint can be higher. Allow that framing offset, not another storey.
        float min=Mathf.Min(_start.y,_goal.y)-1.5f,max=Mathf.Max(_start.y,_goal.y)+.65f;
        for(int i=0;i<_nav.Count;i++)
        {
            var p=_nav[i];
            if(float.IsNaN(p.sqrMagnitude) || float.IsInfinity(p.sqrMagnitude) || p.y<min || p.y>max) return false;
            for(int j=0;j<i-1;j++) if((p-_nav[j]).sqrMagnitude<.04f) return false;
        }
        return true;
    }
    private bool TrySubjectTrail(IGameMonitorRouteAdapter world,IReadOnlyList<Vector3> trail)
    {
        if(trail.Count<2) return false;
        Vector3 from=Points[Points.Count-1];
        int first=-1,last=-1; float nearest=16f,goalDistance=float.PositiveInfinity;
        for(int i=0;i<trail.Count;i++)
        {
            float d=(trail[i]-from).sqrMagnitude;
            if(d<nearest) { nearest=d; first=i; }
        }
        if(first<0) return false;
        // A returning subject may have passed this position several times. Join the
        // newest nearby sample, not the oldest visit and its obsolete outward detour.
        for(int i=first+1;i<trail.Count;i++)
            if((trail[i]-from).sqrMagnitude<=nearest+.16f) first=i;
        for(int i=first;i<trail.Count;i++)
        {
            float d=(trail[i]-_to).sqrMagnitude;
            if(d<goalDistance) { goalDistance=d; last=i; }
        }
        for(int i=last+1;i<trail.Count;i++)
            if((trail[i]-_to).sqrMagnitude<=goalDistance+.16f) last=i;
        if(last<=first || !world.IsMonitorTravelClear(from,trail[first])) return false;
        for(int i=first+1;i<=last;i++) if((trail[i]-trail[i-1]).sqrMagnitude>9f) return false;
        for(int i=first;i<=last;i++) Points.Add(trail[i]);
        if(world.IsMonitorTravelClear(trail[last],_to)) Points.Add(_to);
        return true;
    }
    private void StartGrid(bool preferLevel=true)
    {
        _phase=3; _visited.Clear(); _nodes.Clear(); _heap.Clear(); _expanding=-1;
        // Most doorway detours are horizontal. Search that small slice first, then retry in full 3D
        // for stairs, overhead obstructions or a failed slice. All diagonal and rising joins are cast.
        _flatSearch=preferLevel && Mathf.Abs(_goal.y-_start.y)<2.5f;
        _nodes.Add(new Node { Cell=Vector3Int.zero, Parent=-1,Priority=(_goal-_start).magnitude });
        _visited[Vector3Int.zero]=0; Push(0);
    }
    private Vector3 Position(Vector3Int cell) => _start+(Vector3)cell*Cell;
    private bool InBounds(Vector3 point)
    {
        if((point-_start).sqrMagnitude>1600 || point.y<Mathf.Min(_start.y,_goal.y)-.35f
            || point.y>Mathf.Max(_start.y,_goal.y)+.65f) return false;
        if(!_hasBounds) return (point-_goal).sqrMagnitude<1600;
        Bounds bounds=_bounds.Bounds; bounds.Expand(1.8f);
        return bounds.Contains(_bounds.WorldToLocal.MultiplyPoint3x4(point));
    }
    private void Push(int index)
    {
        int i=_heap.Count; _heap.Add(index);
        while(i>0) { int p=(i-1)/2; if(_nodes[_heap[p]].Priority<=_nodes[index].Priority) break; _heap[i]=_heap[p]; i=p; }
        _heap[i]=index;
    }
    private int Pop()
    {
        int result=_heap[0], tail=_heap[_heap.Count-1]; _heap.RemoveAt(_heap.Count-1);
        if(_heap.Count==0) return result;
        int i=0;
        while(i*2+1<_heap.Count)
        {
            int c=i*2+1;
            if(c+1<_heap.Count && _nodes[_heap[c+1]].Priority<_nodes[_heap[c]].Priority) c++;
            if(_nodes[tail].Priority<=_nodes[_heap[c]].Priority) break;
            _heap[i]=_heap[c]; i=c;
        }
        _heap[i]=tail; return result;
    }
}
