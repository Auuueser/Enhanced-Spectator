using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

public sealed partial class LethalCompanySpectatorRosterAdapter
{
    private readonly LethalCompanySpectatorRosterCursor _cursor = LethalCompanySpectatorRosterCursor.Shared;
    private readonly TextMeshProUGUI?[] _viewerNames = new TextMeshProUGUI?[32];
    private readonly Image?[] _tooltipEdges = new Image?[4];
    private RectTransform? _tooltip;
    private Image? _tooltipDivider;
    private TextMeshProUGUI? _tooltipHeader;
    private SpectatorRoster? _latestRoster;
    private bool _chinese;
    private int _rosterRevision, _tooltipRevision = -1, _hovered = -1, _hoveredCard = -1;

    /// <inheritdoc />
    public void UpdateInteraction(KeyCode cursorKey)
    {
        bool external = HasExternalCursorOwner();
        bool eligible = _visible && _root != null && _root.gameObject.activeInHierarchy && IsViewReady;
        if (!LethalCompanySpectatorRosterCursor.SplitScreenOwnsInput)
            _cursor.Update(eligible, external, !external && IsTogglePressed(cursorKey),
                SpectatorInputService.IsKeyPressedThisFrame(KeyCode.Escape));
        if (!SpectatorPointerCapture.IsActive || Mouse.current == null) { HideTooltip(); return; }
        UpdateHover(Mouse.current.position.ReadValue(), Mouse.current.leftButton.wasPressedThisFrame);
    }

