using UnityEngine;

namespace EnhancedSpectator.Networking;

/// <summary>
/// Pure rules for temporarily increasing remote-pose sampling during fast world motion.
/// </summary>
public static class RemoteSpectatorPoseNetworkRateRules
{
    /// <summary>Fast sampling interval used to fill vehicle and other high-speed motion.</summary>
    public const float BurstIntervalSeconds = 0.05f;

    /// <summary>World speed above which pose sends may temporarily use the burst interval.</summary>
    public const float BurstSpeedThreshold = 6f;

    /// <summary>Resolves a sampling interval that can detect fast motion without increasing idle packet traffic.</summary>
    public static float ResolveSamplingInterval(float configuredSendInterval)
    {
        return Mathf.Min(Mathf.Max(0.02f, configuredSendInterval), BurstIntervalSeconds);
    }

    /// <summary>Gets whether two samples represent fast non-reference world motion.</summary>
    public static bool ShouldUseBurstRate(
        SpectatorPoseState? previous,
        SpectatorPoseState current,
        float sampleDeltaTime)
    {
        if (previous == null
            || current == null
            || sampleDeltaTime <= 0.0001f
            || !previous.IsSpectating
            || !current.IsSpectating
            || previous.LocalClientId != current.LocalClientId
            || previous.TargetClientId != current.TargetClientId)
        {
            return false;
        }

        Vector3 previousComparablePosition;
        Vector3 currentComparablePosition;
        if (previous.HasTargetMotionReference || current.HasTargetMotionReference)
        {
            if (!previous.HasTargetMotionReference || !current.HasTargetMotionReference)
            {
                return false;
            }

            previousComparablePosition = previous.TargetMotionReferenceLocalPosition;
            currentComparablePosition = current.TargetMotionReferenceLocalPosition;
        }
        else
        {
            if (previous.HasMotionReference || current.HasMotionReference)
            {
                return false;
            }

            previousComparablePosition = previous.Position;
            currentComparablePosition = current.Position;
        }

        float speed = Vector3.Distance(previousComparablePosition, currentComparablePosition) / sampleDeltaTime;
        return speed >= BurstSpeedThreshold;
    }
}
