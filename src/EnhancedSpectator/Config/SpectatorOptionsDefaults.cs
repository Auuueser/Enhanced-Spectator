using System.Collections.Generic;
using BepInEx.Configuration;

namespace EnhancedSpectator.Config;

/// <summary>Batch reset for the visible options page; never resets unrelated config or another host.</summary>
public static class SpectatorOptionsDefaults
{
    /// <summary>Restores declared defaults and saves each affected configuration file once.</summary>
    public static void Reset(EnhancedSpectatorConfig config, bool isHost)
    {
        var entries = new List<ConfigEntryBase>
        {
            config.Camera.FirstPersonFov, config.Camera.FreecamDistance, config.ThirdPersonDistance,
            config.Camera.CinematicDistance, config.Camera.CinematicSpeed, config.Camera.CinematicStyle, config.FearModelScaleMultiplier,
            config.RenderFearModelsLocally, config.Camera.GhostVoiceMuted,
            config.Camera.SharedModelScale, config.Camera.RetractAtWalls, config.Camera.FadeModelsNearby,
            config.Camera.FadeRadius, config.Camera.FadeModelsWhileSpectating, config.Camera.HearOtherFearSounds, config.Camera.RepairPlayerNames, config.Camera.ShowKeyHints
        };
        if (isHost) entries.Add(config.EnableFearModeAsHost);
        var files = new Dictionary<ConfigFile, bool>();
        foreach (var entry in entries)
            if (!files.ContainsKey(entry.ConfigFile)) files.Add(entry.ConfigFile, entry.ConfigFile.SaveOnConfigSet);
        try
        {
            foreach (var file in files.Keys) file.SaveOnConfigSet = false;
            foreach (var entry in entries) entry.BoxedValue = entry.DefaultValue;
        }
        finally
        {
            foreach (var pair in files) { pair.Key.SaveOnConfigSet = pair.Value; pair.Key.Save(); }
        }
    }
}
