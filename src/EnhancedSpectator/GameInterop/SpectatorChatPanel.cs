using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// A spectator's chat panel, in place of the game's chat box: the recent messages and the line being typed. It shows
/// while the line is typed and, when allowed, for a few seconds after a new message; it fades in and out whole, and
/// the input line opens and closes inside a panel that stays. Its owner says where it goes (the split-screen, or the
/// corner of a single spectator view).
/// </summary>
internal sealed class SpectatorChatPanel
{
    private const float FadeSeconds = .2f, PopupSeconds = 5, LineSeconds = .18f;
    private const int Lines = 20;
    private static readonly Color Back = new Color(.02f, .022f, .026f, .86f), Edge = new Color(1, .38f, .02f, .55f),
        QuietEdge = new Color(.2f, .21f, .23f, 1), Muted = new Color(.6f, .62f, .64f, 1);
    private readonly RectTransform _root, _logArea, _inputBar;
    private readonly CanvasGroup _group;
    private readonly TextMeshProUGUI _title, _log, _input;
    private readonly Image[] _edges;
    private readonly StringBuilder _text = new StringBuilder();
    private List<string>? _history;
    private bool _typing, _chinese = true, _logDirty = true;
    private string _inputText = string.Empty, _composing = string.Empty;
    private int _version = -1, _caretAt = -1;
    private float _popup, _caret, _logWidth = -1;
    // How far the input line is open (0–1). It opens and closes inside a panel that stays; a panel coming or going
    // keeps one shape throughout, so nothing inside it jumps while it fades.
    private float _line;

