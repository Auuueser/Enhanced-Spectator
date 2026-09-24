using EnhancedSpectator.Config;
using EnhancedSpectator.Features.Spectator;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

public sealed partial class LethalCompanyFearQuickMenuAdapter
{
    private const int HotkeysPerPage = 7;
    private GameObject? _hotkeysRoot;
    private int _hotkeyPage, _listeningKey = -1, _listenAfterFrame;
    private readonly GameObject?[] _hotkeyRows = new GameObject?[HotkeysPerPage];
    private readonly TextMeshProUGUI?[] _hotkeyLabels = new TextMeshProUGUI?[HotkeysPerPage];
    private readonly TextMeshProUGUI?[] _hotkeyValues = new TextMeshProUGUI?[HotkeysPerPage];
    private TextMeshProUGUI? _hotkeyStatus;
    private string? _hotkeyFeedback;
    private float _hotkeyFeedbackUntil;
    private static readonly KeyCode[] BindingKeys = (KeyCode[])System.Enum.GetValues(typeof(KeyCode));

    private void BuildHotkeys(Transform parent, Button? template, TextMeshProUGUI? textTemplate)
    {
        _hotkeysRoot = CreateRectObject("Hotkey Settings", parent); Stretch((RectTransform)_hotkeysRoot.transform);
        for (int slot = 0; slot < HotkeysPerPage; ++slot)
        {
            int captured = slot;
            var row = CreateRectObject("Hotkey Row " + slot, _hotkeysRoot.transform);
            SetTopLeft((RectTransform)row.transform, 16, 94 + slot * 36, 488, 32); _hotkeyRows[slot] = row;
            var label = CreateText("Action", row.transform, textTemplate, 13, TextAlignmentOptions.MidlineLeft);
            SetTopLeft(label.rectTransform, 8, 0, 328, 32); label.enableWordWrapping = false; _hotkeyLabels[slot] = label;
            var button = CreateButton("Bind", row.transform, template, out var obj); SetTopLeft((RectTransform)obj.transform, 338, 0, 150, 32);
            _hotkeyValues[slot] = CreateButtonText(obj.transform, textTemplate, string.Empty, 13);
            button.onClick.AddListener(() => { _listeningKey = _hotkeyPage * HotkeysPerPage + captured; _listenAfterFrame = Time.frameCount + 1; });
        }
        var previous = CreateButton("Hotkeys Previous", _hotkeysRoot.transform, template, out var previousObj);
        SetTopLeft((RectTransform)previousObj.transform, 16, 346, 62, 28); CreatePageGlyph(previousObj.transform, false);
        previous.onClick.AddListener(() => { CancelHotkeyCapture(); _hotkeyPage = Mathf.Max(0, _hotkeyPage - 1); });
        var next = CreateButton("Hotkeys Next", _hotkeysRoot.transform, template, out var nextObj);
        SetTopLeft((RectTransform)nextObj.transform, 442, 346, 62, 28); CreatePageGlyph(nextObj.transform, true);
        next.onClick.AddListener(() => { CancelHotkeyCapture(); _hotkeyPage = Mathf.Min((_options.Hotkeys.Bindings.Length - 1) / HotkeysPerPage, _hotkeyPage + 1); });
        _hotkeyStatus = CreateText("Hotkey Status", _hotkeysRoot.transform, textTemplate, 12, TextAlignmentOptions.Center);
        SetTopLeft(_hotkeyStatus.rectTransform, 82, 346, 356, 28); _hotkeyStatus.enableWordWrapping = false;
    }

    private void CancelHotkeyCapture() { _listeningKey = -1; _hotkeyFeedback = null; }

    private void RenderHotkeys(bool chinese, bool visible)
    {
        if (_hotkeysRoot == null) return;
        _hotkeysRoot.SetActive(visible);
        if (!visible) { CancelHotkeyCapture(); return; }
        if (_listeningKey >= 0 && Time.frameCount >= _listenAfterFrame)
        {
            if (SpectatorInputService.IsKeyPressedThisFrame(KeyCode.Escape)) CancelHotkeyCapture();
            else
            {
                foreach (var key in BindingKeys)
                {
                    if (key == KeyCode.Escape || !SpectatorInputService.IsKeyPressedThisFrame(key)) continue;
                    var selected = key == KeyCode.Backspace ? KeyCode.None : key;
                    if (_options.Hotkeys.TryAssign(_listeningKey, selected, out int conflict))
                    { _listeningKey = -1; _hotkeyFeedback = chinese ? "已保存" : "Saved"; }
                    else
                    {
                        var binding = conflict >= 0 ? _options.Hotkeys.Bindings[conflict] : null;
                        _hotkeyFeedback = binding == null ? (chinese ? "此键不可用" : "Unsupported key")
                            : (chinese ? "已用于：" + binding.Chinese : "In use: " + binding.English);
                    }
                    _hotkeyFeedbackUntil = Time.unscaledTime + 3f; break;
                }
            }
        }
        for (int slot = 0; slot < HotkeysPerPage; ++slot)
        {
            int index = _hotkeyPage * HotkeysPerPage + slot; bool exists = index < _options.Hotkeys.Bindings.Length;
            _hotkeyRows[slot]?.SetActive(exists); if (!exists) continue;
            var binding = _options.Hotkeys.Bindings[index];
            SetText(_hotkeyLabels[slot], chinese ? binding.Chinese : binding.English);
            SetText(_hotkeyValues[slot], index == _listeningKey ? (chinese ? "按下新按键…" : "Press a key…") : SpectatorHotkeySettings.KeyLabel(binding.Entry.Value, chinese));
        }
        string status = _hotkeyFeedback != null && Time.unscaledTime < _hotkeyFeedbackUntil ? _hotkeyFeedback
            : _listeningKey >= 0 ? (chinese ? "Esc 取消 · Backspace 清除" : "Esc: cancel · Backspace: clear")
            : $"{_hotkeyPage + 1} / {(_options.Hotkeys.Bindings.Length + HotkeysPerPage - 1) / HotkeysPerPage}";
        SetText(_hotkeyStatus, status);
    }
}
