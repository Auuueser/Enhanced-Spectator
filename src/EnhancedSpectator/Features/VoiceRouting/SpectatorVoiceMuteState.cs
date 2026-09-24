using BepInEx.Configuration;

namespace EnhancedSpectator.Features.VoiceRouting;

/// <summary>
/// Stores the local, non-networked routed-ghost mute state.
/// </summary>
public sealed class SpectatorVoiceMuteState : ISpectatorVoiceMuteState
{
    private readonly ConfigEntry<bool>? _preference;
    private bool _muted;
    /// <summary>Creates local state with an optional persisted preference.</summary>
    public SpectatorVoiceMuteState(ConfigEntry<bool>? preference = null) => _preference = preference;
    /// <inheritdoc />
    public bool IsMuted => _preference?.Value ?? _muted;

    /// <summary>
    /// Toggles the local mute state and returns the new value.
    /// </summary>
    public bool Toggle()
    {
        _muted = !IsMuted;
        if (_preference != null) _preference.Value = _muted;
        return IsMuted;
    }

    /// <summary>
    /// Clears the local mute state during shutdown.
    /// </summary>
    public void Reset()
    {
        _muted = false;
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
