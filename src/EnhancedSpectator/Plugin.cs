using BepInEx;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Patching;
using EnhancedSpectator.Runtime;
using UnityEngine;

namespace EnhancedSpectator;

/// <summary>
/// BepInEx entry point for Enhanced Spectator.
/// </summary>
[BepInPlugin(PluginMetadata.Guid, PluginMetadata.Name, PluginMetadata.Version)]
[BepInDependency(LCChineseProjectDetection.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    private FeatureBootstrapper? _featureBootstrapper;
    private PatchBootstrapper? _patchBootstrapper;
    private bool _applicationQuitting;
    private bool _shutdownComplete;

    private void Awake()
    {
        RuntimeConnectionState.Reset();
        ModLog.Initialize(Logger);
        ModLog.Info($"{PluginMetadata.Name} {PluginMetadata.Version} starting.");
        Application.quitting += OnApplicationQuitting;

        EnhancedSpectatorConfig config = EnhancedSpectatorConfig.Bind(Config);
        ModLog.SetDebugEnabled(config.EnableDebugLogging.Value);
        if (config.EnableDebugLogging.Value)
        {
            PluginDiagnostics.LogPluginBinaryHash(Info.Location);
        }

        _featureBootstrapper = new FeatureBootstrapper(config);
        _featureBootstrapper.Initialize();

        _patchBootstrapper = new PatchBootstrapper();
        _patchBootstrapper.Register();

        EnhancedSpectatorRuntimeDriver.Install(_featureBootstrapper, Shutdown);

        ModLog.Info("Local spectator freecam initialized.");
    }

    private void OnDestroy()
    {
        if (!_applicationQuitting)
        {
            EnhancedSpectatorRuntimeDriver.EnsureInstalled();
            ModLog.Warning("Plugin component destroyed before application quit; preserving runtime driver and patches.");
            return;
        }

        Shutdown();
    }

    private void OnApplicationQuitting()
    {
        _applicationQuitting = true;
        RuntimeConnectionState.MarkApplicationQuitting();
        Shutdown();
    }

    private void Shutdown()
    {
        if (_shutdownComplete)
        {
            return;
        }

        _shutdownComplete = true;
        RuntimeConnectionState.MarkPluginShuttingDown();
        Application.quitting -= OnApplicationQuitting;
        EnhancedSpectatorRuntimeDriver.BeginShutdown();

        _patchBootstrapper?.Dispose();
        _featureBootstrapper?.Dispose();

        ModLog.Info("Local spectator freecam shut down.");
    }
}
