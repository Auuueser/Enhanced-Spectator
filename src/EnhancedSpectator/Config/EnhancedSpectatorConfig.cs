using BepInEx.Configuration;
using EnhancedSpectator.Features.FloatingHead;
using UnityEngine;

namespace EnhancedSpectator.Config;

/// <summary>
/// Owns all BepInEx configuration entries for Enhanced Spectator.
/// </summary>
public sealed class EnhancedSpectatorConfig
{
    private EnhancedSpectatorConfig(
        ConfigEntry<bool> enableSpectatorModule,
        ConfigEntry<bool> enableEnhancedSpectator,
        ConfigEntry<bool> enableFreecam,
        ConfigEntry<bool> freecamDefaultOn,
        ConfigEntry<float> freecamRadius,
        ConfigEntry<float> freecamMoveSpeed,
        ConfigEntry<float> freecamFastMoveMultiplier,
        ConfigEntry<float> freecamSlowMoveMultiplier,
        ConfigEntry<float> freecamLookSensitivity,
        ConfigEntry<float> freecamSmoothTime,
        ConfigEntry<bool> clampCameraToRadius,
        ConfigEntry<bool> recenterOnTargetSwitch,
        ConfigEntry<bool> disableDuringGameOverOverride,
        ConfigEntry<KeyCode> toggleFreecamKey,
        ConfigEntry<KeyCode> recenterKey,
        ConfigEntry<KeyCode> resetToVanillaViewKey,
        ConfigEntry<KeyCode> fastMoveKey,
        ConfigEntry<KeyCode> slowMoveKey,
        ConfigEntry<KeyCode> ascendKey,
        ConfigEntry<KeyCode> descendKey,
        ConfigEntry<bool> enableDebugLogging,
        ConfigEntry<bool> enableNetworking,
        ConfigEntry<bool> enableCapabilityHandshake,
        ConfigEntry<bool> enableSpectatorTargetSync,
        ConfigEntry<bool> enableSpectatorPoseSync,
        ConfigEntry<bool> enableHostRelay,
        ConfigEntry<float> spectatorPoseSyncInterval,
        ConfigEntry<bool> enableVoiceActivitySync,
        ConfigEntry<float> voiceActivitySyncInterval,
        ConfigEntry<float> voiceActivityStaleSeconds,
        ConfigEntry<bool> debugVoiceActivitySync,
        ConfigEntry<bool> enableSpectatorVoiceToTarget,
        ConfigEntry<SpectatorVoiceAudienceMode> spectatorVoiceAudienceMode,
        ConfigEntry<float> spectatorVoiceToTargetVolume,
        ConfigEntry<bool> spectatorVoiceUseRemotePosePosition,
        ConfigEntry<bool> spectatorVoiceEnableDistanceAttenuation,
        ConfigEntry<float> spectatorVoiceMinDistance,
        ConfigEntry<float> spectatorVoiceMaxDistance,
        ConfigEntry<float> spectatorVoiceRolloffPower,
        ConfigEntry<float> spectatorVoiceMinimumVolume,
        ConfigEntry<bool> spectatorVoiceFallbackTo2DWhenPoseMissing,
        ConfigEntry<bool> debugSpectatorVoiceRouting,
        ConfigEntry<bool> repairVanillaConnectedPlayerState,
        ConfigEntry<bool> repairVanillaPlayerNames,
        ConfigEntry<bool> debugPlayerStateRepair,
        ConfigEntry<bool> debugNetworkMessages,
        ConfigEntry<bool> debugPoseMessages,
        ConfigEntry<bool> enableSpectatorPresenceDebug,
        ConfigEntry<bool> debugLogPresenceChanges,
        ConfigEntry<bool> enableModelInspection,
        ConfigEntry<bool> logLocalPlayerModelOnKey,
        ConfigEntry<bool> logRemotePlayerModelsOnKey,
        ConfigEntry<KeyCode> modelInspectionKey,
        ConfigEntry<bool> includeRendererBounds,
        ConfigEntry<bool> includeMaterials,
        ConfigEntry<int> maxTransformDepth,
        ConfigEntry<bool> enableRuntimeHeadSourceInspection,
        ConfigEntry<KeyCode> runtimeHeadSourceInspectionKey,
        ConfigEntry<bool> runtimeHeadSourceIncludeRendererBounds,
        ConfigEntry<bool> runtimeHeadSourceIncludeMaterials,
        ConfigEntry<int> runtimeHeadSourceMaxTransformDepth,
        ConfigEntry<bool> enableVoiceDiagnostics,
        ConfigEntry<KeyCode> voiceDiagnosticsKey,
        ConfigEntry<bool> logLocalVoiceStateOnKey,
        ConfigEntry<bool> logRemoteVoiceStatesOnKey,
        ConfigEntry<bool> includeVoiceAudioSourceDetails,
        ConfigEntry<bool> includeWalkieVoiceDiagnostics,
        ConfigEntry<bool> enableFloatingHeadVisuals,
        ConfigEntry<bool> enablePlaceholderVisuals,
        ConfigEntry<bool> useRuntimeDetachedHeadVisuals,
        ConfigEntry<float> runtimeDetachedHeadScale,
        ConfigEntry<float> runtimeDetachedHeadPitchOffset,
        ConfigEntry<float> runtimeDetachedHeadYawOffset,
        ConfigEntry<float> runtimeDetachedHeadRollOffset,
        ConfigEntry<bool> fallbackToPlaceholderWhenDetachedHeadUnavailable,
        ConfigEntry<bool> showRemoteSpectators,
        ConfigEntry<bool> showOnlySpectatorsWatchingMe,
        ConfigEntry<bool> showDeadSpectatorsToAlivePlayers,
        ConfigEntry<bool> showDeadSpectatorsToDeadPlayers,
        ConfigEntry<int> maxFloatingHeadsVisible,
        ConfigEntry<FloatingHeadVisualStyle> visualStyle,
        ConfigEntry<float> placeholderScale,
        ConfigEntry<float> billboardSize,
        ConfigEntry<float> baseAlpha,
        ConfigEntry<bool> useUnlitMaterial,
        ConfigEntry<bool> enableDepthTest,
        ConfigEntry<float> floatingHeadRingRadius,
        ConfigEntry<float> floatingHeadHeightOffset,
        ConfigEntry<bool> useCameraVisiblePlacement,
        ConfigEntry<float> cameraForwardOffset,
        ConfigEntry<float> remotePoseSmoothTime,
        ConfigEntry<bool> keepRemotePoseInView,
        ConfigEntry<float> remotePoseVisibleProxyDistance,
        ConfigEntry<bool> enableScreenFallbackVisual,
        ConfigEntry<float> screenFallbackSize,
        ConfigEntry<float> presenceLostGraceSeconds,
        ConfigEntry<bool> floatingHeadFaceCamera,
        ConfigEntry<bool> pulseWhenSpeaking,
        ConfigEntry<float> speakingScaleMultiplier,
        ConfigEntry<float> speakingPulseSpeed,
        ConfigEntry<float> minimumSpeakingVoiceLevel,
        ConfigEntry<float> speakingPulseAmount,
        ConfigEntry<float> voiceAttackSmoothTime,
        ConfigEntry<float> voiceReleaseSmoothTime,
        ConfigEntry<float> silenceScaleMultiplier,
        ConfigEntry<float> amplitudeSmoothing,
        ConfigEntry<bool> destroyOnPresenceLost,
        ConfigEntry<bool> debugVisualLifecycle,
        ConfigEntry<bool> showNameTags,
        ConfigEntry<float> nameTagScale,
        ConfigEntry<float> nameTagHeightOffset,
        ConfigEntry<float> nameTagMaxDistance,
        ConfigEntry<bool> nameTagUseGamePlayerNames,
        ConfigEntry<bool> nameTagUseFallbackIds,
        ConfigEntry<bool> debugNameTagLifecycle,
        ConfigEntry<EnhancedSpectatorLanguageMode> configLanguage,
        bool useChineseText,
        ConfigEntry<bool> enableThirdPerson,
        ConfigEntry<KeyCode> toggleThirdPersonKey,
        ConfigEntry<float> thirdPersonDistance,
        ConfigEntry<float> thirdPersonHeight,
        ConfigEntry<bool> enableFearModeAsHost,
        ConfigEntry<bool> renderFearModelsLocally,
        ConfigEntry<bool> showFearModeQuickMenu,
        ConfigEntry<float> fearModelTargetHeight,
        ConfigEntry<bool> fearModelUseOriginalScale,
        ConfigEntry<float> fearModelScaleMultiplier,
        ConfigEntry<KeyCode> fearModelPreviousKey,
        ConfigEntry<KeyCode> fearModelNextKey,
        ConfigEntry<KeyCode> fearSoundKey,
        ConfigEntry<KeyCode> fearSoundNextKey,
        ConfigEntry<float> fearSoundVolume,
        ConfigEntry<float> fearSoundMinDistance,
        ConfigEntry<float> fearSoundMaxDistance,
        ConfigEntry<float> fearSoundCooldownSeconds,
        ConfigEntry<int> fearSoundMaxNearbyPlayers)
    {
        EnableSpectatorModule = enableSpectatorModule;
        EnableEnhancedSpectator = enableEnhancedSpectator;
        EnableFreecam = enableFreecam;
        FreecamDefaultOn = freecamDefaultOn;
        FreecamRadius = freecamRadius;
        FreecamMoveSpeed = freecamMoveSpeed;
        FreecamFastMoveMultiplier = freecamFastMoveMultiplier;
        FreecamSlowMoveMultiplier = freecamSlowMoveMultiplier;
        FreecamLookSensitivity = freecamLookSensitivity;
        FreecamSmoothTime = freecamSmoothTime;
        ClampCameraToRadius = clampCameraToRadius;
        RecenterOnTargetSwitch = recenterOnTargetSwitch;
        DisableDuringGameOverOverride = disableDuringGameOverOverride;
        ToggleFreecamKey = toggleFreecamKey;
        RecenterKey = recenterKey;
        ResetToVanillaViewKey = resetToVanillaViewKey;
        FastMoveKey = fastMoveKey;
        SlowMoveKey = slowMoveKey;
        AscendKey = ascendKey;
        DescendKey = descendKey;
        EnableDebugLogging = enableDebugLogging;
        EnableNetworking = enableNetworking;
        EnableCapabilityHandshake = enableCapabilityHandshake;
        EnableSpectatorTargetSync = enableSpectatorTargetSync;
        EnableSpectatorPoseSync = enableSpectatorPoseSync;
        EnableHostRelay = enableHostRelay;
        SpectatorPoseSyncInterval = spectatorPoseSyncInterval;
        EnableVoiceActivitySync = enableVoiceActivitySync;
        VoiceActivitySyncInterval = voiceActivitySyncInterval;
        VoiceActivityStaleSeconds = voiceActivityStaleSeconds;
        DebugVoiceActivitySync = debugVoiceActivitySync;
        EnableSpectatorVoiceToTarget = enableSpectatorVoiceToTarget;
        SpectatorVoiceAudienceMode = spectatorVoiceAudienceMode;
        SpectatorVoiceToTargetVolume = spectatorVoiceToTargetVolume;
        SpectatorVoiceUseRemotePosePosition = spectatorVoiceUseRemotePosePosition;
        SpectatorVoiceEnableDistanceAttenuation = spectatorVoiceEnableDistanceAttenuation;
        SpectatorVoiceMinDistance = spectatorVoiceMinDistance;
        SpectatorVoiceMaxDistance = spectatorVoiceMaxDistance;
        SpectatorVoiceRolloffPower = spectatorVoiceRolloffPower;
        SpectatorVoiceMinimumVolume = spectatorVoiceMinimumVolume;
        SpectatorVoiceFallbackTo2DWhenPoseMissing = spectatorVoiceFallbackTo2DWhenPoseMissing;
        DebugSpectatorVoiceRouting = debugSpectatorVoiceRouting;
        RepairVanillaConnectedPlayerState = repairVanillaConnectedPlayerState;
        RepairVanillaPlayerNames = repairVanillaPlayerNames;
        DebugPlayerStateRepair = debugPlayerStateRepair;
        DebugNetworkMessages = debugNetworkMessages;
        DebugPoseMessages = debugPoseMessages;
        EnableSpectatorPresenceDebug = enableSpectatorPresenceDebug;
        DebugLogPresenceChanges = debugLogPresenceChanges;
        EnableModelInspection = enableModelInspection;
        LogLocalPlayerModelOnKey = logLocalPlayerModelOnKey;
        LogRemotePlayerModelsOnKey = logRemotePlayerModelsOnKey;
        ModelInspectionKey = modelInspectionKey;
        IncludeRendererBounds = includeRendererBounds;
        IncludeMaterials = includeMaterials;
        MaxTransformDepth = maxTransformDepth;
        EnableRuntimeHeadSourceInspection = enableRuntimeHeadSourceInspection;
        RuntimeHeadSourceInspectionKey = runtimeHeadSourceInspectionKey;
        RuntimeHeadSourceIncludeRendererBounds = runtimeHeadSourceIncludeRendererBounds;
        RuntimeHeadSourceIncludeMaterials = runtimeHeadSourceIncludeMaterials;
        RuntimeHeadSourceMaxTransformDepth = runtimeHeadSourceMaxTransformDepth;
        EnableVoiceDiagnostics = enableVoiceDiagnostics;
        VoiceDiagnosticsKey = voiceDiagnosticsKey;
        LogLocalVoiceStateOnKey = logLocalVoiceStateOnKey;
        LogRemoteVoiceStatesOnKey = logRemoteVoiceStatesOnKey;
        IncludeVoiceAudioSourceDetails = includeVoiceAudioSourceDetails;
        IncludeWalkieVoiceDiagnostics = includeWalkieVoiceDiagnostics;
        EnableFloatingHeadVisuals = enableFloatingHeadVisuals;
        EnablePlaceholderVisuals = enablePlaceholderVisuals;
        UseRuntimeDetachedHeadVisuals = useRuntimeDetachedHeadVisuals;
        RuntimeDetachedHeadScale = runtimeDetachedHeadScale;
        RuntimeDetachedHeadPitchOffset = runtimeDetachedHeadPitchOffset;
        RuntimeDetachedHeadYawOffset = runtimeDetachedHeadYawOffset;
        RuntimeDetachedHeadRollOffset = runtimeDetachedHeadRollOffset;
        FallbackToPlaceholderWhenDetachedHeadUnavailable = fallbackToPlaceholderWhenDetachedHeadUnavailable;
        ShowRemoteSpectators = showRemoteSpectators;
        ShowOnlySpectatorsWatchingMe = showOnlySpectatorsWatchingMe;
        ShowDeadSpectatorsToAlivePlayers = showDeadSpectatorsToAlivePlayers;
        ShowDeadSpectatorsToDeadPlayers = showDeadSpectatorsToDeadPlayers;
        MaxFloatingHeadsVisible = maxFloatingHeadsVisible;
        VisualStyle = visualStyle;
        PlaceholderScale = placeholderScale;
        BillboardSize = billboardSize;
        BaseAlpha = baseAlpha;
        UseUnlitMaterial = useUnlitMaterial;
        EnableDepthTest = enableDepthTest;
        FloatingHeadRingRadius = floatingHeadRingRadius;
        FloatingHeadHeightOffset = floatingHeadHeightOffset;
        UseCameraVisiblePlacement = useCameraVisiblePlacement;
        CameraForwardOffset = cameraForwardOffset;
        RemotePoseSmoothTime = remotePoseSmoothTime;
        KeepRemotePoseInView = keepRemotePoseInView;
        RemotePoseVisibleProxyDistance = remotePoseVisibleProxyDistance;
        EnableScreenFallbackVisual = enableScreenFallbackVisual;
        ScreenFallbackSize = screenFallbackSize;
        PresenceLostGraceSeconds = presenceLostGraceSeconds;
        FloatingHeadFaceCamera = floatingHeadFaceCamera;
        PulseWhenSpeaking = pulseWhenSpeaking;
        SpeakingScaleMultiplier = speakingScaleMultiplier;
        SpeakingPulseSpeed = speakingPulseSpeed;
        MinimumSpeakingVoiceLevel = minimumSpeakingVoiceLevel;
        SpeakingPulseAmount = speakingPulseAmount;
        VoiceAttackSmoothTime = voiceAttackSmoothTime;
        VoiceReleaseSmoothTime = voiceReleaseSmoothTime;
        SilenceScaleMultiplier = silenceScaleMultiplier;
        AmplitudeSmoothing = amplitudeSmoothing;
        DestroyOnPresenceLost = destroyOnPresenceLost;
        DebugVisualLifecycle = debugVisualLifecycle;
        ShowNameTags = showNameTags;
        NameTagScale = nameTagScale;
        NameTagHeightOffset = nameTagHeightOffset;
        NameTagMaxDistance = nameTagMaxDistance;
        NameTagUseGamePlayerNames = nameTagUseGamePlayerNames;
        NameTagUseFallbackIds = nameTagUseFallbackIds;
        DebugNameTagLifecycle = debugNameTagLifecycle;
        ConfigLanguage = configLanguage;
        UseChineseText = useChineseText;
        EnableThirdPerson = enableThirdPerson;
        ToggleThirdPersonKey = toggleThirdPersonKey;
        ThirdPersonDistance = thirdPersonDistance;
        ThirdPersonHeight = thirdPersonHeight;
        EnableFearModeAsHost = enableFearModeAsHost;
        RenderFearModelsLocally = renderFearModelsLocally;
        ShowFearModeQuickMenu = showFearModeQuickMenu;
        FearModelTargetHeight = fearModelTargetHeight;
        FearModelUseOriginalScale = fearModelUseOriginalScale;
        FearModelScaleMultiplier = fearModelScaleMultiplier;
        FearModelPreviousKey = fearModelPreviousKey;
        FearModelNextKey = fearModelNextKey;
        FearSoundKey = fearSoundKey;
        FearSoundNextKey = fearSoundNextKey;
        FearSoundVolume = fearSoundVolume;
        FearSoundMinDistance = fearSoundMinDistance;
        FearSoundMaxDistance = fearSoundMaxDistance;
        FearSoundCooldownSeconds = fearSoundCooldownSeconds;
        FearSoundMaxNearbyPlayers = fearSoundMaxNearbyPlayers;
    }