    internal SpectatorChatPanel(Transform parent)
    {
        _root = SplitScreenView.CreateImage("Chat", parent, Back).rectTransform;
        _root.anchorMin = _root.anchorMax = _root.pivot = Vector2.zero;
        _edges = SplitScreenView.Frame4("Chat edge ", _root, Edge, 1);
        _group = _root.gameObject.AddComponent<CanvasGroup>(); _group.alpha = 0;
        _title = SplitScreenView.CreateText("Chat title", _root); _title.color = Muted; _title.alignment = TextAlignmentOptions.MidlineLeft;
        _logArea = new GameObject("Chat messages", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        _logArea.SetParent(_root, false);
        _log = SplitScreenView.CreateText("Chat log", _logArea); _log.richText = true; _log.enableWordWrapping = true;
        _log.alignment = TextAlignmentOptions.BottomLeft; _log.color = new Color(.9f, .9f, .9f, 1);
        _inputBar = SplitScreenView.CreateImage("Chat input", _root, new Color(1, .45f, .12f, .12f)).rectTransform;
        _input = SplitScreenView.CreateText("Chat input text", _inputBar); _input.richText = true; _input.enableWordWrapping = true;
        _input.alignment = TextAlignmentOptions.MidlineLeft; _input.color = Color.white;
        foreach (var rect in new[] { _logArea, _title.rectTransform, _inputBar, _input.rectTransform, _log.rectTransform })
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        _root.gameObject.SetActive(false);
    }

    /// <summary>The panel is wanted: the line is typed or a new message is showing.</summary>
    internal bool Wanted => _typing || _popup > 0;
    internal float Alpha => _group.alpha;

    /// <summary>
    /// The game's chat texts' sprites and preprocessing, whoever set them (LC Chinese Project draws emoji this way), so
    /// lines and the typed line show here as they do in the game's chat box.
    /// </summary>
    internal void MatchGameText(TMP_Text messages, TMP_Text line)
    {
        if (_log.spriteAsset != messages.spriteAsset || _log.textPreprocessor != messages.textPreprocessor)
        { _log.spriteAsset = messages.spriteAsset; _log.textPreprocessor = messages.textPreprocessor; _logDirty = true; }
        if (_input.spriteAsset != line.spriteAsset || _input.textPreprocessor != line.textPreprocessor)
        { _input.spriteAsset = line.spriteAsset; _input.textPreprocessor = line.textPreprocessor; }
    }

    internal void ConfigureFont(TMP_FontAsset? font, Material? material)
    {
        foreach (var text in new[] { _title, _log, _input }) { text.font = font; if (material != null) text.fontSharedMaterial = material; }
        _logDirty = true;
    }

    /// <summary>
    /// The chat's state: <paramref name="version"/> is the history's (a new one is a new message, which pops the panel
    /// up when <paramref name="popup"/> allows). <paramref name="caret"/> is where the caret is in the line (-1: its
    /// end), and <paramref name="composing"/> what an input method is composing there (pinyin before it becomes text).
    /// </summary>
    internal void Set(bool typing, string input, List<string> history, int version, bool popup, bool chinese, int caret = -1, string composing = "")
    {
        if (version != _version)
        {
            if (_version >= 0 && popup) _popup = PopupSeconds;
            _version = version; _logDirty = true;
        }
        // Closing the line by hand closes the panel, ending a pop-up it had; only later messages pop up again.
        if (!popup || _typing && !typing) _popup = 0;
        if (typing != _typing || chinese != _chinese) _logDirty = true;
        // The line keeps what it last showed while the panel closes: the game empties it as it closes, which would
        // flash the hint in (and drop the caret's place) during the fade.
        if (typing) { _inputText = input; _caretAt = caret; _composing = composing; }
        _typing = typing; _history = history; _chinese = chinese;
    }

    /// <summary>Moves the fade and the input line on; returns the panel's opacity.</summary>
    internal float Advance(float deltaTime)
    {
        _popup = Mathf.Max(0, _popup - deltaTime);
        bool wanted = Wanted;
        if (_group.alpha <= 0) _line = _typing ? 1 : 0;
        else if (wanted) _line = Mathf.MoveTowards(_line, _typing ? 1 : 0, deltaTime / LineSeconds);
        _group.alpha = Mathf.MoveTowards(_group.alpha, wanted ? 1 : 0, deltaTime / FadeSeconds);
        return _group.alpha;
    }

    /// <summary>Lays the panel out over <paramref name="area"/> (in its parent, from the lower left), sinking a little as it fades; null hides it.</summary>
    internal void Place(Rect? area, float uiScale, float deltaTime)
    {
        bool shown = area.HasValue && _group.alpha > 0;
        if (_root.gameObject.activeSelf != shown) _root.gameObject.SetActive(shown);
        if (!shown) return;
        var box = area!.Value;
        Layout(box, uiScale, deltaTime);
        _root.localPosition = new Vector3(box.x, box.y - (1 - SplitScreenResultsPanel.EaseOut(_group.alpha)) * 8 * uiScale, 0);
    }

    private void Layout(Rect area, float s, float deltaTime)
    {
        float pad = Mathf.Round(10 * s), title = Mathf.Round(18 * s), font = Mathf.Round(14 * s);
        float line = SplitScreenResultsPanel.EaseOut(_line), input = Mathf.Round(30 * s) * line, inner = area.width - pad * 2;
        _root.sizeDelta = area.size;
        var titleRect = _title.rectTransform; titleRect.sizeDelta = new Vector2(inner, title); titleRect.anchoredPosition = new Vector2(pad, area.height - pad - title);
        _inputBar.gameObject.SetActive(_line > 0);
        _inputBar.sizeDelta = new Vector2(area.width - pad, input); _inputBar.anchoredPosition = new Vector2(pad / 2, pad / 2);
        // The messages sit on the input line, rising and settling with it.
        float logBottom = pad + input, logHeight = Mathf.Max(0, area.height - pad * 2 - title - (input + pad / 2) * line - pad / 2);
        _logArea.sizeDelta = new Vector2(inner, logHeight); _logArea.anchoredPosition = new Vector2(pad, logBottom);
        var edgeColor = Color.Lerp(QuietEdge, Edge, line);
        foreach (var edge in _edges) edge.color = edgeColor;
        string heading = _line > .5f ? _chinese ? "聊天 · 回车发送 · ESC 取消" : "Chat · Enter sends · Esc cancels" : _chinese ? "聊天" : "Chat";
        if (_title.text != heading) _title.text = heading;
        if (_logDirty || !Mathf.Approximately(_logWidth, inner))
        {
            _logDirty = false; _logWidth = inner;
            _title.fontSize = Mathf.Round(12 * s);
            _text.Clear();
            if (_history != null)
                for (int i = Mathf.Max(0, _history.Count - Lines); i < _history.Count; i++)
                { if (_text.Length > 0) _text.Append('\n'); _text.Append(_history[i]); }
            _log.fontSize = font; _log.text = _text.ToString();
            // The newest lines sit at the bottom; older ones run off the top of the clipped area.
            float height = Mathf.Ceil(_log.GetPreferredValues(_log.text, inner, 0).y);
            var log = _log.rectTransform; log.sizeDelta = new Vector2(inner, height); log.anchoredPosition = Vector2.zero;
        }
        if (_line <= 0) return;
        _input.alpha = line;
        // The typed line, as typed (no markup), with a blinking caret; a hint while empty.
        _caret = (_caret + deltaTime) % 1;
        // Closing, the caret stops blinking but keeps its place.
        string caret = _typing && _caret < .5f ? "<color=#FF7A1F>|</color>" : "<color=#00000000>|</color>";
        _input.fontSize = font;
        if (_inputText.Length == 0 && _composing.Length == 0)
            _input.text = caret + "<color=#8E9194>" + (_chinese ? "输入消息…" : "Type a message…") + "</color>";
        else
        {
            // The composition sits at the caret, underlined, until the input method turns it into text.
            int at = _caretAt < 0 || _caretAt > _inputText.Length ? _inputText.Length : _caretAt;
            string composing = _composing.Length == 0 ? string.Empty : "<u><color=#FFD08A>" + NoMarkup(_composing) + "</color></u>";
            _input.text = NoMarkup(_inputText.Substring(0, at)) + composing + caret + NoMarkup(_inputText.Substring(at));
        }
        var inputRect = _input.rectTransform; inputRect.sizeDelta = new Vector2(area.width - pad * 2, Mathf.Round(30 * s)); inputRect.anchoredPosition = new Vector2(pad / 2, (input - Mathf.Round(30 * s)) / 2);
    }

    private static string NoMarkup(string text) => text.Length == 0 ? text : "<noparse>" + text.Replace("</noparse>", "</ noparse>") + "</noparse>";

    internal void Clear()
    {
        _typing = false; _popup = _line = 0; _group.alpha = 0; _root.gameObject.SetActive(false);
    }
}
