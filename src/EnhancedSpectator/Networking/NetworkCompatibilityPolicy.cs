namespace EnhancedSpectator.Networking;

/// <summary>
/// Pure policy for deciding whether mod-owned business sync can run after capability probing.
/// </summary>
public static class NetworkCompatibilityPolicy
{
    /// <summary>
    /// Resolves the network lifecycle state from capability probe and handshake readiness.
    /// </summary>
    public static NetworkLifecycleState ResolveLifecycleState(
        bool targetSyncReady,
        bool capabilitySent,
        float capabilityProbeSentRealtime,
        float currentRealtime,
        float noCompatiblePeerTimeoutSeconds)
    {
        if (targetSyncReady)
        {
            return NetworkLifecycleState.TransportRegistered;
        }

        if (capabilitySent
            && capabilityProbeSentRealtime >= 0f
            && currentRealtime - capabilityProbeSentRealtime >= noCompatiblePeerTimeoutSeconds)
        {
            return NetworkLifecycleState.NoCompatiblePeerLocalOnly;
        }

        return NetworkLifecycleState.TransportRegistered;
    }

    /// <summary>
    /// Gets whether target, pose, identity, and voice activity sync should run.
    /// </summary>
    public static bool ShouldRunBusinessSync(NetworkLifecycleState lifecycleState, bool targetSyncReady)
    {
        return targetSyncReady && lifecycleState == NetworkLifecycleState.TransportRegistered;
    }

    /// <summary>
    /// Gets whether a capability probe should be sent again while no compatible peer has answered.
    /// </summary>
    public static bool ShouldRetryCapabilityProbe(
        bool targetSyncReady,
        bool capabilitySent,
        float capabilityProbeSentRealtime,
        float currentRealtime,
        float retryIntervalSeconds)
    {
        return !targetSyncReady
            && capabilitySent
            && capabilityProbeSentRealtime >= 0f
            && retryIntervalSeconds > 0f
            && currentRealtime - capabilityProbeSentRealtime >= retryIntervalSeconds;
    }
}