    /// <summary>
    /// Enables the spectator feature module.
    /// </summary>
    public ConfigEntry<bool> EnableSpectatorModule { get; }

    /// <summary>
    /// Enables all enhanced spectator behavior.
    /// </summary>
    public ConfigEntry<bool> EnableEnhancedSpectator { get; }

    /// <summary>
    /// Enables local spectator freecam behavior.
    /// </summary>
    public ConfigEntry<bool> EnableFreecam { get; }

    /// <summary>
    /// Enables freecam automatically after entering vanilla spectator state.
    /// </summary>
    public ConfigEntry<bool> FreecamDefaultOn { get; }

    /// <summary>
    /// Limits camera distance from the current target anchor.
    /// </summary>
    public ConfigEntry<float> FreecamRadius { get; }

    /// <summary>
    /// Controls freecam movement speed.
    /// </summary>
    public ConfigEntry<float> FreecamMoveSpeed { get; }

    /// <summary>
    /// Controls fast movement speed multiplier.
    /// </summary>
    public ConfigEntry<float> FreecamFastMoveMultiplier { get; }

    /// <summary>
    /// Controls slow movement speed multiplier.
    /// </summary>
    public ConfigEntry<float> FreecamSlowMoveMultiplier { get; }

    /// <summary>
    /// Controls mouse look sensitivity.
    /// </summary>
    public ConfigEntry<float> FreecamLookSensitivity { get; }

    /// <summary>
    /// Controls camera smoothing time. Set to zero to disable smoothing.
    /// </summary>
    public ConfigEntry<float> FreecamSmoothTime { get; }

    /// <summary>
    /// Enables clamping camera offset to the configured radius.
    /// </summary>
    public ConfigEntry<bool> ClampCameraToRadius { get; }

    /// <summary>
    /// Recenters freecam when vanilla target selection changes.
    /// </summary>
    public ConfigEntry<bool> RecenterOnTargetSwitch { get; }

    /// <summary>
    /// Disables enhanced freecam during vanilla game-over camera override.
    /// </summary>
    public ConfigEntry<bool> DisableDuringGameOverOverride { get; }

    /// <summary>
    /// Toggles enhanced freecam while spectating.
    /// </summary>
    public ConfigEntry<KeyCode> ToggleFreecamKey { get; }

    /// <summary>
    /// Recenters enhanced freecam around the current target.
    /// </summary>
    public ConfigEntry<KeyCode> RecenterKey { get; }

    /// <summary>
    /// Disables enhanced freecam and returns to vanilla spectator camera.
    /// </summary>
    public ConfigEntry<KeyCode> ResetToVanillaViewKey { get; }

    /// <summary>
    /// Fast movement modifier key.
    /// </summary>
    public ConfigEntry<KeyCode> FastMoveKey { get; }

    /// <summary>
    /// Slow movement modifier key.
    /// </summary>
    public ConfigEntry<KeyCode> SlowMoveKey { get; }

    /// <summary>
    /// Upward movement key.
    /// </summary>
    public ConfigEntry<KeyCode> AscendKey { get; }

    /// <summary>
    /// Downward movement key.
    /// </summary>
    public ConfigEntry<KeyCode> DescendKey { get; }

    /// <summary>
    /// Enables verbose debug logging.
    /// </summary>
    public ConfigEntry<bool> EnableDebugLogging { get; }

    /// <summary>
    /// Enables Enhanced Spectator networking modules.
    /// </summary>
    public ConfigEntry<bool> EnableNetworking { get; }

    /// <summary>
    /// Enables the mod capability handshake.
    /// </summary>
    public ConfigEntry<bool> EnableCapabilityHandshake { get; }

    /// <summary>
    /// Enables spectator target synchronization.
    /// </summary>
    public ConfigEntry<bool> EnableSpectatorTargetSync { get; }

    /// <summary>
    /// Enables spectator camera pose synchronization.
    /// </summary>
    public ConfigEntry<bool> EnableSpectatorPoseSync { get; }

    /// <summary>
    /// Enables host-mediated relay for client-origin spectator state.
    /// </summary>
    public ConfigEntry<bool> EnableHostRelay { get; }

    /// <summary>
    /// Controls the minimum interval between spectator pose messages.
    /// </summary>
    public ConfigEntry<float> SpectatorPoseSyncInterval { get; }

    /// <summary>
    /// Enables visual-only voice activity synchronization for floating-head scaling.
    /// </summary>
    public ConfigEntry<bool> EnableVoiceActivitySync { get; }

    /// <summary>
    /// Controls the minimum interval between voice activity visual messages.
    /// </summary>
    public ConfigEntry<float> VoiceActivitySyncInterval { get; }

    /// <summary>
    /// Controls how long received voice activity can drive visuals without a refresh.
    /// </summary>
    public ConfigEntry<float> VoiceActivityStaleSeconds { get; }

    /// <summary>
    /// Enables verbose voice activity sync diagnostics.
    /// </summary>
    public ConfigEntry<bool> DebugVoiceActivitySync { get; }

    /// <summary>
    /// Enables the experimental spectator-to-target voice route.
    /// </summary>
    public ConfigEntry<bool> EnableSpectatorVoiceToTarget { get; }

    /// <summary>
    /// Controls which compatible modded players can hear routed dead spectator voice.
    /// </summary>
    public ConfigEntry<SpectatorVoiceAudienceMode> SpectatorVoiceAudienceMode { get; }

    /// <summary>
    /// Controls routed spectator voice playback volume.
    /// </summary>
    public ConfigEntry<float> SpectatorVoiceToTargetVolume { get; }

    /// <summary>
    /// Uses synced spectator camera pose for routed voice position.
    /// </summary>
    public ConfigEntry<bool> SpectatorVoiceUseRemotePosePosition { get; }

    /// <summary>
    /// Enables local volume attenuation by distance from the synced spectator pose.
    /// </summary>
    public ConfigEntry<bool> SpectatorVoiceEnableDistanceAttenuation { get; }

    /// <summary>
    /// Keeps full routed spectator voice volume within this distance.
    /// </summary>
    public ConfigEntry<float> SpectatorVoiceMinDistance { get; }

    /// <summary>
    /// Reaches the minimum routed spectator voice volume at this distance.
    /// </summary>
    public ConfigEntry<float> SpectatorVoiceMaxDistance { get; }

    /// <summary>
    /// Controls the routed spectator voice distance attenuation curve.
    /// </summary>
    public ConfigEntry<float> SpectatorVoiceRolloffPower { get; }

    /// <summary>
    /// Controls the minimum routed spectator voice volume multiplier at maximum distance.
    /// </summary>
    public ConfigEntry<float> SpectatorVoiceMinimumVolume { get; }

