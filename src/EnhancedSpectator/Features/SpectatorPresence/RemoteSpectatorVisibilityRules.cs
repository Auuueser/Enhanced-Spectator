using EnhancedSpectator.Networking;

namespace EnhancedSpectator.Features.SpectatorPresence;

/// <summary>
/// Centralizes local rules for deciding which remote spectators should be visible.
/// </summary>
public static class RemoteSpectatorVisibilityRules
{
    /// <summary>Gets whether a target state belongs to another network client.</summary>
    public static bool IsRemoteSpectator(SpectatorTargetState target, ulong localClientId)
    {
        return target.LocalClientId != localClientId;
    }

    /// <summary>
    /// Gets whether the remote spectator target state points at the local player.
    /// </summary>
    public static bool IsWatchingLocalPlayer(
        SpectatorTargetState remoteTarget,
        ulong localClientId,
        ulong localPlayerSlotId)
    {
        if (!remoteTarget.IsSpectating || !IsRemoteSpectator(remoteTarget, localClientId))
        {
            return false;
        }

        if (remoteTarget.TargetClientId.HasValue)
        {
            return remoteTarget.TargetClientId.Value == localClientId;
        }

        return remoteTarget.TargetPlayerSlotId.HasValue
            && remoteTarget.TargetPlayerSlotId.Value == localPlayerSlotId;
    }

    /// <summary>
    /// Gets whether a remote spectator should be included in local placeholder visibility.
    /// </summary>
    public static bool ShouldShowRemoteSpectator(
        SpectatorTargetState remoteTarget,
        bool isWatchingLocalPlayer,
        bool hasMatchingPose,
        bool localPlayerIsDeadOrSpectating,
        bool showRemoteSpectators,
        bool showOnlySpectatorsWatchingMe,
        bool showDeadSpectatorsToAlivePlayers,
        bool showDeadSpectatorsToDeadPlayers)
    {
        if (!remoteTarget.IsSpectating || !showRemoteSpectators || !hasMatchingPose)
        {
            return false;
        }

        if (showOnlySpectatorsWatchingMe && !isWatchingLocalPlayer)
        {
            return false;
        }

        if (localPlayerIsDeadOrSpectating)
        {
            return showDeadSpectatorsToDeadPlayers;
        }

        return showDeadSpectatorsToAlivePlayers;
    }
}
