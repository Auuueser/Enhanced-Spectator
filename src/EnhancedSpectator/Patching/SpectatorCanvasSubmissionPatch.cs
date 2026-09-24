using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using HarmonyLib;
using UnityEngine;

namespace EnhancedSpectator.Patching;

// Confirmed in V81's UnityEngine.UIModule: this managed entry invokes all willRenderCanvases subscribers.
// Suppression runs after their alpha/active/layout writes and before native UI geometry submission.
[HarmonyPatch(typeof(Canvas), "SendWillRenderCanvases")]
internal static class SpectatorCanvasSubmissionPatch
{
    private static int _nextWarning;
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix() => LethalCompanySpectatorUiVisibility.RestoreRenderers();
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix()
    {
        try
        {
            LethalCompanySpectatorHudAdapter.BeforeSubmit();
            LethalCompanySpectatorUiVisibility.BeforeSubmit();
        }
        catch (System.Exception ex)
        {
            LethalCompanySpectatorUiVisibility.Clear();
            if (Time.frameCount >= _nextWarning)
            { _nextWarning = Time.frameCount + 300; ModLog.Warning("Spectator HUD render boundary restored after failure: " + ex.Message); }
        }
    }
}
