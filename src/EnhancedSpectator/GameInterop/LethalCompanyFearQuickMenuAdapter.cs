using System;
using EnhancedSpectator.Features.FearMode;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Logging;
using GameNetcodeStuff;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Attaches a mod-owned retained view to the confirmed V81 quick-menu canvas.
/// Original player-list objects and listeners are never mutated.
/// </summary>
public sealed partial class LethalCompanyFearQuickMenuAdapter : IGameFearQuickMenuAdapter
{
    private const int CardCount = FearQuickMenuRules.PageSize;
    private static readonly Color NormalButtonColor = new Color(0.23f, 0.14f, 0.09f, 0.96f);
    private static readonly Color HighlightedButtonColor = new Color(0.78f, 0.32f, 0.08f, 1f);
    private readonly IFearModelThumbnailProvider _thumbnails;
    private readonly ModelCard[] _cards = new ModelCard[CardCount];
    private QuickMenuManager? _quickMenu;
    private FearQuickMenuCallbacks? _callbacks;
    private GameObject? _root;
    private GameObject? _entryObject;
    private GameObject? _panelObject;
    private Button? _hostButton;
    private Button? _renderButton;
    private Button? _soundButton;
    private Button? _nextSoundButton;
    private Button? _voiceMuteButton;
    private Button? _previousPageButton;
    private Button? _nextPageButton;
    private TextMeshProUGUI? _titleText;
    private TextMeshProUGUI? _hostText;
    private TextMeshProUGUI? _renderText;
    private TextMeshProUGUI? _soundText;
    private TextMeshProUGUI? _nextSoundText;
    private TextMeshProUGUI? _voiceMuteText;
    private TextMeshProUGUI? _selectedText;
    private TextMeshProUGUI? _pageText;

