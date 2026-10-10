using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// What the demo draws over everything (its own canvas above the split-screen and the social panels): a chapter card
/// that says what a feature is for, a lower-third caption with the key to press and the chapter's progress, a
/// spotlight that dims all but the zoomed subject and rings it, and a simulated pointer that glides to what a step
/// uses and clicks with a ripple. The caption keeps clear of where the subject ends up (not where it passes during
/// the move), stays on its side while it fits there and only moves when the other side has been needed for a moment:
/// it then fades out and slides in on the other side. A new line on the same side keeps the band and only swaps the
/// words; the band's width and height ease to the new line. Positions given to it are unzoomed screen pixels.
/// </summary>
internal sealed class SplitScreenDemoOverlay : IDisposable
{
    private const float CursorSeconds = .65f, RippleSeconds = .55f, CardFadeSeconds = .35f, CaptionOutSeconds = .16f, CaptionInSeconds = .28f,
        WordsOutSeconds = .12f, WordsInSeconds = .2f, SideHoldSeconds = .3f;
    private const int MaxDots = 12;
    private static readonly Color Accent = new Color(.96f, .55f, .2f, 1), TextColor = new Color(.93f, .91f, .87f, 1),
        Muted = new Color(.66f, .68f, .7f, 1), Veil = new Color(0, 0, 0, .8f), Dim = new Color(0, 0, 0, .5f), Band = new Color(.02f, .022f, .026f, .88f);
    private static Sprite? _arrow, _ring;
    private readonly Canvas _canvas;
    private readonly RectTransform _root, _card, _cardContent, _cardRule, _caption, _captionContent, _keyCap, _cursor, _ripple, _frame;
    private readonly CanvasGroup _cardGroup, _captionGroup, _wordsGroup, _spotGroup, _cursorGroup;
    private readonly Image[] _dims = new Image[4];
    private readonly Image[] _dots = new Image[MaxDots];
    private readonly TextMeshProUGUI _cardTitle, _cardText, _cardProgress, _captionTitle, _captionText, _keyText;
    private readonly Image _rippleImage;
    private bool _cardShown, _cursorShown, _clickPending;
    private Vector2 _cursorFrom, _cursorAt, _cursorTarget;
    private float _cursorT = 1, _rippleT = 1, _ui = 1;
    private Rect _ringRect;
    private bool _ringPlaced;
    // The caption asked for and the one on screen: a new side fades the band out and in again, a new line on the same
    // side only swaps the words. The band's size and height on screen ease towards the shown line's.
    private Line? _wanted, _shown;
    private bool _shownTop, _placed;
    private float _bandAlpha, _wordsAlpha, _sideHeld, _width, _targetWidth, _height, _y;

    private sealed class Line
    {
        internal string Title = string.Empty, Text = string.Empty, Key = string.Empty;
        internal int Step, Steps;
        internal bool Same(Line other) => Title == other.Title && Text == other.Text && Key == other.Key && Step == other.Step && Steps == other.Steps;
    }

    internal Canvas Canvas => _canvas;
    /// <summary>The pointer has reached its target (and clicked, when asked).</summary>
    internal bool CursorArrived => _cursorT >= 1;
    /// <summary>Where the simulated pointer is drawn now (screen pixels), while it shows.</summary>
    internal Vector2? CursorScreen => _cursorShown ? _cursor.anchoredPosition : null;