    /// <summary>
    /// Keeps routed voice audible in 2D when synced spectator pose data is temporarily unavailable.
    /// </summary>
    public ConfigEntry<bool> SpectatorVoiceFallbackTo2DWhenPoseMissing { get; }

    /// <summary>
    /// Enables spectator voice routing diagnostics.
    /// </summary>
    public ConfigEntry<bool> DebugSpectatorVoiceRouting { get; }

    /// <summary>
    /// Repairs late vanilla connected-player state for modded peers after identity sync.
    /// </summary>
    public ConfigEntry<bool> RepairVanillaConnectedPlayerState { get; }

    /// <summary>
    /// Applies synced mod peer names to vanilla local player scripts when repairing state.
    /// </summary>
    public ConfigEntry<bool> RepairVanillaPlayerNames { get; }

    /// <summary>
    /// Enables verbose diagnostics for vanilla player state repair.
    /// </summary>
    public ConfigEntry<bool> DebugPlayerStateRepair { get; }

    /// <summary>
    /// Enables verbose networking diagnostics.
    /// </summary>
    public ConfigEntry<bool> DebugNetworkMessages { get; }

    /// <summary>
    /// Enables high-frequency spectator pose network diagnostics.
    /// </summary>
    public ConfigEntry<bool> DebugPoseMessages { get; }

    /// <summary>
    /// Enables debug-only remote spectator presence inference.
    /// </summary>
    public ConfigEntry<bool> EnableSpectatorPresenceDebug { get; }

    /// <summary>
    /// Enables debug logs when remote spectator presence changes.
    /// </summary>
    public ConfigEntry<bool> DebugLogPresenceChanges { get; }

    /// <summary>
    /// Enables key-triggered runtime player model inspection.
    /// </summary>
    public ConfigEntry<bool> EnableModelInspection { get; }

    /// <summary>
    /// Logs local player model information when the inspection key is pressed.
    /// </summary>
    public ConfigEntry<bool> LogLocalPlayerModelOnKey { get; }

    /// <summary>
    /// Logs remote player model information when the inspection key is pressed.
    /// </summary>
    public ConfigEntry<bool> LogRemotePlayerModelsOnKey { get; }

    /// <summary>
    /// Triggers one model inspection log pass.
    /// </summary>
    public ConfigEntry<KeyCode> ModelInspectionKey { get; }

    /// <summary>
    /// Includes renderer world bounds in inspection logs.
    /// </summary>
    public ConfigEntry<bool> IncludeRendererBounds { get; }

    /// <summary>
    /// Includes material names in inspection logs.
    /// </summary>
    public ConfigEntry<bool> IncludeMaterials { get; }

    /// <summary>
    /// Limits transform hierarchy traversal depth.
    /// </summary>
    public ConfigEntry<int> MaxTransformDepth { get; }

    /// <summary>
    /// Enables key-triggered runtime detached-head source inspection.
    /// </summary>
    public ConfigEntry<bool> EnableRuntimeHeadSourceInspection { get; }

    /// <summary>
    /// Triggers one runtime detached-head source inspection pass.
    /// </summary>
    public ConfigEntry<KeyCode> RuntimeHeadSourceInspectionKey { get; }

    /// <summary>
    /// Includes detached-head renderer world bounds in inspection logs.
    /// </summary>
    public ConfigEntry<bool> RuntimeHeadSourceIncludeRendererBounds { get; }

    /// <summary>
    /// Includes detached-head material names in inspection logs.
    /// </summary>
    public ConfigEntry<bool> RuntimeHeadSourceIncludeMaterials { get; }

    /// <summary>
    /// Limits detached-head hierarchy traversal depth.
    /// </summary>
    public ConfigEntry<int> RuntimeHeadSourceMaxTransformDepth { get; }

    /// <summary>
    /// Enables key-triggered read-only voice diagnostics.
    /// </summary>
    public ConfigEntry<bool> EnableVoiceDiagnostics { get; }

    /// <summary>
    /// Triggers one voice diagnostics log pass.
    /// </summary>
    public ConfigEntry<KeyCode> VoiceDiagnosticsKey { get; }

    /// <summary>
    /// Logs local player voice state when the diagnostics key is pressed.
    /// </summary>
    public ConfigEntry<bool> LogLocalVoiceStateOnKey { get; }

    /// <summary>
    /// Logs remote player voice states when the diagnostics key is pressed.
    /// </summary>
    public ConfigEntry<bool> LogRemoteVoiceStatesOnKey { get; }

    /// <summary>
    /// Includes mapped AudioSource details in voice diagnostics logs.
    /// </summary>
    public ConfigEntry<bool> IncludeVoiceAudioSourceDetails { get; }

    /// <summary>
    /// Includes walkie-talkie voice flags in voice diagnostics logs.
    /// </summary>
    public ConfigEntry<bool> IncludeWalkieVoiceDiagnostics { get; }

    /// <summary>
    /// Enables local floating-head placeholder visuals.
    /// </summary>
    public ConfigEntry<bool> EnableFloatingHeadVisuals { get; }

    /// <summary>
    /// Enables runtime-created placeholder visuals.
    /// </summary>
    public ConfigEntry<bool> EnablePlaceholderVisuals { get; }

    /// <summary>
    /// Enables runtime detached-head clones when a confirmed source exists.
    /// </summary>
    public ConfigEntry<bool> UseRuntimeDetachedHeadVisuals { get; }

    /// <summary>
    /// Controls runtime detached-head clone scale.
    /// </summary>
    public ConfigEntry<float> RuntimeDetachedHeadScale { get; }

    /// <summary>
    /// Pitch correction applied to runtime detached-head visuals.
    /// </summary>
    public ConfigEntry<float> RuntimeDetachedHeadPitchOffset { get; }

    /// <summary>
    /// Yaw correction applied to runtime detached-head visuals.
    /// </summary>
    public ConfigEntry<float> RuntimeDetachedHeadYawOffset { get; }

    /// <summary>
    /// Roll correction applied to runtime detached-head visuals.
    /// </summary>
    public ConfigEntry<float> RuntimeDetachedHeadRollOffset { get; }

    /// <summary>
    /// Falls back to placeholder visuals when detached-head source is unavailable.
    /// </summary>
    public ConfigEntry<bool> FallbackToPlaceholderWhenDetachedHeadUnavailable { get; }

    /// <summary>
    /// Shows remote modded players while they are spectating, even when they are not watching the local player.
    /// </summary>
    public ConfigEntry<bool> ShowRemoteSpectators { get; }

    /// <summary>
    /// Restricts placeholder visuals to remote spectators whose current target is the local player.
    /// </summary>
    public ConfigEntry<bool> ShowOnlySpectatorsWatchingMe { get; }

    /// <summary>
    /// Allows living local players to see remote spectator placeholders.
    /// </summary>
    public ConfigEntry<bool> ShowDeadSpectatorsToAlivePlayers { get; }

    /// <summary>
    /// Allows dead or spectating local players to see remote spectator placeholders.
    /// </summary>
    public ConfigEntry<bool> ShowDeadSpectatorsToDeadPlayers { get; }

    /// <summary>
    /// Limits the number of remote spectator placeholders shown at once.
    /// </summary>
    public ConfigEntry<int> MaxFloatingHeadsVisible { get; }

    /// <summary>
    /// Controls the runtime-only placeholder visual style.
    /// </summary>
    public ConfigEntry<FloatingHeadVisualStyle> VisualStyle { get; }

    /// <summary>
    /// Controls placeholder sphere scale.
    /// </summary>
    public ConfigEntry<float> PlaceholderScale { get; }

    /// <summary>
    /// Controls billboard and ring marker size.
    /// </summary>
    public ConfigEntry<float> BillboardSize { get; }

    /// <summary>
    /// Controls placeholder material alpha where the runtime shader supports it.
    /// </summary>
    public ConfigEntry<float> BaseAlpha { get; }

    /// <summary>
    /// Prefers an unlit runtime material for placeholders.
    /// </summary>
    public ConfigEntry<bool> UseUnlitMaterial { get; }

    /// <summary>
    /// Controls whether placeholder material should use normal depth testing when supported.
    /// </summary>
    public ConfigEntry<bool> EnableDepthTest { get; }

    /// <summary>
    /// Controls horizontal ring radius around the local head anchor.
    /// </summary>
    public ConfigEntry<float> FloatingHeadRingRadius { get; }

    /// <summary>
    /// Controls vertical offset above the local head anchor.
    /// </summary>
    public ConfigEntry<float> FloatingHeadHeightOffset { get; }

    /// <summary>
    /// Biases placeholders into the local active camera view.
    /// </summary>
    public ConfigEntry<bool> UseCameraVisiblePlacement { get; }

    /// <summary>
    /// Controls how far placeholders are pushed toward the active camera forward direction.
    /// </summary>
    public ConfigEntry<float> CameraForwardOffset { get; }

    /// <summary>
    /// Smooths remote spectator placeholder movement.
    /// </summary>
    public ConfigEntry<float> RemotePoseSmoothTime { get; }

    /// <summary>
    /// Keeps remote spectator pose markers visible when the true pose is outside the local camera view.
    /// </summary>
    public ConfigEntry<bool> KeepRemotePoseInView { get; }

    /// <summary>
    /// Controls the camera-forward distance used for out-of-view remote pose proxy markers.
    /// </summary>
    public ConfigEntry<float> RemotePoseVisibleProxyDistance { get; }

    /// <summary>
    /// Draws a runtime IMGUI marker at the placeholder screen position for render-pipeline fallback.
    /// </summary>
    public ConfigEntry<bool> EnableScreenFallbackVisual { get; }

    /// <summary>
    /// Controls the screen fallback marker size in pixels.
    /// </summary>
    public ConfigEntry<float> ScreenFallbackSize { get; }

    /// <summary>
    /// Keeps placeholder visuals alive briefly through transient empty presence frames.
    /// </summary>
    public ConfigEntry<float> PresenceLostGraceSeconds { get; }

    /// <summary>
    /// Rotates placeholder visuals toward the local camera.
    /// </summary>
    public ConfigEntry<bool> FloatingHeadFaceCamera { get; }

    /// <summary>
    /// Enables local placeholder scale pulse from available voice activity.
    /// </summary>
    public ConfigEntry<bool> PulseWhenSpeaking { get; }

    /// <summary>
    /// Scale multiplier for speaking spectators.
    /// </summary>
    public ConfigEntry<float> SpeakingScaleMultiplier { get; }

    /// <summary>
    /// Pulse speed for speaking spectators.
    /// </summary>
    public ConfigEntry<float> SpeakingPulseSpeed { get; }

    /// <summary>
    /// Voice level used when speaking is true but amplitude is unavailable.
    /// </summary>
    public ConfigEntry<float> MinimumSpeakingVoiceLevel { get; }

    /// <summary>
    /// Extra scale pulse amount for speaking spectators.
    /// </summary>
    public ConfigEntry<float> SpeakingPulseAmount { get; }

    /// <summary>
    /// Smooth time used when speaking starts.
    /// </summary>
    public ConfigEntry<float> VoiceAttackSmoothTime { get; }

    /// <summary>
    /// Smooth time used when speaking stops.
    /// </summary>
    public ConfigEntry<float> VoiceReleaseSmoothTime { get; }

    /// <summary>
    /// Scale multiplier for silent or unknown voice state.
    /// </summary>
    public ConfigEntry<float> SilenceScaleMultiplier { get; }

    /// <summary>
    /// Smooth time for voice activity amplitude used by placeholders.
    /// </summary>
    public ConfigEntry<float> AmplitudeSmoothing { get; }

    /// <summary>
    /// Destroys placeholder visuals when remote presence is lost.
    /// </summary>
    public ConfigEntry<bool> DestroyOnPresenceLost { get; }

    /// <summary>
    /// Enables verbose placeholder visual lifecycle logs.
    /// </summary>
    public ConfigEntry<bool> DebugVisualLifecycle { get; }

    /// <summary>
    /// Enables runtime-only name tags above floating-head placeholders.
    /// </summary>
    public ConfigEntry<bool> ShowNameTags { get; }

    /// <summary>
    /// Controls the world-space text character size for name tags.
    /// </summary>
    public ConfigEntry<float> NameTagScale { get; }

    /// <summary>
    /// Controls the vertical offset above the floating-head placeholder.
    /// </summary>
    public ConfigEntry<float> NameTagHeightOffset { get; }

    /// <summary>
    /// Hides name tags beyond this camera distance. Set to zero to disable distance culling.
    /// </summary>
    public ConfigEntry<float> NameTagMaxDistance { get; }

