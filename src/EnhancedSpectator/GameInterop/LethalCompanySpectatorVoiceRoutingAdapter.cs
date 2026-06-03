using System.Collections.Generic;
using Dissonance;
using Dissonance.Integrations.Unity_NFGO;
using GameNetcodeStuff;
using EnhancedSpectator.Features.VoiceRouting;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Networking;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Applies default-off spectator voice playback changes through confirmed vanilla voice fields.
/// </summary>
public sealed class LethalCompanySpectatorVoiceRoutingAdapter : IGameSpectatorVoiceRoutingAdapter
{
    private readonly Dictionary<ulong, PlaybackSnapshot> _snapshots = new Dictionary<ulong, PlaybackSnapshot>();
    private readonly Dictionary<ulong, float> _nextPlaybackResolveTime = new Dictionary<ulong, float>();
    private readonly SpectatorVoicePlayerLookupCache<PlayerControllerB> _playerLookupCache =
        new SpectatorVoicePlayerLookupCache<PlayerControllerB>();
    private readonly SpectatorVoiceRouteDiagnosticLimiter _diagnosticLimiter = new SpectatorVoiceRouteDiagnosticLimiter();
    private readonly HashSet<ulong> _fallbackBindingLogged = new HashSet<ulong>();
    private readonly IEnhancedSpectatorNetworkService? _networkService;
    private readonly System.Func<bool> _debugEnabled;
    private PlayerControllerB[]? _cachedPlayerScripts;
    private int _playerLookupCacheFrame = -1;

    /// <summary>
    /// Creates a spectator voice routing adapter.
    /// </summary>
    public LethalCompanySpectatorVoiceRoutingAdapter(
        IEnhancedSpectatorNetworkService? networkService = null,
        System.Func<bool>? debugEnabled = null)
    {
        _networkService = networkService;
        _debugEnabled = debugEnabled ?? (() => false);
    }

    /// <inheritdoc />
    public bool TryGetLocalVoiceReceiverState(
        out bool hasLocalPlayer,
        out bool isLocalPlayerDead,
        out ulong localClientId,
        out ulong localPlayerSlotId)
    {
        PlayerControllerB? localPlayer = StartOfRound.Instance != null
            ? StartOfRound.Instance.localPlayerController
            : null;
        hasLocalPlayer = localPlayer != null;
        isLocalPlayerDead = localPlayer != null && localPlayer.isPlayerDead;
        localClientId = localPlayer != null ? localPlayer.actualClientId : 0;
        localPlayerSlotId = localPlayer != null ? localPlayer.playerClientId : 0;
        return hasLocalPlayer;
    }

