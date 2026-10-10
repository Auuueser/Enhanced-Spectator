using System;
using System.Collections.Generic;
using EnhancedSpectator.Features.SplitScreen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The audience row under the split-screen views: dead players (up to 32) with avatar and name. A speaking
/// player's chip border fades in deep orange and out again; chips drop their names together when they would not
/// fit, and hovering a chip shows a card with the name and who that player is watching. The player being
/// watched together with gets a cyan ring; emotes pop up as bubbles above their sender's chip.
/// </summary>
internal sealed class SplitScreenDeadBar
{
    private const float FadeIn = .05f, FadeOut = .22f, EmoteSeconds = 2.6f, EmotePop = .25f, EmoteFade = .4f;
    internal static readonly Color SpeakingColor = new Color(.8f, .4f, .1f, 1);
    private static readonly Color ChipColor = new Color(.05f, .055f, .062f, .86f), EdgeColor = new Color(.24f, .25f, .27f, 1),
        SpeakingFill = new Color(.16f, .09f, .04f, .9f), NameColor = new Color(.86f, .87f, .88f, 1), FollowColor = new Color(.25f, .55f, 1, 1),
        CardColor = new Color(.035f, .038f, .045f, .96f), HoverEdge = new Color(.62f, .64f, .68f, 1), MutedColor = new Color(.62f, .64f, .66f, 1), BubbleColor = new Color(.96f, .93f, .86f, 1),
        BubbleText = new Color(.1f, .08f, .06f, 1);
    private readonly RectTransform _root, _card;
    internal RectTransform Root => _root;
    internal RectTransform CardRect => _card;
    /// <summary>An audience member's chip, or null when they are not in the row.</summary>
    /// <summary>The first and last avatars shown, which frame the occupied part of the row.</summary>
    internal (RectTransform? First, RectTransform? Last) Ends
        => _players.Count == 0 ? (null, null) : (_chips[0].Root, _chips[Math.Min(_players.Count, _chips.Count) - 1].Root);
    internal RectTransform? ChipRect(SplitScreenKey key)
    { for (int i = 0; i < _players.Count; i++) if (_players[i].Key == key) return _chips[i].Root; return null; }
    private readonly TextMeshProUGUI _cardTitle, _cardWatching, _cardFollowing, _cardHint;
    private readonly List<Chip> _chips = new List<Chip>();
    private readonly List<SplitScreenDeadPlayer> _players = new List<SplitScreenDeadPlayer>(32);
    private TMP_FontAsset? _font;
    private Material? _material;
    private float _ui = 1, _width = 1920;
    private bool _chinese = true, _dirty = true;
    private int _hover = -1;
    private SplitScreenKey? _following;

    /// <summary>Loads a Steam avatar into the image; not called for players without a Steam id (LAN).</summary>
    /// <summary>A Steam id's profile picture once it has arrived (the avatar cache); chips read it every frame.</summary>
    internal Func<ulong, Texture?>? AvatarOf;
    internal float Height => _players.Count > 0 ? Mathf.Round(40 * _ui) : 0;
    internal int Count => _players.Count;

    internal SplitScreenDeadBar(Transform parent)
    {
        _root = new GameObject("Dead players", typeof(RectTransform)).GetComponent<RectTransform>();
        _root.SetParent(parent, false);
        _root.anchorMin = new Vector2(0, 0); _root.anchorMax = new Vector2(1, 0); _root.pivot = new Vector2(.5f, 0);
        var card = SplitScreenView.CreateImage("Dead player card", _root, CardColor);
        _card = card.rectTransform;
        SplitScreenView.Frame4("Dead player card edge ", _card, EdgeColor, 1);
        _cardTitle = Text("Card name", _card); _cardWatching = Text("Card watching", _card); _cardHint = Text("Card hint", _card);
        _cardFollowing = Text("Card following", _card);
        // Only this line colours a name (ours, orange); names stay literal inside <noparse>.
        _cardFollowing.richText = true;
        _cardWatching.color = _cardFollowing.color = MutedColor; _cardHint.color = FollowColor;
        _card.gameObject.SetActive(false);
        _root.gameObject.SetActive(false);
    }

    internal void ConfigureFont(TMP_FontAsset? font, Material? material)
    {
        _font = font; _material = material;
        Apply(_cardTitle); Apply(_cardWatching); Apply(_cardFollowing); Apply(_cardHint);
        foreach (var chip in _chips) { Apply(chip.Name); Apply(chip.Initial); Apply(chip.Bubble); }
        _dirty = true;
    }

