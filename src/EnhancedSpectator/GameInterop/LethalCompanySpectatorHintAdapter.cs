using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.Spectator;
using TMPro;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Fixed single-line action/key columns, borrowing native vote text styling.</summary>
internal sealed class LethalCompanySpectatorHintAdapter : IGameSpectatorHudAdapter
{
    private readonly EnhancedSpectatorConfig _config;
    private readonly Func<bool> _fearAllowed;
    private TextMeshProUGUI? _source;
    private RectTransform? _root;
    private int _nextRefresh;
    private readonly List<(TextMeshProUGUI label, TextMeshProUGUI keys)> _rows = new List<(TextMeshProUGUI, TextMeshProUGUI)>();
    private readonly Vector3[] _corners = new Vector3[4];
    internal LethalCompanySpectatorHintAdapter(EnhancedSpectatorConfig config, Func<bool> fearAllowed)
    { _config = config; _fearAllowed = fearAllowed; }
    public void UpdateLayout()
    {
        var round = StartOfRound.Instance;
        var local = round != null ? round.localPlayerController : null;
        var hud = HUDManager.Instance;
        if (!_config.EnableEnhancedSpectator.Value || local == null || !local.isPlayerDead
            || local.isInGameOverAnimation > 0 || round!.overrideSpectateCamera
            || local.spectatedPlayerScript == null || local.spectatedPlayerScript.isPlayerDead
            || hud == null || hud.holdButtonToEndGameEarlyText == null) { Dispose(); return; }
        var source = hud.holdButtonToEndGameEarlyText;
        if (_source != source || _root == null)
        {
            Dispose(); _source = source;
            _root = new GameObject("EnhancedSpectator Key Hints", typeof(RectTransform)).GetComponent<RectTransform>();
            _root.SetParent(source.transform.parent, false);
            _root.anchorMin = _root.anchorMax = Vector2.zero; _root.pivot = new Vector2(1, 1);
        }
        bool visible = _config.Camera.ShowKeyHints.Value && !LethalCompanySpectatorUiVisibility.Hidden
            && (local.quickMenuManager == null || !local.quickMenuManager.isMenuOpen)
            && !local.isTypingChat && !round.localPlayerUsingController;
        _root.gameObject.SetActive(visible);
        if (!visible) { _nextRefresh = 0; return; }
        if (Time.frameCount < _nextRefresh) return;
        _nextRefresh = Time.frameCount + 6;
        var controller = SpectatorFreecamController.Current;
        SpectatorCameraMode? mode = controller?.OwnsCamera == true ? controller.State.Mode : null;
        var rows = SpectatorHintText.Rows(_config, mode, _fearAllowed());
        var canvas = source.GetComponentInParent<Canvas>();
        if (canvas == null || !(_root.parent is RectTransform parent)) return;
        ((RectTransform)canvas.transform).GetWorldCorners(_corners);
        float left = parent.InverseTransformPoint(_corners[0]).x + 12f;
        float right = parent.InverseTransformPoint(_corners[2]).x - 12f;
        float bottom = parent.InverseTransformPoint(_corners[0]).y + 12f;
        float top = BottomIn(source.rectTransform, parent) - 10f;
        if (hud.holdButtonToEndGameEarlyVotesText != null && hud.holdButtonToEndGameEarlyVotesText.enabled
            && !string.IsNullOrEmpty(hud.holdButtonToEndGameEarlyVotesText.text))
            top = Mathf.Min(top, BottomIn(hud.holdButtonToEndGameEarlyVotesText.rectTransform, parent) - 8f);
        if (hud.holdButtonToEndGameEarlyMeter != null && hud.holdButtonToEndGameEarlyMeter.gameObject.activeInHierarchy)
            top = Mathf.Min(top, BottomIn(hud.holdButtonToEndGameEarlyMeter.rectTransform, parent) - 8f);
        float maxWidth = Mathf.Max(1, Mathf.Min(360f, (right - left) * .45f));
        float font = Mathf.Max(1, Mathf.Min(source.fontSize * .85f, (top - bottom) / Mathf.Max(1, rows.Count) / 1.2f));
        float labelWidth = 0f, keyWidth = 0f;
        for (int i = 0; i < rows.Count; i++)
        {
            if (i == _rows.Count) _rows.Add((Clone(source, "Action"), Clone(source, "Keys")));
            labelWidth = Mathf.Max(labelWidth, Measure(_rows[i].label, rows[i].Label, font));
            keyWidth = Mathf.Max(keyWidth, Measure(_rows[i].keys, "[" + rows[i].Keys + "]", font));
        }
        var layout = SpectatorHintLayout.Fit(labelWidth, keyWidth, font * .65f, maxWidth);
        font *= layout.Scale;
        labelWidth = layout.Label;
        keyWidth = layout.Keys;
        float width = layout.Width;
        float lineHeight = font * 1.2f;
        _root.sizeDelta = new Vector2(width, lineHeight * rows.Count);
        _root.localPosition = new Vector3(right, top, source.rectTransform.localPosition.z);
        for (int i = 0; i < rows.Count; i++)
        {
            if (i == _rows.Count) _rows.Add((Clone(source, "Action"), Clone(source, "Keys")));
            var pair = _rows[i]; pair.label.gameObject.SetActive(true); pair.keys.gameObject.SetActive(true);
            SetCell(pair.label, rows[i].Label, 0, i, labelWidth, lineHeight, font, TextAlignmentOptions.MidlineRight);
            SetCell(pair.keys, "[" + rows[i].Keys + "]", labelWidth + layout.Gap, i, keyWidth, lineHeight, font, TextAlignmentOptions.MidlineLeft);
        }
        for (int i = rows.Count; i < _rows.Count; i++) { _rows[i].label.gameObject.SetActive(false); _rows[i].keys.gameObject.SetActive(false); }
    }
    private float Measure(TextMeshProUGUI cell, string value, float font)
    {
        cell.font = _source!.font; cell.fontSize = font; cell.enableAutoSizing = false;
        return cell.GetPreferredValues(value, float.PositiveInfinity, float.PositiveInfinity).x + 2f;
    }
    private TextMeshProUGUI Clone(TextMeshProUGUI source, string name)
    {
        var text = UnityEngine.Object.Instantiate(source, _root, false);
        text.name = name; text.enabled = true; text.raycastTarget = false;
        text.enableWordWrapping = false; text.enableAutoSizing = true; text.overflowMode = TextOverflowModes.Ellipsis;
        text.margin = Vector4.zero; text.rectTransform.localScale = Vector3.one;
        text.rectTransform.localRotation = Quaternion.identity;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0, 1);
        return text;
    }
    private void SetCell(TextMeshProUGUI cell, string value, float x, int row, float width, float height, float font, TextAlignmentOptions align)
    {
        if (cell.text != value) cell.text = value;
        cell.font = _source!.font; cell.fontSharedMaterial = _source.fontSharedMaterial; cell.color = _source.color;
        cell.enableAutoSizing = true;
        cell.fontSizeMax = font; cell.fontSizeMin = font * .7f; cell.alignment = align;
        cell.rectTransform.anchoredPosition = new Vector2(x, -row * height); cell.rectTransform.sizeDelta = new Vector2(width, height);
    }
    private float BottomIn(RectTransform rect, Transform parent)
    { rect.GetWorldCorners(_corners); return Mathf.Min(parent.InverseTransformPoint(_corners[0]).y, parent.InverseTransformPoint(_corners[3]).y); }
    public void Dispose()
    {
        if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
        _root = null; _source = null; _rows.Clear(); _nextRefresh = 0;
    }
}
