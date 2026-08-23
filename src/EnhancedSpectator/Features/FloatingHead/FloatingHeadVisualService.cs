using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.FearMode;
using EnhancedSpectator.Features.SpectatorPresence;
using EnhancedSpectator.Features.VoiceActivity;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Networking;
using EnhancedSpectator.Runtime;
using UnityEngine;

namespace EnhancedSpectator.Features.FloatingHead;

/// <summary>
/// Owns local placeholder visuals driven by inferred remote spectator presence.
/// </summary>
public sealed class FloatingHeadVisualService : IDisposable
{
    private readonly EnhancedSpectatorConfig _config;
    private readonly ISpectatorPresenceProvider _presenceProvider;
    private readonly IVoiceActivityProvider _voiceActivityProvider;
    private readonly IEnhancedSpectatorNetworkService? _networkService;
    private readonly IGameDetachedHeadVisualSourceAdapter _detachedHeadVisualSourceAdapter;
    private readonly FloatingHeadPlacementService _placementService;
    private readonly PlaceholderHeadVisualFactory _visualFactory;
    private readonly IFearVisualOverrideState? _fearVisualOverrides;
    private readonly RemoteSpectatorPosePresentationService? _posePresentationService;
    private readonly Dictionary<ulong, FloatingHeadVisual> _visuals =
        new Dictionary<ulong, FloatingHeadVisual>();
    private readonly HashSet<ulong> _activeSpectatorIds = new HashSet<ulong>();
    private readonly List<ulong> _staleVisualIds = new List<ulong>();
    private readonly List<RemoteSpectatorInfo> _sortedSpectators = new List<RemoteSpectatorInfo>();
    private readonly HashSet<ulong> _poseLoggedSpectators = new HashSet<ulong>();
    private readonly HashSet<string> _remotePoseLoggedSpectators = new HashSet<string>();
    private readonly HashSet<ulong> _screenFallbackLoggedSpectators = new HashSet<ulong>();
    private readonly HashSet<ulong> _voiceDataLoggedSpectators = new HashSet<ulong>();
    private readonly HashSet<ulong> _voiceDataActiveSpectators = new HashSet<ulong>();
    private readonly HashSet<ulong> _visualSkipLoggedSpectators = new HashSet<ulong>();
    private readonly Dictionary<ulong, NameTagTextCacheEntry> _nameTagTextCache =
        new Dictionary<ulong, NameTagTextCacheEntry>();

    private Texture2D? _screenMarkerTexture;
    private Transform? _detachedHeadTemplateSource;
    private Vector3 _lastAnchorPosition;
    private float _lastPresenceSeenUnscaledTime = float.NegativeInfinity;
    private int _lastFullUpdateFrame = -1;
    private bool _anchorLostLogged;
    private bool _disposed;
    private bool _disabledDueToError;
    private bool _voiceProviderDisabled;
    private bool _presenceGraceLogged;
    private bool _layerWarningLogged;
    private bool _hasCachedVisualState;
    private int _cachedNameTagIdentityRevision = int.MinValue;
    private bool _cachedNameTagUseGamePlayerNames;
    private bool _cachedNameTagUseFallbackIds;

    /// <summary>
    /// Creates a floating-head visual service.
    /// </summary>
    public FloatingHeadVisualService(
        EnhancedSpectatorConfig config,
        ISpectatorPresenceProvider presenceProvider,
        IVoiceActivityProvider voiceActivityProvider,
        IEnhancedSpectatorNetworkService? networkService,
        IGameDetachedHeadVisualSourceAdapter detachedHeadVisualSourceAdapter,
        FloatingHeadPlacementService placementService,
        PlaceholderHeadVisualFactory visualFactory,
        IFearVisualOverrideState? fearVisualOverrides = null,
        RemoteSpectatorPosePresentationService? posePresentationService = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _presenceProvider = presenceProvider ?? throw new ArgumentNullException(nameof(presenceProvider));
        _voiceActivityProvider = voiceActivityProvider ?? throw new ArgumentNullException(nameof(voiceActivityProvider));
        _networkService = networkService;
        _detachedHeadVisualSourceAdapter = detachedHeadVisualSourceAdapter ?? throw new ArgumentNullException(nameof(detachedHeadVisualSourceAdapter));
        _placementService = placementService ?? throw new ArgumentNullException(nameof(placementService));
        _visualFactory = visualFactory ?? throw new ArgumentNullException(nameof(visualFactory));
        _fearVisualOverrides = fearVisualOverrides;
        _posePresentationService = posePresentationService;
    }

    /// <summary>
    /// Updates placeholder visual lifetime and placement.
    /// </summary>
    public void LateTick()
    {
        if (_disposed || _disabledDueToError)
        {
            return;
        }

        try
        {
            if (!FloatingHeadFrameUpdateRules.ShouldRunFullVisualUpdate(
                Time.frameCount,
                _lastFullUpdateFrame,
                _hasCachedVisualState))
            {
                return;
            }

            TickCore(renderingCamera: null, poseSource: "LateUpdate", logPose: _config.DebugVisualLifecycle.Value);
        }
        catch (Exception ex)
        {
            ModLog.Error($"Floating-head placeholder visual failed and was disabled: {ex}");
            DestroyAll("exception");
            _disabledDueToError = true;
        }
    }

    /// <summary>
    /// Updates placeholder visual placement immediately before a game camera renders.
    /// </summary>
    public void CameraPreCullTick(Camera camera)
    {
        if (_disposed || _disabledDueToError || !IsRenderableGameCamera(camera))
        {
            return;
        }

        try
        {
            if (FloatingHeadFrameUpdateRules.ShouldRunFullVisualUpdate(
                Time.frameCount,
                _lastFullUpdateFrame,
                _hasCachedVisualState))
            {
                TickCore(camera, "PreCull", logPose: true);
                return;
            }

            UpdateCameraOnlyPose(camera, "PreCull", logPose: true);
        }
        catch (Exception ex)
        {
            ModLog.Error($"Floating-head placeholder visual pre-cull update failed and was disabled: {ex}");
            DestroyAll("pre-cull exception");
            _disabledDueToError = true;
        }
    }

