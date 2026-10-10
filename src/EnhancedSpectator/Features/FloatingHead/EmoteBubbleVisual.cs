using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.Features.FloatingHead;

/// <summary>
/// A world-space emote above a ghost head: dark text on a white rounded card (the split-screen audience's look),
/// which pops in with a small overshoot, rises a little and fades. The card is sized to the text every frame.
/// HDRP draws world text with the built-in text material but not sprites, so the card is a nine-slice mesh drawn
/// with a copy of the text's own material, whose texture is a rounded white square (only its alpha is used).
/// </summary>
internal sealed class EmoteBubbleVisual : IDisposable
{
    internal const float Seconds = 2.6f;
    private const float Pop = .25f, Fade = .4f;
    // Corner radius and padding as parts of the text's height.
    private const float Radius = .42f, PadX = .55f, PadY = .3f;
    private const int CornerPixels = 24, TextureSize = CornerPixels * 2 + 2;
    private static readonly Color Text = new Color(.12f, .1f, .08f, 1), Card = new Color(1, 1, 1, .94f);
    private static Texture2D? _rounded;
    private readonly GameObject _root;
    private readonly TextMesh _text;
    private readonly MeshRenderer _textRenderer, _cardRenderer;
    private readonly Mesh _cardMesh;
    private Material? _cardMaterial;
    private readonly float _size;
    private float _shownAt = -100;
    private Vector4 _cardShape;

    internal bool Active => Time.unscaledTime - _shownAt < Seconds;
    /// <summary>
    /// Space between a name tag's centre and the bubble's bottom: grows with the size (0.14 m at the default name
    /// tag scale), so a larger name tag never runs into the bubble above it.
    /// </summary>
    internal float Clearance => _size * 3f;

    internal EmoteBubbleVisual(float characterSize)
    {
        _size = characterSize;
        _root = new GameObject("Enhanced Spectator Emote");
        _text = CreateText(_root.transform);
        _textRenderer = _text.GetComponent<MeshRenderer>();
        var card = new GameObject("Emote card", typeof(MeshFilter), typeof(MeshRenderer));
        card.transform.SetParent(_root.transform, false);
        _cardMesh = new Mesh { name = "EnhancedSpectator emote card" };
        card.GetComponent<MeshFilter>().sharedMesh = _cardMesh;
        _cardRenderer = card.GetComponent<MeshRenderer>();
        _cardRenderer.shadowCastingMode = ShadowCastingMode.Off; _cardRenderer.receiveShadows = false;
        // Drawn just before the text it sits behind.
        _cardRenderer.sortingOrder = short.MaxValue - 1;
        _root.SetActive(false);
    }

    // Activated by the next Update, once it has a position.
    internal void Show(string text) { _text.text = text; _shownAt = Time.unscaledTime; }

    internal void SetLayer(int layer) { _root.layer = layer; _text.gameObject.layer = layer; _cardRenderer.gameObject.layer = layer; }

    internal void Update(Vector3 anchor, Camera? camera)
    {
        float age = Time.unscaledTime - _shownAt;
        // The HUD visibility toggle (F2) hides emotes with the name tags.
        if (age >= Seconds || GameInterop.LethalCompanySpectatorUiVisibility.Hidden) { if (_root.activeSelf) _root.SetActive(false); return; }
        float t = Mathf.Clamp01(age / Pop);
        float scale = age < Pop ? Mathf.LerpUnclamped(.55f, 1, 1 + 2.2f * Mathf.Pow(t - 1, 3) + 1.2f * Mathf.Pow(t - 1, 2)) : 1;
        float alpha = Mathf.Clamp01((Seconds - age) / Fade);
        _text.color = new Color(Text.r, Text.g, Text.b, alpha);
        LayoutCard(alpha);
        _root.transform.localScale = Vector3.one * scale;
        _root.transform.position = anchor + Vector3.up * Mathf.Min(age, .5f) * .12f;
        if (camera != null) Face(camera);
        if (!_root.activeSelf) _root.SetActive(true);
    }

    internal void Face(Camera camera)
        => _root.transform.rotation = Quaternion.LookRotation(camera.transform.rotation * Vector3.forward, camera.transform.rotation * Vector3.up);

