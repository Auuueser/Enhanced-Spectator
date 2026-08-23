using System;
using EnhancedSpectator.Runtime;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Runtime module for validated fear visuals.
/// </summary>
public sealed class FearModeVisualModule : IFeatureModule, IRuntimeLateTickable
{
    private readonly FearModeVisualService _service;
    private bool _initialized;

    /// <summary>Creates the module.</summary>
    public FearModeVisualModule(FearModeVisualService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <inheritdoc />
    public void Initialize()
    {
        _initialized = true;
    }

    /// <inheritdoc />
    public void LateTick()
    {
        if (_initialized)
        {
            _service.LateTick();
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
