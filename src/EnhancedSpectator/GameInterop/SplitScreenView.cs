using System;
using System.Collections.Generic;
using EnhancedSpectator.Features.SplitScreen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>Owned video and decoration canvases. Game state, input ownership and texture lifetimes remain with the module.</summary>
internal sealed partial class SplitScreenView : IDisposable
{
    private const float RetirementSeconds = .2f, CrossfadeSeconds = .18f, PulseSeconds = .5f, PlateInset = 6,
        HintSeconds = 5, HintFadeSeconds = .35f;
    private static readonly Color Frame = new Color(.2f, .21f, .23f, 1), HoverFrame = new Color(.62f, .64f, .68f, 1),
        AudioFrame = new Color(1, .38f, .02f, 1), Name = new Color(.94f, .94f, .94f, 1), AudioName = new Color(1, .83f, .67f, 1);
    private readonly Dictionary<SplitScreenKey, Tile> _tiles = new Dictionary<SplitScreenKey, Tile>();
    private readonly List<SplitScreenParticipant> _participants = new List<SplitScreenParticipant>(31);
    private readonly HashSet<SplitScreenKey> _live = new HashSet<SplitScreenKey>();
    private readonly List<SplitScreenKey> _completed = new List<SplitScreenKey>(), _remove = new List<SplitScreenKey>();
    private readonly Canvas _videoCanvas, _hudCanvas;
    private readonly SplitScreenTestToolbar _toolbar;
    private readonly TextMeshProUGUI _empty;
    private readonly RectTransform _hints;
    private readonly CanvasGroup _hintGroup;
    private readonly List<(Image Cap, TextMeshProUGUI Key, TextMeshProUGUI Label)> _hintItems = new();
    private readonly List<(string Label, string Keys)> _hintRows = new();
    private Canvas? _beneath;
    private float _hintAwake;
    private bool _hintsEnabled, _hintsDirty = true;
    private int _hintAccent = -1;
    private Image? _hintAccentBack;
    private readonly SplitScreenDeadBar _deadBar;
    private readonly Dictionary<SplitScreenKey, float> _speaking = new Dictionary<SplitScreenKey, float>();
    private bool _showSpeaking = true;
    private float _clock;
    private static readonly Color SpeakingEdge = SplitScreenDeadBar.SpeakingColor;
    private readonly RectTransform _banner;
    private readonly TextMeshProUGUI _bannerText;
    private readonly CanvasGroup _bannerGroup;
    private float _bannerPulse = 1;
    private bool _bannerShown;
    private TMP_FontAsset? _font;
    private Material? _material;
    private Vector2 _size = new Vector2(1920, 1080);
    private float _uiScale = 1;
    private SplitScreenKey? _focused, _hover;
    private float _primaryTransitionOpacity;
    private bool _visible, _hudVisible = true, _testVisible, _layoutDirty = true;

    internal SplitScreenView()
    {
        _videoCanvas = CreateCanvas("EnhancedSpectator Split Video", 32000);
        var background = CreateImage("Split background", _videoCanvas.transform, new Color(.018f, .02f, .025f, 1));
        Stretch(background.rectTransform);
        _hudCanvas = CreateCanvas("EnhancedSpectator Split HUD", 32001);
        // Everything but the background sits in a root the demo can zoom.
        _videoRoot = SpectatorDemoZoom.CreateRoot(_videoCanvas.transform); _hudRoot = SpectatorDemoZoom.CreateRoot(_hudCanvas.transform);
        _empty = CreateText("No surviving players", _hudRoot);
        Stretch(_empty.rectTransform); _empty.alignment = TextAlignmentOptions.Center; _empty.fontSize = 20;
        _empty.color = new Color(.6f, .62f, .64f, 1);
        _hints = CreateImage("Key hints", _hudRoot, new Color(.02f, .022f, .026f, .72f)).rectTransform;
        _hints.anchorMin = _hints.anchorMax = _hints.pivot = new Vector2(1, .5f);
        _hintGroup = _hints.gameObject.AddComponent<CanvasGroup>(); _hintGroup.alpha = 0;
        _deadBar = new SplitScreenDeadBar(_hudRoot);
        // Watching together: a blue badge in the top-right corner of the view being followed.
        _banner = CreateImage("Watch together banner", _hudRoot, new Color(.04f, .1f, .22f, .9f)).rectTransform;
        _banner.anchorMin = _banner.anchorMax = _banner.pivot = new Vector2(1, 1);
        Frame4("Watch together edge ", _banner, new Color(.25f, .55f, 1, .9f), 1);
        _bannerText = CreateText("Watch together", _banner); _bannerText.alignment = TextAlignmentOptions.Center; _bannerText.color = new Color(.8f, .9f, 1, 1);
        Stretch(_bannerText.rectTransform);
        _bannerGroup = _banner.gameObject.AddComponent<CanvasGroup>(); _bannerGroup.alpha = 0; _banner.gameObject.SetActive(false);
        // "N watching" under the large view's name; hovering it lists who.
        _viewers = CreateImage("Viewer count", _hudRoot, new Color(0, 0, 0, .58f)).rectTransform;
        _viewers.anchorMin = _viewers.anchorMax = _viewers.pivot = new Vector2(0, 1);
        _viewerText = CreateText("Viewer count text", _viewers); _viewerText.alignment = TextAlignmentOptions.MidlineLeft; _viewerText.color = new Color(.86f, .87f, .88f, 1);
        _viewerList = CreateImage("Viewer list", _viewers, new Color(.03f, .032f, .037f, .95f)).rectTransform;
        _viewerList.anchorMin = _viewerList.anchorMax = new Vector2(0, 0); _viewerList.pivot = new Vector2(0, 1);
        Frame4("Viewer list edge ", _viewerList, new Color(.96f, .55f, .2f, .45f), 1);
        for (int i = 0; i < _viewerColumns.Length; i++)
        {
            _viewerColumns[i] = CreateText("Viewer names " + i, _viewerList); _viewerColumns[i].alignment = TextAlignmentOptions.TopLeft;
            _viewerColumns[i].color = new Color(.86f, .87f, .88f, 1);
        }
        _viewerNote = CreateText("Viewer note", _viewerList); _viewerNote.alignment = TextAlignmentOptions.TopLeft; _viewerNote.color = new Color(.6f, .62f, .64f, 1);
        _viewerList.gameObject.SetActive(false); _viewers.gameObject.SetActive(false);
        CreateClock(); CreateChat();
        _demo = new SplitScreenDemoOverlay();
        _toolbar = new SplitScreenTestToolbar();
        SetVisible(false);
    }