    internal SplitScreenDemoOverlay()
    {
        _canvas = SplitScreenView.CreateCanvas("EnhancedSpectator Demo", 32020);
        _root = (RectTransform)_canvas.transform;
        // Spotlight: four dims around the subject and a breathing ring on it.
        var spot = new GameObject("Demo spotlight", typeof(RectTransform)).GetComponent<RectTransform>();
        spot.SetParent(_root, false); SplitScreenView.Stretch(spot);
        _spotGroup = spot.gameObject.AddComponent<CanvasGroup>(); _spotGroup.alpha = 0;
        for (int i = 0; i < 4; i++) { _dims[i] = SplitScreenView.CreateImage("Demo dim " + i, spot, Dim); Corner(_dims[i].rectTransform); }
        _frame = new GameObject("Demo focus ring", typeof(RectTransform)).GetComponent<RectTransform>();
        _frame.SetParent(spot, false); Corner(_frame);
        SplitScreenView.Frame4("Demo focus edge ", _frame, new Color(Accent.r, Accent.g, Accent.b, .95f), 2);
        // Lower-third caption: accent bar, key cap, chapter with its step dots, and the line itself.
        _caption = SplitScreenView.CreateImage("Demo caption", _root, Band).rectTransform;
        _caption.anchorMin = _caption.anchorMax = _caption.pivot = new Vector2(.5f, 0);
        var bar = SplitScreenView.CreateImage("Demo caption accent", _caption, Accent).rectTransform;
        bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(0, 1); bar.pivot = new Vector2(0, .5f); bar.sizeDelta = new Vector2(4, 0); bar.anchoredPosition = Vector2.zero;
        _captionContent = new GameObject("Demo caption words", typeof(RectTransform)).GetComponent<RectTransform>();
        _captionContent.SetParent(_caption, false); SplitScreenView.Stretch(_captionContent);
        _wordsGroup = _captionContent.gameObject.AddComponent<CanvasGroup>();
        _captionTitle = Text("Demo caption title", _captionContent, Accent);
        _captionText = Text("Demo caption text", _captionContent, TextColor);
        for (int i = 0; i < MaxDots; i++) _dots[i] = SplitScreenView.CreateImage("Demo step " + i, _captionContent, Muted);
        _keyCap = SplitScreenView.CreateImage("Demo key cap", _captionContent, new Color(.16f, .16f, .17f, 1)).rectTransform;
        SplitScreenView.Frame4("Demo key edge ", _keyCap, new Color(.75f, .75f, .76f, .9f), 1);
        // A darker lower edge makes the cap read as a key.
        var keyBase = SplitScreenView.CreateImage("Demo key base", _keyCap, new Color(0, 0, 0, .45f)).rectTransform;
        keyBase.anchorMin = Vector2.zero; keyBase.anchorMax = new Vector2(1, 0); keyBase.pivot = new Vector2(.5f, 0); keyBase.sizeDelta = new Vector2(0, 3); keyBase.anchoredPosition = Vector2.zero;
        _keyText = Text("Demo key", _keyCap, TextColor); _keyText.alignment = TextAlignmentOptions.Center; SplitScreenView.Stretch(_keyText.rectTransform);
        _captionGroup = _caption.gameObject.AddComponent<CanvasGroup>(); _captionGroup.alpha = 0;
        // Chapter card over a veil; its content rises into place as it fades in.
        var card = SplitScreenView.CreateImage("Demo chapter card", _root, Veil);
        _card = card.rectTransform; SplitScreenView.Stretch(_card);
        _cardContent = new GameObject("Demo chapter content", typeof(RectTransform)).GetComponent<RectTransform>();
        _cardContent.SetParent(_card, false); SplitScreenView.Stretch(_cardContent);
        _cardProgress = Text("Demo chapter progress", _cardContent, Muted); _cardProgress.alignment = TextAlignmentOptions.Center;
        _cardTitle = Text("Demo chapter title", _cardContent, Accent); _cardTitle.alignment = TextAlignmentOptions.Center;
        _cardRule = SplitScreenView.CreateImage("Demo chapter rule", _cardContent, Accent).rectTransform;
        _cardRule.anchorMin = _cardRule.anchorMax = _cardRule.pivot = new Vector2(.5f, .5f);
        _cardText = Text("Demo chapter text", _cardContent, TextColor); _cardText.alignment = TextAlignmentOptions.Center;
        _cardGroup = _card.gameObject.AddComponent<CanvasGroup>(); _cardGroup.alpha = 0;
        // Simulated pointer and its click ripple.
        var ripple = SplitScreenView.CreateImage("Demo click ripple", _root, Accent);
        _rippleImage = ripple; ripple.sprite = Ring(); _ripple = ripple.rectTransform; _ripple.anchorMin = _ripple.anchorMax = Vector2.zero; _ripple.pivot = new Vector2(.5f, .5f);
        var cursor = SplitScreenView.CreateImage("Demo pointer", _root, Color.white);
        cursor.sprite = Arrow(); _cursor = cursor.rectTransform; _cursor.anchorMin = _cursor.anchorMax = Vector2.zero; _cursor.pivot = new Vector2(0, 1);
        _cursorGroup = cursor.gameObject.AddComponent<CanvasGroup>(); _cursorGroup.alpha = 0;
        _card.gameObject.SetActive(false); _caption.gameObject.SetActive(false); _ripple.gameObject.SetActive(false);
        _canvas.gameObject.SetActive(false);
    }

