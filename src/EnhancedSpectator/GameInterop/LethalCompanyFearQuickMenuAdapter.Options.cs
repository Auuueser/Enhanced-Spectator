using EnhancedSpectator.Features.FearMode;
using EnhancedSpectator.Features.Spectator;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

public sealed partial class LethalCompanyFearQuickMenuAdapter
{
    private readonly SpectatorOptionsController _options;
    private GameObject? _optionsRoot;
    private GameObject? _catalogRoot;
    private bool _optionsOpen;
    private bool _optionsIsHost;
    private float _resetFeedbackUntil;
    private TextMeshProUGUI? _resetText;
    private TextMeshProUGUI? _optionsButtonText;
    private int _optionsPage;
    private readonly Button?[] _optionTabs = new Button?[4];
    private readonly TextMeshProUGUI?[] _optionTabLabels = new TextMeshProUGUI?[4];
    private readonly GameObject?[] _optionRows = new GameObject?[SpectatorOptionsController.RowCount];
    private readonly Button?[] _optionToggles = new Button?[SpectatorOptionsController.RowCount];
    private readonly TextMeshProUGUI?[] _optionLabels = new TextMeshProUGUI?[SpectatorOptionsController.RowCount];
    private readonly TextMeshProUGUI?[] _toggleLabels = new TextMeshProUGUI?[SpectatorOptionsController.RowCount];
    private readonly TextMeshProUGUI?[] _categoryLabels = new TextMeshProUGUI?[5];
    private readonly Button?[] _categoryButtons = new Button?[5];
    private static readonly string[] CategoryEnglish = { "All", "Monsters", "Scrap", "Items / tools", "Miniatures" };
    private static readonly string[] CategoryChinese = { "全部", "怪物", "废料", "工具与物品", "大型模型" };
    private static readonly string[] OptionTabsChinese = { "镜头", "模型", "界面与声音", "热键" };
    private static readonly string[] OptionTabsEnglish = { "Camera", "Models", "UI / Audio", "Hotkeys" };

