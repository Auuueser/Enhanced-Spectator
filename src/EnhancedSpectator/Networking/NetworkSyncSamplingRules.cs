using UnityEngine;

namespace EnhancedSpectator.Networking;

/// <summary>
/// Pure rules for throttling network state provider sampling.
/// </summary>
public static class NetworkSyncSamplingRules
{
    /// <summary>
    /// Returns whether a provider should be sampled at the current time.
    /// </summary>
    public static bool ShouldSample(bool hasObservedState, float now, float nextSampleTime)
    {
        return !hasObservedState || now >= nextSampleTime;
    }

    /// <summary>
    /// Resolves the next provider sample time after a successful sample attempt.
    /// </summary>
    public static float ResolveNextSampleTime(float now, float intervalSeconds)
    {
        return now + Mathf.Max(0f, intervalSeconds);
    }

    /// <summary>
    /// Resolves the voice activity sync interval using the protocol minimum.
    /// </summary>
    public static float ResolveVoiceActivitySyncInterval(float configuredSeconds)
    {
        return Mathf.Max((float)ModNetworkConstants.VoiceActivitySyncMinIntervalSeconds, configuredSeconds);
    }

    /// <summary>
    /// Returns whether an unchanged active state should be refreshed for lossy transport recovery.
    /// </summary>
    public static bool ShouldRefreshUnchangedState(bool stateIsActive, bool hasSentState, float now, float nextRefreshTime)
    {
        return stateIsActive && hasSentState && now >= nextRefreshTime;
    }
}