    /// <inheritdoc />
    public bool TryApplySpectatorVoiceRoute(
        ulong spectatorClientId,
        ulong spectatorSlotId,
        SpectatorPoseState? poseState,
        SpectatorVoicePlaybackSettings settings,
        out string reason)
    {
        reason = string.Empty;
        StartOfRound round = StartOfRound.Instance;
        if (round == null || round.localPlayerController == null)
        {
            reason = "round or local player unavailable";
            return false;
        }

        if (round.voiceChatModule != null && round.voiceChatModule.IsDeafened)
        {
            reason = "local player is deafened";
            return false;
        }

        PlayerControllerB? spectator = FindPlayer(round, spectatorClientId, spectatorSlotId);
        if (spectator == null)
        {
            reason = "spectator player object unavailable";
            return false;
        }

        if (spectator == round.localPlayerController)
        {
            reason = "spectator is local player";
            return false;
        }

        if (!spectator.isPlayerDead)
        {
            reason = "spectator is not dead";
            return false;
        }

        PeerIdentityState? identity = TryGetRemotePeerIdentity(spectatorClientId);
        if (!EnsureVoicePlayback(round, spectator, identity))
        {
            reason = "voice playback objects unavailable";
            return false;
        }

        if (spectator.voicePlayerState.IsLocallyMuted)
        {
            reason = "spectator is locally muted";
            return false;
        }

        if (settings.UseRemotePosePosition)
        {
            if (poseState == null || !poseState.IsSpectating)
            {
                if (!settings.FallbackTo2DWhenPoseMissing)
                {
                    reason = "remote spectator pose unavailable";
                    return false;
                }

                CaptureSnapshotIfNeeded(spectator);
                VoiceListenerFrame fallbackListenerFrame = ResolveVoiceListenerFrame(round);
                Apply2DPlayback(spectator, Mathf.Clamp01(settings.Volume));
                MaybeLogRouteApply(
                    spectatorClientId,
                    poseAvailable: false,
                    fallbackTo2D: true,
                    sourcePosition: spectator.currentVoiceChatAudioSource.transform.position,
                    listenerPosition: fallbackListenerFrame.ActualPosition,
                    remotePosePosition: spectator.currentVoiceChatAudioSource.transform.position,
                    desiredListenerPosition: fallbackListenerFrame.DesiredPosition,
                    distance: 0f,
                    finalVolume: spectator.voicePlayerState.Volume,
                    spatialBlend: spectator.currentVoiceChatAudioSource.spatialBlend,
                    set2D: spectator.currentVoiceChatIngameSettings.set2D,
                    remapped: false);
                return true;
            }

            CaptureSnapshotIfNeeded(spectator);
            Vector3 remotePosePosition = poseState.Position;
            VoiceListenerFrame listenerFrame = ResolveVoiceListenerFrame(round);
            Vector3 playbackPosition = SpectatorVoiceSpatializationRules.ResolvePlaybackSourcePosition(
                remotePosePosition,
                listenerFrame.DesiredPosition,
                listenerFrame.DesiredRotation,
                listenerFrame.ActualPosition,
                listenerFrame.ActualRotation);
            float distance = Vector3.Distance(listenerFrame.ActualPosition, playbackPosition);
            spectator.currentVoiceChatAudioSource.transform.position = playbackPosition;
            spectator.currentVoiceChatAudioSource.spatialBlend = 1f;
            spectator.currentVoiceChatAudioSource.panStereo = 0f;
            spectator.currentVoiceChatIngameSettings.set2D = false;
            spectator.voicePlayerState.Volume = SpectatorVoiceDistanceAttenuation.CalculateVolume(
                settings.Volume,
                settings.EnableDistanceAttenuation,
                distance,
                settings.MinDistance,
                settings.MaxDistance,
                settings.RolloffPower,
                settings.MinimumVolume);
            MaybeLogRouteApply(
                spectatorClientId,
                poseAvailable: true,
                fallbackTo2D: false,
                playbackPosition,
                listenerFrame.ActualPosition,
                remotePosePosition,
                listenerFrame.DesiredPosition,
                distance,
                spectator.voicePlayerState.Volume,
                spectator.currentVoiceChatAudioSource.spatialBlend,
                spectator.currentVoiceChatIngameSettings.set2D,
                listenerFrame.IsRemapped);
        }
        else
        {
            CaptureSnapshotIfNeeded(spectator);
            VoiceListenerFrame listenerFrame = ResolveVoiceListenerFrame(round);
            Apply2DPlayback(spectator, Mathf.Clamp01(settings.Volume));
            MaybeLogRouteApply(
                spectatorClientId,
                poseAvailable: poseState != null && poseState.IsSpectating,
                fallbackTo2D: false,
                sourcePosition: spectator.currentVoiceChatAudioSource.transform.position,
                listenerPosition: listenerFrame.ActualPosition,
                remotePosePosition: spectator.currentVoiceChatAudioSource.transform.position,
                desiredListenerPosition: listenerFrame.DesiredPosition,
                distance: 0f,
                finalVolume: spectator.voicePlayerState.Volume,
                spatialBlend: spectator.currentVoiceChatAudioSource.spatialBlend,
                set2D: spectator.currentVoiceChatIngameSettings.set2D,
                remapped: false);
        }

        return true;
    }

