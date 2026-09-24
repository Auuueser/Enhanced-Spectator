using System;
using System.Collections.Generic;
using EnhancedSpectator.GameInterop;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Builds clean renderer-only hierarchies from loaded enemy prefab data without instantiating the prefab.
/// </summary>
public sealed class RuntimeEnemyVisualFactory
{
    private const int MaxTransformCount = 384;
    /// <summary>
    /// Attempts to create a clean visual containing only transforms and renderer data.
    /// </summary>
    public bool TryCreate(
        FearVisualSource? source,
        string modelKey,
        float targetHeight,
        bool useOriginalScale,
        float scaleMultiplier,
        int visibleLayerMask,
        out RuntimeEnemyVisual? visual,
        out string reason,
        bool enableVisualMotion = false)
    {
        using var build = BeginCreate(source, modelKey, targetHeight, useOriginalScale, scaleMultiplier, visibleLayerMask, enableVisualMotion);
        while (!build.Work.Done) build.Work.Advance(double.PositiveInfinity, int.MaxValue);
        visual = build.Work.Failure == null ? build.Take() : null;
        reason = build.Work.Failure == null ? build.Reason : "renderer-only clone failed: " + build.Work.Failure.GetType().Name;
        return visual != null;
    }

    internal RuntimeVisualBuild BeginCreate(FearVisualSource? source, string modelKey, float targetHeight,
        bool useOriginalScale, float scaleMultiplier, int visibleLayerMask, bool enableVisualMotion = true) =>
        new RuntimeVisualBuild(build => CreateSteps(build, source, modelKey, targetHeight, useOriginalScale, scaleMultiplier, visibleLayerMask, enableVisualMotion));

