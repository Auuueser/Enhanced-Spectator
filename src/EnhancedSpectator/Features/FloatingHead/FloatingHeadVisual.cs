using System;
using EnhancedSpectator.Features.SpectatorPresence;
using UnityEngine;

namespace EnhancedSpectator.Features.FloatingHead;

/// <summary>
/// Runtime-only placeholder visual for one remote spectator.
/// </summary>
public sealed class FloatingHeadVisual : IDisposable
{
    private readonly GameObject _gameObject;
    private readonly Material? _material;
    private readonly Mesh? _runtimeMesh;
    private readonly MeshRenderer? _meshRenderer;
    private readonly MeshFilter? _meshFilter;
    private readonly NameTagVisual? _nameTag;
    private readonly Color _baseColor;
    private readonly float _baseAlpha;
    private readonly bool _colliderRemoved;
    private Vector3 _smoothedPosition;
    private Vector3 _positionVelocity;
    private Quaternion _smoothedRotation = Quaternion.identity;
    private float _smoothedScale;
    private float _scaleVelocity;
    private float _smoothedVoiceLevel;
    private float _voiceVelocity;
    private int _lastSmoothingFrame = -1;
    private int _lastVoiceSmoothingFrame = -1;
    private int _currentLayer = -1;
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

    /// <summary>
    /// Creates a floating-head visual wrapper.
    /// </summary>
    public FloatingHeadVisual(
        ulong spectatorClientId,
        ulong spectatorSlotId,
        FloatingHeadVisualSourceKind sourceKind,
        GameObject gameObject,
        Material? material,
        Mesh? runtimeMesh,
        Color baseColor,
        float baseAlpha,
        MeshRenderer? meshRenderer,
        MeshFilter? meshFilter,
        bool colliderRemoved,
        NameTagVisual? nameTag)
    {
        SpectatorClientId = spectatorClientId;
        SpectatorSlotId = spectatorSlotId;
        SourceKind = sourceKind;
        _gameObject = gameObject ?? throw new ArgumentNullException(nameof(gameObject));
        _material = material;
        _runtimeMesh = runtimeMesh;
        _meshRenderer = meshRenderer;
        _meshFilter = meshFilter;
        _nameTag = nameTag;
        _baseColor = baseColor;
        _baseAlpha = Mathf.Clamp01(baseAlpha);
        _colliderRemoved = colliderRemoved;
        State = new FloatingHeadVisualState(spectatorClientId, spectatorSlotId, false, Vector3.zero);
    }

    /// <summary>
    /// Gets the remote spectator Netcode client id represented by this visual.
    /// </summary>
    public ulong SpectatorClientId { get; }

    /// <summary>
    /// Gets the remote spectator player slot id represented by this visual.
    /// </summary>
    public ulong SpectatorSlotId { get; }

    /// <summary>
    /// Gets the runtime source used to create this visual.
    /// </summary>
    public FloatingHeadVisualSourceKind SourceKind { get; }

    /// <summary>
    /// Gets the runtime material shader name used by the visual.
    /// </summary>
    public string ShaderName => _material != null && _material.shader != null
        ? _material.shader.name
        : "none";

    /// <summary>
    /// Gets the marker base color.
    /// </summary>
    public Color BaseColor => _baseColor;

    /// <summary>
    /// Gets the runtime material render queue.
    /// </summary>
    public int MaterialRenderQueue => _material != null ? _material.renderQueue : -1;

    /// <summary>
    /// Gets whether the visual has a mesh renderer.
    /// </summary>
    public bool HasMeshRenderer => _meshRenderer != null;

    /// <summary>
    /// Gets whether the visual has a mesh filter.
    /// </summary>
    public bool HasMeshFilter => _meshFilter != null;

    /// <summary>
    /// Gets the current visual mesh name.
    /// </summary>
    public string MeshName
    {
        get
        {
            Mesh? mesh = _meshFilter != null ? _meshFilter.sharedMesh : null;
            return mesh != null ? mesh.name : "none";
        }
    }

    /// <summary>
    /// Gets whether the primitive collider was removed.
    /// </summary>
    public bool ColliderRemoved => _colliderRemoved;

    /// <summary>
    /// Gets whether this placeholder has a runtime name tag.
    /// </summary>
    public bool HasNameTag => _nameTag != null;

    /// <summary>
    /// Gets the current runtime name tag text.
    /// </summary>
    public string NameTagText => _nameTag != null ? _nameTag.Text : string.Empty;

    /// <summary>
    /// Gets whether the visual renderer is enabled.
    /// </summary>
    public bool RendererEnabled => _meshRenderer != null && _meshRenderer.enabled;

    /// <summary>
    /// Gets whether the visual renderer is force-disabled.
    /// </summary>
    public bool ForceRenderingOff => _meshRenderer != null && _meshRenderer.forceRenderingOff;

