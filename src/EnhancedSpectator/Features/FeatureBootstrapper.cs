using System;
using System.Collections.Generic;
using EnhancedSpectator.Features.FloatingHead;
using EnhancedSpectator.Features.FearMode;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.ModelInspection;
using EnhancedSpectator.Features.PlayerStateSync;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Features.SpectatorPresence;
using EnhancedSpectator.Features.VoiceActivity;
using EnhancedSpectator.Features.VoiceDiagnostics;
using EnhancedSpectator.Features.VoiceRouting;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Networking;
using EnhancedSpectator.Runtime;
using UnityEngine;

namespace EnhancedSpectator.Features;

/// <summary>
/// Coordinates feature module creation and lifetime.
/// </summary>
public sealed class FeatureBootstrapper : IDisposable
{
    private readonly List<IFeatureModule> _features = new List<IFeatureModule>();
    private readonly FeatureRuntimeDispatchLists _runtimeDispatchLists = new FeatureRuntimeDispatchLists();
    private bool _initialized;

    /// <summary>
    /// Creates the configured feature modules.
    /// </summary>
    public FeatureBootstrapper(EnhancedSpectatorConfig config)
    {
        if (config.EnableSpectatorModule.Value)
        {
            IGameSpectatorAdapter gameSpectatorAdapter = new LethalCompanySpectatorAdapter();
            SpectatorFreecamSettings freecamSettings = new SpectatorFreecamSettings(config);
            SpectatorModule spectatorModule = new SpectatorModule(gameSpectatorAdapter, freecamSettings);
            FearVisualOverrideRegistry fearVisualOverrides = new FearVisualOverrideRegistry();
            RemoteSpectatorPosePresentationService posePresentationService =
                new RemoteSpectatorPosePresentationService(gameSpectatorAdapter);
            _features.Add(spectatorModule);
            _runtimeDispatchLists.AddTickable(spectatorModule);
            _runtimeDispatchLists.AddLateTickable(spectatorModule);
            _runtimeDispatchLists.AddCameraPreCullTickable(spectatorModule);

            LocalSpectatorAvatarModule? localAvatarModule = null;
            if (config.EnableThirdPerson.Value)
            {
                localAvatarModule = new LocalSpectatorAvatarModule(
                    new LocalSpectatorAvatarVisualService(
                        config,
                        spectatorModule,
                        gameSpectatorAdapter,
                        new PlaceholderHeadVisualFactory(),
                        fearVisualOverrides,
                        new LethalCompanyDetachedHeadVisualSourceAdapter()));
            }

            SpectatorDisconnectTargetSwitchService disconnectTargetSwitchService =
                new SpectatorDisconnectTargetSwitchService(new LethalCompanySpectatorTargetSwitchAdapter());
            _features.Add(disconnectTargetSwitchService);
            _runtimeDispatchLists.AddTickable(disconnectTargetSwitchService);

            if (config.EnableNetworking.Value)
            {
                LethalCompanyVoiceActivityProvider voiceActivityProvider = new LethalCompanyVoiceActivityProvider();
                EnhancedSpectatorNetworkService networkService = new EnhancedSpectatorNetworkService(
                    config,
                    spectatorModule,
                    spectatorModule,
                    spectatorModule,
                    voiceActivityProvider,
                    new UnityNetcodeMessagingTransport(() => config.DebugNetworkMessages.Value));
                NetworkingModule networkingModule = new NetworkingModule(networkService);
                _features.Add(networkingModule);
                _runtimeDispatchLists.AddTickable(networkingModule);

                ConnectedPlayerStateRepairModule playerStateRepairModule = new ConnectedPlayerStateRepairModule(
                    config,
                    networkService,
                    new LethalCompanyConnectedPlayerStateRepairAdapter());
                _features.Add(playerStateRepairModule);
                _runtimeDispatchLists.AddTickable(playerStateRepairModule);

                SpectatorPresenceService presenceService = new SpectatorPresenceService(
                    config,
                    gameSpectatorAdapter,
                    networkService);
                SpectatorPresenceModule presenceModule = new SpectatorPresenceModule(presenceService);
                _features.Add(presenceModule);
                _runtimeDispatchLists.AddTickable(presenceModule);

                LethalCompanyFearModeAdapter fearModeAdapter = new LethalCompanyFearModeAdapter();
                FearModelCatalog fearModelCatalog = new FearModelCatalog(fearModeAdapter);
                FearModeNetworkService fearModeNetworkService = new FearModeNetworkService(
                    config,
                    fearModeAdapter,
                    fearModelCatalog);
                FearModeModule fearModeModule = new FearModeModule(fearModeNetworkService);
                _features.Add(fearModeModule);
                _runtimeDispatchLists.AddTickable(fearModeModule);

                FearSoundModule fearSoundModule = new FearSoundModule(
                    config,
                    fearModeNetworkService,
                    fearModeAdapter,
                    spectatorModule,
                    networkService,
                    posePresentationService);
                _features.Add(fearSoundModule);
                _runtimeDispatchLists.AddTickable(fearSoundModule);
                _runtimeDispatchLists.AddLateTickable(fearSoundModule);

                SpectatorVoiceMuteState voiceMuteState = new SpectatorVoiceMuteState();
                FearModelThumbnailService fearThumbnailService = new FearModelThumbnailService(
                    fearModelCatalog,
                    fearModeAdapter,
                    new RuntimeEnemyVisualFactory(),
                    new LethalCompanyDetachedHeadVisualSourceAdapter());
                FearModeQuickMenuModule fearQuickMenuModule = new FearModeQuickMenuModule(
                    config,
                    fearModeNetworkService,
                    fearModeAdapter,
                    new LethalCompanyFearQuickMenuAdapter(fearThumbnailService),
                    fearSoundModule,
                    fearThumbnailService,
                    voiceMuteState);
                _features.Add(fearQuickMenuModule);
                _runtimeDispatchLists.AddTickable(fearQuickMenuModule);

                SpectatorVoiceMuteModule voiceMuteModule = new SpectatorVoiceMuteModule(voiceMuteState);
                _features.Add(voiceMuteModule);

                SpectatorVoiceRoutingModule voiceRoutingModule = new SpectatorVoiceRoutingModule(
                    new SpectatorVoiceRoutingService(
                        config,
                        networkService,
                        new LethalCompanySpectatorVoiceRoutingAdapter(
                            networkService,
                            () => ModLog.IsDebugEnabled
                                && config.EnableDebugLogging.Value
                                && config.DebugSpectatorVoiceRouting.Value),
                        voiceMuteState));
                _features.Add(voiceRoutingModule);
                _runtimeDispatchLists.AddLateTickable(voiceRoutingModule);

                FearModeVisualModule fearVisualModule = new FearModeVisualModule(
                    new FearModeVisualService(
                        config,
                        fearModeNetworkService,
                        fearModelCatalog,
                        presenceService,
                        spectatorModule,
                        fearModeAdapter,
                        fearVisualOverrides,
                        new RuntimeEnemyVisualFactory(),
                        posePresentationService));
                _features.Add(fearVisualModule);
                _runtimeDispatchLists.AddLateTickable(fearVisualModule);

                if (config.EnableFloatingHeadVisuals.Value)
                {
                    FloatingHeadVisualService visualService = new FloatingHeadVisualService(
                        config,
                        presenceService,
                        voiceActivityProvider,
                        networkService,
                        new LethalCompanyDetachedHeadVisualSourceAdapter(),
                        new FloatingHeadPlacementService(gameSpectatorAdapter),
                        new PlaceholderHeadVisualFactory(),
                        fearVisualOverrides,
                        posePresentationService);
                    FloatingHeadModule floatingHeadModule = new FloatingHeadModule(visualService);
                    _features.Add(floatingHeadModule);
                    _runtimeDispatchLists.AddLateTickable(floatingHeadModule);
                    _runtimeDispatchLists.AddCameraPreCullTickable(floatingHeadModule);
                    _runtimeDispatchLists.AddGuiTickable(floatingHeadModule);
                }
            }

            if (localAvatarModule != null)
            {
                _features.Add(localAvatarModule);
                _runtimeDispatchLists.AddLateTickable(localAvatarModule);
            }
        }

        if (config.EnableModelInspection.Value)
        {
            ModelInspectionModule modelInspectionModule = new ModelInspectionModule(
                config,
                new PlayerModelInspectionService(config, new LethalCompanyPlayerModelInspectionAdapter()));
            _features.Add(modelInspectionModule);
            _runtimeDispatchLists.AddTickable(modelInspectionModule);
        }

        if (config.EnableRuntimeHeadSourceInspection.Value)
        {
            DeadBodyHeadSourceInspectionModule headSourceInspectionModule = new DeadBodyHeadSourceInspectionModule(
                config,
                new DeadBodyHeadSourceInspectionService(
                    config,
                    new LethalCompanyDeadBodyHeadSourceInspectionAdapter()));
            _features.Add(headSourceInspectionModule);
            _runtimeDispatchLists.AddTickable(headSourceInspectionModule);
        }

        if (config.EnableVoiceDiagnostics.Value)
        {
            VoiceDiagnosticsModule voiceDiagnosticsModule = new VoiceDiagnosticsModule(
                config,
                new VoiceDiagnosticsService(
                    config,
                    new LethalCompanyVoiceDiagnosticsAdapter()));
            _features.Add(voiceDiagnosticsModule);
            _runtimeDispatchLists.AddTickable(voiceDiagnosticsModule);
        }
    }

