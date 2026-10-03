using System.Collections.Generic;
using System.Diagnostics;
using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

internal enum MonitorAgentRouteStatus { Idle, Searching, Complete, Unavailable }

/// <summary>Ordered passage legs, verified navigation candidates, then bounded free-space A*.</summary>
internal sealed class MonitorAgentRoutePlanner
{
    private const int NodeLimit = 4096, HeapLimit = 8192, RoomLimit = 512;
    private readonly List<Vector3> _path = new(128), _goals = new(64), _nav = new(128), _reverse = new(128);
    private readonly List<MonitorPortal> _portals = new(16), _itinerary = new(32);
    private readonly List<int> _roomQueue = new(64);
    private readonly Dictionary<int, MonitorPortal> _roomParents = new();
    private readonly Dictionary<int, MonitorRoom> _rooms = new();
    private readonly Dictionary<Vector3Int, int> _visited = new();
    private readonly List<Node> _nodes = new(NodeLimit);
    private readonly List<HeapEntry> _heap = new(HeapLimit);
    private readonly Dictionary<(Vector3, Vector3), CachedProbe> _edges = new();
    private readonly Stopwatch _watch = new();
    private static readonly Vector3Int[] Directions = MakeDirections();
    private Vector3 _from, _destination, _start, _goal;
    private MonitorRoom _startRoom, _goalRoom;
    private Bounds _bounds;
    private bool _knownRooms, _legRoomsKnown, _legHasPortal, _fine, _checkedGoal;
    private int _legFromRoom, _legToRoom;
    private MonitorPortal _legPortal;
    private float _cell, _now;
    private int _revision = -1, _roomCursor, _leg, _stage, _navCursor, _expanding = -1, _neighbor, _pullAnchor, _pullCandidate;
    private string _reason = "idle";
    internal MonitorAgentRouteStatus Status { get; private set; }
    internal int Expanded { get; private set; }
    internal int Queries { get; private set; }

    internal void Clear()
    {
        Status = MonitorAgentRouteStatus.Idle;
        _path.Clear(); _goals.Clear(); _nav.Clear(); _reverse.Clear(); _itinerary.Clear();
        _roomQueue.Clear(); _roomParents.Clear(); _rooms.Clear(); _nodes.Clear(); _heap.Clear(); _visited.Clear();
        _stage = _roomCursor = _leg = 0; _expanding = -1; Expanded = Queries = 0; _reason = "idle";
    }

    internal void Request(IGameMonitorCameraAdapter adapter, Vector3 from, Vector3 goal, float now)
    {
        Clear(); _from = from; _destination = goal; _now = now;
        if (!(adapter is IGameMonitorAgentAdapter world)) { Unavailable("agent-interop-missing"); return; }
        int revision = world.MonitorAgentWorldRevision;
        if (revision != _revision) { _edges.Clear(); _revision = revision; }
        Status = MonitorAgentRouteStatus.Searching; _reason = "topology";
    }

