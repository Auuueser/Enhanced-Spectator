using BepInEx.Bootstrap;
using EnhancedSpectator.Features.Spectator;
using TMPro;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Moves the spectator name below BetterClock without modifying the clock.</summary>
public sealed class LethalCompanySpectatorHudAdapter : IGameSpectatorHudAdapter
{
    private static LethalCompanySpectatorHudAdapter? _current;
    internal static void BeforeSubmit() => _current?.UpdateLayout();
    private TextMeshProUGUI? _name;
    private RectTransform? _clockSource;
    private Vector2 _canvasSize, _clockSize;
    private readonly SpectatorClockAnchor _clockAnchor = new SpectatorClockAnchor();
    private Vector2 _position;
    private Vector2 _size;
    private bool _autoSize;
    private bool _wrap;
    private float _fontSize;
    private float _fontMin;
    private float _fontMax;
    private TextOverflowModes _overflow;
    private readonly Vector3[] _corners = new Vector3[4];

    /// <inheritdoc />
    public void UpdateLayout()
    {
        var hud = HUDManager.Instance;
        var local = StartOfRound.Instance != null ? StartOfRound.Instance.localPlayerController : null;
        if (!Chainloader.PluginInfos.ContainsKey("BlueAmulet.BetterClock") || local == null
            || hud == null || hud.clockNumber == null || hud.spectatingPlayerText == null)
        {
            Dispose();
            return;
        }
        var clock = hud.clockNumber.transform.parent as RectTransform;
        var canvas = hud.spectatingPlayerText.GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        if (clock == null || canvasRect == null) return;
        bool changed = _clockSource != clock || _canvasSize != canvasRect.rect.size || _clockSize != clock.rect.size;
        _clockSource = clock; _canvasSize = canvasRect.rect.size; _clockSize = clock.rect.size;
        // Reserve the compact clock region even while its CanvasGroup is invisible.
        // Cache in root-canvas coordinates before death; fade-in/parent animation cannot drag the label.
        float clockY = _clockAnchor.Update(BottomIn(clock, canvasRect), local.isPlayerDead, changed);
        if (!local.isPlayerDead) { RestoreName(); return; }
        if (_name != hud.spectatingPlayerText)
        {
            RestoreName();
            _name = hud.spectatingPlayerText;
            _position = _name.rectTransform.anchoredPosition;
            _size = _name.rectTransform.sizeDelta;
            _autoSize = _name.enableAutoSizing;
            _wrap = _name.enableWordWrapping;
            _fontSize = _name.fontSize;
            _fontMin = _name.fontSizeMin;
            _fontMax = _name.fontSizeMax;
            _overflow = _name.overflowMode;
        }
        _current = this;
        var rect = _name.rectTransform;
        var parent = rect.parent as RectTransform;
        if (parent == null) return;
        float clockBottom = parent.InverseTransformPoint(canvasRect.TransformPoint(new Vector3(0f, clockY, 0f))).y;
        float canvasBottom = BottomIn(canvasRect, parent) + 8f;
        canvasRect.GetWorldCorners(_corners);
        float canvasLeft = parent.InverseTransformPoint(_corners[0]).x + 8f;
        float canvasRight = parent.InverseTransformPoint(_corners[3]).x - 8f;
        float originalWidth = _size.x + parent.rect.width * (rect.anchorMax.x - rect.anchorMin.x);
        float originalHeight = _size.y + parent.rect.height * (rect.anchorMax.y - rect.anchorMin.y);
        float width = Mathf.Min(originalWidth, Mathf.Max(0f, canvasRight - canvasLeft));
        if (!Mathf.Approximately(rect.rect.width, width)) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        float height = SpectatorHudLayoutRules.AvailableHeight(clockBottom, canvasBottom, Mathf.Min(originalHeight, _fontSize * 1.2f), gap: 4f);
        if (!Mathf.Approximately(rect.rect.height, height)) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        rect.GetWorldCorners(_corners);
        Vector2 appliedOffset = rect.anchoredPosition - _position;
        float top = parent.InverseTransformPoint(_corners[1]).y - appliedOffset.y;
        float left = parent.InverseTransformPoint(_corners[0]).x - appliedOffset.x;
        float right = parent.InverseTransformPoint(_corners[3]).x - appliedOffset.x;
        float shiftX = left < canvasLeft ? canvasLeft - left : right > canvasRight ? canvasRight - right : 0f;
        float shiftY = SpectatorHudLayoutRules.DownwardOffset(top, clockBottom, gap: 4f);
        // Keep an already-low original position above the canvas floor without crossing the clock.
        shiftY = Mathf.Max(shiftY, canvasBottom + height - top);
        rect.anchoredPosition = _position + new Vector2(shiftX, shiftY);
        _name.enableWordWrapping = false;
        _name.enableAutoSizing = true;
        _name.fontSizeMin = Mathf.Min(10f, _fontSize);
        _name.fontSizeMax = _fontSize;
        _name.overflowMode = TextOverflowModes.Ellipsis;
    }

    private float BottomIn(RectTransform rect, Transform space)
    {
        rect.GetWorldCorners(_corners);
        return Mathf.Min(space.InverseTransformPoint(_corners[0]).y, space.InverseTransformPoint(_corners[3]).y);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        RestoreName();
        _clockSource = null;
        _clockAnchor.Clear();
    }

    private void RestoreName()
    {
        if (_name != null)
        {
            _name.rectTransform.anchoredPosition = _position;
            _name.rectTransform.sizeDelta = _size;
            _name.enableAutoSizing = _autoSize;
            _name.enableWordWrapping = _wrap;
            _name.fontSize = _fontSize;
            _name.fontSizeMin = _fontMin;
            _name.fontSizeMax = _fontMax;
            _name.overflowMode = _overflow;
        }
        _name = null;
        if (_current == this) _current = null;
    }
}
