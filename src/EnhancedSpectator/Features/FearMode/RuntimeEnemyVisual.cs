using System;
using System.Collections.Generic;
using EnhancedSpectator.Features.SpectatorPresence;
using EnhancedSpectator.Logging;
using EnhancedSpectator.GameInterop;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Owns one clean renderer-only runtime enemy visual.
/// </summary>
public sealed partial class RuntimeEnemyVisual : IDisposable
{
    private readonly GameObject _root;
    private readonly Transform _contentRoot;
    private readonly Renderer[] _renderers;
    private readonly Mesh[] _ownedMeshes;
    private readonly Vector3 _baseScale;
    private Bounds? _fadeEnvelope;
    private Vector3 _smoothedPosition;
    private Vector3 _positionVelocity;
    private Quaternion _smoothedRotation = Quaternion.identity;
    private bool _runtimeRecenterPending;
    private bool _hasPose;
    private bool _hasMotionReference;
    private SpectatorMotionReferencePose _motionReference;
    private bool _hasNetworkSample;
    private long _networkSampleTimestampTicks;
    private Vector3 _networkSamplePosition;
    private Vector3 _networkSampleVelocity;
    private float _networkSampleTime;
    private bool _hasNetworkSampleVelocity;
    private bool _disposed;

    internal RuntimeEnemyVisual(
        string modelKey,
        GameObject root,
        Transform contentRoot,
        bool runtimeRecenterPending, Mesh[]? ownedMeshes = null, Bounds? fadeEnvelope = null)
    {
        _fadeEnvelope = fadeEnvelope;
        _ownedMeshes = ownedMeshes ?? Array.Empty<Mesh>();
        ModelKey = modelKey ?? string.Empty;
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _baseScale = root.transform.localScale;
        _contentRoot = contentRoot ?? throw new ArgumentNullException(nameof(contentRoot));
        _renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
        _runtimeRecenterPending = runtimeRecenterPending;
    }

    /// <summary>Gets the source model key.</summary>
    public string ModelKey { get; }
    internal (string Model, int Hierarchy, int Renderers, int Mask) ReuseKey { get; set; }
    internal void ParkForReuse()
    {
        DisableCameraFade();
        SetWatchedTarget(null, null);
        _hasPose = _hasMotionReference = _hasNetworkSample = _hasNetworkSampleVelocity = false;
        _positionVelocity = _networkSampleVelocity = Vector3.zero;
        TongueMotion?.ResetForReuse();
        SetVisible(false);
    }
    internal OriginalTongueVisualMotion? TongueMotion { get; set; }
    internal FearVisualSource SnapshotSource() => new FearVisualSource(_root.transform, _root.transform,
        forceRendererVisibility: false, normalizeRootPose: true, miniature: true);

    /// <summary>Updates owner size without rebuilding the hierarchy or altering ghost/voice position.</summary>
    public void ApplyOwnerScale(float scale)
    {
        if (!_disposed && _root != null) _root.transform.localScale = _baseScale * FearModelAppearanceRules.ClampScale(scale);
    }

