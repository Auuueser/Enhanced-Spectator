using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// A living player's notice that someone watching through their eyes (first person, so no ghost to see) is talking:
/// cards at the top right with the speaker's avatar and "X 正在说话", fading in and out with the voice. Hidden under
/// the game's menus and while the local player is dead.
/// </summary>
internal sealed class LethalCompanyWatcherCards : IDisposable
{
    private const float Width = 260, Height = 40, Gap = 6, Margin = 18, FadeSeconds = .2f, HoldSeconds = .5f;
    private static readonly Color Background = new Color(.035f, .038f, .045f, .86f), Speaking = new Color(1, .38f, .02f, 1);
    private sealed class Card
    {
        internal RectTransform Root = null!;
        internal CanvasGroup Group = null!;
        internal RawImage Avatar = null!;
        internal TextMeshProUGUI Text = null!;
        internal float LastHeard, Alpha;
        internal ulong ClientId;
    }
    private readonly List<Card> _cards = new();
    private GameObject? _host;
    private RectTransform? _root;

    /// <summary>Once per frame: who is speaking right now (client id, display name, Steam id).</summary>
    internal void Update(IReadOnlyList<(ulong ClientId, string Name, ulong SteamId)> speaking, bool chinese, float deltaTime)
    {
        var round = StartOfRound.Instance;
        var local = round != null ? round.localPlayerController : null;
        bool shown = local != null && !local.isPlayerDead && !local.isTypingChat && local.quickMenuManager != null && !local.quickMenuManager.isMenuOpen;
        if (!shown) speaking = Array.Empty<(ulong, string, ulong)>();
        if (speaking.Count == 0 && _cards.Count == 0) return;
        EnsureCanvas();
        float now = Time.unscaledTime;
        foreach (var (id, name, steam) in speaking)
        {
            var card = _cards.Find(c => c.ClientId == id) ?? CreateCard(id);
            card.LastHeard = now;
            string text = chinese ? $"{name} 正在说话" : $"{name} is speaking";
            if (card.Text.text != text) card.Text.text = text;
            card.Avatar.texture = SteamAvatarCache.Get(steam);
            card.Avatar.enabled = card.Avatar.texture != null;
        }
        for (int i = _cards.Count - 1; i >= 0; i--)
        {
            var card = _cards[i];
            bool live = shown && now - card.LastHeard < HoldSeconds;
            card.Alpha = Mathf.MoveTowards(card.Alpha, live ? 1 : 0, deltaTime / FadeSeconds);
            card.Group.alpha = card.Alpha;
            if (card.Alpha <= 0 && !live) { UnityEngine.Object.Destroy(card.Root.gameObject); _cards.RemoveAt(i); }
        }
        for (int i = 0; i < _cards.Count; i++) _cards[i].Root.anchoredPosition = new Vector2(-Margin, -Margin - i * (Height + Gap));
    }

    private void EnsureCanvas()
    {
        if (_root != null) return;
        _host = new GameObject("Enhanced Spectator watcher cards", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        UnityEngine.Object.DontDestroyOnLoad(_host);
        var canvas = _host.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31000;
        var scaler = _host.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
        _root = (RectTransform)_host.transform;
    }

    private Card CreateCard(ulong clientId)
    {
        var root = new GameObject("Watcher card", typeof(RectTransform), typeof(Image), typeof(CanvasGroup)).GetComponent<RectTransform>();
        root.SetParent(_root, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1, 1);
        root.sizeDelta = new Vector2(Width, Height);
        var background = root.GetComponent<Image>(); background.color = Background; background.raycastTarget = false;
        var edge = new GameObject("Speaking edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        edge.transform.SetParent(root, false); edge.color = Speaking; edge.raycastTarget = false;
        var edgeRect = edge.rectTransform; edgeRect.anchorMin = new Vector2(0, 0); edgeRect.anchorMax = new Vector2(0, 1);
        edgeRect.pivot = new Vector2(0, .5f); edgeRect.sizeDelta = new Vector2(3, 0); edgeRect.anchoredPosition = Vector2.zero;
        var avatar = new GameObject("Avatar", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        avatar.transform.SetParent(root, false); avatar.raycastTarget = false;
        var avatarRect = avatar.rectTransform; avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0, .5f);
        avatarRect.pivot = new Vector2(0, .5f); avatarRect.sizeDelta = new Vector2(Height - 10, Height - 10); avatarRect.anchoredPosition = new Vector2(9, 0);
        var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(root, false); text.raycastTarget = false; text.richText = false;
        if (LethalCompanyWorldTextFont.Font is { } font) text.font = font;
        if (LethalCompanyWorldTextFont.LabelMaterial is { } material) text.fontSharedMaterial = material;
        text.fontSize = 16; text.color = new Color(.94f, .94f, .94f, 1);
        text.alignment = TextAlignmentOptions.MidlineLeft; text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Ellipsis;
        var textRect = text.rectTransform; textRect.anchorMin = new Vector2(0, 0); textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = new Vector2(Height + 6, 0); textRect.offsetMax = new Vector2(-10, 0);
        var card = new Card { Root = root, Group = root.GetComponent<CanvasGroup>(), Avatar = avatar, Text = text, ClientId = clientId };
        card.Group.alpha = 0; card.Group.blocksRaycasts = false; card.Group.interactable = false;
        _cards.Add(card);
        return card;
    }

    /// <summary>A spectator's Steam id (0 on LAN or when unknown), for the avatar.</summary>
    internal static ulong SteamIdOf(ulong clientId)
    {
        var round = StartOfRound.Instance;
        if (round == null || round.allPlayerScripts == null) return 0;
        foreach (var player in round.allPlayerScripts) if (player != null && player.actualClientId == clientId) return player.playerSteamId;
        return 0;
    }

    public void Dispose()
    {
        if (_host != null) UnityEngine.Object.Destroy(_host);
        _host = null; _root = null; _cards.Clear();
    }
}