    internal void ConfigureFont(TMP_FontAsset? font, Material? material)
    {
        foreach (var text in _canvas.GetComponentsInChildren<TextMeshProUGUI>(true)) { text.font = font; if (material != null) text.fontSharedMaterial = material; }
    }

    /// <summary>A step's caption: the chapter, one line, the key it uses and the step's place in its chapter; null text hides it.</summary>
    internal void SetCaption(string? title, string? text, string? key, int step = 0, int steps = 0)
    {
        if (text == null) { _wanted = null; return; }
        var line = new Line { Title = title ?? string.Empty, Text = text, Key = key ?? string.Empty, Step = step, Steps = Math.Min(steps, MaxDots) };
        if (_wanted == null || !_wanted.Same(line)) _wanted = line;
    }

    /// <summary>The chapter card: what the feature is for, over a veil.</summary>
    internal void ShowCard(string title, string text, string progress)
    {
        _cardTitle.text = title; _cardText.text = text; _cardProgress.text = progress; _cardShown = true;
        LayoutCard();
    }
    internal void HideCard() => _cardShown = false;

    /// <summary>Glides the pointer to <paramref name="point"/> (unzoomed screen pixels) and clicks there when asked; null hides it.</summary>
    internal void MoveCursor(Vector2? point, bool click)
    {
        if (point is not { } target) { _cursorShown = false; _cursorT = 1; _clickPending = false; return; }
        if (!_cursorShown) _cursorAt = target + new Vector2(80, -60) * _ui;
        _cursorShown = true; _cursorFrom = _cursorAt; _cursorTarget = target; _cursorT = 0; _clickPending = click;
    }

    internal void Clear()
    {
        _cardShown = _cursorShown = _placed = false; _wanted = _shown = null; _bandAlpha = _wordsAlpha = _sideHeld = 0;
        _cardGroup.alpha = _captionGroup.alpha = _spotGroup.alpha = _cursorGroup.alpha = 0;
        _card.gameObject.SetActive(false); _caption.gameObject.SetActive(false); _ripple.gameObject.SetActive(false);
        _canvas.gameObject.SetActive(false);
    }

    /// <summary>Animates everything; <paramref name="bottomInset"/> is the audience row the caption sits above.</summary>
    internal void Tick(float deltaTime, float ui, float bottomInset, Vector2 area)
    {
        if (Math.Abs(ui - _ui) > .001f) { _ui = ui; if (_shown != null) LayoutCaption(_shown); LayoutCard(); }
        bool any = _cardShown || _wanted != null || _shown != null || _cursorShown || SpectatorDemoZoom.Focus.HasValue
            || _cardGroup.alpha > 0 || _spotGroup.alpha > 0 || _cursorGroup.alpha > 0 || _ripple.gameObject.activeSelf;
        if (_canvas.gameObject.activeSelf != any) _canvas.gameObject.SetActive(any);
        if (!any) return;
        TickCard(deltaTime);
        TickSpotlight(deltaTime, area);
        TickCaption(deltaTime, bottomInset, area);
        TickCursor(deltaTime);
    }

