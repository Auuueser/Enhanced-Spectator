using System;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.FearMode;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Features.SpectatorPresence;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using UnityEngine;

namespace EnhancedSpectator.Features.FloatingHead;

/// <summary>
/// Renders the local logical ghost/avatar only while self-ghost third-person view is active.
/// </summary>
public sealed class LocalSpectatorAvatarVisualService : IDisposable
{
    private readonly EnhancedSpectatorConfig _config;
    private readonly SpectatorModule _spectatorModule;
    private readonly IGameSpectatorAdapter _gameAdapter;
    private readonly PlaceholderHeadVisualFactory _visualFactory;
    private readonly IFearVisualOverrideState? _fearVisualOverrides;
    private readonly IGameDetachedHeadVisualSourceAdapter? _detachedHeadVisualSourceAdapter;
    private FloatingHeadVisual? _visual;
    private Transform? _detachedHeadTemplateSource;
    private bool _disposed;
    private bool _disabledDueToError;

    /// <summary>
    /// Creates the local self-avatar visual service.
    /// </summary>
    public LocalSpectatorAvatarVisualService(
        EnhancedSpectatorConfig config,
        SpectatorModule spectatorModule,
        IGameSpectatorAdapter gameAdapter,
        PlaceholderHeadVisualFactory visualFactory,
        IFearVisualOverrideState? fearVisualOverrides = null,
        IGameDetachedHeadVisualSourceAdapter? detachedHeadVisualSourceAdapter = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _spectatorModule = spectatorModule ?? throw new ArgumentNullException(nameof(spectatorModule));
        _gameAdapter = gameAdapter ?? throw new ArgumentNullException(nameof(gameAdapter));
        _visualFactory = visualFactory ?? throw new ArgumentNullException(nameof(visualFactory));
        _fearVisualOverrides = fearVisualOverrides;
        _detachedHeadVisualSourceAdapter = detachedHeadVisualSourceAdapter;
        _config.Camera.FadeModelsNearby.SettingChanged += OnFadePreferenceChanged;
        _config.Camera.FadeModelsWhileSpectating.SettingChanged += OnFadePreferenceChanged;
    }

    private void OnFadePreferenceChanged(object sender, EventArgs args)
    {
        if (!LethalCompanyFearViewCamera.ShouldFade(_config.Camera) && _visual != null) _visual.FadeNearby = false;
    }

