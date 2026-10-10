using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.Features.FloatingHead;

/// <summary>
/// Runtime-only world-space text label attached to a floating-head placeholder, in the game's HUD font.
/// </summary>
public sealed class NameTagVisual : IDisposable
{
    private static readonly int FaceColor = Shader.PropertyToID("_FaceColor");
    private readonly GameObject _gameObject;
    private readonly TextMeshPro _text;
    private readonly MeshRenderer _renderer;
    // The fade is a per-renderer override of the face colour, read when a camera draws the text. A text colour is
    // baked into the mesh before rendering (a per-camera change never reached the frame being drawn), and a material
    // of our own is replaced whenever the font changes, as font-fallback mods (LC Chinese Project) do for names they
    // cannot draw; the renderer's override survives both.
    private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
    private readonly float _heightOffset;
    private readonly float _maxDistance;
    private bool _disposed;

    /// <summary>
    /// Creates a name tag visual.
    /// </summary>
    private NameTagVisual(
        GameObject gameObject,
        TextMeshPro text,
        MeshRenderer renderer,
        Color color,
        float heightOffset,
        float maxDistance)
    {
        _gameObject = gameObject;
        _text = text;
        _renderer = renderer;
        _baseColor = color;
        _heightOffset = Mathf.Max(0f, heightOffset);
        _maxDistance = Mathf.Max(0f, maxDistance);
        ApplyFace();
    }

