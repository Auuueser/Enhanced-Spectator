using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The demo's camera move over the whole spectator UI: one scale and offset shared by the split-screen layers and
/// the social panels, each of which keeps its content under a zoom root. A focus rectangle (screen pixels of the
/// unzoomed layout, origin bottom-left) is eased in with a cubic in-out curve; small moves of the focus are followed
/// without restarting the move. Outside the demo everything stays at scale 1.
/// </summary>
internal static class SpectatorDemoZoom
{
    private const float MoveSeconds = .7f, MaxScale = 2.4f, Fill = .78f;
    private static Rect? _focus, _arrived;
    private static float _fromScale = 1, _toScale = 1, _t = 1;
    private static Vector2 _fromOffset, _toOffset;

    internal static float Scale { get; private set; } = 1;
    internal static Vector2 Offset { get; private set; }
    /// <summary>The current focus in zoomed screen pixels, while one is set.</summary>
    internal static Rect? ScreenFocus => _focus is { } f ? new Rect(Offset + f.position * Scale, f.size * Scale) : null;
    /// <summary>The focus being moved to or held (unzoomed), or null for the whole picture.</summary>
    internal static Rect? Focus => _focus;

    /// <summary>Content under a zoom root keeps its unzoomed layout; the root carries the move.</summary>
    internal static RectTransform CreateRoot(Transform parent)
    {
        var root = new GameObject("Zoom", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.pivot = Vector2.zero;
        root.offsetMin = root.offsetMax = Vector2.zero;
        return root;
    }

    internal static void Apply(RectTransform root)
    {
        var scale = new Vector3(Scale, Scale, 1);
        if (root.localScale != scale) root.localScale = scale;
        if (root.anchoredPosition != Offset) root.anchoredPosition = Offset;
    }

    /// <summary>The move has arrived (the focus may still drift a little, which is followed in place).</summary>
    internal static bool Settled => _t >= 1;
    /// <summary>The framing is kept on the last subject while the step's own subject is not drawn yet.</summary>
    internal static bool Held { get; private set; }
    /// <summary>The current move goes to a subject next to (or overlapping) the one it left, like the next row.</summary>
    internal static bool Continuous { get; private set; }

    /// <summary>Where the focus will be drawn once the move arrives, over a screen of <paramref name="area"/> pixels.</summary>
    internal static Rect? FinalScreenFocus(Vector2 area)
    {
        if (_focus is not { } f) return null;
        Target(area, out float scale, out var offset);
        return new Rect(offset + f.position * scale, f.size * scale);
    }

    /// <summary>An unzoomed screen point where it is drawn now.</summary>
    internal static Vector2 ToScreen(Vector2 point) => Offset + point * Scale;

    internal static void SetFocus(Rect? focus, bool held = false) { _focus = focus; Held = held; }

    /// <summary>
    /// Where a laid-out element sits in the unzoomed picture: a zoom root's local space is the unzoomed screen, so
    /// this holds whatever the current zoom. The element's own scale is left out (cards that pop or unfold are framed
    /// at their laid-out size, so the move does not chase the animation). Null for a missing or hidden element.
    /// </summary>
    internal static Rect? Measure(RectTransform root, RectTransform? rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return null;
        Rect box = rect.rect; Vector3 at = rect.localPosition;
        Vector2 min = root.InverseTransformPoint(rect.parent.TransformPoint(at + (Vector3)box.min)),
            max = root.InverseTransformPoint(rect.parent.TransformPoint(at + (Vector3)box.max));
        return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
    }
    internal static Rect? Union(Rect? a, Rect? b)
        => a is not { } x ? b : b is not { } y ? a : Rect.MinMaxRect(Mathf.Min(x.xMin, y.xMin), Mathf.Min(x.yMin, y.yMin), Mathf.Max(x.xMax, y.xMax), Mathf.Max(x.yMax, y.yMax));
    internal static Rect? Grow(Rect? rect, float by) => rect is { } r ? Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by) : null;

    /// <summary>Back to the whole picture at once (demo end, preview exit).</summary>
    internal static void Reset()
    {
        _focus = _arrived = null; Held = Continuous = false;
        Scale = _fromScale = _toScale = 1; Offset = _fromOffset = _toOffset = Vector2.zero; _t = 1;
    }

    /// <summary>Advances the move towards the focus over a screen of <paramref name="area"/> pixels.</summary>
    internal static void Tick(float deltaTime, Vector2 area)
    {
        Target(area, out float scale, out var offset);
        // A new subject restarts the eased move; a subject that only shifts a little is followed in place.
        if (Mathf.Abs(scale - _toScale) > .03f * _toScale || (offset - _toOffset).magnitude > 24)
        { _fromScale = Scale; _fromOffset = Offset; _t = 0; Continuous = _arrived is { } from && _focus is { } to && Next(from, to); }
        _toScale = scale; _toOffset = offset;
        _t = Mathf.Min(1, _t + deltaTime / MoveSeconds);
        if (_t >= 1) _arrived = _focus;
        float e = _t < .5f ? 4 * _t * _t * _t : 1 - Mathf.Pow(-2 * _t + 2, 3) / 2;
        Scale = Mathf.LerpUnclamped(_fromScale, _toScale, e); Offset = Vector2.LerpUnclamped(_fromOffset, _toOffset, e);
    }

    // Next to each other: they touch once each is grown by a tenth of its size (adjacent rows, a label and its list).
    private static bool Next(Rect a, Rect b)
    {
        float ga = Mathf.Max(a.width, a.height) * .1f, gb = Mathf.Max(b.width, b.height) * .1f;
        return Rect.MinMaxRect(a.xMin - ga, a.yMin - ga, a.xMax + ga, a.yMax + ga).Overlaps(Rect.MinMaxRect(b.xMin - gb, b.yMin - gb, b.xMax + gb, b.yMax + gb));
    }

    private static void Target(Vector2 area, out float scale, out Vector2 offset)
    {
        scale = 1; offset = Vector2.zero;
        if (_focus is not { } focus || focus.width < 1 || focus.height < 1) return;
        scale = Mathf.Clamp(Mathf.Min(area.x * Fill / focus.width, area.y * Fill / focus.height), 1, MaxScale);
        offset = area * .5f - focus.center * scale;
        // Never past the picture's edges: an edge subject is enlarged in place instead of leaving empty space.
        offset.x = Mathf.Clamp(offset.x, area.x * (1 - scale), 0); offset.y = Mathf.Clamp(offset.y, area.y * (1 - scale), 0);
    }
}