    /// <summary>
    /// Gets whether the visual object is active locally.
    /// </summary>
    public bool ActiveSelf => _gameObject != null && _gameObject.activeSelf;

    /// <summary>
    /// Gets whether the visual object is active in the scene hierarchy.
    /// </summary>
    public bool ActiveInHierarchy => _gameObject != null && _gameObject.activeInHierarchy;

    /// <summary>
    /// Gets the visual object's current layer.
    /// </summary>
    public int Layer => _gameObject != null ? _gameObject.layer : -1;

    /// <summary>
    /// Gets whether the visual root object is active.
    /// </summary>
    public bool RootActiveInHierarchy
    {
        get
        {
            if (_gameObject == null || _gameObject.transform.parent == null)
            {
                return false;
            }

            return _gameObject.transform.parent.gameObject.activeInHierarchy;
        }
    }

    /// <summary>
    /// Gets the latest visual state.
    /// </summary>
    public FloatingHeadVisualState State { get; private set; }

    /// <summary>
    /// Gets the most recent applied visual scale.
    /// </summary>
    public float CurrentScale => _hasPose ? Mathf.Max(0.01f, _smoothedScale) : 1f;

    /// <summary>
    /// Updates local smoothed voice activity and material brightness.
    /// </summary>
    public float UpdateVoiceLevel(float targetLevel, float smoothTime)
    {
        return UpdateVoiceLevel(targetLevel, smoothTime, smoothTime);
    }

    /// <summary>
    /// Updates local smoothed voice activity with separate attack and release smoothing.
    /// </summary>
    public float UpdateVoiceLevel(float targetLevel, float attackSmoothTime, float releaseSmoothTime)
    {
        if (_disposed)
        {
            return 0f;
        }

        float clampedTarget = Mathf.Clamp01(targetLevel);
        float clampedSmoothTime = FloatingHeadVoiceScaleRules.ResolveVoiceSmoothTime(
            _smoothedVoiceLevel,
            clampedTarget,
            attackSmoothTime,
            releaseSmoothTime);
        if (clampedSmoothTime <= 0f)
        {
            _smoothedVoiceLevel = clampedTarget;
            _voiceVelocity = 0f;
            _lastVoiceSmoothingFrame = Time.frameCount;
        }
        else if (_lastVoiceSmoothingFrame != Time.frameCount)
        {
            _smoothedVoiceLevel = Mathf.SmoothDamp(
                _smoothedVoiceLevel,
                clampedTarget,
                ref _voiceVelocity,
                clampedSmoothTime,
                Mathf.Infinity,
                Mathf.Max(Time.deltaTime, 0.0001f));
            _lastVoiceSmoothingFrame = Time.frameCount;
        }

        ApplyMaterialVoiceLevel(_smoothedVoiceLevel);
        return _smoothedVoiceLevel;
    }

    /// <summary>
    /// Applies a world pose and scale to the visual.
    /// </summary>
    public void ApplyPose(Vector3 position, Quaternion rotation, float scale)
    {
        ApplyPose(position, rotation, scale, smoothTime: 0f);
    }

    /// <summary>
    /// Applies a world pose and scale to the visual, optionally smoothing movement.
    /// </summary>
    public void ApplyPose(Vector3 position, Quaternion rotation, float scale, float smoothTime)
    {
        ApplyPose(position, rotation, scale, smoothTime, smoothTime);
    }

    /// <summary>
    /// Applies a world pose and scale to the visual, with separate scale smoothing.
    /// </summary>
    public void ApplyPose(Vector3 position, Quaternion rotation, float scale, float smoothTime, float scaleSmoothTime)
    {
        ApplyPose(
            position,
            rotation,
            scale,
            smoothTime,
            scaleSmoothTime,
            sampleTimestampTicks: 0,
            motionReferenced: false,
            motionReference: default);
    }

    /// <summary>
    /// Applies a synchronized world pose while preserving moving-reference motion and predicting between samples.
    /// </summary>
    public void ApplyPose(
        Vector3 position,
        Quaternion rotation,
        float scale,
        float smoothTime,
        float scaleSmoothTime,
        long sampleTimestampTicks,
        bool motionReferenced,
        SpectatorMotionReferencePose motionReference)
    {
        if (_disposed || _gameObject == null)
        {
            return;
        }

        position = ResolvePredictedPosition(
            position,
            sampleTimestampTicks,
            motionReferenced);
        Vector3 renderedPosition = position;
        Quaternion renderedRotation = rotation;
        float clampedSmoothTime = Mathf.Max(0f, smoothTime);
        float clampedScaleSmoothTime = Mathf.Max(0f, scaleSmoothTime);
        if (!_hasPose || clampedSmoothTime <= 0f)
        {
            _smoothedPosition = position;
            _positionVelocity = Vector3.zero;
            _smoothedRotation = rotation;
            _smoothedScale = Mathf.Max(0.01f, scale);
            _scaleVelocity = 0f;
            _hasPose = true;
            _lastSmoothingFrame = Time.frameCount;
        }
        else
        {
            if (_lastSmoothingFrame != Time.frameCount)
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
                if (clampedScaleSmoothTime <= 0f)
                {
                    _smoothedScale = Mathf.Max(0.01f, scale);
                    _scaleVelocity = 0f;
                }
                else
                {
                    _smoothedScale = Mathf.SmoothDamp(
                        _smoothedScale,
                        Mathf.Max(0.01f, scale),
                        ref _scaleVelocity,
                        clampedScaleSmoothTime,
                        Mathf.Infinity,
                        deltaTime);
                }
                _lastSmoothingFrame = Time.frameCount;
            }

            renderedPosition = _smoothedPosition;
            renderedRotation = _smoothedRotation;
            scale = _smoothedScale;
        }

