using System;
using EnhancedSpectator.Features;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Runtime;

namespace EnhancedSpectator.Networking;

/// <summary>
/// Feature module that owns Enhanced Spectator networking lifetime.
/// </summary>
public sealed class NetworkingModule : IFeatureModule, IRuntimeAfterLateTickable
{
    private readonly IEnhancedSpectatorNetworkService _networkService;
    private bool _initialized;

    /// <summary>
    /// Creates a networking module.
    /// </summary>
    public NetworkingModule(IEnhancedSpectatorNetworkService networkService)
    {
        _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
    }

    /// <inheritdoc />
    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _networkService.Initialize();
        _initialized = true;
        ModLog.Debug("Networking module initialized.");
    }

    /// <inheritdoc />
    public void AfterLateTick()
    {
        if (!_initialized)
        {
            return;
        }

        _networkService.Tick();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        _networkService.Dispose();
        _initialized = false;
        ModLog.Debug("Networking module disposed.");
    }
}
