using System;
using EnhancedSpectator.Runtime;

namespace EnhancedSpectator.Features.VoiceRouting;

/// <summary>
/// Owns the lifecycle of the local routed-ghost mute state.
/// </summary>
public sealed class SpectatorVoiceMuteModule : IFeatureModule
{
    private readonly SpectatorVoiceMuteState _state;

    /// <summary>
    /// Creates the local mute-state lifecycle module.
    /// </summary>
    public SpectatorVoiceMuteModule(SpectatorVoiceMuteState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <inheritdoc />
    public void Initialize()
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _state.Reset();
    }
}
