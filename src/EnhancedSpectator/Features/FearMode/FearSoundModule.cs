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
/// Handles the local fear-sound hotkey and safe positional playback of host-authorized events.
/// </summary>
public sealed class FearSoundModule : IFeatureModule, IRuntimeTickable, IRuntimeLateTickable, IFearSoundActions
{
    private const float NextSoundHoldSeconds = 0.65f;
    private readonly EnhancedSpectatorConfig _config;
    private readonly FearModeNetworkService _fearService;
    private readonly IGameFearModeAdapter _gameAdapter;
    private readonly SpectatorModule _spectatorModule;
    private readonly IEnhancedSpectatorNetworkService _networkService;
    private readonly RemoteSpectatorPosePresentationService _posePresentationService;
    private readonly List<AudioClip> _clipScratch = new List<AudioClip>();
    private readonly List<ActivePlayback> _activePlaybacks = new List<ActivePlayback>();
    private string _clipCursorModelKey = string.Empty;
    private string _localIntentModelKey = string.Empty;
    private int _clipCursor = -1;
    private float _keyPressedAt = -1f;
    private bool _longHoldTriggered;
    private bool _holdStartedPlayback;
    private bool _autoCycleArmed;
    private float _autoCycleResumeAt;
    private bool _localIntentPlaying;
    private bool _initialized;

