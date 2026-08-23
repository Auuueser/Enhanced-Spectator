using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Features.SpectatorPresence;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Networking;
using EnhancedSpectator.Runtime;
using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Renders validated player-selected models and maintains default-head suppression state.
/// </summary>
public sealed class FearModeVisualService : IDisposable
{
    private readonly EnhancedSpectatorConfig _config;
    private readonly FearModeNetworkService _fearState;
    private readonly FearModelCatalog _catalog;
    private readonly ISpectatorPresenceProvider _presenceProvider;
    private readonly SpectatorModule _spectatorModule;
    private readonly IGameFearModeAdapter _gameAdapter;
    private readonly FearVisualOverrideRegistry _overrides;
    private readonly RuntimeEnemyVisualFactory _visualFactory;
    private readonly RemoteSpectatorPosePresentationService _posePresentationService;
    private readonly Dictionary<ulong, RuntimeEnemyVisual> _remoteVisuals =
        new Dictionary<ulong, RuntimeEnemyVisual>();
    private readonly HashSet<ulong> _activeRemoteIds = new HashSet<ulong>();
    private readonly List<ulong> _staleIds = new List<ulong>();
    private RuntimeEnemyVisual? _localVisual;
    private ulong _localVisualClientId;
    private bool _hasLocalVisualClientId;
    private bool _disposed;
    private bool _disabledDueToError;