    internal void Tick(IGameMonitorCameraAdapter adapter, float now)
    {
        if (Status != MonitorAgentRouteStatus.Searching) return;
        var world = (IGameMonitorAgentAdapter)adapter;
        if (world.MonitorAgentWorldRevision != _revision) { Unavailable("world-changed"); return; }
        _now = now;
        int before = world.MonitorAgentQueries, work = 0;
        _watch.Restart();
        while (Status == MonitorAgentRouteStatus.Searching && work++ < 192 && _watch.Elapsed.TotalMilliseconds < 1)
        {
            if (_stage == 0)
            {
                _knownRooms = world.TryGetMonitorAgentRoom(_from, out _startRoom)
                    & world.TryGetMonitorAgentRoom(_destination, out _goalRoom);
                _path.Add(_from);
                if (_knownRooms)
                {
                    _rooms[_startRoom.Id] = _startRoom; _rooms[_goalRoom.Id] = _goalRoom;
                    _roomQueue.Add(_startRoom.Id); _roomParents[_startRoom.Id] = default;
                    _stage = 1;
                }
                else { _goals.Add(_destination); _stage = 2; }
                continue;
            }
            if (_stage == 1)
            {
                if (_roomCursor == _roomQueue.Count) { Unavailable("room-graph-disconnected"); break; }
                int room = _roomQueue[_roomCursor++];
                if (room == _goalRoom.Id)
                {
                    while (room != _startRoom.Id)
                    { var portal = _roomParents[room]; _itinerary.Add(portal); room = portal.From; }
                    _itinerary.Reverse();
                    foreach (var portal in _itinerary)
                    { _goals.Add(portal.Entry); _goals.Add(portal.Center); _goals.Add(portal.Exit); }
                    _goals.Add(_destination); _navCursor = 0; _stage = 8;
                    continue;
                }
                if (!(adapter is IGameMonitorRouteAdapter topology)) { Unavailable("portal-interop-missing"); break; }
                topology.GetMonitorPortals(room, _portals);
                foreach (var portal in _portals)
                    if (!_roomParents.ContainsKey(portal.To))
                    { _roomParents[portal.To] = portal; _roomQueue.Add(portal.To); }
                if (_roomQueue.Count > RoomLimit) Unavailable("room-limit");
                continue;
            }
            if (_stage == 8)
            {
                // Resolve room extents once, not by raycasting at every expanded grid node.
                if (_navCursor < _itinerary.Count)
                {
                    var portal = _itinerary[_navCursor++];
                    if (world.TryGetMonitorAgentRoom(portal.Entry, out var a)) _rooms[a.Id] = a;
                    if (world.TryGetMonitorAgentRoom(portal.Exit, out var b)) _rooms[b.Id] = b;
                    if (world.MonitorAgentQueries >= 62) break;
                }
                else _stage = 2;
                continue;
            }
            if (_stage == 2)
            {
                if (_leg == _goals.Count)
                { Status = MonitorAgentRouteStatus.Complete; _reason = "complete"; break; }
                _start = _path[_path.Count - 1]; _goal = _goals[_leg];
                SetBounds(); _nav.Clear(); _navCursor = 1; _stage = 3;
                continue;
            }
            if (_stage == 3)
            {
                var endpoint = Probe(world, _goal, _goal);
                if (endpoint.Status == MonitorAgentProbeStatus.Deferred) break;
                if (!endpoint.Passable) { Unavailable("occupied-leg-endpoint"); break; }
                var direct = Probe(world, _start, _goal);
                if (direct.Status == MonitorAgentProbeStatus.Deferred) break;
                if (direct.Passable && SegmentAllowed(_start, _goal)) { Append(_goal); _leg++; _stage = 2; continue; }
                _stage = 4; continue;
            }
            if (_stage == 4)
            {
                if (world.MonitorAgentQueries > 46) break;
                bool navigation = world.TryGetMonitorAgentNavRoute(_start, _goal, _nav);
                if (navigation && ValidCandidate()) { _stage = 5; _reason = "validating-navigation"; }
                else StartGrid(false);
                // One synchronous NavMesh candidate request per tick; search work resumes next tick.
                break;
            }
            if (_stage == 5)
            {
                Vector3 a = _nav[_navCursor - 1], b = _nav[_navCursor];
                if (!SegmentAllowed(a, b)) { StartGrid(false); continue; }
                var probe = Probe(world, a, b);
                if (probe.Status == MonitorAgentProbeStatus.Deferred) break;
                if (!probe.Passable) { StartGrid(false); continue; }
                if (++_navCursor == _nav.Count)
                    StartPull("simplifying-navigation-leg");
                continue;
            }
            if (_stage == 9)
            {
                // Only this leg is shortcut. The previously committed prefix and every
                // Entry/Center/Exit boundary remain in their original passage order.
                Vector3 from = _nav[_pullAnchor], candidate = _nav[_pullCandidate];
                MonitorAgentProbe probe = SegmentAllowed(from, candidate)
                    ? Probe(world, from, candidate) : new MonitorAgentProbe(MonitorAgentProbeStatus.Blocked);
                if (probe.Status == MonitorAgentProbeStatus.Deferred) break;
                if (probe.Passable)
                {
                    Append(candidate); _pullAnchor = _pullCandidate;
                    if (_pullAnchor == _nav.Count - 1) { _leg++; _stage = 2; }
                    else _pullCandidate = _nav.Count - 1;
                }
                else if (_pullCandidate > _pullAnchor + 1) _pullCandidate--;
                else { Unavailable("leg-changed-during-shortcut"); break; }
                continue;
            }
            if (_stage == 6)
            {
                if (_expanding < 0)
                {
                    if (_heap.Count == 0)
                    {
                        if (!_fine) { StartGrid(true); continue; }
                        Unavailable("free-space-exhausted"); break;
                    }
                    var entry = Pop(); var node = _nodes[entry.Index];
                    if (node.Closed || entry.Priority > node.Priority + .0001f) continue;
                    node.Closed = true; _nodes[entry.Index] = node; Expanded++;
                    _expanding = entry.Index; _neighbor = 0; _checkedGoal = false;
                }
                var current = _nodes[_expanding]; Vector3 point = Position(current.Cell);
                if (!_checkedGoal)
                {
                    if ((point - _goal).sqrMagnitude < _cell * _cell * 9 || (Expanded & 15) == 0)
                    {
                        var probe = Probe(world, point, _goal);
                        if (probe.Status == MonitorAgentProbeStatus.Deferred) break;
                        if (probe.Passable && SegmentAllowed(point, _goal))
                        { CompleteLeg(_expanding); continue; }
                    }
                    _checkedGoal = true;
                }
                if (_neighbor == Directions.Length) { _expanding = -1; continue; }
                Vector3Int offset = Directions[_neighbor], cell = current.Cell + offset;
                Vector3 next = Position(cell);
                if (!Allowed(next)) { _neighbor++; continue; }
                float cost = current.Cost + ((Vector3)offset).magnitude * _cell;
                bool seen = _visited.TryGetValue(cell, out int index);
                if (seen && cost >= _nodes[index].Cost - .0001f) { _neighbor++; continue; }
                var edge = Probe(world, point, next);
                if (edge.Status == MonitorAgentProbeStatus.Deferred) break;
                _neighbor++;
                if (!edge.Passable) continue;
                if (!seen)
                {
                    if (_nodes.Count >= (_fine ? NodeLimit : 1024))
                    {
                        if (!_fine) { StartGrid(true); continue; }
                        Unavailable("free-space-node-limit"); break;
                    }
                    index = _nodes.Count; _nodes.Add(default); _visited[cell] = index;
                }
                var replacement = new Node { Cell = cell, Parent = _expanding, Cost = cost,
                    Priority = cost + (next - _goal).magnitude * 1.35f };
                _nodes[index] = replacement;
                if (_heap.Count >= HeapLimit) { Unavailable("free-space-open-limit"); break; }
                Push(new HeapEntry(index, replacement.Priority));
                continue;
            }
        }
        _watch.Stop(); Queries += world.MonitorAgentQueries - before;
    }