    // The card wraps the text's laid-out bounds; until the text mesh (and its material) exist it stays hidden.
    private void LayoutCard(float alpha)
    {
        var bounds = _textRenderer.localBounds;
        bool ready = bounds.size.x > 0 && bounds.size.y > 0 && _textRenderer.sharedMaterial != null;
        _cardRenderer.enabled = ready;
        if (!ready) return;
        if (_cardMaterial == null)
        {
            _cardMaterial = new Material(_textRenderer.sharedMaterial) { name = "EnhancedSpectator emote card", mainTexture = Rounded() };
            _cardRenderer.sharedMaterial = _cardMaterial;
        }
        _cardMaterial.color = new Color(Card.r, Card.g, Card.b, Card.a * alpha);
        float height = bounds.size.y;
        var shape = new Vector4(bounds.center.x, bounds.center.y, bounds.size.x + height * PadX * 2, height * (1 + PadY * 2));
        if (shape == _cardShape) return;
        _cardShape = shape;
        BuildCard(shape.x, shape.y, shape.z, shape.w, height * Radius);
    }

    // Nine quads: fixed rounded corners, stretched edges and centre.
    private void BuildCard(float centerX, float centerY, float width, float height, float radius)
    {
        radius = Mathf.Min(radius, width * .5f, height * .5f);
        float[] xs = { -width * .5f, -width * .5f + radius, width * .5f - radius, width * .5f };
        float[] ys = { -height * .5f, -height * .5f + radius, height * .5f - radius, height * .5f };
        float c = CornerPixels / (float)TextureSize;
        float[] uv = { 0, c, 1 - c, 1 };
        var vertices = new Vector3[16]; var uvs = new Vector2[16]; var colors = new Color[16];
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                vertices[y * 4 + x] = new Vector3(centerX + xs[x], centerY + ys[y], .006f);
                uvs[y * 4 + x] = new Vector2(uv[x], uv[y]); colors[y * 4 + x] = Color.white;
            }
        var triangles = new int[54]; int n = 0;
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                int i = y * 4 + x;
                triangles[n++] = i; triangles[n++] = i + 4; triangles[n++] = i + 1;
                triangles[n++] = i + 1; triangles[n++] = i + 4; triangles[n++] = i + 5;
            }
        _cardMesh.Clear(); _cardMesh.vertices = vertices; _cardMesh.uv = uvs; _cardMesh.colors = colors; _cardMesh.triangles = triangles;
        _cardMesh.RecalculateBounds();
    }

    private TextMesh CreateText(Transform parent)
    {
        var gameObject = new GameObject("Emote text");
        gameObject.transform.SetParent(parent, false);
        var text = gameObject.AddComponent<TextMesh>();
        text.anchor = TextAnchor.LowerCenter; text.alignment = TextAlignment.Center; text.fontSize = 64;
        text.characterSize = _size; text.richText = false; text.color = Text;
        var renderer = gameObject.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.sortingOrder = short.MaxValue;
        return text;
    }

    // A white rounded square with anti-aliased corners; the nine-slice mesh stretches it without bending them.
    private static Texture2D Rounded()
    {
        if (_rounded != null) return _rounded;
        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "EnhancedSpectator emote card" };
        var pixels = new Color32[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                // Distance outside the rounded square of corner radius CornerPixels.
                float dx = Mathf.Max(0, Mathf.Abs(x + .5f - TextureSize * .5f) - (TextureSize * .5f - CornerPixels));
                float dy = Mathf.Max(0, Mathf.Abs(y + .5f - TextureSize * .5f) - (TextureSize * .5f - CornerPixels));
                float outside = Mathf.Sqrt(dx * dx + dy * dy) - CornerPixels;
                pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(.5f - outside)));
            }
        texture.SetPixels32(pixels); texture.Apply(false, true);
        return _rounded = texture;
    }

    internal void Hide() { if (_root.activeSelf) _root.SetActive(false); }

    public void Dispose()
    {
        UnityEngine.Object.Destroy(_root); UnityEngine.Object.Destroy(_cardMesh);
        if (_cardMaterial != null) UnityEngine.Object.Destroy(_cardMaterial);
    }
}
