using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.Features.FloatingHead;

/// <summary>
/// Runtime-only world-space text label attached to a floating-head placeholder.
/// </summary>
public sealed class NameTagVisual : IDisposable
{
    private readonly GameObject _gameObject;
    private readonly TextMesh _textMesh;
    private readonly MeshRenderer _renderer;
    private readonly float _heightOffset;
    private readonly float _maxDistance;
    private bool _disposed;

    /// <summary>
    /// Creates a name tag visual.
    /// </summary>
    public NameTagVisual(
        GameObject gameObject,
        TextMesh textMesh,
        MeshRenderer renderer,
        float heightOffset,
        float maxDistance)
    {
        _gameObject = gameObject ?? throw new ArgumentNullException(nameof(gameObject));
        _textMesh = textMesh ?? throw new ArgumentNullException(nameof(textMesh));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _heightOffset = Mathf.Max(0f, heightOffset);
        _maxDistance = Mathf.Max(0f, maxDistance);
    }

    /// <summary>
    /// Gets whether the name tag object is active in the scene hierarchy.
    /// </summary>
    public bool ActiveInHierarchy => _gameObject != null && _gameObject.activeInHierarchy;

    /// <summary>
    /// Gets the visible label text.
    /// </summary>
    public string Text => _textMesh != null ? _textMesh.text : string.Empty;

    /// <summary>
    /// Updates the displayed text when it changes.
    /// </summary>
    public bool SetText(string text)
    {
        if (_disposed || _textMesh == null || _textMesh.text == text)
        {
            return false;
        }

        _textMesh.text = text;
        return true;
    }

    /// <summary>
    /// Updates the tag's world position and camera-facing rotation.
    /// </summary>
    public void ApplyPose(
        Vector3 markerPosition,
        Quaternion fallbackRotation,
        Camera? camera,
        float minimumHeightOffset = 0f)
    {
        if (_disposed || _gameObject == null)
        {
            return;
        }

        float resolvedHeightOffset = Mathf.Max(_heightOffset, minimumHeightOffset);
        Vector3 position = markerPosition + (Vector3.up * resolvedHeightOffset);
        bool visible = true;
        Quaternion rotation = fallbackRotation;
        if (camera != null)
        {
            Vector3 toCamera = camera.transform.position - position;
            float distance = toCamera.magnitude;
            visible = _maxDistance <= 0f || distance <= _maxDistance;
            rotation = Quaternion.LookRotation(
                camera.transform.rotation * Vector3.forward,
                camera.transform.rotation * Vector3.up);
        }

        _gameObject.transform.SetPositionAndRotation(position, rotation);
        SetVisible(visible);
    }

    /// <summary>
    /// Sets the Unity layer for the tag object.
    /// </summary>
    public void SetLayer(int layer)
    {
        if (_disposed || _gameObject == null || layer < 0 || layer > 31)
        {
            return;
        }

        if (_gameObject.layer != layer)
        {
            _gameObject.layer = layer;
        }
    }

    /// <summary>
    /// Sets whether the tag should render.
    /// </summary>
    public void SetVisible(bool visible)
    {
        visible &= !GameInterop.LethalCompanySpectatorUiVisibility.Hidden;
        if (_disposed || _gameObject == null)
        {
            return;
        }

        if (_gameObject.activeSelf != visible)
        {
            _gameObject.SetActive(visible);
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
        if (_gameObject != null)
        {
            UnityEngine.Object.Destroy(_gameObject);
        }
    }

    /// <summary>
    /// Creates a runtime text object under the provided visual root.
    /// </summary>
    public static NameTagVisual Create(
        Transform root,
        string text,
        Color color,
        float scale,
        float heightOffset,
        float maxDistance)
    {
        GameObject gameObject = new GameObject("Enhanced Spectator Name Tag");
        gameObject.SetActive(false);
        gameObject.transform.SetParent(root, false);

        TextMesh textMesh = gameObject.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 64;
        textMesh.characterSize = Mathf.Max(0.005f, scale);
        textMesh.richText = false;
        textMesh.color = color;

        MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.allowOcclusionWhenDynamic = false;
        renderer.sortingOrder = short.MaxValue;

        return new NameTagVisual(
            gameObject,
            textMesh,
            renderer,
            heightOffset,
            maxDistance);
    }
}