    private void TickCard(float deltaTime)
    {
        _cardGroup.alpha = Mathf.MoveTowards(_cardGroup.alpha, _cardShown ? 1 : 0, deltaTime / CardFadeSeconds);
        if (_card.gameObject.activeSelf != _cardGroup.alpha > 0) _card.gameObject.SetActive(_cardGroup.alpha > 0);
        // Rises in as it appears; leaves in place.
        float eased = 1 - (1 - _cardGroup.alpha) * (1 - _cardGroup.alpha);
        _cardContent.anchoredPosition = new Vector2(0, _cardShown ? -(1 - eased) * 22 * _ui : 0);
        _cardRule.sizeDelta = new Vector2(Mathf.Round(72 * _ui) * (_cardShown ? eased : 1), Mathf.Max(2, Mathf.Round(3 * _ui)));
    }

    // The spotlight rings the step's subject once the picture has arrived on it. Moving on to a subject next to the
    // last one (the next row, a list opening under its label) the ring glides along; moving anywhere else it fades
    // at once and contracts onto the new subject on arrival. While the framing is only held (the step's subject is
    // not drawn yet) nothing is ringed.
    private void TickSpotlight(float deltaTime, Vector2 area)
    {
        var focus = SpectatorDemoZoom.ScreenFocus;
        bool show = focus.HasValue && !SpectatorDemoZoom.Held && !_cardShown
            && (SpectatorDemoZoom.Settled || SpectatorDemoZoom.Continuous && _spotGroup.alpha > 0);
        _spotGroup.alpha = Mathf.MoveTowards(_spotGroup.alpha, show ? 1 : 0, deltaTime / (show ? .3f : .12f));
        if (focus is not { } subject || _spotGroup.alpha <= 0) { _ringPlaced = false; return; }
        float pad = Mathf.Round(8 * _ui);
        var target = Rect.MinMaxRect(subject.xMin - pad, subject.yMin - pad, subject.xMax + pad, subject.yMax + pad);
        if (!_ringPlaced) { _ringRect = target; _ringPlaced = true; }
        else
        {
            float k = 1 - Mathf.Exp(-deltaTime / .09f);
            _ringRect = Rect.MinMaxRect(Mathf.Lerp(_ringRect.xMin, target.xMin, k), Mathf.Lerp(_ringRect.yMin, target.yMin, k), Mathf.Lerp(_ringRect.xMax, target.xMax, k), Mathf.Lerp(_ringRect.yMax, target.yMax, k));
        }
        // Appearing, the ring closes in from a little further out.
        float eased = 1 - (1 - _spotGroup.alpha) * (1 - _spotGroup.alpha), lockOn = show ? (1 - eased) * 28 * _ui : 0;
        var rect = Rect.MinMaxRect(Mathf.Max(0, _ringRect.xMin - lockOn), Mathf.Max(0, _ringRect.yMin - lockOn), Mathf.Min(area.x, _ringRect.xMax + lockOn), Mathf.Min(area.y, _ringRect.yMax + lockOn));
        Place(_dims[0].rectTransform, 0, rect.yMax, area.x, area.y - rect.yMax);
        Place(_dims[1].rectTransform, 0, 0, area.x, rect.yMin);
        Place(_dims[2].rectTransform, 0, rect.yMin, rect.xMin, rect.height);
        Place(_dims[3].rectTransform, rect.xMax, rect.yMin, area.x - rect.xMax, rect.height);
        Place(_frame, rect.xMin, rect.yMin, rect.width, rect.height);
    }