    private void BuildOptions(RectTransform panel, Button? template, TextMeshProUGUI? textTemplate)
    {
        panel.sizeDelta = new Vector2(520f, 450f);
        _optionsRoot = CreateRectObject("Spectator Options", panel);
        Stretch((RectTransform)_optionsRoot.transform);
        _hostButton!.transform.SetParent(_optionsRoot.transform, false);
        _renderButton!.transform.SetParent(_optionsRoot.transform, false);
        _voiceMuteButton!.transform.SetParent(_optionsRoot.transform, false);
        SetTopLeft((RectTransform)_hostButton.transform, 16f, 94f, 239f, 32f);
        SetTopLeft((RectTransform)_renderButton.transform, 265f, 94f, 239f, 32f);
        SetTopLeft((RectTransform)_voiceMuteButton.transform, 16f, 94f, 488f, 32f);
        for (int index = 0; index < _optionTabs.Length; index++)
        {
            int tab = index;
            var button = CreateButton("Options Page " + index, _optionsRoot.transform, template, out var obj);
            SetTopLeft((RectTransform)obj.transform, 16f + index * 124f, 48f, 116f, 32f);
            _optionTabLabels[index] = CreateButtonText(obj.transform, textTemplate, string.Empty, 13f);
            _optionTabs[index] = button;
            button.onClick.AddListener(() => { CancelHotkeyCapture(); _optionsPage = tab; });
        }

        for (int index = 0; index < _categoryButtons.Length; index++)
        {
            int category = index;
            var button = CreateButton("Category " + index, panel, template, out var obj);
            SetTopLeft((RectTransform)obj.transform, 16f + index * 98f, 44f, 94f, 30f);
            _categoryLabels[index] = CreateButtonText(obj.transform, textTemplate, string.Empty, 11f);
            _categoryButtons[index] = button;
            button.onClick.AddListener(() => _callbacks?.ChangeCategory?.Invoke((FearModelCategory)category));
        }

        _catalogRoot = CreateRectObject("Catalog Content", panel);
        Stretch((RectTransform)_catalogRoot.transform);
        // Move only our own controls; never move or rebind vanilla quick-menu widgets.
        for (int index = panel.childCount - 1; index >= 0; index--)
        {
            Transform child = panel.GetChild(index);
            if (child == _optionsRoot.transform || child == _catalogRoot.transform || child.name == "Title" || child.name == "Close") continue;
            child.SetParent(_catalogRoot.transform, false);
        }
        SetTopLeft((RectTransform)_previousPageButton!.transform, 152f, 416f, 62f, 26f);
        SetTopLeft((RectTransform)_nextPageButton!.transform, 306f, 416f, 62f, 26f);
        SetTopLeft(_pageText!.rectTransform, 220f, 416f, 80f, 26f);

        int[] pageRows = { 0, 0, 0 };
        for (int index = 0; index < SpectatorOptionsController.RowCount; index++)
        {
            int row = index;
            int page = SpectatorOptionsController.PageForRow(row);
            float top = (page == 0 ? 94f : 140f) + pageRows[page]++ * (page == 0 ? 34f : 36f);
            var rowObject = CreateRectObject("Option Row " + row, _optionsRoot.transform);
            SetTopLeft((RectTransform)rowObject.transform, 16f, top, 488f, 32f);
            _optionRows[row] = rowObject;
            var background = rowObject.AddComponent<Image>();
            background.color = new Color(.19f, .12f, .07f, .4f); background.raycastTarget = false;
            var label = CreateText("Option " + row, rowObject.transform, textTemplate, 13f, TextAlignmentOptions.MidlineLeft);
            SetTopLeft(label.rectTransform, 8f, 0f, 365f, 32f);
            label.enableWordWrapping = false;
            _optionLabels[row] = label;
            if (SpectatorOptionsController.IsToggle(row))
            {
                var toggle = CreateButton("Toggle " + row, rowObject.transform, template, out var toggleObj);
                SetTopLeft((RectTransform)toggleObj.transform, 380f, 0f, 108f, 32f);
                _toggleLabels[row] = CreateButtonText(toggleObj.transform, textTemplate, string.Empty, 12f);
                _optionToggles[row] = toggle;
                toggle.onClick.AddListener(() => _options.Adjust(row, 1));
                continue;
            }
            var minus = CreateButton("Decrease " + row, rowObject.transform, template, out var minusObj);
            SetTopLeft((RectTransform)minusObj.transform, 392f, 0f, 42f, 32f);
            if (SpectatorOptionsController.IsSelector(row)) CreatePageGlyph(minusObj.transform, false);
            else CreateStepGlyph(minusObj.transform, positive: false);
            minus.onClick.AddListener(() => _options.Adjust(row, -1));
            var plus = CreateButton("Increase " + row, rowObject.transform, template, out var plusObj);
            SetTopLeft((RectTransform)plusObj.transform, 446f, 0f, 42f, 32f);
            if (SpectatorOptionsController.IsSelector(row)) CreatePageGlyph(plusObj.transform, true);
            else CreateStepGlyph(plusObj.transform, positive: true);
            plus.onClick.AddListener(() => _options.Adjust(row, 1));
        }
        BuildHotkeys(_optionsRoot.transform, template, textTemplate);
        var reset = CreateButton("Reset Defaults", _optionsRoot.transform, template, out var resetObj);
        SetTopLeft((RectTransform)resetObj.transform, 16f, 378f, 270f, 28f);
        _resetText = CreateButtonText(resetObj.transform, textTemplate, string.Empty, 13f);
        reset.onClick.AddListener(() =>
        {
            CancelHotkeyCapture();
            if (_optionsPage == 3) _options.Hotkeys.Reset();
            else _options.ResetDefaults(_optionsIsHost);
            _resetFeedbackUntil = Time.unscaledTime + 2f;
        });
        var optionsButton = CreateButton("Options Toggle", panel, template, out var optionsObj);
        SetTopLeft((RectTransform)optionsObj.transform, 390f, 416f, 114f, 26f);
        _optionsButtonText = CreateButtonText(optionsObj.transform, textTemplate, string.Empty, 13f);
        optionsButton.onClick.AddListener(() => { CancelHotkeyCapture(); _optionsOpen = !_optionsOpen; });
        _optionsRoot.SetActive(false);
    }