    /// <summary>
    /// Draws a runtime screen fallback marker for render pipelines that do not show the 3D placeholder.
    /// </summary>
    public void GuiTick()
    {
        if (_disposed
            || _disabledDueToError
            || !_config.EnableScreenFallbackVisual.Value
            || !_config.EnableFloatingHeadVisuals.Value
            || (!_config.EnablePlaceholderVisuals.Value && !_config.UseRuntimeDetachedHeadVisuals.Value)
            || Event.current == null
            || Event.current.type != EventType.Repaint)
        {
            return;
        }

        if (!RuntimeConnectionState.CanUseModNetworking(out _)
            || !_placementService.TryGetActiveCamera(out Camera? camera)
            || camera == null)
        {
            return;
        }

        foreach (FloatingHeadVisual visual in _visuals.Values)
        {
            DrawScreenFallback(camera, visual);
        }
    }

    /// <summary>
    /// Clears all placeholder visuals.
    /// </summary>
    public void Clear()
    {
        DestroyAll("clear");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DestroyAll("dispose");
        if (_screenMarkerTexture != null)
        {
            UnityEngine.Object.Destroy(_screenMarkerTexture);
            _screenMarkerTexture = null;
        }

        _visualFactory.Dispose();
        _disposed = true;
    }

    private void TickCore(Camera? renderingCamera, string poseSource, bool logPose)
    {
        if (!_config.EnableFloatingHeadVisuals.Value
            || (!_config.EnablePlaceholderVisuals.Value && !_config.UseRuntimeDetachedHeadVisuals.Value))
        {
            DestroyAll("disabled by config");
            return;
        }

        if (!RuntimeConnectionState.CanUseModNetworking(out string reason))
        {
            DestroyAll($"network/lifecycle unavailable: {reason}");
            return;
        }

        LocalSpectatorPresenceState presence = _presenceProvider.Current;
        if (!presence.HasLocalPlayer || presence.RemoteSpectators.Count == 0)
        {
            if (ShouldHoldPresenceGrace())
            {
                return;
            }

            ClearForPresenceLost();
            return;
        }

        _lastPresenceSeenUnscaledTime = Time.unscaledTime;
        _presenceGraceLogged = false;
        if (!_placementService.TryGetAnchorPosition(out Vector3 anchorPosition))
        {
            if (!_anchorLostLogged)
            {
                LogDebug("Floating-head placeholder anchor lost; clearing visuals.");
                _anchorLostLogged = true;
            }

            DestroyAll("anchor lost");
            return;
        }

        _anchorLostLogged = false;
        IReadOnlyList<RemoteSpectatorInfo> spectators = SortSpectators(presence.RemoteSpectators);
        _lastAnchorPosition = anchorPosition;
        _hasCachedVisualState = true;
        _lastFullUpdateFrame = Time.frameCount;
        SyncVisuals(spectators);
        UpdateVisualPoses(anchorPosition, spectators, renderingCamera, poseSource, logPose, updateDynamicState: true);
    }

    private void UpdateCameraOnlyPose(Camera renderingCamera, string poseSource, bool logPose)
    {
        if (!_hasCachedVisualState || _visuals.Count == 0 || _sortedSpectators.Count == 0)
        {
            return;
        }

        UpdateVisualPoses(
            _lastAnchorPosition,
            _sortedSpectators,
            renderingCamera,
            poseSource,
            logPose,
            updateDynamicState: false);
    }

    private void ClearForPresenceLost()
    {
        InvalidateCachedVisualState();
        if (_config.DestroyOnPresenceLost.Value)
        {
            DestroyAll("presence lost");
            return;
        }

        foreach (FloatingHeadVisual visual in _visuals.Values)
        {
            visual.SetVisible(false);
        }
    }

    private bool ShouldHoldPresenceGrace()
    {
        if (_visuals.Count == 0)
        {
            return false;
        }

        float graceSeconds = Mathf.Max(0f, _config.PresenceLostGraceSeconds.Value);
        if (graceSeconds <= 0f)
        {
            return false;
        }

        float elapsed = Time.unscaledTime - _lastPresenceSeenUnscaledTime;
        if (elapsed < 0f || elapsed > graceSeconds)
        {
            return false;
        }

        if (!_presenceGraceLogged)
        {
            LogDebug($"Floating-head placeholder presence temporarily empty; holding visuals for {graceSeconds:0.00}s grace window.");
            _presenceGraceLogged = true;
        }

        return true;
    }