    private static IEnumerator<bool> CreateSteps(RuntimeVisualBuild build, FearVisualSource? source, string modelKey,
        float targetHeight, bool useOriginalScale, float scaleMultiplier, int visibleLayerMask, bool enableVisualMotion)
    {
        build.Reason = string.Empty;
        if (source == null)
        {
            build.Reason = "source root unavailable";
            yield break;
        }

        if (!FearModeRules.IsValidModelKey(modelKey))
        {
            build.Reason = "invalid model key";
            yield break;
        }

        GameObject cleanRoot = new GameObject($"Enhanced Spectator Fear Visual {modelKey}");
        cleanRoot.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(cleanRoot);
        var ownedMeshes = new List<Mesh>();
        try
        {
            Transform hierarchyRoot = source.HierarchyRoot;
            Transform rendererRoot = source.RendererRoot;
            GameObject contentObject = new GameObject("Renderer Content");
            contentObject.transform.SetParent(cleanRoot.transform, worldPositionStays: false);
            Dictionary<Transform, Transform> transformMap = new Dictionary<Transform, Transform>();
            HashSet<Renderer> excludedLodRenderers = BuildExcludedLodRendererSet(rendererRoot);
            if (source.IncludedRenderers != null)
                foreach (var renderer in rendererRoot.GetComponentsInChildren<Renderer>(true))
                    if (!source.IncludedRenderers.Contains(renderer)) excludedLodRenderers.Add(renderer);
            // Tongue constraints also reference non-rendering rig objects: keep its complete hierarchy.
            HashSet<Transform>? allowedTransforms = modelKey == "item:SeveredTongue" ? null
                : CollectVisualTransforms(source, visibleLayerMask, excludedLodRenderers);
            yield return false;
            // Adult V81 has a 0.7223538 parent scale. BakeMesh(false) still folds
            // that ancestor scale into its output; restore it only after freezing,
            // otherwise both the baked vertices and their transform apply it.
            foreach (bool step in CloneTransformHierarchy(
                hierarchyRoot,
                contentObject.transform,
                transformMap,
                isSourceRoot: true,
                normalizeSourceRootPose: source.NormalizeRootPose || FearModelPresentationRules.ShouldNormalizeSourceRootPose(modelKey),
                allowedTransforms, normalizeSourceScale: modelKey == FearModelPresentationRules.ManeaterAdultModelKey)) yield return step;
            ActivateRendererRootPath(rendererRoot, hierarchyRoot, transformMap);

            if (!source.SkinnedMeshOnly)
            {
                foreach (bool step in CopyMeshRenderers(
                    rendererRoot,
                    transformMap,
                    excludedLodRenderers,
                    visibleLayerMask,
                    source.ForceRendererVisibility)) yield return step;
            }
            foreach (bool step in CopySkinnedMeshRenderers(
                rendererRoot,
                transformMap,
                excludedLodRenderers,
                visibleLayerMask,
                source.ForceRendererVisibility)) yield return step;
            int rendererCount = cleanRoot.GetComponentsInChildren<Renderer>(true).Length;
            if (rendererCount == 0)
            {
                build.Reason = "source contains no supported mesh renderers";
                yield break;
            }

            if (!ContainsOnlyAllowedComponents(cleanRoot, out string forbiddenType))
            {
                build.Reason = $"clean hierarchy unexpectedly contains forbidden component {forbiddenType}";
                yield break;
            }

            yield return false; // Bounds/baking runs in its own slice, never interleaved with rendering.
            cleanRoot.SetActive(true);
            bool freezePose = modelKey == FearModelIdentityRules.BushWolf || modelKey == FearModelPresentationRules.ManeaterAdultModelKey;
            if (freezePose)
            {
                // Freeze renderer-only poses whose imported skin/culling frames disagree. The
                // displayed mesh and fade envelope now use the very same baked vertices.
                foreach (var skin in cleanRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled || skin.forceRenderingOff || skin.sharedMesh == null) continue;
                    var mesh = new Mesh { name = "Enhanced Spectator frozen pose " + modelKey };
                    ownedMeshes.Add(mesh);
                    skin.BakeMesh(mesh, false);
                    mesh.RecalculateBounds();
                    skin.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = skin.gameObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = skin.sharedMaterials;
                    renderer.shadowCastingMode = skin.shadowCastingMode;
                    renderer.receiveShadows = skin.receiveShadows;
                    skin.enabled = false;
                }
            }
            if (modelKey == FearModelPresentationRules.ManeaterAdultModelKey)
                transformMap[hierarchyRoot].localScale = hierarchyRoot.localScale;
            bool hasBounds = source.UseMeshBoundsForNormalization
                ? TryGetCombinedMeshBounds(cleanRoot, out Bounds visibleBounds)
                : TryGetCombinedVisibleBounds(cleanRoot, out visibleBounds);
            if (!hasBounds)
            {
                build.Reason = "clean hierarchy contains no active visible renderer bounds";
                yield break;
            }

            Vector3 localCenter = cleanRoot.transform.InverseTransformPoint(visibleBounds.center);
            contentObject.transform.localPosition += FearVisualNormalizationRules.ResolveContentOffset(localCenter);
            float uniformScale = FearVisualNormalizationRules.ResolveFinalScale(
                useOriginalScale,
                targetHeight,
                visibleBounds.size.y,
                scaleMultiplier);
            if (source.Miniature) uniformScale = FearModelIdentityRules.MiniatureScale(visibleBounds.size.x, visibleBounds.size.y, visibleBounds.size.z, scaleMultiplier);
            cleanRoot.transform.localScale = Vector3.one * uniformScale;
            cleanRoot.SetActive(false);
            bool runtimeRecenter = !freezePose && FearModelPresentationRules.ShouldRecenterFromRuntimeBounds(modelKey);
            build.Reason = $"renderers={rendererCount}, transforms={transformMap.Count}, frozenPose={freezePose}, sourceHeight={visibleBounds.size.y:0.###}, originalScale={useOriginalScale}, scale={uniformScale:0.###}, centeredFrom={localCenter}, runtimeRecenter={runtimeRecenter}";
            var completed = new RuntimeEnemyVisual(modelKey, cleanRoot, contentObject.transform, runtimeRecenter, ownedMeshes.ToArray(), new Bounds(Vector3.zero, visibleBounds.size));
            if (enableVisualMotion && modelKey == "item:SeveredTongue")
                completed.TongueMotion = OriginalTongueVisualMotion.Create(hierarchyRoot, transformMap);
            build.Visual = completed;
            yield break;
        }
        finally
        {
            if (build.Visual == null)
            {
                foreach (var mesh in ownedMeshes) UnityEngine.Object.Destroy(mesh);
                UnityEngine.Object.Destroy(cleanRoot);
            }
        }
    }

    private static HashSet<Transform> CollectVisualTransforms(FearVisualSource source, int mask, HashSet<Renderer> excluded)
    {
        var result = new HashSet<Transform> { source.HierarchyRoot };
        void Include(Transform current)
        {
            while (current != null && current != source.HierarchyRoot)
            {
                if (!result.Add(current)) break;
                current = current.parent;
            }
        }
        Include(source.RendererRoot);
        foreach (var renderer in source.RendererRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (excluded.Contains(renderer) || !IsLayerVisible(renderer.gameObject.layer, mask)
                || (!source.ForceRendererVisibility && (!renderer.enabled || renderer.forceRenderingOff
                    || !IsActiveWithinSourceRoot(renderer.transform, source.RendererRoot)))) continue;
            if (renderer is SkinnedMeshRenderer skin)
            {
                if (skin.sharedMesh == null) continue;
                Include(skin.transform);
                if (skin.rootBone != null) Include(skin.rootBone);
                foreach (var bone in skin.bones) if (bone != null) Include(bone);
            }
            else if (!source.SkinnedMeshOnly && renderer is MeshRenderer)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) Include(renderer.transform);
            }
        }
        return result;
    }

    private static IEnumerable<bool> CloneTransformHierarchy(
        Transform source,
        Transform cleanParent,
        Dictionary<Transform, Transform> transformMap,
        bool isSourceRoot,
        bool normalizeSourceRootPose,
        HashSet<Transform>? allowedTransforms, bool normalizeSourceScale = false)
    {
        if (allowedTransforms != null && !allowedTransforms.Contains(source)) yield break;
        if (transformMap.Count >= MaxTransformCount)
        {
            throw new InvalidOperationException("enemy visual hierarchy exceeds transform safety cap");
        }

        GameObject cloneObject = new GameObject(source.name);
        cloneObject.layer = source.gameObject.layer;
        cloneObject.SetActive(isSourceRoot || source.gameObject.activeSelf);
        Transform clone = cloneObject.transform;
        clone.SetParent(cleanParent, worldPositionStays: false);
        bool normalizePose = isSourceRoot && normalizeSourceRootPose;
        clone.localPosition = normalizePose ? Vector3.zero : source.localPosition;
        clone.localRotation = normalizePose ? Quaternion.identity : source.localRotation;
        clone.localScale = isSourceRoot && normalizeSourceScale ? Vector3.one : source.localScale;
        transformMap[source] = clone;
        yield return true;

        for (int childIndex = 0; childIndex < source.childCount; childIndex++)
        {
            foreach (bool step in CloneTransformHierarchy(
                source.GetChild(childIndex),
                clone,
                transformMap,
                isSourceRoot: false,
                normalizeSourceRootPose, allowedTransforms)) yield return step;
        }
    }

    private static IEnumerable<bool> CopyMeshRenderers(
        Transform sourceRoot,
        Dictionary<Transform, Transform> transformMap,
        HashSet<Renderer> excludedLodRenderers,
        int visibleLayerMask,
        bool forceRendererVisibility)
    {
        MeshFilter[] sourceFilters = sourceRoot.GetComponentsInChildren<MeshFilter>(includeInactive: true);
        for (int index = 0; index < sourceFilters.Length; index++)
        {
            MeshFilter sourceFilter = sourceFilters[index];
            MeshRenderer sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();
            Mesh sourceMesh = sourceFilter.sharedMesh;
            if (sourceRenderer == null
                || sourceMesh == null
                || (!forceRendererVisibility && !sourceRenderer.enabled)
                || (!forceRendererVisibility && sourceRenderer.forceRenderingOff)
                || excludedLodRenderers.Contains(sourceRenderer)
                || !IsLayerVisible(sourceRenderer.gameObject.layer, visibleLayerMask)
                || (!forceRendererVisibility && !IsActiveWithinSourceRoot(sourceFilter.transform, sourceRoot))
                || !transformMap.TryGetValue(sourceFilter.transform, out Transform cleanTransform))
            {
                continue;
            }

            MeshFilter cleanFilter = cleanTransform.gameObject.AddComponent<MeshFilter>();
            MeshRenderer cleanRenderer = cleanTransform.gameObject.AddComponent<MeshRenderer>();
            if (forceRendererVisibility)
            {
                ActivateClonePath(sourceFilter.transform, sourceRoot, transformMap);
            }

            cleanFilter.sharedMesh = sourceMesh;
            cleanRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            ConfigureRenderer(cleanRenderer, sourceRenderer, forceRendererVisibility);
            yield return true;
        }
    }

    private static void ActivateRendererRootPath(
        Transform rendererRoot,
        Transform hierarchyRoot,
        Dictionary<Transform, Transform> transformMap)
    {
        Transform? current = rendererRoot;
        while (current != null && transformMap.TryGetValue(current, out Transform clone))
        {
            clone.gameObject.SetActive(true);
            if (current == hierarchyRoot)
            {
                return;
            }

            current = current.parent;
        }

        throw new InvalidOperationException("renderer root is outside the cloned hierarchy");
    }

    private static void ActivateClonePath(
        Transform sourceTransform,
        Transform sourceRoot,
        Dictionary<Transform, Transform> transformMap)
    {
        Transform? current = sourceTransform;
        while (current != null && transformMap.TryGetValue(current, out Transform clone))
        {
            clone.gameObject.SetActive(true);
            if (current == sourceRoot)
            {
                return;
            }

            current = current.parent;
        }
    }

    private static IEnumerable<bool> CopySkinnedMeshRenderers(
        Transform sourceRoot,
        Dictionary<Transform, Transform> transformMap,
        HashSet<Renderer> excludedLodRenderers,
        int visibleLayerMask,
        bool forceRendererVisibility)
    {
        SkinnedMeshRenderer[] sourceRenderers =
            sourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
        for (int index = 0; index < sourceRenderers.Length; index++)
        {
            SkinnedMeshRenderer sourceRenderer = sourceRenderers[index];
            Mesh sourceMesh = sourceRenderer.sharedMesh;
            if (sourceMesh == null
                || (!forceRendererVisibility && !sourceRenderer.enabled)
                || (!forceRendererVisibility && sourceRenderer.forceRenderingOff)
                || excludedLodRenderers.Contains(sourceRenderer)
                || !IsLayerVisible(sourceRenderer.gameObject.layer, visibleLayerMask)
                || (!forceRendererVisibility && !IsActiveWithinSourceRoot(sourceRenderer.transform, sourceRoot))
                || !transformMap.TryGetValue(sourceRenderer.transform, out Transform cleanTransform))
            {
                continue;
            }

            Transform[] sourceBones = sourceRenderer.bones;
            Transform[] cleanBones = new Transform[sourceBones.Length];
            bool bonesComplete = true;
            for (int boneIndex = 0; boneIndex < sourceBones.Length; boneIndex++)
            {
                Transform sourceBone = sourceBones[boneIndex];
                if (sourceBone == null || !transformMap.TryGetValue(sourceBone, out cleanBones[boneIndex]))
                {
                    bonesComplete = false;
                    break;
                }
            }

            if (!bonesComplete)
            {
                continue;
            }

            SkinnedMeshRenderer cleanRenderer = cleanTransform.gameObject.AddComponent<SkinnedMeshRenderer>();
            if (forceRendererVisibility)
            {
                ActivateClonePath(sourceRenderer.transform, sourceRoot, transformMap);
            }

            cleanRenderer.sharedMesh = sourceMesh;
            cleanRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            cleanRenderer.bones = cleanBones;
            if (sourceRenderer.rootBone != null
                && transformMap.TryGetValue(sourceRenderer.rootBone, out Transform cleanRootBone))
            {
                cleanRenderer.rootBone = cleanRootBone;
            }

            cleanRenderer.localBounds = sourceRenderer.localBounds;
            cleanRenderer.updateWhenOffscreen = false;
            ConfigureRenderer(cleanRenderer, sourceRenderer, forceRendererVisibility);
            yield return true;
        }
    }

    private static void ConfigureRenderer(
        Renderer renderer,
        Renderer sourceRenderer,
        bool forceRendererVisibility)
    {
        renderer.enabled = forceRendererVisibility || sourceRenderer.enabled;
        renderer.forceRenderingOff = !forceRendererVisibility && sourceRenderer.forceRenderingOff;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.allowOcclusionWhenDynamic = false;
    }

    private static HashSet<Renderer> BuildExcludedLodRendererSet(Transform sourceRoot)
    {
        HashSet<Renderer> excluded = new HashSet<Renderer>();
        LODGroup[] lodGroups = sourceRoot.GetComponentsInChildren<LODGroup>(includeInactive: true);
        for (int groupIndex = 0; groupIndex < lodGroups.Length; groupIndex++)
        {
            LOD[] lods = lodGroups[groupIndex].GetLODs();
            for (int lodIndex = 1; lodIndex < lods.Length; lodIndex++)
            {
                Renderer[] renderers = lods[lodIndex].renderers;
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    if (renderers[rendererIndex] != null)
                    {
                        excluded.Add(renderers[rendererIndex]);
                    }
                }
            }
        }

        return excluded;
    }

    private static bool IsActiveWithinSourceRoot(Transform transform, Transform sourceRoot)
    {
        Transform? current = transform;
        while (current != null)
        {
            if (current == sourceRoot)
            {
                return true;
            }

            if (!current.gameObject.activeSelf)
            {
                return false;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool IsLayerVisible(int layer, int visibleLayerMask)
    {
        return layer >= 0
            && layer <= 31
            && (visibleLayerMask & (1 << layer)) != 0;
    }

    private static bool TryGetCombinedVisibleBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: false);
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null || !renderer.enabled || renderer.forceRenderingOff)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds && bounds.size.y > 0.001f;
    }

    private static bool TryGetCombinedMeshBounds(GameObject root, out Bounds bounds) =>
        VisualPoseBounds.TryCapture(root.GetComponentsInChildren<Renderer>(false), Matrix4x4.identity, out bounds);

    private static bool ContainsOnlyAllowedComponents(GameObject root, out string forbiddenType)
    {
        Component[] components = root.GetComponentsInChildren<Component>(includeInactive: true);
        for (int index = 0; index < components.Length; index++)
        {
            Component component = components[index];
            if (component is Transform
                || component is MeshFilter
                || component is MeshRenderer
                || component is SkinnedMeshRenderer)
            {
                continue;
            }

            forbiddenType = component != null ? component.GetType().FullName ?? component.GetType().Name : "null";
            return false;
        }

        forbiddenType = string.Empty;
        return true;
    }
}
