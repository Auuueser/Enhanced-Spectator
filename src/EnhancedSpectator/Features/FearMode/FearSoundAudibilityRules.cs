namespace EnhancedSpectator.Features.FearMode;

/// <summary>Pure local rules for limiting simultaneously audible nearby fear-sound origins.</summary>
public static class FearSoundAudibilityRules
{
    /// <summary>Gets whether a source with this many closer players remains audible.</summary>
    public static bool IsAudible(int closerPlayerCount, int maximumAudiblePlayers)
    {
        return maximumAudiblePlayers <= 0 || closerPlayerCount < maximumAudiblePlayers;
    }
}
