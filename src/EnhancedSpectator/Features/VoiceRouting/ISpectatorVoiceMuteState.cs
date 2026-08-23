namespace EnhancedSpectator.Features.VoiceRouting;

/// <summary>
/// Exposes the local listener's Enhanced Spectator routed-voice mute state.
/// </summary>
public interface ISpectatorVoiceMuteState
{
    /// <summary>
    /// Gets whether Enhanced Spectator routed ghost voice is locally muted.
    /// </summary>
    bool IsMuted { get; }
}

internal sealed class UnmutedSpectatorVoiceState : ISpectatorVoiceMuteState
{
    public static UnmutedSpectatorVoiceState Instance { get; } = new UnmutedSpectatorVoiceState();

    private UnmutedSpectatorVoiceState()
    {
    }

    public bool IsMuted => false;
}