    internal void SetSize(float width, float ui) { if (_width == width && _ui == ui) return; _width = width; _ui = ui; _dirty = true; }

    /// <summary>Returns true when the bar appeared or disappeared, which changes the view layout.</summary>
    internal bool SetPlayers(IReadOnlyList<SplitScreenDeadPlayer> players, bool chinese)
    {
        bool same = players.Count == _players.Count && chinese == _chinese;
        bool watchingChanged = false;
        for (int i = 0; same && i < players.Count; i++)
        {
            same = players[i].Key == _players[i].Key && players[i].Name == _players[i].Name && players[i].SteamId == _players[i].SteamId;
            watchingChanged |= players[i].WatchingName != _players[i].WatchingName || players[i].SplitView != _players[i].SplitView
                || players[i].FollowingName != _players[i].FollowingName || players[i].FollowLoop != _players[i].FollowLoop;
        }
        if (same)
        {
            if (watchingChanged) { for (int i = 0; i < players.Count; i++) _players[i] = players[i]; RefreshCard(); }
            return false;
        }
        bool shownBefore = _players.Count > 0;
        _players.Clear();
        for (int i = 0; i < players.Count && i < 32; i++) _players.Add(players[i]);
        _chinese = chinese; _dirty = true;
        if (_hover >= _players.Count) _hover = -1;
        return shownBefore != _players.Count > 0;
    }

    /// <summary>The dead player under the pointer, if any.</summary>
    internal SplitScreenDeadPlayer? HitTest(Vector2 screenPoint, Camera? camera)
    {
        if (!_root.gameObject.activeInHierarchy) return null;
        for (int i = 0; i < _players.Count; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(_chips[i].Root, screenPoint, camera)) return _players[i];
        return null;
    }

    internal void SetHover(SplitScreenKey? key)
    {
        int hover = -1;
        if (key.HasValue) for (int i = 0; i < _players.Count; i++) if (_players[i].Key == key.Value) hover = i;
        if (hover == _hover) return;
        int previous = _hover;
        _hover = hover; RefreshCard();
        // Hovered chips get a lighter outline; Tick blends it with the speaking colour.
        if (previous >= 0 && previous < _chips.Count) _chips[previous].Dirty = true;
        if (hover >= 0) _chips[hover].Dirty = true;
    }

    /// <summary>The dead player watched together with, ringed in cyan.</summary>
    internal void SetFollowing(SplitScreenKey? key)
    {
        if (_following == key) return;
        _following = key; RefreshCard();
        for (int i = 0; i < _players.Count && i < _chips.Count; i++) ApplyFollow(_chips[i], _players[i].Key == key);
    }

    /// <summary>Pops an emote bubble above the sender's chip.</summary>
    internal void ShowEmote(ulong clientId, string text)
    { for (int i = 0; i < _players.Count; i++) if (_players[i].Source.ClientId == clientId) PopEmote(_chips[i], text); }
    /// <summary>An emote above one audience member (preview identities share their source player).</summary>
    internal void ShowEmote(SplitScreenKey key, string text)
    { for (int i = 0; i < _players.Count; i++) if (_players[i].Key == key) PopEmote(_chips[i], text); }

    private void PopEmote(Chip chip, string text)
    {
        chip.Bubble.text = text; chip.EmoteAge = 0;
        float width = Mathf.Ceil(chip.Bubble.GetPreferredValues(text, 4096, 0).x) + Mathf.Round(14 * _ui);
        chip.BubbleRoot.sizeDelta = new Vector2(Mathf.Max(width, Mathf.Round(30 * _ui)), Mathf.Round(24 * _ui));
        chip.BubbleRoot.gameObject.SetActive(true);
    }

