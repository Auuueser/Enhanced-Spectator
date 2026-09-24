using UnityEngine;

namespace EnhancedSpectator.GameInterop;

internal static class LethalCompanyWatchedBody
{
    internal static bool TryGetBounds(ulong? clientId, ulong? slotId, out Bounds bounds)
    {
        bounds = default;
        var round = StartOfRound.Instance;
        if (round == null || round.allPlayerScripts == null || (!clientId.HasValue && !slotId.HasValue)) return false;
        foreach (var player in round.allPlayerScripts)
        {
            if (player == null || !player.isPlayerControlled || player.isPlayerDead || player.disconnectedMidGame) continue;
            bool matches = clientId.HasValue ? player.actualClientId == clientId.Value : player.playerClientId == slotId;
            if (!matches) continue;
            if (player.playerCollider != null) bounds = player.playerCollider.bounds;
            if (bounds.size.sqrMagnitude <= .0001f && player.thisPlayerModel != null) bounds = player.thisPlayerModel.bounds;
            return bounds.size.sqrMagnitude > .0001f;
        }
        return false;
    }

    internal static Quaternion AimShotgun(Vector3 modelPosition, ulong? clientId, ulong? slotId, Quaternion fallback)
    {
        Quaternion upright = Quaternion.Euler(0f, 0f, 90f);
        if (!TryGetBounds(clientId, slotId, out var body)) return fallback * upright;
        Vector3 direction = body.center - modelPosition;
        // Confirmed ShotgunItem.shotgunRayPoint has identity local rotation: muzzle is local +Z.
        return (direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction, Vector3.up) : fallback) * upright;
    }
}
