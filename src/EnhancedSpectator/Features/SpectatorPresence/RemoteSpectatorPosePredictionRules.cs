using UnityEngine;

namespace EnhancedSpectator.Features.SpectatorPresence;

/// <summary>
/// Pure rules for short, bounded remote-pose prediction between network samples.
/// </summary>
public static class RemoteSpectatorPosePredictionRules
{
    /// <summary>Maximum time that a received velocity may advance a pose without a newer sample.</summary>
    public const float MaximumPredictionSeconds = 0.12f;

    /// <summary>Maximum accepted presentation velocity, guarding against teleports and invalid samples.</summary>
    public const float MaximumVelocity = 80f;

    /// <summary>Resolves a stable velocity from two received position samples.</summary>
    public static Vector3 ResolveVelocity(
        Vector3 previousPosition,
        Vector3 currentPosition,
        float sampleDeltaTime,
        Vector3 previousVelocity,
        bool hadVelocity)
    {
        if (sampleDeltaTime <= 0.0001f || sampleDeltaTime > 1f)
        {
            return Vector3.zero;
        }

        Vector3 measuredVelocity = (currentPosition - previousPosition) / sampleDeltaTime;
        if (measuredVelocity.sqrMagnitude > MaximumVelocity * MaximumVelocity)
        {
            return Vector3.zero;
        }

        if (measuredVelocity.sqrMagnitude <= 0.0025f)
        {
            return Vector3.zero;
        }

        return hadVelocity
            ? Vector3.Lerp(previousVelocity, measuredVelocity, 0.65f)
            : measuredVelocity;
    }

    /// <summary>Advances the latest position sample for a bounded amount of time.</summary>
    public static Vector3 ResolvePredictedPosition(
        Vector3 sampledPosition,
        Vector3 velocity,
        float secondsSinceSample)
    {
        float predictionSeconds = Mathf.Clamp(secondsSinceSample, 0f, MaximumPredictionSeconds);
        return sampledPosition + (velocity * predictionSeconds);
    }
}
