using System;
using System.Collections.Generic;
using EnhancedSpectator.Features.SplitScreen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The game's clock centred under the top edge of the large view; tiled, in the top row on the left of the vote strip
/// (the two centred together, sliding as either comes or goes). It fades and drops into place, and fades out where it
/// was; moving between the two places it fades out at one and comes back at the other.
/// </summary>
internal sealed partial class SplitScreenView
{
    private const float ClockFadeSeconds = .25f;
    private RectTransform _clockChip = null!;
    private CanvasGroup _clockGroup = null!;
    private RawImage _clockIcon = null!;
    private TextMeshProUGUI _clockText = null!;
    private readonly Vector3[] _clockCorners = new Vector3[4];
    // The game's clock icons are 1-pixel line art (53x48); drawn at about half that, filtering thins or drops the
    // lines. Each is redrawn once at the height it shows, drawn 1:1 on whole pixels (see IconCopy).
    private readonly Dictionary<(Texture, Rect, int), Texture2D> _iconCopies = new Dictionary<(Texture, Rect, int), Texture2D>();
    private int _iconHeight;
    private SplitScreenKey? _clockView;
    private string? _clockPending, _clockSource;
    private Sprite? _iconPending, _iconSource;
    private int _phasePending = -1, _phaseSource = -1;
    // Below this height the hand-drawn set is drawn (at its own 22 pixels) where it has the phase; otherwise (larger, or
    // the midnight skull) the game's art redrawn.
    private const int HandIconsBelow = 28;
    private bool _clockWanted, _clockDirty = true;
    private float _clockWidth;
    // Tiled, the clock is placed in the top row (the views make room for it); _clockDocked is where it is placed now.
    private bool _clockDocked, _clockInRow;
    // How far each card has taken its place in the top row (0 to 1), and the widths the row is laid out with: both ease,
    // so a card coming, going or changing size (the vote turning into the departure time) slides the other along.
    private float _clockSlot, _stripSlot, _clockSlotWidth, _stripSlotWidth;

