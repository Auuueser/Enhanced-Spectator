using GameNetcodeStuff;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Extends the confirmed V81 pivot raycast distance without changing target selection.</summary>
internal static class LethalCompanyVanillaDistance
{
    internal static bool TryApply(PlayerControllerB player, float distance)
    {
        var round = StartOfRound.Instance;
        if (round == null || round.localPlayerController != player || !player.isPlayerDead
            || round.overrideSpectateCamera || player.isInGameOverAnimation > 0f
            || player.spectatedPlayerScript == null || player.spectatedPlayerScript.isPlayerDead
            || player.spectateCameraPivot == null || round.spectateCamera == null) return false;
        // Preserve vanilla's 0.1m ray extension and 0.25m wall margin.
        var pivot = player.spectateCameraPivot;
        var ray = new Ray(pivot.position, -pivot.forward);
        float actual = Physics.Raycast(ray, out var hit, distance + .1f, player.walkableSurfacesNoPlayersMask, QueryTriggerInteraction.Ignore)
            ? Mathf.Max(0f, hit.distance - .25f) : distance;
        var camera = round.spectateCamera.transform;
        camera.position = ray.GetPoint(actual);
        if (actual > .0001f) camera.LookAt(pivot); else camera.rotation = pivot.rotation;
        return true;
    }
}