    /// <inheritdoc />
    public void ClearSpectatorVoiceRoute(ulong spectatorClientId, ulong spectatorSlotId)
    {
        StartOfRound round = StartOfRound.Instance;
        if (round == null || round.localPlayerController == null)
        {
            return;
        }

        PlayerControllerB? spectator = FindPlayer(round, spectatorClientId, spectatorSlotId);
        if (spectator == null || spectator == round.localPlayerController)
        {
            return;
        }

        PeerIdentityState? identity = TryGetRemotePeerIdentity(spectatorClientId);
        if (!EnsureVoicePlayback(round, spectator, identity))
        {
            return;
        }

        if (_snapshots.TryGetValue(spectator.actualClientId, out PlaybackSnapshot snapshot)
            || _snapshots.TryGetValue(spectator.playerClientId, out snapshot))
        {
            RestoreSnapshot(spectator, snapshot);
            _snapshots.Remove(spectator.actualClientId);
            _snapshots.Remove(spectator.playerClientId);
            _diagnosticLimiter.Clear(spectator.actualClientId);
            _diagnosticLimiter.Clear(spectator.playerClientId);
            _fallbackBindingLogged.Remove(spectator.actualClientId);
            _fallbackBindingLogged.Remove(spectator.playerClientId);
            _nextPlaybackResolveTime.Remove(spectator.actualClientId);
            _nextPlaybackResolveTime.Remove(spectator.playerClientId);
            return;
        }

        if (!spectator.isPlayerDead || round.localPlayerController.isPlayerDead)
        {
            return;
        }

        spectator.voicePlayerState.Volume = 0f;
        spectator.currentVoiceChatAudioSource.spatialBlend = 1f;
        spectator.currentVoiceChatIngameSettings.set2D = false;
        _diagnosticLimiter.Clear(spectatorClientId);
        _diagnosticLimiter.Clear(spectatorSlotId);
        _fallbackBindingLogged.Remove(spectatorClientId);
        _fallbackBindingLogged.Remove(spectatorSlotId);
        _nextPlaybackResolveTime.Remove(spectatorClientId);
        _nextPlaybackResolveTime.Remove(spectatorSlotId);
    }

    /// <inheritdoc />
    public void ClearCachedVoiceRouteLookups()
    {
        ClearPlayerLookupCache();
    }

    private bool EnsureVoicePlayback(StartOfRound round, PlayerControllerB player, PeerIdentityState? identity)
    {
        if (player.voicePlayerState != null
            && player.currentVoiceChatAudioSource != null
            && player.currentVoiceChatIngameSettings != null)
        {
            _nextPlaybackResolveTime.Remove(player.actualClientId);
            return true;
        }

        if (_nextPlaybackResolveTime.TryGetValue(player.actualClientId, out float nextResolveTime)
            && Time.unscaledTime < nextResolveTime)
        {
            return false;
        }

        round.RefreshPlayerVoicePlaybackObjects();
        if (player.voicePlayerState != null
            && player.currentVoiceChatAudioSource != null
            && player.currentVoiceChatIngameSettings != null)
        {
            _nextPlaybackResolveTime.Remove(player.actualClientId);
            return true;
        }

        if (TryBindVoicePlaybackFromIdentity(round, player, identity))
        {
            _nextPlaybackResolveTime.Remove(player.actualClientId);
            return true;
        }

        _nextPlaybackResolveTime[player.actualClientId] = Time.unscaledTime + 0.5f;
        return false;
    }

    private PeerIdentityState? TryGetRemotePeerIdentity(ulong clientId)
    {
        if (_networkService != null && _networkService.TryGetRemotePeerIdentity(clientId, out PeerIdentityState identity))
        {
            return identity;
        }

        return null;
    }

