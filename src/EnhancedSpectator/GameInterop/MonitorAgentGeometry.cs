using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnhancedSpectator.GameInterop;

/// <summary>Budgeted observations of a virtual camera hull. Never clips or moves a camera.</summary>
internal sealed class MonitorAgentGeometry
{
    private const float Radius = MonitorCameraGeometry.Clearance + .04f;
    private const float Subdivision = .2f;
    private const int CacheLimit = 4096;
    private readonly PhysicsScene _world;
    private readonly RaycastHit[] _hits = new RaycastHit[32];
    private readonly Collider[] _overlaps = new Collider[16];
    private readonly Dictionary<(Vector3, Vector3, int, bool), TravelWork> _edges = new();
    private readonly Dictionary<(Vector3, int), PointWork> _points = new();
    private readonly Dictionary<int, bool> _doors = new();
    private readonly List<Opening> _openings = new();
    private SphereCollider? _shape;
    private Scene _shapeScene;
    private int _frame = -1, _budget, _frameBudget;
    private float _now;
    internal int Queries { get; private set; }
    internal int Remaining => _budget - Queries;

    internal MonitorAgentGeometry() : this(Physics.defaultPhysicsScene) { }
    internal MonitorAgentGeometry(PhysicsScene world) => _world = world;

    internal void BeginFrame(int frame, float now, int queryBudget)
    {
        _now = now;
        if (_frame == frame) return;
        _frame = frame; _budget = _frameBudget = queryBudget; Queries = 0;
    }

    internal void SetQueryLimit(int limit) => _budget = Mathf.Min(limit, _frameBudget);

    // Interop's floor samples share the same raw-query budget as travel and sight.
    internal bool Spend()
    {
        if (Queries >= _budget) return false;
        Queries++; return true;
    }

    internal void AddOpening(Matrix4x4 worldToLocal, Vector2 size)
        => _openings.Add(new Opening(worldToLocal, size));

    internal void Clear()
    {
        _edges.Clear(); _points.Clear(); _doors.Clear(); _openings.Clear();
        if (_shape != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(_shape.gameObject);
            else UnityEngine.Object.DestroyImmediate(_shape.gameObject);
        }
        _shape = null;
        if (_shapeScene.IsValid() && _shapeScene.isLoaded) SceneManager.UnloadSceneAsync(_shapeScene);
        _shapeScene = default;
    }

