namespace EnhancedSpectator.Features.FearMode;

/// <summary>Pure local rules for limiting simultaneously audible nearby fear-sound origins.</summary>
public static class FearSoundAudibilityRules
{
    /// <summary>Restores from the configured volume, never from a muted AudioSource's cached zero value.</summary>
    public static float OutputVolume(float configuredVolume, bool ownSound, bool hearOthers, int closerPlayers, int maximumAudiblePlayers)
    {
        if (ShouldMute(ownSound, hearOthers, closerPlayers, maximumAudiblePlayers)) return 0f;
        if (float.IsNaN(configuredVolume) || float.IsInfinity(configuredVolume)) return 1f;
        return System.Math.Max(0f, System.Math.Min(1f, configuredVolume));
    }
    /// <summary>Recomputed each frame; reception and nearby limits cannot leave a source permanently muted.</summary>
    public static bool ShouldMute(bool ownSound, bool hearOthers, int closerPlayers, int maximumAudiblePlayers)
        => !FearSoundRules.ShouldHear(ownSound, hearOthers) || !IsAudible(closerPlayers, maximumAudiblePlayers);

    /// <summary>Gets whether a source with this many closer players remains audible.</summary>
    public static bool IsAudible(int closerPlayerCount, int maximumAudiblePlayers)
    {
        return maximumAudiblePlayers <= 0 || closerPlayerCount < maximumAudiblePlayers;
    }
}