    private void TickCaption(float deltaTime, float bottomInset, Vector2 area)
    {
        bool top = ChooseTop(deltaTime, area);
        if (_shown == null)
        {
            if (_wanted == null || _cardShown) { if (_caption.gameObject.activeSelf) _caption.gameObject.SetActive(false); return; }
            _shown = _wanted; _shownTop = top; LayoutCaption(_shown); _width = _targetWidth; _wordsAlpha = 1; _placed = false;
        }
        // Leaving (hidden, under the card, or moving to the other side): the whole band fades out.
        bool leave = _wanted == null || _cardShown || top != _shownTop;
        _bandAlpha = Mathf.MoveTowards(_bandAlpha, leave ? 0 : 1, deltaTime / (leave ? CaptionOutSeconds : CaptionInSeconds));
        if (leave && _bandAlpha <= 0) { _shown = null; _caption.gameObject.SetActive(false); return; }
        // Same place, new line: the band stays and only the words change.
        if (!leave && !_wanted!.Same(_shown))
        {
            _wordsAlpha = Mathf.MoveTowards(_wordsAlpha, 0, deltaTime / WordsOutSeconds);
            if (_wordsAlpha <= 0) { _shown = _wanted; LayoutCaption(_shown); }
        }
        else _wordsAlpha = Mathf.MoveTowards(_wordsAlpha, 1, deltaTime / WordsInSeconds);
        if (!_caption.gameObject.activeSelf) _caption.gameObject.SetActive(true);
        _width = Mathf.Lerp(_width, _targetWidth, 1 - Mathf.Exp(-deltaTime / .07f));
        _caption.sizeDelta = new Vector2(_width, _height);
        // Above the audience row with nothing zoomed, nearer the edge with a subject: the height eases between them.
        float margin = Mathf.Round(26 * _ui), baseline = _shownTop ? -margin : SpectatorDemoZoom.Focus.HasValue ? margin : bottomInset + margin;
        _y = _placed ? Mathf.Lerp(_y, baseline, 1 - Mathf.Exp(-deltaTime / .12f)) : baseline; _placed = true;
        float eased = 1 - (1 - _bandAlpha) * (1 - _bandAlpha), slide = (1 - eased) * 14 * _ui;
        _captionGroup.alpha = eased; _wordsGroup.alpha = _wordsAlpha;
        _caption.anchorMin = _caption.anchorMax = _caption.pivot = new Vector2(.5f, _shownTop ? 1 : 0);
        // Slides in from (and out to) its own edge of the screen.
        _caption.anchoredPosition = new Vector2(0, _y + (_shownTop ? slide : -slide));
    }

    // Above the audience row with nothing zoomed. With a subject: the side it ends up leaving room on, keeping the
    // current side while the caption still fits there; the other side must be needed for a moment before the
    // caption moves, so a subject that settles or a panel that redraws never makes it hop.
    private bool ChooseTop(float deltaTime, Vector2 area)
    {
        bool wanted = PreferredTop(area);
        if (_shown == null || wanted == _shownTop) { _sideHeld = 0; return _shown == null ? wanted : _shownTop; }
        _sideHeld += deltaTime;
        return _sideHeld >= SideHoldSeconds ? wanted : _shownTop;
    }

    private bool PreferredTop(Vector2 area)
    {
        if (SpectatorDemoZoom.FinalScreenFocus(area) is not { } focus) return false;
        float need = CaptionHeight + Mathf.Round(26 * _ui) * 2;
        float above = area.y - focus.yMax, below = focus.yMin;
        bool fitsTop = above >= need, fitsBottom = below >= need;
        if (_shownTop ? fitsTop : fitsBottom) return _shownTop;
        if (_shownTop ? fitsBottom : fitsTop) return !_shownTop;
        return above > below;
    }

    private float CaptionHeight => Mathf.Round(14 * _ui) * 2 + Mathf.Round(20 * _ui) + Mathf.Round(30 * _ui);

