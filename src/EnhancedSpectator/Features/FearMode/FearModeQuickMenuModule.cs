using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Features.VoiceRouting;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Runtime;
using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>Coordinates the cursor-operated retained fear catalog inside the vanilla ESC menu.</summary>
public sealed class FearModeQuickMenuModule : IFeatureModule, IRuntimeTickable
{
    private const float ButtonFeedbackDurationSeconds = 0.24f;
    private readonly EnhancedSpectatorConfig _config;
    private readonly FearModeNetworkService _service;
    private readonly IGameFearModeAdapter _gameAdapter;
    private readonly IGameFearQuickMenuAdapter _quickMenuAdapter;
    private readonly IFearSoundActions _soundActions;
    private readonly IFearModelThumbnailProvider _thumbnailProvider;
    private readonly SpectatorVoiceMuteState _voiceMuteState;
    private readonly FearQuickMenuCallbacks _callbacks;
    private readonly List<FearQuickMenuModelEntry> _entries = new List<FearQuickMenuModelEntry>();
    private FearModelCategory _category;
    private readonly List<string> _filteredKeys = new List<string>();
    private readonly List<AudioClip> _availableSoundClips = new List<AudioClip>();
    private string? _soundAvailabilityKey;
    private int _nextSoundAvailabilityFrame;
    private int _selectedIndex;
    private int _pageIndex;
    private int _catalogFingerprint;
    private string _selectedModelKey = string.Empty;
    private string _lastUiFailureReason = string.Empty;
    private bool _presentationChinese;
    private bool _panelOpen;
    private bool _refreshCatalogOnOpen;
    private bool _initialized;
    private float _soundButtonHighlightUntil;
    private float _nextSoundButtonHighlightUntil;