    internal bool IsReady => _videoCanvas != null && _hudCanvas != null && _font != null;
    internal Canvas VideoCanvas => _videoCanvas;
    // A scene unload can destroy the canvases before the next EnsureView rebuilds them (see IsReady); the module
    // asks this at the end of every update, early returns included.
    internal bool DrawsBeneathHud => _videoCanvas != null && _videoCanvas.gameObject.activeInHierarchy && _beneath != null;
    internal Canvas HudCanvas => _hudCanvas;
    // Existing mod-owned roster/hints can borrow this front layer; their adapters restore their original parent before this view is disposed.
    internal RectTransform OverlayParent => (RectTransform)_hudCanvas.transform;
    internal Canvas TestCanvas => _toolbar.Canvas;
    internal bool OwnsVideoCanvas(Canvas canvas) => canvas == _videoCanvas;
    internal bool OwnsCanvas(Canvas canvas) => canvas == _videoCanvas || canvas == _hudCanvas || canvas == _toolbar.Canvas || canvas == _demo.Canvas;
    /// <summary>
    /// Adds the glyphs of <paramref name="rows"/> to the font and its fallbacks ahead of their first showing: a dynamic
    /// font asset renders a character into its atlas the first time a text uses it.
    /// </summary>
    internal void WarmText(List<(string Label, string Keys)> rows)
    {
        if (_font == null) return;
        var text = new System.Text.StringBuilder();
        foreach (var row in rows) text.Append(row.Label).Append(row.Keys);
        string characters = text.ToString();
        _font.TryAddCharacters(characters);
        if (_font.fallbackFontAssetTable != null)
            foreach (var fallback in _font.fallbackFontAssetTable) if (fallback != null) fallback.TryAddCharacters(characters);
    }

    internal void ConfigureFont(TMP_FontAsset? font)
    {
        if (_material != null) UnityEngine.Object.Destroy(_material);
        _font = font; _material = font != null ? SpectatorTextStyle.CreateLabelMaterial(font) : null;
        ApplyFont(_empty);
        foreach (var tile in _tiles.Values) ApplyFont(tile.Title);
        foreach (var item in _hintItems) { ApplyFont(item.Key); ApplyFont(item.Label); }
        _deadBar.ConfigureFont(font, _material); ApplyFont(_bannerText); ConfigureClockFont(); ConfigureChatFont(); ApplyFont(_viewerText); ApplyFont(_viewerNote);
        _demo.ConfigureFont(font, _material);
        foreach (var column in _viewerColumns) ApplyFont(column);
        _toolbar.ConfigureFont(font, _material);
    }

    internal void SetSize(Vector2 viewport)
    {
        if (_size == viewport) return;
        _size = viewport; _layoutDirty = true; _uiScale = SpectatorTextStyle.UiScale(viewport);
        _empty.fontSize = 20 * _uiScale;
        foreach (var tile in _tiles.Values) tile.FontSize = 0;
        _hintsDirty = true; ResetClockLayout(); _deadBar.SetSize(viewport.x, _uiScale);
        _toolbar.SetSize(viewport);
    }

    internal void SetTiles(IReadOnlyList<SplitScreenParticipant> participants, SplitScreenKey? focused, SplitScreenKey? currentAudio)
    {
        bool changed = _layoutDirty || _focused != focused || _participants.Count != participants.Count;
        if (!changed)
            for (int i = 0; i < participants.Count; i++)
                if (_participants[i].Key != participants[i].Key) { changed = true; break; }
        _focused = focused; _audioKey = currentAudio;
        _live.Clear();
        foreach (var participant in participants) _live.Add(participant.Key);
        foreach (var tile in _tiles.Values)
            if (!_live.Contains(tile.Key) && !tile.Retiring)
            {
                tile.Retiring = true; tile.Retirement = RetirementSeconds;
                Highlight(tile, false);
                var rect = tile.Motion.Current;
                tile.Motion.Retarget(new SplitScreenTileRect(rect.CenterX, rect.CenterY, rect.Width * .9f));
            }
        if (changed)
        {
            _participants.Clear();
            for (int i = 0; i < participants.Count; i++) _participants.Add(participants[i]);
            Relayout();
        }
        for (int i = 0; i < participants.Count; i++)
        {
            var participant = participants[i];
            var tile = _tiles[participant.Key];
            if (tile.Title.text != participant.Name) { tile.Title.text = participant.Name; tile.FontSize = 0; }
            tile.Source = participant.Source;
            bool audio = currentAudio == participant.Key;
            if (tile.Retiring || tile.Audio != audio) { tile.Retiring = false; Highlight(tile, audio); }
        }
        // Overlays stay above every tile frame: the audience row (with its hover card), then the key hints.
        // The focused tile's frame rises just beneath them.
        var hud = _hudRoot;
        int count = hud.childCount;
        if (_hints.GetSiblingIndex() != count - 1 || _deadBar.Root.GetSiblingIndex() != count - 2)
        { _deadBar.Root.SetAsLastSibling(); _hints.SetAsLastSibling(); }
        if (focused.HasValue && _tiles.TryGetValue(focused.Value, out var focus))
        {
            ToFront(focus.Video);
            if (focus.Decoration.GetSiblingIndex() != count - 3) focus.Decoration.SetSiblingIndex(count - 3);
        }
        _empty.gameObject.SetActive(participants.Count == 0);
    }

