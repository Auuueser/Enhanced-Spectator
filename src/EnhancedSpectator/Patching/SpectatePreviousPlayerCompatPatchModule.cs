using System;
using System.Reflection;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Logging;
using HarmonyLib;

namespace EnhancedSpectator.Patching;

/// <summary>
/// SpectatePreviousPlayer (kerotein, 1.0.3) spectates the previous player on every press of the ping-scan button
/// (right mouse / R-trigger) while dead, from its HUDManager.Update postfix. In the live split-screen that button holds
/// the early-leave vote and a view is chosen by clicking it, so each press switched the large view. Its switch is
/// skipped there only; outside split-screen it works as it does. The mod is found by its assembly, loaded before or
/// after this one.
/// </summary>
internal sealed class SpectatePreviousPlayerCompatPatchModule : IPatchModule
{
    private const string AssemblyName = "SpectatePreviousPlayer", TypeName = "SPP.patch.Patch", MethodName = "SpectatePreviousPlayer";
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
        var method = assembly.GetType(TypeName)?.GetMethod(MethodName, BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null)
        {
            ModLog.Warning("SpectatePreviousPlayer is installed without its known right-click switch; in split-screen its right-click may still change the large view.");
            return true;
        }
        _harmony!.Patch(method, prefix: new HarmonyMethod(typeof(SpectatePreviousPlayerCompatPatchModule).GetMethod(nameof(SkipInSplitScreen), BindingFlags.Static | BindingFlags.NonPublic)));
        ModLog.Info("SpectatePreviousPlayer found: its right-click switch is left to the early-leave vote in split-screen.");
        return true;
    }

    private static bool SkipInSplitScreen() => !SpectatorVanillaInputGuard.SplitScreenActive;
}