    private bool TryBindVoicePlaybackFromIdentity(StartOfRound round, PlayerControllerB player, PeerIdentityState? identity)
    {
        List<string> candidateNames = BuildVoicePlayerNameCandidates(player, identity);
        if (candidateNames.Count == 0)
        {
            return false;
        }

        PlayerVoiceIngameSettings[] voiceSettings = UnityEngine.Object.FindObjectsOfType<PlayerVoiceIngameSettings>(true);
        foreach (PlayerVoiceIngameSettings setting in voiceSettings)
        {
            if (setting == null)
            {
                continue;
            }

            setting.FindPlayerIfNull();
            if (!IsMatchingVoicePlayback(setting, candidateNames))
            {
                continue;
            }

            VoicePlayerState? voiceState = setting._playerState != null
                ? setting._playerState
                : TryFindVoicePlayerState(round, candidateNames);
            AudioSource? voiceAudio = setting.voiceAudio;
            if (voiceState == null || voiceAudio == null)
            {
                continue;
            }

            if (IsVoicePlaybackAlreadyAssigned(round, player, setting, voiceAudio, voiceState))
            {
                continue;
            }

            player.voicePlayerState = voiceState;
            player.currentVoiceChatAudioSource = voiceAudio;
            player.currentVoiceChatIngameSettings = setting;
            setting._playerState = voiceState;
            ApplyVoiceMixer(player, voiceAudio);
            MaybeLogFallbackBinding(player, setting, candidateNames[0]);
            return true;
        }

        return false;
    }

    private static List<string> BuildVoicePlayerNameCandidates(PlayerControllerB player, PeerIdentityState? identity)
    {
        List<string> candidates = new List<string>();
        AddCandidate(candidates, identity != null ? identity.VoicePlayerName : string.Empty);
        AddCandidate(candidates, TryGetNfgoPlayerId(player));
        AddCandidate(candidates, player.voicePlayerState != null ? player.voicePlayerState.Name : string.Empty);
        return candidates;
    }

    private static void AddCandidate(List<string> candidates, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string trimmed = value.Trim();
        foreach (string candidate in candidates)
        {
            if (string.Equals(candidate, trimmed, System.StringComparison.Ordinal))
            {
                return;
            }
        }

        candidates.Add(trimmed);
    }

    private static string TryGetNfgoPlayerId(PlayerControllerB player)
    {
        NfgoPlayer? nfgoPlayer = player.gameObject != null
            ? player.gameObject.GetComponentInChildren<NfgoPlayer>()
            : null;
        return nfgoPlayer != null ? nfgoPlayer.PlayerId : string.Empty;
    }