    /// <summary>
    /// Uses confirmed in-game player usernames when available.
    /// </summary>
    public ConfigEntry<bool> NameTagUseGamePlayerNames { get; }

    /// <summary>
    /// Uses fallback client and slot ids when a game player name is unavailable.
    /// </summary>
    public ConfigEntry<bool> NameTagUseFallbackIds { get; }

    /// <summary>
    /// Enables verbose name tag lifecycle diagnostics.
    /// </summary>
    public ConfigEntry<bool> DebugNameTagLifecycle { get; }

    /// <summary>
    /// Selects the config and runtime UI language.
    /// </summary>
    public ConfigEntry<EnhancedSpectatorLanguageMode> ConfigLanguage { get; }

    /// <summary>
    /// Gets the language resolved at plugin startup.
    /// </summary>
    public bool UseChineseText { get; }

    /// <summary>
    /// Enables self-ghost third-person spectator mode.
    /// </summary>
    public ConfigEntry<bool> EnableThirdPerson { get; }

    /// <summary>
    /// Toggles self-ghost third-person spectator mode.
    /// </summary>
    public ConfigEntry<KeyCode> ToggleThirdPersonKey { get; }

    /// <summary>
    /// Controls trailing third-person camera distance.
    /// </summary>
    public ConfigEntry<float> ThirdPersonDistance { get; }

    /// <summary>
    /// Controls third-person camera height above the logical ghost.
    /// </summary>
    public ConfigEntry<float> ThirdPersonHeight { get; }

    /// <summary>Host-only session gate for player-selected fear visuals.</summary>
    public ConfigEntry<bool> EnableFearModeAsHost { get; }

    /// <summary>Local viewer opt-in for fear model rendering.</summary>
    public ConfigEntry<bool> RenderFearModelsLocally { get; }

    /// <summary>Shows the fear-mode selection surface in the ESC quick menu.</summary>
    public ConfigEntry<bool> ShowFearModeQuickMenu { get; }

    /// <summary>Target world height used to normalize renderer-only monster visuals.</summary>
    public ConfigEntry<float> FearModelTargetHeight { get; }

    /// <summary>Uses each enemy prefab's original visible size instead of uniform target-height normalization.</summary>
    public ConfigEntry<bool> FearModelUseOriginalScale { get; }

    /// <summary>Global multiplier applied after original or normalized fear-model scaling.</summary>
    public ConfigEntry<float> FearModelScaleMultiplier { get; }

    /// <summary>Hotkey used to select the previous fear model.</summary>
    public ConfigEntry<KeyCode> FearModelPreviousKey { get; }

    /// <summary>Hotkey used to select the next fear model.</summary>
    public ConfigEntry<KeyCode> FearModelNextKey { get; }

    /// <summary>Hotkey used by a dead player to play the selected monster's fear sound.</summary>
    public ConfigEntry<KeyCode> FearSoundKey { get; }

    /// <summary>Hotkey used to switch to the next selected-monster sound and play it immediately.</summary>
    public ConfigEntry<KeyCode> FearSoundNextKey { get; }

    /// <summary>Local playback volume for spatial fear sounds.</summary>
    public ConfigEntry<float> FearSoundVolume { get; }

    /// <summary>Distance at which spatial fear sound begins attenuating.</summary>
    public ConfigEntry<float> FearSoundMinDistance { get; }

    /// <summary>Maximum audible distance for spatial fear sound.</summary>
    public ConfigEntry<float> FearSoundMaxDistance { get; }

    /// <summary>Host-enforced minimum interval between fear sounds from one player.</summary>
    public ConfigEntry<float> FearSoundCooldownSeconds { get; }

    /// <summary>Maximum nearby player fear sounds heard at once; zero means unlimited.</summary>
    public ConfigEntry<int> FearSoundMaxNearbyPlayers { get; }

