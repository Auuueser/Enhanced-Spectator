using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;

namespace EnhancedSpectator.Config;

/// <summary>
/// Detects LC Chinese Project by its stable BepInEx plugin GUID without depending on its version.
/// </summary>
public static class LCChineseProjectDetection
{
    /// <summary>
    /// Stable BepInEx GUID published by LC Chinese Project.
    /// </summary>
    public const string PluginGuid = "Aueser.LCChineseProject";

    /// <summary>
    /// Gets whether LC Chinese Project is registered in the current BepInEx chainloader.
    /// </summary>
    public static bool IsInstalled()
    {
        return Chainloader.PluginInfos != null && Chainloader.PluginInfos.ContainsKey(PluginGuid);
    }

    /// <summary>
    /// Pure GUID matching helper used by tests and alternate plugin registries.
    /// </summary>
    public static bool ContainsPluginGuid(IEnumerable<string>? pluginGuids)
    {
        if (pluginGuids == null)
        {
            return false;
        }

        foreach (string pluginGuid in pluginGuids)
        {
            if (string.Equals(pluginGuid, PluginGuid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
