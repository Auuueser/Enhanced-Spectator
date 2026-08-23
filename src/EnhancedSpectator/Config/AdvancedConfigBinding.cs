using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace EnhancedSpectator.Config;

/// <summary>
/// Moves advanced entries out of the primary config while preserving live ConfigEntry references.
/// </summary>
internal static class AdvancedConfigBinding
{
    private const string AdvancedFileName = "Auuueser.EnhancedSpectator.Advanced.cfg";

    public static ConfigFile Create(string? overridePath, out bool migrateFromPrimary)
    {
        string path = string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(Paths.ConfigPath, AdvancedFileName)
            : Path.GetFullPath(overridePath);
        migrateFromPrimary = !File.Exists(path) || new FileInfo(path).Length == 0;
        return new ConfigFile(path, saveOnInit: true);
    }

    public static ConfigEntry<T> Move<T>(
        ConfigFile primaryConfig,
        ConfigFile advancedConfig,
        ConfigEntry<T> sourceEntry,
        bool migrateFromPrimary)
    {
        ConfigEntry<T> advancedEntry = advancedConfig.Bind(
            sourceEntry.Definition,
            sourceEntry.Value,
            sourceEntry.Description);
        if (migrateFromPrimary)
        {
            advancedEntry.Value = sourceEntry.Value;
        }
        else
        {
            sourceEntry.Value = advancedEntry.Value;
        }

        bool synchronizing = false;
        sourceEntry.SettingChanged += (_, _) =>
        {
            if (synchronizing)
            {
                return;
            }

            synchronizing = true;
            try
            {
                advancedEntry.Value = sourceEntry.Value;
            }
            finally
            {
                synchronizing = false;
            }
        };
        advancedEntry.SettingChanged += (_, _) =>
        {
            if (synchronizing)
            {
                return;
            }

            synchronizing = true;
            try
            {
                sourceEntry.Value = advancedEntry.Value;
            }
            finally
            {
                synchronizing = false;
            }
        };

        primaryConfig.Remove(sourceEntry.Definition);
        return sourceEntry;
    }
}