    /// <summary>Creates an adapter backed by renderer-only model thumbnails.</summary>
    public LethalCompanyFearQuickMenuAdapter(IFearModelThumbnailProvider thumbnails, SpectatorOptionsController options)
    {
        _thumbnails = thumbnails ?? throw new ArgumentNullException(nameof(thumbnails));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public bool IsQuickMenuOpen
    {
        get
        {
            QuickMenuManager? menu = ResolveQuickMenu();
            return menu != null && menu.isMenuOpen;
        }
    }

    /// <inheritdoc />
    public bool TryEnsureView(FearQuickMenuCallbacks callbacks, out string reason)
    {
        if (callbacks == null)
        {
            throw new ArgumentNullException(nameof(callbacks));
        }

        QuickMenuManager? menu = ResolveQuickMenu();
        if (menu == null || menu.menuContainer == null)
        {
            DisposeView();
            reason = "local QuickMenuManager/menuContainer unavailable";
            return false;
        }

        if (_quickMenu == menu && _root != null)
        {
            reason = string.Empty;
            return true;
        }

        DisposeView();
        try
        {
            _quickMenu = menu;
            _callbacks = callbacks;
            BuildView(menu);
            reason = string.Empty;
            ModLog.Debug("Fear quick-menu retained UI attached to the current vanilla menu.");
            return true;
        }
        catch (Exception ex)
        {
            DisposeView();
            reason = $"retained UI creation failed: {ex.GetType().Name}";
            ModLog.Error($"Fear quick-menu UI creation failed; keyboard shortcuts remain available: {ex}");
            return false;
        }
    }

    /// <inheritdoc />
    public void SetEntryVisible(bool visible)
    {
        if (_entryObject != null && _entryObject.activeSelf != visible)
        {
            _entryObject.SetActive(visible);
        }

        if (visible && _root != null)
        {
            _root.transform.SetAsLastSibling();
        }
    }

    /// <inheritdoc />
    public void SetPanelVisible(bool visible)
    {
        if (!visible) CancelHotkeyCapture();
        if (_panelObject != null && _panelObject.activeSelf != visible)
        {
            _panelObject.SetActive(visible);
        }

        if (visible && _root != null)
        {
            _root.transform.SetAsLastSibling();
        }

        if (_entryObject != null)
        {
            _entryObject.transform.SetAsLastSibling();
        }
    }

    /// <inheritdoc />
    public void Render(FearQuickMenuViewState state)
    {
        if (state == null || _panelObject == null)
        {
            return;
        }

        bool chinese = state.UseChineseText;
        SetText(_titleText, chinese ? (_optionsOpen ? "增强观战：选项" : "增强观战：恐惧模式") : (_optionsOpen ? "ENHANCED SPECTATOR: OPTIONS" : "ENHANCED SPECTATOR: FEAR MODE"));
        SetText(
            _hostText,
            chinese
                ? $"本局恐惧：{OnOff(state.SessionEnabled, true)}"
                : $"HOST SESSION: {OnOff(state.SessionEnabled, false)}");
        SetText(
            _renderText,
            chinese
                ? $"显示模型：{OnOff(state.LocalRenderingEnabled, true)}"
                : $"SHOW MODELS: {OnOff(state.LocalRenderingEnabled, false)}");
        SetText(
            _soundText,
            chinese
                ? (state.SoundPlaying ? "停止音效" : "播放音效")
                : (state.SoundPlaying ? "STOP SOUND" : "PLAY SOUND"));
        SetText(_nextSoundText, chinese ? "下一音效" : "NEXT SOUND");
        SetText(
            _voiceMuteText,
            chinese
                ? (state.GhostVoiceMuted ? "鬼魂语音：静音" : "鬼魂语音：开启")
                : (state.GhostVoiceMuted ? "GHOST VOICE: MUTED" : "GHOST VOICE: ON"));
        SetText(
            _selectedText,
            !string.IsNullOrEmpty(state.StatusText) ? state.StatusText : chinese
                ? $"当前模型：{state.SelectedDisplayName}"
                : $"SELECTED MODEL: {state.SelectedDisplayName}");
        SetText(_pageText, $"{state.PageIndex + 1} / {state.PageCount}");

        SetActive(_hostButton, state.IsHost);
        SetInteractable(_hostButton, state.IsHost);
        SetInteractable(_renderButton, state.SessionEnabled);
        SetInteractable(_soundButton, state.CanUseSound);
        SetInteractable(_nextSoundButton, state.CanUseSound);
        SetInteractable(_previousPageButton, state.PageIndex > 0);
        SetInteractable(_nextPageButton, state.PageIndex + 1 < state.PageCount);
        SetButtonHighlighted(_hostButton, state.SessionEnabled);
        SetButtonHighlighted(_renderButton, state.LocalRenderingEnabled);
        SetButtonHighlighted(_soundButton, state.SoundButtonHighlighted);
        SetButtonHighlighted(_nextSoundButton, state.NextSoundButtonHighlighted);
        SetButtonHighlighted(
            _voiceMuteButton,
            FearQuickMenuButtonRules.ShouldHighlightGhostVoiceEnabled(state.GhostVoiceMuted));

        // Page visibility is applied last so shared host controls cannot reappear over camera rows.
        RenderOptions(state);
        if (_optionsOpen) return;
        int firstIndex = state.PageIndex * FearQuickMenuRules.PageSize;
        for (int slotIndex = 0; slotIndex < _cards.Length; slotIndex++)
        {
            int entryIndex = firstIndex + slotIndex;
            ModelCard card = _cards[slotIndex];
            if (entryIndex < 0 || entryIndex >= state.Entries.Count)
            {
                card.Set(null, null, false, false, null);
                continue;
            }

            FearQuickMenuModelEntry entry = state.Entries[entryIndex];
            _thumbnails.Request(entry.ModelKey);
            _thumbnails.TryGet(entry.ModelKey, out Sprite? thumbnail);
            bool pendingDropship = entry.ModelKey == FearModelIdentityRules.Dropship && !state.DropshipAvailable;
            card.Set(
                entry.ModelKey,
                pendingDropship ? (state.UseChineseText ? "补给火箭（外观待加载）" : "Delivery rocket (visual pending)") : entry.DisplayName,
                entry.Selected,
                !pendingDropship && state.CanSelectModels && (state.SupportsExpandedModels || !FearModelIdentityRules.IsExpanded(entry.ModelKey)),
                thumbnail);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeView();
    }

    private void BuildView(QuickMenuManager menu)
    {
        TextMeshProUGUI? textTemplate = ResolveTextTemplate(menu);
        Button? buttonTemplate = ResolveButtonTemplate(menu);
        Canvas? menuCanvas = menu.menuContainer.GetComponentInParent<Canvas>();
        if (menuCanvas == null)
        {
            throw new InvalidOperationException("vanilla quick-menu canvas unavailable");
        }

        _root = CreateRectObject("Enhanced Spectator Fear UI", menuCanvas.transform);
        RectTransform rootRect = (RectTransform)_root.transform;
        Stretch(rootRect);
        _root.transform.SetAsLastSibling();

        Button entryButton = CreateButton("Fear Menu Pixel Icon", rootRect, buttonTemplate, out _entryObject);
        SetTopRight((RectTransform)_entryObject!.transform, 18f, 16f, 48f, 48f);
        _thumbnails.TryGetEntryIcon(out Sprite? entrySprite);
        Image entryIcon = CreateImage("Pixel Icon", _entryObject.transform, entrySprite);
        Stretch((RectTransform)entryIcon.transform, 8f);
        entryIcon.raycastTarget = false;
        entryButton.onClick.AddListener(() => _callbacks?.TogglePanel());

        _panelObject = CreateRectObject("Fear Catalog Panel", rootRect);
        RectTransform panelRect = (RectTransform)_panelObject.transform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(1f, 1f);
        panelRect.sizeDelta = new Vector2(520f, 410f);
        panelRect.anchoredPosition = new Vector2(-18f, -72f);
        Image panelImage = _panelObject.AddComponent<Image>();
        panelImage.sprite = null;
        panelImage.type = Image.Type.Simple;
        panelImage.color = new Color(0.055f, 0.032f, 0.022f, 0.985f);
        panelImage.raycastTarget = true;
        Outline panelOutline = _panelObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.70f, 0.28f, 0.06f, 0.95f);
        panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

        _titleText = CreateText("Title", panelRect, textTemplate, 18f, TextAlignmentOptions.Left);
        SetTopLeft((RectTransform)_titleText.transform, 16f, 10f, 430f, 30f);

        Button closeButton = CreateButton("Close", panelRect, buttonTemplate, out GameObject closeObject);
        SetTopRight((RectTransform)closeObject.transform, 10f, 8f, 32f, 28f);
        TextMeshProUGUI closeText = CreateButtonText(closeObject.transform, textTemplate, "X", 16f);
        closeButton.onClick.AddListener(() => _callbacks?.ClosePanel());

        _hostButton = CreateButton("Host Session", panelRect, buttonTemplate, out GameObject hostObject);
        SetTopLeft((RectTransform)hostObject.transform, 16f, 44f, 238f, 30f);
        _hostText = CreateButtonText(hostObject.transform, textTemplate, string.Empty, 13f);
        _hostButton.onClick.AddListener(() => _callbacks?.ToggleHostSession());

        _renderButton = CreateButton("Local Rendering", panelRect, buttonTemplate, out GameObject renderObject);
        SetTopLeft((RectTransform)renderObject.transform, 266f, 44f, 238f, 30f);
        _renderText = CreateButtonText(renderObject.transform, textTemplate, string.Empty, 13f);
        _renderButton.onClick.AddListener(() => _callbacks?.ToggleLocalRendering());

        _soundButton = CreateButton("Play Stop Sound", panelRect, buttonTemplate, out GameObject soundObject);
        SetTopLeft((RectTransform)soundObject.transform, 16f, 80f, 238f, 30f);
        _soundText = CreateButtonText(soundObject.transform, textTemplate, string.Empty, 13f);
        _soundButton.onClick.AddListener(() => _callbacks?.ToggleSound());

        _nextSoundButton = CreateButton("Next Sound", panelRect, buttonTemplate, out GameObject nextSoundObject);
        SetTopLeft((RectTransform)nextSoundObject.transform, 266f, 80f, 238f, 30f);
        _nextSoundText = CreateButtonText(nextSoundObject.transform, textTemplate, string.Empty, 13f);
        _nextSoundButton.onClick.AddListener(() => _callbacks?.NextSound());

        _selectedText = CreateText("Selected Model", panelRect, textTemplate, 13f, TextAlignmentOptions.Left);
        SetTopLeft((RectTransform)_selectedText.transform, 18f, 116f, 484f, 24f);

        for (int index = 0; index < _cards.Length; index++)
        {
            int column = index % 3;
            int row = index / 3;
            Button cardButton = CreateButton($"Model Card {index + 1}", panelRect, buttonTemplate, out GameObject cardObject);
            SetTopLeft(
                (RectTransform)cardObject.transform,
                16f + (column * 164f),
                146f + (row * 74f),
                156f,
                66f);
            Image icon = CreateImage("Model Icon", cardObject.transform, entrySprite);
            RectTransform iconRect = (RectTransform)icon.transform;
            SetTopLeft(iconRect, 6f, 7f, 52f, 52f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            TextMeshProUGUI label = CreateText(
                "Model Name",
                cardObject.transform,
                textTemplate,
                12f,
                TextAlignmentOptions.MidlineLeft);
            SetTopLeft((RectTransform)label.transform, 64f, 8f, 84f, 50f);
            ModelCard card = new ModelCard(cardObject, cardButton, icon, label);
            _cards[index] = card;
            cardButton.onClick.AddListener(() =>
            {
                if (!string.IsNullOrWhiteSpace(card.ModelKey))
                {
                    _callbacks?.SelectModel(card.ModelKey!);
                }
            });
        }

        _previousPageButton = CreateButton("Previous Page", panelRect, buttonTemplate, out GameObject previousObject);
        SetTopLeft((RectTransform)previousObject.transform, 152f, 374f, 62f, 26f);
        CreateButtonText(previousObject.transform, textTemplate, "<", 16f);
        _previousPageButton.onClick.AddListener(() => _callbacks?.ChangePage(-1));

        _pageText = CreateText("Page", panelRect, textTemplate, 13f, TextAlignmentOptions.Center);
        SetTopLeft((RectTransform)_pageText.transform, 220f, 374f, 80f, 26f);

        _nextPageButton = CreateButton("Next Page", panelRect, buttonTemplate, out GameObject nextObject);
        SetTopLeft((RectTransform)nextObject.transform, 306f, 374f, 62f, 26f);
        CreateButtonText(nextObject.transform, textTemplate, ">", 16f);
        _nextPageButton.onClick.AddListener(() => _callbacks?.ChangePage(1));

        _voiceMuteButton = CreateButton("Ghost Voice Mute", panelRect, buttonTemplate, out GameObject voiceMuteObject);
        _voiceMuteButton.transition = Selectable.Transition.None;
        SetTopLeft((RectTransform)voiceMuteObject.transform, 16f, 374f, 128f, 26f);
        _voiceMuteText = CreateButtonText(voiceMuteObject.transform, textTemplate, string.Empty, 10.5f);
        _voiceMuteButton.onClick.AddListener(() => _callbacks?.ToggleGhostVoiceMute());

        BuildOptions(panelRect, buttonTemplate, textTemplate);
        _entryObject.SetActive(false);
        _panelObject.SetActive(false);
        _entryObject.transform.SetAsLastSibling();
    }

    private void DisposeView()
    {
        CancelHotkeyCapture();
        if (_root != null)
        {
            _root.SetActive(false);
            UnityEngine.Object.Destroy(_root);
        }

        _optionsRoot = null;
        _catalogRoot = null;
        _optionsOpen = false;
        _quickMenu = null;
        _callbacks = null;
        _root = null;
        _entryObject = null;
        _panelObject = null;
        _hostButton = null;
        _renderButton = null;
        _soundButton = null;
        _nextSoundButton = null;
        _voiceMuteButton = null;
        _previousPageButton = null;
        _nextPageButton = null;
        _titleText = null;
        _hostText = null;
        _renderText = null;
        _soundText = null;
        _nextSoundText = null;
        _voiceMuteText = null;
        _selectedText = null;
        _pageText = null;
        Array.Clear(_cards, 0, _cards.Length);
    }

    private static QuickMenuManager? ResolveQuickMenu()
    {
        StartOfRound? round = StartOfRound.Instance;
        PlayerControllerB? localPlayer = round != null ? round.localPlayerController : null;
        return localPlayer != null ? localPlayer.quickMenuManager : null;
    }

    private static TextMeshProUGUI? ResolveTextTemplate(QuickMenuManager menu)
    {
        if (menu.settingsBackButton != null)
        {
            return menu.settingsBackButton;
        }

        if (menu.interactTipText != null)
        {
            return menu.interactTipText;
        }

        if (menu.playerListSlots != null)
        {
            for (int index = 0; index < menu.playerListSlots.Length; index++)
            {
                if (menu.playerListSlots[index]?.usernameHeader != null)
                {
                    return menu.playerListSlots[index].usernameHeader;
                }
            }
        }

        return null;
    }

    private static Button? ResolveButtonTemplate(QuickMenuManager menu)
    {
        if (menu.PleaseConfirmChangesSettingsPanelBackButton != null)
        {
            return menu.PleaseConfirmChangesSettingsPanelBackButton;
        }

        return menu.mainButtonsPanel != null
            ? menu.mainButtonsPanel.GetComponentInChildren<Button>(includeInactive: true)
            : null;
    }

    private static GameObject CreateRectObject(string name, Transform parent)
    {
        GameObject value = new GameObject(name, typeof(RectTransform));
        value.transform.SetParent(parent, worldPositionStays: false);
        return value;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        Button? template,
        out GameObject buttonObject)
    {
        buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, worldPositionStays: false);
        Image image = buttonObject.GetComponent<Image>();
        Button button = buttonObject.GetComponent<Button>();
        if (template != null)
        {
            button.transition = template.transition;
            button.colors = template.colors;
            button.spriteState = template.spriteState;
            button.animationTriggers = template.animationTriggers;
            Image? sourceImage = template.targetGraphic as Image;
            CopyImageStyle(sourceImage, image);
        }

        image.color = NormalButtonColor;

        button.navigation = Navigation.defaultNavigation;
        button.targetGraphic = image;
        return button;
    }

    private static Image CreateImage(string name, Transform parent, Sprite? sprite)
    {
        GameObject value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        value.transform.SetParent(parent, worldPositionStays: false);
        Image image = value.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        TextMeshProUGUI? template,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        value.transform.SetParent(parent, worldPositionStays: false);
        TextMeshProUGUI text = value.GetComponent<TextMeshProUGUI>();
        if (template != null)
        {
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
            text.color = template.color;
            text.fontStyle = template.fontStyle;
        }
        else
        {
            text.color = new Color(0.94f, 0.87f, 0.76f, 1f);
        }

        text.fontSize = fontSize;
        text.alignment = alignment;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static TextMeshProUGUI CreateButtonText(
        Transform parent,
        TextMeshProUGUI? template,
        string value,
        float fontSize)
    {
        TextMeshProUGUI text = CreateText("Label", parent, template, fontSize, TextAlignmentOptions.Center);
        Stretch((RectTransform)text.transform, 0f);
        text.margin = new Vector4(4f, 0f, 4f, 0f);
        text.alignment = TextAlignmentOptions.Midline;
        text.fontStyle = FontStyles.Normal;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.text = value;
        // Labels are populated later. Never choose clipping/padding based on their initial empty text.
        return text;
    }

    private static void CopyImageStyle(Image? source, Image destination)
    {
        if (source == null)
        {
            return;
        }

        destination.sprite = source.sprite;
        destination.material = source.material;
        destination.type = source.type;
        destination.preserveAspect = source.preserveAspect;
        destination.fillCenter = source.fillCenter;
        destination.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
    }

    private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetTopRight(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetText(TextMeshProUGUI? text, string value)
    {
        if (text != null && !string.Equals(text.text, value, StringComparison.Ordinal))
        {
            text.text = value;
        }
    }

    private static void SetActive(Button? button, bool active)
    {
        if (button != null && button.gameObject.activeSelf != active)
        {
            button.gameObject.SetActive(active);
        }
    }

    private static void SetInteractable(Button? button, bool interactable)
    {
        if (button != null && button.interactable != interactable)
        {
            button.interactable = interactable;
        }
    }

    private static void SetButtonHighlighted(Button? button, bool highlighted)
    {
        if (button?.targetGraphic != null)
        {
            button.targetGraphic.color = highlighted ? HighlightedButtonColor : NormalButtonColor;
        }
    }

    private static string OnOff(bool value, bool chinese)
    {
        return chinese ? (value ? "开启" : "关闭") : (value ? "ON" : "OFF");
    }

    private sealed class ModelCard
    {
        private readonly GameObject _root;
        private readonly Button _button;
        private readonly Image _background;
        private readonly Image _icon;
        private readonly TextMeshProUGUI _label;

        public ModelCard(GameObject root, Button button, Image icon, TextMeshProUGUI label)
        {
            _root = root;
            _button = button;
            _background = root.GetComponent<Image>();
            _icon = icon;
            _label = label;
        }

        public string? ModelKey { get; private set; }

        public void Set(
            string? modelKey,
            string? displayName,
            bool selected,
            bool interactable,
            Sprite? icon)
        {
            bool visible = !string.IsNullOrWhiteSpace(modelKey);
            if (_root.activeSelf != visible)
            {
                _root.SetActive(visible);
            }

            if (!visible)
            {
                ModelKey = null;
                return;
            }

            ModelKey = modelKey;
            _button.interactable = interactable;
            _background.color = selected
                ? new Color(0.78f, 0.32f, 0.08f, 1f)
                : new Color(0.20f, 0.12f, 0.08f, 0.96f);
            _icon.sprite = icon;
            SetText(_label, displayName ?? modelKey!);
        }
    }

}
