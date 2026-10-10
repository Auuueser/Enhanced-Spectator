using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// A row of emote bubbles over the bottom of the screen: number keys or a click send one. It slides up when
/// opened, dims while the send cooldown runs and closes itself after a few idle seconds.
/// </summary>
internal sealed class SpectatorEmotePicker : IDisposable
{
    private const float OpenSeconds = .18f, IdleSeconds = 5f;
    private static readonly Color Panel = new Color(.03f, .032f, .037f, .92f), Bubble = new Color(.96f, .93f, .86f, 1),
        BubbleText = new Color(.1f, .08f, .06f, 1), Number = new Color(.8f, .4f, .1f, 1), Edge = new Color(.8f, .4f, .1f, .45f);
    private readonly Canvas _canvas;
    private readonly RectTransform _zoom, _panel;
    private readonly CanvasGroup _group;
    private readonly RectTransform[] _bubbles;
    private readonly Image[] _fills;
    private readonly float[] _hover;
    private int _sent = -1;
    private float _sentAge = 1;
    private readonly TextMeshProUGUI[] _labels, _numbers;
    private readonly TextMeshProUGUI _cooldown;
    private TMP_FontAsset? _font;
    private Material? _material;
    private float _open, _idle, _bottom, _ui = 1;
    internal bool IsOpen { get; private set; }
    internal Canvas Canvas => _canvas;

    internal SpectatorEmotePicker(int count)
    {
        _canvas = SplitScreenView.CreateCanvas("EnhancedSpectator Emote Picker", 32006);
        _zoom = SpectatorDemoZoom.CreateRoot(_canvas.transform);
        _panel = SplitScreenView.CreateImage("Emote picker", _zoom, Panel).rectTransform;
        _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(.5f, 0);
        SplitScreenView.Frame4("Emote picker edge ", _panel, Edge, 1);
        _group = _panel.gameObject.AddComponent<CanvasGroup>();
        _bubbles = new RectTransform[count]; _labels = new TextMeshProUGUI[count]; _numbers = new TextMeshProUGUI[count];
        _fills = new Image[count]; _hover = new float[count];
        for (int i = 0; i < count; i++)
        {
            _fills[i] = SplitScreenView.CreateImage("Emote " + i, _panel, Bubble); _bubbles[i] = _fills[i].rectTransform;
            _bubbles[i].pivot = new Vector2(.5f, .5f);
            _labels[i] = SplitScreenView.CreateText("Emote text " + i, _bubbles[i]); _labels[i].alignment = TextAlignmentOptions.Center; _labels[i].color = BubbleText;
            _numbers[i] = SplitScreenView.CreateText("Emote key " + i, _panel); _numbers[i].alignment = TextAlignmentOptions.Center; _numbers[i].color = Number;
            _numbers[i].text = (i + 1).ToString();
        }
        _cooldown = SplitScreenView.CreateText("Emote cooldown", _panel); _cooldown.alignment = TextAlignmentOptions.Center; _cooldown.color = new Color(.7f, .72f, .74f, 1);
        _canvas.gameObject.SetActive(false);
    }

    internal void ConfigureFont(TMP_FontAsset? font)
    {
        if (font == null || font == _font) return;
        if (_material != null) UnityEngine.Object.Destroy(_material);
        _font = font; _material = SpectatorTextStyle.CreateLabelMaterial(font);
        foreach (var text in _canvas.GetComponentsInChildren<TextMeshProUGUI>(true)) { text.font = font; text.fontSharedMaterial = _material; }
    }

