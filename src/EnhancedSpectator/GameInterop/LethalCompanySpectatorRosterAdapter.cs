using System;
using System.Collections.Generic;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>Owned spectator HUD with opt-in viewer hover; reads verified V81 identities directly.</summary>
public sealed partial class LethalCompanySpectatorRosterAdapter : IGameSpectatorRosterAdapter
{
    private static LethalCompanySpectatorRosterAdapter? _current;
    internal static void KeepAboveKeyHints()
    {
        var current = _current;
        if (current == null || current._root == null || !current._root.gameObject.activeInHierarchy) return;
        var panel = current._root;
        bool tooltipVisible = current._tooltip != null && current._tooltip.gameObject.activeInHierarchy
            && current._tooltip.parent == panel.parent;
        int last = panel.parent.childCount - 1;
        if (panel.GetSiblingIndex() == last - (tooltipVisible ? 1 : 0)
            && (!tooltipVisible || current._tooltip!.GetSiblingIndex() == last)) return;
        panel.SetAsLastSibling();
        if (tooltipVisible) current._tooltip!.SetAsLastSibling();
    }
    private readonly LethalCompanySpectatorAdapter _input = new LethalCompanySpectatorAdapter();
    private readonly TextMeshProUGUI?[] _names = new TextMeshProUGUI?[32], _counts = new TextMeshProUGUI?[32], _watching = new TextMeshProUGUI?[32];
    private readonly Image?[] _backgrounds = new Image?[32], _markers = new Image?[32], _edges = new Image?[4];
    private readonly Color[] _cardColors = new Color[32];
    private RectTransform? _root;
    private Image? _divider;
    private TextMeshProUGUI? _source, _label, _title, _header, _footer;
    private Material? _material;
    private SpectatorTargetState? _local;
    private string _targetName = string.Empty;
    private bool _visible;
    private Vector2 _canvasSize;

    /// <inheritdoc />
    public bool IsViewReady => _root != null && _source != null && HUDManager.Instance != null
        && _source == HUDManager.Instance.spectatingPlayerText && _title != null && _title.font == _source.font
        && _root.parent == RosterCanvas(_source)?.transform
        && _root.parent is RectTransform canvas && canvas.rect.size == _canvasSize;

    private static Canvas? RosterCanvas(TextMeshProUGUI source) => Features.SplitScreen.SplitScreenModule.Current?.OverlayCanvas
        ?? source.GetComponentInParent<Canvas>()?.rootCanvas;

    /// <inheritdoc />
    public bool TryGetLocalTarget(out SpectatorTargetState target)
    {
        target = null!;
        var round = StartOfRound.Instance;
        var local = round != null ? round.localPlayerController : null;
        var watched = local != null ? local.spectatedPlayerScript : null;
        if (local == null || !local.isPlayerDead || local.isInGameOverAnimation > 0 || round!.overrideSpectateCamera
            || watched == null || watched.isPlayerDead || watched.disconnectedMidGame || _input.IsUiInputBlocked()) return false;
        _targetName = PlayerDisplayNames.Of(watched);
        if (_local == null || _local.LocalClientId != local.actualClientId || _local.LocalPlayerSlotId != local.playerClientId
            || _local.TargetClientId != watched.actualClientId || _local.TargetPlayerSlotId != watched.playerClientId)
            _local = new SpectatorTargetState(true, local.actualClientId, local.playerClientId, watched.actualClientId, watched.playerClientId, DateTime.UtcNow.Ticks);
        target = _local;
        return true;
    }
    /// <inheritdoc />
    public bool IsTogglePressed(KeyCode key) => SpectatorInputService.IsKeyPressedThisFrame(key);

    /// <inheritdoc />
    public void CopyPlayersTo(List<SpectatorRosterPlayer> players)
    {
        players.Clear();
        var round = StartOfRound.Instance;
        if (round == null || round.ClientPlayerList == null || round.allPlayerScripts == null) return;
        foreach (var entry in round.ClientPlayerList)
        {
            int slot = entry.Value;
            if (slot < 0 || slot >= round.allPlayerScripts.Length) continue;
            var player = round.allPlayerScripts[slot];
            if (player == null || player.disconnectedMidGame || player.actualClientId != entry.Key) continue;
            players.Add(new SpectatorRosterPlayer(entry.Key, (ulong)slot, PlayerDisplayNames.Of(player), player.isPlayerDead));
        }
        players.Sort((a, b) => a.SlotId.CompareTo(b.SlotId));
    }

