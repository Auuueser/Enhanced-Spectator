using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Pure feed-forward rules for keeping camera state in a moving target reference frame.
/// </summary>
public static class SpectatorCameraMotionCompensationRules
{
    /// <summary>
    /// Gets whether an anchor delta should reset smoothing instead of being fed forward.
    /// </summary>
    public static bool ShouldResetForAnchorDelta(
        Vector3 previousAnchorPosition,
        Vector3 currentAnchorPosition,
        bool hasPreviousAnchor,
        float teleportThreshold)
    {
        if (!hasPreviousAnchor)
        {
            return false;
        }

        float safeThreshold = Mathf.Max(0f, teleportThreshold);
        return safeThreshold > 0f
            && (currentAnchorPosition - previousAnchorPosition).sqrMagnitude > safeThreshold * safeThreshold;
    }

    /// <summary>
    /// Applies deterministic anchor translation to a previously smoothed representation position.
    /// </summary>
    public static Vector3 ApplyAnchorTranslation(
        Vector3 smoothedPosition,
        Vector3 previousAnchorPosition,
        Vector3 currentAnchorPosition,
        bool hasPreviousAnchor,
        float teleportThreshold)
    {
        if (!hasPreviousAnchor)
        {
            return smoothedPosition;
        }

        if (ShouldResetForAnchorDelta(
            previousAnchorPosition,
            currentAnchorPosition,
            hasPreviousAnchor,
            teleportThreshold))
        {
            return smoothedPosition;
        }

        return smoothedPosition + (currentAnchorPosition - previousAnchorPosition);
    }
}