    private void SyncVisuals(IReadOnlyList<RemoteSpectatorInfo> spectators)
    {
        _activeSpectatorIds.Clear();
        bool hasDetachedHeadTemplate = TryGetRuntimeDetachedHeadTemplate(out Transform? detachedHeadTemplate);
        foreach (RemoteSpectatorInfo spectator in spectators)
        {
            _activeSpectatorIds.Add(spectator.SpectatorClientId);
            bool fearOverrideActive =
                _fearVisualOverrides?.IsFearVisualActive(spectator.SpectatorClientId) == true;
            bool shouldCreateVisual = DetachedHeadVisualSourceRules.TryResolveVisualSourceKind(
                _config.EnablePlaceholderVisuals.Value,
                _config.UseRuntimeDetachedHeadVisuals.Value,
                hasDetachedHeadTemplate,
                _config.FallbackToPlaceholderWhenDetachedHeadUnavailable.Value,
                out FloatingHeadVisualSourceKind desiredSourceKind);

            if (_visuals.TryGetValue(spectator.SpectatorClientId, out FloatingHeadVisual existingVisual))
            {
                if (!shouldCreateVisual)
                {
                    RemoveVisual(spectator.SpectatorClientId, "visual source unavailable");
                    LogVisualSkippedOnce(spectator, hasDetachedHeadTemplate);
                    continue;
                }

                if (existingVisual.SourceKind != desiredSourceKind)
                {
                    RemoveVisual(
                        spectator.SpectatorClientId,
                        $"visual source changed from {existingVisual.SourceKind} to {desiredSourceKind}");
                }
                else
                {
                    existingVisual.SetPrimaryRendererVisible(!fearOverrideActive);
                    continue;
                }
            }

            if (!shouldCreateVisual)
            {
                LogVisualSkippedOnce(spectator, hasDetachedHeadTemplate);
                continue;
            }

            if (!_visuals.ContainsKey(spectator.SpectatorClientId))
            {
                FloatingHeadVisual? visual = CreateVisual(spectator, desiredSourceKind, detachedHeadTemplate);
                if (visual == null)
                {
                    LogVisualSkippedOnce(spectator, hasDetachedHeadTemplate);
                    continue;
                }

                _visuals[spectator.SpectatorClientId] = visual;
                visual.SetPrimaryRendererVisible(!fearOverrideActive);
                _visualSkipLoggedSpectators.Remove(spectator.SpectatorClientId);
                LogDebug(
                    $"Floating-head visual created: spectatorClient={spectator.SpectatorClientId}, spectatorSlot={spectator.SpectatorSlotId}, source={visual.SourceKind}, style={_config.VisualStyle.Value}, {GetCreationDebugInfo(visual)}.");
            }
        }

        _staleVisualIds.Clear();
        foreach (ulong spectatorClientId in _visuals.Keys)
        {
            if (!_activeSpectatorIds.Contains(spectatorClientId))
            {
                _staleVisualIds.Add(spectatorClientId);
            }
        }

        foreach (ulong spectatorClientId in _staleVisualIds)
        {
            RemoveVisual(spectatorClientId, "presence lost");
        }

        _staleVisualIds.Clear();
    }

    private void UpdateVisualPoses(
        Vector3 anchorPosition,
        IReadOnlyList<RemoteSpectatorInfo> spectators,
        Camera? renderingCamera,
        string poseSource,
        bool logPose,
        bool updateDynamicState)
    {
        int visualCount = spectators.Count;
        for (int index = 0; index < visualCount; index++)
        {
            RemoteSpectatorInfo spectator = spectators[index];
            if (!_visuals.TryGetValue(spectator.SpectatorClientId, out FloatingHeadVisual visual))
            {
                continue;
            }

            float scale = updateDynamicState
                ? CalculateVisualScale(spectator, visual)
                : visual.CurrentScale;
            if (updateDynamicState)
            {
                UpdateNameTagText(spectator, visual);
            }

            if (TryGetRemoteSpectatorPose(
                spectator,
                visual.SourceKind,
                renderingCamera,
                out Vector3 remotePosition,
                out Quaternion remoteRotation,
                out Vector3 rawRemotePosition,
                out bool usedVisibleProxy,
                out bool motionReferenced,
                out SpectatorMotionReferencePose motionReference))
            {
                int visualLayer = ResolveVisibleLayer(renderingCamera);
                visual.SetLayer(visualLayer);
                visual.ApplyPose(
                    remotePosition,
                    remoteRotation,
                    scale,
                    Mathf.Max(0f, _config.RemotePoseSmoothTime.Value),
                    scaleSmoothTime: 0f,
                    spectator.PoseState!.TimestampTicks,
                    motionReferenced && !usedVisibleProxy,
                    motionReference);
                float fearTopOffset = 0f;
                bool hasFearTop = _fearVisualOverrides != null
                    && _fearVisualOverrides.TryGetWorldTopOffset(
                        spectator.SpectatorClientId,
                        out fearTopOffset);
                float nameTagHeight = FearNameTagPlacementRules.ResolveHeightOffset(
                    _config.NameTagHeightOffset.Value,
                    hasFearTop,
                    hasFearTop ? fearTopOffset : 0f);

                visual.UpdateNameTag(renderingCamera, nameTagHeight);
                if (logPose)
                {
                    LogFirstRemotePose(
                        spectator.SpectatorClientId,
                        remotePosition,
                        rawRemotePosition,
                        anchorPosition,
                        visual,
                        visualLayer,
                        renderingCamera,
                        usedVisibleProxy ? $"{poseSource}:RemotePoseVisibleProxy" : $"{poseSource}:RemotePose",
                        usedVisibleProxy,
                        remoteRotation);
                }

                continue;
            }

            if (_placementService.TryGetPose(
                anchorPosition,
                index,
                visualCount,
                _config.FloatingHeadRingRadius.Value,
                _config.FloatingHeadHeightOffset.Value,
                _config.UseCameraVisiblePlacement.Value,
                _config.CameraForwardOffset.Value,
                _config.FloatingHeadFaceCamera.Value,
                renderingCamera,
                out Vector3 position,
                out Quaternion rotation))
            {
                int visualLayer = ResolveVisibleLayer(renderingCamera);
                visual.SetLayer(visualLayer);
                visual.ApplyPose(position, rotation, scale);
                visual.UpdateNameTag(renderingCamera);
                if (logPose)
                {
                    LogFirstPose(spectator.SpectatorClientId, position, anchorPosition, visual, visualLayer, renderingCamera, poseSource);
                }
            }
        }
    }

