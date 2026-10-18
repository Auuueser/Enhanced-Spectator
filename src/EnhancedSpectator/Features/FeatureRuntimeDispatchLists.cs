using System;
using System.Collections.Generic;
using EnhancedSpectator.Runtime;
using UnityEngine;

namespace EnhancedSpectator.Features;

/// <summary>
/// Stores runtime feature callbacks grouped by Unity phase.
/// </summary>
public sealed class FeatureRuntimeDispatchLists
{
    private readonly List<IRuntimeTickable> _tickables = new List<IRuntimeTickable>();
    private readonly List<IRuntimeLateTickable> _lateTickables = new List<IRuntimeLateTickable>();
    private readonly List<IRuntimeAfterLateTickable> _afterLateTickables = new List<IRuntimeAfterLateTickable>();
    private readonly List<IRuntimeCameraPreCullTickable> _cameraPreCullTickables = new List<IRuntimeCameraPreCullTickable>();
    private readonly List<IRuntimeGuiTickable> _guiTickables = new List<IRuntimeGuiTickable>();

    /// <summary>
    /// Gets features that receive Unity Update callbacks.
    /// </summary>
    public IReadOnlyList<IRuntimeTickable> Tickables => _tickables;

    /// <summary>
    /// Gets features that receive Unity LateUpdate callbacks.
    /// </summary>
    public IReadOnlyList<IRuntimeLateTickable> LateTickables => _lateTickables;

    /// <summary>Callbacks after every game script's LateUpdate, with camera solving before networking.</summary>
    public IReadOnlyList<IRuntimeAfterLateTickable> AfterLateTickables => _afterLateTickables;

    /// <summary>Adds a callback after every game script's LateUpdate.</summary>
    public void AddAfterLateTickable(IRuntimeAfterLateTickable tickable)
        => _afterLateTickables.Add(tickable ?? throw new ArgumentNullException(nameof(tickable)));

    /// <summary>
    /// Dispatches completed-camera callbacks in registration order. The camera, models, split-screen views and
    /// networking share this phase: one module failing must not stop the ones after it (reported once each).
    /// </summary>
    public void AfterLateTickAll()
    {
        foreach (var tickable in _afterLateTickables)
        {
            try { tickable.AfterLateTick(); }
            catch (Exception ex)
            {
                if (_failedAfterLate.Add(tickable)) Logging.ModLog.Error($"{tickable.GetType().Name} failed after LateUpdate: {ex}");
            }
        }
    }
    private readonly HashSet<IRuntimeAfterLateTickable> _failedAfterLate = new HashSet<IRuntimeAfterLateTickable>();

    /// <summary>
    /// Gets features that receive Unity camera pre-cull callbacks.
    /// </summary>
    public IReadOnlyList<IRuntimeCameraPreCullTickable> CameraPreCullTickables => _cameraPreCullTickables;

    /// <summary>
    /// Gets features that receive Unity OnGUI callbacks.
    /// </summary>
    public IReadOnlyList<IRuntimeGuiTickable> GuiTickables => _guiTickables;

    /// <summary>
    /// Adds a Unity Update callback.
    /// </summary>
    public void AddTickable(IRuntimeTickable tickable)
    {
        if (tickable == null)
        {
            throw new ArgumentNullException(nameof(tickable));
        }

        _tickables.Add(tickable);
    }

    /// <summary>
    /// Adds a Unity LateUpdate callback.
    /// </summary>
    public void AddLateTickable(IRuntimeLateTickable lateTickable)
    {
        if (lateTickable == null)
        {
            throw new ArgumentNullException(nameof(lateTickable));
        }

        _lateTickables.Add(lateTickable);
    }

    /// <summary>
    /// Adds a Unity camera pre-cull callback.
    /// </summary>
    public void AddCameraPreCullTickable(IRuntimeCameraPreCullTickable cameraPreCullTickable)
    {
        if (cameraPreCullTickable == null)
        {
            throw new ArgumentNullException(nameof(cameraPreCullTickable));
        }

        _cameraPreCullTickables.Add(cameraPreCullTickable);
    }

    /// <summary>
    /// Adds a Unity OnGUI callback.
    /// </summary>
    public void AddGuiTickable(IRuntimeGuiTickable guiTickable)
    {
        if (guiTickable == null)
        {
            throw new ArgumentNullException(nameof(guiTickable));
        }

        _guiTickables.Add(guiTickable);
    }

    /// <summary>
    /// Dispatches Unity Update callbacks.
    /// </summary>
    public void TickAll()
    {
        for (int index = 0; index < _tickables.Count; index++)
        {
            _tickables[index].Tick();
        }
    }

    /// <summary>
    /// Dispatches Unity LateUpdate callbacks.
    /// </summary>
    public void LateTickAll()
    {
        for (int index = 0; index < _lateTickables.Count; index++)
        {
            _lateTickables[index].LateTick();
        }
    }

    /// <summary>
    /// Dispatches Unity camera pre-cull callbacks.
    /// </summary>
    public void CameraPreCullTickAll(Camera camera)
    {
        for (int index = 0; index < _cameraPreCullTickables.Count; index++)
        {
            _cameraPreCullTickables[index].CameraPreCullTick(camera);
        }
    }

    /// <summary>
    /// Dispatches Unity OnGUI callbacks.
    /// </summary>
    public void GuiTickAll()
    {
        for (int index = 0; index < _guiTickables.Count; index++)
        {
            _guiTickables[index].GuiTick();
        }
    }
}
