using EnhancedSpectator.GameInterop;
using HarmonyLib;
using UnityEngine.Rendering.HighDefinition;

namespace EnhancedSpectator.Patching;

// Confirmed in V81 HDRP: resolved per-camera volume stack refresh happens inside HDCamera.Update.
[HarmonyPatch(typeof(HDCamera), "UpdateVolumeAndPhysicalParameters")]
internal static class SpectatorPresentationPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix(HDCamera __instance) => LethalCompanySpectatorPresentation.BeforeVolumeUpdate(__instance);
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix(HDCamera __instance) => LethalCompanySpectatorPresentation.AfterVolumeUpdate(__instance);
}

[HarmonyPatch(typeof(HDCamera), "Update")]
internal static class SpectatorPresentationFramesPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    private static void Prefix(HDCamera __instance, ref FrameSettings currentFrameSettings) =>
        LethalCompanySpectatorPresentation.Prepare(__instance,ref currentFrameSettings);
}