    /// <summary>Applies the visual world pose.</summary>
    public void ApplyPose(
        Vector3 position,
        Quaternion rotation,
        float smoothTime = 0f,
        long sampleTimestampTicks = 0,
        bool motionReferenced = false,
        SpectatorMotionReferencePose motionReference = default)
    {
        if (_disposed || _root == null)
        {
            return;
        }

        position = ResolvePredictedPosition(position, sampleTimestampTicks, motionReferenced);
        float clampedSmoothTime = Mathf.Max(0f, smoothTime);
        if (!_hasPose || clampedSmoothTime <= 0f)
        {
            _smoothedPosition = position;
            _positionVelocity = Vector3.zero;
            _smoothedRotation = rotation;
            _hasPose = true;
        }
        else
        {
            ApplyMotionReferenceDelta(motionReferenced, motionReference);
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            _smoothedPosition = Vector3.SmoothDamp(
                _smoothedPosition,
                position,
                ref _positionVelocity,
                clampedSmoothTime,
                Mathf.Infinity,
                deltaTime);
            float rotationT = 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.0001f, clampedSmoothTime));
            _smoothedRotation = Quaternion.Slerp(_smoothedRotation, rotation, rotationT);
        }

        _hasMotionReference = motionReferenced;
        if (motionReferenced)
        {
            _motionReference = motionReference;
        }

        _root.transform.SetPositionAndRotation(_smoothedPosition, _smoothedRotation);
        if (!_root.activeSelf)
        {
            _root.SetActive(true);
        }

        TryApplyRuntimeBoundsRecenter();
        TongueMotion?.Tick();
    }

    private Vector3 ResolvePredictedPosition(Vector3 position, long sampleTimestampTicks, bool motionReferenced)
    {
        if (motionReferenced || sampleTimestampTicks == 0)
        {
            _hasNetworkSample = false;
            _hasNetworkSampleVelocity = false;
            return position;
        }

        float now = Time.unscaledTime;
        if (!_hasNetworkSample)
        {
            _hasNetworkSample = true;
            _networkSampleTimestampTicks = sampleTimestampTicks;
            _networkSamplePosition = position;
            _networkSampleVelocity = Vector3.zero;
            _networkSampleTime = now;
            _hasNetworkSampleVelocity = false;
        }
        else if (_networkSampleTimestampTicks != sampleTimestampTicks)
        {
            float sampleDeltaTime = now - _networkSampleTime;
            _networkSampleVelocity = RemoteSpectatorPosePredictionRules.ResolveVelocity(
                _networkSamplePosition,
                position,
                sampleDeltaTime,
                _networkSampleVelocity,
                _hasNetworkSampleVelocity);
            _hasNetworkSampleVelocity = _networkSampleVelocity.sqrMagnitude > 0.000001f;
            _networkSampleTimestampTicks = sampleTimestampTicks;
            _networkSamplePosition = position;
            _networkSampleTime = now;
        }

        return RemoteSpectatorPosePredictionRules.ResolvePredictedPosition(
            _networkSamplePosition,
            _networkSampleVelocity,
            now - _networkSampleTime);
    }

    private void ApplyMotionReferenceDelta(bool motionReferenced, SpectatorMotionReferencePose motionReference)
    {
        if (!motionReferenced || !_hasMotionReference)
        {
            return;
        }

        Vector3 velocityEnd = _smoothedPosition + _positionVelocity;
        Vector3 movedPosition = RemoteSpectatorMotionCompensationRules.ResolvePosition(
            _smoothedPosition,
            _motionReference,
            motionReference);
        Vector3 movedVelocityEnd = RemoteSpectatorMotionCompensationRules.ResolvePosition(
            velocityEnd,
            _motionReference,
            motionReference);
        _smoothedPosition = movedPosition;
        _positionVelocity = movedVelocityEnd - movedPosition;
        _smoothedRotation = RemoteSpectatorMotionCompensationRules.ResolveRotation(
            _smoothedRotation,
            _motionReference,
            motionReference);
    }

    private void TryApplyRuntimeBoundsRecenter()
    {
        if (!_runtimeRecenterPending || _contentRoot == null)
        {
            return;
        }

        bool hasBounds = false;
        Bounds combinedBounds = default;
        for (int index = 0; index < _renderers.Length; index++)
        {
            Renderer renderer = _renderers[index];
            if (renderer == null
                || !renderer.enabled
                || renderer.forceRenderingOff
                || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return;
        }

        Vector3 worldOffset = FearVisualNormalizationRules.ResolveRuntimeWorldOffset(
            _smoothedPosition,
            combinedBounds.center);
        if (float.IsNaN(worldOffset.x)
            || float.IsNaN(worldOffset.y)
            || float.IsNaN(worldOffset.z)
            || float.IsInfinity(worldOffset.x)
            || float.IsInfinity(worldOffset.y)
            || float.IsInfinity(worldOffset.z))
        {
            return;
        }

        _contentRoot.position += worldOffset;
        if (_fadeEnvelope.HasValue)
        {
            var envelope = _fadeEnvelope.Value;
            envelope.center += _root.transform.InverseTransformVector(worldOffset);
            _fadeEnvelope = envelope;
        }
        _runtimeRecenterPending = false;
        ModLog.Info(
            $"Fear visual runtime recentered: model={ModelKey}, offset=({worldOffset.x:0.###}, {worldOffset.y:0.###}, {worldOffset.z:0.###}).");
    }

    /// <summary>Gets the current visible world height above the representation origin.</summary>
    public bool TryGetWorldTopOffset(out float topOffset)
    {
        topOffset = 0f;
        if (_disposed || _root == null)
        {
            return false;
        }

        bool hasBounds = false;
        float highestPoint = float.NegativeInfinity;
        for (int index = 0; index < _renderers.Length; index++)
        {
            Renderer renderer = _renderers[index];
            if (renderer == null
                || !renderer.enabled
                || renderer.forceRenderingOff
                || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            highestPoint = Mathf.Max(highestPoint, renderer.bounds.max.y);
            hasBounds = true;
        }

        if (!hasBounds)
        {
            return false;
        }

        topOffset = Mathf.Max(0f, highestPoint - _root.transform.position.y);
        return true;
    }

    /// <summary>Gets combined active renderer bounds for an isolated thumbnail render.</summary>
    public bool TryGetWorldBounds(out Bounds bounds)
    {
        bounds = default;
        if (_disposed || _root == null)
        {
            return false;
        }

        bool hasBounds = false;
        for (int index = 0; index < _renderers.Length; index++)
        {
            Renderer renderer = _renderers[index];
            if (renderer == null
                || !renderer.enabled
                || renderer.forceRenderingOff
                || !renderer.gameObject.activeInHierarchy)
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

        return hasBounds && bounds.extents.sqrMagnitude > 0.000001f;
    }

    /// <summary>Applies a Unity layer to the clean hierarchy.</summary>
    public void SetLayer(int layer)
    {
        if (_disposed || layer < 0 || layer > 31)
        {
            return;
        }

        Transform[] transforms = _root.GetComponentsInChildren<Transform>(includeInactive: true);
        for (int index = 0; index < transforms.Length; index++)
        {
            transforms[index].gameObject.layer = layer;
        }
    }

    /// <summary>
    /// Replaces scene-dependent shared materials with owned unlit thumbnail copies while preserving albedo data.
    /// </summary>
    public bool TryApplyThumbnailMaterials(out Material[] ownedMaterials, out string reason)
    {
        ownedMaterials = Array.Empty<Material>();
        if (_disposed || _root == null)
        {
            reason = "visual is disposed";
            return false;
        }

        bool studio = FearItemPoseRules.UseStudioLighting(ModelKey);
        Shader? shader = studio ? Shader.Find("HDRP/Lit") : FindThumbnailShader();
        if (shader == null)
        {
            reason = "no compatible unlit shader is loaded";
            return false;
        }

        List<Material> created = new List<Material>();
        int replacedRendererCount = 0;
        int texturedMaterialCount = 0;
        for (int rendererIndex = 0; rendererIndex < _renderers.Length; rendererIndex++)
        {
            Renderer renderer = _renderers[rendererIndex];
            if (renderer == null || !renderer.enabled || renderer.forceRenderingOff)
            {
                continue;
            }

            Material[] sourceMaterials = renderer.sharedMaterials;
            if (sourceMaterials == null || sourceMaterials.Length == 0)
            {
                continue;
            }

            Material[] previewMaterials = new Material[sourceMaterials.Length];
            for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
            {
                Material source = sourceMaterials[materialIndex];
                Material preview;
                bool copiedTexture;
                if (studio && source != null && source.shader == shader)
                {
                    preview = new Material(source);
                    copiedTexture = ResolveSourceTexture(source) != null;
                    ConfigureThumbnailMaterial(preview);
                }
                else preview = CreateThumbnailMaterial(source, shader, out copiedTexture);
                if (studio)
                {
                    SetFloatIfPresent(preview, "_Metallic", 0.15f);
                    SetFloatIfPresent(preview, "_Smoothness", 0.35f);
                }
                previewMaterials[materialIndex] = preview;
                created.Add(preview);
                if (copiedTexture)
                {
                    texturedMaterialCount++;
                }
            }

            renderer.sharedMaterials = previewMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;
            replacedRendererCount++;
        }

        ownedMaterials = created.ToArray();
        reason = replacedRendererCount > 0
            ? string.Empty
            : "visual has no active renderer materials";
        if (replacedRendererCount > 0)
        {
            ModLog.Debug(
                $"Fear card materials prepared: model={ModelKey}, shader={shader.name}, " +
                $"renderers={replacedRendererCount}, materials={created.Count}, textured={texturedMaterialCount}.");
        }

        return replacedRendererCount > 0;
    }

    private static Shader? FindThumbnailShader()
    {
        string[] shaderNames =
        {
            "HDRP/Unlit",
            "Universal Render Pipeline/Unlit",
            "Unlit/Texture",
            "Sprites/Default",
        };

        for (int index = 0; index < shaderNames.Length; index++)
        {
            Shader shader = Shader.Find(shaderNames[index]);
            if (shader != null)
            {
                return shader;
            }
        }

        return null;
    }

    private static Material CreateThumbnailMaterial(
        Material? source,
        Shader shader,
        out bool copiedTexture)
    {
        Material material = new Material(shader)
        {
            name = source != null
                ? $"Enhanced Spectator Card {source.name}"
                : "Enhanced Spectator Card Material",
            renderQueue = 2000,
        };

        Texture? texture = ResolveSourceTexture(source);
        copiedTexture = texture != null;
        Color color = ResolveSourceColor(source);
        ApplyTexture(material, texture);
        ApplyColor(material, color);
        ConfigureThumbnailMaterial(material);
        return material;
    }

    private static Texture? ResolveSourceTexture(Material? source)
    {
        if (source == null)
        {
            return null;
        }

        string[] propertyNames =
        {
            "_Diffuse", // Confirmed original BushWolfMat / FurShader albedo.
            "_BaseColorMap",
            "_BaseMap",
            "_MainTex",
            "_UnlitColorMap",
        };
        for (int index = 0; index < propertyNames.Length; index++)
        {
            if (source.HasProperty(propertyNames[index]))
            {
                Texture texture = source.GetTexture(propertyNames[index]);
                if (texture != null)
                {
                    return texture;
                }
            }
        }

        return null;
    }

    private static Color ResolveSourceColor(Material? source)
    {
        if (source == null)
        {
            return Color.white;
        }

        string[] propertyNames =
        {
            "_BaseColor",
            "_Color",
            "_UnlitColor",
            "_EmissiveColor",
            "_EmissionColor",
        };
        for (int index = 0; index < propertyNames.Length; index++)
        {
            if (source.HasProperty(propertyNames[index]))
            {
                Color value = source.GetColor(propertyNames[index]);
                value.a = value.a <= 0.01f ? 1f : value.a;
                return value;
            }
        }

        return Color.white;
    }

    private static void ApplyTexture(Material material, Texture? texture)
    {
        string[] propertyNames =
        {
            "_BaseColorMap",
            "_BaseMap",
            "_MainTex",
            "_UnlitColorMap",
            "_EmissiveColorMap",
        };
        for (int index = 0; index < propertyNames.Length; index++)
        {
            if (material.HasProperty(propertyNames[index]))
            {
                material.SetTexture(propertyNames[index], texture);
            }
        }
    }

    private static void ApplyColor(Material material, Color color)
    {
        string[] propertyNames =
        {
            "_BaseColor",
            "_Color",
            "_UnlitColor",
            "_TintColor",
        };
        for (int index = 0; index < propertyNames.Length; index++)
        {
            if (material.HasProperty(propertyNames[index]))
            {
                material.SetColor(propertyNames[index], color);
            }
        }
    }

    private static void ConfigureThumbnailMaterial(Material material)
    {
        SetFloatIfPresent(material, "_SurfaceType", 0f);
        SetFloatIfPresent(material, "_ZWrite", 1f);
        SetFloatIfPresent(material, "_ZTest", (float)CompareFunction.LessEqual);
        SetFloatIfPresent(material, "_Cull", (float)CullMode.Off);
        SetFloatIfPresent(material, "_CullMode", (float)CullMode.Off);
        SetFloatIfPresent(material, "_TransparentCullMode", (float)CullMode.Off);
        SetFloatIfPresent(material, "_DoubleSidedEnable", 1f);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_EMISSION");
        material.EnableKeyword("_DOUBLESIDED_ON");
        SetColorIfPresent(material, "_EmissiveColor", Color.black);
        SetColorIfPresent(material, "_EmissionColor", Color.black);
        SetTextureIfPresent(material, "_EmissiveColorMap", null);
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static void SetTextureIfPresent(Material material, string propertyName, Texture? value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, value);
        }
    }

    /// <summary>Shows or hides the visual.</summary>
    public void SetVisible(bool visible)
    {
        if (!_disposed && _root != null && _root.activeSelf != visible)
        {
            _root.SetActive(visible);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeFadeMaterials();
        TongueMotion?.Dispose();
        foreach (var mesh in _ownedMeshes) if (mesh != null) UnityEngine.Object.Destroy(mesh);
        if (_root != null)
        {
            UnityEngine.Object.Destroy(_root);
        }
    }
}
