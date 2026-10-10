using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The chat panel of a single spectator view (no split-screen): in the game's chat corner, on its own canvas, in place
/// of the game's chat box.
/// </summary>
internal sealed class SpectatorChatOverlay : IDisposable
{
    private readonly Canvas _canvas;
    private readonly SpectatorChatPanel _panel;
    private TMP_FontAsset? _font;
    private Material? _material;

    internal SpectatorChatOverlay()
    {
        _canvas = SplitScreenView.CreateCanvas("EnhancedSpectator Chat", 32003);
        // It outlives a game's scenes, like the player it belongs to.
        UnityEngine.Object.DontDestroyOnLoad(_canvas.gameObject);
        _panel = new SpectatorChatPanel(SpectatorDemoZoom.CreateRoot(_canvas.transform));
        _canvas.gameObject.SetActive(false);
    }

    internal Canvas Canvas => _canvas;

    /// <summary>See <see cref="SpectatorChatPanel.MatchGameText"/>.</summary>
    internal void MatchChatText(TMP_Text messages, TMP_Text line) => _panel.MatchGameText(messages, line);

    internal void ConfigureFont(TMP_FontAsset? font)
    {
        if (font == null || font == _font) return;
        if (_material != null) UnityEngine.Object.Destroy(_material);
        _font = font; _material = SpectatorTextStyle.CreateLabelMaterial(font);
        _panel.ConfigureFont(font, _material);
    }

    /// <summary>
    /// <paramref name="shown"/>: a dead player watches in a single view. The panel then shows while the line is typed
    /// and, when <paramref name="popup"/> allows, for a few seconds after a new message.
    /// </summary>
    internal void Tick(bool shown, bool typing, string input, List<string> history, int version, bool popup, bool chinese, float deltaTime,
        int caret = -1, string composing = "")
    {
        // Hidden, it still follows the history, so what arrived meanwhile does not pop up when it shows again.
        _panel.Set(typing && shown, input, history, version, popup && shown, chinese, caret, composing);
        if (!shown) { Hide(); return; }
        if (!_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);
        _panel.Advance(deltaTime);
        var size = new Vector2(Screen.width, Screen.height);
        float ui = SpectatorTextStyle.UiScale(size), margin = Mathf.Round(16 * ui);
        _panel.Place(new Rect(margin, margin, Mathf.Round(Mathf.Min(size.x * .32f, 560 * ui)), Mathf.Round(Mathf.Min(size.y * .42f, 320 * ui))), ui, deltaTime);
    }

    internal void Hide()
    {
        if (_canvas.gameObject.activeSelf) { _panel.Clear(); _canvas.gameObject.SetActive(false); }
    }

    public void Dispose()
    {
        if (_material != null) UnityEngine.Object.Destroy(_material);
        UnityEngine.Object.Destroy(_canvas.gameObject);
    }
}