    private float CalculateVisualScale(RemoteSpectatorInfo spectator, FloatingHeadVisual visual)
    {
        float targetVoiceLevel = 0f;
        bool hasVoiceData = TryGetVoiceActivity(spectator, out VoiceActivityState voiceState);
        targetVoiceLevel = FloatingHeadVoiceScaleRules.ResolveTargetVoiceLevel(
            hasVoiceData && voiceState.HasData,
            voiceState.IsSpeaking,
            voiceState.Amplitude,
            _config.MinimumSpeakingVoiceLevel.Value);

        float voiceLevel = visual.UpdateVoiceLevel(
            targetVoiceLevel,
            Mathf.Max(0f, _config.VoiceAttackSmoothTime.Value),
            Mathf.Max(0f, _config.VoiceReleaseSmoothTime.Value));
        float pulse = 0.5f + (0.5f * Mathf.Sin(Time.time * Mathf.Max(0f, _config.SpeakingPulseSpeed.Value)));
        float scaleMultiplier = FloatingHeadVoiceScaleRules.ResolveScaleMultiplier(
            _config.SilenceScaleMultiplier.Value,
            _config.SpeakingScaleMultiplier.Value,
            voiceLevel,
            _config.PulseWhenSpeaking.Value,
            _config.SpeakingPulseAmount.Value,
            pulse);

        float baseScale = visual.SourceKind == FloatingHeadVisualSourceKind.RuntimeDetachedHead
            ? _config.RuntimeDetachedHeadScale.Value
            : _config.VisualStyle.Value == FloatingHeadVisualStyle.Sphere
            ? _config.PlaceholderScale.Value
            : _config.BillboardSize.Value;
        return Mathf.Max(0.01f, baseScale * scaleMultiplier);
    }

    private FloatingHeadVisual? CreateVisual(
        RemoteSpectatorInfo spectator,
        FloatingHeadVisualSourceKind sourceKind,
        Transform? detachedHeadSource)
    {
        string nameTagText = FormatNameTagText(spectator, out bool canCacheNameTagText);
        if (canCacheNameTagText)
        {
            _nameTagTextCache[spectator.SpectatorClientId] =
                new NameTagTextCacheEntry(spectator.SpectatorSlotId, nameTagText);
        }

        if (sourceKind == FloatingHeadVisualSourceKind.RuntimeDetachedHead && detachedHeadSource != null)
        {
            return _visualFactory.CreateFromDetachedHead(
                spectator,
                detachedHeadSource,
                _config.RuntimeDetachedHeadScale.Value,
                _config.ShowNameTags.Value,
                _config.NameTagScale.Value,
                _config.NameTagHeightOffset.Value,
                _config.NameTagMaxDistance.Value,
                nameTagText);
        }

        if (sourceKind != FloatingHeadVisualSourceKind.Placeholder)
        {
            return null;
        }

        return _visualFactory.Create(
            spectator,
            _config.PlaceholderScale.Value,
            _config.VisualStyle.Value,
            _config.BillboardSize.Value,
            _config.BaseAlpha.Value,
            _config.UseUnlitMaterial.Value,
            _config.EnableDepthTest.Value,
            _config.ShowNameTags.Value,
            _config.NameTagScale.Value,
            _config.NameTagHeightOffset.Value,
            _config.NameTagMaxDistance.Value,
            nameTagText);
    }

    private void LogVisualSkippedOnce(RemoteSpectatorInfo spectator, bool hasDetachedHeadTemplate)
    {
        if (!_visualSkipLoggedSpectators.Add(spectator.SpectatorClientId))
        {
            return;
        }

        LogDebug(
            $"Floating-head visual skipped: spectatorClient={spectator.SpectatorClientId}, detachedHeadEnabled={_config.UseRuntimeDetachedHeadVisuals.Value}, hasDetachedHeadTemplate={hasDetachedHeadTemplate}, placeholderEnabled={_config.EnablePlaceholderVisuals.Value}, fallbackEnabled={_config.FallbackToPlaceholderWhenDetachedHeadUnavailable.Value}.");
    }

    private bool TryGetRuntimeDetachedHeadTemplate(out Transform? source)
    {
        if (!_config.UseRuntimeDetachedHeadVisuals.Value)
        {
            _detachedHeadTemplateSource = null;
            source = null;
            return false;
        }

        if (_detachedHeadTemplateSource != null)
        {
            source = _detachedHeadTemplateSource;
            return true;
        }

        try
        {
            if (_detachedHeadVisualSourceAdapter.TryGetDetachedHeadVisualTemplate(out source)
                && source != null)
            {
                _detachedHeadTemplateSource = source;
                LogDebug($"Runtime detached-head visual template resolved: source={source.name}.");
                return true;
            }

            source = null;
            return false;
        }
        catch (Exception ex)
        {
            source = null;
            _detachedHeadTemplateSource = null;
            LogDebug($"Runtime detached-head visual template lookup failed: {ex.GetType().Name}.");
            return false;
        }
    }

    private bool TryGetVoiceActivity(RemoteSpectatorInfo spectator, out VoiceActivityState state)
    {
        state = VoiceActivityState.NoData;
        if (TryGetSyncedVoiceActivity(spectator, out state))
        {
            return true;
        }

        if (!FloatingHeadVoiceActivityRules.ShouldUseLocalFallback(
            _config.EnableVoiceActivitySync.Value,
            _networkService != null))
        {
            LogVoiceDataLost(spectator.SpectatorClientId);
            return false;
        }

        if (_voiceProviderDisabled)
        {
            return false;
        }

        try
        {
            if (!_voiceActivityProvider.TryGetVoiceActivity(
                spectator.SpectatorClientId,
                spectator.SpectatorSlotId,
                out state)
                || !state.HasData)
            {
                LogVoiceDataLost(spectator.SpectatorClientId);
                return false;
            }

            LogVoiceDataDetected(spectator.SpectatorClientId);
            return true;
        }
        catch (Exception ex)
        {
            _voiceProviderDisabled = true;
            LogDebug($"Voice activity provider failed and was disabled for this session: {ex.GetType().Name}.");
            state = VoiceActivityState.NoData;
            return false;
        }
    }

