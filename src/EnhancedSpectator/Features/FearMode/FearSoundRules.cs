using System;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Pure validation rules for player-owned fear sounds.
/// </summary>
public static class FearSoundRules
{
    /// <summary>Gets whether a host may authorize one requested sound.</summary>
    public static bool CanHostAccept(
        bool isHost,
        bool sessionEnabled,
        ulong senderClientId,
        ulong originClientId,
        bool originIsDead,
        string selectedModelKey,
        string requestedModelKey,
        FearSoundAction action,
        int clipIndex,
        int clipCount,
        long nowTicks,
        long lastAcceptedTicks,
        float cooldownSeconds)
    {
        float effectiveCooldown = action == FearSoundAction.PlayNext
            ? Math.Min(Math.Max(0f, cooldownSeconds), 0.1f)
            : Math.Max(0f, cooldownSeconds);
        long cooldownTicks = (long)(effectiveCooldown * TimeSpan.TicksPerSecond);
        bool commonAuthority = isHost
            && sessionEnabled
            && senderClientId == originClientId
            && originIsDead
            && string.Equals(selectedModelKey, requestedModelKey, StringComparison.Ordinal);
        if (!commonAuthority)
        {
            return false;
        }

        if (action == FearSoundAction.Stop)
        {
            return clipIndex == -1;
        }

        return (action == FearSoundAction.Play || action == FearSoundAction.PlayNext)
            && clipIndex >= 0
            && clipIndex < clipCount
            && (lastAcceptedTicks <= 0 || nowTicks - lastAcceptedTicks >= cooldownTicks);
    }
}
