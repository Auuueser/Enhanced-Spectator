using EnhancedSpectator.Features.VoiceRouting;
using EnhancedSpectator.Networking;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Applies local vanilla voice playback changes for spectator voice experiments.
/// </summary>
public interface IGameSpectatorVoiceRoutingAdapter
{
    /// <summary>
    /// Gets whether the local player can receive spectator-to-target voice.
    /// </summary>
    bool TryGetLocalVoiceReceiverState(
        out bool hasLocalPlayer,
        out bool isLocalPlayerDead,
        out ulong localClientId,
        out ulong localPlayerSlotId);

    /// <summary>
    /// Makes one remote dead spectator audible to the local watched player.
    /// </summary>
    bool TryApplySpectatorVoiceRoute(
        ulong spectatorClientId,
        ulong spectatorSlotId,
        SpectatorPoseState? poseState,
        SpectatorVoicePlaybackSettings settings,
        out string reason);

    /// <summary>
    /// Clears one remote spectator voice route and restores the previous local playback state when available.
    /// </summary>
    void ClearSpectatorVoiceRoute(ulong spectatorClientId, ulong spectatorSlotId);

    /// <summary>
    /// Clears cached lookup state that is only useful while routes are active.
    /// </summary>
    void ClearCachedVoiceRouteLookups();
}