    /// <inheritdoc />
    public void Present(SpectatorRoster roster, SpectatorTargetState local, bool chinese)
    {
        var source = HUDManager.Instance != null ? HUDManager.Instance.spectatingPlayerText : null;
        var canvas = source != null ? RosterCanvas(source) : null;
        var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        if (source == null || canvasRect == null) { Dispose(); return; }
        if (_root == null || _source != source)
        {
            Dispose(); _source = source;
            _root = new GameObject("Enhanced Spectator Watch Roster", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            _root.SetParent(canvasRect, false);
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(1, 0);
            _root.anchoredPosition = new Vector2(-12, 12);
            Image panel = _root.GetComponent<Image>(); panel.color = RosterStyle.Panel; panel.raycastTarget = false;
            for (int edge = 0; edge < 4; edge++) _edges[edge] = Fill("Panel edge " + edge, _root, RosterStyle.Border);
            _divider = Fill("Header divider", _root, RosterStyle.Divider);
            _label = Clone(source, "Watch label"); _title = Clone(source, "Watched player name");
            _header = Clone(source, "Watch summary"); _footer = Clone(source, "Roster coverage");
        }
        if (_root!.parent != canvasRect) _root.SetParent(canvasRect, false);
        _current = this;
        _latestRoster = roster; _chinese = chinese; _rosterRevision++;
        bool incomplete = roster.UnknownTargets > 0;
        _canvasSize = canvasRect.rect.size;
        int count = roster.Rows.Count;
        int columns = SpectatorRosterPresentation.Columns(count), rows = SpectatorRosterPresentation.RowCount(count);
        float ui = TextScale(canvasRect);
        float width = Mathf.Min((columns == 1 ? 250f : columns == 2 ? 380f : 600f) * ui, Mathf.Max(140, canvasRect.rect.width - 24));
        float font = Mathf.Clamp(source.fontSize * .5f, 11f, 13f);
        // Unscaled layout: header (label + name), divider, card grid, footer.
        float pad = 8, nameLine = font * 1.35f, detailLine = font * 1.15f, cardHeight = nameLine + detailLine + 6, gap = 3;
        float headerHeight = font * 1.15f + font * 1.5f, gridTop = pad + headerHeight + 8;
        float footerHeight = font * 1.15f;
        float height = gridTop + (count > 0 ? rows * (cardHeight + gap) : 0) + footerHeight + pad;
        float scale = ui * Mathf.Min(1, Mathf.Max(70, canvasRect.rect.height * .45f) / (height * ui));
        font *= scale; pad *= scale; nameLine *= scale; detailLine *= scale; cardHeight *= scale; gap *= scale;
        headerHeight *= scale; gridTop *= scale; footerHeight *= scale; height *= scale;
        _root!.sizeDelta = new Vector2(width, height);
        PlaceFill(_edges[0]!, 0, 0, width, 1); PlaceFill(_edges[1]!, 0, height - 1, width, 1);
        PlaceFill(_edges[2]!, 0, 0, 1, height); PlaceFill(_edges[3]!, width - 1, 0, 1, height);

        _label!.text = chinese ? "正在观看" : "WATCHING";
        _label.color = RosterStyle.Accent; _label.characterSpacing = 4;
        Place(_label, pad, pad, width * .4f, font * 1.15f, font * .82f, false);
        _header!.text = SpectatorRosterPresentation.Count(roster.CurrentViewers, incomplete, chinese)
            + (chinese ? $" · 共 {roster.Spectators} 人观战" : $" · {roster.Spectators} spectators");
        _header.color = RosterStyle.Muted;
        Place(_header, width * .4f, pad, width * .6f - pad, font * 1.15f, font * .82f, false);
        _header.alignment = TextAlignmentOptions.TopRight;
        _title!.text = SpectatorRosterPresentation.Name(_targetName);
        _title.color = RosterStyle.Text; _title.fontStyle = FontStyles.Bold;
        Place(_title, pad, pad + font * 1.15f, width - pad * 2, font * 1.5f, font * 1.15f, false);
        PlaceFill(_divider!, pad, pad + headerHeight + 3 * scale, width - pad * 2, 1);

        float cellWidth = (width - pad * 2 + gap * 2) / columns;
        for (int i = 0; i < 32; i++)
        {
            if (i >= count)
            {
                _names[i]?.gameObject.SetActive(false); _counts[i]?.gameObject.SetActive(false); _watching[i]?.gameObject.SetActive(false);
                _backgrounds[i]?.gameObject.SetActive(false); _markers[i]?.gameObject.SetActive(false); continue;
            }
            if (_names[i] == null)
            {
                _backgrounds[i] = Fill("Player card " + i, _root, RosterStyle.Card);
                _markers[i] = Fill("Current target marker " + i, _root, RosterStyle.Accent);
                _names[i] = Clone(source, "Player name " + i); _counts[i] = Clone(source, "Player count " + i);
                _watching[i] = Clone(source, "Watching target " + i);
            }
            var row = roster.Rows[i];
            bool current = local.TargetClientId == row.Player.ClientId;
            float x = pad + i / rows * cellWidth, y = gridTop + i % rows * (cardHeight + gap), cardWidth = cellWidth - gap * 2;
            var background = _backgrounds[i]!; var marker = _markers[i]!;
            var name = _names[i]!; var viewers = _counts[i]!; var watching = _watching[i]!;
            background.gameObject.SetActive(true); marker.gameObject.SetActive(current);
            name.gameObject.SetActive(true); viewers.gameObject.SetActive(true); watching.gameObject.SetActive(true);
            background.color = _cardColors[i] = current ? RosterStyle.CurrentCard : RosterStyle.Card;
            if (i == _hoveredCard) background.color += new Color(.06f, .06f, .06f, .06f);
            PlaceFill(background, x, y, cardWidth, cardHeight);
            PlaceFill(marker, x, y, 2 * Mathf.Max(scale, .5f), cardHeight);
            float inset = 7 * scale;
            viewers.text = SpectatorRosterPresentation.Badge(row.Viewers, incomplete, chinese);
            viewers.color = row.Viewers > 0 ? RosterStyle.Accent : RosterStyle.Faint;
            // The count takes only its own width; the name keeps the rest of the card.
            viewers.font = source.font; viewers.fontSize = font * .9f;
            float badge = viewers.GetPreferredValues(viewers.text).x + 4 * scale;
            Place(viewers, x + cardWidth - badge - inset, y + 3 * scale, badge, nameLine, font * .9f, false);
            viewers.alignment = TextAlignmentOptions.TopRight;
            name.text = SpectatorRosterPresentation.Name(row.Player.Name);
            name.color = current ? RosterStyle.Highlight : RosterStyle.Text;
            Place(name, x + inset, y + 3 * scale, cardWidth - inset * 2 - badge, nameLine, font, false);
            watching.text = SpectatorRosterPresentation.Watching(row, chinese);
            watching.color = row.Viewers > 0 ? RosterStyle.Muted : RosterStyle.Faint;
            Place(watching, x + inset, y + 3 * scale + nameLine, cardWidth - inset * 2, detailLine, font * .82f, false);
        }
        _footer!.text = chinese ? "观众信息仅来自增强观战用户" : "Viewer data only from Enhanced Spectator users.";
        _footer.color = RosterStyle.Faint;
        Place(_footer, pad, height - pad - footerHeight, width - pad * 2, footerHeight, font * .76f, true);
        _footer.enableWordWrapping = false;
        SetVisible(_visible);
    }

    /// <summary>Muted Lethal Company terminal palette: dark glass, amber accents.</summary>
    private static class RosterStyle
    {
        internal static readonly Color Panel = new Color(.035f, .035f, .04f, .86f);
        internal static readonly Color Popup = new Color(.03f, .03f, .035f, 1);
        internal static readonly Color Border = new Color(.96f, .55f, .2f, .55f);
        internal static readonly Color Divider = new Color(.96f, .55f, .2f, .35f);
        internal static readonly Color Card = new Color(1, 1, 1, .045f);
        internal static readonly Color CurrentCard = new Color(.96f, .55f, .2f, .16f);
        internal static readonly Color Accent = new Color(.98f, .6f, .24f, 1);
        internal static readonly Color Highlight = new Color(1, .86f, .7f, 1);
        internal static readonly Color Text = new Color(.93f, .93f, .9f, 1);
        internal static readonly Color Muted = new Color(.64f, .67f, .68f, 1);
        internal static readonly Color Faint = new Color(.48f, .5f, .5f, 1);
    }

    private static Image Fill(string name, RectTransform parent, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false); image.raycastTarget = false; image.color = color;
        image.transform.SetAsFirstSibling();
        return image;
    }
    private static void PlaceFill(Image image, float x, float y, float width, float height)
    {
        var rect = (RectTransform)image.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }

