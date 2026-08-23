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
        out string reason)
    {
        visual = null;
        reason = string.Empty;
        if (source == null)
        {
            reason = "source root unavailable";
            return false;
        }

        if (!FearModeRules.IsValidModelKey(modelKey))
        {
            reason = "invalid model key";
            return false;
        }

        GameObject cleanRoot = new GameObject($"Enhanced Spectator Fear Visual {modelKey}");
        cleanRoot.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(cleanRoot);
        try
        {
            Transform hierarchyRoot = source.HierarchyRoot;
            Transform rendererRoot = source.RendererRoot;
            GameObject contentObject = new GameObject("Renderer Content");
            contentObject.transform.SetParent(cleanRoot.transform, worldPositionStays: false);
            Dictionary<Transform, Transform> transformMap = new Dictionary<Transform, Transform>();
            int transformCount = 0;
            CloneTransformHierarchy(
                hierarchyRoot,
                contentObject.transform,
                transformMap,
                ref transformCount,
                isSourceRoot: true,
                normalizeSourceRootPose: FearModelPresentationRules.ShouldNormalizeSourceRootPose(modelKey));
            ActivateRendererRootPath(rendererRoot, hierarchyRoot, transformMap);

            HashSet<Renderer> excludedLodRenderers = BuildExcludedLodRendererSet(rendererRoot);

            int rendererCount = 0;
            if (!source.SkinnedMeshOnly)
            {
                CopyMeshRenderers(
                    rendererRoot,
                    transformMap,
                    excludedLodRenderers,
                    visibleLayerMask,
                    source.ForceRendererVisibility,
                    ref rendererCount);
            }
            CopySkinnedMeshRenderers(
                rendererRoot,
                transformMap,
                excludedLodRenderers,
                visibleLayerMask,
                source.ForceRendererVisibility,
                ref rendererCount);
            if (rendererCount == 0)
            {
                reason = "source contains no supported mesh renderers";
                UnityEngine.Object.Destroy(cleanRoot);
                return false;
            }

            if (!ContainsOnlyAllowedComponents(cleanRoot, out string forbiddenType))
            {
                reason = $"clean hierarchy unexpectedly contains forbidden component {forbiddenType}";
                UnityEngine.Object.Destroy(cleanRoot);
                return false;
            }

            cleanRoot.SetActive(true);
            bool hasBounds = source.UseMeshBoundsForNormalization
                ? TryGetCombinedMeshBounds(cleanRoot, out Bounds visibleBounds)
                : TryGetCombinedVisibleBounds(cleanRoot, out visibleBounds);
            if (!hasBounds)
            {
                reason = "clean hierarchy contains no active visible renderer bounds";
                UnityEngine.Object.Destroy(cleanRoot);
                return false;
            }

            Vector3 localCenter = cleanRoot.transform.InverseTransformPoint(visibleBounds.center);
            contentObject.transform.localPosition += FearVisualNormalizationRules.ResolveContentOffset(localCenter);
            float uniformScale = FearVisualNormalizationRules.ResolveFinalScale(
                useOriginalScale,
                targetHeight,
                visibleBounds.size.y,
                scaleMultiplier);
            cleanRoot.transform.localScale = Vector3.one * uniformScale;
            cleanRoot.SetActive(false);
            bool runtimeRecenter = FearModelPresentationRules.ShouldRecenterFromRuntimeBounds(modelKey);
            reason = $"renderers={rendererCount}, sourceHeight={visibleBounds.size.y:0.###}, originalScale={useOriginalScale}, scale={uniformScale:0.###}, centeredFrom={localCenter}, runtimeRecenter={runtimeRecenter}";
            visual = new RuntimeEnemyVisual(modelKey, cleanRoot, contentObject.transform, runtimeRecenter);
            return true;
        }
        catch (Exception ex)
        {
            reason = $"renderer-only clone failed: {ex.GetType().Name}";
            UnityEngine.Object.Destroy(cleanRoot);
            return false;
        }
    }

    private static void CloneTransformHierarchy(
        Transform source,
        Transform cleanParent,
        Dictionary<Transform, Transform> transformMap,
        ref int transformCount,
        bool isSourceRoot,
        bool normalizeSourceRootPose)
    {
        transformCount++;
        if (transformCount > MaxTransformCount)
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
        clone.localScale = source.localScale;
        transformMap[source] = clone;

        for (int childIndex = 0; childIndex < source.childCount; childIndex++)
        {
            CloneTransformHierarchy(
                source.GetChild(childIndex),
                clone,
                transformMap,
                ref transformCount,
                isSourceRoot: false,
                normalizeSourceRootPose);
        }
    }

    private static void CopyMeshRenderers(
        Transform sourceRoot,
        Dictionary<Transform, Transform> transformMap,
        HashSet<Renderer> excludedLodRenderers,
        int visibleLayerMask,
        bool forceRendererVisibility,
        ref int rendererCount)
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
            rendererCount++;
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

    private static void CopySkinnedMeshRenderers(
        Transform sourceRoot,
        Dictionary<Transform, Transform> transformMap,
        HashSet<Renderer> excludedLodRenderers,
        int visibleLayerMask,
        bool forceRendererVisibility,
        ref int rendererCount)
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
            rendererCount++;
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

    private static bool TryGetCombinedMeshBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(includeInactive: false);
        for (int index = 0; index < meshFilters.Length; index++)
        {
            MeshFilter filter = meshFilters[index];
            if (filter.sharedMesh != null)
            {
                EncapsulateTransformedBounds(
                    filter.sharedMesh.bounds,
                    filter.transform.localToWorldMatrix,
                    ref bounds,
                    ref hasBounds);
            }
        }

        SkinnedMeshRenderer[] skinnedRenderers =
            root.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: false);
        for (int index = 0; index < skinnedRenderers.Length; index++)
        {
            SkinnedMeshRenderer renderer = skinnedRenderers[index];
            if (renderer.sharedMesh != null && renderer.enabled && !renderer.forceRenderingOff)
            {
                Mesh bakedMesh = new Mesh();
                Bounds skinnedBounds = renderer.sharedMesh.bounds;
                try
                {
                    renderer.BakeMesh(bakedMesh);
                    if (bakedMesh.vertexCount > 0)
                    {
                        skinnedBounds = bakedMesh.bounds;
                    }
                }
                catch (Exception)
                {
                    // Some imported meshes cannot be baked before their animator runs; retain the safe shared-mesh fallback.
                }
                finally
                {
                    UnityEngine.Object.Destroy(bakedMesh);
                }

                EncapsulateTransformedBounds(
                    skinnedBounds,
                    renderer.transform.localToWorldMatrix,
                    ref bounds,
                    ref hasBounds);
            }
        }

        return hasBounds && bounds.size.y > 0.001f;
    }

    private static void EncapsulateTransformedBounds(
        Bounds localBounds,
        Matrix4x4 localToWorld,
        ref Bounds combined,
        ref bool hasBounds)
    {
        Vector3 center = localBounds.center;
        Vector3 extents = localBounds.extents;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 worldCorner = localToWorld.MultiplyPoint3x4(corner);
                    if (!hasBounds)
                    {
                        combined = new Bounds(worldCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        combined.Encapsulate(worldCorner);
                    }
                }
            }
        }
    }

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
