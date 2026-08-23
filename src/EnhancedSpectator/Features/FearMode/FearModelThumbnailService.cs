using System;
using System.Collections.Generic;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Renders loaded game monster data into local card thumbnails through the existing renderer-only clone boundary.
/// </summary>
public sealed class FearModelThumbnailService : IFearModelThumbnailProvider
{
    private const int ThumbnailLayer = 31;
    private const int ThumbnailSize = 256;
    private readonly FearModelCatalog _catalog;
    private readonly IGameFearModeAdapter _gameAdapter;
    private readonly RuntimeEnemyVisualFactory _visualFactory;
    private readonly IGameDetachedHeadVisualSourceAdapter _detachedHeadSource;
    private readonly Dictionary<string, Sprite> _sprites =
        new Dictionary<string, Sprite>(StringComparer.Ordinal);
    private readonly HashSet<string> _queued =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly Queue<string> _queue = new Queue<string>();
    private GameObject? _cameraObject;
    private Camera? _camera;
    private HDAdditionalCameraData? _hdCameraData;
    private Sprite? _fallbackSprite;
    private Texture2D? _fallbackTexture;
    private bool _disposed;

    /// <summary>Creates a thumbnail renderer for the loaded fear-model catalog.</summary>
    public FearModelThumbnailService(
        FearModelCatalog catalog,
        IGameFearModeAdapter gameAdapter,
        RuntimeEnemyVisualFactory visualFactory,
        IGameDetachedHeadVisualSourceAdapter detachedHeadSource)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _gameAdapter = gameAdapter ?? throw new ArgumentNullException(nameof(gameAdapter));
        _visualFactory = visualFactory ?? throw new ArgumentNullException(nameof(visualFactory));
        _detachedHeadSource = detachedHeadSource ?? throw new ArgumentNullException(nameof(detachedHeadSource));
        CreateFallback();
    }

    /// <inheritdoc />
    public int Revision { get; private set; }

    /// <inheritdoc />
    public void Request(string modelKey)
    {
        if (_disposed
            || string.IsNullOrWhiteSpace(modelKey)
            || _sprites.ContainsKey(modelKey)
            || !_queued.Add(modelKey))
        {
            return;
        }

        _queue.Enqueue(modelKey);
    }

    /// <inheritdoc />
    public bool TryGet(string modelKey, out Sprite? sprite)
    {
        if (!string.IsNullOrWhiteSpace(modelKey)
            && _sprites.TryGetValue(modelKey, out Sprite value)
            && value != null)
        {
            sprite = value;
            return true;
        }

        sprite = _fallbackSprite;
        return sprite != null;
    }

    /// <inheritdoc />
    public bool TryGetEntryIcon(out Sprite? sprite)
    {
        sprite = _fallbackSprite;
        return sprite != null;
    }

    /// <inheritdoc />
    public void Tick()
    {
        if (_disposed || _queue.Count == 0)
        {
            return;
        }

        string modelKey = _queue.Dequeue();
        _queued.Remove(modelKey);
        TryRender(modelKey);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (KeyValuePair<string, Sprite> pair in _sprites)
        {
            Texture2D texture = pair.Value != null ? pair.Value.texture : null!;
            if (pair.Value != null)
            {
                UnityEngine.Object.Destroy(pair.Value);
            }

            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        _sprites.Clear();
        _queue.Clear();
        _queued.Clear();
        if (_fallbackSprite != null) UnityEngine.Object.Destroy(_fallbackSprite);
        if (_fallbackTexture != null) UnityEngine.Object.Destroy(_fallbackTexture);
        if (_cameraObject != null) UnityEngine.Object.Destroy(_cameraObject);
        _fallbackSprite = null;
        _fallbackTexture = null;
        _cameraObject = null;
        _camera = null;
        _hdCameraData = null;
    }

    private void TryRender(string modelKey)
    {
        if (!_gameAdapter.TryGetActiveCameraCullingMask(out int visibleLayerMask))
        {
            ModLog.Debug($"Fear card thumbnail unavailable: model={modelKey}, camera mask unavailable.");
            return;
        }

        RuntimeEnemyVisual? visual = null;
        Material[] thumbnailMaterials = Array.Empty<Material>();
        RenderTexture? target = null;
        RenderTexture? previousTarget = RenderTexture.active;
        try
        {
            if (!TryCreateVisual(modelKey, visibleLayerMask, out visual, out string reason)
                || visual == null)
            {
                ModLog.Debug($"Fear card thumbnail clone rejected: model={modelKey}, reason={reason}.");
                return;
            }

            EnsureRenderRig();
            visual.SetLayer(ThumbnailLayer);
            if (!visual.TryApplyThumbnailMaterials(out thumbnailMaterials, out string materialReason))
            {
                ModLog.Warning($"Fear card thumbnail material fallback: model={modelKey}, reason={materialReason}.");
            }

            Vector3 isolatedPosition = new Vector3(12000f, 12000f, 12000f);
            Quaternion rotation = FearThumbnailPoseRules.ResolveCardRotation(
                FearModelPresentationRules.ResolveWorldRotation(Quaternion.identity, modelKey),
                modelKey);
            visual.ApplyPose(isolatedPosition, rotation);
            if (!visual.TryGetWorldBounds(out Bounds bounds))
            {
                ModLog.Debug($"Fear card thumbnail has no active bounds: model={modelKey}.");
                return;
            }

            ConfigureRig(bounds, FearThumbnailPoseRules.ResolveOrthographicScale(modelKey));
            target = new RenderTexture(
                ThumbnailSize,
                ThumbnailSize,
                24,
                RenderTextureFormat.ARGBHalf,
                RenderTextureReadWrite.Linear)
            {
                name = $"Enhanced Spectator Fear Thumbnail RT {modelKey}",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            target.Create();
            _camera!.targetTexture = target;
            Color[] blackBackground = CapturePixels(target, Color.black);
            Color[] whiteBackground = CapturePixels(target, Color.white);
            Texture2D texture = ComposeThumbnail(modelKey, blackBackground, whiteBackground);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, ThumbnailSize, ThumbnailSize),
                new Vector2(0.5f, 0.5f),
                ThumbnailSize);
            sprite.name = $"Enhanced Spectator Fear Card {modelKey}";
            _sprites[modelKey] = sprite;
            Revision++;
            ModLog.Debug($"Fear card thumbnail rendered from safe model data: model={modelKey}.");
        }
        catch (Exception ex)
        {
            ModLog.Warning($"Fear card thumbnail failed for {modelKey}: {ex.GetType().Name}.");
        }
        finally
        {
            if (_camera != null)
            {
                _camera.targetTexture = null;
            }

            RenderTexture.active = previousTarget;
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.Destroy(target);
            }

            visual?.Dispose();
            for (int index = 0; index < thumbnailMaterials.Length; index++)
            {
                if (thumbnailMaterials[index] != null)
                {
                    UnityEngine.Object.Destroy(thumbnailMaterials[index]);
                }
            }
        }
    }

    private bool TryCreateVisual(
        string modelKey,
        int visibleLayerMask,
        out RuntimeEnemyVisual? visual,
        out string reason)
    {
        if (string.Equals(modelKey, FearModeRules.DefaultModelKey, StringComparison.Ordinal))
        {
            return TryCreateDetachedHeadVisual(out visual, out reason);
        }

        if (!_catalog.TryGetVisualSource(modelKey, out FearVisualSource? source) || source == null)
        {
            visual = null;
            reason = "catalog visual source unavailable";
            return false;
        }

        return _visualFactory.TryCreate(
            source,
            modelKey,
            targetHeight: 1.8f,
            useOriginalScale: false,
            scaleMultiplier: 1f,
            visibleLayerMask,
            out visual,
            out reason);
    }

    private bool TryCreateDetachedHeadVisual(out RuntimeEnemyVisual? visual, out string reason)
    {
        if (!_detachedHeadSource.TryGetDetachedHeadVisualTemplate(out Transform? source) || source == null)
        {
            visual = null;
            reason = "detached-head template unavailable";
            return false;
        }

        MeshRenderer sourceRenderer = source.GetComponentInChildren<MeshRenderer>(includeInactive: true);
        MeshFilter sourceFilter = sourceRenderer != null ? sourceRenderer.GetComponent<MeshFilter>() : null!;
        if (sourceRenderer == null || sourceFilter == null || sourceFilter.sharedMesh == null)
        {
            visual = null;
            reason = "detached-head template has no supported mesh renderer";
            return false;
        }

        GameObject root = new GameObject("Enhanced Spectator Default Head Thumbnail");
        root.SetActive(false);
        GameObject content = new GameObject("Detached Head Mesh");
        content.transform.SetParent(root.transform, worldPositionStays: false);
        MeshFilter meshFilter = content.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = content.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = sourceFilter.sharedMesh;
        meshRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        meshRenderer.enabled = true;
        meshRenderer.forceRenderingOff = false;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.allowOcclusionWhenDynamic = false;
        visual = new RuntimeEnemyVisual(FearModeRules.DefaultModelKey, root, content.transform, false);
        reason = string.Empty;
        return true;
    }

    private void EnsureRenderRig()
    {
        if (_camera == null)
        {
            _cameraObject = new GameObject("Enhanced Spectator Fear Thumbnail Camera");
            UnityEngine.Object.DontDestroyOnLoad(_cameraObject);
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.cullingMask = 1 << ThumbnailLayer;
            _camera.orthographic = true;
            _camera.allowHDR = true;
            _camera.allowMSAA = false;
            _camera.useOcclusionCulling = false;
            _hdCameraData = _cameraObject.AddComponent<HDAdditionalCameraData>();
            ConfigureHdrpCamera(_hdCameraData);
        }
    }

    private static void ConfigureHdrpCamera(HDAdditionalCameraData cameraData)
    {
        cameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        cameraData.backgroundColorHDR = Color.black;
        cameraData.volumeLayerMask = 0;
        cameraData.volumeAnchorOverride = null;
        cameraData.exposureTarget = null;
        cameraData.customRenderingSettings = true;
        cameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
        cameraData.dithering = false;
        cameraData.stopNaNs = false;
        SetHdrpFrameSetting(cameraData, FrameSettingsField.Postprocess, false);
        SetHdrpFrameSetting(cameraData, FrameSettingsField.ExposureControl, false);
        SetHdrpFrameSetting(cameraData, FrameSettingsField.ColorGrading, false);
        SetHdrpFrameSetting(cameraData, FrameSettingsField.CustomPostProcess, false);
        SetHdrpFrameSetting(cameraData, FrameSettingsField.AfterPostprocess, false);
    }

    private static void SetHdrpFrameSetting(
        HDAdditionalCameraData cameraData,
        FrameSettingsField field,
        bool enabled)
    {
        ref FrameSettings frameSettings = ref cameraData.renderingPathCustomFrameSettings;
        frameSettings.SetEnabled(field, enabled);
        FrameSettingsOverrideMask overrideMask = cameraData.renderingPathCustomFrameSettingsOverrideMask;
        BitArray128 bits = overrideMask.mask;
        bits[(uint)field] = true;
        overrideMask.mask = bits;
        cameraData.renderingPathCustomFrameSettingsOverrideMask = overrideMask;
    }

    private void ConfigureRig(Bounds bounds, float orthographicScale)
    {
        float largestExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        _camera!.orthographicSize = Mathf.Max(0.2f, largestExtent * 1.28f * orthographicScale);
        Vector3 direction = FearThumbnailPoseRules.ResolveFrontCameraDirection();
        _camera.transform.position = bounds.center + (direction * Mathf.Max(3f, largestExtent * 4f));
        _camera.transform.LookAt(bounds.center + (Vector3.up * bounds.extents.y * 0.05f));
        _camera.nearClipPlane = 0.01f;
        _camera.farClipPlane = Mathf.Max(20f, largestExtent * 12f);
    }

    private Color[] CapturePixels(RenderTexture target, Color background)
    {
        _camera!.backgroundColor = background;
        if (_hdCameraData != null)
        {
            _hdCameraData.backgroundColorHDR = background;
        }

        _camera.Render();
        RenderTexture.active = target;
        Texture2D staging = new Texture2D(
            ThumbnailSize,
            ThumbnailSize,
            TextureFormat.RGBAHalf,
            mipChain: false,
            linear: true);
        try
        {
            staging.ReadPixels(
                new Rect(0f, 0f, ThumbnailSize, ThumbnailSize),
                0,
                0,
                recalculateMipMaps: false);
            staging.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return staging.GetPixels();
        }
        finally
        {
            UnityEngine.Object.Destroy(staging);
        }
    }

    private static Texture2D ComposeThumbnail(
        string modelKey,
        IReadOnlyList<Color> blackBackground,
        IReadOnlyList<Color> whiteBackground)
    {
        int pixelCount = ThumbnailSize * ThumbnailSize;
        Color[] output = new Color[pixelCount];
        for (int index = 0; index < pixelCount; index++)
        {
            Color black = blackBackground[index];
            Color white = whiteBackground[index];
            float alpha = FearThumbnailColorRules.ResolveAlpha(black, white);
            if (alpha <= 0.015f)
            {
                output[index] = Color.clear;
                continue;
            }

            float inverseAlpha = 1f / Mathf.Max(alpha, 0.05f);
            output[index] = new Color(
                FearThumbnailColorRules.ToneMapToGamma(black.r * inverseAlpha),
                FearThumbnailColorRules.ToneMapToGamma(black.g * inverseAlpha),
                FearThumbnailColorRules.ToneMapToGamma(black.b * inverseAlpha),
                Mathf.Clamp01(alpha));
        }

        Texture2D texture = new Texture2D(
            ThumbnailSize,
            ThumbnailSize,
            TextureFormat.RGBA32,
            mipChain: false,
            linear: false)
        {
            name = $"Enhanced Spectator Fear Thumbnail {modelKey}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(output);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        return texture;
    }

    private void CreateFallback()
    {
        const int size = 48;
        _fallbackTexture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            name = "Enhanced Spectator Default Ghost Card",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color32[] pixels = new Color32[size * size];
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 cream = new Color32(238, 223, 190, 255);
        Color32 red = new Color32(180, 42, 22, 255);
        for (int index = 0; index < pixels.Length; index++) pixels[index] = clear;
        for (int y = 8; y < 40; y++)
        {
            for (int x = 8; x < 40; x++)
            {
                int dx = x - 24;
                int dy = y - 23;
                if ((dx * dx) + (dy * dy) <= 230) pixels[(y * size) + x] = cream;
            }
        }

        for (int y = 19; y < 26; y++)
        {
            for (int x = 15; x < 21; x++) pixels[(y * size) + x] = red;
            for (int x = 28; x < 34; x++) pixels[(y * size) + x] = red;
        }

        _fallbackTexture.SetPixels32(pixels);
        _fallbackTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        _fallbackSprite = Sprite.Create(
            _fallbackTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        _fallbackSprite.name = "Enhanced Spectator Default Ghost Card";
    }
}
