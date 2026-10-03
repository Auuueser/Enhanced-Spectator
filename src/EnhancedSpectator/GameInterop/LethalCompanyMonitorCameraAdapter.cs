using DunGen;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

public sealed partial class LethalCompanySpectatorAdapter : IGameMonitorCameraAdapter, IGameMonitorFramingAdapter
{
    MonitorFraming IGameMonitorFramingAdapter.EvaluateMonitorFraming(Vector3 position, Quaternion rotation)
    {
        var target=GetLocalPlayer()?.spectatedPlayerScript;
        var camera=StartOfRound.Instance?.spectateCamera;
        if(target==null || camera==null) return default;
        // Body samples, not cosmetic renderer bounds. No per-frame hierarchy scan or camera mutation.
        Vector3 feet=target.transform.position, right=target.transform.right*.28f;
        float height=target.isCrouching ? 1.2f : 1.8f;
        if(target.thisController!=null)
        { var body=target.thisController.bounds; feet=body.center-Vector3.up*body.extents.y; height=Mathf.Max(.6f,body.size.y); }
        return MonitorFrameGeometry.Evaluate(feet,right,height,position,rotation,camera.fieldOfView,camera.aspect,MonitorWorldMask);
    }
    bool IGameMonitorCameraAdapter.IsMonitorTargetIndoors => GetLocalPlayer()?.spectatedPlayerScript is { } target
        && !target.isPlayerDead && target.isPlayerControlled && target.isInsideFactory;
    private Dungeon? _monitorDungeon;
    private Tile? _monitorTile;
    private float _nextMonitorRoomProbe;
    private int _monitorTileCount;
    private Vector3 _monitorProbePosition;
    private string _monitorPlacementReason="not-requested";

    void IGameMonitorCameraAdapter.ClearMonitorRoom()
    { ClearMonitorRoutes(); _monitorDungeon = null; _monitorTile = null; _nextMonitorRoomProbe = 0; _monitorTileCount = 0; }

    bool IGameMonitorCameraAdapter.TryGetMonitorRoom(out MonitorRoom room)
    {
        room = default;
        var target = GetLocalPlayer()?.spectatedPlayerScript;
        var runtime = RoundManager.Instance != null ? RoundManager.Instance.dungeonGenerator : null;
        var dungeon = runtime != null ? runtime.Generator.CurrentDungeon : null;
        if (target == null || target.isPlayerDead || !target.isPlayerControlled || !target.isInsideFactory
            || dungeon == null || dungeon.AllTiles.Count == 0)
        {
            ((IGameMonitorCameraAdapter)this).ClearMonitorRoom();
            return false;
        }
        Vector3 feet = target.transform.position;
        if (dungeon != _monitorDungeon || _monitorTileCount != dungeon.AllTiles.Count)
        {
            _monitorDungeon = dungeon; _monitorTileCount = dungeon.AllTiles.Count;
            _monitorTile = null; _nextMonitorRoomProbe = 0;
        }
        if (Time.unscaledTime >= _nextMonitorRoomProbe || (feet - _monitorProbePosition).sqrMagnitude > 9f)
        {
            _nextMonitorRoomProbe = Time.unscaledTime + .2f; _monitorProbePosition = feet;
            Tile? found = SpectatorInteriorVisibility.SpecialEntrance(dungeon,feet);
            // Floor ownership distinguishes stacked rooms and concave tiles with overlapping bounds.
            if (found==null && Physics.Raycast(feet + Vector3.up * .4f, Vector3.down, out var floor, 3f,
                target.walkableSurfacesNoPlayersMask, QueryTriggerInteraction.Ignore))
            {
                var tile = floor.collider.GetComponentInParent<Tile>();
                if (ValidMonitorTile(tile, dungeon, feet)) found = tile;
            }
            if (found == null && ValidMonitorTile(_monitorTile, dungeon, feet)) found = _monitorTile;
            if (found == null)
            {
                float smallest = float.PositiveInfinity;
                foreach (var tile in dungeon.AllTiles)
                {
                    if (!ValidMonitorTile(tile, dungeon, feet)) continue;
                    Vector3 size = tile.Placement.LocalBounds.size;
                    float volume = size.x * size.y * size.z;
                    if (volume < smallest) { smallest = volume; found = tile; }
                }
            }
            if (found == null && Physics.Raycast(feet + Vector3.up * .4f, Vector3.down, out var lowerFloor,
                12f, target.walkableSurfacesNoPlayersMask, QueryTriggerInteraction.Ignore))
            {
                var lowerTile = lowerFloor.collider.GetComponentInParent<Tile>();
                if (ValidMonitorTile(lowerTile, dungeon, feet, 10f)) found = lowerTile;
            }
            _monitorTile = found;
        }
        if (_monitorTile == null || !_monitorTile.HasValidBounds || !_monitorTile.gameObject.activeInHierarchy) return false;
        var transform = _monitorTile.transform;
        room = new MonitorRoom(_monitorTile.GetInstanceID(), _monitorTile.Placement.LocalBounds,
            transform.localToWorldMatrix, transform.worldToLocalMatrix);
        return true;
    }

    private static bool ValidMonitorTile(Tile? tile, Dungeon dungeon, Vector3 feet, float upwardSlack = 0)
    {
        if (tile == null || tile.Dungeon != dungeon || !tile.HasValidBounds || !tile.gameObject.activeInHierarchy) return false;
        Bounds bounds = tile.Placement.LocalBounds;
        bounds.Expand(.3f);
        bounds.max += Vector3.up * upwardSlack;
        return bounds.Contains(tile.transform.InverseTransformPoint(feet + Vector3.up * .5f));
    }

    private static int MonitorWorldMask => GetLocalPlayer() is { } local
        ? local.walkableSurfacesNoPlayersMask : Physics.DefaultRaycastLayers;
    bool IGameMonitorCameraAdapter.TryPlaceMonitorCamera(Vector3 focus, Vector3 candidate, out Vector3 position) =>
        MonitorCameraGeometry.TryPlace(focus, candidate, MonitorWorldMask, out position,out _monitorPlacementReason);
    bool IGameMonitorCameraAdapter.IsMonitorSightClear(Vector3 position, Vector3 focus) =>
        MonitorCameraGeometry.SightClear(position, focus, MonitorWorldMask);
    bool IGameMonitorCameraAdapter.IsMonitorPathClear(Vector3 from, Vector3 to) =>
        MonitorCameraGeometry.PathClear(from, to, MonitorWorldMask);
}
