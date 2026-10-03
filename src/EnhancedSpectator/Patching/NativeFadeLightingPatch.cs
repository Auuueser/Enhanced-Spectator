using EnhancedSpectator.GameInterop;
using HarmonyLib;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering.HighDefinition;

namespace EnhancedSpectator.Patching;

// V81 signatures confirmed from its installed HDRP assembly. Publicized direct
// access; no reflection and no global quality/optimizer changes.
[HarmonyPatch(typeof(HDRenderPipeline), nameof(HDRenderPipeline.RenderForwardOpaque))]
internal static class NativeFadeLightingCapturePatch
{
    [HarmonyPrefix]
    private static void Prefix(HDCamera hdCamera, in HDRenderPipeline.BuildGPULightListOutput lightLists,
        in HDRenderPipeline.LightingBuffers lightingBuffers, in HDRenderPipeline.PrepassOutput prepassOutput, ShadowResult shadowResult) =>
        NativeFadePass.CaptureLighting(hdCamera, in lightLists, in lightingBuffers, in prepassOutput, in shadowResult);
}

[HarmonyPatch(typeof(CustomPass), nameof(CustomPass.ReadRenderTargets))]
internal static class NativeFadeLightingRetentionPatch
{
    [HarmonyPrefix]
    private static void Prefix(CustomPass __instance, in RenderGraphBuilder builder)
    {
        // Only our pass is extended. Other volumes retain their own resource declarations.
        if (__instance is NativeFadePass fade) fade.RetainLighting(builder);
    }
}
