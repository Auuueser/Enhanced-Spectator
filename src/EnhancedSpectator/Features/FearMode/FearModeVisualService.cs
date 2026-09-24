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
using UnityEngine.Rendering;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Renders validated player-selected models and maintains default-head suppression state.
/// </summary>
public sealed partial class FearModeVisualService : IDisposable
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
    private float _lastScale = float.NaN;
    private float _lastTargetHeight = float.NaN;
    private bool _lastOriginalScale;

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
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += SceneUnloaded;
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
        _config.Camera.FadeModelsNearby.SettingChanged += OnFadePreferenceChanged;
        _config.Camera.FadeModelsWhileSpectating.SettingChanged += OnFadePreferenceChanged;
    }

    /// <summary>Updates local and remote fear visuals after network/presence state.</summary>
    public void LateTick()
    {
        RestoreFades();
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

            if (_lastScale != _config.FearModelScaleMultiplier.Value
                || _lastTargetHeight != _config.FearModelTargetHeight.Value
                || _lastOriginalScale != _config.FearModelUseOriginalScale.Value)
            {
                ClearAll();
                _lastScale = _config.FearModelScaleMultiplier.Value;
                _lastTargetHeight = _config.FearModelTargetHeight.Value;
                _lastOriginalScale = _config.FearModelUseOriginalScale.Value;
            }
            _requestedBuildOwners.Clear();
            UpdateRemoteVisuals();
            UpdateLocalVisual();
            AdvancePendingVisuals();
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
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= SceneUnloaded;
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        _config.Camera.FadeModelsNearby.SettingChanged -= OnFadePreferenceChanged;
        _config.Camera.FadeModelsWhileSpectating.SettingChanged -= OnFadePreferenceChanged;
        _disposed = true;
    }

    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        RestoreFades();
        if (_disposed || !LethalCompanyFearViewCamera.IsActiveView(camera)) return;
        try
        {
            bool enabled = LethalCompanyFearViewCamera.ShouldFade(_config.Camera);
            foreach (var visual in _remoteVisuals.Values) visual.BeginCameraFade(enabled);
            _localVisual?.BeginCameraFade(enabled);
        }
        catch (Exception ex) { RestoreFades(); ModLog.Debug("Model camera fade skipped: " + ex.GetType().Name); }
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        foreach (var visual in _remoteVisuals.Values) visual.EndCameraFade(camera);
        _localVisual?.EndCameraFade(camera);
    }

    private void OnFadePreferenceChanged(object sender, EventArgs args)
    {
        if (LethalCompanyFearViewCamera.ShouldFade(_config.Camera)) return;
        foreach (var visual in _remoteVisuals.Values) visual.DisableCameraFade();
        _localVisual?.DisableCameraFade();
    }

    private void RestoreFades()
    {
        foreach (var visual in _remoteVisuals.Values) visual.RestoreCameraFade();
        _localVisual?.RestoreCameraFade();
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
                if (TryAcquireVisual(spectator.SpectatorClientId,
                    source!, selection!.ModelKey, visibleLayerMask, out RuntimeEnemyVisual? created, out string reason)
                    && created != null)
                {
                    float previousOpacity = visual?.FadeOpacity ?? 1f;
                    RemoveRemoteVisual(spectator.SpectatorClientId);
                    visual = created;
                    visual.SeedFadeOpacity(previousOpacity);
                    _remoteVisuals[spectator.SpectatorClientId] = visual;
                    ModLog.Info($"Fear visual created for client {spectator.SpectatorClientId}: model={visual.ModelKey}, {reason}.");
                }
                else if (visual == null) continue;
                // Keep the previous complete model while its replacement is prepared.
            }

            SpectatorPoseState pose = spectator.PoseState;
            if (_config.Camera.FadeModelsNearby.Value) visual.PrepareFadeMaterials();
            visual.SetWatchedTarget(pose.TargetClientId, pose.TargetPlayerSlotId);
            visual.ApplyOwnerScale(selection!.ModelScale);
            _posePresentationService.Resolve(
                pose,
                out Vector3 presentedPosition,
                out Quaternion presentedRotation,
                out bool motionReferenced,
                out SpectatorMotionReferencePose motionReference);
            visual.ApplyPose(
                presentedPosition,
                visual.ModelKey == "item:Shotgun"
                    ? LethalCompanyWatchedBody.AimShotgun(presentedPosition, pose.TargetClientId, pose.TargetPlayerSlotId, presentedRotation)
                    : FearModelPresentationRules.ResolveWorldRotation(presentedRotation, visual.ModelKey),
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
            if (TryAcquireVisual(clientId, source, selection.ModelKey,
                _gameAdapter.TryGetActiveCameraCullingMask(out int localCullingMask) ? localCullingMask : ~0,
                out var created, out string reason) && created != null)
            {
                float previousOpacity = _localVisual?.FadeOpacity ?? 1f;
                RemoveLocalVisual();
                _localVisual = created;
                _localVisual.SeedFadeOpacity(previousOpacity);
                _localVisualClientId = clientId;
                _hasLocalVisualClientId = true;
                ModLog.Info($"Local fear visual created: model={_localVisual.ModelKey}, {reason}.");
            }
            else if (_localVisual == null) return;
        }

        if (_config.Camera.FadeModelsNearby.Value) _localVisual.PrepareFadeMaterials();
        _localVisual.ApplyOwnerScale(selection.ModelScale);
        _localVisual.SetWatchedTarget(cameraState.TargetActualClientId, cameraState.TargetSlotId);
        _localVisual.ApplyPose(
            cameraState.WorldPosition,
            _localVisual.ModelKey == "item:Shotgun"
                ? LethalCompanyWatchedBody.AimShotgun(cameraState.WorldPosition, cameraState.TargetActualClientId, cameraState.TargetSlotId, cameraState.RepresentationRotation)
                : FearModelPresentationRules.ResolveWorldRotation(
                cameraState.RepresentationRotation,
                _localVisual.ModelKey));
        if (_localVisual.TryGetWorldBounds(out Bounds localBounds))
        {
            cameraState.LocalModelCenter = Quaternion.Inverse(cameraState.RepresentationRotation) * (localBounds.center - cameraState.WorldPosition);
            cameraState.LocalModelRadius = localBounds.extents.magnitude;
            cameraState.HasLocalModelBounds = true;
        }
        _overrides.SetActive(clientId, active: true);
    }

    private void RemoveRemoteVisual(ulong clientId)
    {
        if (_remoteVisuals.TryGetValue(clientId, out RuntimeEnemyVisual visual))
        {
            ParkVisual(visual);
            _remoteVisuals.Remove(clientId);
        }

        _overrides.SetActive(clientId, active: false);
        _posePresentationService.Remove(clientId);
    }

    private void RemoveLocalVisual()
    {
        _spectatorModule.CameraState.HasLocalModelBounds = false;
        if (_localVisual != null) ParkVisual(_localVisual);
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
        ClearPendingVisuals();
        foreach (RuntimeEnemyVisual visual in _remoteVisuals.Values)
        {
            visual.Dispose();
        }

        _remoteVisuals.Clear();
        _activeRemoteIds.Clear();
        _staleIds.Clear();
        RemoveLocalVisual();
        _idleVisuals.Dispose();
        _overrides.Clear();
    }
}