    private void TickCursor(float deltaTime)
    {
        _cursorGroup.alpha = Mathf.MoveTowards(_cursorGroup.alpha, _cursorShown && !_cardShown ? 1 : 0, deltaTime / .25f);
        // The target follows the zoom, so the pointer lands where the thing is drawn now.
        Vector2 target = SpectatorDemoZoom.ToScreen(_cursorTarget), from = SpectatorDemoZoom.ToScreen(_cursorFrom);
        if (_cursorT < 1)
        {
            _cursorT = Mathf.Min(1, _cursorT + deltaTime / CursorSeconds);
            float e = _cursorT < .5f ? 4 * _cursorT * _cursorT * _cursorT : 1 - Mathf.Pow(-2 * _cursorT + 2, 3) / 2;
            // A slight arc reads as a hand moving the mouse rather than a straight slide.
            var arc = Vector2.Perpendicular(target - from) * .12f * Mathf.Sin(e * Mathf.PI);
            _cursor.anchoredPosition = Vector2.Lerp(from, target, e) + arc;
            _cursorAt = Vector2.Lerp(_cursorFrom, _cursorTarget, e);
            if (_cursorT >= 1 && _clickPending) { _clickPending = false; _rippleT = 0; }
        }
        else _cursor.anchoredPosition = target;
        float size = Mathf.Round(34 * _ui), press = _rippleT < .18f ? .86f : 1;
        _cursor.sizeDelta = new Vector2(size * 13f / 21f, size) * press;
        _ripple.gameObject.SetActive(_rippleT < 1);
        if (_rippleT >= 1) return;
        _rippleT = Mathf.Min(1, _rippleT + deltaTime / RippleSeconds);
        float radius = Mathf.Lerp(8, 46, 1 - (1 - _rippleT) * (1 - _rippleT)) * _ui;
        _ripple.anchoredPosition = target; _ripple.sizeDelta = new Vector2(radius * 2, radius * 2);
        _rippleImage.color = new Color(Accent.r, Accent.g, Accent.b, 1 - _rippleT);
    }

    private void LayoutCaption(Line line)
    {
        _captionTitle.text = line.Title; _captionText.text = line.Text; _keyText.text = line.Key;
        float u = _ui, pad = Mathf.Round(14 * u), titleHeight = Mathf.Round(20 * u), textHeight = Mathf.Round(30 * u);
        _captionTitle.fontSize = Mathf.Round(14 * u); _captionText.fontSize = Mathf.Round(21 * u); _keyText.fontSize = Mathf.Round(15 * u);
        bool key = line.Key.Length > 0;
        float keyHeight = Mathf.Round(36 * u);
        float keyWidth = key ? Mathf.Max(keyHeight, Mathf.Ceil(_keyText.GetPreferredValues(line.Key, 4096, 0).x) + Mathf.Round(18 * u)) : 0;
        float keyGap = key ? Mathf.Round(14 * u) : 0;
        // The chapter's steps as dots after its name: done, current, still to come.
        float dot = Mathf.Max(4, Mathf.Round(6 * u)), dotGap = Mathf.Round(5 * u);
        float titleWidth = Mathf.Ceil(_captionTitle.GetPreferredValues(line.Title, 4096, 0).x);
        float dotsWidth = line.Steps > 1 ? Mathf.Round(10 * u) + line.Steps * (dot + dotGap) - dotGap : 0;
        float textWidth = Mathf.Max(Mathf.Ceil(_captionText.GetPreferredValues(line.Text, 4096, 0).x), titleWidth + dotsWidth);
        float left = pad + Mathf.Round(4 * u) + keyWidth + keyGap;
        _targetWidth = left + textWidth + pad + 2; _height = CaptionHeight;
        Top(_captionTitle.rectTransform, left, pad, titleWidth + 2, titleHeight);
        Top(_captionText.rectTransform, left, pad + titleHeight, textWidth + 2, textHeight);
        for (int i = 0; i < MaxDots; i++)
        {
            bool shown = line.Steps > 1 && i < line.Steps;
            _dots[i].gameObject.SetActive(shown);
            if (!shown) continue;
            Top(_dots[i].rectTransform, left + titleWidth + Mathf.Round(10 * u) + i * (dot + dotGap), pad + (titleHeight - dot) * .5f, dot, dot);
            _dots[i].color = i == line.Step ? Accent : i < line.Step ? new Color(Accent.r, Accent.g, Accent.b, .4f) : new Color(Muted.r, Muted.g, Muted.b, .3f);
        }
        _keyCap.gameObject.SetActive(key);
        if (key) Top(_keyCap, pad + Mathf.Round(4 * u), pad + (titleHeight + textHeight - keyHeight) * .5f, keyWidth, keyHeight);
    }