    /// <summary>
    /// Draws beneath the game's camera-space HUD canvas while a menu or the chat is open (live views stay visible
    /// behind them), or above everything again with null.
    /// </summary>
    internal void SetBeneath(Canvas? hud)
    {
        if (_beneath == hud) return;
        _beneath = hud;
        Layer(_videoCanvas, hud, 32000, -2); Layer(_hudCanvas, hud, 32001, -1);
    }
    internal static void Layer(Canvas canvas, Canvas? hud, int overlayOrder, int offset)
    {
        if (hud == null) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = overlayOrder; return; }
        // The UI camera only draws canvases on layers in its culling mask, so take the HUD canvas's layer.
        canvas.gameObject.layer = hud.gameObject.layer;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = hud.worldCamera; canvas.planeDistance = hud.planeDistance;
        canvas.sortingLayerID = hud.sortingLayerID; canvas.sortingOrder = hud.sortingOrder + offset;
    }

    /// <summary>Players speaking this moment: small views flash their name plate edge, dead players their chip.</summary>
    /// <summary>Who speaks and how loudly (0–1); views light while anyone behind them speaks, audience chips follow the loudness.</summary>
    internal void SetVoices(IEnumerable<(SplitScreenKey Key, float Level)> speaking, bool showOnViews)
    {
        _speaking.Clear(); foreach (var voice in speaking) _speaking[voice.Key] = voice.Level;
        _showSpeaking = showOnViews;
    }
    /// <summary>Dead-player bar under the views; an empty list hides it and gives the space back to the views.</summary>
    internal void SetDeadPlayers(IReadOnlyList<SplitScreenDeadPlayer> players, bool chinese, Func<ulong, Texture?>? avatarOf)
    {
        _deadBar.AvatarOf = avatarOf;
        if (_deadBar.SetPlayers(players, chinese)) { _layoutDirty = true; _hintsDirty = true; }
    }
    internal int DeadPlayerCount => _deadBar.Count;
    internal SplitScreenDeadPlayer? HitTestDead(Vector2 screenPoint)
        => _visible && _hudCanvas.gameObject.activeInHierarchy ? _deadBar.HitTest(screenPoint, CanvasCamera(_hudCanvas)) : null;
    internal void SetDeadHover(SplitScreenKey? key) => _deadBar.SetHover(key);
    internal void ShowEmote(ulong clientId, string text) => _deadBar.ShowEmote(clientId, text);
    internal void ShowEmote(SplitScreenKey key, string text) => _deadBar.ShowEmote(key, text);
    internal float DeadBarHeight => _deadBar.Height;

    /// <summary>
    /// Watching together: ring the followed player's chip and put a blue badge in the top-right corner of the view
    /// showing what they watch (<paramref name="view"/>); null ends it.
    /// </summary>
    internal void SetWatchTogether(SplitScreenKey? key, string? text, SplitScreenKey? view = null)
    {
        _deadBar.SetFollowing(key);
        _bannerShown = text != null;
        _bannerView = view;
        if (text == null || _bannerText.text == text) return;
        _bannerText.text = text; _bannerText.fontSize = Mathf.Round(13 * _uiScale);
        float width = Mathf.Ceil(_bannerText.GetPreferredValues(text, 4096, 0).x) + Mathf.Round(20 * _uiScale);
        _banner.sizeDelta = new Vector2(width, Mathf.Round(24 * _uiScale));
        _bannerPulse = 0;
    }
    private SplitScreenKey? _bannerView;

    // ---- Demo: the video and HUD roots follow the shared demo zoom; the overlay draws the demo's own layer. ----
    private readonly SplitScreenDemoOverlay _demo;
    private readonly RectTransform _videoRoot, _hudRoot;
    internal RectTransform HudRoot => _hudRoot;
    internal SplitScreenDemoOverlay Demo => _demo;

    /// <summary>
    /// Eases the whole spectator UI towards <paramref name="focus"/> (unzoomed screen pixels, origin bottom-left) so it
    /// fills most of the screen; null eases back to the whole picture, or jumps there when <paramref name="instant"/>.
    /// </summary>
    internal void SetZoom(Rect? focus, bool instant = false, bool held = false)
    {
        if (instant && focus == null) { SpectatorDemoZoom.Reset(); SpectatorDemoZoom.Apply(_videoRoot); SpectatorDemoZoom.Apply(_hudRoot); }
        else SpectatorDemoZoom.SetFocus(focus, held);
    }

    /// <summary>A step's caption: chapter, one line, the key it uses and its place in the chapter; null text hides it.</summary>
    internal void SetCaption(string? title, string? text, string? key = null, int step = 0, int steps = 0) => _demo.SetCaption(title, text, key, step, steps);
    internal void ShowDemoCard(string title, string text, string progress) => _demo.ShowCard(title, text, progress);
    internal void HideDemoCard() => _demo.HideCard();
    /// <summary>Glides the demo pointer to an unzoomed screen point (clicking there when asked); null hides it.</summary>
    internal void MoveDemoCursor(Vector2? point, bool click) => _demo.MoveCursor(point, click);
    internal bool DemoCursorArrived => _demo.CursorArrived;
    internal Vector2? DemoCursorScreen => _demo.CursorScreen;
    /// <summary>The pointer is where the step needs it and the picture has finished moving: the step can act now.</summary>
    internal bool DemoReady => _demo.CursorArrived && SpectatorDemoZoom.Settled;
    /// <summary>Everything the demo drew goes at once and the picture is whole again.</summary>
    internal void ClearDemo() { _demo.Clear(); SetZoom(null, instant: true); }

    private void TickDemo(float deltaTime)
    {
        SpectatorDemoZoom.Tick(deltaTime, _hudRoot.rect.size);
        SpectatorDemoZoom.Apply(_videoRoot); SpectatorDemoZoom.Apply(_hudRoot);
        _demo.Tick(deltaTime, _uiScale, _deadBar.Height, _hudRoot.rect.size);
    }

    private Rect? LocalRect(RectTransform? rect) => SpectatorDemoZoom.Measure(_hudRoot, rect);
    private static Rect? Union(Rect? a, Rect? b) => SpectatorDemoZoom.Union(a, b);
    private static Rect? Grow(Rect? rect, float by) => SpectatorDemoZoom.Grow(rect, by);

    /// <summary>A view's rectangle.</summary>
    internal Rect? FocusTile(SplitScreenKey key) => _tiles.TryGetValue(key, out var tile) ? LocalRect(tile.Decoration) : null;
    /// <summary>The top-left of a view: its name plate and audio mark.</summary>
    internal Rect? FocusTileCorner(SplitScreenKey key)
        => FocusTile(key) is { } r ? Rect.MinMaxRect(r.xMin, r.yMax - r.height * .3f, r.xMin + r.width * .4f, r.yMax) : null;
    /// <summary>The top-right of a view: the watch-along badge.</summary>
    internal Rect? FocusBadge() => _bannerShown ? Grow(LocalRect(_banner), 60 * _uiScale) : null;
    /// <summary>The viewer count under the large view's name, with its list while open.</summary>
    internal Rect? FocusViewers() => Grow(Union(LocalRect(_viewers), _viewerHover ? LocalRect(_viewerList) : null), 24 * _uiScale);
    /// <summary>The audience row.</summary>
    internal Rect? FocusAudience() => Grow(AudienceAvatars(), 16 * _uiScale);
    // The occupied part of the row (the row itself spans the screen).
    private Rect? AudienceAvatars() => Union(LocalRect(_deadBar.Ends.First), LocalRect(_deadBar.Ends.Last));
    /// <summary>The audience row with the room above it where emotes and the emote row appear.</summary>
    internal Rect? FocusAudienceRoom() => AudienceAvatars() is { } row ? Rect.MinMaxRect(row.xMin - 60 * _uiScale, row.yMin - 8 * _uiScale, row.xMax + 60 * _uiScale, row.yMax + 150 * _uiScale) : null;
    /// <summary>One audience member's avatar.</summary>
    internal Rect? FocusAudienceChip(SplitScreenKey key) => LocalRect(_deadBar.ChipRect(key));
    /// <summary>One audience member with its hover card.</summary>
    internal Rect? FocusAudienceMember(SplitScreenKey key) => Grow(Union(LocalRect(_deadBar.ChipRect(key)), LocalRect(_deadBar.CardRect)), 40 * _uiScale);

    private const int ViewerRows = 12;
    private readonly RectTransform _viewers, _viewerList;
    private readonly TextMeshProUGUI _viewerText, _viewerNote;
    private readonly TextMeshProUGUI[] _viewerColumns = new TextMeshProUGUI[3];
    private readonly List<string> _viewerNames = new List<string>(32);
    private SplitScreenKey? _viewersView;
    private int _viewerUnknown = -1;
    private bool _viewerChinese, _viewerHover;

    /// <summary>
    /// How many watch <paramref name="view"/> (the large view; null hides the label) and who; <paramref name="unknown"/>
    /// audience members run no mod, so whom they watch is not known.
    /// </summary>
    internal void SetViewers(SplitScreenKey? view, IReadOnlyList<string> names, int unknown, bool chinese)
    {
        _viewersView = view;
        bool same = unknown == _viewerUnknown && chinese == _viewerChinese && names.Count == _viewerNames.Count;
        for (int i = 0; same && i < names.Count; i++) same = names[i] == _viewerNames[i];
        if (same) return;
        _viewerNames.Clear(); _viewerNames.AddRange(names); _viewerUnknown = unknown; _viewerChinese = chinese;
        _viewerText.text = chinese ? $"{names.Count} 人正在观看" : $"{names.Count} watching";
        if (_viewerHover) LayoutViewerList();
    }
    /// <summary>Whether the pointer is on the viewer count; its list opens while it is.</summary>
    internal bool HitTestViewers(Vector2 screenPoint)
        => _visible && _viewers.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(_viewers, screenPoint, CanvasCamera(_hudCanvas));
    internal void SetViewersHover(bool hover)
    {
        if (hover == _viewerHover) return;
        _viewerHover = hover; _viewerList.gameObject.SetActive(hover);
        if (hover) LayoutViewerList();
    }

    // Names in columns of twelve; a muted note counts the audience whose target is unknown.
    private void LayoutViewerList()
    {
        float u = _uiScale, pad = Mathf.Round(8 * u), font = Mathf.Round(12 * u);
        int columns = Mathf.Max(1, (_viewerNames.Count + ViewerRows - 1) / ViewerRows);
        float columnWidth = 0, namesHeight = 0;
        for (int c = 0; c < _viewerColumns.Length; c++)
        {
            var column = _viewerColumns[c];
            bool used = c < columns && _viewerNames.Count > 0;
            column.gameObject.SetActive(used);
            if (!used) continue;
            column.fontSize = font; column.lineSpacing = 0;
            column.text = string.Join("\n", _viewerNames.GetRange(c * ViewerRows, Mathf.Min(ViewerRows, _viewerNames.Count - c * ViewerRows)));
            var preferred = column.GetPreferredValues(column.text, 4096, 0);
            columnWidth = Mathf.Max(columnWidth, Mathf.Ceil(preferred.x)); namesHeight = Mathf.Max(namesHeight, Mathf.Ceil(preferred.y));
        }
        string note = _viewerNames.Count == 0 ? (_viewerChinese ? "没有已知的观众" : "No known viewers") : string.Empty;
        if (_viewerUnknown > 0) note += (note.Length > 0 ? "\n" : string.Empty)
            + (_viewerChinese ? $"另有 {_viewerUnknown} 位未装模组的观众无法统计" : $"{_viewerUnknown} more without the mod can't be counted");
        _viewerNote.text = note; _viewerNote.fontSize = font; _viewerNote.gameObject.SetActive(note.Length > 0);
        float noteWidth = note.Length > 0 ? Mathf.Ceil(_viewerNote.GetPreferredValues(note, 4096, 0).x) : 0;
        float noteHeight = note.Length > 0 ? Mathf.Ceil(_viewerNote.GetPreferredValues(note, 4096, 0).y) : 0;
        float gap = Mathf.Round(14 * u), width = Mathf.Max(columns * columnWidth + (columns - 1) * gap, noteWidth) + pad * 2;
        for (int c = 0; c < columns && c < _viewerColumns.Length; c++)
        {
            var rect = _viewerColumns[c].rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(pad + c * (columnWidth + gap), -pad); rect.sizeDelta = new Vector2(columnWidth + 2, namesHeight + 2);
        }
        var noteRect = _viewerNote.rectTransform;
        noteRect.anchorMin = noteRect.anchorMax = noteRect.pivot = new Vector2(0, 1);
        noteRect.anchoredPosition = new Vector2(pad, -pad - namesHeight - (namesHeight > 0 && noteHeight > 0 ? Mathf.Round(4 * u) : 0));
        noteRect.sizeDelta = new Vector2(width - pad * 2, noteHeight);
        _viewerList.sizeDelta = new Vector2(width, pad * 2 + namesHeight + noteHeight + (namesHeight > 0 && noteHeight > 0 ? Mathf.Round(4 * u) : 0));
        _viewerList.anchoredPosition = new Vector2(0, -Mathf.Round(4 * u));
    }

    // The label rides on the large view's frame just under its name plate, like the watch badge.
    private void PlaceViewers()
    {
        Tile? tile = _viewersView.HasValue && _tiles.TryGetValue(_viewersView.Value, out var found) && !found.Retiring ? found : null;
        bool show = tile != null;
        if (_viewers.gameObject.activeSelf != show) _viewers.gameObject.SetActive(show);
        if (tile == null) { SetViewersHover(false); return; }
        if (_viewers.parent != tile.Decoration) { _viewers.SetParent(tile.Decoration, false); _viewers.SetAsLastSibling(); }
        int size = Mathf.Max(11, tile.FontSize - 2);
        if (_viewerText.fontSize != size) _viewerText.fontSize = size;
        float height = size + 6, width = Mathf.Ceil(_viewerText.GetPreferredValues(_viewerText.text, 4096, 0).x) + 14;
        _viewers.sizeDelta = new Vector2(width, height);
        _viewerText.rectTransform.anchorMin = Vector2.zero; _viewerText.rectTransform.anchorMax = Vector2.one;
        _viewerText.rectTransform.offsetMin = new Vector2(7, 0); _viewerText.rectTransform.offsetMax = new Vector2(-4, 0);
        _viewers.anchoredPosition = new Vector2(PlateInset, -(PlateInset + tile.FontSize + 8 + 4));
    }

    // The badge rides on the followed view's frame, so it moves and resizes with it.
    private void PlaceWatchBadge()
    {
        Transform? frame = _bannerView.HasValue && _tiles.TryGetValue(_bannerView.Value, out var tile) && !tile.Retiring ? tile.Decoration : null;
        if (frame == null) { _bannerShown = false; return; }
        if (_banner.parent != frame) { _banner.SetParent(frame, false); _banner.SetAsLastSibling(); }
        _banner.anchoredPosition = new Vector2(-Mathf.Round(8 * _uiScale), -Mathf.Round(8 * _uiScale));
    }
    /// <summary>The followed player switched target: the banner flashes once.</summary>
    internal void PulseWatchTogether() => _bannerPulse = 0;

    // The audio view's player (enlarged or orange-framed) speaking: its plate edge fades in green and flickers gently,
    // then fades out after speech. Other views stay calm, so a full grid does not flash all over.
    private SplitScreenKey? _audioKey;
    private void AnimateSpeaking(Tile tile, float deltaTime)
    {
        float target = _showSpeaking && !tile.Retiring && tile.Key == _audioKey && (_speaking.ContainsKey(tile.Source) || _speaking.ContainsKey(tile.Key)) ? 1 : 0;
        if (target == 0 && tile.Speak == 0) return;
        tile.Speak = Mathf.MoveTowards(tile.Speak, target, deltaTime / (target > tile.Speak ? .12f : .45f));
        float flicker = .7f + .3f * Mathf.Sin(_clock * 12.5f);
        var color = new Color(SpeakingEdge.r, SpeakingEdge.g, SpeakingEdge.b, tile.Speak * flicker);
        foreach (var edge in tile.SpeakEdges) edge.color = color;
    }

    /// <summary>A column of key caps at the right edge instead of rows of text; it fades out while idle.</summary>
    // accentRow: the early-leave vote's row, orange to stand out from the rest (-1: none).
    internal void SetKeyHints(List<(string Label, string Keys)> rows, bool enabled, int accentRow = -1)
    {
        _hintsEnabled = enabled;
        bool same = rows.Count == _hintRows.Count && accentRow == _hintAccent;
        _hintAccent = accentRow;
        for (int i = 0; same && i < rows.Count; i++) same = rows[i].Label == _hintRows[i].Label && rows[i].Keys == _hintRows[i].Keys;
        if (same) return;
        _hintRows.Clear(); _hintRows.AddRange(rows); _hintsDirty = true; WakeKeyHints();
    }
    internal void WakeKeyHints() => _hintAwake = HintSeconds;
    /// <summary>Hides the key hints at once (no fade), e.g. as the demo starts.</summary>
    internal void HideKeyHintsNow() { _hintsEnabled = false; _hintAwake = 0; _hintGroup.alpha = 0; _hints.gameObject.SetActive(false); }
    internal float KeyHintOpacity => _hintGroup.alpha;

    // The bottom of the screen belongs to the dead-player bar, so the hints stand in a column at the right edge.
    private void LayoutKeyHints()
    {
        _hintsDirty = false;
        float font = Mathf.Round(13 * _uiScale), pad = Mathf.Round(6 * _uiScale), cap = Mathf.Round(20 * _uiScale);
        // A row without keys is the status line: no cap, brighter, across the column.
        float keysWidth = cap, labelsWidth = 0, statusWidth = 0;
        for (int i = 0; i < _hintRows.Count; i++)
        {
            if (i == _hintItems.Count)
            {
                var capImage = CreateImage("Key cap " + i, _hints, new Color(1, 1, 1, .13f));
                var key = CreateText("Key " + i, capImage.rectTransform); ApplyFont(key); key.alignment = TextAlignmentOptions.Center;
                key.color = new Color(1, .97f, .92f, 1);
                var label = CreateText("Key action " + i, _hints); ApplyFont(label); label.alignment = TextAlignmentOptions.MidlineLeft;
                label.color = new Color(.78f, .8f, .82f, 1);
                _hintItems.Add((capImage, key, label));
            }
            var item = _hintItems[i];
            bool status = _hintRows[i].Keys.Length == 0;
            item.Cap.gameObject.SetActive(!status); item.Label.gameObject.SetActive(true);
            item.Key.text = _hintRows[i].Keys; item.Label.text = _hintRows[i].Label;
            item.Key.fontSize = font; item.Label.fontSize = font;
            bool accent = i == _hintAccent && !status;
            item.Cap.color = accent ? VoteOrange : new Color(1, 1, 1, .13f);
            item.Key.color = accent ? VoteInk : new Color(1, .97f, .92f, 1);
            item.Label.color = accent ? VoteOrange : status ? new Color(1, .97f, .92f, 1) : new Color(.78f, .8f, .82f, 1);
            float labelWidth = Mathf.Ceil(item.Label.GetPreferredValues(item.Label.text, 4096, 0).x);
            if (status) { statusWidth = Mathf.Max(statusWidth, labelWidth); continue; }
            keysWidth = Mathf.Max(keysWidth, Mathf.Ceil(item.Key.GetPreferredValues(item.Key.text, 4096, 0).x) + pad * 2);
            labelsWidth = Mathf.Max(labelsWidth, labelWidth);
        }
        float width = Mathf.Max(pad * 3 + keysWidth + labelsWidth, pad * 2 + statusWidth);
        float y = pad;
        if (_hintAccentBack == null)
        {
            _hintAccentBack = CreateImage("Key hint accent", _hints, new Color(1, .45f, .12f, .16f));
            _hintAccentBack.transform.SetAsFirstSibling(); TopLeft(_hintAccentBack.rectTransform);
        }
        _hintAccentBack.gameObject.SetActive(_hintAccent >= 0 && _hintAccent < _hintRows.Count);
        _hintAccentBack.rectTransform.anchoredPosition = new Vector2(pad / 2, -pad / 2 - Mathf.Max(0, _hintAccent) * (cap + pad));
        _hintAccentBack.rectTransform.sizeDelta = new Vector2(width - pad, cap + pad);
        for (int i = 0; i < _hintRows.Count; i++)
        {
            var item = _hintItems[i];
            var capRect = item.Cap.rectTransform; TopLeft(capRect);
            capRect.anchoredPosition = new Vector2(pad, -y); capRect.sizeDelta = new Vector2(keysWidth, cap);
            Stretch(item.Key.rectTransform);
            var labelRect = item.Label.rectTransform; TopLeft(labelRect);
            bool status = _hintRows[i].Keys.Length == 0;
            labelRect.anchoredPosition = new Vector2(status ? pad : pad * 2 + keysWidth, -y);
            labelRect.sizeDelta = new Vector2(status ? width - pad * 2 : labelsWidth, cap);
            y += cap + pad;
        }
        for (int i = _hintRows.Count; i < _hintItems.Count; i++) { _hintItems[i].Cap.gameObject.SetActive(false); _hintItems[i].Label.gameObject.SetActive(false); }
        _hints.sizeDelta = new Vector2(width, y);
        float margin = Mathf.Round(10 * _uiScale);
        _hints.anchoredPosition = new Vector2(-margin, 0);
        // Centred on the right edge: a long list shrinks to stay clear of the dead bar (and the preview toolbar) on
        // both sides instead of running off the screen.
        float room = _size.y - 2 * (margin + Mathf.Max(_deadBar.Height, _testVisible ? SplitScreenTestToolbar.ReservedHeight(_size) : 0));
        _hints.localScale = Vector3.one * Mathf.Min(1, room / y);
    }

    private void TickKeyHints(float deltaTime)
    {
        if (_hintsDirty && _hintRows.Count > 0) LayoutKeyHints();
        _hintAwake -= deltaTime;
        float target = _hintsEnabled && _hintRows.Count > 0 && _hintAwake > 0 ? 1 : 0;
        float alpha = Mathf.MoveTowards(_hintGroup.alpha, target, deltaTime / HintFadeSeconds);
        if (alpha != _hintGroup.alpha) _hintGroup.alpha = alpha;
        bool shown = alpha > 0;
        if (_hints.gameObject.activeSelf != shown) _hints.gameObject.SetActive(shown);
    }

    private void Relayout()
    {
        int focusIndex = -1;
        for (int i = 0; i < _participants.Count; i++) if (_focused == _participants[i].Key) focusIndex = i;
        float top = Mathf.Max(_testVisible ? SplitScreenTestToolbar.ReservedHeight(_size) : 0, LeaveVoteReserve), bottom = _deadBar.Height;
        // The tiled chat panel: tall, on the left like an enlarged view, the others making room beside it.
        float height = _size.y - SplitScreenLayout.Margin * 2 - top - bottom;
        float chat = _chatDocked ? Mathf.Round(Mathf.Min(height * .56f, (_size.x - SplitScreenLayout.Margin * 2) * .3f)) : 0;
        if (chat > 0) _chatDock = new Rect(SplitScreenLayout.Margin, bottom + SplitScreenLayout.Margin, chat, height);
        var rects = SplitScreenLayout.Calculate(_participants.Count, _size.x, _size.y, focusIndex, top, bottom, chat);
        for (int i = 0; i < _participants.Count; i++)
        {
            var participant = _participants[i];
            if (!_tiles.TryGetValue(participant.Key, out var tile))
            {
                var rect = rects[i];
                tile = CreateTile(participant.Key, new SplitScreenTileRect(rect.CenterX, rect.CenterY, rect.Width * .92f));
                _tiles.Add(participant.Key, tile);
            }
            tile.Motion.Retarget(rects[i]);
        }
        _layoutDirty = false;
    }

    /// <summary>Pointer-mode hover; only a lighter frame, never a layout change.</summary>
    internal void SetHover(SplitScreenKey? key)
    {
        if (_hover == key) return;
        _hover = key;
        foreach (var tile in _tiles.Values) if (!tile.Retiring) Highlight(tile, tile.Audio);
    }

    internal bool TryGetTargetSize(SplitScreenKey key, out Vector2 size)
    {
        if (_tiles.TryGetValue(key, out var tile) && !tile.Retiring)
        { size = new Vector2(tile.Motion.Target.Width, tile.Motion.Target.Height); return true; }
        size = default; return false;
    }

    internal void SetTexture(SplitScreenKey key, Texture? texture, bool crossfade = true)
    {
        if (!_tiles.TryGetValue(key, out var tile) || tile.Picture.texture == texture) return;
        tile.Previous.texture = crossfade ? tile.Picture.texture : null;
        tile.Picture.texture = texture;
        tile.Crossfade = tile.Previous.texture != null && texture != null ? 0 : CrossfadeSeconds;
    }

    /// <summary>The renderer keeps replaced textures alive while either crossfade layer still uses them.</summary>
    internal bool ReferencesTexture(Texture texture)
    {
        foreach (var tile in _tiles.Values)
            if (tile.Picture.texture == texture || tile.Previous.texture == texture) return true;
        return false;
    }

    internal void Tick(float unscaledDeltaTime)
    {
        _toolbar.Tick(unscaledDeltaTime);
        TickKeyHints(unscaledDeltaTime);
        TickClock(unscaledDeltaTime);
        TickChat(unscaledDeltaTime);
        _deadBar.Tick(unscaledDeltaTime, _speaking);
        if (_bannerShown) PlaceWatchBadge();
        PlaceViewers();
        TickDemo(unscaledDeltaTime);
        float bannerAlpha = Mathf.MoveTowards(_bannerGroup.alpha, _bannerShown ? 1 : 0, unscaledDeltaTime / .25f);
        if (bannerAlpha != _bannerGroup.alpha) _bannerGroup.alpha = bannerAlpha;
        if (_banner.gameObject.activeSelf != bannerAlpha > 0) _banner.gameObject.SetActive(bannerAlpha > 0);
        if (_bannerPulse < 1)
        {
            _bannerPulse = Mathf.Min(1, _bannerPulse + unscaledDeltaTime / .6f);
            float glow = 1 - _bannerPulse;
            _banner.localScale = Vector3.one * (1 + .06f * glow * glow);
            _bannerText.color = Color.Lerp(new Color(.85f, .95f, 1, 1), Color.white, glow);
        }
        _clock += unscaledDeltaTime;
        _remove.Clear();
        foreach (var tile in _tiles.Values)
        {
            bool moved = tile.Motion.Tick(unscaledDeltaTime);
            tile.Age += unscaledDeltaTime;
            tile.Crossfade = Mathf.Min(CrossfadeSeconds, tile.Crossfade + unscaledDeltaTime);
            if (tile.Retiring) tile.Retirement -= unscaledDeltaTime;
            if (tile.Retiring && tile.Retirement <= 0)
            { _remove.Add(tile.Key); continue; }
            float alpha = tile.Retiring ? Mathf.Clamp01(tile.Retirement / RetirementSeconds) : Mathf.Clamp01(tile.Age / RetirementSeconds);
            float mix = Mathf.SmoothStep(0, 1, tile.Crossfade / CrossfadeSeconds);
            if (tile.VideoGroup.alpha != alpha) tile.VideoGroup.alpha = alpha;
            tile.Picture.color = new Color(1, 1, 1, tile.Picture.texture != null ? mix : 0);
            tile.Previous.color = tile.Previous.texture != null ? Color.white : Color.clear;
            if (tile.Crossfade >= CrossfadeSeconds) tile.Previous.texture = null;
            if (tile.DecorationGroup.alpha != alpha) tile.DecorationGroup.alpha = alpha;
            tile.Transition.color = new Color(0, 0, 0, _focused == tile.Key ? _primaryTransitionOpacity : 0);
            // Settled views keep their rect transforms untouched so the canvases are not rebuilt every frame.
            if (moved || tile.FontSize == 0)
            {
                // A resting view lands on whole pixels, so neither its picture nor its label is resampled half a pixel off.
                bool snap = tile.Motion.Settled;
                Place(tile.Video, tile.Motion.Current, snap); Place(tile.Decoration, tile.Motion.Current, snap); PlaceNamePlate(tile, _uiScale);
            }
            if (tile.Pulse < PulseSeconds) AnimatePulse(tile, unscaledDeltaTime);
            AnimateSpeaking(tile, unscaledDeltaTime);
        }
        foreach (var key in _remove)
        { DestroyTile(_tiles[key]); _tiles.Remove(key); _completed.Add(key); }
    }

    internal SplitScreenKey? HitTest(Vector2 screenPoint)
    {
        if (!_visible || (_testVisible && _toolbar.Contains(screenPoint))) return null;
        if (_focused.HasValue && _tiles.TryGetValue(_focused.Value, out var focused) && Contains(focused, screenPoint)) return focused.Key;
        for (int i = _participants.Count - 1; i >= 0; i--)
            if (_tiles.TryGetValue(_participants[i].Key, out var tile) && Contains(tile, screenPoint)) return tile.Key;
        return null;
    }

    private bool Contains(Tile tile, Vector2 point)
        => !tile.Retiring && RectTransformUtility.RectangleContainsScreenPoint(tile.Video, point, CanvasCamera(_videoCanvas));
    private static Camera? CanvasCamera(Canvas canvas) => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

    internal void CopyCompletedRetirementsTo(List<SplitScreenKey> destination)
    { destination.Clear(); destination.AddRange(_completed); _completed.Clear(); }
    internal void SetVisible(bool visible)
    {
        _visible = visible;
        _videoCanvas.gameObject.SetActive(visible);
        _hudCanvas.gameObject.SetActive(visible && _hudVisible);
        _toolbar.SetVisible(visible && _testVisible, immediate: !visible);
    }
    /// <summary>The preview's split-screen toggle: views and labels go, the test toolbar stays to bring them back.</summary>
    internal void SetSuspended(bool suspended)
    {
        _videoCanvas.gameObject.SetActive(_visible && !suspended);
        _hudCanvas.gameObject.SetActive(_visible && _hudVisible && !suspended);
    }
    internal void SetHudVisible(bool visible)
    { _hudVisible = visible; _hudCanvas.gameObject.SetActive(_visible && visible); }
    internal void SetPrimaryTransitionOpacity(float opacity)
    {
        _primaryTransitionOpacity = opacity;
        foreach (var tile in _tiles.Values)
            tile.Transition.color = new Color(0, 0, 0, _focused == tile.Key ? opacity : 0);
    }
    internal void SetTestVisible(bool visible, Action<SplitScreenTestAction, int, SplitScreenKey?>? callback = null)
    {
        if (_testVisible != visible) { _testVisible = visible; _layoutDirty = true; }
        _toolbar.Callback = callback;
        _toolbar.SetVisible(_visible && visible);
    }
    internal void SetTestPanelKey(string key) => _toolbar.SetPanelKey(key);
    internal bool HandleTestPointer(Vector2 point, bool clicked) => _visible && _testVisible && _toolbar.HandlePointer(point, clicked);
    internal void UpdateStatus(string text, bool chinese, int count, bool playing, string quality, SplitScreenKey? selected,
        int mode = -1, bool labels = true, bool thermal = false, bool speaking = false, int audience = 0, bool captions = true)
    {
        _empty.text = chinese ? "暂无存活玩家" : "No surviving players";
        _toolbar.UpdateStatus(text, chinese, count, playing, quality, selected, mode, labels, thermal, speaking, audience, captions);
    }
    internal void Clear()
    {
        foreach (var tile in _tiles.Values) { DestroyTile(tile); _completed.Add(tile.Key); }
        _tiles.Clear(); _participants.Clear(); _live.Clear(); _focused = null; _layoutDirty = true;
        SetBeneath(null); _hintAwake = 0; _hintGroup.alpha = 0; _hints.gameObject.SetActive(false);
        _deadBar.Clear(); _speaking.Clear(); _bannerShown = false; _bannerGroup.alpha = 0; _banner.gameObject.SetActive(false);
        ClearClock(); ClearChat();
    }
    public void Dispose()
    {
        Clear();
        UnityEngine.Object.Destroy(_videoCanvas.gameObject); UnityEngine.Object.Destroy(_hudCanvas.gameObject);
        _toolbar.Dispose(); _demo.Dispose(); DisposeClock();
        if (_material != null) UnityEngine.Object.Destroy(_material);
    }

    private Tile CreateTile(SplitScreenKey key, SplitScreenTileRect initial)
    {
        var video = CreateImage("Split video " + key.SlotId, _videoRoot, Color.black).rectTransform;
        var previous = CreatePicture("Previous picture", video); var picture = CreatePicture("Live picture", video);
        var transition = CreateImage("Focused camera transition", video, Color.clear); Stretch(transition.rectTransform);
        var decoration = new GameObject("Split label " + key.SlotId, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        decoration.SetParent(_hudRoot, false);
        var edges = Frame4("Video border ", decoration, Frame);
        // The ring expands outward once when this player becomes the audio target.
        var pulse = new GameObject("Audio pulse", typeof(RectTransform)).GetComponent<RectTransform>();
        pulse.SetParent(decoration, false); Stretch(pulse);
        var pulseEdges = Frame4("Audio pulse ", pulse, Color.clear);
        // A compact name plate over the picture instead of a full-width bar hiding its top.
        var plate = CreateImage("Player name background", decoration, new Color(0, 0, 0, .58f));
        TopLeft(plate.rectTransform);
        var dot = CreateImage("Audio indicator", plate.rectTransform, AudioFrame);
        dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = dot.rectTransform.pivot = new Vector2(0, .5f);
        var title = CreateText("Player name", plate.rectTransform); ApplyFont(title);
        TopLeft(title.rectTransform); title.alignment = TextAlignmentOptions.MidlineLeft;
        var speak = Frame4("Speaking edge ", plate.rectTransform, Color.clear);
        var tile = new Tile(key, video, previous, picture, transition, decoration, title, edges, initial, plate, dot, pulseEdges, speak);
        Place(video, initial, false); Place(decoration, initial, false);
        Highlight(tile, false);
        return tile;
    }
    private void Highlight(Tile tile, bool audio)
    {
        if (audio && !tile.Audio) tile.Pulse = 0;
        if (!audio) { tile.Pulse = PulseSeconds; foreach (var edge in tile.PulseEdges) edge.color = Color.clear; }
        if (audio != tile.Audio) { tile.Audio = audio; tile.FontSize = 0; }
        var color = audio ? AudioFrame : _hover == tile.Key && !tile.Retiring ? HoverFrame : Frame;
        foreach (var edge in tile.Edges) edge.color = color;
        tile.Title.color = audio ? AudioName : Name;
        tile.Dot.gameObject.SetActive(audio);
    }

    /// <summary>Plate size follows the animated width; text is only re-measured when its size or content changes.</summary>
    private static void PlaceNamePlate(Tile tile, float scale)
    {
        float width = tile.Motion.Current.Width;
        int size = Mathf.RoundToInt(Mathf.Clamp(width * .045f, 12 * scale, 16 * scale));
        if (size != tile.FontSize)
        {
            tile.FontSize = size; tile.Title.fontSize = size;
            tile.TextWidth = tile.Title.GetPreferredValues(tile.Title.text, 4096, 0).x;
        }
        float height = size + 8, textLeft = tile.Audio ? Mathf.Round(size * .5f) + 12 : 8;
        float plateWidth = Mathf.Min(textLeft + tile.TextWidth + 8, Mathf.Max(0, width - PlateInset * 2));
        var plate = tile.Plate.rectTransform;
        plate.anchoredPosition = new Vector2(PlateInset, -PlateInset); plate.sizeDelta = new Vector2(plateWidth, height);
        float dot = Mathf.Round(size * .5f);
        tile.Dot.rectTransform.anchoredPosition = new Vector2(7, 0); tile.Dot.rectTransform.sizeDelta = new Vector2(dot, dot);
        tile.Title.rectTransform.anchoredPosition = new Vector2(textLeft, 0);
        tile.Title.rectTransform.sizeDelta = new Vector2(Mathf.Max(0, plateWidth - textLeft - 6), height);
    }

    private static void AnimatePulse(Tile tile, float deltaTime)
    {
        tile.Pulse = Mathf.Min(PulseSeconds, tile.Pulse + deltaTime);
        float t = tile.Pulse / PulseSeconds, eased = 1 - (1 - t) * (1 - t);
        float spread = 2 + 8 * eased;
        var ring = (RectTransform)tile.PulseEdges[0].transform.parent;
        ring.offsetMin = new Vector2(-spread, -spread); ring.offsetMax = new Vector2(spread, spread);
        var color = new Color(AudioFrame.r, AudioFrame.g, AudioFrame.b, .6f * (1 - t) * (1 - t));
        foreach (var edge in tile.PulseEdges) edge.color = color;
    }
    private static void ToFront(Transform transform)
    {
        if (transform.GetSiblingIndex() != transform.parent.childCount - 1) transform.SetAsLastSibling();
    }
    private void ApplyFont(TextMeshProUGUI text)
    { text.font = _font; if (_material != null) text.fontSharedMaterial = _material; }
    private void DestroyTile(Tile tile)
    {
        // The watch badge and viewer count ride on a view's frame; they stay when that view goes.
        if (_banner.parent == tile.Decoration)
        { _banner.SetParent(_hudRoot, false); _bannerShown = false; _bannerGroup.alpha = 0; _banner.gameObject.SetActive(false); }
        if (_viewers.parent == tile.Decoration)
        { _viewers.SetParent(_hudRoot, false); _viewers.gameObject.SetActive(false); SetViewersHover(false); }
        tile.Video.gameObject.SetActive(false); tile.Decoration.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(tile.Video.gameObject); UnityEngine.Object.Destroy(tile.Decoration.gameObject);
    }
    private static void Place(RectTransform rect, SplitScreenTileRect box, bool snap)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(.5f, .5f);
        float left = box.Left, top = box.Top, width = box.Width, height = box.Height;
        if (snap) { left = Mathf.Round(left); top = Mathf.Round(top); width = Mathf.Round(width); height = Mathf.Round(height); }
        rect.anchoredPosition = new Vector2(left + width * .5f, -(top + height * .5f)); rect.sizeDelta = new Vector2(width, height);
    }
    internal static Canvas CreateCanvas(string name, int order)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.hideFlags = HideFlags.DontSave;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
        root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        return canvas;
    }
    internal static Image CreateImage(string name, Transform parent, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false); image.color = color; image.raycastTarget = false; return image;
    }
    private static RawImage CreatePicture(string name, Transform parent)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(parent, false); image.raycastTarget = false; image.color = Color.clear; Stretch(image.rectTransform); return image;
    }
    internal static TextMeshProUGUI CreateText(string name, Transform parent)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false); text.raycastTarget = false; text.richText = false;
        text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Ellipsis; return text;
    }
    internal static Image[] Frame4(string prefix, RectTransform parent, Color color, float thickness = 2)
    {
        var edges = new Image[4];
        for (int i = 0; i < 4; i++) edges[i] = CreateImage(prefix + i, parent, color);
        Edge(edges[0].rectTransform, new Vector2(0, 1), new Vector2(1, 1), thickness, true);
        Edge(edges[1].rectTransform, Vector2.zero, new Vector2(1, 0), thickness, true);
        Edge(edges[2].rectTransform, Vector2.zero, new Vector2(0, 1), thickness, false);
        Edge(edges[3].rectTransform, new Vector2(1, 0), Vector2.one, thickness, false);
        return edges;
    }
    private static void TopLeft(RectTransform rect)
    { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); }
    internal static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Edge(RectTransform rect, Vector2 min, Vector2 max, float thickness, bool horizontal)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = min;
        if (max.x == 1 && min.x == 1) rect.pivot = new Vector2(1, 0);
        if (max.y == 1 && min.y == 1) rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = horizontal ? new Vector2(0, thickness) : new Vector2(thickness, 0);
    }
    private sealed class Tile
    {
        internal readonly SplitScreenKey Key;
        internal readonly RectTransform Video, Decoration;
        internal readonly RawImage Previous, Picture;
        internal readonly Image Transition;
        internal readonly CanvasGroup VideoGroup, DecorationGroup;
        internal readonly TextMeshProUGUI Title;
        internal readonly Image[] Edges, PulseEdges;
        internal readonly Image Plate, Dot;
        internal readonly Image[] SpeakEdges;
        internal float Speak;
        internal SplitScreenKey Source;
        internal readonly SplitScreenTileMotion Motion;
        internal bool Retiring, Audio;
        internal int FontSize;
        internal float Age, Retirement, Crossfade = CrossfadeSeconds, Pulse = PulseSeconds, TextWidth;
        internal Tile(SplitScreenKey key, RectTransform video, RawImage previous, RawImage picture, Image transition, RectTransform decoration,
            TextMeshProUGUI title, Image[] edges, SplitScreenTileRect initial, Image plate, Image dot, Image[] pulseEdges, Image[] speakEdges)
        {
            Key = key; Video = video; Previous = previous; Picture = picture; Transition = transition; Decoration = decoration;
            VideoGroup = video.gameObject.AddComponent<CanvasGroup>();
            DecorationGroup = decoration.GetComponent<CanvasGroup>(); Title = title; Edges = edges; Motion = new SplitScreenTileMotion(initial);
            Plate = plate; Dot = dot; PulseEdges = pulseEdges; SpeakEdges = speakEdges;
        }
    }
}