    internal void CopyPath(List<Vector3> result)
    { result.Clear(); if (Status == MonitorAgentRouteStatus.Complete) result.AddRange(_path); }
    internal void CopyReferencePath(List<Vector3> result)
    {
        result.Clear(); result.Add(_from);
        for (int i = 1; i < _path.Count; i++) result.Add(_path[i]);
        // Preserve the certified prefix and the known Entry/Center/Exit order.
        // An unresolved topology contributes no invented intermediate waypoints.
        for (int i = _leg; i < _goals.Count; i++)
            if (!result[result.Count - 1].Equals(_goals[i])) result.Add(_goals[i]);
        if (!result[result.Count - 1].Equals(_destination)) result.Add(_destination);
    }
    internal string Describe() => $"{Status}/{_reason},leg={_leg}/{_goals.Count},nodes={_nodes.Count},expanded={Expanded},queries={Queries},cell={_cell:F2},from={_from:F2},goal={_destination:F2}";

    private MonitorAgentProbe Probe(IGameMonitorAgentAdapter world, Vector3 from, Vector3 to)
    {
        var key = (from, to);
        if (_edges.TryGetValue(key, out var cached) && cached.Expires > _now) return cached.Probe;
        var probe = world.ProbeMonitorAgentTravel(from, to);
        if (probe.Status != MonitorAgentProbeStatus.Deferred)
        {
            if (_edges.Count >= 4096) _edges.Clear();
            _edges[key] = new CachedProbe(probe, _now + .2f);
        }
        return probe;
    }

