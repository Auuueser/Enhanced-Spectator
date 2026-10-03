using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EnhancedSpectator.Features.FearMode;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

public sealed partial class LethalCompanyFearModeAdapter : IGameFearCatalogPreparationAdapter
{
    private readonly FearCatalogDiscoverySchedule _discovery = new FearCatalogDiscoverySchedule();
    private readonly Dictionary<int, bool> _supportedPrefabs = new Dictionary<int, bool>();
    private bool _dropshipRequested;
    private int _preparationFrame = -1;
    private readonly Dictionary<string, FearVisualSource> _expandedSources = new Dictionary<string, FearVisualSource>(StringComparer.Ordinal);
    private readonly Dictionary<string, Item> _originalItems = new Dictionary<string, Item>(StringComparer.Ordinal);
    private ItemDropship? _dropship;
    private RuntimeEnemyVisual? _dropshipSnapshot;
    private RuntimeVisualBuild? _dropshipSnapshotBuild;
    private float _nextSnapshotRetry;
    private readonly OriginalDropshipVisualSource _originalDropship = new OriginalDropshipVisualSource();
    private readonly OriginalDropshipSoundSource _originalDropshipSounds = new OriginalDropshipSoundSource();
    private FearVisualSource? _dropshipSource;
    private readonly List<AudioClip> _dropshipClips = new List<AudioClip>();
    private readonly OriginalItemSoundCatalog _itemSounds = new OriginalItemSoundCatalog();

    void IGameFearCatalogPreparationAdapter.InvalidateCatalogSources() => _discovery.Invalidate();
    void IGameFearCatalogPreparationAdapter.TickCatalogPreparation()
    {
        if (!_dropshipRequested || _preparationFrame == Time.frameCount) return;
        _preparationFrame = Time.frameCount;
        var original = _originalDropship.Poll();
        if (original != null && original.HierarchyRoot != null)
        {
            _dropshipSource = original;
            _expandedSources[FearModelIdentityRules.Dropship] = original;
            return;
        }
        // Preserve the persistent live-source fallback if V81 resource validation fails.
        if (!_originalDropship.Finished || _dropshipSnapshot != null) return;
        if (_dropshipSnapshotBuild == null && _dropshipSource?.HierarchyRoot != null && Time.unscaledTime >= _nextSnapshotRetry)
        {
            _nextSnapshotRetry = Time.unscaledTime + 5f;
            _dropshipSnapshotBuild = new RuntimeEnemyVisualFactory().BeginCreate(
                _dropshipSource, FearModelIdentityRules.Dropship, 2f, true, 1f, ~0, false);
        }
        if (_dropshipSnapshotBuild == null) return;
        _dropshipSnapshotBuild.Work.Advance();
        if (!_dropshipSnapshotBuild.Work.Done) return;
        _dropshipSnapshot = _dropshipSnapshotBuild.Take();
        _dropshipSnapshotBuild.Dispose(); _dropshipSnapshotBuild = null;
        if (_dropshipSnapshot == null) return;
        _dropshipSource = _dropshipSnapshot.SnapshotSource();
        _expandedSources[FearModelIdentityRules.Dropship] = _dropshipSource;
    }
    private static readonly string[] ShipVisualBranches =
    {
        "ShipHull", "ShipInside", "ShipInside.001", "ShipModels2b", "ShipRailPosts", "ShipRails",
        "ShipSupportBeams", "ShipSupportBeams.001", "SideMachineryLeft", "SideMachineryRight",
        "ThrusterBackLeft", "ThrusterBackRight", "ThrusterFrontLeft", "ThrusterFrontRight",
        "UnderbellyMachineParts", "HangarDoorLeft", "HangarDoorRight", "CatwalkShip",
        "CatwalkRailLining", "CatwalkRailLiningB", "CatwalkUnderneathSupports", "SmallDetails"
    };