    private TextMeshProUGUI Clone(TextMeshProUGUI source, string name)
    {
        var text = UnityEngine.Object.Instantiate(source, _root, false);
        text.gameObject.SetActive(true);
        text.name = name; text.enabled = true; text.raycastTarget = false; text.richText = false;
        text.color = Color.white; text.margin = Vector4.zero; text.fontStyle = FontStyles.Normal; text.characterSpacing = 0;
        text.rectTransform.localScale = Vector3.one; text.rectTransform.localRotation = Quaternion.identity;
        return text;
    }
    private void Place(TextMeshProUGUI text, float x, float y, float width, float height, float font, bool multiline)
    {
        RectTransform rect = text.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
        rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(width,height);
        text.font = _source!.font; text.fontSharedMaterial = Material(); text.fontSize = font; text.enableAutoSizing = multiline;
        text.fontSizeMin = Mathf.Min(8,font); text.fontSizeMax = font; text.enableWordWrapping = multiline;
        text.overflowMode = TextOverflowModes.Ellipsis; text.alignment = TextAlignmentOptions.TopLeft;
    }
    // On the split-screen's pixel-sized canvas the panel grows with the screen like the split labels; the game's
    // own canvas already scales. Both use the dilated label material, so thin 3270 strokes stay whole when small.
    private static float TextScale(RectTransform canvas)
        => canvas == Features.SplitScreen.SplitScreenModule.Current?.OverlayCanvas?.transform ? SpectatorTextStyle.UiScale(canvas.rect.size) : 1;
    private Material Material() => _material ??= SpectatorTextStyle.CreateLabelMaterial(_source!.font);
    /// <inheritdoc />
    public void SetVisible(bool visible)
    {
        _visible = visible;
        bool active = visible && !LethalCompanySpectatorUiVisibility.Hidden;
        if (!active) ClearInteraction();
        if (_root != null && _root.gameObject.activeSelf != active) _root.gameObject.SetActive(active);
        if (active) KeepAboveKeyHints();
    }
    /// <inheritdoc />
    public void Dispose()
    {
        ClearInteraction();
        if (_tooltip != null) UnityEngine.Object.Destroy(_tooltip.gameObject);
        _tooltip = null; _tooltipHeader = null; _tooltipDivider = null; _latestRoster = null;
        Array.Clear(_tooltipEdges, 0, 4);
        Array.Clear(_viewerNames, 0, _viewerNames.Length);
        if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
        if (_material != null) UnityEngine.Object.Destroy(_material);
        _root = null; _divider = null; _material = null; _hoveredCard = -1; _source = _label = _title = _header = _footer = null;
        Array.Clear(_names, 0, 32); Array.Clear(_counts, 0, 32); Array.Clear(_watching, 0, 32);
        Array.Clear(_backgrounds, 0, 32); Array.Clear(_markers, 0, 32); Array.Clear(_edges, 0, 4);
        if (_current == this) _current = null;
    }
}