    private void LayoutCard()
    {
        float u = _ui;
        _cardTitle.fontSize = Mathf.Round(44 * u); _cardText.fontSize = Mathf.Round(21 * u); _cardProgress.fontSize = Mathf.Round(14 * u);
        Middle(_cardProgress.rectTransform, Mathf.Round(74 * u), Mathf.Round(22 * u));
        Middle(_cardTitle.rectTransform, Mathf.Round(32 * u), Mathf.Round(58 * u));
        _cardRule.anchoredPosition = new Vector2(0, Mathf.Round(-4 * u));
        Middle(_cardText.rectTransform, Mathf.Round(-38 * u), Mathf.Round(32 * u));
    }

    private static TextMeshProUGUI Text(string name, Transform parent, Color color)
    {
        var text = SplitScreenView.CreateText(name, parent); text.text = string.Empty; text.color = color; text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }
    private static void Corner(RectTransform rect) { rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    { rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(Mathf.Max(0, width), Mathf.Max(0, height)); }
    private static void Top(RectTransform rect, float x, float y, float width, float height)
    { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); }
    private static void Middle(RectTransform rect, float y, float height)
    { rect.anchorMin = new Vector2(0, .5f); rect.anchorMax = new Vector2(1, .5f); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2(0, y); rect.sizeDelta = new Vector2(0, height); }

    // A classic arrow pointer, white with a dark outline, drawn once at 4x with anti-aliased edges.
    private static Sprite Arrow()
    {
        if (_arrow != null) return _arrow;
        Vector2[] shape = { new(0, 0), new(0, 17), new(4, 13), new(7, 20), new(9, 19), new(6, 12), new(12, 12) };
        const int scale = 4, width = 13 * scale + 8, height = 21 * scale + 8;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "EnhancedSpectator demo pointer" };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                // Shape space: origin at the tip (top-left), y down.
                var p = new Vector2((x + .5f - 4) / scale, (height - y - .5f - 4) / scale);
                float d = SignedDistance(p, shape) * scale;
                float outer = Mathf.Clamp01(.5f - (d - 1.5f)), inner = Mathf.Clamp01(.5f - (d + 1.5f));
                var color = Color.Lerp(new Color(.05f, .05f, .06f, 1), Color.white, inner);
                pixels[y * width + x] = new Color(color.r, color.g, color.b, outer);
            }
        texture.SetPixels32(pixels); texture.Apply(false, true);
        return _arrow = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0, 1), 100);
    }

    private static float SignedDistance(Vector2 p, IReadOnlyList<Vector2> polygon)
    {
        float distance = float.MaxValue; bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[j], b = polygon[i], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            distance = Mathf.Min(distance, (a + ab * t - p).magnitude);
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside ? -distance : distance;
    }

    // A thin anti-aliased ring for the click ripple.
    private static Sprite Ring()
    {
        if (_ring != null) return _ring;
        const int size = 128; const float radius = 60, half = 3;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "EnhancedSpectator demo ripple" };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Abs(new Vector2(x + .5f - size * .5f, y + .5f - size * .5f).magnitude - radius);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(half - d + .5f)));
            }
        texture.SetPixels32(pixels); texture.Apply(false, true);
        return _ring = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
    }

    public void Dispose() => UnityEngine.Object.Destroy(_canvas.gameObject);
}