    /// <summary>
    /// Updates the local self-avatar pose after spectator camera state has advanced.
    /// </summary>
    public void LateTick()
    {
        if (_disposed || _disabledDueToError)
        {
            return;
        }

        try
        {
            SpectatorCameraState state = _spectatorModule.CameraState;
            if (!state.IsThirdPerson || !state.HasWorldPose)
            {
                ClearVisual();
                return;
            }

            if (_gameAdapter.TryGetLocalPlayerIdentity(out ulong clientId, out _)
                && _fearVisualOverrides?.IsFearVisualActive(clientId) == true)
            {
                ClearVisual();
                return;
            }

            if (!TryResolveDesiredSource(out FloatingHeadVisualSourceKind desiredSourceKind, out Transform? detachedHeadSource))
            {
                ClearVisual();
                return;
            }

            if (_visual != null && _visual.SourceKind != desiredSourceKind)
            {
                ClearVisual();
            }

            if (_visual == null && !TryCreateVisual(desiredSourceKind, detachedHeadSource))
            {
                return;
            }

            float scale = _visual!.SourceKind == FloatingHeadVisualSourceKind.RuntimeDetachedHead
                ? _config.RuntimeDetachedHeadScale.Value
                : _config.VisualStyle.Value == FloatingHeadVisualStyle.Sphere
                    ? _config.PlaceholderScale.Value
                    : _config.BillboardSize.Value;
            Quaternion rotation = _visual.SourceKind == FloatingHeadVisualSourceKind.RuntimeDetachedHead
                ? FloatingHeadRotationRules.ApplyRuntimeDetachedHeadOffset(
                    state.RepresentationRotation,
                    _config.RuntimeDetachedHeadPitchOffset.Value,
                    _config.RuntimeDetachedHeadYawOffset.Value,
                    _config.RuntimeDetachedHeadRollOffset.Value)
                : state.RepresentationRotation;
            _visual!.ApplyPose(
                state.WorldPosition,
                rotation,
                Math.Max(0.01f, scale * (_fearVisualOverrides?.GetOwnerScale(clientId) ?? 1f)));
            _visual.FadeNearby = LethalCompanyFearViewCamera.ShouldFade(_config.Camera);
            _visual.PrepareCameraFade();
            _visual.SetWatchedTarget(state.TargetActualClientId, state.TargetSlotId);
        }
        catch (Exception ex)
        {
            ModLog.Error($"Local third-person ghost visual failed and was disabled: {ex}");
            ClearVisual();
            _disabledDueToError = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ClearVisual();
        _visualFactory.Dispose();
        _config.Camera.FadeModelsNearby.SettingChanged -= OnFadePreferenceChanged;
        _config.Camera.FadeModelsWhileSpectating.SettingChanged -= OnFadePreferenceChanged;
        _disposed = true;
    }

    private bool TryCreateVisual(
        FloatingHeadVisualSourceKind sourceKind,
        Transform? detachedHeadSource)
    {
        if (!_gameAdapter.TryGetLocalPlayerIdentity(out ulong clientId, out ulong slotId))
        {
            return false;
        }

        RemoteSpectatorInfo localInfo = new RemoteSpectatorInfo(
            clientId,
            slotId,
            isWatchingLocalPlayer: false,
            DateTime.UtcNow.Ticks,
            poseState: null);
        if (sourceKind == FloatingHeadVisualSourceKind.RuntimeDetachedHead && detachedHeadSource != null)
        {
            _visual = _visualFactory.CreateFromDetachedHead(
                localInfo,
                detachedHeadSource,
                _config.RuntimeDetachedHeadScale.Value,
                showNameTag: false,
                _config.NameTagScale.Value,
                _config.NameTagHeightOffset.Value,
                _config.NameTagMaxDistance.Value,
                string.Empty);
            ModLog.Info("Local third-person ghost visual created: source=RuntimeDetachedHead.");
            return true;
        }

        _visual = _visualFactory.Create(
            localInfo,
            _config.PlaceholderScale.Value,
            _config.VisualStyle.Value,
            _config.BillboardSize.Value,
            _config.BaseAlpha.Value,
            _config.UseUnlitMaterial.Value,
            _config.EnableDepthTest.Value,
            showNameTag: false,
            _config.NameTagScale.Value,
            _config.NameTagHeightOffset.Value,
            _config.NameTagMaxDistance.Value,
            string.Empty);
        ModLog.Info("Local third-person ghost visual created: source=Placeholder.");
        return true;
    }

    private bool TryResolveDesiredSource(
        out FloatingHeadVisualSourceKind sourceKind,
        out Transform? detachedHeadSource)
    {
        bool hasDetachedHeadTemplate = false;
        detachedHeadSource = _detachedHeadTemplateSource;
        if (_config.UseRuntimeDetachedHeadVisuals.Value && detachedHeadSource == null)
        {
            try
            {
                if (_detachedHeadVisualSourceAdapter?.TryGetDetachedHeadVisualTemplate(out detachedHeadSource) == true
                    && detachedHeadSource != null)
                {
                    _detachedHeadTemplateSource = detachedHeadSource;
                }
            }
            catch (Exception ex)
            {
                ModLog.Debug($"Local detached-head template lookup failed: {ex.GetType().Name}.");
                detachedHeadSource = null;
            }
        }

        hasDetachedHeadTemplate = detachedHeadSource != null;
        return DetachedHeadVisualSourceRules.TryResolveVisualSourceKind(
            _config.EnablePlaceholderVisuals.Value,
            _config.UseRuntimeDetachedHeadVisuals.Value,
            hasDetachedHeadTemplate,
            _config.FallbackToPlaceholderWhenDetachedHeadUnavailable.Value,
            out sourceKind);
    }

    private void ClearVisual()
    {
        _visual?.Dispose();
        _visual = null;
    }
}