    internal void Tick(float deltaTime, Dictionary<SplitScreenKey, float> speaking)
    {
        if (_dirty) Layout();
        for (int i = 0; i < _players.Count; i++)
        {
            var chip = _chips[i];
            AnimateEmote(chip, deltaTime);
            // The glow follows the voice's loudness: it rises quickly with each word and settles slowly between them.
            float target = Mathf.Max(speaking.TryGetValue(_players[i].Source, out float source) ? source : 0,
                speaking.TryGetValue(_players[i].Key, out float own) ? own : 0);
            float speak = Mathf.Lerp(chip.Speak, target, 1 - Mathf.Exp(-deltaTime / (target > chip.Speak ? FadeIn : FadeOut)));
            if (Mathf.Abs(speak - target) < .005f) speak = target;
            var picture = chip.SteamId != 0 ? AvatarOf?.Invoke(chip.SteamId) : null;
            if (chip.Avatar.texture != picture) chip.Avatar.texture = picture;
            bool avatar = picture != null;
            // An empty RawImage would draw a white square until the Steam avatar arrives.
            if (chip.Avatar.enabled != avatar) { chip.Avatar.enabled = avatar; chip.AvatarBack.gameObject.SetActive(!avatar); }
            if (speak == chip.Speak && speak == 0 && !chip.Dirty) continue;
            chip.Speak = speak; chip.Dirty = false;
            var edge = Color.Lerp(i == _hover ? HoverEdge : EdgeColor, SpeakingColor, speak);
            foreach (var image in chip.Edges) image.color = edge;
            chip.Background.color = Color.Lerp(ChipColor, SpeakingFill, speak);
        }
    }

    private void AnimateEmote(Chip chip, float deltaTime)
    {
        if (!chip.BubbleRoot.gameObject.activeSelf) return;
        chip.EmoteAge += deltaTime;
        float age = chip.EmoteAge;
        if (age >= EmoteSeconds) { chip.BubbleRoot.gameObject.SetActive(false); return; }
        // Pop with a small overshoot, then rest and fade.
        float t = Mathf.Clamp01(age / EmotePop);
        float scale = age < EmotePop ? Mathf.LerpUnclamped(.55f, 1, 1 + 2.2f * Mathf.Pow(t - 1, 3) + 1.2f * Mathf.Pow(t - 1, 2)) : 1;
        chip.BubbleRoot.localScale = new Vector3(scale, scale, 1);
        chip.BubbleGroup.alpha = Mathf.Clamp01((EmoteSeconds - age) / EmoteFade);
        chip.BubbleRoot.anchoredPosition = new Vector2(0, chip.Root.sizeDelta.y + Mathf.Round(6 * _ui) + Mathf.Min(age, .5f) * 8 * _ui);
    }

    private void Layout()
    {
        _dirty = false;
        _root.gameObject.SetActive(_players.Count > 0);
        if (_players.Count == 0) { _card.gameObject.SetActive(false); return; }
        float height = Height, chipHeight = Mathf.Round(30 * _ui), avatar = Mathf.Round(22 * _ui), pad = Mathf.Round(4 * _ui), gap = Mathf.Round(6 * _ui);
        float font = Mathf.Round(13 * _ui);
        _root.sizeDelta = new Vector2(0, height); _root.anchoredPosition = Vector2.zero;
        for (int i = 0; i < _players.Count; i++)
        {
            if (i == _chips.Count) _chips.Add(CreateChip(i));
            var chip = _chips[i]; var player = _players[i];
            chip.Root.gameObject.SetActive(true);
            chip.Name.text = player.Name; chip.Name.fontSize = font; chip.Initial.fontSize = Mathf.Round(12 * _ui);
            chip.Bubble.fontSize = font;
            chip.Initial.text = player.Name.Length > 0 ? player.Name.Substring(0, 1).ToUpperInvariant() : "?";
            chip.AvatarBack.color = Color.HSVToRGB(player.Source.SlotId * .137f % 1f, .45f, .55f);
            chip.SteamId = player.SteamId;
            chip.NameWidth = Mathf.Min(Mathf.Ceil(chip.Name.GetPreferredValues(chip.Name.text, 4096, 0).x), 160 * _ui);
            ApplyFollow(chip, player.Key == _following);
        }
        for (int i = _players.Count; i < _chips.Count; i++) _chips[i].Root.gameObject.SetActive(false);
        float full = 0, compact = 0;
        for (int i = 0; i < _players.Count; i++) { full += pad * 3 + avatar + _chips[i].NameWidth + gap; compact += chipHeight + gap; }
        // Names are dropped together, never for only some players; hovering shows them.
        bool names = full - gap <= _width - 32;
        float x = Mathf.Round((_width - ((names ? full : compact) - gap)) * .5f), top = Mathf.Round((height - chipHeight) * .5f);
        for (int i = 0; i < _players.Count; i++)
        {
            var chip = _chips[i];
            float width = names ? pad * 3 + avatar + chip.NameWidth : chipHeight;
            Place(chip.Root, x, top, width, chipHeight);
            float avatarLeft = names ? pad : Mathf.Round((chipHeight - avatar) * .5f), avatarTop = Mathf.Round((chipHeight - avatar) * .5f);
            Place(chip.Avatar.rectTransform, avatarLeft, avatarTop, avatar, avatar);
            Place(chip.AvatarBack.rectTransform, avatarLeft, avatarTop, avatar, avatar);
            chip.Name.gameObject.SetActive(names);
            Place(chip.Name.rectTransform, pad * 2 + avatar, 0, chip.NameWidth, chipHeight);
            chip.BubbleRoot.anchorMin = chip.BubbleRoot.anchorMax = chip.BubbleRoot.pivot = new Vector2(.5f, 0);
            chip.BubbleRoot.anchoredPosition = new Vector2(0, chipHeight + Mathf.Round(6 * _ui));
            x += width + gap;
        }
        RefreshCard();
    }

