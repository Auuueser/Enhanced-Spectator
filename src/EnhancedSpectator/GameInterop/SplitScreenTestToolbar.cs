using System;
using System.Collections.Generic;
using EnhancedSpectator.Features.SplitScreen;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>Local preview controls, manually hit-tested so the native ESC buttons never receive a preview click.</summary>
internal sealed class SplitScreenTestToolbar : IDisposable
{
    internal const float Height = 268f;
    private const float Width = 936, Row = 26, Left = 58;
    internal readonly Canvas Canvas;
    internal Action<SplitScreenTestAction, int, SplitScreenKey?>? Callback;
    private readonly RectTransform _panel;
    private readonly TextMeshProUGUI _title, _status, _hint, _audienceHint, _panelKey, _panelLabel;
    private readonly TextMeshProUGUI[] _sections = new TextMeshProUGUI[6];
    private readonly List<Control> _controls = new List<Control>();
    private TMP_FontAsset? _font;
    private Material? _material;
    private Control? _hovered;
    private bool _chinese, _playing, _labels = true, _thermal, _speaking, _captions = true, _statusInitialized;
    private int _count, _mode = -1, _audience;
    private string _quality = string.Empty;
    private SplitScreenKey? _selected;
    private EventSystem? _eventSystem;
    private GameObject? _previousSelection;
    private bool _previousNavigationEvents;
    private static readonly Color Normal = new Color(.12f, .115f, .11f, 1), Hover = new Color(.22f, .17f, .13f, 1),
        Active = new Color(.6f, .27f, .07f, 1), ActiveHover = new Color(.72f, .33f, .1f, 1),
        Danger = new Color(.24f, .08f, .06f, 1), DangerHover = new Color(.42f, .12f, .08f, 1),
        Label = new Color(.9f, .88f, .84f, 1), ActiveLabel = new Color(1, .97f, .92f, 1), Disabled = new Color(.42f, .42f, .42f, 1),
        Muted = new Color(.58f, .6f, .62f, 1), Status = new Color(.74f, .76f, .78f, 1), Accent = new Color(.96f, .55f, .2f, 1);