    private bool TryGetSyncedVoiceActivity(RemoteSpectatorInfo spectator, out VoiceActivityState state)
    {
        state = VoiceActivityState.NoData;
        if (!_config.EnableVoiceActivitySync.Value || _networkService == null)
        {
            return false;
        }

        if (_networkService.TryGetRemoteVoiceActivity(spectator.SpectatorClientId, out state)
            && state.HasData
            && state.ClientId == spectator.SpectatorClientId
            && state.SlotId == spectator.SpectatorSlotId)
        {
            LogVoiceDataDetected(spectator.SpectatorClientId);
            return true;
        }

        if (_networkService.TryGetPeerCapability(spectator.SpectatorClientId, out ModPeerCapability capability)
            && ModPeerCapabilityRules.SupportsCurrentVoiceActivitySync(capability))
        {
            state = new VoiceActivityState(
                true,
                false,
                0f,
                0f,
                spectator.SpectatorClientId,
                spectator.SpectatorSlotId,
                DateTime.UtcNow.Ticks);
            LogVoiceDataDetected(spectator.SpectatorClientId);
            return true;
        }

        return false;
    }

    private bool TryGetRemoteSpectatorPose(
        RemoteSpectatorInfo spectator,
        FloatingHeadVisualSourceKind sourceKind,
        Camera? renderingCamera,
        out Vector3 position,
        out Quaternion rotation,
        out Vector3 rawPosition,
        out bool usedVisibleProxy,
        out bool motionReferenced,
        out SpectatorMotionReferencePose motionReference)
    {
        if (spectator.PoseState == null || !spectator.PoseState.IsSpectating)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            rawPosition = Vector3.zero;
            usedVisibleProxy = false;
            motionReferenced = false;
            motionReference = default;
            return false;
        }

        rawPosition = spectator.PoseState.Position;
        if (_posePresentationService != null)
        {
            _posePresentationService.Resolve(
                spectator.PoseState,
                out position,
                out rotation,
                out motionReferenced,
                out motionReference);
        }
        else
        {
            position = rawPosition;
            rotation = spectator.PoseState.Rotation;
            motionReferenced = false;
            motionReference = default;
        }
        if (sourceKind == FloatingHeadVisualSourceKind.RuntimeDetachedHead)
        {
            rotation = FloatingHeadRotationRules.ApplyRuntimeDetachedHeadOffset(
                rotation,
                _config.RuntimeDetachedHeadPitchOffset.Value,
                _config.RuntimeDetachedHeadYawOffset.Value,
                _config.RuntimeDetachedHeadRollOffset.Value);
        }

        Camera? camera = renderingCamera;
        if (camera == null)
        {
            _placementService.TryGetActiveCamera(out camera);
        }

        usedVisibleProxy = _config.KeepRemotePoseInView.Value
            && camera != null
            && TryGetVisibleProxyPosition(position, camera, out position);

