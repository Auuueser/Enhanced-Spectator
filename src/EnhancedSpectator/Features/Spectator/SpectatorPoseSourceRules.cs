namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Selects the camera pose source used for spectator pose synchronization.
/// </summary>
public static class SpectatorPoseSourceRules
{
    /// <summary>
    /// Gets whether the enhanced freecam pose should be preferred over the vanilla spectator camera.
    /// </summary>
    public static bool ShouldUseFreecamPose(bool freecamActive, bool freecamHasWorldPose)
    {
        return freecamActive && freecamHasWorldPose;
    }

    /// <summary>
    /// Gets whether the vanilla spectator camera pose is valid for pose synchronization.
    /// </summary>
    public static bool ShouldUseVanillaSpectatorPose(bool hasSpectateCamera, bool isSpectateCameraActive)
    {
        return hasSpectateCamera && isSpectateCameraActive;
    }

    /// <summary>
    /// Gets whether an active spectator pose should be published.
    /// </summary>
    public static bool ShouldPublishSpectatorPose(
        bool isLocalPlayerDead,
        bool hasSpectatedTarget,
        bool useFreecamPose,
        bool useVanillaSpectatorPose)
    {
        return isLocalPlayerDead
            && hasSpectatedTarget
            && (useFreecamPose || useVanillaSpectatorPose);
    }

    /// <summary>
    /// Gets whether any active spectator pose can reuse the watched player's locally smoothed moving frame.
    /// </summary>
    public static bool ShouldCaptureTargetMotionReference(bool hasPose, bool hasSpectatedTarget)
    {
        return hasPose && hasSpectatedTarget;
    }
}