    /// <summary>Creates the fear visual service.</summary>
    public FearModeVisualService(
        EnhancedSpectatorConfig config,
        FearModeNetworkService fearState,
        FearModelCatalog catalog,
        ISpectatorPresenceProvider presenceProvider,
        SpectatorModule spectatorModule,
        IGameFearModeAdapter gameAdapter,
        FearVisualOverrideRegistry overrides,
        RuntimeEnemyVisualFactory visualFactory,
        RemoteSpectatorPosePresentationService posePresentationService)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _fearState = fearState ?? throw new ArgumentNullException(nameof(fearState));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _presenceProvider = presenceProvider ?? throw new ArgumentNullException(nameof(presenceProvider));
        _spectatorModule = spectatorModule ?? throw new ArgumentNullException(nameof(spectatorModule));
        _gameAdapter = gameAdapter ?? throw new ArgumentNullException(nameof(gameAdapter));
        _overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
        _visualFactory = visualFactory ?? throw new ArgumentNullException(nameof(visualFactory));
        _posePresentationService = posePresentationService ?? throw new ArgumentNullException(nameof(posePresentationService));
    }

    /// <summary>Updates local and remote fear visuals after network/presence state.</summary>
    public void LateTick()
    {
        if (_disposed || _disabledDueToError)
        {
            return;
        }

        try
        {
            if (!RuntimeConnectionState.CanUseModNetworking(out _)
                || !_fearState.IsSessionEnabled
                || !_fearState.IsLocalRenderingEnabled)
            {
                ClearAll();
                return;
            }

            UpdateRemoteVisuals();
            UpdateLocalVisual();
        }
        catch (Exception ex)
        {
            ModLog.Error($"Fear visuals failed and were disabled for this session: {ex}");
            ClearAll();
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

        ClearAll();
        _disposed = true;
    }

    private void UpdateRemoteVisuals()
    {
        _activeRemoteIds.Clear();
        int visibleLayerMask = _gameAdapter.TryGetActiveCameraCullingMask(out int remoteCullingMask)
            ? remoteCullingMask
            : ~0;
        IReadOnlyList<RemoteSpectatorInfo> spectators = _presenceProvider.Current.RemoteSpectators;
        for (int index = 0; index < spectators.Count; index++)
        {
            RemoteSpectatorInfo spectator = spectators[index];
            FearModeSelectionState? selection = _fearState.TryGetSelection(
                spectator.SpectatorClientId,
                out FearModeSelectionState selected)
                ? selected
                : null;
            FearVisualSource? source = null;
            bool sourceAvailable = selection != null
                && _catalog.TryGetVisualSource(selection.ModelKey, out source)
                && source != null;
            bool shouldRender = FearModeRules.ShouldRenderFearVisual(
                _fearState.IsSessionEnabled,
                _fearState.IsLocalRenderingEnabled,
                selection,
                sourceAvailable);
            if (!shouldRender || spectator.PoseState == null || !spectator.PoseState.IsSpectating)
            {
                RemoveRemoteVisual(spectator.SpectatorClientId);
                continue;
            }

            if (!_remoteVisuals.TryGetValue(spectator.SpectatorClientId, out RuntimeEnemyVisual visual)
                || !string.Equals(visual.ModelKey, selection!.ModelKey, StringComparison.Ordinal))
            {
                RemoveRemoteVisual(spectator.SpectatorClientId);
                if (!_visualFactory.TryCreate(
                    source,
                    selection!.ModelKey,
                    _config.FearModelTargetHeight.Value,
                    _config.FearModelUseOriginalScale.Value,
                    _config.FearModelScaleMultiplier.Value,
                    visibleLayerMask,
                    out RuntimeEnemyVisual? created,
                    out string reason)
                    || created == null)
                {
                    ModLog.Debug($"Fear visual skipped for {spectator.SpectatorClientId}: {reason}.");
                    continue;
                }

                visual = created;
                _remoteVisuals[spectator.SpectatorClientId] = visual;
                Vector3 correction = FearModelPresentationRules.ResolveCorrectionEuler(selection.ModelKey);
                ModLog.Info(
                    $"Fear visual created for client {spectator.SpectatorClientId}: model={selection.ModelKey}, correction={correction}, {reason}.");
            }

            SpectatorPoseState pose = spectator.PoseState;
            _posePresentationService.Resolve(
                pose,
                out Vector3 presentedPosition,
                out Quaternion presentedRotation,
                out bool motionReferenced,
                out SpectatorMotionReferencePose motionReference);
            visual.ApplyPose(
                presentedPosition,
                FearModelPresentationRules.ResolveWorldRotation(presentedRotation, selection.ModelKey),
                Mathf.Max(0f, _config.RemotePoseSmoothTime.Value),
                pose.TimestampTicks,
                motionReferenced,
                motionReference);
            _activeRemoteIds.Add(spectator.SpectatorClientId);
            _overrides.SetActive(
                spectator.SpectatorClientId,
                active: true,
                visual.TryGetWorldTopOffset(out float topOffset) ? topOffset : 0f);
        }

        _staleIds.Clear();
        foreach (ulong clientId in _remoteVisuals.Keys)
        {
            if (!_activeRemoteIds.Contains(clientId))
            {
                _staleIds.Add(clientId);
            }
        }

        for (int index = 0; index < _staleIds.Count; index++)
        {
            RemoveRemoteVisual(_staleIds[index]);
        }
    }

    private void UpdateLocalVisual()
    {
        SpectatorCameraState cameraState = _spectatorModule.CameraState;
        if (!cameraState.IsThirdPerson
            || !cameraState.HasWorldPose
            || !_gameAdapter.TryGetLocalDeadPlayerIdentity(out ulong clientId, out _)
            || !_fearState.TryGetSelection(clientId, out FearModeSelectionState selection)
            || !_catalog.TryGetVisualSource(selection.ModelKey, out FearVisualSource? source)
            || source == null
            || !FearModeRules.ShouldRenderFearVisual(
                _fearState.IsSessionEnabled,
                _fearState.IsLocalRenderingEnabled,
                selection,
                sourceAvailable: true))
        {
            RemoveLocalVisual();
            return;
        }

        if (_localVisual == null
            || _localVisualClientId != clientId
            || !string.Equals(_localVisual.ModelKey, selection.ModelKey, StringComparison.Ordinal))
        {
            RemoveLocalVisual();
            if (!_visualFactory.TryCreate(
                source,
                selection.ModelKey,
                _config.FearModelTargetHeight.Value,
                _config.FearModelUseOriginalScale.Value,
                _config.FearModelScaleMultiplier.Value,
                _gameAdapter.TryGetActiveCameraCullingMask(out int localCullingMask) ? localCullingMask : ~0,
                out _localVisual,
                out string reason)
                || _localVisual == null)
            {
                ModLog.Debug($"Local fear visual skipped: {reason}.");
                return;
            }

            _localVisualClientId = clientId;
            _hasLocalVisualClientId = true;
            Vector3 correction = FearModelPresentationRules.ResolveCorrectionEuler(selection.ModelKey);
            ModLog.Info($"Local fear visual created: model={selection.ModelKey}, correction={correction}, {reason}.");
        }

        _localVisual.ApplyPose(
            cameraState.WorldPosition,
            FearModelPresentationRules.ResolveWorldRotation(
                cameraState.RepresentationRotation,
                selection.ModelKey));
        _overrides.SetActive(clientId, active: true);
    }

    private void RemoveRemoteVisual(ulong clientId)
    {
        if (_remoteVisuals.TryGetValue(clientId, out RuntimeEnemyVisual visual))
        {
            visual.Dispose();
            _remoteVisuals.Remove(clientId);
        }

        _overrides.SetActive(clientId, active: false);
        _posePresentationService.Remove(clientId);
    }

    private void RemoveLocalVisual()
    {
        _localVisual?.Dispose();
        _localVisual = null;
        if (_hasLocalVisualClientId)
        {
            _overrides.SetActive(_localVisualClientId, active: false);
        }

        _localVisualClientId = 0;
        _hasLocalVisualClientId = false;
    }

    private void ClearAll()
    {
        foreach (RuntimeEnemyVisual visual in _remoteVisuals.Values)
        {
            visual.Dispose();
        }

        _remoteVisuals.Clear();
        _activeRemoteIds.Clear();
        _staleIds.Clear();
        RemoveLocalVisual();
        _overrides.Clear();
    }
}
