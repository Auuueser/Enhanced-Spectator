using System;
using BepInEx.Configuration;
using EnhancedSpectator.Features.Spectator;

namespace EnhancedSpectator.Config;

internal static class SpectatorDistanceMigration
{
    internal static void Apply(ConfigFile advanced, ConfigEntry<float> radius)
    {
        var revision = advanced.Bind("Compatibility", "DistanceLimitRevision", 0, "Internal one-time distance-limit migration marker.");
        if (revision.Value >= 1) return;
        if (Math.Abs(radius.Value - 8f) < .0001f) radius.Value = SpectatorCameraRules.DefaultTravelRadius;
        revision.Value = 1;
    }
}