    internal SplitScreenTestToolbar()
    {
        Canvas = SplitScreenView.CreateCanvas("EnhancedSpectator Split Test Controls", 32002);
        Canvas.gameObject.AddComponent<GraphicRaycaster>();
        // The event system stops at this transparent surface. Our explicit hit tests dispatch one action only.
        var blocker = SplitScreenView.CreateImage("Preview input surface", Canvas.transform, Color.clear);
        blocker.raycastTarget = true; blocker.canvasRenderer.cullTransparentMesh = false;
        SplitScreenView.Stretch(blocker.rectTransform);
        _panel = SplitScreenView.CreateImage("Preview toolbar", Canvas.transform, new Color(.03f, .032f, .037f, .97f)).rectTransform;
        _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(.5f, 1);
        _panel.anchoredPosition = new Vector2(0, -8); _panel.sizeDelta = new Vector2(Width, Height - 8);
        var border = new Color(Accent.r, Accent.g, Accent.b, .35f);
        Fill("Toolbar edge top", 0, 0, Width, 1, border); Fill("Toolbar edge bottom", 0, Height - 9, Width, 1, border);
        Fill("Toolbar edge left", 0, 0, 1, Height - 8, border); Fill("Toolbar edge right", Width - 1, 0, 1, Height - 8, border);
        Fill("Toolbar divider", 12, 50, Width - 24, 1, new Color(Accent.r, Accent.g, Accent.b, .18f));
        _title = Text("Preview title", 12, 6, 380, 22, 16);
        _hint = Text("Preview exit hint", 400, 6, Width - 552, 22, 12); _hint.color = Muted; _hint.alignment = TextAlignmentOptions.MidlineRight;
        // Top right: the key that hides this panel.
        var panelCap = SplitScreenView.CreateImage("Preview panel key cap", _panel, new Color(1, 1, 1, .13f)).rectTransform;
        Place(panelCap, Width - 136, 8, 40, 18);
        _panelKey = SplitScreenView.CreateText("Preview panel key", panelCap); _panelKey.fontSize = 12; _panelKey.color = ActiveLabel;
        _panelKey.alignment = TextAlignmentOptions.Center; SplitScreenView.Stretch(_panelKey.rectTransform);
        _panelLabel = Text("Preview panel key label", Width - 92, 6, 80, 22, 12); _panelLabel.color = Muted;
        _status = Text("Preview measured status", 12, 28, Width - 24, 18, 13); _status.color = Status;
        for (int i = 0; i < 6; i++) { _sections[i] = Text("Preview section " + i, 12, 58 + i * 34, Left - 16, Row, 12); _sections[i].color = Muted; }

        int[] counts = { 0, 1, 2, 3, 4, 5, 8, 16, 31 };
        for (int i = 0; i < counts.Length; i++)
        {
            int count = counts[i];
            Add("Count " + count, Left + i * 44, 58, 40, () => count.ToString(), () => Send(SplitScreenTestAction.SetCount, count), active: () => _count == count);
        }
        Add("Decrease count", Left + 400, 58, 36, () => "−", () => Send(SplitScreenTestAction.SetCount, Mathf.Max(0, _count - 1)), () => _count > 0);
        Add("Increase count", Left + 440, 58, 36, () => "+", () => Send(SplitScreenTestAction.SetCount, Mathf.Min(31, _count + 1)), () => _count < 31);
        Add("Toggle labels", 560, 58, 176, () => Tr("名称标签", "Name labels"), () => Send(SplitScreenTestAction.ToggleHud), active: () => _labels);
        Add("Toggle thermal", 740, 58, 184, () => Tr("热成像", "Thermal"), () => Send(SplitScreenTestAction.ToggleThermal), active: () => _thermal);

        Add("Kill selected", Left, 92, 122, () => Tr("选中窗死亡", "Kill selected"), () => Send(SplitScreenTestAction.KillSelected), () => _selected.HasValue);
        Add("Kill random", Left + 126, 92, 110, () => Tr("随机死亡", "Random death"), () => Send(SplitScreenTestAction.KillRandom), () => _count > 0);
        Add("Revive", Left + 240, 92, 90, () => Tr("复活", "Revive"), () => Send(SplitScreenTestAction.Revive), () => _count < 31);
        Add("Reset", Left + 334, 92, 90, () => Tr("重置", "Reset"), () => Send(SplitScreenTestAction.Reset));
        Add("Playback", Left + 428, 92, 150, () => Tr(_playing ? "暂停自动减员" : "自动减员", _playing ? "Pause sequence" : "Auto deaths"),
            () => Send(SplitScreenTestAction.TogglePlayback), active: () => _playing);
        Add("Preview report", Left + 582, 92, 86, () => Tr("结算画面", "Report"), () => Send(SplitScreenTestAction.ShowReport));
        Add("Preview leave vote", Left + 672, 92, 86, () => Tr("投票演示", "Leave vote"), () => Send(SplitScreenTestAction.ShowLeaveVote));
        Add("Exit", 820, 92, 104, () => Tr("退出测试", "Exit preview"), () => Send(SplitScreenTestAction.Exit), danger: true);

        string[] chinese = { "原版", "自由视角", "第三人称", "第一人称", "电影", "穿梭", "监视器" };
        string[] english = { "Vanilla", "Freecam", "Third", "First", "Cinema", "Travelling", "Monitor" };
        for (int i = 0; i < chinese.Length; i++)
        {
            int mode = i;
            Add("Preview mode " + mode, Left + i * 88, 126, 84, () => _chinese ? chinese[mode] : english[mode],
                () => Send(SplitScreenTestAction.SelectMode, mode), active: () => _mode == mode);
        }
        Add("Preview quality", 680, 126, 244, QualityLabel, () => Send(SplitScreenTestAction.CycleQuality));

        // Social features run locally on the preview identities: no network, nothing reaches other players.
        // The round's end (bets settling, the review notice) plays in the Bets and Reviews demo chapters.
        Add("Preview speaking", Left, 160, 170, () => Tr("模拟说话", "Simulate voices"), () => Send(SplitScreenTestAction.ToggleSpeaking), active: () => _speaking);
        Add("Preview audience emote", Left + 174, 160, 170, () => Tr("观众发表情", "Audience emote"), () => Send(SplitScreenTestAction.AudienceEmote));
        Add("Preview emotes", Left + 348, 160, 170, () => Tr("表情条", "Emote picker"), () => Send(SplitScreenTestAction.OpenEmotes));
        Add("Preview bets", Left + 522, 160, 170, () => Tr("观众竞猜", "Audience bets"), () => Send(SplitScreenTestAction.OpenBets));
        Add("Preview ratings", Left + 696, 160, 170, () => Tr("同事评估", "Colleague review"), () => Send(SplitScreenTestAction.OpenRatings));

        // Audience-only identities for the audience row, bets and reviews; every fourth plays a player without the mod.
        int[] audiences = { 0, 1, 2, 4, 8, 16, 31 };
        for (int i = 0; i < audiences.Length; i++)
        {
            int audience = audiences[i];
            Add("Audience " + audience, Left + i * 44, 194, 40, () => audience.ToString(), () => Send(SplitScreenTestAction.SetAudience, audience), active: () => _audience == audience);
        }
        Add("Decrease audience", Left + 312, 194, 36, () => "−", () => Send(SplitScreenTestAction.SetAudience, Mathf.Max(0, _audience - 1)), () => _audience > 0);
        Add("Increase audience", Left + 352, 194, 36, () => "+", () => Send(SplitScreenTestAction.SetAudience, Mathf.Min(31, _audience + 1)), () => _audience < 31);
        _audienceHint = Text("Audience hint", Left + 400, 194, Width - Left - 412, Row, 12); _audienceHint.color = Muted;

        // Demo: the whole tour or one chapter, played without the toolbar; ESC stops it.
        string[] chapterZh = { "完整演示", "分屏观战", "视角热成像", "谁在说话", "观众席", "表情", "观众竞猜", "同事评估" };
        string[] chapterEn = { "Full demo", "Split view", "Cameras", "Speaking", "Audience", "Emotes", "Bets", "Reviews" };
        for (int i = 0; i < chapterZh.Length; i++)
        {
            int chapter = i - 1;
            Add("Demo " + chapter, Left + i * 86, 228, 83, () => _chinese ? chapterZh[chapter + 1] : chapterEn[chapter + 1], () => Send(SplitScreenTestAction.Demo, chapter));
        }
        Add("Demo captions", Left + chapterZh.Length * 86, 228, 866 - chapterZh.Length * 86, () => Tr("字幕", "Captions"), () => Send(SplitScreenTestAction.ToggleCaptions), active: () => _captions);
        SetVisible(false);
    }