    private void RefreshCard()
    {
        if (_hover < 0 || _hover >= _players.Count) { _card.gameObject.SetActive(false); return; }
        var player = _players[_hover]; var chip = _chips[_hover];
        float font = Mathf.Round(13 * _ui), pad = Mathf.Round(8 * _ui), line = Mathf.Round(18 * _ui);
        _cardTitle.text = player.Name; _cardTitle.fontSize = Mathf.Round(14 * _ui);
        _cardWatching.fontSize = _cardFollowing.fontSize = _cardHint.fontSize = font;
        bool audience = player.SplitView == Networking.SpectatorSplitView.Audience;
        _cardWatching.text = !player.Enhanced ? (_chinese ? "非增强观战玩家" : "Not using Enhanced Spectator")
            : audience ? (player.WatchingName.Length > 0
                ? (_chinese ? "观众席 · 正在观看 " : "Audience · watching ") + player.WatchingName : _chinese ? "在观众席" : "In the audience")
            : player.WatchingName.Length > 0 ? (_chinese ? "正在观看 " : "Watching ") + player.WatchingName
            : _chinese ? "正在切换观察对象" : "Choosing who to watch";
        // Whom they follow; our own name in the audio orange when it is us.
        string followed = player.FollowingYou ? $"<color=#FF6105><noparse>{player.FollowingName}</noparse></color>" : $"<noparse>{player.FollowingName}</noparse>";
        _cardFollowing.text = player.FollowingName.Length > 0 ? (_chinese ? $"正在跟随 {followed} 观战" : $"Following {followed}") : string.Empty;
        // Only split-screen players can be followed (a single-view spectator is neither seen nor heard in our views),
        // and not one whose follow chain leads back to us.
        bool split = player.SplitView != Networking.SpectatorSplitView.None;
        bool followable = !player.Local && player.Enhanced && split && !player.FollowLoop;
        _cardHint.text = player.Local || !player.Enhanced ? string.Empty
            : !split ? (_chinese ? "非分屏模式，无法一起观看" : "Not in split-screen: cannot watch together")
            : player.FollowLoop ? (player.FollowingYou ? (_chinese ? "TA 正在跟随你，无法一起观看" : "Following you: cannot watch together")
                : _chinese ? "会形成循环跟随，无法一起观看" : "Would loop back to you: cannot watch together")
            : player.Key == _following ? (_chinese ? "再次点击停止一起观看" : "Click again to stop watching together")
            : audience ? (_chinese ? "点击跟随 TA（一起留在观众席）" : "Click to follow (stay in the audience together)")
            : _chinese ? "点击与 TA 一起观看" : "Click to watch together";
        _cardHint.color = followable ? FollowColor : MutedColor;
        _cardFollowing.gameObject.SetActive(_cardFollowing.text.Length > 0);
        _cardHint.gameObject.SetActive(_cardHint.text.Length > 0);
        float width = 0;
        foreach (var text in new[] { _cardTitle, _cardWatching, _cardFollowing, _cardHint })
            if (text.gameObject.activeSelf) width = Mathf.Max(width, Mathf.Ceil(text.GetPreferredValues(text.text, 4096, 0).x));
        width += pad * 2;
        int lines = 2;
        Place(_cardTitle.rectTransform, pad, pad, width - pad * 2, line);
        Place(_cardWatching.rectTransform, pad, pad + line, width - pad * 2, line);
        if (_cardFollowing.gameObject.activeSelf) Place(_cardFollowing.rectTransform, pad, pad + line * lines++, width - pad * 2, line);
        if (_cardHint.gameObject.activeSelf) Place(_cardHint.rectTransform, pad, pad + line * lines++, width - pad * 2, line);
        float height = pad * 2 + line * lines;
        float chipCenter = chip.Root.anchoredPosition.x + chip.Root.sizeDelta.x * .5f;
        float left = Mathf.Clamp(chipCenter - width * .5f, 8, _width - width - 8);
        Place(_card, Mathf.Round(left), -(height + Mathf.Round(8 * _ui)), width, height);
        _card.gameObject.SetActive(true); _card.SetAsLastSibling();
    }