    private void ApplyFace()
    {
        _block.SetColor(FaceColor, new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * _fade));
        _renderer.SetPropertyBlock(_block);
    }

    /// <summary>
    /// Gets whether the name tag object is active in the scene hierarchy.
    /// </summary>
    public bool ActiveInHierarchy => _gameObject != null && _gameObject.activeInHierarchy;

    /// <summary>
    /// Gets the visible label text.
    /// </summary>
    public string Text => _text.text;

    /// <summary>
    /// Updates the displayed text when it changes.
    /// </summary>
    public bool SetText(string text)
    {
        if (_disposed || _text.text == text)
        {
            return false;
        }

        _text.text = text;
        // Lay the new name out now, so the speaker icon placed beside it uses its new bounds.
        _text.ForceMeshUpdate();
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

    /// <summary>A model that has faded away completely still keeps its name this visible.</summary>
    internal const float MinimumOpacity = .15f;
    private Color _baseColor = Color.white;
    private float _fade = 1;

    /// <summary>
    /// Fades the name with its model, less strongly: names stay readable (at least <see cref="MinimumOpacity"/>)
    /// but follow the model's fade. Applied through the renderer's face colour override, so it holds for the camera drawing.
    /// </summary>
    internal void SetFade(float modelOpacity)
    {
        float fade = Mathf.Lerp(MinimumOpacity, 1, Mathf.Clamp01(modelOpacity));
        if (_disposed || Mathf.Abs(fade - _fade) < .01f) return;
        _fade = fade;
        ApplyFace();
        ApplyIconColor();
    }

    // The game's speaker glyph after the name while its spectator talks: quick to appear, a little slower to go so
    // it does not flicker between words. HDRP draws world text but not sprites, so the icon is a quad drawn with a
    // copy of Unity's built-in text material showing the glyph's texture (that shader uses only its alpha).
    private const float IconInSeconds = .12f, IconOutSeconds = .35f;
    private MeshRenderer? _icon;
    private Mesh? _iconMesh;
    private Material? _iconMaterial;
    private float _iconAspect = 1, _iconOpacity;

    /// <summary>Once per frame: shows the speaker icon while the spectator is speaking.</summary>
    internal void SetSpeaking(bool speaking, float deltaTime)
    {
        if (_disposed) return;
        float opacity = Mathf.MoveTowards(_iconOpacity, speaking ? 1 : 0, deltaTime / (speaking ? IconInSeconds : IconOutSeconds));
        if (opacity > 0 && _icon == null && !CreateIcon()) return;
        if (_icon == null) return;
        _iconOpacity = opacity;
        _icon.enabled = opacity > 0;
        if (opacity <= 0) return;
        // Beside the name as laid out now (names change), as tall as a capital letter whatever the line count (the
        // fallback-ID label has two). TextMeshPro's world em is a tenth of its font size.
        var bounds = _renderer.localBounds;
        float height = _text.fontSize * .07f, width = height * _iconAspect;
        _icon.transform.localPosition = new Vector3(bounds.max.x + height * .3f + width * .5f, bounds.center.y, 0);
        _icon.transform.localScale = new Vector3(width, height, 1);
        ApplyIconColor();
    }

    private bool CreateIcon()
    {
        var sprite = GameInterop.LethalCompanySpeakerIcon.Sprite;
        if (sprite == null) return false;
        var icon = new GameObject("Speaking icon", typeof(MeshFilter), typeof(MeshRenderer));
        icon.layer = _gameObject.layer;
        icon.transform.SetParent(_gameObject.transform, false);
        // The glyph's area of its (possibly atlased) texture.
        Vector2 min = sprite.uv[0], max = sprite.uv[0];
        foreach (var uv in sprite.uv) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }
        _iconAspect = sprite.bounds.size.x / sprite.bounds.size.y;
        _iconMesh = new Mesh
        {
            name = "EnhancedSpectator speaking icon",
            vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) },
            uv = new[] { min, new Vector2(max.x, min.y), new Vector2(min.x, max.y), max },
            colors = new[] { Color.white, Color.white, Color.white, Color.white },
            triangles = new[] { 0, 2, 1, 1, 2, 3 }
        };
        _iconMesh.RecalculateBounds();
        icon.GetComponent<MeshFilter>().sharedMesh = _iconMesh;
        _icon = icon.GetComponent<MeshRenderer>();
        _icon.shadowCastingMode = ShadowCastingMode.Off; _icon.receiveShadows = false; _icon.allowOcclusionWhenDynamic = false;
        _icon.sortingOrder = short.MaxValue;
        _iconMaterial = new Material(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf").material) { name = "EnhancedSpectator speaking icon", mainTexture = sprite.texture };
        _icon.sharedMaterial = _iconMaterial;
        return true;
    }

    // The name's colour and fade, times the icon's own fade.
    private void ApplyIconColor()
    {
        if (_iconMaterial != null)
            _iconMaterial.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * _fade * _iconOpacity);
    }

    /// <summary>Configured height of the tag above its marker.</summary>
    public float HeightOffset => _heightOffset;

    /// <summary>Faces the given camera, keeping the current position.</summary>
    public void Face(Camera camera)
    {
        if (_disposed || _gameObject == null) return;
        _gameObject.transform.rotation = Quaternion.LookRotation(camera.transform.rotation * Vector3.forward, camera.transform.rotation * Vector3.up);
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
            if (_icon != null) _icon.gameObject.layer = layer;
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
        if (_iconMesh != null) UnityEngine.Object.Destroy(_iconMesh);
        if (_iconMaterial != null) UnityEngine.Object.Destroy(_iconMaterial);
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
        // Configured while active: TextMeshPro sets itself up in Awake, which an inactive object has not run yet.
        GameObject gameObject = new GameObject("Enhanced Spectator Name Tag");
        gameObject.transform.SetParent(root, false);

        // The game's HUD font with the split-screen label look; the scale keeps the old world size (a name tag scale
        // of .02 gives an em of about 0.13 m).
        TextMeshPro textMesh = gameObject.AddComponent<TextMeshPro>();
        if (GameInterop.LethalCompanyWorldTextFont.Font is { } font) textMesh.font = font;
        if (GameInterop.LethalCompanyWorldTextFont.LabelMaterial is { } label) textMesh.fontSharedMaterial = label;
        textMesh.text = text;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.enableWordWrapping = false;
        textMesh.overflowMode = TextOverflowModes.Overflow;
        textMesh.richText = false;
        textMesh.fontSize = Mathf.Max(0.005f, scale) * 64f;
        textMesh.color = Color.white;
        textMesh.rectTransform.sizeDelta = Vector2.zero;

        MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.allowOcclusionWhenDynamic = false;
        renderer.sortingOrder = short.MaxValue;

        gameObject.SetActive(false);
        return new NameTagVisual(gameObject, textMesh, renderer, color, heightOffset, maxDistance);
    }
}