    internal void ConfigureFont(TMP_FontAsset? font, Material? material)
    {
        _font = font; _material = material;
        ApplyFont(_title); ApplyFont(_status); ApplyFont(_hint); ApplyFont(_audienceHint); ApplyFont(_panelKey); ApplyFont(_panelLabel);
        foreach (var section in _sections) ApplyFont(section);
        foreach (var control in _controls) ApplyFont(control.Label);
    }
    internal void SetSize(Vector2 size) => _panel.localScale = Vector3.one * Scale(size);
    // Grows with the screen like the labels, but never wider than the screen.
    private static float Scale(Vector2 size) => Mathf.Min(SpectatorTextStyle.UiScale(size), (size.x - 24) / Width);
    /// <summary>Screen height the toolbar occupies, including its margins, for the view layout.</summary>
    internal static float ReservedHeight(Vector2 size) => 8 + (Height - 8) * Scale(size) + 8;
    internal void UpdateStatus(string text, bool chinese, int count, bool playing, string quality, SplitScreenKey? selected,
        int mode = -1, bool labels = true, bool thermal = false, bool speaking = false, int audience = 0, bool captions = true)
    {
        bool titleChanged = !_statusInitialized || _chinese != chinese || _count != count;
        bool controlsChanged = titleChanged || _playing != playing || _quality != quality || _selected != selected
            || _mode != mode || _labels != labels || _thermal != thermal || _speaking != speaking || _audience != audience || _captions != captions;
        _chinese = chinese; _count = count; _playing = playing; _quality = quality; _selected = selected;
        _mode = mode; _labels = labels; _thermal = thermal; _speaking = speaking; _audience = audience; _captions = captions;
        _statusInitialized = true;
        if (titleChanged)
        {
            _title.text = Tr($"分屏测试 · {count} 个窗口", $"Split-screen preview · {count} views");
            _hint.text = Tr("ESC 切换角色操作 / 面板操作 · 仅“退出测试”会结束", "ESC: control player / use panel · only Exit preview ends");
            _panelLabel.text = Tr("隐藏面板", "Hide panel");
            string[] sections = chinese ? new[] { "人数", "事件", "视角", "社交", "观众", "演示" } : new[] { "Count", "Events", "Camera", "Social", "Crowd", "Demo" };
            for (int i = 0; i < 6; i++) _sections[i].text = sections[i];
            _audienceHint.text = Tr("观众席另加的阵亡观众 · 每第 4 位模拟未装模组 · 点击头像一起看", "Extra audience · every 4th: no mod · click to watch along");
        }
        if (_status.text != text) _status.text = text;
        if (!controlsChanged) return;
        foreach (var control in _controls)
        {
            string label = control.Text(); if (control.Label.text != label) control.Label.text = label;
            Paint(control);
        }
    }
    internal bool Contains(Vector2 point) => _shown && Canvas.gameObject.activeInHierarchy
        && RectTransformUtility.RectangleContainsScreenPoint(_panel, point, Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera);
    internal bool HandlePointer(Vector2 point, bool clicked)
    {
        if (!_shown || !Canvas.gameObject.activeInHierarchy) return false;
        var camera = Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera;
        Control? hit = null;
        foreach (var control in _controls)
            if (RectTransformUtility.RectangleContainsScreenPoint(control.Image.rectTransform, point, camera)) { hit = control; break; }
        if (hit != _hovered)
        {
            var previous = _hovered; _hovered = hit;
            if (previous != null) Paint(previous);
            if (hit != null) Paint(hit);
        }
        if (hit != null && clicked && hit.Enabled()) { hit.Click(); return true; }
        return Contains(point);
    }
    // Shown and hidden with a short fade. Input (its clicks, the game menu's keyboard navigation) changes hands at
    // once; only the picture fades.
    private const float FadeSeconds = .25f;
    private CanvasGroup? _group;
    private bool _shown;
    /// <summary>The key that hides the panel, as its top-right corner shows it.</summary>
    internal void SetPanelKey(string key) { if (_panelKey.text != key) _panelKey.text = key; }
    // immediate: gone at once (the split-screen closing), not faded by ticks that will not come.
    internal void SetVisible(bool visible, bool immediate = false)
    {
        _group ??= Canvas.gameObject.AddComponent<CanvasGroup>();
        // A fading panel no longer blocks the pointer.
        _group.blocksRaycasts = visible;
        if (!visible && immediate && Canvas.gameObject.activeSelf) { _group.alpha = 0; Canvas.gameObject.SetActive(false); }
        if (_shown == visible) return;
        _shown = visible;
        if (visible)
        {
            _eventSystem = EventSystem.current;
            if (_eventSystem != null)
            {
                _previousNavigationEvents = _eventSystem.sendNavigationEvents;
                _previousSelection = _eventSystem.currentSelectedGameObject;
                _eventSystem.sendNavigationEvents = false;
                _eventSystem.SetSelectedGameObject(null);
            }
            if (!Canvas.gameObject.activeSelf) { _group.alpha = 0; Canvas.gameObject.SetActive(true); }
        }
        else
        {
            if (_eventSystem != null)
            {
                _eventSystem.sendNavigationEvents = _previousNavigationEvents;
                _eventSystem.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
            }
            _eventSystem = null; _previousSelection = null;
        }
    }
    internal void Tick(float deltaTime)
    {
        if (_group == null || !Canvas.gameObject.activeSelf) return;
        _group.alpha = Mathf.MoveTowards(_group.alpha, _shown ? 1 : 0, deltaTime / FadeSeconds);
        if (!_shown && _group.alpha <= 0) Canvas.gameObject.SetActive(false);
    }
    public void Dispose()
    { SetVisible(false); UnityEngine.Object.Destroy(Canvas.gameObject); }

