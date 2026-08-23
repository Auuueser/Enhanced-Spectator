using System;
using EnhancedSpectator.Runtime;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Runtime module for the optional fear-mode protocol.
/// </summary>
public sealed class FearModeModule : IFeatureModule, IRuntimeTickable
{
    private readonly FearModeNetworkService _service;
    private bool _initialized;

    /// <summary>Creates the module.</summary>
    public FearModeModule(FearModeNetworkService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <inheritdoc />
    public void Initialize()
    {
        _initialized = true;
    }

    /// <inheritdoc />
    public void Tick()
    {
        if (_initialized)
        {
            _service.Tick();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        _service.Dispose();
        _initialized = false;
    }
}