    private void ApplyFollow(Chip chip, bool following)
    {
        foreach (var ring in chip.FollowRing) ring.color = following ? FollowColor : Color.clear;
    }

    private Chip CreateChip(int index)
    {
        var root = SplitScreenView.CreateImage("Dead player " + index, _root, ChipColor);
        var edges = SplitScreenView.Frame4("Dead player edge ", root.rectTransform, EdgeColor, 1);
        var ringHolder = new GameObject("Follow ring", typeof(RectTransform)).GetComponent<RectTransform>();
        ringHolder.SetParent(root.transform, false); SplitScreenView.Stretch(ringHolder);
        ringHolder.offsetMin = new Vector2(-3, -3); ringHolder.offsetMax = new Vector2(3, 3);
        var ring = SplitScreenView.Frame4("Follow ring ", ringHolder, Color.clear, 2);
        var back = SplitScreenView.CreateImage("Avatar placeholder", root.rectTransform, Color.gray);
        var initial = SplitScreenView.CreateText("Avatar initial", back.rectTransform); initial.alignment = TextAlignmentOptions.Center; Apply(initial);
        SplitScreenView.Stretch(initial.rectTransform); initial.color = Color.white;
        var avatar = new GameObject("Avatar", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        avatar.transform.SetParent(root.transform, false); avatar.raycastTarget = false; avatar.enabled = false;
        var name = SplitScreenView.CreateText("Dead player name", root.rectTransform); name.alignment = TextAlignmentOptions.MidlineLeft; Apply(name);
        name.color = NameColor;
        var bubble = SplitScreenView.CreateImage("Emote bubble", root.rectTransform, BubbleColor).rectTransform;
        var group = bubble.gameObject.AddComponent<CanvasGroup>();
        var bubbleText = SplitScreenView.CreateText("Emote", bubble); bubbleText.alignment = TextAlignmentOptions.Center; bubbleText.color = BubbleText; Apply(bubbleText);
        SplitScreenView.Stretch(bubbleText.rectTransform);
        bubble.gameObject.SetActive(false);
        return new Chip(root.rectTransform, root, edges, ring, avatar, back, initial, name, bubble, group, bubbleText);
    }

    private TextMeshProUGUI Text(string name, Transform parent)
    { var text = SplitScreenView.CreateText(name, parent); text.alignment = TextAlignmentOptions.MidlineLeft; text.color = NameColor; return text; }
    private void Apply(TextMeshProUGUI text) { text.font = _font; if (_material != null) text.fontSharedMaterial = _material; }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }

    internal void Clear() { _players.Clear(); _hover = -1; _following = null; _dirty = true; Layout(); }

    private sealed class Chip
    {
        internal readonly RectTransform Root, BubbleRoot;
        internal readonly Image Background, AvatarBack;
        internal readonly Image[] Edges, FollowRing;
        internal readonly RawImage Avatar;
        internal readonly TextMeshProUGUI Initial, Name, Bubble;
        internal readonly CanvasGroup BubbleGroup;
        internal float Speak, NameWidth, EmoteAge;
        internal bool Dirty;
        internal ulong SteamId;
        internal Chip(RectTransform root, Image background, Image[] edges, Image[] followRing, RawImage avatar, Image avatarBack, TextMeshProUGUI initial,
            TextMeshProUGUI name, RectTransform bubbleRoot, CanvasGroup bubbleGroup, TextMeshProUGUI bubble)
        {
            Root = root; Background = background; Edges = edges; FollowRing = followRing; Avatar = avatar; AvatarBack = avatarBack; Initial = initial;
            Name = name; BubbleRoot = bubbleRoot; BubbleGroup = bubbleGroup; Bubble = bubble;
        }
    }
}