    /// <summary>Creates the fear-sound module.</summary>
    public FearSoundModule(
        EnhancedSpectatorConfig config,
        FearModeNetworkService fearService,
        IGameFearModeAdapter gameAdapter,
        SpectatorModule spectatorModule,
        IEnhancedSpectatorNetworkService networkService,
        RemoteSpectatorPosePresentationService posePresentationService)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _fearService = fearService ?? throw new ArgumentNullException(nameof(fearService));
        _gameAdapter = gameAdapter ?? throw new ArgumentNullException(nameof(gameAdapter));
        _spectatorModule = spectatorModule ?? throw new ArgumentNullException(nameof(spectatorModule));
        _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
        _posePresentationService = posePresentationService ?? throw new ArgumentNullException(nameof(posePresentationService));
    }

    /// <inheritdoc />
    public void Initialize()
    {
        _initialized = true;
    }

    /// <inheritdoc />
    public bool IsLocalSoundPlaying => _localIntentPlaying;

    /// <inheritdoc />
    public bool TryToggleLocalSound()
    {
        if (!_initialized
            || !_fearService.IsSessionEnabled
            || !_fearService.IsLocalRenderingEnabled)
        {
            return false;
        }

        bool wasPlaying = _localIntentPlaying;
        bool started = HandleInitialPress();
        return wasPlaying ? !_localIntentPlaying : started;
    }

    /// <inheritdoc />
    public bool TryPlayNextLocalSound()
    {
        return _initialized
            && _fearService.IsSessionEnabled
            && _fearService.IsLocalRenderingEnabled
            && TryPlayNextSound();
    }

    /// <inheritdoc />
    public void Tick()
    {
        if (!_initialized
            || !_fearService.IsSessionEnabled
            || !_fearService.IsLocalRenderingEnabled
            || _gameAdapter.IsLocalQuickMenuOpen())
        {
            ResetKeyHold();
            return;
        }

        if (SpectatorInputService.IsKeyPressedThisFrame(_config.FearSoundNextKey.Value))
        {
            ResetKeyHold();
            TryPlayNextLocalSound();
            return;
        }

        bool held = SpectatorInputService.IsKeyHeld(_config.FearSoundKey.Value);
        bool pressed = SpectatorInputService.IsKeyPressedThisFrame(_config.FearSoundKey.Value);
        if (!held)
        {
            ResetKeyHold();
            return;
        }

        if (pressed)
        {
            _keyPressedAt = Time.unscaledTime;
            _longHoldTriggered = false;
            _autoCycleArmed = false;
            _holdStartedPlayback = HandleInitialPress();
            return;
        }

        if (!_longHoldTriggered
            && _holdStartedPlayback
            && _keyPressedAt >= 0f
            && Time.unscaledTime - _keyPressedAt >= NextSoundHoldSeconds)
        {
            _longHoldTriggered = true;
            _autoCycleArmed = true;
            TryContinueAutoCycle();
        }
    }

    /// <inheritdoc />
    public void LateTick()
    {
        if (!_initialized)
        {
            return;
        }

        if (!_fearService.IsSessionEnabled || !_fearService.IsLocalRenderingEnabled)
        {
            StopAllPlaybacks();
            _localIntentPlaying = false;
        }

        while (_fearService.TryDequeueSoundEvent(out FearSoundEventState soundEvent))
        {
            if (soundEvent.Action == FearSoundAction.Stop)
            {
                StopPlayback(soundEvent.ClientId);
            }
            else
            {
                TryStartPlayback(soundEvent);
            }
        }

        for (int index = _activePlaybacks.Count - 1; index >= 0; index--)
        {
            ActivePlayback playback = _activePlaybacks[index];
            bool hasMatchingSelection = _fearService.TryGetSelection(
                    playback.ClientId,
                    out FearModeSelectionState selection)
                && string.Equals(selection.ModelKey, playback.ModelKey, StringComparison.Ordinal);
            if (playback.Source != null && playback.Source.isPlaying)
            {
                playback.MarkObservedPlaying();
            }

            bool waitingForStartup = playback.Source != null
                && !playback.Source.isPlaying
                && !playback.HasObservedPlaying
                && Time.unscaledTime < playback.StartupGraceUntil
                && hasMatchingSelection;
            if (waitingForStartup)
            {
                if (TryResolveWorldPosition(playback.ClientId, out Vector3 startupPosition))
                {
                    playback.Source!.transform.position = startupPosition;
                }

                continue;
            }

            bool completedNaturally = playback.Source != null
                && !playback.Source.isPlaying
                && hasMatchingSelection;
            if (playback.Source == null || !playback.Source.isPlaying || !hasMatchingSelection)
            {
                bool localPlayback = IsLocalClient(playback.ClientId);
                playback.Dispose();
                _activePlaybacks.RemoveAt(index);
                if (localPlayback
                    && completedNaturally
                    && FearSoundCycleRules.ShouldAdvance(
                        _autoCycleArmed,
                        SpectatorInputService.IsKeyHeld(_config.FearSoundKey.Value),
                        hasMatchingSelection))
                {
                    _localIntentPlaying = false;
                    _autoCycleResumeAt = Mathf.Max(
                        Time.unscaledTime,
                        playback.StartedAt + 0.55f);
                }
                else
                {
                    ClearLocalIntentIfNeeded(playback.ClientId);
                }

                continue;
            }

            if (TryResolveWorldPosition(playback.ClientId, out Vector3 position))
            {
                playback.Source.transform.position = position;
            }
        }

        TryContinueAutoCycle();
        ApplyNearbyPlayerLimit();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        StopAllPlaybacks();
        _clipScratch.Clear();
        _clipCursorModelKey = string.Empty;
        _localIntentModelKey = string.Empty;
        _clipCursor = -1;
        _localIntentPlaying = false;
        _autoCycleArmed = false;
        _autoCycleResumeAt = 0f;
        ResetKeyHold();
        _initialized = false;
    }

    private bool HandleInitialPress()
    {
        if (!TryGetLocalSelectionAndClips(out ulong clientId, out FearModeSelectionState selection))
        {
            return false;
        }

        if (_localIntentPlaying
            && string.Equals(_localIntentModelKey, selection.ModelKey, StringComparison.Ordinal))
        {
            if (!_fearService.TryStopLocalSound(out string stopReason))
            {
                ModLog.Debug($"Fear sound stop request rejected: {stopReason}.");
            }

            StopPlayback(clientId);
            _localIntentPlaying = false;
            return false;
        }

        EnsureClipCursor(selection.ModelKey);
        if (_clipCursor < 0 || _clipCursor >= _clipScratch.Count)
        {
            _clipCursor = 0;
        }

        return RequestPlay(selection.ModelKey, _clipCursor, FearSoundAction.Play);
    }

    private bool TryPlayNextSound()
    {
        if (!TryGetLocalSelectionAndClips(out _, out FearModeSelectionState selection))
        {
            return false;
        }

        EnsureClipCursor(selection.ModelKey);
        int nextIndex = FearSoundCycleRules.ResolveNextIndex(_clipCursor, _clipScratch.Count);
        if (!RequestPlay(selection.ModelKey, nextIndex, FearSoundAction.PlayNext))
        {
            return false;
        }

        _clipCursor = nextIndex;
        return true;
    }

    private bool TryGetLocalSelectionAndClips(
        out ulong clientId,
        out FearModeSelectionState selection)
    {
        selection = null!;
        if (!_gameAdapter.TryGetLocalDeadPlayerIdentity(out clientId, out _)
            || !_fearService.TryGetSelection(clientId, out selection))
        {
            return false;
        }

        _gameAdapter.CopyFearSoundClipsTo(selection.ModelKey, _clipScratch);
        if (_clipScratch.Count > 0)
        {
            return true;
        }

        ModLog.Info($"Fear sound unavailable for model {selection.ModelKey}: no original clips found.");
        return false;
    }

    private void EnsureClipCursor(string modelKey)
    {
        if (string.Equals(_clipCursorModelKey, modelKey, StringComparison.Ordinal))
        {
            return;
        }

        _clipCursorModelKey = modelKey;
        _clipCursor = -1;
    }

    private bool RequestPlay(string modelKey, int clipIndex, FearSoundAction action)
    {
        if (!_fearService.TryTriggerLocalSound(clipIndex, action, out string reason))
        {
            ModLog.Debug($"Fear sound request rejected: {reason}.");
            return false;
        }

        _localIntentModelKey = modelKey;
        _localIntentPlaying = true;
        return true;
    }

    private void ResetKeyHold()
    {
        _keyPressedAt = -1f;
        _longHoldTriggered = false;
        _holdStartedPlayback = false;
        _autoCycleArmed = false;
    }

    private void TryContinueAutoCycle()
    {
        if (!_autoCycleArmed
            || _localIntentPlaying
            || Time.unscaledTime < _autoCycleResumeAt
            || !SpectatorInputService.IsKeyHeld(_config.FearSoundKey.Value))
        {
            return;
        }

        if (TryPlayNextSound())
        {
            _autoCycleResumeAt = 0f;
        }
    }

    private void TryStartPlayback(FearSoundEventState soundEvent)
    {
        if (!_fearService.IsSessionEnabled || !_fearService.IsLocalRenderingEnabled)
        {
            return;
        }

        StopPlayback(soundEvent.ClientId);

        _gameAdapter.CopyFearSoundClipsTo(soundEvent.ModelKey, _clipScratch);
        if (soundEvent.ClipIndex < 0
            || soundEvent.ClipIndex >= _clipScratch.Count
            || !TryResolveWorldPosition(soundEvent.ClientId, out Vector3 position))
        {
            return;
        }

        AudioClip clip = _clipScratch[soundEvent.ClipIndex];
        bool neededAudioLoad = clip.loadState == AudioDataLoadState.Unloaded;
        if (neededAudioLoad)
        {
            clip.LoadAudioData();
        }

        GameObject sourceObject = new GameObject($"Enhanced Spectator Fear Sound {soundEvent.ClientId}");
        sourceObject.transform.position = position;
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = Mathf.Max(0.1f, _config.FearSoundMinDistance.Value);
        source.maxDistance = Mathf.Max(source.minDistance + 0.1f, _config.FearSoundMaxDistance.Value);
        source.volume = Mathf.Clamp01(_config.FearSoundVolume.Value);
        source.clip = clip;
        source.Play();
        _activePlaybacks.Add(new ActivePlayback(
            soundEvent.ClientId,
            soundEvent.ModelKey,
            sourceObject,
            source,
            Time.unscaledTime,
            Time.unscaledTime + (neededAudioLoad ? 1.5f : 0.15f)));
        if (_gameAdapter.TryGetLocalDeadPlayerIdentity(out ulong localClientId, out _)
            && soundEvent.ClientId == localClientId)
        {
            _localIntentModelKey = soundEvent.ModelKey;
            _localIntentPlaying = true;
        }

        ModLog.Info(
            $"Fear sound playing: client={soundEvent.ClientId}, model={soundEvent.ModelKey}, clip={clip.name}, index={soundEvent.ClipIndex + 1}/{_clipScratch.Count}, duration={clip.length:0.###}s, spatialMax={source.maxDistance:0.#}m.");
    }

    private void ApplyNearbyPlayerLimit()
    {
        int maximumAudiblePlayers = Math.Max(0, _config.FearSoundMaxNearbyPlayers.Value);
        if (maximumAudiblePlayers == 0
            || maximumAudiblePlayers >= _activePlaybacks.Count
            || !_gameAdapter.TryGetActiveAudioListenerPosition(out Vector3 listenerPosition))
        {
            for (int index = 0; index < _activePlaybacks.Count; index++)
            {
                _activePlaybacks[index].Source.mute = false;
            }

            return;
        }

        for (int index = 0; index < _activePlaybacks.Count; index++)
        {
            ActivePlayback candidate = _activePlaybacks[index];
            float candidateDistance = (candidate.Source.transform.position - listenerPosition).sqrMagnitude;
            int closerPlayers = 0;
            for (int otherIndex = 0; otherIndex < _activePlaybacks.Count; otherIndex++)
            {
                if (otherIndex == index)
                {
                    continue;
                }

                ActivePlayback other = _activePlaybacks[otherIndex];
                float otherDistance = (other.Source.transform.position - listenerPosition).sqrMagnitude;
                if (otherDistance < candidateDistance
                    || (Mathf.Approximately(otherDistance, candidateDistance)
                        && other.ClientId < candidate.ClientId))
                {
                    closerPlayers++;
                }
            }

            candidate.Source.mute = !FearSoundAudibilityRules.IsAudible(
                closerPlayers,
                maximumAudiblePlayers);
        }
    }

    private void StopPlayback(ulong clientId)
    {
        for (int index = _activePlaybacks.Count - 1; index >= 0; index--)
        {
            ActivePlayback playback = _activePlaybacks[index];
            if (playback.ClientId != clientId)
            {
                continue;
            }

            playback.Dispose();
            _activePlaybacks.RemoveAt(index);
        }

        ClearLocalIntentIfNeeded(clientId);
    }

    private void StopAllPlaybacks()
    {
        for (int index = 0; index < _activePlaybacks.Count; index++)
        {
            _activePlaybacks[index].Dispose();
        }

        _activePlaybacks.Clear();
    }

    private void ClearLocalIntentIfNeeded(ulong clientId)
    {
        if (_gameAdapter.TryGetLocalDeadPlayerIdentity(out ulong localClientId, out _)
            && clientId == localClientId)
        {
            _localIntentPlaying = false;
        }
    }

    private bool IsLocalClient(ulong clientId)
    {
        return _gameAdapter.TryGetLocalDeadPlayerIdentity(out ulong localClientId, out _)
            && clientId == localClientId;
    }

    private bool TryResolveWorldPosition(ulong clientId, out Vector3 position)
    {
        if (_gameAdapter.TryGetLocalDeadPlayerIdentity(out ulong localClientId, out _)
            && clientId == localClientId
            && _spectatorModule.TryGetCurrentSpectatorPose(out SpectatorPoseState localPose)
            && localPose.IsSpectating)
        {
            position = localPose.Position;
            return true;
        }

        if (_networkService.TryGetRemoteSpectatorPose(clientId, out SpectatorPoseState remotePose)
            && remotePose.IsSpectating)
        {
            _posePresentationService.Resolve(remotePose, out position, out _, out _);
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    private sealed class ActivePlayback : IDisposable
    {
        private readonly GameObject _gameObject;

        public ActivePlayback(
            ulong clientId,
            string modelKey,
            GameObject gameObject,
            AudioSource source,
            float startedAt,
            float startupGraceUntil)
        {
            ClientId = clientId;
            ModelKey = modelKey;
            _gameObject = gameObject;
            Source = source;
            StartedAt = startedAt;
            StartupGraceUntil = startupGraceUntil;
        }

        public ulong ClientId { get; }
        public string ModelKey { get; }
        public AudioSource Source { get; }
        public float StartedAt { get; }
        public float StartupGraceUntil { get; }
        public bool HasObservedPlaying { get; private set; }

        public void MarkObservedPlaying()
        {
            HasObservedPlaying = true;
        }

        public void Dispose()
        {
            if (_gameObject != null)
            {
                UnityEngine.Object.Destroy(_gameObject);
            }
        }
    }
}