    private void CreateClock()
    {
        _clockChip = CreateImage("Clock", _hudRoot, new Color(.02f, .022f, .026f, .72f)).rectTransform;
        _clockChip.anchorMin = _clockChip.anchorMax = _clockChip.pivot = new Vector2(.5f, 1);
        Frame4("Clock edge ", _clockChip, new Color(1, .38f, .02f, .55f), 1);
        _clockGroup = _clockChip.gameObject.AddComponent<CanvasGroup>(); _clockGroup.alpha = 0;
        _clockIcon = new GameObject("Clock icon", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        _clockIcon.transform.SetParent(_clockChip, false); _clockIcon.raycastTarget = false;
        _clockText = CreateText("Clock time", _clockChip); _clockText.alignment = TextAlignmentOptions.MidlineLeft; _clockText.color = new Color(1, .45f, .12f, 1);
        // Clock mods colour parts of the time (LCBetterClock dims the leading zero) with rich-text tags.
        _clockText.richText = true; _clockText.overflowMode = TextOverflowModes.Ellipsis; _clockText.enableWordWrapping = false;
        _clockChip.gameObject.SetActive(false);
        CreateLeaveVote();
    }
    private void ConfigureClockFont() { ApplyFont(_clockText); _clockDirty = true; ConfigureLeaveVoteFont(); }
    private void ResetClockLayout() { _clockDirty = true; ResetLeaveVoteLayout(); }

    /// <summary>
    /// The game's clock centred under the top edge of view <paramref name="view"/>, never wider than it (the text is
    /// cut short instead); null hides it. <paramref name="phase"/> is the day phase its icon shows (0 dawn to 3
    /// midnight), -1 for an icon not the game's.
    /// </summary>
    internal void SetClock(SplitScreenKey? view, string? clock, Sprite? icon, int phase = -1)
    {
        _clockWanted = view.HasValue && clock != null && _tiles.ContainsKey(view.Value);
        if (!_clockWanted) return;
        _clockView = view; _clockPending = clock; _iconPending = icon; _phasePending = phase;
    }

    private void TickClock(float deltaTime)
    {
        TickLeaveVote(deltaTime);
        // Tiled (more than one view, none enlarged) the clock belongs in the top row; otherwise in the view it names.
        // The preview's toolbar takes the top of the screen while it shows, so the clock stays in its view then.
        bool docks = _focused == null && _participants.Count > 1 && !_testVisible, moving = docks != _clockDocked;
        float alpha = Mathf.MoveTowards(_clockGroup.alpha, _clockWanted && !moving ? 1 : 0, deltaTime / ClockFadeSeconds);
        _clockGroup.alpha = alpha;
        if (moving && alpha <= 0) { _clockDocked = docks; moving = false; }
        bool inRow = _clockDocked && _clockWanted && !moving;
        if (inRow != _clockInRow) { _clockInRow = inRow; _layoutDirty = true; }
        _clockSlot = Mathf.MoveTowards(_clockSlot, inRow ? 1 : 0, deltaTime / ClockFadeSeconds);
        _stripSlot = Mathf.MoveTowards(_stripSlot, _stripVisible ? 1 : 0, deltaTime / VoteFadeSeconds);
        Tile? tile = _clockView.HasValue && _tiles.TryGetValue(_clockView.Value, out var found) ? found : null;
        if (tile == null && !_clockDocked) alpha = 0;
        if (_clockChip.gameObject.activeSelf != alpha > 0) _clockChip.gameObject.SetActive(alpha > 0);
        if (alpha > 0 && _clockDocked) LayoutClock(Mathf.Round(_size.x * .4f), Even(26 * _uiScale));
        else if (alpha > 0)
        {
            // The view's top edge in the HUD root (the view slides and scales as the layout animates).
            tile!.Decoration.GetWorldCorners(_clockCorners);
            Vector3 left = _hudRoot.InverseTransformPoint(_clockCorners[1]), right = _hudRoot.InverseTransformPoint(_clockCorners[2]);
            float margin = Mathf.Round(10 * _uiScale), height = Even(30 * _uiScale);
            LayoutClock(right.x - left.x - 2 * margin, height);
            // On whole pixels (with an even width and height, its centre too), so the icon inside it lands on them.
            var at = (left + right) / 2 - new Vector3(0, margin - (1 - SplitScreenResultsPanel.EaseOut(alpha)) * 8 * _uiScale, 0);
            _clockChip.localPosition = new Vector3(Mathf.Round(at.x), Mathf.Round(at.y), 0);
        }
        PlaceTopRow(deltaTime);
    }

    /// <summary>Room the views leave above themselves for the top row (the vote strip, and tiled the clock).</summary>
    private float TopRowReserve => _stripVisible || _clockInRow ? Mathf.Round(32 * _uiScale) : 0;

    // The top row centred as one: the clock on the left, a gap, the vote strip. Each card takes its share of the row as
    // it eases in, so the other slides over rather than jumping; both drop into place as they fade in.
    private void PlaceTopRow(float deltaTime)
    {
        bool clockShown = _clockDocked && _clockChip.gameObject.activeSelf, stripShown = _strip.gameObject.activeSelf;
        if (!clockShown && !stripShown) { _clockSlotWidth = _stripSlotWidth = 0; return; }
        float follow = 1 - Mathf.Exp(-8 * deltaTime);
        if (clockShown) _clockSlotWidth = _clockSlotWidth > 0 ? Mathf.Lerp(_clockSlotWidth, _clockChip.sizeDelta.x, follow) : _clockChip.sizeDelta.x;
        if (stripShown) _stripSlotWidth = _stripSlotWidth > 0 ? Mathf.Lerp(_stripSlotWidth, _strip.sizeDelta.x, follow) : _strip.sizeDelta.x;
        float clockIn = Mathf.SmoothStep(0, 1, _clockSlot), stripIn = Mathf.SmoothStep(0, 1, _stripSlot);
        float clock = _clockSlotWidth * clockIn, strip = _stripSlotWidth * stripIn, gap = Mathf.Round(8 * _uiScale) * Mathf.Min(clockIn, stripIn);
        float left = -(clock + gap + strip) / 2;
        // Alone, a card stays centred in its share; beside the other, it keeps to its own side, so they never overlap.
        float height = Mathf.Max(stripShown ? _strip.sizeDelta.y : 0, clockShown ? _clockChip.sizeDelta.y : 0);
        float top = TopRowTop(height, left, left + clock + gap + strip);
        if (stripShown)
            _strip.anchoredPosition = new Vector2(left + clock + gap + Mathf.Lerp(strip, _stripSlotWidth, clockIn) / 2,
                -(top + (height - _strip.sizeDelta.y) / 2) + (1 - SplitScreenResultsPanel.EaseOut(_stripGroup.alpha)) * 8 * _uiScale);
        // On whole pixels, as in a view (an even width and height put its centre on one too).
        if (clockShown)
            _clockChip.anchoredPosition = new Vector2(Mathf.Round(left + Mathf.Lerp(clock / 2, clock - _clockSlotWidth / 2, stripIn)),
                Mathf.Round(-(top + (height - _clockChip.sizeDelta.y) / 2) + (1 - SplitScreenResultsPanel.EaseOut(_clockGroup.alpha)) * 8 * _uiScale));
    }

    private void LayoutClock(float room, float height)
    {
        float pad = Mathf.Round(8 * _uiScale);
        int iconHeight = Mathf.RoundToInt(22 * _uiScale);
        if (_iconPending != _iconSource || _phasePending != _phaseSource || iconHeight != _iconHeight)
        {
            _iconSource = _iconPending; _phaseSource = _phasePending; _iconHeight = iconHeight;
            _clockIcon.enabled = _iconSource != null;
            if (_iconSource != null)
            {
                _clockIcon.texture = iconHeight < HandIconsBelow && _phaseSource >= 0 && _phaseSource < SpectatorClockIcons.Count
                    ? SpectatorClockIcons.For(_phaseSource) : IconCopy(_iconSource, iconHeight);
                _clockIcon.uvRect = new Rect(0, 0, 1, 1);
            }
            _clockDirty = true;
        }
        float iconWidth = _iconSource != null ? _clockIcon.texture.width : 0;
        if (_iconSource != null) iconHeight = _clockIcon.texture.height;
        if (_clockDirty || _clockPending != _clockSource)
        {
            // The game breaks the time over two lines ("8:19 \n AM"); one line here.
            _clockDirty = false; _clockSource = _clockPending;
            _clockText.text = string.Join(" ", (_clockSource ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            _clockText.fontSize = 18 * _uiScale;
            _clockWidth = Mathf.Ceil(_clockText.GetPreferredValues(_clockText.text).x);
            var iconRect = _clockIcon.rectTransform; iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0, 1);
            iconRect.sizeDelta = new Vector2(iconWidth, iconHeight); iconRect.anchoredPosition = new Vector2(pad, -Mathf.Round((height - iconHeight) / 2));
            var textRect = _clockText.rectTransform; textRect.anchorMin = textRect.anchorMax = textRect.pivot = new Vector2(0, .5f);
            textRect.anchoredPosition = new Vector2(iconWidth > 0 ? pad * 2 + iconWidth : pad, 0);
        }
        float lead = iconWidth > 0 ? pad * 3 + iconWidth : pad * 2;
        float textWidth = Mathf.Clamp(_clockWidth, 0, room - lead);
        _clockChip.sizeDelta = new Vector2(Even(lead + textWidth), height);
        _clockText.rectTransform.sizeDelta = new Vector2(textWidth + 2, height);
    }

    private static float Even(float value) => Mathf.Ceil(value / 2) * 2;

    // The icon redrawn at exactly the height it shows, drawn 1:1: each source pixel goes to the target pixel its centre
    // falls in, which keeps the strongest. A 1-pixel line so stays a solid, connected 1-pixel line wherever it sits,
    // where averaging would thin it or split it across two half-lit pixels. The game's textures cannot be read
    // directly; a render texture reads them.
    private Texture2D IconCopy(Sprite sprite, int height)
    {
        var source = sprite.texture; var rect = sprite.textureRect;
        var key = (source, rect, height);
        if (_iconCopies.TryGetValue(key, out var copy)) return copy;
        int w = Mathf.RoundToInt(rect.width), h = Mathf.RoundToInt(rect.height);
        var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var active = RenderTexture.active;
        Graphics.Blit(source, target);
        RenderTexture.active = target;
        var read = new Texture2D(w, h, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(Mathf.Round(rect.x), Mathf.Round(rect.y), w, h), 0, 0); read.Apply(false);
        RenderTexture.active = active; RenderTexture.ReleaseTemporary(target);
        var pixels = read.GetPixels32(); UnityEngine.Object.Destroy(read);
        int width = Mathf.Max(1, Mathf.RoundToInt(w * (float)height / h));
        var shrunk = new Color32[width * height];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int tx = Mathf.Min(width - 1, (int)((x + .5f) * width / w)), ty = Mathf.Min(height - 1, (int)((y + .5f) * height / h));
                var p = pixels[y * w + x];
                if (p.a > shrunk[ty * width + tx].a) shrunk[ty * width + tx] = p;
            }
        copy = new Texture2D(width, height, TextureFormat.RGBA32, false)
        { name = "EnhancedSpectator Clock Icon", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        copy.SetPixels32(shrunk); copy.Apply(false, true);
        _iconCopies.Add(key, copy);
        return copy;
    }

    private void ClearClock()
    {
        _clockWanted = false; _clockView = null; _clockGroup.alpha = 0; _clockChip.gameObject.SetActive(false);
        _clockDocked = _clockInRow = false; _clockSlot = _stripSlot = _clockSlotWidth = _stripSlotWidth = 0;
        ClearLeaveVote();
    }
    private void DisposeClock()
    {
        foreach (var copy in _iconCopies.Values) UnityEngine.Object.Destroy(copy);
        _iconCopies.Clear();
    }
}