        if (FloatingHeadRotationRules.ShouldFaceLocalCamera(sourceKind, _config.FloatingHeadFaceCamera.Value))
        {
            if (camera != null)
            {
                Vector3 toCamera = camera.transform.position - position;
                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
                }
            }
        }

        return true;
    }

    private bool TryGetVisibleProxyPosition(Vector3 remotePosition, Camera camera, out Vector3 proxyPosition)
    {
        const float visibleMinX = 0.08f;
        const float visibleMaxX = 0.92f;
        const float visibleMinY = 0.12f;
        const float visibleMaxY = 0.88f;
        proxyPosition = remotePosition;

        Vector3 viewport = camera.WorldToViewportPoint(remotePosition);
        if (viewport.z > camera.nearClipPlane + 0.05f
            && viewport.x >= visibleMinX
            && viewport.x <= visibleMaxX
            && viewport.y >= visibleMinY
            && viewport.y <= visibleMaxY)
        {
            return false;
        }

        float distance = Mathf.Max(
            camera.nearClipPlane + 0.35f,
            _config.RemotePoseVisibleProxyDistance.Value);

        Transform cameraTransform = camera.transform;
        Vector2 direction;
        if (viewport.z > camera.nearClipPlane + 0.05f)
        {
            direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
        }
        else
        {
            Vector3 localRemote = cameraTransform.InverseTransformPoint(remotePosition);
            direction = new Vector2(localRemote.x, localRemote.y);
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.up;
        }
        else
        {
            direction.Normalize();
        }

        Vector2 edgeViewport = ProjectDirectionToViewportEdge(
            direction,
            visibleMinX,
            visibleMaxX,
            visibleMinY,
            visibleMaxY);
        Vector3 proxyViewport = new Vector3(edgeViewport.x, edgeViewport.y, distance);
        proxyPosition = camera.ViewportToWorldPoint(proxyViewport);
        return true;
    }

    private static Vector2 ProjectDirectionToViewportEdge(
        Vector2 direction,
        float minX,
        float maxX,
        float minY,
        float maxY)
    {
        const float centerX = 0.5f;
        const float centerY = 0.5f;
        float scaleX = float.PositiveInfinity;
        float scaleY = float.PositiveInfinity;

        if (direction.x > 0.0001f)
        {
            scaleX = (maxX - centerX) / direction.x;
        }
        else if (direction.x < -0.0001f)
        {
            scaleX = (minX - centerX) / direction.x;
        }

        if (direction.y > 0.0001f)
        {
            scaleY = (maxY - centerY) / direction.y;
        }
        else if (direction.y < -0.0001f)
        {
            scaleY = (minY - centerY) / direction.y;
        }

        float scale = Mathf.Min(scaleX, scaleY);
        if (float.IsInfinity(scale) || scale <= 0f)
        {
            scale = 0f;
        }

        return new Vector2(
            Mathf.Clamp(centerX + (direction.x * scale), minX, maxX),
            Mathf.Clamp(centerY + (direction.y * scale), minY, maxY));
    }

    private void LogFirstPose(
        ulong spectatorClientId,
        Vector3 position,
        Vector3 anchorPosition,
        FloatingHeadVisual visual,
        int visualLayer,
        Camera? renderingCamera,
        string poseSource)
    {
        if (!_config.DebugVisualLifecycle.Value || !_poseLoggedSpectators.Add(spectatorClientId))
        {
            return;
        }

        string cameraInfo = TryGetCameraDebugInfo(renderingCamera);
        string visibilityInfo = TryGetPoseVisibilityDebugInfo(renderingCamera, visual, position, visualLayer, usedVisibleProxy: false);
        LogDebug(
            $"Floating-head placeholder pose applied: spectatorClient={spectatorClientId}, source={poseSource}, position={FormatVector(position)}, anchor={FormatVector(anchorPosition)}, layer={visualLayer}, cameraVisiblePlacement={_config.UseCameraVisiblePlacement.Value}, {cameraInfo}, {visibilityInfo}.");
    }

    private void LogFirstRemotePose(
        ulong spectatorClientId,
        Vector3 position,
        Vector3 rawPosition,
        Vector3 anchorPosition,
        FloatingHeadVisual visual,
        int visualLayer,
        Camera? renderingCamera,
        string poseSource,
        bool usedVisibleProxy,
        Quaternion rotation)
    {
        string logKey = $"{spectatorClientId}:{poseSource}";
        if (!_config.DebugVisualLifecycle.Value || !_remotePoseLoggedSpectators.Add(logKey))
        {
            return;
        }

        string cameraInfo = TryGetCameraDebugInfo(renderingCamera);
        string visibilityInfo = TryGetPoseVisibilityDebugInfo(renderingCamera, visual, position, visualLayer, usedVisibleProxy);
        LogDebug(
            $"Floating-head placeholder remote pose applied: spectatorClient={spectatorClientId}, source={poseSource}, position={FormatVector(position)}, rawPosition={FormatVector(rawPosition)}, rotationEuler={FormatVector(rotation.eulerAngles)}, visibleProxy={usedVisibleProxy}, anchor={FormatVector(anchorPosition)}, layer={visualLayer}, {cameraInfo}, {visibilityInfo}.");
    }

    private void RemoveVisual(ulong spectatorClientId, string reason)
    {
        if (!_visuals.TryGetValue(spectatorClientId, out FloatingHeadVisual visual))
        {
            return;
        }

        _visuals.Remove(spectatorClientId);
        _poseLoggedSpectators.Remove(spectatorClientId);
        string logKeyPrefix = $"{spectatorClientId}:";
        _remotePoseLoggedSpectators.RemoveWhere(key => key.StartsWith(logKeyPrefix, StringComparison.Ordinal));
        _screenFallbackLoggedSpectators.Remove(spectatorClientId);
        _voiceDataLoggedSpectators.Remove(spectatorClientId);
        _voiceDataActiveSpectators.Remove(spectatorClientId);
        _visualSkipLoggedSpectators.Remove(spectatorClientId);
        _nameTagTextCache.Remove(spectatorClientId);
        visual.Dispose();
        LogDebug($"Floating-head placeholder visual destroyed: spectatorClient={spectatorClientId}, reason={reason}.");
    }

    private void DestroyAll(string reason)
    {
        if (_visuals.Count == 0)
        {
            InvalidateCachedVisualState();
            return;
        }

        List<ulong> ids = new List<ulong>(_visuals.Keys);
        foreach (ulong spectatorClientId in ids)
        {
            FloatingHeadVisual visual = _visuals[spectatorClientId];
            visual.Dispose();
            LogDebug($"Floating-head placeholder visual destroyed: spectatorClient={spectatorClientId}, reason={reason}.");
        }

        _visuals.Clear();
        _poseLoggedSpectators.Clear();
        _remotePoseLoggedSpectators.Clear();
        _screenFallbackLoggedSpectators.Clear();
        _voiceDataLoggedSpectators.Clear();
        _voiceDataActiveSpectators.Clear();
        _visualSkipLoggedSpectators.Clear();
        _nameTagTextCache.Clear();
        InvalidateCachedVisualState();
    }

    private void InvalidateCachedVisualState()
    {
        _hasCachedVisualState = false;
        _lastFullUpdateFrame = -1;
        _lastAnchorPosition = Vector3.zero;
        _sortedSpectators.Clear();
    }

    private void DrawScreenFallback(Camera camera, FloatingHeadVisual visual)
    {
        if (!visual.State.IsVisible)
        {
            return;
        }

        float size = Mathf.Clamp(_config.ScreenFallbackSize.Value, 8f, 96f);
        float halfSize = size * 0.5f;
        Vector3 screenPosition = camera.WorldToScreenPoint(visual.State.Position);
        bool behindCamera = screenPosition.z <= camera.nearClipPlane;
        Vector2 fallbackPosition = ResolveScreenFallbackPosition(camera, visual.State.Position, screenPosition, halfSize, behindCamera);
        float x = fallbackPosition.x;
        float y = fallbackPosition.y;
        Rect rect = new Rect(x - halfSize, y - halfSize, size, size);

        Texture2D texture = GetOrCreateScreenMarkerTexture();
        Color previousColor = GUI.color;
        Color markerColor = visual.BaseColor;
        markerColor.a = Mathf.Clamp01(Mathf.Max(markerColor.a, _config.BaseAlpha.Value));
        GUI.color = markerColor;
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        GUI.color = previousColor;

        if (_config.DebugVisualLifecycle.Value && _screenFallbackLoggedSpectators.Add(visual.SpectatorClientId))
        {
            Vector3 viewport = camera.WorldToViewportPoint(visual.State.Position);
            LogDebug(
                $"Floating-head screen fallback active: spectatorClient={visual.SpectatorClientId}, screen=({x:0}, {y:0}), rawScreen=({screenPosition.x:0}, {screenPosition.y:0}, {screenPosition.z:0.00}), viewport=({viewport.x:0.00}, {viewport.y:0.00}, {viewport.z:0.00}), behindCamera={behindCamera}, world={FormatVector(visual.State.Position)}, camera={camera.name}.");
        }
    }

    private static Vector2 ResolveScreenFallbackPosition(
        Camera camera,
        Vector3 worldPosition,
        Vector3 screenPosition,
        float halfSize,
        bool behindCamera)
    {
        if (!behindCamera)
        {
            return new Vector2(
                Mathf.Clamp(screenPosition.x, halfSize, Screen.width - halfSize),
                Mathf.Clamp(Screen.height - screenPosition.y, halfSize, Screen.height - halfSize));
        }

        Vector3 localPosition = camera.transform.InverseTransformPoint(worldPosition);
        Vector2 direction = new Vector2(localPosition.x, localPosition.y);
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.up;
        }
        else
        {
            direction.Normalize();
        }

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        float edgeX = direction.x >= 0f ? Screen.width - halfSize : halfSize;
        float edgeY = direction.y >= 0f ? halfSize : Screen.height - halfSize;
        float scaleX = Mathf.Abs(direction.x) > 0.0001f
            ? (edgeX - centerX) / direction.x
            : float.PositiveInfinity;
        float scaleY = Mathf.Abs(direction.y) > 0.0001f
            ? (edgeY - centerY) / -direction.y
            : float.PositiveInfinity;
        float scale = Mathf.Min(Mathf.Abs(scaleX), Mathf.Abs(scaleY));
        if (float.IsInfinity(scale) || scale <= 0f)
        {
            scale = Mathf.Min(Screen.width, Screen.height) * 0.25f;
        }

        return new Vector2(
            Mathf.Clamp(centerX + (direction.x * scale), halfSize, Screen.width - halfSize),
            Mathf.Clamp(centerY - (direction.y * scale), halfSize, Screen.height - halfSize));
    }

    private Texture2D GetOrCreateScreenMarkerTexture()
    {
        if (_screenMarkerTexture != null)
        {
            return _screenMarkerTexture;
        }

        const int size = 32;
        const float radius = (size - 2) * 0.5f;
        const float center = (size - 1) * 0.5f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                float alpha = Mathf.Clamp01(radius - distance);
                pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        _screenMarkerTexture = new Texture2D(size, size, TextureFormat.ARGB32, false)
        {
            name = "Enhanced Spectator Screen Marker",
            hideFlags = HideFlags.HideAndDontSave,
        };
        _screenMarkerTexture.SetPixels(pixels);
        _screenMarkerTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        return _screenMarkerTexture;
    }

    private void LogVoiceDataDetected(ulong spectatorClientId)
    {
        _voiceDataActiveSpectators.Add(spectatorClientId);
        if (_config.DebugVisualLifecycle.Value && _voiceDataLoggedSpectators.Add(spectatorClientId))
        {
            LogDebug($"Voice activity data detected for placeholder spectatorClient={spectatorClientId}.");
        }
    }

    private void LogVoiceDataLost(ulong spectatorClientId)
    {
        if (_voiceDataActiveSpectators.Remove(spectatorClientId) && _config.DebugVisualLifecycle.Value)
        {
            LogDebug($"Voice activity data lost for placeholder spectatorClient={spectatorClientId}.");
        }
    }

    private void LogDebug(string message)
    {
        if (_config.DebugVisualLifecycle.Value)
        {
            ModLog.Debug(message);
        }
    }

    private static string GetCreationDebugInfo(FloatingHeadVisual visual)
    {
        return $"source={visual.SourceKind}, shader={visual.ShaderName}, materialRenderQueue={visual.MaterialRenderQueue}, rendererEnabled={visual.RendererEnabled}, forceRenderingOff={visual.ForceRenderingOff}, activeSelf={visual.ActiveSelf}, activeInHierarchy={visual.ActiveInHierarchy}, rootActive={visual.RootActiveInHierarchy}, layer={visual.Layer}, hasMeshRenderer={visual.HasMeshRenderer}, hasMeshFilter={visual.HasMeshFilter}, mesh={visual.MeshName}, colliderRemoved={visual.ColliderRemoved}, hasNameTag={visual.HasNameTag}";
    }

    private IReadOnlyList<RemoteSpectatorInfo> SortSpectators(IReadOnlyList<RemoteSpectatorInfo> spectators)
    {
        _sortedSpectators.Clear();
        for (int index = 0; index < spectators.Count; index++)
        {
            _sortedSpectators.Add(spectators[index]);
        }

        _sortedSpectators.Sort((left, right) => left.SpectatorClientId.CompareTo(right.SpectatorClientId));
        return _sortedSpectators;
    }

    private void UpdateNameTagText(RemoteSpectatorInfo spectator, FloatingHeadVisual visual)
    {
        if (!_config.ShowNameTags.Value)
        {
            return;
        }

        RefreshNameTagTextCache();
        string text;
        if (_nameTagTextCache.TryGetValue(spectator.SpectatorClientId, out NameTagTextCacheEntry entry)
            && entry.SpectatorSlotId == spectator.SpectatorSlotId)
        {
            text = entry.Text;
        }
        else
        {
            text = FormatNameTagText(spectator, out bool canCache);
            if (canCache)
            {
                _nameTagTextCache[spectator.SpectatorClientId] =
                    new NameTagTextCacheEntry(spectator.SpectatorSlotId, text);
            }
        }

        if (visual.TrySetNameTagText(text) && _config.DebugNameTagLifecycle.Value)
        {
            ModLog.Debug($"Floating-head name tag updated: spectatorClient={spectator.SpectatorClientId}, text={text.Replace('\n', ' ')}.");
        }
    }

    private void RefreshNameTagTextCache()
    {
        int identityRevision = _networkService != null ? _networkService.RemotePeerIdentityRevision : -1;
        bool useGamePlayerNames = _config.NameTagUseGamePlayerNames.Value;
        bool useFallbackIds = _config.NameTagUseFallbackIds.Value;
        if (!NameTagTextCacheRules.ShouldClear(
            _cachedNameTagIdentityRevision,
            identityRevision,
            _cachedNameTagUseGamePlayerNames,
            useGamePlayerNames,
            _cachedNameTagUseFallbackIds,
            useFallbackIds))
        {
            return;
        }

        _nameTagTextCache.Clear();
        _cachedNameTagIdentityRevision = identityRevision;
        _cachedNameTagUseGamePlayerNames = useGamePlayerNames;
        _cachedNameTagUseFallbackIds = useFallbackIds;
    }

    private string FormatNameTagText(RemoteSpectatorInfo spectator, out bool canCache)
    {
        if (_config.NameTagUseGamePlayerNames.Value
            && TryGetSyncedDisplayName(spectator.SpectatorClientId, out string syncedDisplayName))
        {
            canCache = true;
            return syncedDisplayName;
        }

        if (_config.NameTagUseGamePlayerNames.Value
            && _placementService.TryGetPlayerDisplayName(
                spectator.SpectatorClientId,
                spectator.SpectatorSlotId,
                out string displayName)
            && !string.IsNullOrWhiteSpace(displayName))
        {
            canCache = false;
            return displayName.Trim();
        }

        if (_config.NameTagUseFallbackIds.Value)
        {
            canCache = true;
            return $"Client {spectator.SpectatorClientId}\nSlot {spectator.SpectatorSlotId}";
        }

        canCache = true;
        return "Spectator";
    }

    private bool TryGetSyncedDisplayName(ulong spectatorClientId, out string displayName)
    {
        if (_networkService != null
            && _networkService.TryGetRemotePeerIdentity(spectatorClientId, out PeerIdentityState identity)
            && PlayerDisplayNameRules.TryNormalize(identity.DisplayName, out displayName))
        {
            return true;
        }

        displayName = string.Empty;
        return false;
    }

    private int ResolveVisibleLayer(Camera? renderingCamera)
    {
        const int defaultLayer = 0;
        Camera? camera = renderingCamera;
        if (camera == null && (!_placementService.TryGetActiveCamera(out camera) || camera == null))
        {
            return defaultLayer;
        }

        if (IsLayerVisible(camera, defaultLayer))
        {
            return defaultLayer;
        }

        if (!_layerWarningLogged)
        {
            LogDebug($"Floating-head default layer is not visible to camera={camera.name}; cullingMask={camera.cullingMask}. Searching for a visible layer.");
            _layerWarningLogged = true;
        }

        for (int layer = 0; layer < 32; layer++)
        {
            if (IsLayerVisible(camera, layer))
            {
                return layer;
            }
        }

        LogDebug($"Floating-head could not find any visible layer for camera={camera.name}; falling back to Default layer.");
        return defaultLayer;
    }

    private string TryGetCameraDebugInfo(Camera? renderingCamera)
    {
        Camera? camera = renderingCamera;
        if (camera == null && (!_placementService.TryGetActiveCamera(out camera) || camera == null))
        {
            return "camera=none";
        }

        return $"camera={camera.name}, cameraType={camera.cameraType}, cameraEnabled={camera.enabled}, cameraActive={camera.gameObject.activeInHierarchy}, cameraPosition={FormatVector(camera.transform.position)}, cameraForward={FormatVector(camera.transform.forward)}, nearClip={camera.nearClipPlane:0.00}, cullingMask={camera.cullingMask}";
    }

    private string TryGetPoseVisibilityDebugInfo(
        Camera? renderingCamera,
        FloatingHeadVisual visual,
        Vector3 position,
        int selectedLayer,
        bool usedVisibleProxy)
    {
        Camera? camera = renderingCamera;
        if (camera == null && (!_placementService.TryGetActiveCamera(out camera) || camera == null))
        {
            return $"screenFallbackEnabled={_config.EnableScreenFallbackVisual.Value}, visibleProxy={usedVisibleProxy}, poseVisibilityCamera=none";
        }

        Vector3 screen = camera.WorldToScreenPoint(position);
        Vector3 viewport = camera.WorldToViewportPoint(position);
        float distance = Vector3.Distance(camera.transform.position, position);
        bool selectedLayerVisible = selectedLayer >= 0 && selectedLayer < 32 && IsLayerVisible(camera, selectedLayer);
        bool defaultLayerVisible = IsLayerVisible(camera, 0);
        return $"screen=({screen.x:0}, {screen.y:0}, {screen.z:0.00}), viewport=({viewport.x:0.00}, {viewport.y:0.00}, {viewport.z:0.00}), distance={distance:0.00}, z={screen.z:0.00}, nearClip={camera.nearClipPlane:0.00}, selectedLayerVisible={selectedLayerVisible}, defaultLayerVisible={defaultLayerVisible}, screenFallbackEnabled={_config.EnableScreenFallbackVisual.Value}, visibleProxy={usedVisibleProxy}, visualActiveSelf={visual.ActiveSelf}, visualActiveInHierarchy={visual.ActiveInHierarchy}, rootActive={visual.RootActiveInHierarchy}, materialRenderQueue={visual.MaterialRenderQueue}, rendererEnabled={visual.RendererEnabled}, forceRenderingOff={visual.ForceRenderingOff}";
    }

    private static bool IsLayerVisible(Camera camera, int layer)
    {
        return (camera.cullingMask & (1 << layer)) != 0;
    }

    private static bool IsRenderableGameCamera(Camera? camera)
    {
        return camera != null
            && camera.enabled
            && camera.gameObject.activeInHierarchy
            && camera.cameraType == CameraType.Game;
    }

    private static string FormatVector(Vector3 vector)
    {
        return $"({vector.x:0.00}, {vector.y:0.00}, {vector.z:0.00})";
    }

    private readonly struct NameTagTextCacheEntry
    {
        public NameTagTextCacheEntry(ulong spectatorSlotId, string text)
        {
            SpectatorSlotId = spectatorSlotId;
            Text = text;
        }

        public ulong SpectatorSlotId { get; }

        public string Text { get; }
    }
}
