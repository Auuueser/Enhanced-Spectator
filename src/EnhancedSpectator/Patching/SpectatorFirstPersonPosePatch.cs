using EnhancedSpectator.GameInterop;
using GameNetcodeStuff;
using HarmonyLib;

namespace EnhancedSpectator.Patching;

[HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.IsInSpecialAnimationClientRpc))]
internal static class SpectatorToolPosePatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerControllerB __instance, bool specialAnimation, float timed, bool climbingLadder) =>
        LethalCompanyFirstPersonPose.ObserveTool(__instance, specialAnimation, timed, climbingLadder);
}

[HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.UpdatePlayerRotationClientRpc))]
internal static class SpectatorLookPosePatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerControllerB __instance, short newRot) => LethalCompanyFirstPersonPose.ObserveRotation(__instance, newRot);
}
