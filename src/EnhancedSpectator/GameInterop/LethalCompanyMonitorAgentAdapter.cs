using System.Collections.Generic;
using DunGen;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace EnhancedSpectator.GameInterop;

public sealed partial class LethalCompanySpectatorAdapter : IGameMonitorAgentAdapter
{
    private readonly MonitorAgentGeometry _agentGeometry = new();
    private readonly Dictionary<Vector3, AgentRoomObservation> _agentRooms = new();
    private int _agentWorldRevision, _agentNavFrame = -1;
    private float _agentNow;

    void IGameMonitorAgentAdapter.BeginMonitorAgentFrame(float now, int queryBudget)
    {
        _agentNow = now; _agentGeometry.BeginFrame(Time.frameCount, now, queryBudget);
        EnsureMonitorRoutes();
    }
    int IGameMonitorAgentAdapter.MonitorAgentQueries => _agentGeometry.Queries;
    void IGameMonitorAgentAdapter.SetMonitorAgentQueryLimit(int limit) => _agentGeometry.SetQueryLimit(limit);
    int IGameMonitorAgentAdapter.MonitorAgentWorldRevision { get { EnsureMonitorRoutes(); return _agentWorldRevision; } }
    MonitorAgentProbe IGameMonitorAgentAdapter.ProbeMonitorAgentTravel(Vector3 from, Vector3 to)
        => _agentGeometry.ProbeTravel(from, to, MonitorWorldMask);
    MonitorAgentProbe IGameMonitorAgentAdapter.ProbeMonitorAgentSight(Vector3 from, Vector3 to)
        => _agentGeometry.ProbeSight(from, to, MonitorWorldMask);

    bool IGameMonitorAgentAdapter.TryGetMonitorAgentRoom(Vector3 position, out MonitorRoom room)
    {
        EnsureMonitorRoutes(); room = default;
        if (_routeTiles.Count == 0) return false;
        if (_agentRooms.TryGetValue(position, out var cached) && cached.Expires > _agentNow)
        { room = cached.Room; return true; }
        Tile? found = SpectatorInteriorVisibility.SpecialEntrance(_routeDungeon, position);
        bool sampledFloor = found == null && _agentGeometry.Spend();
        if (sampledFloor
            && Physics.Raycast(position, Vector3.down, out var hit, 12f, MonitorWorldMask, QueryTriggerInteraction.Ignore))
        {
            var owner = hit.collider.GetComponentInParent<Tile>();
            if (owner != null && owner.Dungeon == _routeDungeon && ContainsAgentPoint(owner, position)) found = owner;
        }
        if (found == null)
        {
            float smallest = float.PositiveInfinity;
            foreach (var tile in _routeTiles)
            {
                if (!ContainsAgentPoint(tile, position)) continue;
                Vector3 size = tile.Placement.LocalBounds.size;
                float volume = size.x * size.y * size.z;
                if (volume < smallest) { smallest = volume; found = tile; }
            }
        }
        if (found == null) return false;
        room = new MonitorRoom(found.GetInstanceID(), found.Placement.LocalBounds,
            found.transform.localToWorldMatrix, found.transform.worldToLocalMatrix);
        if (sampledFloor)
        {
            if (_agentRooms.Count >= 512) _agentRooms.Clear();
            _agentRooms[position] = new AgentRoomObservation(room, _agentNow + .2f);
        }
        return true;
    }

    private static bool ContainsAgentPoint(Tile tile, Vector3 position)
    {
        if (!tile.HasValidBounds || !tile.gameObject.activeInHierarchy) return false;
        var bounds = tile.Placement.LocalBounds; bounds.Expand(.8f);
        return bounds.Contains(tile.transform.InverseTransformPoint(position));
    }

    bool IGameMonitorAgentAdapter.TryGetMonitorAgentNavRoute(Vector3 from, Vector3 to, List<Vector3> result)
    {
        result.Clear();
        if (_agentNavFrame == Time.frameCount || _agentGeometry.Remaining < 2) return false;
        _agentNavFrame = Time.frameCount;
        _agentGeometry.Spend();
        if (!Physics.Raycast(from, Vector3.down, out var floorA, 12f, MonitorWorldMask, QueryTriggerInteraction.Ignore)) return false;
        _agentGeometry.Spend();
        if (!Physics.Raycast(to, Vector3.down, out var floorB, 12f, MonitorWorldMask, QueryTriggerInteraction.Ignore)) return false;

        // AI Navigation owns the game's surfaces. Read their agent type; never rebuild, remove,
        // or add NavMesh data that the monsters are using.
        NavMeshSurface? surface = null;
        float nearest = float.PositiveInfinity;
        foreach (var candidate in NavMeshSurface.activeSurfaces)
        {
            if (candidate == null || candidate.navMeshData == null) continue;
            float distance = (candidate.transform.position - floorA.point).sqrMagnitude;
            if (distance < nearest) { nearest = distance; surface = candidate; }
        }
        NavMeshHit a, b = default;
        bool sampled;
        if (surface != null)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = NavMesh.AllAreas };
            sampled = NavMesh.SamplePosition(floorA.point, out a, .8f, filter)
                && NavMesh.SamplePosition(floorB.point, out b, .8f, filter);
            if (!sampled) return false;
            if (Mathf.Abs(a.position.y - floorA.point.y) > .5f || Mathf.Abs(b.position.y - floorB.point.y) > .5f) return false;
            if (!NavMesh.CalculatePath(a.position, b.position, filter, _routeNav)) return false;
        }
        else
        {
            sampled = NavMesh.SamplePosition(floorA.point, out a, .8f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(floorB.point, out b, .8f, NavMesh.AllAreas);
            if (!sampled) return false;
            if (Mathf.Abs(a.position.y - floorA.point.y) > .5f || Mathf.Abs(b.position.y - floorB.point.y) > .5f) return false;
            if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, _routeNav)) return false;
        }
        if (_routeNav.status != NavMeshPathStatus.PathComplete) return false;
        int count = _routeNav.GetCornersNonAlloc(_routeCorners);
        if (count < 2 || count >= _routeCorners.Length) return false;
        result.Add(from);
        for (int i = 1; i < count - 1; i++) result.Add(_routeCorners[i] + Vector3.up * 1.3f);
        result.Add(to);
        _routeNavOutcome = "agent-surface-candidate-awaiting-volume-validation";
        return true;
    }

    private void ClearMonitorAgentRoutes()
    { _agentWorldRevision++; _agentNavFrame = -1; _agentRooms.Clear(); _agentGeometry.Clear(); }

    private readonly struct AgentRoomObservation
    {
        internal readonly MonitorRoom Room;
        internal readonly float Expires;
        internal AgentRoomObservation(MonitorRoom room, float expires) { Room = room; Expires = expires; }
    }
}
