using System;
using System.Collections.Generic;
using EnhancedSpectator.Logging;
using HarmonyLib;

namespace EnhancedSpectator.Patching;

/// <summary>
/// Coordinates Harmony patch module registration.
/// </summary>
public sealed class PatchBootstrapper : IDisposable
{
    private readonly Harmony _harmony = new Harmony(PluginMetadata.Guid);
    private readonly List<IPatchModule> _modules = new List<IPatchModule>
    {
        new SpectatorLifecyclePatchModule(),
        new SpectatePreviousPlayerCompatPatchModule(),
    };

    private bool _registered;

    /// <summary>
    /// Registers all patch modules.
    /// </summary>
    public void Register()
    {
        if (_registered)
        {
            return;
        }

        foreach (IPatchModule module in _modules)
        {
            module.Register(_harmony);
        }

        _registered = true;
        ModLog.Debug("Patch modules registered.");
    }

    /// <summary>
    /// Unregisters all patch modules.
    /// </summary>
    public void Dispose()
    {
        if (!_registered)
        {
            return;
        }

        for (int index = _modules.Count - 1; index >= 0; index--)
        {
            _modules[index].Unregister(_harmony);
        }

        _harmony.UnpatchSelf();
        _registered = false;
        ModLog.Debug("Patch modules unregistered.");
    }
}
