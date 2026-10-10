using System;
using System.Collections.Generic;
using EnhancedSpectator.Features.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Two views: the rating panel where dead players tag living teammates (praise in green, teasing in orange, two per
/// person), and the end-of-round summary where each rated player's card slides in, tag counts pop up one by one and
/// the overall verdict lands like a stamp. Both hold up to a full 32-player lobby: the panel's rows scroll under its
/// header, and the summary turns compact for large crews and scrolls along as its rows unfold.
/// </summary>
internal sealed class SpectatorRatingPanel : IDisposable
{
    private const float MinimumSummarySeconds = 16f, UnfoldBudget = 4f, CompactAbove = 12;
    private static readonly Color PanelColor = new Color(.03f, .032f, .037f, .95f), CardColor = new Color(.07f, .075f, .085f, 1),
        Chip = new Color(.12f, .115f, .11f, 1), Praise = new Color(.26f, .34f, .22f, 1), Tease = new Color(.46f, .14f, .11f, 1),
        PraiseText = new Color(.74f, .8f, .66f, 1), TeaseText = new Color(.92f, .62f, .55f, 1),
        TextColor = new Color(.9f, .88f, .84f, 1), Muted = new Color(.6f, .62f, .64f, 1), Accent = new Color(.96f, .55f, .2f, 1),
        Stamp = new Color(.82f, .16f, .12f, 1), StampGood = new Color(.8f, .78f, .68f, 1);
    private readonly Canvas _canvas;
    private readonly RectTransform _zoom, _panel, _summary, _summaryHeader;
    // Each colleague's row from the last render, for the demo to point at.
    private readonly List<RectTransform> _rowFills = new List<RectTransform>();
    private RectTransform? _summaryTitle;
    private readonly CanvasGroup _panelGroup, _summaryGroup;
    private readonly SpectatorScrollArea _rows, _summaryCards;
    private readonly List<Image> _images = new List<Image>();
    private readonly List<TextMeshProUGUI> _texts = new List<TextMeshProUGUI>();
    private readonly List<(RectTransform Rect, ulong Target, int Tag)> _chips = new List<(RectTransform, ulong, int)>();
    private readonly List<Color> _chipColors = new List<Color>();
    private readonly List<SummaryCard> _cards = new List<SummaryCard>();
    private TMP_FontAsset? _font;
    private Material? _material;
    private Transform _layer;
    private int _usedImages, _usedTexts, _flash = -1;
    private float _open, _summaryAge = 99, _summarySeconds = MinimumSummarySeconds, _flashAge = 1, _ui = 1, _sinceRender;
    private bool _dirty = true, _renderedCursor;
    private readonly List<(ulong ClientId, string Name, bool Alive)> _targets = new List<(ulong, string, bool)>(32);
    internal bool IsOpen { get; private set; }
    internal Canvas Canvas => _canvas;
    /// <summary>Screen height taken at the top (the preview toolbar); the panel and summary stay below it.</summary>
    internal float TopInset { get; set; }
    private Camera? Eye => _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
    // The pixel size the canvas covers: the screen, or its camera's target when drawn through a camera.
    private Vector2 Area => Eye is { } eye ? new Vector2(eye.pixelWidth, eye.pixelHeight) : new Vector2(Screen.width, Screen.height);

    private sealed class SummaryCard
    {
        internal RectTransform Root = null!, Stamp = null!;
        internal readonly List<RectTransform> Tags = new List<RectTransform>();
        internal float Delay, Bottom;
        internal bool Revealed;
    }

    internal SpectatorRatingPanel()
    {
        _canvas = SplitScreenView.CreateCanvas("EnhancedSpectator Ratings", 32008);
        _zoom = SpectatorDemoZoom.CreateRoot(_canvas.transform);
        _panel = SplitScreenView.CreateImage("Rating panel", _zoom, PanelColor).rectTransform;
        _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(.5f, .5f);
        SplitScreenView.Frame4("Rating edge ", _panel, new Color(Accent.r, Accent.g, Accent.b, .45f), 1);
        _panelGroup = _panel.gameObject.AddComponent<CanvasGroup>();
        _rows = new SpectatorScrollArea(_panel, "Rating rows", new Color(Accent.r, Accent.g, Accent.b, .6f));
        _layer = _panel;
        _summary = new GameObject("Rating summary", typeof(RectTransform)).GetComponent<RectTransform>();
        _summary.SetParent(_zoom, false); SplitScreenView.Stretch(_summary);
        _summaryGroup = _summary.gameObject.AddComponent<CanvasGroup>();
        // A dim sheet behind the notice keeps it readable over any scene.
        SplitScreenView.Stretch(SplitScreenView.CreateImage("Notice backdrop", _summary, new Color(0, 0, 0, .55f)).rectTransform);
        _summaryHeader = new GameObject("Notice header", typeof(RectTransform)).GetComponent<RectTransform>();
        _summaryHeader.SetParent(_summary, false); SplitScreenView.Stretch(_summaryHeader);
        _summaryCards = new SpectatorScrollArea(_summary, "Review cards", new Color(Accent.r, Accent.g, Accent.b, .6f));
        _panel.gameObject.SetActive(false); _summary.gameObject.SetActive(false); _canvas.gameObject.SetActive(false);
    }

    internal void ConfigureFont(TMP_FontAsset? font)
    {
        if (font == null || font == _font) return;
        if (_material != null) UnityEngine.Object.Destroy(_material);
        _font = font; _material = SpectatorTextStyle.CreateLabelMaterial(font);
        foreach (var text in _canvas.GetComponentsInChildren<TextMeshProUGUI>(true)) Apply(text);
    }

    internal void Toggle() { IsOpen = !IsOpen; _dirty = true; if (IsOpen) _rows.Reset(); }
    internal void Close() => IsOpen = false;
    internal void Refresh() => _dirty = true;
    internal bool Contains(Vector2 point) => IsOpen && RectTransformUtility.RectangleContainsScreenPoint(_panel, point, Eye);
    internal bool SummaryShowing => _summaryAge < _summarySeconds;
    internal void DismissSummary() { if (_summaryAge < _summarySeconds - .5f) _summaryAge = _summarySeconds - .5f; }

    // ---- For the demo: where things are in the unzoomed picture. ----
    internal Rect? FocusPanel() => IsOpen ? SpectatorDemoZoom.Measure(_zoom, _panel) : null;
    internal Rect? FocusRow(int row) => IsOpen && row < _rowFills.Count ? SpectatorDemoZoom.Measure(_zoom, _rowFills[row]) : null;
    internal Rect? FocusChip(int row, int tag)
    {
        if (!IsOpen || row >= _targets.Count) return null;
        foreach (var chip in _chips) if (chip.Target == _targets[row].ClientId && chip.Tag == tag) return SpectatorDemoZoom.Measure(_zoom, chip.Rect);
        return null;
    }
    /// <summary>The notice's title with its first <paramref name="cards"/> cards.</summary>
    internal Rect? FocusSummary(int cards)
    {
        if (!SummaryShowing) return null;
        var rect = SpectatorDemoZoom.Measure(_zoom, _summaryTitle);
        for (int i = 0; i < cards && i < _cards.Count; i++) rect = SpectatorDemoZoom.Union(rect, SpectatorDemoZoom.Measure(_zoom, _cards[i].Root));
        return rect;
    }

    /// <summary>Mouse wheel: over the open panel it scrolls the rows; while the summary shows it scrolls the cards.</summary>
    internal void Scroll(Vector2 point, float notches)
    {
        if (Contains(point)) _rows.Scroll(notches);
        else if (SummaryShowing) _summaryCards.Scroll(notches);
    }

    /// <summary>The chip under the pointer: target and tag, or false.</summary>
    internal bool HitTest(Vector2 point, out ulong target, out int tag)
    {
        target = 0; tag = -1;
        if (!IsOpen || !_rows.Contains(point, Eye)) return false;
        for (int i = 0; i < _chips.Count; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(_chips[i].Rect, point, Eye))
            { target = _chips[i].Target; tag = _chips[i].Tag; _flash = i; _flashAge = 0; return true; }
        return false;
    }

    // mine: the local player's own choices this round; copyTargets fills the teammates who can be rated now.
    internal void Tick(float deltaTime, RatingState mine, ulong local, bool canRate, Action<List<(ulong ClientId, string Name, bool Alive)>> copyTargets,
        bool chinese, Vector2? pointer)
    {
        bool cursor = pointer.HasValue;
        _ui = SpectatorTextStyle.UiScale(Area);
        SpectatorDemoZoom.Apply(_zoom);
        _open = Mathf.MoveTowards(_open, IsOpen ? 1 : 0, deltaTime / .18f);
        _summaryAge += deltaTime;
        bool active = _open > 0 || _summaryAge < _summarySeconds;
        if (_canvas.gameObject.activeSelf != active) _canvas.gameObject.SetActive(active);
        _panel.gameObject.SetActive(_open > 0);
        if (_open > 0)
        {
            float eased = 1 - (1 - _open) * (1 - _open);
            _panelGroup.alpha = eased; _panel.localScale = Vector3.one * (.96f + .04f * eased);
            // Redrawn on change, and each second so teammates who die drop off the list.
            _sinceRender += deltaTime;
            if (_dirty || cursor != _renderedCursor || _sinceRender >= 1) Render(mine, local, canRate, copyTargets, chinese, cursor);
            _rows.Tick(deltaTime);
            AnimateChips(deltaTime, pointer);
        }
        AnimateSummary(deltaTime);
    }

    private void Render(RatingState mine, ulong local, bool canRate, Action<List<(ulong ClientId, string Name, bool Alive)>> copyTargets, bool chinese, bool cursor)
    {
        _dirty = false; _sinceRender = 0; _renderedCursor = cursor; _usedImages = _usedTexts = 0; _chips.Clear(); _chipColors.Clear(); _layer = _panel; _rowFills.Clear();
        float u = _ui, pad = Mathf.Round(12 * u), width = Mathf.Round(880 * u), font = Mathf.Round(13 * u), rowHeight = Mathf.Round(70 * u);
        float y = pad;
        var targets = _targets; targets.Clear();
        copyTargets(targets);
        Text(chinese ? "同事绩效评估" : "Colleague review", pad, y, width - pad * 2, Mathf.Round(22 * u), Mathf.Round(17 * u), Accent);
        Text((chinese ? $"{targets.Count} 位同事 · 每人最多两条 · 回到轨道后由公司公布" : $"{targets.Count} colleagues · two each · read out in orbit"),
            pad, y, width - pad * 2, Mathf.Round(22 * u), Mathf.Round(12 * u), Muted, TextAlignmentOptions.MidlineRight);
        y += Mathf.Round(30 * u);
        if (!canRate || targets.Count == 0)
        {
            Text(chinese ? "目前没有还活着的同事可供评估" : "No living colleagues left to review", pad, y, width - pad * 2, Mathf.Round(26 * u), font, Muted);
            y += Mathf.Round(32 * u);
        }
        if (!cursor) { Text(chinese ? "按 P 显示光标后点击标签" : "Press P for the pointer, then click a tag", pad, y, width - pad * 2, Mathf.Round(20 * u), Mathf.Round(11 * u), Muted); y += Mathf.Round(24 * u); }
        float header = y, rowWidth = width - pad * 2;
        _layer = _rows.Content; y = 0;
        // Praise on the upper line, teasing on the lower, four chips each.
        float gap = Mathf.Round(4 * u), chipHeight = Mathf.Round(26 * u), nameWidth = Mathf.Round(150 * u);
        float chipWidth = Mathf.Floor((rowWidth - nameWidth - gap * 3 - Mathf.Round(6 * u)) / RatingState.PositiveCount);
        foreach (var target in targets)
        {
            _rowFills.Add(Fill(0, y, rowWidth, rowHeight - Mathf.Round(6 * u), CardColor).rectTransform);
            int given = mine.GivenTo(local, target.ClientId);
            Text(target.Name, Mathf.Round(8 * u), y + Mathf.Round(10 * u), nameWidth - Mathf.Round(10 * u), Mathf.Round(22 * u), Mathf.Round(14 * u), TextColor);
            Text($"{given}/{RatingState.PerTarget}", Mathf.Round(8 * u), y + Mathf.Round(34 * u), nameWidth - Mathf.Round(10 * u), Mathf.Round(18 * u), Mathf.Round(11 * u), given > 0 ? Accent : Muted);
            for (int tag = 0; tag < RatingState.TagCount; tag++)
            {
                int line = tag / RatingState.PositiveCount, column = tag % RatingState.PositiveCount;
                float x = nameWidth + column * (chipWidth + gap), top = y + Mathf.Round(6 * u) + line * (chipHeight + gap);
                bool on = mine.Has(local, target.ClientId, tag), full = given >= RatingState.PerTarget && !on;
                var color = on ? (RatingState.Positive(tag) ? Praise : Tease) : full ? new Color(.08f, .08f, .085f, 1) : Chip;
                var chip = Fill(x, top, chipWidth, chipHeight, color);
                Text(RatingState.Tag(tag, chinese), x, top, chipWidth, chipHeight, Mathf.Round(12 * u),
                    full ? Muted : on ? Color.white : RatingState.Positive(tag) ? PraiseText : TeaseText, TextAlignmentOptions.Center);
                if (!full) { _chips.Add((chip.rectTransform, target.ClientId, tag)); _chipColors.Add(color); }
            }
            y += rowHeight;
        }
        // Up to most of the screen below any top inset; more colleagues scroll.
        float view = Mathf.Min(y, Mathf.Max(rowHeight * 2, (Area.y - TopInset) * .82f - header - pad));
        _rows.Layout(pad, header, rowWidth, view, y, u);
        _panel.sizeDelta = new Vector2(width, header + view + pad);
        _panel.anchoredPosition = new Vector2(0, -TopInset * .5f);
        for (int i = _usedImages; i < _images.Count; i++) _images[i].gameObject.SetActive(false);
        for (int i = _usedTexts; i < _texts.Count; i++) _texts[i].gameObject.SetActive(false);
    }

    // Hover lightens a chip; a click pops it before the panel redraws with the new choice.
    private void AnimateChips(float deltaTime, Vector2? pointer)
    {
        int hovered = -1;
        if (pointer is { } point && _rows.Contains(point, Eye))
            for (int i = 0; i < _chips.Count; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(_chips[i].Rect, point, Eye)) { hovered = i; break; }
        _flashAge += deltaTime;
        for (int i = 0; i < _chips.Count; i++)
        {
            float flash = i == _flash ? Mathf.Clamp01(1 - _flashAge / .25f) : 0;
            var image = _chips[i].Rect.GetComponent<Image>();
            var color = Color.Lerp(_chipColors[i] + (i == hovered ? new Color(.08f, .07f, .05f, 0) : Color.clear), Color.white, flash * .5f);
            if (image.color != color) image.color = color;
            float scale = 1 + .1f * flash;
            _chips[i].Rect.localScale = new Vector3(scale, scale, 1);
        }
    }

    /// <summary>
    /// Shows the end-of-round summary. Up to 12 people get full cards (three tags); more get compact cards (two tags)
    /// so a full lobby usually fits one screen; anything beyond scrolls along as the rows unfold.
    /// </summary>
    internal void ShowSummary(List<(ulong Target, int[] Counts)> summary, Func<ulong, string> name, bool chinese)
    {
        IsOpen = false;
        _cards.Clear();
        Clear(_summaryHeader); Clear(_summaryCards.Content);
        float u = _ui = SpectatorTextStyle.UiScale(Area);
        bool compact = summary.Count > CompactAbove;
        int shownTags = compact ? 2 : 3;
        float cardWidth = Mathf.Round((compact ? 210 : 250) * u), gap = Mathf.Round(12 * u);
        // Room under the tags for the tilted stamp, whose corners swing about 11 px below its own edge.
        float cardHeight = Mathf.Round(10 * u) * 2 + Mathf.Round(30 * u) + shownTags * Mathf.Round(28 * u) + Mathf.Round(50 * u);
        float margin = Mathf.Round(40 * u), headerHeight = Mathf.Round(62 * u), bottom = Mathf.Round(24 * u);
        float areaWidth = Area.x - margin * 2;
        int perRow = Mathf.Max(1, Mathf.Min(summary.Count, Mathf.FloorToInt((areaWidth + gap) / (cardWidth + gap))));
        int rows = (summary.Count + perRow - 1) / perRow;
        float content = rows * (cardHeight + gap) - gap;
        float available = Area.y - TopInset - headerHeight - bottom - Mathf.Round(24 * u);
        float view = Mathf.Min(content, available);
        float top = TopInset + Mathf.Max(Mathf.Round(24 * u), Mathf.Round((Area.y - TopInset - headerHeight - view) * .5f));
        var title = SummaryText(_summaryHeader, chinese ? "本日绩效通报" : "Today's performance notice", Mathf.Round(24 * u), Accent);
        title.alignment = TextAlignmentOptions.Center;
        var note = SummaryText(_summaryHeader, chinese ? "以下评语由已故同事提供 · 公司感谢各位的牺牲" : "Feedback provided by deceased colleagues · The Company thanks you for your sacrifice",
            Mathf.Round(12 * u), Muted);
        note.alignment = TextAlignmentOptions.Center;
        PlaceCentered(title.rectTransform, 0, top, Mathf.Round(600 * u), Mathf.Round(36 * u));
        _summaryTitle = title.rectTransform;
        PlaceCentered(note.rectTransform, 0, top + Mathf.Round(32 * u), Mathf.Round(900 * u), Mathf.Round(18 * u));
        _summaryCards.Layout(margin, top + headerHeight, areaWidth, view, content, u);
        _summaryCards.Reset();
        // Every card is out within a few seconds however many there are.
        float stagger = Mathf.Min(.45f, UnfoldBudget / Mathf.Max(1, summary.Count));
        for (int i = 0; i < summary.Count; i++)
        {
            int row = i / perRow, column = i % perRow, inRow = Mathf.Min(perRow, summary.Count - row * perRow);
            float rowWidth = inRow * (cardWidth + gap) - gap;
            float x = (areaWidth - rowWidth) * .5f + column * (cardWidth + gap), y = row * (cardHeight + gap);
            _cards.Add(BuildCard(summary[i].Target, summary[i].Counts, name, chinese, x, y, cardWidth, cardHeight, shownTags, .3f + i * stagger));
        }
        float unfolded = .3f + summary.Count * stagger + 1.5f;
        _summarySeconds = Mathf.Max(MinimumSummarySeconds, unfolded + 8 + (content - view) / (120 * u));
        _summaryAge = 0; _summary.gameObject.SetActive(true); _canvas.gameObject.SetActive(true);
    }

    private SummaryCard BuildCard(ulong target, int[] counts, Func<ulong, string> name, bool chinese, float x, float y, float width, float height, int shownTags, float delay)
    {
        float u = _ui, pad = Mathf.Round(10 * u);
        var card = new SummaryCard { Delay = delay, Bottom = y + height };
        var root = SplitScreenView.CreateImage("Review card", _summaryCards.Content, CardColor);
        card.Root = root.rectTransform;
        Place(card.Root, x, y, width, height);
        // Unfolds from its middle.
        card.Root.pivot = new Vector2(0, .5f); card.Root.anchoredPosition = new Vector2(x, -y - height * .5f);
        SplitScreenView.Frame4("Review edge ", card.Root, new Color(Accent.r, Accent.g, Accent.b, .4f), 1);
        var who = SummaryText(card.Root, name(target), Mathf.Round(16 * u), TextColor); Place(who.rectTransform, pad, pad, width - pad * 2, Mathf.Round(24 * u));
        string verdict = RatingState.Verdict(counts, chinese, out _);
        int praise = 0, tease = 0;
        for (int tag = 0; tag < counts.Length; tag++) if (RatingState.Positive(tag)) praise += counts[tag]; else tease += counts[tag];
        bool good = praise >= tease;
        // The most given tags, each a chip with its count.
        var order = new List<int>();
        for (int tag = 0; tag < counts.Length; tag++) if (counts[tag] > 0) order.Add(tag);
        order.Sort((a, b) => counts[b].CompareTo(counts[a]));
        float ty = pad + Mathf.Round(30 * u);
        for (int i = 0; i < order.Count && i < shownTags; i++)
        {
            int tag = order[i];
            var chip = SplitScreenView.CreateImage("Review tag", card.Root, RatingState.Positive(tag) ? Praise : Tease).rectTransform;
            Place(chip, pad, ty, width - pad * 2, Mathf.Round(24 * u));
            var label = SummaryText(chip, RatingState.Tag(tag, chinese), Mathf.Round(13 * u), Color.white); Place(label.rectTransform, Mathf.Round(8 * u), 0, width - pad * 4, Mathf.Round(24 * u));
            var count = SummaryText(chip, "×" + counts[tag], Mathf.Round(13 * u), Color.white); count.alignment = TextAlignmentOptions.MidlineRight;
            Place(count.rectTransform, 0, 0, width - pad * 2 - Mathf.Round(8 * u), Mathf.Round(24 * u));
            chip.localScale = Vector3.zero;
            card.Tags.Add(chip);
            ty += Mathf.Round(28 * u);
        }
        var stampImage = SplitScreenView.CreateImage("Verdict stamp", card.Root, good ? StampGood : Stamp);
        stampImage.sprite = SpectatorStampSprite.Get(); stampImage.type = Image.Type.Sliced;
        stampImage.pixelsPerUnitMultiplier = SpectatorStampSprite.Supersample / u;
        var stamp = stampImage.rectTransform;
        var stampText = SummaryText(stamp, verdict, Mathf.Round(16 * u), good ? StampGood : Stamp);
        stampText.alignment = TextAlignmentOptions.Center; SplitScreenView.Stretch(stampText.rectTransform);
        stamp.anchorMin = stamp.anchorMax = stamp.pivot = new Vector2(.5f, 0);
        stamp.anchoredPosition = new Vector2(0, pad + Mathf.Round(10 * u)); stamp.sizeDelta = new Vector2(width * .86f, Mathf.Round(32 * u));
        stamp.localScale = Vector3.zero;
        card.Stamp = stamp;
        card.Root.localScale = new Vector3(1, 0, 1);
        return card;
    }

    // Cards unfold one after another, tag chips pop in turn, then the verdict stamp drops in with a little shake.
    // When the cards outgrow the screen the view glides down to each newly unfolding row.
    private void AnimateSummary(float deltaTime)
    {
        if (!_summary.gameObject.activeSelf) return;
        if (_summaryAge >= _summarySeconds) { _summary.gameObject.SetActive(false); return; }
        _summaryGroup.alpha = Mathf.Clamp01(_summaryAge / .3f) * Mathf.Clamp01((_summarySeconds - _summaryAge) / .6f);
        foreach (var card in _cards)
        {
            float t = _summaryAge - card.Delay;
            // Each card brings itself into view once, as it unfolds; after that the wheel is the reader's.
            if (t >= 0 && !card.Revealed) { card.Revealed = true; _summaryCards.Reveal(card.Bottom); }
            float unfold = Mathf.Clamp01(t / .3f);
            card.Root.localScale = new Vector3(1, 1 - Mathf.Pow(1 - unfold, 3), 1);
            for (int i = 0; i < card.Tags.Count; i++)
            {
                float pop = Mathf.Clamp01((t - .3f - i * .18f) / .22f);
                float s = pop <= 0 ? 0 : 1 + .25f * Mathf.Sin(pop * Mathf.PI) * (1 - pop) * 2;
                card.Tags[i].localScale = new Vector3(Mathf.Min(s, 1.2f), Mathf.Min(s, 1.2f), 1);
            }
            float stampT = Mathf.Clamp01((t - .45f - card.Tags.Count * .18f) / .28f);
            float stamp = stampT <= 0 ? 0 : Mathf.Lerp(2.4f, 1, 1 - Mathf.Pow(1 - stampT, 3));
            card.Stamp.localScale = new Vector3(stamp, stamp, 1);
            // After landing the stamp wobbles briefly, as if pressed down hard.
            float landed = t - .73f - card.Tags.Count * .18f;
            float wobble = landed > 0 ? Mathf.Sin(landed * 60) * Mathf.Max(0, .25f - landed) * 12 : 0;
            card.Stamp.localRotation = Quaternion.Euler(0, 0, -6 + 18 * (1 - stampT) + wobble);
        }
        _summaryCards.Tick(deltaTime);
    }

    // The previous summary leaves at once (destruction itself waits for the end of the frame).
    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        { var child = parent.GetChild(i); child.SetParent(null, false); UnityEngine.Object.Destroy(child.gameObject); }
    }

    private Image Fill(float x, float y, float width, float height, Color color)
    {
        if (_usedImages == _images.Count) _images.Add(SplitScreenView.CreateImage("Rating fill", _layer, color));
        var image = _images[_usedImages++]; image.gameObject.SetActive(true); image.color = color;
        if (image.transform.parent != _layer) image.transform.SetParent(_layer, false);
        image.transform.SetAsLastSibling();
        image.rectTransform.localScale = Vector3.one;
        Place(image.rectTransform, x, y, width, height);
        return image;
    }
    private void Text(string value, float x, float y, float width, float height, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        if (_usedTexts == _texts.Count) { var created = SplitScreenView.CreateText("Rating text", _layer); Apply(created); _texts.Add(created); }
        var text = _texts[_usedTexts++]; text.gameObject.SetActive(true);
        if (text.transform.parent != _layer) text.transform.SetParent(_layer, false);
        text.transform.SetAsLastSibling();
        text.text = value; text.fontSize = size; text.color = color; text.alignment = align;
        Place(text.rectTransform, x, y, width, height);
    }
    private TextMeshProUGUI SummaryText(Transform parent, string value, float size, Color color)
    {
        var text = SplitScreenView.CreateText("Review text", parent); Apply(text);
        text.text = value; text.fontSize = size; text.color = color; text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }
    private void Apply(TextMeshProUGUI text) { text.font = _font; if (_material != null) text.fontSharedMaterial = _material; }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
    // Horizontally centred on the screen, measured from its top.
    private static void PlaceCentered(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }

    public void Dispose()
    {
        if (_material != null) UnityEngine.Object.Destroy(_material);
        UnityEngine.Object.Destroy(_canvas.gameObject);
    }
}
