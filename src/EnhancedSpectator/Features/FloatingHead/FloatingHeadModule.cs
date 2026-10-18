using System;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Runtime;
using UnityEngine;

namespace EnhancedSpectator.Features.FloatingHead;

/// <summary>
/// Feature module for local placeholder visuals representing remote spectators.
/// </summary>
public sealed class FloatingHeadModule :
    IFeatureModule,
    IRuntimeAfterLateTickable,
    IRuntimeCameraPreCullTickable,
    IRuntimeGuiTickable
{
    private readonly FloatingHeadVisualService _visualService;
    private bool _initialized;

    /// <summary>
    /// Creates a floating-head placeholder module.
    /// </summary>
    public FloatingHeadModule(FloatingHeadVisualService visualService)
    {
        _visualService = visualService ?? throw new ArgumentNullException(nameof(visualService));
    }

    /// <inheritdoc />
    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        ModLog.Debug("Floating-head placeholder module initialized.");
    }

    /// <inheritdoc />
    public void AfterLateTick()
    {
        if (!_initialized)
        {
            return;
        }

        _visualService.LateTick();
    }

    /// <inheritdoc />
    public void CameraPreCullTick(Camera camera)
    {
        if (!_initialized)
        {
            return;
        }

        _visualService.CameraPreCullTick(camera);
    }

    /// <inheritdoc />
    public void GuiTick()
    {
        if (!_initialized)
        {
            return;
        }

        _visualService.GuiTick();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        _visualService.Dispose();
        _initialized = false;
        ModLog.Debug("Floating-head placeholder module disposed.");
    }
}
