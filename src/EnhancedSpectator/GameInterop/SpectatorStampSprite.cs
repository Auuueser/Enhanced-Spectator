using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// A rubber-stamp frame: a heavy outer line and a thin inner line with rounded corners, drawn once at twice the
/// on-screen resolution with anti-aliased edges and transparent padding, and used as a 9-sliced sprite so a tilted
/// stamp stays smooth instead of showing stair-stepped lines. White; the Image colour tints it.
/// </summary>
internal static class SpectatorStampSprite
{
    // Texture pixels per on-screen pixel at UI scale 1; Image.pixelsPerUnitMultiplier = Supersample / uiScale.
    internal const float Supersample = 2;
    private const int Size = 64, Padding = 4, Slice = 22;
    private const float Radius = 8, OuterWidth = 5, Gap = 4, InnerWidth = 2;
    private static Sprite? _sprite;

    internal static Sprite Get()
    {
        if (_sprite != null) return _sprite;
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "EnhancedSpectator stamp" };
        var pixels = new Color32[Size * Size];
        float half = Size * .5f - Padding;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // Signed distance to the rounded outer edge: 0 on it, negative inside.
                float px = Mathf.Abs(x + .5f - Size * .5f) - (half - Radius), py = Mathf.Abs(y + .5f - Size * .5f) - (half - Radius);
                float d = new Vector2(Mathf.Max(px, 0), Mathf.Max(py, 0)).magnitude + Mathf.Min(Mathf.Max(px, py), 0) - Radius;
                float outer = Ring(d, -OuterWidth * .5f, OuterWidth * .5f), inner = Ring(d, -OuterWidth - Gap - InnerWidth * .5f, InnerWidth * .5f);
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255 * Mathf.Max(outer, inner)));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        _sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(Slice, Slice, Slice, Slice));
        _sprite.name = texture.name;
        return _sprite;
    }

    // Coverage of a line centred at `centre` with half-width `halfWidth`, softened over one texture pixel.
    private static float Ring(float distance, float centre, float halfWidth) => Mathf.Clamp01(halfWidth - Mathf.Abs(distance - centre) + .5f);
}