    // Called only by the bounded catalog refresh, never from a render tick.
    private void RefreshExpandedSources(List<string> destination)
    {
        var round = StartOfRound.Instance;
        if (round == null)
        {
            _discovery.Invalidate(); _expandedSources.Clear(); _originalItems.Clear(); return;
        }
        int roundId = round.GetInstanceID();
        int definitions = round.allItemsList != null ? RuntimeHelpers.GetHashCode(round.allItemsList) : 0;
        int count = round.allItemsList?.itemsList?.Count ?? 0;
        foreach (var source in _expandedSources.Values)
            if (source.HierarchyRoot == null) { _discovery.Invalidate(); break; }
        if (!_discovery.NeedsRefresh(roundId, definitions, count, Time.unscaledTime))
        {
            CopyExpandedKeys(destination);
            return;
        }
        _expandedSources.Clear(); _originalItems.Clear(); _supportedPrefabs.Clear();
        if (round.allItemsList != null)
            foreach (var item in round.allItemsList.itemsList) AddOriginalItem(item);
        // Includes original scene-only equipment and already-loaded original definitions omitted from save lists.
        foreach (var item in Resources.FindObjectsOfTypeAll<Item>()) AddOriginalItem(item);
        foreach (var prop in UnityEngine.Object.FindObjectsOfType<GrabbableObject>())
        {
            var item = prop.itemProperties;
            if (item == null || item.spawnPrefab != null || !IsOriginalItem(item)) continue;
            string key = "item:" + item.name;
            if (HasSupportedRenderer(prop.transform))
            {
                _originalItems[key] = item;
                _expandedSources[key] = new FearVisualSource(prop.transform, prop.transform, normalizeRootPose: true);
            }
        }

        Transform ship = round.elevatorTransform;
        if (ship != null)
        {
            var renderers = new HashSet<Renderer>();
            foreach (string path in ShipVisualBranches)
            {
                Transform branch = ship.Find(path);
                if (branch == null) continue;
                foreach (var renderer in branch.GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.GetComponentInParent<GrabbableObject>() == null
                        && renderer.GetComponentInParent<GameNetcodeStuff.PlayerControllerB>() == null) renderers.Add(renderer);
            }
            if (renderers.Count > 0)
                _expandedSources[FearModelIdentityRules.Ship] = new FearVisualSource(ship, ship,
                    normalizeRootPose: true, includedRenderers: renderers, miniature: true);
        }
        RefreshDropshipSource();
        if (TryGetBushWolf(out var wolf) && wolf != null)
            _expandedSources[FearModelIdentityRules.BushWolf] = new FearVisualSource(wolf.enemyPrefab.transform, wolf.enemyPrefab.transform,
                normalizeRootPose: true, useMeshBoundsForNormalization: true, skinnedMeshOnly: true);
        _discovery.Refreshed(roundId, definitions, count, Time.unscaledTime);
        CopyExpandedKeys(destination);
    }

    private void CopyExpandedKeys(List<string> destination)
    {
        // RoundManager may publish this original enemy after the scene-loaded event.
        // Check its small direct list without repeating global object discovery.
        if (TryGetBushWolf(out var wolf) && wolf != null)
            _expandedSources[FearModelIdentityRules.BushWolf] = new FearVisualSource(wolf.enemyPrefab.transform, wolf.enemyPrefab.transform,
                normalizeRootPose: true, useMeshBoundsForNormalization: true, skinnedMeshOnly: true);
        foreach (var pair in _expandedSources) destination.Add(pair.Key);
        // Keep the card discoverable during the one-time asynchronous original resource read.
        if (!destination.Contains(FearModelIdentityRules.Dropship)) destination.Add(FearModelIdentityRules.Dropship);
    }