    private static bool IsMatchingVoicePlayback(PlayerVoiceIngameSettings setting, List<string> candidateNames)
    {
        string stateName = setting._playerState != null ? setting._playerState.Name : string.Empty;
        string playbackName = setting._playbackComponent != null ? setting._playbackComponent.PlayerName : string.Empty;
        foreach (string candidate in candidateNames)
        {
            if (string.Equals(stateName, candidate, System.StringComparison.Ordinal)
                || string.Equals(playbackName, candidate, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static VoicePlayerState? TryFindVoicePlayerState(StartOfRound round, List<string> candidateNames)
    {
        if (round.voiceChatModule == null)
        {
            return null;
        }

        foreach (string candidate in candidateNames)
        {
            VoicePlayerState voiceState = round.voiceChatModule.FindPlayer(candidate);
            if (voiceState != null)
            {
                return voiceState;
            }
        }

        return null;
    }

    private static bool IsVoicePlaybackAlreadyAssigned(
        StartOfRound round,
        PlayerControllerB targetPlayer,
        PlayerVoiceIngameSettings setting,
        AudioSource voiceAudio,
        VoicePlayerState voiceState)
    {
        if (round.allPlayerScripts == null)
        {
            return false;
        }

        foreach (PlayerControllerB player in round.allPlayerScripts)
        {
            if (player == null || player == targetPlayer)
            {
                continue;
            }

            if (player.currentVoiceChatIngameSettings == setting
                || player.currentVoiceChatAudioSource == voiceAudio
                || player.voicePlayerState == voiceState)
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyVoiceMixer(PlayerControllerB player, AudioSource voiceAudio)
    {
        SoundManager soundManager = SoundManager.Instance;
        if (soundManager == null
            || soundManager.playerVoiceMixers == null
            || player.playerClientId >= (ulong)soundManager.playerVoiceMixers.Length)
        {
            return;
        }

        voiceAudio.outputAudioMixerGroup = soundManager.playerVoiceMixers[(int)player.playerClientId];
    }

    private void MaybeLogFallbackBinding(
        PlayerControllerB player,
        PlayerVoiceIngameSettings setting,
        string matchedVoiceName)
    {
        if (!_debugEnabled() || !_fallbackBindingLogged.Add(player.actualClientId))
        {
            return;
        }

        string playbackName = setting._playbackComponent != null ? setting._playbackComponent.PlayerName : "none";
        string voiceStateName = setting._playerState != null ? setting._playerState.Name : "none";
        ModLog.Debug(
            $"Spectator voice playback fallback bound: client={player.actualClientId}, slot={player.playerClientId}, matchedVoiceName={FormatVoiceNameForLog(matchedVoiceName)}, playbackName={FormatVoiceNameForLog(playbackName)}, voiceStateName={FormatVoiceNameForLog(voiceStateName)}.");
    }

    private static string FormatVoiceNameForLog(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "none" : "present";
    }

    private PlayerControllerB? FindPlayer(StartOfRound round, ulong clientId, ulong slotId)
    {
        PlayerControllerB[]? players = round.allPlayerScripts;
        if (players == null)
        {
            ClearPlayerLookupCache();
            return null;
        }

        int frame = Time.frameCount;
        if (_playerLookupCacheFrame != frame || _cachedPlayerScripts != players)
        {
            RebuildPlayerLookupCache(players, frame);
        }

        return _playerLookupCache.TryGet(clientId, slotId, out PlayerControllerB player) ? player : null;
    }

    private void RebuildPlayerLookupCache(PlayerControllerB[] players, int frame)
    {
        _playerLookupCache.Clear();
        _cachedPlayerScripts = players;
        _playerLookupCacheFrame = frame;
        for (int index = 0; index < players.Length; index++)
        {
            PlayerControllerB player = players[index];
            if (player == null)
            {
                continue;
            }

            _playerLookupCache.Store(player, player.actualClientId, player.playerClientId);
        }
    }

    private void ClearPlayerLookupCache()
    {
        _playerLookupCache.Clear();
        _cachedPlayerScripts = null;
        _playerLookupCacheFrame = -1;
    }

    private static VoiceListenerFrame ResolveVoiceListenerFrame(StartOfRound round)
    {
        Transform? desiredTransform = TryGetDesiredListenerTransform(round);
        Transform? actualTransform = TryGetActualAudioListenerTransform(round);

        if (desiredTransform == null && actualTransform == null)
        {
            PlayerControllerB? localPlayer = round.localPlayerController;
            Vector3 fallbackPosition = localPlayer != null ? localPlayer.transform.position : Vector3.zero;
            Quaternion fallbackRotation = localPlayer != null ? localPlayer.transform.rotation : Quaternion.identity;
            return new VoiceListenerFrame(fallbackPosition, fallbackRotation, fallbackPosition, fallbackRotation);
        }

        desiredTransform ??= actualTransform;
        actualTransform ??= desiredTransform;
        return new VoiceListenerFrame(
            desiredTransform!.position,
            desiredTransform.rotation,
            actualTransform!.position,
            actualTransform.rotation);
    }

    private static Transform? TryGetDesiredListenerTransform(StartOfRound round)
    {
        PlayerControllerB? localPlayer = round.localPlayerController;
        if (localPlayer != null
            && localPlayer.isPlayerDead
            && round.spectateCamera != null
            && round.spectateCamera.enabled)
        {
            return round.spectateCamera.transform;
        }

        Camera? activeCamera = round.activeCamera != null ? round.activeCamera : Camera.main;
        return activeCamera != null ? activeCamera.transform : localPlayer != null ? localPlayer.transform : null;
    }

    private static Transform? TryGetActualAudioListenerTransform(StartOfRound round)
    {
        if (round.audioListener != null && round.audioListener.enabled)
        {
            return round.audioListener.transform;
        }

        PlayerControllerB? localPlayer = round.localPlayerController;
        if (localPlayer != null && localPlayer.activeAudioListener != null && localPlayer.activeAudioListener.enabled)
        {
            return localPlayer.activeAudioListener.transform;
        }

        return null;
    }

    private static void Apply2DPlayback(PlayerControllerB spectator, float volume)
    {
        spectator.voicePlayerState.Volume = volume;
        spectator.currentVoiceChatAudioSource.spatialBlend = 0f;
        spectator.currentVoiceChatAudioSource.panStereo = 0f;
        spectator.currentVoiceChatIngameSettings.set2D = true;
    }

    private void CaptureSnapshotIfNeeded(PlayerControllerB spectator)
    {
        ulong key = spectator.actualClientId;
        if (_snapshots.ContainsKey(key))
        {
            return;
        }

        _snapshots[key] = new PlaybackSnapshot(
            spectator.voicePlayerState.Volume,
            spectator.currentVoiceChatAudioSource.spatialBlend,
            spectator.currentVoiceChatAudioSource.panStereo,
            spectator.currentVoiceChatIngameSettings.set2D,
            spectator.currentVoiceChatAudioSource.transform.position);
    }

    private void MaybeLogRouteApply(
        ulong spectatorClientId,
        bool poseAvailable,
        bool fallbackTo2D,
        Vector3 sourcePosition,
        Vector3 listenerPosition,
        Vector3 remotePosePosition,
        Vector3 desiredListenerPosition,
        float distance,
        float finalVolume,
        float spatialBlend,
        bool set2D,
        bool remapped)
    {
        if (!_debugEnabled()
            || !_diagnosticLimiter.ShouldLog(
                spectatorClientId,
                Time.frameCount,
                poseAvailable,
                fallbackTo2D,
                distance,
                finalVolume,
                spatialBlend,
                set2D))
        {
            return;
        }

        string mode = spatialBlend >= 0.5f && !set2D ? "3D" : "2D";
        ModLog.Debug(
            $"Spectator voice route apply: spectatorClient={spectatorClientId}, mode={mode}, poseAvailable={poseAvailable}, fallbackTo2D={fallbackTo2D}, source={FormatVector(sourcePosition)}, listener={FormatVector(listenerPosition)}, remoteSource={FormatVector(remotePosePosition)}, desiredListener={FormatVector(desiredListenerPosition)}, remapped={remapped}, distance={distance:F2}, finalVolume={finalVolume:F2}, spatialBlend={spatialBlend:F2}, set2D={set2D}.");
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
    }

    private static void RestoreSnapshot(PlayerControllerB spectator, PlaybackSnapshot snapshot)
    {
        if (spectator.voicePlayerState != null)
        {
            spectator.voicePlayerState.Volume = snapshot.Volume;
        }

        if (spectator.currentVoiceChatAudioSource != null)
        {
            spectator.currentVoiceChatAudioSource.spatialBlend = snapshot.SpatialBlend;
            spectator.currentVoiceChatAudioSource.panStereo = snapshot.PanStereo;
            spectator.currentVoiceChatAudioSource.transform.position = snapshot.Position;
        }

        if (spectator.currentVoiceChatIngameSettings != null)
        {
            spectator.currentVoiceChatIngameSettings.set2D = snapshot.Set2D;
        }
    }

    private readonly struct PlaybackSnapshot
    {
        public PlaybackSnapshot(float volume, float spatialBlend, float panStereo, bool set2D, Vector3 position)
        {
            Volume = volume;
            SpatialBlend = spatialBlend;
            PanStereo = panStereo;
            Set2D = set2D;
            Position = position;
        }

        public float Volume { get; }

        public float SpatialBlend { get; }

        public float PanStereo { get; }

        public bool Set2D { get; }

        public Vector3 Position { get; }
    }

    private readonly struct VoiceListenerFrame
    {
        public VoiceListenerFrame(
            Vector3 desiredPosition,
            Quaternion desiredRotation,
            Vector3 actualPosition,
            Quaternion actualRotation)
        {
            DesiredPosition = desiredPosition;
            DesiredRotation = desiredRotation;
            ActualPosition = actualPosition;
            ActualRotation = actualRotation;
        }

        public Vector3 DesiredPosition { get; }

        public Quaternion DesiredRotation { get; }

        public Vector3 ActualPosition { get; }

        public Quaternion ActualRotation { get; }

        public bool IsRemapped =>
            (DesiredPosition - ActualPosition).sqrMagnitude > 0.0001f
            || Quaternion.Dot(DesiredRotation, ActualRotation) < 0.9995f;
    }
}
