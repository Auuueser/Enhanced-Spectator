using System.Collections.Generic;
using EnhancedSpectator.Features.SplitScreen;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The split-screen's chat panel, in place of the game's chat box (which the views cover). With a large view (or the
/// ship view) it sits in that view's lower left, and new messages can briefly show there too; tiled, it opens only
/// while typing, as a tall panel on the left the views make room for.
/// </summary>
internal sealed partial class SplitScreenView
{
    private SpectatorChatPanel _chat = null!;
    private SplitScreenKey? _chatInside;
    private bool _chatDocked;
    private Rect _chatDock;
    internal bool ChatAvailable => _hudCanvas != null && _hudCanvas.gameObject.activeInHierarchy;

    private void CreateChat() => _chat = new SpectatorChatPanel(_hudRoot);
    private void ConfigureChatFont() => _chat.ConfigureFont(_font, _material);
    /// <summary>See <see cref="SpectatorChatPanel.MatchGameText"/>.</summary>
    internal void MatchChatText(TMPro.TMP_Text messages, TMPro.TMP_Text line) => _chat.MatchGameText(messages, line);

    /// <summary>
    /// The chat: <paramref name="inside"/> is the view the panel sits in (the large or ship view; null when tiled),
    /// <paramref name="typing"/> whether the line is being typed, <paramref name="version"/> the history's version
    /// (a new one is a new message, which pops the panel up in a view when <paramref name="popup"/> allows);
    /// <paramref name="caret"/> and <paramref name="composing"/> as <see cref="SpectatorChatPanel.Set"/> takes them.
    /// </summary>
    internal void SetChat(SplitScreenKey? inside, bool typing, string input, List<string> history, int version, bool popup, bool chinese,
        int caret = -1, string composing = "")
    {
        if (inside.HasValue && !_tiles.ContainsKey(inside.Value)) inside = null;
        _chat.Set(typing, input, history, version, popup && inside.HasValue, chinese, caret, composing);
        bool docked = typing && !inside.HasValue;
        if (docked != _chatDocked) { _chatDocked = docked; _layoutDirty = true; }
        _chatInside = inside;
    }

    private void TickChat(float deltaTime)
    {
        float alpha = _chat.Advance(deltaTime);
        Rect? area = null;
        if (_chatInside.HasValue && _tiles.TryGetValue(_chatInside.Value, out var tile))
        {
            // The large view's lower left, a share of it.
            tile.Decoration.GetWorldCorners(_clockCorners);
            Vector3 low = _hudRoot.InverseTransformPoint(_clockCorners[0]), high = _hudRoot.InverseTransformPoint(_clockCorners[2]);
            float margin = Mathf.Round(12 * _uiScale), width = high.x - low.x, height = high.y - low.y;
            area = new Rect(low.x + margin, low.y + margin, Mathf.Round(Mathf.Min(width * .42f, 560 * _uiScale)), Mathf.Round(Mathf.Min(height * .5f, 320 * _uiScale)));
        }
        else if (_chatDocked || alpha > 0 && _chatDock.width > 0) area = _chatDock;
        _chat.Place(area, _uiScale, deltaTime);
    }

    private void ClearChat()
    {
        _chatDocked = false; _chatInside = null; _chat.Clear();
    }
}