    /// <summary>
    /// Binds all configuration entries from the provided BepInEx config file.
    /// </summary>
    public static EnhancedSpectatorConfig Bind(
        ConfigFile config,
        bool? lcChineseProjectInstalled = null,
        string? advancedConfigPath = null)
    {
        bool primarySaveOnConfigSet = config.SaveOnConfigSet;
        config.SaveOnConfigSet = false;
        config.Remove(new ConfigDefinition("Spectator.Audio", "MuteSpectatorVoiceKey"));
        config.Remove(new ConfigDefinition("Spectator.Audio", "ShowSpectatorVoiceMuteHud"));
        ConfigEntry<EnhancedSpectatorLanguageMode> configLanguage = config.Bind(
            "General",
            "ConfigLanguage",
            EnhancedSpectatorLanguageMode.Auto,
            "Config/UI language. Auto uses Chinese when LC Chinese Project is installed. 配置与界面语言；Auto 在检测到 LC Chinese Project 时使用中文。");
        bool useChineseText = EnhancedSpectatorLanguageRules.UseChinese(
            configLanguage.Value,
            lcChineseProjectInstalled ?? LCChineseProjectDetection.IsInstalled());

        ConfigEntry<bool> enableSpectatorModule = config.Bind(
            "Features",
            "EnableSpectatorModule",
            true,
            "Loads the spectator feature module.");

        ConfigEntry<bool> enableEnhancedSpectator = config.Bind(
            "Spectator.Freecam",
            "EnableEnhancedSpectator",
            true,
            EnhancedSpectatorText.Select(useChineseText, "Enables all enhanced spectator behavior.", "启用全部增强观战行为。"));

        ConfigEntry<bool> enableFreecam = config.Bind(
            "Spectator.Freecam",
            "EnableFreecam",
            true,
            EnhancedSpectatorText.Select(useChineseText, "Enables local spectator freecam behavior.", "启用本地死亡观战自由镜头。"));

        ConfigEntry<bool> freecamDefaultOn = config.Bind(
            "Spectator.Freecam",
            "FreecamDefaultOn",
            true,
            EnhancedSpectatorText.Select(useChineseText, "Automatically enables freecam after entering vanilla spectator state.", "进入原版死亡观战状态后自动启用自由镜头。"));

        ConfigEntry<float> freecamRadius = config.Bind(
            "Spectator.Freecam",
            "FreecamRadius",
            8.0f,
            EnhancedSpectatorText.Select(useChineseText, "Maximum freecam offset radius from the current target anchor.", "自由镜头相对当前观战目标锚点的最大半径。"));

        ConfigEntry<float> freecamMoveSpeed = config.Bind(
            "Spectator.Freecam",
            "FreecamMoveSpeed",
            4.0f,
            EnhancedSpectatorText.Select(useChineseText, "Base freecam movement speed in units per second.", "自由镜头基础移动速度。"));

        ConfigEntry<float> freecamFastMoveMultiplier = config.Bind(
            "Spectator.Freecam",
            "FreecamFastMoveMultiplier",
            2.5f,
            "Movement multiplier while the fast movement key is held.");

        ConfigEntry<float> freecamSlowMoveMultiplier = config.Bind(
            "Spectator.Freecam",
            "FreecamSlowMoveMultiplier",
            0.35f,
            "Movement multiplier while the slow movement key is held.");

        ConfigEntry<float> freecamLookSensitivity = config.Bind(
            "Spectator.Freecam",
            "FreecamLookSensitivity",
            1.0f,
            "Mouse look sensitivity multiplier.");

        ConfigEntry<float> freecamSmoothTime = config.Bind(
            "Spectator.Freecam",
            "FreecamSmoothTime",
            0.04f,
            EnhancedSpectatorText.Select(useChineseText, "Smooth damp time for camera position. Set to 0 to disable smoothing.", "镜头位置平滑时间；设为 0 可关闭平滑。"));

        ConfigEntry<bool> clampCameraToRadius = config.Bind(
            "Spectator.Freecam",
            "ClampCameraToRadius",
            true,
            "Clamps freecam offset to FreecamRadius.");

        ConfigEntry<bool> recenterOnTargetSwitch = config.Bind(
            "Spectator.Freecam",
            "RecenterOnTargetSwitch",
            true,
            "Recenters freecam when vanilla switches the spectated target.");

        ConfigEntry<bool> disableDuringGameOverOverride = config.Bind(
            "Spectator.Freecam",
            "DisableDuringGameOverOverride",
            true,
            "Disables enhanced freecam while vanilla game-over spectator camera override is active.");

        ConfigEntry<KeyCode> toggleFreecamKey = config.Bind(
            "Spectator.Freecam.Keys",
            "ToggleFreecamKey",
            KeyCode.F6,
            EnhancedSpectatorText.Select(useChineseText, "Toggles enhanced freecam while spectating.", "旁观时切换增强自由镜头。"));

        ConfigEntry<KeyCode> recenterKey = config.Bind(
            "Spectator.Freecam.Keys",
            "RecenterKey",
            KeyCode.R,
            "Recenters enhanced freecam around the current spectated target.");

        ConfigEntry<KeyCode> resetToVanillaViewKey = config.Bind(
            "Spectator.Freecam.Keys",
            "ResetToVanillaViewKey",
            KeyCode.F7,
            EnhancedSpectatorText.Select(useChineseText, "Disables enhanced freecam and returns to vanilla spectator camera until toggled again.", "关闭增强镜头并返回原版观战视角，直到再次切换。"));

        ConfigEntry<KeyCode> fastMoveKey = config.Bind(
            "Spectator.Freecam.Keys",
            "FastMoveKey",
            KeyCode.LeftShift,
            "Fast movement modifier key.");

        ConfigEntry<KeyCode> slowMoveKey = config.Bind(
            "Spectator.Freecam.Keys",
            "SlowMoveKey",
            KeyCode.LeftAlt,
            "Slow movement modifier key.");

        ConfigEntry<KeyCode> ascendKey = config.Bind(
            "Spectator.Freecam.Keys",
            "AscendKey",
            KeyCode.Space,
            "Moves the freecam upward while held.");

        ConfigEntry<KeyCode> descendKey = config.Bind(
            "Spectator.Freecam.Keys",
            "DescendKey",
            KeyCode.LeftControl,
            "Moves the freecam downward while held.");

        ConfigEntry<bool> enableThirdPerson = config.Bind(
            "Spectator.ThirdPerson",
            "EnableThirdPerson",
            true,
            EnhancedSpectatorText.Select(
                useChineseText,
                "Enables third-person viewing of your own ghost while dead.",
                "死亡后允许以第三人称观察自己的鬼魂。"));

        ConfigEntry<KeyCode> toggleThirdPersonKey = config.Bind(
            "Spectator.ThirdPerson",
            "ToggleThirdPersonKey",
            KeyCode.F3,
            EnhancedSpectatorText.Select(
                useChineseText,
                "Toggles self-ghost third-person view while spectating.",
                "旁观时切换观察自身鬼魂的第三人称视角。"));

        ConfigEntry<float> thirdPersonDistance = config.Bind(
            "Spectator.ThirdPerson",
            "ThirdPersonDistance",
            5.0f,
            EnhancedSpectatorText.Select(
                useChineseText,
                "Default distance behind your logical ghost. Use the mouse wheel in third person to zoom.",
                "第三人称相机与自身鬼魂的默认距离；第三人称中可用鼠标滚轮缩放。"));

        ConfigEntry<float> thirdPersonHeight = config.Bind(
            "Spectator.ThirdPerson",
            "ThirdPersonHeight",
            0.45f,
            EnhancedSpectatorText.Select(
                useChineseText,
                "Height above your logical ghost used by third-person view.",
                "第三人称相机相对自身鬼魂的高度。"));

        ConfigEntry<bool> enableFearModeAsHost = config.Bind(
            "FearMode",
            "EnableFearModeAsHost",
            false,
            EnhancedSpectatorText.Select(
                useChineseText,
                "Host-only gate allowing dead players to select and relay safe fear visuals for this session.",
                "仅房主生效：允许死亡玩家在本局选择并中继安全恐惧外观。"));

        ConfigEntry<bool> renderFearModelsLocally = config.Bind(
            "FearMode",
            "RenderFearModelsLocally",
            false,
            EnhancedSpectatorText.Select(
                useChineseText,
                "Shows validated player-selected fear models on this client. Off always uses default ghost heads.",
                "本客户端显示已验证的玩家自选恐惧模型；关闭时始终显示默认鬼头。"));

        ConfigEntry<bool> showFearModeQuickMenu = config.Bind(
            "FearMode",
            "ShowFearModeQuickMenu",
            true,
            EnhancedSpectatorText.Select(
                useChineseText,
                "Shows the local fear model selector while the ESC menu is open and the player is dead.",
                "死亡后打开 ESC 菜单时显示本地恐惧模型选择器。"));

        ConfigEntry<float> fearModelTargetHeight = config.Bind(
            "FearMode",
            "FearModelTargetHeight",
            0.75f,
            EnhancedSpectatorText.Select(
                useChineseText,
                "Normalizes renderer-only monster visuals to this approximate world height.",
                "将纯渲染怪物外观归一化到此近似世界高度。"));

        ConfigEntry<bool> fearModelUseOriginalScale = config.Bind(
            "FearMode",
            "FearModelUseOriginalScale",
            true,
            EnhancedSpectatorText.Select(
                useChineseText,
                "Uses each monster prefab's original game size. Disable to use FearModelTargetHeight normalization.",
                "使用每个怪物 prefab 的原版游戏尺寸；关闭后改用 FearModelTargetHeight 统一高度。"));

        ConfigEntry<float> fearModelScaleMultiplier = config.Bind(
            "FearMode",
            "FearModelScaleMultiplier",
            1.0f,
            EnhancedSpectatorText.Select(
                useChineseText,
                "Global multiplier applied to all fear-model sizes.",
                "应用于全部恐惧模型尺寸的全局倍率。"));

        ConfigEntry<KeyCode> fearModelPreviousKey = config.Bind(
            "FearMode",
            "FearModelPreviousKey",
            KeyCode.LeftArrow,
            EnhancedSpectatorText.Select(
                useChineseText,
                "Selects the previous fear model while dead.",
                "死亡后快捷切换到上一个恐惧模型。"));

        ConfigEntry<KeyCode> fearModelNextKey = config.Bind(
            "FearMode",
            "FearModelNextKey",
            KeyCode.RightArrow,
            EnhancedSpectatorText.Select(
                useChineseText,
                "Selects the next fear model while dead.",
                "死亡后快捷切换到下一个恐惧模型。"));

        ConfigEntry<KeyCode> fearSoundKey = config.Bind(
            "FearMode",
            "FearSoundKey",
            KeyCode.Z,
            EnhancedSpectatorText.Select(
                useChineseText,
                "Plays the selected monster's spatial fear sound while fear mode is active and you are dead.",
                "恐惧模式生效且已死亡时，播放所选怪物的空间恐惧音效。"));

        ConfigEntry<KeyCode> fearSoundNextKey = config.Bind(
            "FearMode",
            "FearSoundNextKey",
            KeyCode.X,
            EnhancedSpectatorText.Select(
                useChineseText,
                "Switches to the next selected-monster sound and plays it immediately.",
                "切换到所选怪物的下一条音效并立即播放。"));

        ConfigEntry<float> fearSoundVolume = config.Bind(
            "FearMode",
            "FearSoundVolume",
            1f,
            "Local volume for positional fear sounds (0-1).");

        ConfigEntry<float> fearSoundMinDistance = config.Bind(
            "FearMode",
            "FearSoundMinDistance",
            2f,
            "Distance where positional fear sound attenuation begins.");

        ConfigEntry<float> fearSoundMaxDistance = config.Bind(
            "FearMode",
            "FearSoundMaxDistance",
            35f,
            "Maximum audible distance for positional fear sounds.");

        ConfigEntry<float> fearSoundCooldownSeconds = config.Bind(
            "FearMode",
            "FearSoundCooldownSeconds",
            2f,
            "Host-enforced minimum interval between fear sounds from one player.");

        ConfigEntry<int> fearSoundMaxNearbyPlayers = config.Bind(
            "FearMode",
            "FearSoundMaxNearbyPlayers",
            0,
            "Maximum nearby players whose fear sounds are audible at once. 0 means unlimited. 周围最多同时听到几个玩家的恐惧音效；0 表示不限。");

        ConfigEntry<bool> enableDebugLogging = config.Bind(
            "Logging",
            "EnableDebugLogging",
            false,
            "Enables verbose Enhanced Spectator debug logs.");

        ConfigEntry<bool> enableNetworking = config.Bind(
            "Networking",
            "EnableNetworking",
            true,
            "Enables Enhanced Spectator mod-owned networking modules.");

        ConfigEntry<bool> enableCapabilityHandshake = config.Bind(
            "Networking",
            "EnableCapabilityHandshake",
            true,
            "Enables mod capability handshake messages over Unity Netcode custom messaging.");

        ConfigEntry<bool> enableSpectatorTargetSync = config.Bind(
            "Networking",
            "EnableSpectatorTargetSync",
            true,
            "Enables handshake-gated spectator target state synchronization.");

        ConfigEntry<bool> enableSpectatorPoseSync = config.Bind(
            "Networking",
            "EnableSpectatorPoseSync",
            true,
            "Enables handshake-gated spectator camera pose synchronization for placeholder visuals.");

        ConfigEntry<bool> enableHostRelay = config.Bind(
            "Networking",
            "EnableHostRelay",
            true,
            EnhancedSpectatorText.Select(useChineseText, "Enables host-mediated relay of compatible client spectator state to other modded clients.", "启用房主中继，将兼容客户端的观战状态发送给其他已安装客户端。"));

        ConfigEntry<float> spectatorPoseSyncInterval = config.Bind(
            "Networking",
            "SpectatorPoseSyncInterval",
            0.1f,
            "Minimum seconds between spectator camera pose messages. Higher values reduce traffic; lower values track movement more closely.");

        ConfigEntry<bool> enableVoiceActivitySync = config.Bind(
            "Networking",
            "EnableVoiceActivitySync",
            true,
            "Enables visual-only voice activity synchronization so remote floating heads can scale from the speaker's local microphone amplitude. This does not forward voice audio.");

        ConfigEntry<float> voiceActivitySyncInterval = config.Bind(
            "Networking",
            "VoiceActivitySyncInterval",
            0.066f,
            "Minimum seconds between voice activity visual messages. Lower values react faster but send more network metadata.");

        ConfigEntry<float> voiceActivityStaleSeconds = config.Bind(
            "Networking",
            "VoiceActivityStaleSeconds",
            0.5f,
            "Seconds before a received voice activity visual state is considered stale. This prevents dropped silence packets from leaving a remote head enlarged.");

        ConfigEntry<bool> debugVoiceActivitySync = config.Bind(
            "Networking",
            "DebugVoiceActivitySync",
            false,
            "Logs voice activity sync send/receive/relay diagnostics. Keep disabled during normal testing.");

        ConfigEntry<bool> enableSpectatorVoiceToTarget = config.Bind(
            "VoiceRouting",
            "EnableSpectatorVoiceToTarget",
            true,
            EnhancedSpectatorText.Select(useChineseText, "Enables routed dead-spectator voice between compatible Enhanced Spectator peers.", "允许兼容的 Enhanced Spectator 玩家听到路由后的死亡观战者语音。"));

        ConfigEntry<SpectatorVoiceAudienceMode> spectatorVoiceAudienceMode = config.Bind(
            "VoiceRouting",
            "SpectatorVoiceAudienceMode",
            EnhancedSpectator.Config.SpectatorVoiceAudienceMode.AllModdedPlayers,
            EnhancedSpectatorText.Select(useChineseText, "Controls which compatible players hear routed dead-spectator voice.", "控制哪些兼容玩家可以听到路由后的死亡观战者语音。"));

        ConfigEntry<float> spectatorVoiceToTargetVolume = config.Bind(
            "VoiceRouting",
            "SpectatorVoiceToTargetVolume",
            1.0f,
            EnhancedSpectatorText.Select(useChineseText, "Local playback volume for routed spectator voice.", "路由观战者语音的本地播放音量。"));

        ConfigEntry<bool> spectatorVoiceUseRemotePosePosition = config.Bind(
            "VoiceRouting",
            "SpectatorVoiceUseRemotePosePosition",
            true,
            "Positions routed spectator voice at the synced spectator camera pose when available. Disable to force safer 2D local playback.");

        ConfigEntry<bool> spectatorVoiceEnableDistanceAttenuation = config.Bind(
            "VoiceRouting",
            "SpectatorVoiceEnableDistanceAttenuation",
            true,
            "Reduces routed spectator voice volume by distance from the synced spectator camera pose. Requires SpectatorVoiceUseRemotePosePosition=true and pose sync.");

        ConfigEntry<float> spectatorVoiceMinDistance = config.Bind(
            "VoiceRouting",
            "SpectatorVoiceMinDistance",
            2.0f,
            "Distance in meters that keeps routed spectator voice at full configured volume.");

        ConfigEntry<float> spectatorVoiceMaxDistance = config.Bind(
            "VoiceRouting",
            "SpectatorVoiceMaxDistance",
            18.0f,
            "Distance in meters where routed spectator voice reaches SpectatorVoiceMinimumVolume.");

        ConfigEntry<float> spectatorVoiceRolloffPower = config.Bind(
            "VoiceRouting",
            "SpectatorVoiceRolloffPower",
            1.25f,
            "Distance attenuation curve. 1 is linear; higher values keep near voices louder and fade more near the max distance.");

        ConfigEntry<float> spectatorVoiceMinimumVolume = config.Bind(
            "VoiceRouting",
            "SpectatorVoiceMinimumVolume",
            0.0f,
            "Minimum routed spectator voice volume multiplier at or beyond SpectatorVoiceMaxDistance.");

        ConfigEntry<bool> spectatorVoiceFallbackTo2DWhenPoseMissing = config.Bind(
            "VoiceRouting",
            "SpectatorVoiceFallbackTo2DWhenPoseMissing",
            false,
            "Falls back to 2D routed spectator voice when synced pose data is temporarily unavailable instead of dropping voice entirely. Disabled by default so relayed listeners do not hear stale global voice when pose sync is missing.");

        ConfigEntry<bool> debugSpectatorVoiceRouting = config.Bind(
            "VoiceRouting",
            "DebugSpectatorVoiceRouting",
            false,
            "Logs spectator voice route enable/clear diagnostics. Requires Logging.EnableDebugLogging=true.");

        ConfigEntry<bool> repairVanillaConnectedPlayerState = config.Bind(
            "Networking",
            "RepairVanillaConnectedPlayerState",
            true,
            "Repairs late vanilla connected-player controlled flags using vanilla ClientPlayerList. This also runs in local-only sessions when the host is unmodded, fixing cases where another connected client is missing from the ESC player list or vanilla spectator target list.");

        ConfigEntry<bool> repairVanillaPlayerNames = config.Bind(
            "Networking",
            "RepairVanillaPlayerNames",
            true,
            EnhancedSpectatorText.Select(useChineseText, "Repairs generic player names using synced identity and conservative vanilla fallbacks.", "使用同步身份和保守的原版回退修复通用玩家名称。"));

        ConfigEntry<bool> debugPlayerStateRepair = config.Bind(
            "Networking",
            "DebugPlayerStateRepair",
            false,
            "Logs each vanilla connected-player state repair. Requires Logging.EnableDebugLogging for debug output.");

        ConfigEntry<bool> debugNetworkMessages = config.Bind(
            "Networking",
            "DebugNetworkMessages",
            false,
            "Enables verbose network message diagnostics.");

        ConfigEntry<bool> debugPoseMessages = config.Bind(
            "Networking",
            "DebugPoseMessages",
            false,
            "Logs high-frequency spectator pose observe/send/receive diagnostics. Keep disabled during normal testing.");

        ConfigEntry<bool> enableSpectatorPresenceDebug = config.Bind(
            "Presence",
            "EnableSpectatorPresenceDebug",
            true,
            "Enables debug-only inference of remote spectators watching the local player.");

        ConfigEntry<bool> debugLogPresenceChanges = config.Bind(
            "Presence",
            "DebugLogPresenceChanges",
            false,
            "Logs when a remote spectator starts or stops watching the local player. Requires Logging.EnableDebugLogging.");

        ConfigEntry<bool> enableModelInspection = config.Bind(
            "ModelInspection",
            "EnableModelInspection",
            false,
            "Enables key-triggered runtime player model hierarchy inspection.");

        ConfigEntry<bool> logLocalPlayerModelOnKey = config.Bind(
            "ModelInspection",
            "LogLocalPlayerModelOnKey",
            true,
            "Logs local player model information when the inspection key is pressed.");

        ConfigEntry<bool> logRemotePlayerModelsOnKey = config.Bind(
            "ModelInspection",
            "LogRemotePlayerModelsOnKey",
            true,
            "Logs remote player model information when the inspection key is pressed.");

        ConfigEntry<KeyCode> modelInspectionKey = config.Bind(
            "ModelInspection",
            "InspectionKey",
            KeyCode.F8,
            "Runs one runtime player model hierarchy inspection pass.");

        ConfigEntry<bool> includeRendererBounds = config.Bind(
            "ModelInspection",
            "IncludeRendererBounds",
            true,
            "Includes renderer world bounds in inspection logs.");

        ConfigEntry<bool> includeMaterials = config.Bind(
            "ModelInspection",
            "IncludeMaterials",
            false,
            "Includes material names in inspection logs. Disabled by default to keep logs smaller.");

        ConfigEntry<int> maxTransformDepth = config.Bind(
            "ModelInspection",
            "MaxTransformDepth",
            8,
            "Maximum transform depth scanned below each player root.");

        ConfigEntry<bool> enableRuntimeHeadSourceInspection = config.Bind(
            "HeadSourceInspection",
            "EnableRuntimeHeadSourceInspection",
            false,
            "Enables key-triggered runtime inspection of dead-body detached-head source candidates.");

        ConfigEntry<KeyCode> runtimeHeadSourceInspectionKey = config.Bind(
            "HeadSourceInspection",
            "InspectionKey",
            KeyCode.F10,
            "Runs one runtime detached-head source inspection pass.");

        ConfigEntry<bool> runtimeHeadSourceIncludeRendererBounds = config.Bind(
            "HeadSourceInspection",
            "IncludeRendererBounds",
            true,
            "Includes detached-head renderer world bounds in inspection logs.");

        ConfigEntry<bool> runtimeHeadSourceIncludeMaterials = config.Bind(
            "HeadSourceInspection",
            "IncludeMaterials",
            false,
            "Includes detached-head material names in inspection logs. Disabled by default to keep logs smaller.");

        ConfigEntry<int> runtimeHeadSourceMaxTransformDepth = config.Bind(
            "HeadSourceInspection",
            "MaxTransformDepth",
            6,
            "Maximum transform depth scanned below each detached-head object.");

        ConfigEntry<bool> enableVoiceDiagnostics = config.Bind(
            "VoiceDiagnostics",
            "EnableVoiceDiagnostics",
            false,
            "Enables key-triggered read-only voice diagnostics. This does not route, forward, or modify voice.");

        ConfigEntry<KeyCode> voiceDiagnosticsKey = config.Bind(
            "VoiceDiagnostics",
            "InspectionKey",
            KeyCode.F11,
            "Runs one voice diagnostics log pass.");

        ConfigEntry<bool> logLocalVoiceStateOnKey = config.Bind(
            "VoiceDiagnostics",
            "LogLocalVoiceStateOnKey",
            true,
            "Logs local player voice state when the diagnostics key is pressed.");

        ConfigEntry<bool> logRemoteVoiceStatesOnKey = config.Bind(
            "VoiceDiagnostics",
            "LogRemoteVoiceStatesOnKey",
            true,
            "Logs remote player voice states when the diagnostics key is pressed.");

        ConfigEntry<bool> includeVoiceAudioSourceDetails = config.Bind(
            "VoiceDiagnostics",
            "IncludeAudioSourceDetails",
            true,
            "Includes mapped AudioSource playback details in voice diagnostics logs.");

        ConfigEntry<bool> includeWalkieVoiceDiagnostics = config.Bind(
            "VoiceDiagnostics",
            "IncludeWalkieDiagnostics",
            true,
            "Includes walkie-talkie voice flags in voice diagnostics logs.");

        ConfigEntry<bool> enableFloatingHeadVisuals = config.Bind(
            "FloatingHead",
            "EnableFloatingHeadVisuals",
            true,
            EnhancedSpectatorText.Select(useChineseText, "Enables local visuals for remote modded spectators.", "显示远程已安装玩家的观战者外观。"));

        ConfigEntry<bool> enablePlaceholderVisuals = config.Bind(
            "FloatingHead",
            "EnablePlaceholderVisuals",
            true,
            "Creates simple runtime placeholder visuals instead of real head mesh clones.");

        ConfigEntry<bool> useRuntimeDetachedHeadVisuals = config.Bind(
            "FloatingHead",
            "UseRuntimeDetachedHeadVisuals",
            true,
            EnhancedSpectatorText.Select(useChineseText, "Uses the loaded detached-head template as the default runtime ghost head when available.", "可用时使用已加载的断头模板作为默认运行时鬼头。"));

        ConfigEntry<float> runtimeDetachedHeadScale = config.Bind(
            "FloatingHead",
            "RuntimeDetachedHeadScale",
            0.35f,
            "World scale multiplier applied to runtime detached-head visual clones.");

        ConfigEntry<float> runtimeDetachedHeadPitchOffset = config.Bind(
            "FloatingHead",
            "RuntimeDetachedHeadPitchOffset",
            FloatingHeadRotationRules.DefaultRuntimeDetachedHeadPitchOffsetDegrees,
            "Pitch correction in degrees applied after the remote spectator camera rotation for runtime detached-head visuals. Default matches the calibrated detached-head template orientation.");

        ConfigEntry<float> runtimeDetachedHeadYawOffset = config.Bind(
            "FloatingHead",
            "RuntimeDetachedHeadYawOffset",
            FloatingHeadRotationRules.DefaultRuntimeDetachedHeadYawOffsetDegrees,
            "Yaw correction in degrees applied after the remote spectator camera rotation for runtime detached-head visuals. Default matches the calibrated detached-head template orientation.");

        ConfigEntry<float> runtimeDetachedHeadRollOffset = config.Bind(
            "FloatingHead",
            "RuntimeDetachedHeadRollOffset",
            FloatingHeadRotationRules.DefaultRuntimeDetachedHeadRollOffsetDegrees,
            "Roll correction in degrees applied after the remote spectator camera rotation for runtime detached-head visuals. Default matches the calibrated detached-head template orientation.");

        ConfigEntry<bool> fallbackToPlaceholderWhenDetachedHeadUnavailable = config.Bind(
            "FloatingHead",
            "FallbackToPlaceholderWhenDetachedHeadUnavailable",
            true,
            "Falls back to placeholder visuals when runtime detached-head source data is unavailable.");

        ConfigEntry<bool> showRemoteSpectators = config.Bind(
            "FloatingHead",
            "ShowRemoteSpectators",
            true,
            EnhancedSpectatorText.Select(useChineseText, "Shows remote modded players while they are spectating and have a valid pose.", "远程已安装玩家处于观战状态且姿态有效时显示其外观。"));

        ConfigEntry<bool> showOnlySpectatorsWatchingMe = config.Bind(
            "FloatingHead",
            "ShowOnlySpectatorsWatchingMe",
            false,
            "When enabled, only shows remote spectators whose current target is the local player. When disabled, all remote spectators can be shown.");

        ConfigEntry<bool> showDeadSpectatorsToAlivePlayers = config.Bind(
            "FloatingHead",
            "ShowDeadSpectatorsToAlivePlayers",
            true,
            "Allows living local players to see remote spectator placeholders.");

        ConfigEntry<bool> showDeadSpectatorsToDeadPlayers = config.Bind(
            "FloatingHead",
            "ShowDeadSpectatorsToDeadPlayers",
            true,
            "Allows dead or spectating local players to see remote spectator placeholders.");

        ConfigEntry<int> maxFloatingHeadsVisible = config.Bind(
            "FloatingHead",
            "MaxFloatingHeadsVisible",
            8,
            EnhancedSpectatorText.Select(useChineseText, "Maximum remote spectator visuals shown at once. Set to 0 to hide them.", "同时显示的远程观战者外观上限；设为 0 可全部隐藏。"));

        ConfigEntry<FloatingHeadVisualStyle> visualStyle = config.Bind(
            "FloatingHead",
            "VisualStyle",
            FloatingHeadVisualStyle.Sphere,
            "Runtime-only placeholder style: Sphere, Billboard, or Ring. Sphere is the default world-space marker style.");

        ConfigEntry<float> placeholderScale = config.Bind(
            "FloatingHead",
            "PlaceholderScale",
            0.18f,
            "World scale for each floating-head placeholder sphere.");

        ConfigEntry<float> billboardSize = config.Bind(
            "FloatingHead",
            "BillboardSize",
            0.22f,
            "World size for billboard and ring placeholder styles.");

        ConfigEntry<float> baseAlpha = config.Bind(
            "FloatingHead",
            "BaseAlpha",
            1.0f,
            "Placeholder material alpha where the runtime shader supports transparency.");

        ConfigEntry<bool> useUnlitMaterial = config.Bind(
            "FloatingHead",
            "UseUnlitMaterial",
            true,
            "Prefers an unlit runtime material for placeholder visibility.");

        ConfigEntry<bool> enableDepthTest = config.Bind(
            "FloatingHead",
            "EnableDepthTest",
            true,
            "Keeps normal depth testing for placeholders when the runtime shader supports it.");

        ConfigEntry<float> floatingHeadRingRadius = config.Bind(
            "FloatingHead",
            "RingRadius",
            0.45f,
            "Horizontal radius around the local head anchor used to distribute multiple placeholders.");

        ConfigEntry<float> floatingHeadHeightOffset = config.Bind(
            "FloatingHead",
            "HeightOffset",
            0.25f,
            "Vertical offset above the local head anchor for placeholder visuals.");

        ConfigEntry<bool> useCameraVisiblePlacement = config.Bind(
            "FloatingHead",
            "UseCameraVisiblePlacement",
            false,
            "Places fallback placeholders in front of the active camera. Diagnostic only; disabled by default so remote freecam poses stay in world space.");

        ConfigEntry<float> cameraForwardOffset = config.Bind(
            "FloatingHead",
            "CameraForwardOffset",
            1.15f,
            "Forward distance from the active camera when camera-visible placement is enabled.");

        ConfigEntry<float> remotePoseSmoothTime = config.Bind(
            "FloatingHead",
            "RemotePoseSmoothTime",
            0.08f,
            "Smooth damp time for remote spectator placeholder movement. Set to 0 to snap to received poses.");

        ConfigEntry<bool> keepRemotePoseInView = config.Bind(
            "FloatingHead",
            "KeepRemotePoseInView",
            false,
            "Projects remote spectator pose markers into the local camera view edge when the true remote pose is behind or outside the current view. Diagnostic only; disabled by default for strict world-space freecam following.");

        ConfigEntry<float> remotePoseVisibleProxyDistance = config.Bind(
            "FloatingHead",
            "RemotePoseVisibleProxyDistance",
            1.35f,
            "Camera-forward distance for visible proxy placement when a remote spectator pose is outside the local camera view.");

        ConfigEntry<bool> enableScreenFallbackVisual = config.Bind(
            "FloatingHead",
            "EnableScreenFallbackVisual",
            false,
            "Draws a runtime IMGUI fallback marker at the placeholder screen position when 3D marker rendering is unreliable. Intended for diagnostics only.");

        ConfigEntry<float> screenFallbackSize = config.Bind(
            "FloatingHead",
            "ScreenFallbackSize",
            48f,
            "Screen fallback marker diameter in pixels.");

        ConfigEntry<float> presenceLostGraceSeconds = config.Bind(
            "FloatingHead",
            "PresenceLostGraceSeconds",
            0.3f,
            "Keeps existing placeholder visuals alive briefly through transient empty presence frames. Disconnect and shutdown still clear immediately.");

        ConfigEntry<bool> floatingHeadFaceCamera = config.Bind(
            "FloatingHead",
            "FaceCamera",
            true,
            "Rotates placeholder visuals toward the local camera when possible.");

        ConfigEntry<bool> pulseWhenSpeaking = config.Bind(
            "FloatingHead",
            "PulseWhenSpeaking",
            true,
            "Pulses placeholder scale when local voice activity data says the remote spectator is speaking.");

        ConfigEntry<float> speakingScaleMultiplier = config.Bind(
            "FloatingHead",
            "SpeakingScaleMultiplier",
            1.65f,
            "Maximum visual scale multiplier while the represented spectator is speaking at high observed amplitude.");

        ConfigEntry<float> speakingPulseSpeed = config.Bind(
            "FloatingHead",
            "SpeakingPulseSpeed",
            8.0f,
            "Speed of the speaking visual pulse.");

        ConfigEntry<float> minimumSpeakingVoiceLevel = config.Bind(
            "FloatingHead",
            "MinimumSpeakingVoiceLevel",
            FloatingHeadVoiceScaleRules.DefaultMinimumSpeakingVoiceLevel,
            "Normalized fallback voice level used only when IsSpeaking is true and amplitude is unavailable or zero. Positive amplitude values are preserved so quiet syllables do not stay fully enlarged.");

        ConfigEntry<float> speakingPulseAmount = config.Bind(
            "FloatingHead",
            "SpeakingPulseAmount",
            FloatingHeadVoiceScaleRules.DefaultSpeakingPulseAmount,
            "Extra scale pulse amount applied while the represented spectator is speaking.");

        ConfigEntry<float> voiceAttackSmoothTime = config.Bind(
            "FloatingHead",
            "VoiceAttackSmoothTime",
            FloatingHeadVoiceScaleRules.DefaultVoiceAttackSmoothTime,
            "Smooth time used when voice activity starts. Lower values make the head enlarge faster.");

        ConfigEntry<float> voiceReleaseSmoothTime = config.Bind(
            "FloatingHead",
            "VoiceReleaseSmoothTime",
            FloatingHeadVoiceScaleRules.DefaultVoiceReleaseSmoothTime,
            "Smooth time used when voice activity stops. Lower values make the head shrink back faster.");

        ConfigEntry<float> silenceScaleMultiplier = config.Bind(
            "FloatingHead",
            "SilenceScaleMultiplier",
            1.0f,
            "Visual scale multiplier while silent or when no voice activity data is available.");

        ConfigEntry<float> amplitudeSmoothing = config.Bind(
            "FloatingHead",
            "AmplitudeSmoothing",
            0.08f,
            "Legacy voice activity smooth time retained for config compatibility. Current visual response uses VoiceAttackSmoothTime and VoiceReleaseSmoothTime.");

        ConfigEntry<bool> destroyOnPresenceLost = config.Bind(
            "FloatingHead",
            "DestroyOnPresenceLost",
            true,
            "Destroys placeholder visuals when remote spectator presence is lost.");

        ConfigEntry<bool> debugVisualLifecycle = config.Bind(
            "FloatingHead",
            "DebugVisualLifecycle",
            false,
            "Logs placeholder visual creation, destruction, and anchor loss diagnostics.");

        ConfigEntry<bool> showNameTags = config.Bind(
            "NameTag",
            "ShowNameTags",
            true,
            EnhancedSpectatorText.Select(useChineseText, "Shows runtime identity labels above spectator visuals.", "在观战者外观上方显示运行时身份名称。"));

        ConfigEntry<float> nameTagScale = config.Bind(
            "NameTag",
            "NameTagScale",
            0.035f,
            "World-space character size for floating-head name tags.");

        ConfigEntry<float> nameTagHeightOffset = config.Bind(
            "NameTag",
            "NameTagHeightOffset",
            0.78f,
            "Vertical world offset above each floating-head placeholder.");

        ConfigEntry<float> nameTagMaxDistance = config.Bind(
            "NameTag",
            "NameTagMaxDistance",
            35f,
            "Maximum camera distance for rendering name tags. Set to 0 to disable distance culling.");

        ConfigEntry<bool> nameTagUseGamePlayerNames = config.Bind(
            "NameTag",
            "NameTagUseGamePlayerNames",
            true,
            "Uses the confirmed PlayerControllerB.playerUsername display name when available.");

        ConfigEntry<bool> nameTagUseFallbackIds = config.Bind(
            "NameTag",
            "NameTagUseFallbackIds",
            true,
            "Falls back to Client/Slot identifiers when the in-game player name is unavailable.");

        ConfigEntry<bool> debugNameTagLifecycle = config.Bind(
            "NameTag",
            "DebugNameTagLifecycle",
            false,
            "Reserved for verbose name tag diagnostics.");

        ConfigFile advancedConfig = AdvancedConfigBinding.Create(advancedConfigPath, out bool migrateToAdvanced);
        bool advancedSaveOnConfigSet = advancedConfig.SaveOnConfigSet;
        advancedConfig.SaveOnConfigSet = false;
        enableSpectatorModule = AdvancedConfigBinding.Move(config, advancedConfig, enableSpectatorModule, migrateToAdvanced);
        freecamFastMoveMultiplier = AdvancedConfigBinding.Move(config, advancedConfig, freecamFastMoveMultiplier, migrateToAdvanced);
        freecamSlowMoveMultiplier = AdvancedConfigBinding.Move(config, advancedConfig, freecamSlowMoveMultiplier, migrateToAdvanced);
        freecamLookSensitivity = AdvancedConfigBinding.Move(config, advancedConfig, freecamLookSensitivity, migrateToAdvanced);
        clampCameraToRadius = AdvancedConfigBinding.Move(config, advancedConfig, clampCameraToRadius, migrateToAdvanced);
        recenterOnTargetSwitch = AdvancedConfigBinding.Move(config, advancedConfig, recenterOnTargetSwitch, migrateToAdvanced);
        disableDuringGameOverOverride = AdvancedConfigBinding.Move(config, advancedConfig, disableDuringGameOverOverride, migrateToAdvanced);
        recenterKey = AdvancedConfigBinding.Move(config, advancedConfig, recenterKey, migrateToAdvanced);
        fastMoveKey = AdvancedConfigBinding.Move(config, advancedConfig, fastMoveKey, migrateToAdvanced);
        slowMoveKey = AdvancedConfigBinding.Move(config, advancedConfig, slowMoveKey, migrateToAdvanced);
        ascendKey = AdvancedConfigBinding.Move(config, advancedConfig, ascendKey, migrateToAdvanced);
        descendKey = AdvancedConfigBinding.Move(config, advancedConfig, descendKey, migrateToAdvanced);
        enableDebugLogging = AdvancedConfigBinding.Move(config, advancedConfig, enableDebugLogging, migrateToAdvanced);
        enableNetworking = AdvancedConfigBinding.Move(config, advancedConfig, enableNetworking, migrateToAdvanced);
        enableCapabilityHandshake = AdvancedConfigBinding.Move(config, advancedConfig, enableCapabilityHandshake, migrateToAdvanced);
        enableSpectatorTargetSync = AdvancedConfigBinding.Move(config, advancedConfig, enableSpectatorTargetSync, migrateToAdvanced);
        enableSpectatorPoseSync = AdvancedConfigBinding.Move(config, advancedConfig, enableSpectatorPoseSync, migrateToAdvanced);
        spectatorPoseSyncInterval = AdvancedConfigBinding.Move(config, advancedConfig, spectatorPoseSyncInterval, migrateToAdvanced);
        enableVoiceActivitySync = AdvancedConfigBinding.Move(config, advancedConfig, enableVoiceActivitySync, migrateToAdvanced);
        voiceActivitySyncInterval = AdvancedConfigBinding.Move(config, advancedConfig, voiceActivitySyncInterval, migrateToAdvanced);
        voiceActivityStaleSeconds = AdvancedConfigBinding.Move(config, advancedConfig, voiceActivityStaleSeconds, migrateToAdvanced);
        debugVoiceActivitySync = AdvancedConfigBinding.Move(config, advancedConfig, debugVoiceActivitySync, migrateToAdvanced);
        spectatorVoiceUseRemotePosePosition = AdvancedConfigBinding.Move(config, advancedConfig, spectatorVoiceUseRemotePosePosition, migrateToAdvanced);
        spectatorVoiceEnableDistanceAttenuation = AdvancedConfigBinding.Move(config, advancedConfig, spectatorVoiceEnableDistanceAttenuation, migrateToAdvanced);
        spectatorVoiceMinDistance = AdvancedConfigBinding.Move(config, advancedConfig, spectatorVoiceMinDistance, migrateToAdvanced);
        spectatorVoiceMaxDistance = AdvancedConfigBinding.Move(config, advancedConfig, spectatorVoiceMaxDistance, migrateToAdvanced);
        spectatorVoiceRolloffPower = AdvancedConfigBinding.Move(config, advancedConfig, spectatorVoiceRolloffPower, migrateToAdvanced);
        spectatorVoiceMinimumVolume = AdvancedConfigBinding.Move(config, advancedConfig, spectatorVoiceMinimumVolume, migrateToAdvanced);
        spectatorVoiceFallbackTo2DWhenPoseMissing = AdvancedConfigBinding.Move(config, advancedConfig, spectatorVoiceFallbackTo2DWhenPoseMissing, migrateToAdvanced);
        debugSpectatorVoiceRouting = AdvancedConfigBinding.Move(config, advancedConfig, debugSpectatorVoiceRouting, migrateToAdvanced);
        repairVanillaConnectedPlayerState = AdvancedConfigBinding.Move(config, advancedConfig, repairVanillaConnectedPlayerState, migrateToAdvanced);
        debugPlayerStateRepair = AdvancedConfigBinding.Move(config, advancedConfig, debugPlayerStateRepair, migrateToAdvanced);
        debugNetworkMessages = AdvancedConfigBinding.Move(config, advancedConfig, debugNetworkMessages, migrateToAdvanced);
        debugPoseMessages = AdvancedConfigBinding.Move(config, advancedConfig, debugPoseMessages, migrateToAdvanced);
        enableSpectatorPresenceDebug = AdvancedConfigBinding.Move(config, advancedConfig, enableSpectatorPresenceDebug, migrateToAdvanced);
        debugLogPresenceChanges = AdvancedConfigBinding.Move(config, advancedConfig, debugLogPresenceChanges, migrateToAdvanced);
        enableModelInspection = AdvancedConfigBinding.Move(config, advancedConfig, enableModelInspection, migrateToAdvanced);
        logLocalPlayerModelOnKey = AdvancedConfigBinding.Move(config, advancedConfig, logLocalPlayerModelOnKey, migrateToAdvanced);
        logRemotePlayerModelsOnKey = AdvancedConfigBinding.Move(config, advancedConfig, logRemotePlayerModelsOnKey, migrateToAdvanced);
        modelInspectionKey = AdvancedConfigBinding.Move(config, advancedConfig, modelInspectionKey, migrateToAdvanced);
        includeRendererBounds = AdvancedConfigBinding.Move(config, advancedConfig, includeRendererBounds, migrateToAdvanced);
        includeMaterials = AdvancedConfigBinding.Move(config, advancedConfig, includeMaterials, migrateToAdvanced);
        maxTransformDepth = AdvancedConfigBinding.Move(config, advancedConfig, maxTransformDepth, migrateToAdvanced);
        enableRuntimeHeadSourceInspection = AdvancedConfigBinding.Move(config, advancedConfig, enableRuntimeHeadSourceInspection, migrateToAdvanced);
        runtimeHeadSourceInspectionKey = AdvancedConfigBinding.Move(config, advancedConfig, runtimeHeadSourceInspectionKey, migrateToAdvanced);
        runtimeHeadSourceIncludeRendererBounds = AdvancedConfigBinding.Move(config, advancedConfig, runtimeHeadSourceIncludeRendererBounds, migrateToAdvanced);
        runtimeHeadSourceIncludeMaterials = AdvancedConfigBinding.Move(config, advancedConfig, runtimeHeadSourceIncludeMaterials, migrateToAdvanced);
        runtimeHeadSourceMaxTransformDepth = AdvancedConfigBinding.Move(config, advancedConfig, runtimeHeadSourceMaxTransformDepth, migrateToAdvanced);
        enableVoiceDiagnostics = AdvancedConfigBinding.Move(config, advancedConfig, enableVoiceDiagnostics, migrateToAdvanced);
        voiceDiagnosticsKey = AdvancedConfigBinding.Move(config, advancedConfig, voiceDiagnosticsKey, migrateToAdvanced);
        logLocalVoiceStateOnKey = AdvancedConfigBinding.Move(config, advancedConfig, logLocalVoiceStateOnKey, migrateToAdvanced);
        logRemoteVoiceStatesOnKey = AdvancedConfigBinding.Move(config, advancedConfig, logRemoteVoiceStatesOnKey, migrateToAdvanced);
        includeVoiceAudioSourceDetails = AdvancedConfigBinding.Move(config, advancedConfig, includeVoiceAudioSourceDetails, migrateToAdvanced);
        includeWalkieVoiceDiagnostics = AdvancedConfigBinding.Move(config, advancedConfig, includeWalkieVoiceDiagnostics, migrateToAdvanced);
        enablePlaceholderVisuals = AdvancedConfigBinding.Move(config, advancedConfig, enablePlaceholderVisuals, migrateToAdvanced);
        runtimeDetachedHeadScale = AdvancedConfigBinding.Move(config, advancedConfig, runtimeDetachedHeadScale, migrateToAdvanced);
        runtimeDetachedHeadPitchOffset = AdvancedConfigBinding.Move(config, advancedConfig, runtimeDetachedHeadPitchOffset, migrateToAdvanced);
        runtimeDetachedHeadYawOffset = AdvancedConfigBinding.Move(config, advancedConfig, runtimeDetachedHeadYawOffset, migrateToAdvanced);
        runtimeDetachedHeadRollOffset = AdvancedConfigBinding.Move(config, advancedConfig, runtimeDetachedHeadRollOffset, migrateToAdvanced);
        fallbackToPlaceholderWhenDetachedHeadUnavailable = AdvancedConfigBinding.Move(config, advancedConfig, fallbackToPlaceholderWhenDetachedHeadUnavailable, migrateToAdvanced);
        showOnlySpectatorsWatchingMe = AdvancedConfigBinding.Move(config, advancedConfig, showOnlySpectatorsWatchingMe, migrateToAdvanced);
        showDeadSpectatorsToAlivePlayers = AdvancedConfigBinding.Move(config, advancedConfig, showDeadSpectatorsToAlivePlayers, migrateToAdvanced);
        showDeadSpectatorsToDeadPlayers = AdvancedConfigBinding.Move(config, advancedConfig, showDeadSpectatorsToDeadPlayers, migrateToAdvanced);
        visualStyle = AdvancedConfigBinding.Move(config, advancedConfig, visualStyle, migrateToAdvanced);
        placeholderScale = AdvancedConfigBinding.Move(config, advancedConfig, placeholderScale, migrateToAdvanced);
        billboardSize = AdvancedConfigBinding.Move(config, advancedConfig, billboardSize, migrateToAdvanced);
        baseAlpha = AdvancedConfigBinding.Move(config, advancedConfig, baseAlpha, migrateToAdvanced);
        useUnlitMaterial = AdvancedConfigBinding.Move(config, advancedConfig, useUnlitMaterial, migrateToAdvanced);
        enableDepthTest = AdvancedConfigBinding.Move(config, advancedConfig, enableDepthTest, migrateToAdvanced);
        floatingHeadRingRadius = AdvancedConfigBinding.Move(config, advancedConfig, floatingHeadRingRadius, migrateToAdvanced);
        floatingHeadHeightOffset = AdvancedConfigBinding.Move(config, advancedConfig, floatingHeadHeightOffset, migrateToAdvanced);
        useCameraVisiblePlacement = AdvancedConfigBinding.Move(config, advancedConfig, useCameraVisiblePlacement, migrateToAdvanced);
        cameraForwardOffset = AdvancedConfigBinding.Move(config, advancedConfig, cameraForwardOffset, migrateToAdvanced);
        remotePoseSmoothTime = AdvancedConfigBinding.Move(config, advancedConfig, remotePoseSmoothTime, migrateToAdvanced);
        keepRemotePoseInView = AdvancedConfigBinding.Move(config, advancedConfig, keepRemotePoseInView, migrateToAdvanced);
        remotePoseVisibleProxyDistance = AdvancedConfigBinding.Move(config, advancedConfig, remotePoseVisibleProxyDistance, migrateToAdvanced);
        enableScreenFallbackVisual = AdvancedConfigBinding.Move(config, advancedConfig, enableScreenFallbackVisual, migrateToAdvanced);
        screenFallbackSize = AdvancedConfigBinding.Move(config, advancedConfig, screenFallbackSize, migrateToAdvanced);
        presenceLostGraceSeconds = AdvancedConfigBinding.Move(config, advancedConfig, presenceLostGraceSeconds, migrateToAdvanced);
        floatingHeadFaceCamera = AdvancedConfigBinding.Move(config, advancedConfig, floatingHeadFaceCamera, migrateToAdvanced);
        pulseWhenSpeaking = AdvancedConfigBinding.Move(config, advancedConfig, pulseWhenSpeaking, migrateToAdvanced);
        speakingScaleMultiplier = AdvancedConfigBinding.Move(config, advancedConfig, speakingScaleMultiplier, migrateToAdvanced);
        speakingPulseSpeed = AdvancedConfigBinding.Move(config, advancedConfig, speakingPulseSpeed, migrateToAdvanced);
        minimumSpeakingVoiceLevel = AdvancedConfigBinding.Move(config, advancedConfig, minimumSpeakingVoiceLevel, migrateToAdvanced);
        speakingPulseAmount = AdvancedConfigBinding.Move(config, advancedConfig, speakingPulseAmount, migrateToAdvanced);
        voiceAttackSmoothTime = AdvancedConfigBinding.Move(config, advancedConfig, voiceAttackSmoothTime, migrateToAdvanced);
        voiceReleaseSmoothTime = AdvancedConfigBinding.Move(config, advancedConfig, voiceReleaseSmoothTime, migrateToAdvanced);
        silenceScaleMultiplier = AdvancedConfigBinding.Move(config, advancedConfig, silenceScaleMultiplier, migrateToAdvanced);
        amplitudeSmoothing = AdvancedConfigBinding.Move(config, advancedConfig, amplitudeSmoothing, migrateToAdvanced);
        destroyOnPresenceLost = AdvancedConfigBinding.Move(config, advancedConfig, destroyOnPresenceLost, migrateToAdvanced);
        debugVisualLifecycle = AdvancedConfigBinding.Move(config, advancedConfig, debugVisualLifecycle, migrateToAdvanced);
        nameTagScale = AdvancedConfigBinding.Move(config, advancedConfig, nameTagScale, migrateToAdvanced);
        nameTagHeightOffset = AdvancedConfigBinding.Move(config, advancedConfig, nameTagHeightOffset, migrateToAdvanced);
        nameTagMaxDistance = AdvancedConfigBinding.Move(config, advancedConfig, nameTagMaxDistance, migrateToAdvanced);
        nameTagUseGamePlayerNames = AdvancedConfigBinding.Move(config, advancedConfig, nameTagUseGamePlayerNames, migrateToAdvanced);
        nameTagUseFallbackIds = AdvancedConfigBinding.Move(config, advancedConfig, nameTagUseFallbackIds, migrateToAdvanced);
        debugNameTagLifecycle = AdvancedConfigBinding.Move(config, advancedConfig, debugNameTagLifecycle, migrateToAdvanced);
        thirdPersonHeight = AdvancedConfigBinding.Move(config, advancedConfig, thirdPersonHeight, migrateToAdvanced);
        fearModelTargetHeight = AdvancedConfigBinding.Move(config, advancedConfig, fearModelTargetHeight, migrateToAdvanced);
        fearModelScaleMultiplier = AdvancedConfigBinding.Move(config, advancedConfig, fearModelScaleMultiplier, migrateToAdvanced);
        fearModelPreviousKey = AdvancedConfigBinding.Move(config, advancedConfig, fearModelPreviousKey, migrateToAdvanced);
        fearModelNextKey = AdvancedConfigBinding.Move(config, advancedConfig, fearModelNextKey, migrateToAdvanced);
        fearSoundVolume = AdvancedConfigBinding.Move(config, advancedConfig, fearSoundVolume, migrateToAdvanced);
        fearSoundMinDistance = AdvancedConfigBinding.Move(config, advancedConfig, fearSoundMinDistance, migrateToAdvanced);
        fearSoundMaxDistance = AdvancedConfigBinding.Move(config, advancedConfig, fearSoundMaxDistance, migrateToAdvanced);
        fearSoundCooldownSeconds = AdvancedConfigBinding.Move(config, advancedConfig, fearSoundCooldownSeconds, migrateToAdvanced);
        fearSoundMaxNearbyPlayers = AdvancedConfigBinding.Move(config, advancedConfig, fearSoundMaxNearbyPlayers, migrateToAdvanced);
        advancedConfig.SaveOnConfigSet = advancedSaveOnConfigSet;
        advancedConfig.Save();
        config.SaveOnConfigSet = primarySaveOnConfigSet;
        config.Save();

        return new EnhancedSpectatorConfig(
            enableSpectatorModule,
            enableEnhancedSpectator,
            enableFreecam,
            freecamDefaultOn,
            freecamRadius,
            freecamMoveSpeed,
            freecamFastMoveMultiplier,
            freecamSlowMoveMultiplier,
            freecamLookSensitivity,
            freecamSmoothTime,
            clampCameraToRadius,
            recenterOnTargetSwitch,
            disableDuringGameOverOverride,
            toggleFreecamKey,
            recenterKey,
            resetToVanillaViewKey,
            fastMoveKey,
            slowMoveKey,
            ascendKey,
            descendKey,
            enableDebugLogging,
            enableNetworking,
            enableCapabilityHandshake,
            enableSpectatorTargetSync,
            enableSpectatorPoseSync,
            enableHostRelay,
            spectatorPoseSyncInterval,
            enableVoiceActivitySync,
            voiceActivitySyncInterval,
            voiceActivityStaleSeconds,
            debugVoiceActivitySync,
            enableSpectatorVoiceToTarget,
            spectatorVoiceAudienceMode,
            spectatorVoiceToTargetVolume,
            spectatorVoiceUseRemotePosePosition,
            spectatorVoiceEnableDistanceAttenuation,
            spectatorVoiceMinDistance,
            spectatorVoiceMaxDistance,
            spectatorVoiceRolloffPower,
            spectatorVoiceMinimumVolume,
            spectatorVoiceFallbackTo2DWhenPoseMissing,
            debugSpectatorVoiceRouting,
            repairVanillaConnectedPlayerState,
            repairVanillaPlayerNames,
            debugPlayerStateRepair,
            debugNetworkMessages,
            debugPoseMessages,
            enableSpectatorPresenceDebug,
            debugLogPresenceChanges,
            enableModelInspection,
            logLocalPlayerModelOnKey,
            logRemotePlayerModelsOnKey,
            modelInspectionKey,
            includeRendererBounds,
            includeMaterials,
            maxTransformDepth,
            enableRuntimeHeadSourceInspection,
            runtimeHeadSourceInspectionKey,
            runtimeHeadSourceIncludeRendererBounds,
            runtimeHeadSourceIncludeMaterials,
            runtimeHeadSourceMaxTransformDepth,
            enableVoiceDiagnostics,
            voiceDiagnosticsKey,
            logLocalVoiceStateOnKey,
            logRemoteVoiceStatesOnKey,
            includeVoiceAudioSourceDetails,
            includeWalkieVoiceDiagnostics,
            enableFloatingHeadVisuals,
            enablePlaceholderVisuals,
            useRuntimeDetachedHeadVisuals,
            runtimeDetachedHeadScale,
            runtimeDetachedHeadPitchOffset,
            runtimeDetachedHeadYawOffset,
            runtimeDetachedHeadRollOffset,
            fallbackToPlaceholderWhenDetachedHeadUnavailable,
            showRemoteSpectators,
            showOnlySpectatorsWatchingMe,
            showDeadSpectatorsToAlivePlayers,
            showDeadSpectatorsToDeadPlayers,
            maxFloatingHeadsVisible,
            visualStyle,
            placeholderScale,
            billboardSize,
            baseAlpha,
            useUnlitMaterial,
            enableDepthTest,
            floatingHeadRingRadius,
            floatingHeadHeightOffset,
            useCameraVisiblePlacement,
            cameraForwardOffset,
            remotePoseSmoothTime,
            keepRemotePoseInView,
            remotePoseVisibleProxyDistance,
            enableScreenFallbackVisual,
            screenFallbackSize,
            presenceLostGraceSeconds,
            floatingHeadFaceCamera,
            pulseWhenSpeaking,
            speakingScaleMultiplier,
            speakingPulseSpeed,
            minimumSpeakingVoiceLevel,
            speakingPulseAmount,
            voiceAttackSmoothTime,
            voiceReleaseSmoothTime,
            silenceScaleMultiplier,
            amplitudeSmoothing,
            destroyOnPresenceLost,
            debugVisualLifecycle,
            showNameTags,
            nameTagScale,
            nameTagHeightOffset,
            nameTagMaxDistance,
            nameTagUseGamePlayerNames,
            nameTagUseFallbackIds,
            debugNameTagLifecycle,
            configLanguage,
            useChineseText,
            enableThirdPerson,
            toggleThirdPersonKey,
            thirdPersonDistance,
            thirdPersonHeight,
            enableFearModeAsHost,
            renderFearModelsLocally,
            showFearModeQuickMenu,
            fearModelTargetHeight,
            fearModelUseOriginalScale,
            fearModelScaleMultiplier,
            fearModelPreviousKey,
            fearModelNextKey,
            fearSoundKey,
            fearSoundNextKey,
            fearSoundVolume,
            fearSoundMinDistance,
            fearSoundMaxDistance,
            fearSoundCooldownSeconds,
            fearSoundMaxNearbyPlayers);
    }
}
