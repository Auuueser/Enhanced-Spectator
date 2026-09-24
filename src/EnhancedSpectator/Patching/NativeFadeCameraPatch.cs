using EnhancedSpectator.GameInterop;
using HarmonyLib;
using UnityEngine.Rendering.HighDefinition;

namespace EnhancedSpectator.Patching;

// Signatures confirmed against V81 HDRP: HDCamera.Update receives a value-copy of
// aggregated FrameSettings, before custom-pass culling and beginCameraRendering.
[HarmonyPatch(typeof(HDCamera), "Update")]
internal static class NativeFadeCameraPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    private static void Prefix(HDCamera __instance, ref FrameSettings currentFrameSettings) =>
        NativeFadePass.PrepareCamera(__instance, ref currentFrameSettings);
}

[HarmonyPatch(typeof(CustomPass), "WillBeExecuted")]
internal static class NativeFadeOtherPassPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    private static bool Prefix(CustomPass __instance, HDCamera hdCamera, ref bool __result)
    {
        if (!NativeFadePass.SuppressOtherPass(__instance, hdCamera)) return true;
        __result = false;
        return false;
    }
}
