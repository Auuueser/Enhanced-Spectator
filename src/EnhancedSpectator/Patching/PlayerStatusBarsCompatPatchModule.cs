using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using GameNetcodeStuff;
using HarmonyLib;

namespace EnhancedSpectator.Patching;

/// <summary>
/// Player_Status_Bars (Aueser, 0.2.x) shows a bar within its display distance of the game's active camera, which in the
/// split-screen is only the large view's (or the heard one's): bars near a small view's player went missing there, and
/// bars near the large view showed in small views far away. In the live split-screen its distance is taken from the
/// nearest split-screen camera instead, so a bar near any view is shown and kept up to date; each view then hides the
/// ones far from its own camera (<see cref="SplitScreenForeignBillboards"/>). Outside split-screen it works as it does.
/// The mod is found by its assembly, loaded before or after this one; its setting by its plugin id once it has bound it.
/// </summary>
internal sealed class PlayerStatusBarsCompatPatchModule : IPatchModule
{
    private const string AssemblyName = "PlayerStatusBars", TypeName = "OtherPlayerStatusBars.PlayerStatusBarView",
        MethodName = "ShouldShowForDistance", PluginGuid = "playerstatusbars";
    private static ConfigEntry<float>? _distance;
    private Harmony? _harmony;

    public void Register(Harmony harmony)
    {
        _harmony = harmony;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (TryPatch(assembly)) return;
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
    }

    public void Unregister(Harmony harmony) => AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;

    private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
    {
        if (TryPatch(args.LoadedAssembly)) AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
    }

    // True once the mod's assembly has been dealt with.
    private bool TryPatch(Assembly assembly)
    {
        if (assembly.GetName().Name != AssemblyName) return false;
        var method = assembly.GetType(TypeName)?.GetMethod(MethodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            ModLog.Warning("Player_Status_Bars is installed without its known distance check; in split-screen its bars keep to the large view's distance.");
            return true;
        }
        _harmony!.Patch(method, prefix: new HarmonyMethod(typeof(PlayerStatusBarsCompatPatchModule).GetMethod(nameof(NearestSplitCamera), BindingFlags.Static | BindingFlags.NonPublic)));
        SplitScreenForeignBillboards.MaxDistance = () => TryDistance(out float distance) ? distance : float.PositiveInfinity;
        ModLog.Info("Player_Status_Bars found: in split-screen each view shows the bars near its own camera.");
        return true;
    }

    // The mod binds its setting in its Awake, after its assembly has loaded: looked up when first needed.
    private static bool TryDistance(out float distance)
    {
        if (_distance == null && Chainloader.PluginInfos.TryGetValue(PluginGuid, out var plugin) && plugin.Instance != null)
            plugin.Instance.Config.TryGetEntry("General", "Max Display Distance", out _distance);
        distance = _distance?.Value ?? 0;
        return _distance != null;
    }

    private static bool NearestSplitCamera(PlayerControllerB ___targetPlayer, ref float distanceSqr, ref bool __result)
    {
        if (!SpectatorVanillaInputGuard.SplitScreenActive || !TryDistance(out float distance)
            || !SplitScreenForeignBillboards.TryNearestCamera(___targetPlayer.transform.position, out float nearest)) return true;
        distanceSqr = nearest;
        __result = nearest <= distance * distance;
        return false;
    }
}