    private void Paint(Control control)
    {
        bool enabled = control.Enabled(), hover = enabled && control == _hovered, active = control.Active();
        control.Image.color = control.Danger ? hover ? DangerHover : Danger : active ? hover ? ActiveHover : Active : hover ? Hover : Normal;
        control.Label.color = !enabled ? Disabled : active ? ActiveLabel : Label;
    }
    private string Tr(string chinese, string english) => _chinese ? chinese : english;
    private string QualityLabel() => Tr("渲染档位：", "Rendering: ") + _quality;
    private void Send(SplitScreenTestAction action, int value = 0) => Callback?.Invoke(action, value, _selected);
    private void Add(string name, float x, float y, float width, Func<string> text, Action click, Func<bool>? enabled = null,
        Func<bool>? active = null, bool danger = false)
    {
        var image = SplitScreenView.CreateImage(name, _panel, Normal);
        Place(image.rectTransform, x, y, width, Row);
        var label = Text(name + " label", x + 3, y, width - 6, Row, 13);
        label.alignment = TextAlignmentOptions.Center;
        var control = new Control(image, label, text, click, enabled ?? (() => true), active ?? (() => false), danger);
        _controls.Add(control); Paint(control);
    }
    private void Fill(string name, float x, float y, float width, float height, Color color)
        => Place(SplitScreenView.CreateImage(name, _panel, color).rectTransform, x, y, width, height);
    private TextMeshProUGUI Text(string name, float x, float y, float width, float height, float fontSize)
    {
        var text = SplitScreenView.CreateText(name, _panel); text.fontSize = fontSize; text.color = Label;
        text.alignment = TextAlignmentOptions.MidlineLeft; ApplyFont(text); Place(text.rectTransform, x, y, width, height); return text;
    }
    private void ApplyFont(TextMeshProUGUI text)
    { text.font = _font; if (_material != null) text.fontSharedMaterial = _material; }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
    private sealed class Control
    {
        internal readonly Image Image;
        internal readonly TextMeshProUGUI Label;
        internal readonly Func<string> Text;
        internal readonly Action Click;
        internal readonly Func<bool> Enabled, Active;
        internal readonly bool Danger;
        internal Control(Image image, TextMeshProUGUI label, Func<string> text, Action click, Func<bool> enabled, Func<bool> active, bool danger)
        { Image = image; Label = label; Text = text; Click = click; Enabled = enabled; Active = active; Danger = danger; }
    }
}