        _hasMotionReference = motionReferenced;
        if (motionReferenced)
        {
            _motionReference = motionReference;
        }

        Transform transform = _gameObject.transform;
        transform.SetPositionAndRotation(renderedPosition, renderedRotation);
        transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        SetVisible(true);
        State = new FloatingHeadVisualState(SpectatorClientId, SpectatorSlotId, true, renderedPosition);
        _nameTag?.ApplyPose(renderedPosition, renderedRotation, camera: null);
    }

    private Vector3 ResolvePredictedPosition(
        Vector3 position,
        long sampleTimestampTicks,
        bool motionReferenced)
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

    private void ApplyMotionReferenceDelta(
        bool motionReferenced,
        SpectatorMotionReferencePose motionReference)
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

    /// <summary>
    /// Updates the optional name tag against the current render camera.
    /// </summary>
    public void UpdateNameTag(Camera? camera, float minimumHeightOffset = 0f)
    {
        if (_disposed || _nameTag == null || _gameObject == null || !State.IsVisible)
        {
            return;
        }

        _nameTag.ApplyPose(State.Position, _gameObject.transform.rotation, camera, minimumHeightOffset);
    }

    /// <summary>
    /// Updates the optional name tag text if it has changed.
    /// </summary>
    public bool TrySetNameTagText(string text)
    {
        if (_disposed || _nameTag == null)
        {
            return false;
        }

        return _nameTag.SetText(text);
    }

    /// <summary>
    /// Shows or hides the primary head renderer while preserving pose and optional name tag visibility.
    /// </summary>
    public void SetPrimaryRendererVisible(bool visible)
    {
        if (_disposed || _meshRenderer == null)
        {
            return;
        }

        _meshRenderer.enabled = true;
        _meshRenderer.forceRenderingOff = !visible;
    }

    /// <summary>
    /// Sets the visual object's Unity layer.
    /// </summary>
    public void SetLayer(int layer)
    {
        if (_disposed || _gameObject == null || layer < 0 || layer > 31)
        {
            return;
        }

        if (_currentLayer == layer)
        {
            return;
        }

        SetLayerRecursive(_gameObject.transform, layer);
        _currentLayer = layer;

        _nameTag?.SetLayer(layer);
    }

    /// <summary>
    /// Sets whether the visual object is active.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (_disposed || _gameObject == null)
        {
            return;
        }

        if (_gameObject.activeSelf != visible)
        {
            _gameObject.SetActive(visible);
        }

        _nameTag?.SetVisible(visible);
        State = new FloatingHeadVisualState(
            SpectatorClientId,
            SpectatorSlotId,
            visible,
            _gameObject.transform.position);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_gameObject != null)
        {
            UnityEngine.Object.Destroy(_gameObject);
        }

        if (_material != null)
        {
            UnityEngine.Object.Destroy(_material);
        }

        if (_runtimeMesh != null)
        {
            UnityEngine.Object.Destroy(_runtimeMesh);
        }

        _nameTag?.Dispose();
    }

    private void ApplyMaterialVoiceLevel(float voiceLevel)
    {
        if (_material == null)
        {
            return;
        }

        float intensity = 1f + (voiceLevel * 0.65f);
        Color color = _baseColor * intensity;
        color.a = _baseAlpha;

        SetMaterialColor("_BaseColor", color);
        SetMaterialColor("_UnlitColor", color);
        SetMaterialColor("_Color", color);
        SetMaterialColor("_EmissionColor", color);
        SetMaterialColor("_EmissiveColor", color);
    }

    private void SetMaterialColor(string propertyName, Color color)
    {
        if (_material != null && _material.HasProperty(propertyName))
        {
            _material.SetColor(propertyName, color);
        }
    }

    private static void SetLayerRecursive(Transform transform, int layer)
    {
        if (transform.gameObject.layer != layer)
        {
            transform.gameObject.layer = layer;
        }

        for (int index = 0; index < transform.childCount; index++)
        {
            Transform child = transform.GetChild(index);
            if (child != null)
            {
                SetLayerRecursive(child, layer);
            }
        }
    }
}