    private void SetBounds()
    {
        _bounds = new Bounds((_start + _goal) * .5f, Vector3.zero);
        _bounds.Encapsulate(_start); _bounds.Encapsulate(_goal);
        int portalIndex=_leg/3;
        _legHasPortal=_knownRooms && portalIndex<_itinerary.Count && _leg%3!=0;
        if (_knownRooms && portalIndex<_itinerary.Count)
        {
            _legPortal=_itinerary[portalIndex];
            _legFromRoom=_legPortal.From;
            _legToRoom=_legHasPortal ? _legPortal.To : _legFromRoom;
        }
        else _legFromRoom=_legToRoom=_goalRoom.Id;
        _legRoomsKnown=_knownRooms && _rooms.ContainsKey(_legFromRoom) && _rooms.ContainsKey(_legToRoom);
        if (_legRoomsKnown)
        {
            EncapsulateRoom(_rooms[_legFromRoom]);
            if (_legToRoom!=_legFromRoom) EncapsulateRoom(_rooms[_legToRoom]);
            _bounds.Expand(.8f);
        }
        else _bounds.Expand(new Vector3(8, 16, 8));
    }
    private void EncapsulateRoom(MonitorRoom room)
    {
        Vector3 min=room.Bounds.min, max=room.Bounds.max;
        for (int i=0; i<8; i++)
            _bounds.Encapsulate(room.LocalToWorld.MultiplyPoint3x4(new Vector3(
                (i&1)==0 ? min.x : max.x, (i&2)==0 ? min.y : max.y, (i&4)==0 ? min.z : max.z)));
    }
    private bool Allowed(Vector3 point)
    {
        if (!_bounds.Contains(point)) return false;
        if (!_legRoomsKnown) return true;
        return ContainsRoom(_rooms[_legFromRoom],point)
            || (_legToRoom!=_legFromRoom && ContainsRoom(_rooms[_legToRoom],point))
            || (_legHasPortal && DistanceToSegment(point,_legPortal.Entry,_legPortal.Exit)<.8f);
    }
    private static bool ContainsRoom(MonitorRoom room, Vector3 point)
    { var bounds=room.Bounds; bounds.Expand(.8f); return bounds.Contains(room.WorldToLocal.MultiplyPoint3x4(point)); }
    private bool SegmentAllowed(Vector3 from, Vector3 to)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt((to - from).magnitude / .6f));
        for (int i = 0; i <= steps; i++) if (!Allowed(Vector3.Lerp(from, to, (float)i / steps))) return false;
        return true;
    }
    private bool ValidCandidate()
    {
        if (_nav.Count < 2 || (_nav[0] - _start).sqrMagnitude > .0004f
            || (_nav[_nav.Count - 1] - _goal).sqrMagnitude > .0004f) return false;
        for (int i = 0; i < _nav.Count; i++)
        {
            if (!Allowed(_nav[i])) return false;
            for (int j = 0; j < i - 1; j++) if ((_nav[i] - _nav[j]).sqrMagnitude < .0004f) return false;
        }
        return true;
    }
    private void StartGrid(bool fine)
    {
        _fine = fine; _cell = fine ? .6f : 1.2f; _nodes.Clear(); _heap.Clear(); _visited.Clear();
        _expanding = -1; _stage = 6; _reason = fine ? "free-space-fine" : "free-space-coarse";
        var root = new Node { Cell = Vector3Int.zero, Parent = -1, Cost = 0, Priority = (_goal - _start).magnitude * 1.35f };
        _nodes.Add(root); _visited[Vector3Int.zero] = 0; Push(new HeapEntry(0, root.Priority));
    }
    private void CompleteLeg(int index)
    {
        _reverse.Clear();
        while (index > 0) { _reverse.Add(Position(_nodes[index].Cell)); index = _nodes[index].Parent; }
        _nav.Clear(); _nav.Add(_start);
        for (int i = _reverse.Count - 1; i >= 0; i--) _nav.Add(_reverse[i]);
        // A* already verified this exact final chord. String pulling verifies every
        // replacement chord too; it never treats a nearby voxel as the destination.
        _nav.Add(_goal); StartPull("simplifying-spatial-leg");
    }
    private void StartPull(string reason)
    { _pullAnchor = 0; _pullCandidate = _nav.Count - 1; _stage = 9; _reason = reason; }
    private void Append(Vector3 point)
    { if (!_path[_path.Count - 1].Equals(point)) _path.Add(point); }
    private Vector3 Position(Vector3Int cell) => _start + (Vector3)cell * _cell;
    private void Unavailable(string reason) { Status = MonitorAgentRouteStatus.Unavailable; _reason = reason; }
    private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    { Vector3 delta = to - from; float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector3.Dot(point - from, delta) / delta.sqrMagnitude) : 0; return (point - from - delta * t).magnitude; }
    private void Push(HeapEntry entry)
    {
        int child = _heap.Count; _heap.Add(entry);
        while (child > 0)
        {
            int parent = (child - 1) / 2;
            if (_heap[parent].Priority <= entry.Priority) break;
            _heap[child] = _heap[parent]; child = parent;
        }
        _heap[child] = entry;
    }
    private HeapEntry Pop()
    {
        var result = _heap[0]; var tail = _heap[_heap.Count - 1]; _heap.RemoveAt(_heap.Count - 1);
        if (_heap.Count == 0) return result;
        int parent = 0;
        while (parent * 2 + 1 < _heap.Count)
        {
            int child = parent * 2 + 1;
            if (child + 1 < _heap.Count && _heap[child + 1].Priority < _heap[child].Priority) child++;
            if (_heap[child].Priority >= tail.Priority) break;
            _heap[parent] = _heap[child]; parent = child;
        }
        _heap[parent] = tail; return result;
    }
    private static Vector3Int[] MakeDirections()
    {
        var result = new List<Vector3Int>(26);
        for (int length = 1; length <= 3; length++)
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
                if (x * x + y * y + z * z == length) result.Add(new Vector3Int(x, y, z));
        return result.ToArray();
    }
    private struct Node { internal Vector3Int Cell; internal int Parent; internal float Cost, Priority; internal bool Closed; }
    private readonly struct HeapEntry
    { internal readonly int Index; internal readonly float Priority; internal HeapEntry(int index, float priority) { Index = index; Priority = priority; } }
    private readonly struct CachedProbe
    { internal readonly MonitorAgentProbe Probe; internal readonly float Expires; internal CachedProbe(MonitorAgentProbe probe, float expires) { Probe = probe; Expires = expires; } }
}
