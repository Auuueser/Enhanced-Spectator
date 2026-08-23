using System;
using EnhancedSpectator.Runtime;

namespace EnhancedSpectator.Features.FloatingHead;

/// <summary>
/// Runtime module for the local self-ghost third-person visual.
/// </summary>
public sealed class LocalSpectatorAvatarModule : IFeatureModule, IRuntimeLateTickable
{
    private readonly LocalSpectatorAvatarVisualService _service;
    private bool _initialized;

    /// <summary>
    /// Creates the module.
    /// </summary>
    public LocalSpectatorAvatarModule(LocalSpectatorAvatarVisualService service)
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
