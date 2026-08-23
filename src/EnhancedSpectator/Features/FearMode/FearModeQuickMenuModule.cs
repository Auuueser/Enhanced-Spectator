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

        _thumbnailProvider.Tick();
        bool quickMenuOpen = _quickMenuAdapter.IsQuickMenuOpen;
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
                _service.RefreshCatalogNow();
            }

            RenderPanel();
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

        int direction = SpectatorInputService.IsKeyPressedThisFrame(_config.FearModelPreviousKey.Value)
            ? -1
            : SpectatorInputService.IsKeyPressedThisFrame(_config.FearModelNextKey.Value)
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

        _selectedIndex = FearModelCycleRules.ResolveNextIndex(currentIndex, keys.Count, direction);
        SubmitSelection(keys[_selectedIndex]);
    }

    private void RenderPanel()
    {
        IReadOnlyList<string> keys = _service.Catalog.ModelKeys;
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

        _pageIndex = FearQuickMenuRules.ClampPage(_pageIndex, _entries.Count);
        bool canSelect = FearQuickMenuRules.CanSelectModel(
            _service.IsSessionEnabled,
            localPlayerDead);
        bool canUseSound = FearQuickMenuRules.CanUseSound(
            _service.IsSessionEnabled,
            _service.IsLocalRenderingEnabled,
            localPlayerDead);
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
            selectedDisplayName));
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

        SubmitSelection(modelKey);
    }

    private void SubmitSelection(string modelKey)
    {
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
