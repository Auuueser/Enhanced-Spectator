namespace EnhancedSpectator.Features.VoiceRouting;

/// <summary>
/// Stores the local, non-networked routed-ghost mute state.
/// </summary>
public sealed class SpectatorVoiceMuteState : ISpectatorVoiceMuteState
{
    /// <inheritdoc />
    public bool IsMuted { get; private set; }

    /// <summary>
    /// Toggles the local mute state and returns the new value.
    /// </summary>
    public bool Toggle()
    {
        IsMuted = !IsMuted;
        return IsMuted;
    }

    /// <summary>
    /// Clears the local mute state during shutdown.
    /// </summary>
    public void Reset()
    {
        IsMuted = false;
    }
}

/// <summary>
/// Pure routing gate for local routed spectator voice.
/// </summary>
public static class SpectatorVoiceMuteRules
{
    /// <summary>
    /// Gets whether route evaluation may continue.
    /// </summary>
    public static bool ShouldEvaluateRoutes(bool featureEnabled, bool isMuted)
    {
        return featureEnabled && !isMuted;
    }
}