    private void RenderOptions(FearQuickMenuViewState state)
    {
        if (_optionsRoot == null || _catalogRoot == null) return;
        _optionsIsHost = state.IsHost;
        bool resetFeedback = Time.unscaledTime < _resetFeedbackUntil;
        SetText(_resetText, state.UseChineseText ? (resetFeedback ? "已恢复默认设置" : _optionsPage == 3 ? "恢复默认热键" : "恢复默认")
            : (resetFeedback ? "Defaults restored" : _optionsPage == 3 ? "Reset hotkeys" : "Reset to defaults"));
        _optionsRoot.SetActive(_optionsOpen);
        _catalogRoot.SetActive(!_optionsOpen);
        SetActive(_hostButton, _optionsPage == 1);
        SetActive(_renderButton, _optionsPage == 1);
        SetActive(_voiceMuteButton, _optionsPage == 2);
        RenderHotkeys(state.UseChineseText, _optionsOpen && _optionsPage == 3);
        string[] tabs = state.UseChineseText ? OptionTabsChinese : OptionTabsEnglish;
        for (int index = 0; index < _optionTabs.Length; index++)
        {
            SetText(_optionTabLabels[index], tabs[index]);
            SetButtonHighlighted(_optionTabs[index], index == _optionsPage);
        }
        SetText(_optionsButtonText, state.UseChineseText ? (_optionsOpen ? "返回模型" : "选项") : (_optionsOpen ? "Models" : "Options"));
        for (int index = 0; index < _categoryLabels.Length; index++)
        {
            SetText(_categoryLabels[index], state.UseChineseText ? CategoryChinese[index] : CategoryEnglish[index]);
            SetButtonHighlighted(_categoryButtons[index], (int)state.Category == index);
        }
        if (_optionsOpen)
            for (int index = 0; index < _optionLabels.Length; index++)
            {
                _optionRows[index]?.SetActive(SpectatorOptionsController.PageForRow(index) == _optionsPage);
                SetText(_optionLabels[index], _options.Describe(index, state.UseChineseText));
                if (_toggleLabels[index] != null) SetText(_toggleLabels[index], _options.ToggleActionLabel(index, state.UseChineseText));
                if (_optionToggles[index] != null) SetButtonHighlighted(_optionToggles[index], _options.ToggleValue(index));
            }
        if (_panelObject != null && _root != null)
        {
            var rootRect = (RectTransform)_root.transform;
            float scale = Mathf.Min(1f, Mathf.Min((rootRect.rect.width - 36f) / 520f, (rootRect.rect.height - 90f) / 450f));
            _panelObject.transform.localScale = Vector3.one * Mathf.Max(0.25f, scale);
        }
    }

    private static void CreatePageGlyph(Transform parent, bool next)
    {
        // Draw < and > as strokes so translated fallback fonts cannot lose the glyphs.
        for (int half = -1; half <= 1; half += 2)
        {
            var image = CreateImage("Arrow stroke", parent, null);
            image.color = new Color(1f, .48f, 0f, 1f); image.raycastTarget = false;
            var rect = (RectTransform)image.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0f, half * 3f);
            rect.sizeDelta = new Vector2(9f, 2f);
            rect.localRotation = Quaternion.Euler(0f, 0f, (next ? -1f : 1f) * half * 45f);
        }
    }

    private static void CreateStepGlyph(Transform parent, bool positive)
    {
        // Rect graphics avoid font substitution, missing glyphs and TMP line-height clipping entirely.
        var horizontal = CreateImage("Minus stroke", parent, null);
        horizontal.color = new Color(1f, 0.48f, 0f, 1f);
        horizontal.raycastTarget = false;
        var rect = (RectTransform)horizontal.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(12f, 2f);
        if (!positive) return;
        var vertical = CreateImage("Plus stroke", parent, null);
        vertical.color = horizontal.color; vertical.raycastTarget = false;
        rect = (RectTransform)vertical.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(2f, 12f);
    }
}