    internal void UpdateHover(Vector2 screenPoint, bool click = false)
    {
        if (!SpectatorPointerCapture.IsActive || _latestRoster == null || _root == null || !_root.gameObject.activeInHierarchy)
        { HideTooltip(); HoverCard(-1); return; }
        var canvas = _root.GetComponentInParent<Canvas>()?.rootCanvas;
        if (canvas == null || !(canvas.transform is RectTransform canvasRect)) { HideTooltip(); HoverCard(-1); return; }
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        // Clicking a player's card watches that player.
        int card = -1;
        for (int i = 0; i < _latestRoster.Rows.Count; i++)
            if (_backgrounds[i] != null && _backgrounds[i]!.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(_backgrounds[i]!.rectTransform, screenPoint, camera))
            { card = i; break; }
        HoverCard(card);
        if (card >= 0 && click) Spectate(_latestRoster.Rows[card].Player);
        int hit = -1;
        for (int i = 0; i < _latestRoster.Rows.Count; i++)
            if (_watching[i] != null && _watching[i]!.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(_watching[i]!.rectTransform, screenPoint, camera))
            { hit = i; break; }
        if (hit < 0) { HideTooltip(); return; }
        if (_tooltip == null || _tooltip.parent != canvasRect || _hovered != hit || _tooltipRevision != _rosterRevision)
        { BuildTooltip(_latestRoster.Rows[hit], canvasRect); _hovered = hit; _tooltipRevision = _rosterRevision; }
        _tooltip!.gameObject.SetActive(true); KeepAboveKeyHints();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, camera, out Vector2 p))
        {
            float x = Mathf.Clamp(p.x - canvasRect.rect.xMin + 12, 12, Mathf.Max(12, canvasRect.rect.width - _tooltip.rect.width - 12));
            float y = Mathf.Clamp(canvasRect.rect.yMax - p.y - _tooltip.rect.height - 12, 12, Mathf.Max(12, canvasRect.rect.height - _tooltip.rect.height - 12));
            _tooltip.anchoredPosition = new Vector2(x, -y);
        }
    }

    private void BuildTooltip(SpectatorRosterRow row, RectTransform canvas)
    {
        if (_tooltip == null)
        {
            _tooltip = new GameObject("Enhanced Spectator Viewer Tooltip", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            _tooltip.SetParent(canvas, false); _tooltip.anchorMin = _tooltip.anchorMax = _tooltip.pivot = new Vector2(0, 1);
            var background = _tooltip.GetComponent<Image>(); background.raycastTarget = false; background.color = RosterStyle.Popup;
            for (int edge = 0; edge < 4; edge++) _tooltipEdges[edge] = Fill("Viewer list edge " + edge, _tooltip, RosterStyle.Border);
            _tooltipDivider = Fill("Viewer list divider", _tooltip, RosterStyle.Divider);
            _tooltipHeader = TooltipText("Viewer list heading");
        }
        if (_tooltip.parent != canvas) _tooltip.SetParent(canvas, false);
        int viewers = Mathf.Min(32, row.ViewerPlayers.Count), count = Mathf.Max(1, viewers);
        int columns = count <= 8 ? 1 : count <= 16 ? 2 : 4, rows = (count + columns - 1) / columns;
        float ui = TextScale(canvas);
        float width = Mathf.Min((columns == 1 ? 260 : columns == 2 ? 460 : 760) * ui, canvas.rect.width - 24);
        float cellWidth = (width - 16) / columns, font = Mathf.Clamp(_source!.fontSize * .55f, 10, 14) * ui, rowHeight = 20;
        for (int i = 0; i < 32; i++)
        {
            if (i >= count) { _viewerNames[i]?.gameObject.SetActive(false); continue; }
            if (_viewerNames[i] == null) _viewerNames[i] = TooltipText("Viewer full name " + i);
            var text = _viewerNames[i]!; text.gameObject.SetActive(true); text.font = _source.font; text.color = viewers == 0 ? RosterStyle.Muted : RosterStyle.Text;
            text.enableWordWrapping = true; text.enableAutoSizing = false; text.overflowMode = TextOverflowModes.Overflow;
            text.text = viewers == 0 ? (_chinese ? "暂无已同步观众" : "No synced viewers")
                : SpectatorRosterPresentation.Name(row.ViewerPlayers[i].Name);
        }
        // Measure every full name with wrapping. A large list expands temporarily, rather than clipping names.
        for (int pass = 0; pass < 8; pass++)
        {
            rowHeight = font * 1.3f;
            for (int i = 0; i < count; i++)
            { var text = _viewerNames[i]!; text.fontSize = font; rowHeight = Mathf.Max(rowHeight, text.GetPreferredValues(text.text, cellWidth - 8, float.PositiveInfinity).y + 4); }
            float height = 40 * ui + rows * rowHeight;
            if (height <= canvas.rect.height - 24 || pass == 7) break;
            font *= Mathf.Min(.95f, (canvas.rect.height - 64) / (rows * rowHeight));
        }
        float tall = 40 * ui + rows * rowHeight;
        _tooltip.sizeDelta = new Vector2(width, tall);
        PlaceFill(_tooltipEdges[0]!, 0, 0, width, 1); PlaceFill(_tooltipEdges[1]!, 0, tall - 1, width, 1);
        PlaceFill(_tooltipEdges[2]!, 0, 0, 1, tall); PlaceFill(_tooltipEdges[3]!, width - 1, 0, 1, tall);
        PlaceFill(_tooltipDivider!, 8, 31 * ui, width - 16, 1);
        _tooltipHeader!.text = SpectatorRosterPresentation.Name(row.Player.Name)
            + (_chinese ? $" · 已同步观众 {row.Viewers} 人" : $" · {row.Viewers} synced viewers");
        _tooltipHeader.color = RosterStyle.Accent;
        Place(_tooltipHeader, 8, 6 * ui, width - 16, 24 * ui, font, true);
        for (int i = 0; i < count; i++)
        {
            var text = _viewerNames[i]!;
            Place(text, 8 + i / rows * cellWidth, 36 * ui + i % rows * rowHeight, cellWidth - 8, rowHeight, font, true);
            text.enableAutoSizing = false; text.overflowMode = TextOverflowModes.Overflow;
        }
    }

    /// <summary>Whether the visible roster panel is under the pointer; its clicks are not for views beneath it.</summary>
    internal static bool ContainsPointer(Vector2 screenPoint)
    {
        var root = _current?._root;
        if (root == null || !root.gameObject.activeInHierarchy) return false;
        var canvas = root.GetComponentInParent<Canvas>().rootCanvas;
        return RectTransformUtility.RectangleContainsScreenPoint(root, screenPoint, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera);
    }

    private void HoverCard(int card)
    {
        if (_hoveredCard == card) return;
        if (_hoveredCard >= 0 && _backgrounds[_hoveredCard] != null) _backgrounds[_hoveredCard]!.color = _cardColors[_hoveredCard];
        _hoveredCard = card;
        if (card >= 0 && _backgrounds[card] != null) _backgrounds[card]!.color = _cardColors[card] + new Color(.06f, .06f, .06f, .06f);
    }

    // The same vanilla fields SpectateNextPlayer sets, marked as a manual switch so no death hand-off travels.
    private static void Spectate(SpectatorRosterPlayer target)
    {
        var round = StartOfRound.Instance;
        var local = round != null ? round.localPlayerController : null;
        if (local == null || !local.isPlayerDead || round!.allPlayerScripts == null || target.SlotId >= (ulong)round.allPlayerScripts.Length) return;
        var player = round.allPlayerScripts[target.SlotId];
        if (player == null || player.actualClientId != target.ClientId || player.isPlayerDead || !player.isPlayerControlled
            || player == local || player == local.spectatedPlayerScript) return;
        SpectatorFreecamController.Current?.BeginVanillaTargetSwitch(false);
        local.spectatedPlayerScript = player; local.spectatedPlayerDeadTimer = 0;
        round.SetPlayerSafeInShip(); local.SetSpectatedPlayerEffects(false);
        SpectatorFreecamController.Current?.EndVanillaTargetSwitch(false);
    }

    private TextMeshProUGUI TooltipText(string name)
    { var text = Clone(_source!, name); text.transform.SetParent(_tooltip, false); return text; }
    private void HideTooltip()
    { _hovered = -1; if (_tooltip != null) _tooltip.gameObject.SetActive(false); }
    private void ClearInteraction()
    {
        if (!LethalCompanySpectatorRosterCursor.SplitScreenOwnsInput) _cursor.Release(HasExternalCursorOwner());
        HideTooltip();
    }
    private bool HasExternalCursorOwner()
        => _input.IsUiInputBlocked() || !Application.isFocused
            || !RuntimeConnectionState.CanRunLocalDiagnostics(out _)
            || StartOfRound.Instance == null || StartOfRound.Instance.localPlayerController == null
            || StartOfRound.Instance.localPlayerController.isInGameOverAnimation > 0;
}
