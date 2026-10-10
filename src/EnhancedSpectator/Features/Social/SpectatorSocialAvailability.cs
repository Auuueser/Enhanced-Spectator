namespace EnhancedSpectator.Features.Social;

/// <summary>
/// Which social features this game has, for the key hints: bets when the host turned them on, reviews when the
/// host runs this mod, emotes also through the Steam lobby. The social module sets them every frame.
/// </summary>
internal static class SpectatorSocialAvailability
{
    internal static bool Emotes { get; set; }
    internal static bool Bets { get; set; }
    internal static bool Reviews { get; set; }
}