    /// <summary>Creates the retained quick-menu controller.</summary>
    public FearModeQuickMenuModule(
        EnhancedSpectatorConfig config,
        FearModeNetworkService service,
        IGameFearModeAdapter gameAdapter,
        IGameFearQuickMenuAdapter quickMenuAdapter,
        IFearSoundActions soundActions,
        IFearModelThumbnailProvider thumbnailProvider,
        SpectatorVoiceMuteState voiceMuteState)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _gameAdapter = gameAdapter ?? throw new ArgumentNullException(nameof(gameAdapter));
        _quickMenuAdapter = quickMenuAdapter ?? throw new ArgumentNullException(nameof(quickMenuAdapter));
        _soundActions = soundActions ?? throw new ArgumentNullException(nameof(soundActions));
        _thumbnailProvider = thumbnailProvider ?? throw new ArgumentNullException(nameof(thumbnailProvider));
        _voiceMuteState = voiceMuteState ?? throw new ArgumentNullException(nameof(voiceMuteState));
        _callbacks = new FearQuickMenuCallbacks(
            TogglePanel,
            ClosePanel,
            ToggleHostSession,
            ToggleLocalRendering,
            ToggleSound,
            NextSound,
            ToggleGhostVoiceMute,
            ChangePage,
            SelectModel);
        _callbacks.ChangeCategory = category =>
        {
            _category = category;
            _pageIndex = 0;
            _catalogFingerprint = int.MinValue;
        };
    }

    /// <inheritdoc />
    public void Initialize()
    {
        _initialized = true;
    }

    /// <inheritdoc />
    public void Tick()
    {
        if (!_initialized)
        {
            return;
        }

        bool quickMenuOpen = _quickMenuAdapter.IsQuickMenuOpen;
        var audit = _thumbnailProvider as FearModelThumbnailService;
        bool renderedAudit = audit?.TickDeveloperAudit() == true;
        if (quickMenuOpen && audit != null && audit.TryGetAuditPage(out int auditPage))
        {
            _panelOpen = true;
            _category = FearModelCategory.All;
            _pageIndex = auditPage;
        }
        if (!renderedAudit) _thumbnailProvider.Tick();
        if (!quickMenuOpen)
        {
            HandleModelCycleHotkeys();
        }

        bool showEntry = FearQuickMenuRules.ShouldShowEntry(
            _config.ShowFearModeQuickMenu.Value,
            quickMenuOpen,
            _service.IsHost,
            _service.IsSessionEnabled);
        if (!showEntry)
        {
            _panelOpen = false;
            _quickMenuAdapter.SetPanelVisible(false);
            _quickMenuAdapter.SetEntryVisible(false);
            return;
        }

        if (!_quickMenuAdapter.TryEnsureView(_callbacks, out string reason))
        {
            if (!string.Equals(reason, _lastUiFailureReason, StringComparison.Ordinal))
            {
                _lastUiFailureReason = reason;
                ModLog.Warning($"Fear quick-menu UI unavailable: {reason}; keyboard shortcuts remain enabled.");
            }

            return;
        }

        _lastUiFailureReason = string.Empty;
        _quickMenuAdapter.SetEntryVisible(true);
        _quickMenuAdapter.SetPanelVisible(_panelOpen);
        if (_panelOpen)
        {
            if (_refreshCatalogOnOpen)
            {
                _refreshCatalogOnOpen = false;
                // The network service already refreshes sources at a bounded cadence.
                // Opening a retained view must not repeat a whole-scene source scan.
                if (_service.Catalog.ModelKeys.Count <= 1) _service.RefreshCatalogNow();
            }

            RenderPanel();
            audit?.CaptureAuditPage(FearQuickMenuRules.ResolvePageCount(_entries.Count));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _panelOpen = false;
        _entries.Clear();
        _quickMenuAdapter.Dispose();
        _thumbnailProvider.Dispose();
        _initialized = false;
    }

    private void HandleModelCycleHotkeys()
    {
        if (!_service.IsSessionEnabled)
        {
            return;
        }

        bool Pressed(KeyCode key) => (SpectatorFreecamController.Current?.AllowsModelShortcut(key) ?? true)
            && SpectatorInputService.IsKeyPressedThisFrame(key);
        int direction = Pressed(_config.FearModelPreviousKey.Value)
            ? -1
            : Pressed(_config.FearModelNextKey.Value)
                ? 1
                : 0;
        if (direction == 0
            || !_gameAdapter.TryGetLocalDeadPlayerIdentity(out ulong clientId, out _))
        {
            return;
        }

        IReadOnlyList<string> keys = _service.Catalog.ModelKeys;
        if (keys.Count == 0)
        {
            return;
        }

        int currentIndex = 0;
        if (_service.TryGetSelection(clientId, out FearModeSelectionState currentSelection))
        {
            int selectedIndex = IndexOf(keys, currentSelection.ModelKey);
            if (selectedIndex >= 0)
            {
                currentIndex = selectedIndex;
            }
        }

        for (int attempt = 0; attempt < keys.Count; attempt++)
        {
            currentIndex = FearModelCycleRules.ResolveNextIndex(currentIndex, keys.Count, direction);
            if (!_service.CanSelectCatalogKey(keys[currentIndex])) continue;
            _selectedIndex = currentIndex;
            SubmitSelection(keys[currentIndex]);
            break;
        }
    }

    private void RenderPanel()
    {
        _filteredKeys.Clear();
        foreach (string key in _service.Catalog.ModelKeys)
            if (_category == FearModelCategory.All || FearModelIdentityRules.Category(key) == _category) _filteredKeys.Add(key);
        IReadOnlyList<string> keys = _filteredKeys;
        bool localPlayerDead = _gameAdapter.TryGetLocalDeadPlayerIdentity(out ulong clientId, out _);
        string selectedKey = FearModeRules.DefaultModelKey;
        if (localPlayerDead
            && _service.TryGetSelection(clientId, out FearModeSelectionState selection))
        {
            selectedKey = selection.ModelKey;
        }

        int fingerprint = ComputeCatalogFingerprint(keys);
        bool presentationChanged = fingerprint != _catalogFingerprint
            || !string.Equals(selectedKey, _selectedModelKey, StringComparison.Ordinal)
            || _presentationChinese != _config.UseChineseText;
        if (presentationChanged)
        {
            _catalogFingerprint = fingerprint;
            _presentationChinese = _config.UseChineseText;
            bool selectionChanged = !string.Equals(selectedKey, _selectedModelKey, StringComparison.Ordinal);
            _selectedModelKey = selectedKey;
            FearModelUiPresentationRules.CopyEntriesTo(
                keys,
                selectedKey,
                _presentationChinese,
                _entries);
            int selectedIndex = IndexOf(keys, selectedKey);
            if (selectedIndex >= 0)
            {
                _selectedIndex = selectedIndex;
                if (selectionChanged)
                {
                    _pageIndex = FearQuickMenuRules.ResolvePageForItem(
                        selectedIndex,
                        _entries.Count);
                }
            }
        }

        if (_thumbnailProvider is FearModelThumbnailService capture && capture.TryGetAuditPage(out int requestedPage)) _pageIndex = requestedPage;
        _pageIndex = FearQuickMenuRules.ClampPage(_pageIndex, _entries.Count);
        bool canSelect = FearQuickMenuRules.CanSelectModel(
            _service.IsSessionEnabled,
            localPlayerDead);
        bool canUseSound = FearQuickMenuRules.CanUseSound(
            _service.IsSessionEnabled,
            _service.IsLocalRenderingEnabled,
            localPlayerDead);
        if (_soundAvailabilityKey != selectedKey || Time.frameCount >= _nextSoundAvailabilityFrame)
        {
            _gameAdapter.CopyFearSoundClipsTo(selectedKey, _availableSoundClips);
            _soundAvailabilityKey = selectedKey;
            _nextSoundAvailabilityFrame = Time.frameCount + 120;
        }
        canUseSound = canUseSound && _availableSoundClips.Count > 0 && _service.CanUseModelSound(selectedKey);
        string selectedDisplayName = FearModelUiPresentationRules.ResolveDisplayName(
            selectedKey,
            _config.UseChineseText);
        _quickMenuAdapter.Render(new FearQuickMenuViewState(
            _entries,
            _pageIndex,
            _config.UseChineseText,
            _service.IsHost,
            _service.IsSessionEnabled,
            _service.IsLocalRenderingEnabled,
            canSelect,
            canUseSound,
            _soundActions.IsLocalSoundPlaying,
            _soundActions.IsLocalSoundPlaying || Time.unscaledTime < _soundButtonHighlightUntil,
            Time.unscaledTime < _nextSoundButtonHighlightUntil,
            _voiceMuteState.IsMuted,
            selectedDisplayName)
        {
            SupportsExpandedModels = _service.SupportsExpandedCatalog,
            DropshipAvailable = _service.Catalog.TryGetVisualSource(FearModelIdentityRules.Dropship, out _),
            Category = _category,
            StatusText = ResolveStatus(localPlayerDead)
        });
    }

    private string ResolveStatus(bool localDead)
    {
        bool cn = _config.UseChineseText;
        if (!_config.EnableNetworking.Value) return cn ? "网络功能已关闭；本地观战选项仍可使用" : "Networking disabled; local view options remain available";
        if (!_service.HasCompatibleHost) return cn ? "房主未提供兼容恐惧模式；本地选项可用" : "No compatible fear host; local options available";
        if (!_service.IsSessionEnabled) return cn ? "房主尚未开启恐惧模式" : "Fear mode is disabled by the host";
        if (!localDead) return cn ? "死亡后可选择恐惧模型和播放音效" : "Model selection and sounds are available after death";
        if (!_service.SupportsExpandedCatalog) return cn ? "房主版本较旧；新增模型不可选择" : "Older host: expanded models are unavailable";
        return string.Empty;
    }

    private void TogglePanel()
    {
        _panelOpen = !_panelOpen;
        _refreshCatalogOnOpen = _panelOpen;
        _quickMenuAdapter.SetPanelVisible(_panelOpen);
        ModLog.Info(_panelOpen
            ? "Fear quick-menu panel opened."
            : "Fear quick-menu panel closed.");
    }

    private void ClosePanel()
    {
        _panelOpen = false;
        _quickMenuAdapter.SetPanelVisible(false);
    }

    private void ToggleHostSession()
    {
        if (_service.IsHost)
        {
            _config.EnableFearModeAsHost.Value = !_config.EnableFearModeAsHost.Value;
        }
    }

    private void ToggleLocalRendering()
    {
        if (_service.IsSessionEnabled)
        {
            _config.RenderFearModelsLocally.Value = !_config.RenderFearModelsLocally.Value;
        }
    }

    private void ToggleSound()
    {
        if (_soundActions.TryToggleLocalSound())
        {
            _soundButtonHighlightUntil = Time.unscaledTime + ButtonFeedbackDurationSeconds;
        }
    }

    private void NextSound()
    {
        if (_soundActions.TryPlayNextLocalSound())
        {
            _nextSoundButtonHighlightUntil = Time.unscaledTime + ButtonFeedbackDurationSeconds;
        }
    }

    private void ToggleGhostVoiceMute()
    {
        bool muted = _voiceMuteState.Toggle();
        ModLog.Info(muted
            ? "Routed ghost voice muted from fear quick menu."
            : "Routed ghost voice unmuted from fear quick menu.");
    }

    private void ChangePage(int direction)
    {
        _pageIndex = FearQuickMenuRules.ResolveAdjacentPage(
            _pageIndex,
            direction,
            _entries.Count);
    }

    private void SelectModel(string modelKey)
    {
        if (!FearQuickMenuRules.CanSelectModel(
                _service.IsSessionEnabled,
                _gameAdapter.TryGetLocalDeadPlayerIdentity(out _, out _)))
        {
            return;
        }

        if (_service.CanSelectCatalogKey(modelKey)) SubmitSelection(modelKey);
    }

    private void SubmitSelection(string modelKey)
    {
        FearVisualWorkSchedule.Shared.ModelChanged(UnityEngine.Time.frameCount);
        if (!_service.TrySelectLocalModel(modelKey, out string reason))
        {
            ModLog.Warning($"Fear model selection rejected: {reason}.");
        }
    }

    private static int ComputeCatalogFingerprint(IReadOnlyList<string> values)
    {
        unchecked
        {
            int hash = 17;
            for (int index = 0; index < values.Count; index++)
            {
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(values[index]);
            }

            return hash;
        }
    }

    private static int IndexOf(IReadOnlyList<string> values, string value)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