    /// <summary>
    /// Initializes all configured feature modules.
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        foreach (IFeatureModule feature in _features)
        {
            feature.Initialize();
        }

        _initialized = true;
        ModLog.Debug("Feature modules initialized.");
    }

    /// <summary>
    /// Ticks runtime feature modules during Unity Update.
    /// </summary>
    public void Tick()
    {
        if (!_initialized)
        {
            return;
        }

        _runtimeDispatchLists.TickAll();
    }

    /// <summary>
    /// Ticks runtime feature modules during Unity LateUpdate.
    /// </summary>
    public void LateTick()
    {
        if (!_initialized)
        {
            return;
        }

        _runtimeDispatchLists.LateTickAll();
    }

    /// <summary>
    /// Ticks runtime feature modules immediately before Unity renders a camera.
    /// </summary>
    public void CameraPreCullTick(Camera camera)
    {
        if (!_initialized)
        {
            return;
        }

        _runtimeDispatchLists.CameraPreCullTickAll(camera);
    }

    /// <summary>
    /// Ticks runtime feature modules during Unity OnGUI.
    /// </summary>
    public void GuiTick()
    {
        if (!_initialized)
        {
            return;
        }

        _runtimeDispatchLists.GuiTickAll();
    }

    /// <summary>
    /// Disposes all configured feature modules in reverse order.
    /// </summary>
    public void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        for (int index = _features.Count - 1; index >= 0; index--)
        {
            _features[index].Dispose();
        }

        _initialized = false;
        ModLog.Debug("Feature modules disposed.");
    }
}