    internal MonitorAgentProbe ProbeSight(Vector3 from, Vector3 to, int mask)
    {
        var key = (from, to, mask, true);
        if (_edges.TryGetValue(key, out var cached) && cached.Expires > _now) return cached.Result;
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < .000001f) return new MonitorAgentProbe(MonitorAgentProbeStatus.Clear);
        if (!Spend()) return Deferred;
        int count = _world.Raycast(from, delta.normalized, _hits, delta.magnitude, mask, QueryTriggerInteraction.Ignore);
        var result = new MonitorAgentProbe(MonitorAgentProbeStatus.Clear);
        if (count == _hits.Length) result = new MonitorAgentProbe(MonitorAgentProbeStatus.Blocked);
        else for (int i = 0; i < count; i++)
            if (result.Status == MonitorAgentProbeStatus.Clear || _hits[i].distance < result.Distance)
                result = new MonitorAgentProbe(MonitorAgentProbeStatus.Blocked, _hits[i].distance, _hits[i].normal);
        TrimEdges();
        _edges[key] = new TravelWork(from, to) { Result = result, Finished = true, Expires = _now + .2f };
        return result;
    }

    internal MonitorAgentProbe ProbeTravel(Vector3 from, Vector3 to, int mask)
    {
        var key = (from, to, mask, false);
        if (!_edges.TryGetValue(key, out var work) || work.Expires <= _now)
        {
            TrimEdges(); work = new TravelWork(from, to) { Expires = _now + 2f }; _edges[key] = work;
        }
        if (work.Finished) return work.Result;
        while (true)
        {
            MonitorAgentProbe probe;
            switch (work.Stage)
            {
                case 0:
                    probe = ProbePoint(work.From, mask);
                    if (!Accept(work, probe)) return probe;
                    work.Stage = 1; break;
                case 1:
                    probe = ProbePoint(work.To, mask);
                    if (!Accept(work, probe)) return probe;
                    work.Stage = 2; break;
                case 2:
                    probe = Sweep(work.From, work.To, mask, work);
                    if (!Accept(work, probe)) return probe;
                    work.Stage = 3; break;
                case 3:
                    probe = Ray(work.From, work.To, mask);
                    if (!Accept(work, probe)) return probe;
                    if (!work.Uncertain || work.Length <= Subdivision) return Finish(work);
                    work.Steps = Mathf.CeilToInt(work.Length / Subdivision);
                    work.Stage = 4; break;
                default:
                    // Only uncertain concave/shared meshes need fine checks. Resume the same
                    // subsegment next frame rather than treating the exhausted budget as a wall.
                    Vector3 a = Vector3.Lerp(work.From, work.To, (float)work.Index / work.Steps);
                    Vector3 b = Vector3.Lerp(work.From, work.To, (float)(work.Index + 1) / work.Steps);
                    if (work.FineStage == 0)
                    {
                        probe = ProbePoint(b, mask);
                        if (!Accept(work, probe)) return probe;
                        work.FineStage = 1;
                    }
                    if (work.FineStage == 1)
                    {
                        probe = Sweep(a, b, mask, work);
                        if (!Accept(work, probe)) return probe;
                        work.FineStage = 2;
                    }
                    probe = Ray(a, b, mask);
                    if (!Accept(work, probe)) return probe;
                    work.FineStage = 0;
                    if (++work.Index == work.Steps) return Finish(work);
                    break;
            }
        }
    }

    private MonitorAgentProbe ProbePoint(Vector3 point, int mask)
    {
        var key = (point, mask);
        if (!_points.TryGetValue(key, out var work) || work.Expires <= _now)
        {
            if (_points.Count >= CacheLimit) _points.Clear();
            work = new PointWork { Expires = _now + 2f }; _points[key] = work;
        }
        if (work.Finished) return work.Result;
        if (work.Count < 0)
        {
            if (!Spend()) return Deferred;
            work.Count = _world.OverlapSphere(point, Radius, _overlaps, mask, QueryTriggerInteraction.Ignore);
            if (work.Count == _overlaps.Length) return FinishPoint(work, new MonitorAgentProbe(MonitorAgentProbeStatus.Blocked));
        }
        var colliders = work.Colliders ?? _overlaps;
        for (; work.Cursor < work.Count; work.Cursor++)
        {
            var collider = colliders[work.Cursor];
            if (collider == null) continue; // A scene collider can be destroyed between deferred frames.
            if (!Spend())
            {
                // Allocate a retained buffer only when a dense point really spans frames.
                if (work.Colliders == null)
                { work.Colliders = new Collider[work.Count]; Array.Copy(_overlaps, work.Colliders, work.Count); }
                return Deferred;
            }
            var shape = ProbeShape;
            if (!Physics.ComputePenetration(shape, point, Quaternion.identity, collider, collider.transform.position,
                collider.transform.rotation, out var normal, out float depth) || depth <= .00001f) continue;
            if (!SoftContact(collider, point - normal * Radius, normal))
                return FinishPoint(work, new MonitorAgentProbe(MonitorAgentProbeStatus.Blocked, 0, normal));
            work.Contact = true;
        }
        return FinishPoint(work, new MonitorAgentProbe(work.Contact ? MonitorAgentProbeStatus.Contact : MonitorAgentProbeStatus.Clear));
    }

    private MonitorAgentProbe Sweep(Vector3 from, Vector3 to, int mask, TravelWork work)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < .000001f) return new MonitorAgentProbe(MonitorAgentProbeStatus.Clear);
        if (!Spend()) return Deferred;
        int count = _world.SphereCast(from, Radius, delta.normalized, _hits, delta.magnitude, mask, QueryTriggerInteraction.Ignore);
        if (count == _hits.Length) return new MonitorAgentProbe(MonitorAgentProbeStatus.Blocked);
        bool contact = false;
        for (int i = 0; i < count; i++)
        {
            var hit = _hits[i];
            if (hit.distance <= .0001f) { work.Uncertain = true; continue; }
            if (!SoftContact(hit.collider, hit.point, hit.normal))
                return new MonitorAgentProbe(MonitorAgentProbeStatus.Blocked, hit.distance, hit.normal);
            contact = true;
            if (hit.collider is MeshCollider mesh && !mesh.convex) work.Uncertain = true;
        }
        return new MonitorAgentProbe(contact ? MonitorAgentProbeStatus.Contact : MonitorAgentProbeStatus.Clear);
    }

    private MonitorAgentProbe Ray(Vector3 from, Vector3 to, int mask)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < .000001f) return new MonitorAgentProbe(MonitorAgentProbeStatus.Clear);
        if (!Spend()) return Deferred;
        int count = _world.Raycast(from, delta.normalized, _hits, delta.magnitude, mask, QueryTriggerInteraction.Ignore);
        if (count == _hits.Length) return new MonitorAgentProbe(MonitorAgentProbeStatus.Blocked);
        bool contact = false;
        for (int i = 0; i < count; i++)
        {
            var hit = _hits[i];
            if (!SoftContact(hit.collider, hit.point, hit.normal))
                return new MonitorAgentProbe(MonitorAgentProbeStatus.Blocked, hit.distance, hit.normal);
            contact = true;
        }
        return new MonitorAgentProbe(contact ? MonitorAgentProbeStatus.Contact : MonitorAgentProbeStatus.Clear);
    }

    private bool SoftContact(Collider collider, Vector3 point, Vector3 normal)
    {
        if (Mathf.Abs(normal.y) > .55f) return false;
        int id = collider.GetInstanceID();
        if (!_doors.TryGetValue(id, out bool door))
        {
            if (_doors.Count >= CacheLimit) _doors.Clear();
            door = MonitorDoorGeometry.IsAuditedDoor(collider.transform); _doors[id] = door;
        }
        Vector3 size = collider.bounds.size;
        if (door && Mathf.Min(size.x, size.y, size.z) <= .6f) return true;
        for (int i = 0; i < _openings.Count; i++) if (_openings[i].Contains(point)) return true;
        return false;
    }

    private SphereCollider ProbeShape
    {
        get
        {
            if (_shape != null) return _shape;
            var go = new GameObject("EnhancedSpectator agent hull query") { hideFlags = HideFlags.HideAndDontSave };
            _shapeScene = SceneManager.CreateScene("EnhancedSpectator camera query agent " + go.GetInstanceID(),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            SceneManager.MoveGameObjectToScene(go, _shapeScene);
            _shape = go.AddComponent<SphereCollider>(); _shape.radius = Radius; _shape.isTrigger = true;
            return _shape;
        }
    }

    private bool Accept(TravelWork work, MonitorAgentProbe probe)
    {
        if (probe.Status == MonitorAgentProbeStatus.Deferred) return false;
        if (probe.Status == MonitorAgentProbeStatus.Blocked)
        { work.Result = probe; work.Finished = true; work.Expires = _now + .2f; return false; }
        work.Contact |= probe.Status == MonitorAgentProbeStatus.Contact; return true;
    }
    private MonitorAgentProbe Finish(TravelWork work)
    {
        work.Result = new MonitorAgentProbe(work.Contact ? MonitorAgentProbeStatus.Contact : MonitorAgentProbeStatus.Clear);
        work.Finished = true; work.Expires = _now + .2f; return work.Result;
    }
    private MonitorAgentProbe FinishPoint(PointWork work, MonitorAgentProbe result)
    { work.Result = result; work.Finished = true; work.Colliders = null; work.Expires = _now + .2f; return result; }
    private void TrimEdges() { if (_edges.Count >= CacheLimit) _edges.Clear(); }
    private static MonitorAgentProbe Deferred => new(MonitorAgentProbeStatus.Deferred);
    private sealed class PointWork
    {
        internal int Count = -1, Cursor;
        internal Collider[]? Colliders;
        internal bool Contact, Finished;
        internal float Expires;
        internal MonitorAgentProbe Result;
    }
    private sealed class TravelWork
    {
        internal readonly Vector3 From, To;
        internal readonly float Length;
        internal int Stage, Steps, Index, FineStage;
        internal bool Contact, Uncertain, Finished;
        internal float Expires;
        internal MonitorAgentProbe Result;
        internal TravelWork(Vector3 from, Vector3 to) { From = from; To = to; Length = (to - from).magnitude; }
    }
    private readonly struct Opening
    {
        private readonly Matrix4x4 _matrix;
        private readonly Vector2 _size;
        internal Opening(Matrix4x4 matrix, Vector2 size) { _matrix = matrix; _size = size; }
        internal bool Contains(Vector3 point)
        {
            Vector3 p = _matrix.MultiplyPoint3x4(point);
            return Mathf.Abs(p.x) <= _size.x * .5f + .25f && p.y >= .25f && p.y <= _size.y + .25f && Mathf.Abs(p.z) <= .45f;
        }
    }
}