    // bottom: screen-pixel height to keep clear underneath, e.g. the split-screen audience row.
    internal void Open(Func<int, string> text, float bottom)
    {
        IsOpen = true; _idle = 0; _bottom = bottom;
        _ui = SpectatorTextStyle.UiScale(new Vector2(Screen.width, Screen.height));
        float font = Mathf.Round(15 * _ui), pad = Mathf.Round(8 * _ui), height = Mathf.Round(30 * _ui), number = Mathf.Round(16 * _ui);
        float x = pad;
        for (int i = 0; i < _bubbles.Length; i++)
        {
            _labels[i].text = text(i); _labels[i].fontSize = font; _numbers[i].fontSize = Mathf.Round(12 * _ui);
            float width = Mathf.Max(height * 1.4f, Mathf.Ceil(_labels[i].GetPreferredValues(_labels[i].text, 4096, 0).x) + pad * 2);
            Place(_numbers[i].rectTransform, x, pad, width, number);
            Place(_bubbles[i], x, pad + number, width, height);
            // Scale around the bubble's centre.
            _bubbles[i].pivot = new Vector2(.5f, .5f); _bubbles[i].anchoredPosition += new Vector2(width * .5f, -height * .5f);
            SplitScreenView.Stretch(_labels[i].rectTransform);
            x += width + pad;
        }
        float panelHeight = pad * 2 + number + height + Mathf.Round(18 * _ui);
        Place(_cooldown.rectTransform, pad, pad + number + height + Mathf.Round(2 * _ui), x - pad * 2, Mathf.Round(16 * _ui));
        _cooldown.fontSize = Mathf.Round(11 * _ui);
        _panel.sizeDelta = new Vector2(x, panelHeight);
        _canvas.gameObject.SetActive(true);
    }

    internal void Close() => IsOpen = false;
    /// <summary>The sent bubble flashes while the row closes.</summary>
    internal void Sent(int emote) { _sent = emote; _sentAge = 0; IsOpen = false; }
    internal void Touch() => _idle = 0;

    // ---- For the demo: where things are in the unzoomed picture. ----
    internal Rect? FocusPicker() => IsOpen ? SpectatorDemoZoom.Measure(_zoom, _panel) : null;
    internal Rect? FocusEmote(int emote) => IsOpen && emote < _bubbles.Length ? SpectatorDemoZoom.Measure(_zoom, _bubbles[emote]) : null;

    /// <summary>Index of the bubble under the pointer, or -1.</summary>
    internal int HitTest(Vector2 screenPoint)
    {
        if (!IsOpen) return -1;
        for (int i = 0; i < _bubbles.Length; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(_bubbles[i], screenPoint, null)) return i;
        return -1;
    }

    internal void Tick(float deltaTime, float cooldown, bool chinese, Vector2? pointer)
    {
        if (IsOpen && (_idle += deltaTime) > IdleSeconds) IsOpen = false;
        SpectatorDemoZoom.Apply(_zoom);
        _open = Mathf.MoveTowards(_open, IsOpen ? 1 : 0, deltaTime / OpenSeconds);
        if (_open <= 0) { if (_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(false); return; }
        float eased = 1 - (1 - _open) * (1 - _open);
        _group.alpha = eased;
        _panel.anchoredPosition = new Vector2(0, _bottom + Mathf.Round(12 * _ui) + (1 - eased) * 18 * _ui);
        bool ready = cooldown <= 0;
        // Hover lifts and brightens a bubble; the sent one flashes white, then the row closes.
        int hovered = -1;
        if (IsOpen && ready && pointer is { } point) hovered = HitTest(point);
        _sentAge += deltaTime;
        for (int i = 0; i < _bubbles.Length; i++)
        {
            _hover[i] = Mathf.MoveTowards(_hover[i], i == hovered ? 1 : 0, deltaTime / .1f);
            float flash = i == _sent ? Mathf.Clamp01(1 - _sentAge / .3f) : 0;
            var color = ready ? Color.Lerp(Bubble, Color.white, _hover[i] * .35f + flash) : new Color(Bubble.r * .55f, Bubble.g * .55f, Bubble.b * .55f, 1);
            if (_fills[i].color != color) _fills[i].color = color;
            float scale = 1 + .08f * _hover[i] + .12f * flash;
            _bubbles[i].localScale = new Vector3(scale, scale, 1);
        }
        _cooldown.text = ready ? (chinese ? "按数字键或点击发送" : "Press a number or click to send")
            : (chinese ? $"冷却中 {cooldown:0.0} 秒" : $"Cooling down {cooldown:0.0}s");
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }

    public void Dispose()
    {
        if (_material != null) UnityEngine.Object.Destroy(_material);
        UnityEngine.Object.Destroy(_canvas.gameObject);
    }
}