    private void RefreshDropshipSource()
    {
        if (_dropshipSource != null && _dropshipSource.HierarchyRoot != null)
            _expandedSources[FearModelIdentityRules.Dropship] = _dropshipSource;
        _dropship = null;
        // Includes inactive scene sources; selecting from loaded objects never creates delivery logic.
        foreach (var candidate in Resources.FindObjectsOfTypeAll<ItemDropship>())
        {
            if (candidate == null || candidate.shipAnimator == null) continue;
            if (_dropship == null || candidate.gameObject.scene.IsValid()) _dropship = candidate;
            if (candidate.gameObject.scene.IsValid()) break;
        }
        if (_dropship == null)
        {
            return;
        }
        _dropshipClips.Clear();
        ReadDropshipClips(_dropshipClips);
        Transform hierarchy = _dropship.shipAnimator.transform;
        // V81: ItemShipAnimContainer / ItemShip (script) / ItemShip (mesh) / Door1..8.
        // Find the confirmed mesh within the animator hierarchy, not a fixed one-level path.
        foreach (var hullRenderer in hierarchy.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (hullRenderer.name != "ItemShip" || hullRenderer.GetComponent<MeshFilter>()?.sharedMesh == null) continue;
            Transform hull = hullRenderer.transform;
            var renderers = new HashSet<Renderer> { hullRenderer };
            for (int index = 1; index <= 8; index++)
            {
                var door = hull.Find("Door" + index)?.GetComponent<MeshRenderer>();
                if (door != null) renderers.Add(door);
            }
            var liveSource = new FearVisualSource(hull, hull,
                normalizeRootPose: true, forceRendererVisibility: true, includedRenderers: renderers, miniature: true);
            if (_dropshipSource == null || _dropshipSource.HierarchyRoot == null)
            {
                // Discovery is read-only. Persistent original geometry is prepared on demand.
                _dropshipSource = liveSource;
            }
            _expandedSources[FearModelIdentityRules.Dropship] = _dropshipSource ?? liveSource;
            return;
        }
        EnhancedSpectator.Logging.ModLog.Debug("Mini dropship unavailable: animator hierarchy has no confirmed ItemShip hull mesh.");
    }

    private static bool IsOriginalItem(Item item) => item != null
        && OriginalItemCatalog.Items.TryGetValue(item.name, out var identity)
        && identity.Matches(item.itemId, item.spawnPrefab != null ? item.spawnPrefab.name : null);

    private void AddOriginalItem(Item item)
    {
        if (!IsOriginalItem(item) || item.spawnPrefab == null) return;
        int prefab = item.spawnPrefab.GetInstanceID();
        if (!_supportedPrefabs.TryGetValue(prefab, out bool supported))
        {
            supported = HasSupportedRenderer(item.spawnPrefab.transform);
            _supportedPrefabs[prefab] = supported;
        }
        if (!supported) return;
        string key = "item:" + item.name;
        _originalItems[key] = item;
        _expandedSources[key] = new FearVisualSource(item.spawnPrefab.transform, item.spawnPrefab.transform, normalizeRootPose: true);
    }

    private static bool HasSupportedRenderer(Transform root)
    {
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null && filter.GetComponent<MeshRenderer>() != null) return true;
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (renderer.sharedMesh != null) return true;
        return false;
    }

    private static bool TryGetBushWolf(out EnemyType? wolf)
    {
        wolf = null;
        var manager = RoundManager.Instance;
        if (manager == null || manager.WeedEnemies == null) return false;
        foreach (var entry in manager.WeedEnemies)
        {
            var type = entry?.enemyType;
            if (type != null && type.enemyPrefab != null && type.enemyPrefab.GetComponent<BushWolfEnemy>() != null)
            { wolf = type; return true; }
        }
        return false;
    }

    private bool CopyExpandedClips(string key, List<AudioClip> clips)
    {
        if (_originalItems.TryGetValue(key, out var item) && item != null)
        {
            _itemSounds.Copy(item, clips);
            return true;
        }
        var round = StartOfRound.Instance;
        if (key == FearModelIdentityRules.Ship && round != null)
        {
            AddClip(clips, round.shipArriveSFX); AddClip(clips, round.shipDepartSFX);
            AddClip(clips, round.openingHangarDoorAudio); AddClips(clips, round.shipCreakSFX);
            return true;
        }
        if (key == FearModelIdentityRules.Dropship)
        {
            var prepared = _originalDropshipSounds.Poll();
            if (prepared.Count > 0) foreach (var clip in prepared) AddClip(clips, clip);
            else foreach (var clip in _dropshipClips) AddClip(clips, clip);
            return true;
        }
        return key.StartsWith("item:", StringComparison.Ordinal);
    }

    private void ReadDropshipClips(List<AudioClip> clips)
    {
        if (_dropship == null) return;
        AddClip(clips, _dropship.transform.Find("Music")?.GetComponent<AudioSource>()?.clip);
        var audioEvent = _dropship.GetComponent<PlayAudioAnimationEvent>();
        if (audioEvent == null) return;
        AddClip(clips, audioEvent.audioClip); AddClip(clips, audioEvent.audioClip2);
        AddClip(clips, audioEvent.audioClip3); AddClips(clips, audioEvent.randomClips);
    }
}
