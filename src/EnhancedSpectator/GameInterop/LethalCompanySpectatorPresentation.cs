using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Logging;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace EnhancedSpectator.GameInterop;

internal static class LethalCompanySpectatorPresentation
{
    private static EnhancedSpectatorConfig? _config;
    // One grade scope per camera: the large view and each split-screen small view own separate volume stacks.
    private static readonly Dictionary<HDCamera, SpectatorGradeScope> Scopes = new();
    private static readonly List<HDCamera> Stale = new();
    private static bool _reportedFailure;
    internal static int ThermalPalette => _config!.Camera.ThermalPalette.Value;
    internal static float ThermalStrength => _config!.Camera.ThermalStrength.Value;
    internal static bool ThermalEnabled => _config?.Camera.MonitorInfrared.Value==true && _config.EnableEnhancedSpectator.Value;
    /// <summary>Thermal on every split-screen view, not only the enhanced large view.</summary>
    internal static bool ThermalAllViews => ThermalEnabled && SplitScreenCameraContext.Active && _config!.Camera.SplitScreen.ThermalAllViews.Value;
    internal static bool ThermalRequested(Camera camera) => SpectatorCameraRules.UseMonitorInfrared(_config?.Camera.MonitorInfrared.Value==true,
        Eligible(camera) || ThermalAllViews && SplitScreenCameraContext.IsView(camera),
        SpectatorFreecamController.Current?.State.Mode??SpectatorCameraMode.Freecam);
    // Brightness balancing applies to every split-screen view at the large view's standard.
    private static bool Graded(Camera camera) => Eligible(camera) || _config?.EnableEnhancedSpectator.Value==true && SplitScreenCameraContext.IsView(camera);
    internal static void Configure(EnhancedSpectatorConfig config) { Clear(); _config=config; }
    private static bool Eligible(Camera camera)
    {
        if (SplitScreenCameraContext.Active)
        {
            // The enlarged view, whatever its camera (vanilla included); the grid only with thermal on all views.
            if (!SplitScreenCameraContext.IsPrimary(camera) || !SplitScreenCameraContext.Focused) return false;
            if (SplitScreenCameraContext.Preview) return _config?.EnableEnhancedSpectator.Value == true
                && SplitScreenCameraContext.Target is { isPlayerDead: false, isPlayerControlled: true };
        }
        var round=StartOfRound.Instance;
        var local=round!=null ? round.localPlayerController : null;
        return _config?.EnableEnhancedSpectator.Value==true && round!=null && camera!=null
            && camera==round.spectateCamera && camera==round.activeCamera && local!=null && local.isPlayerDead
            && local.spectatedPlayerScript!=null && !local.spectatedPlayerScript.isPlayerDead;
    }
    internal static void Prepare(HDCamera camera, ref FrameSettings settings)
    {
        if(!Graded(camera.camera)) return;
        // Respect the post-processing master graphics switch. Only enable requested per-camera subeffects.
        if(!settings.IsEnabled(FrameSettingsField.Postprocess)) return;
        var c=_config!.Camera;
        if(c.BalanceSpectatorBrightness.Value)
            settings.SetEnabled(FrameSettingsField.ColorGrading,true);
    }
    internal static void BeforeVolumeUpdate(HDCamera camera)
    { if(Scopes.TryGetValue(camera,out var scope)) scope.Restore(); }
    internal static void AfterVolumeUpdate(HDCamera camera)
    {
        if(!Graded(camera.camera)) return;
        try
        {
            if(!Scopes.TryGetValue(camera,out var scope) || !ReferenceEquals(scope.Stack,camera.volumeStack))
            { scope?.Restore(); Scopes[camera]=scope=new SpectatorGradeScope(camera.volumeStack); }
            var c=_config!.Camera;

            scope.Apply(!SpectatorThermalPass.Requested(camera.camera) && c.BalanceSpectatorBrightness.Value ? c.SpectatorBrightness.Value : 0,
                0, 0, 0, 5, true);
        }
        catch(Exception ex)
        {
            Clear();
            if(!_reportedFailure) { _reportedFailure=true; ModLog.Warning($"Spectator image effects unavailable: {ex.GetType().Name}."); }
        }
    }
    internal static void EndCamera(Camera camera)
    { foreach(var pair in Scopes) if(pair.Key.camera==camera) { pair.Value.Restore(); return; } }
    internal static void Tick()
    {
        LethalCompanyCameraTransition.Tick(); SpectatorThermalPass.Tick();
        // Small views are only "views" inside their own Render call, so keep their scopes while the split-screen runs.
        Stale.Clear();
        foreach(var pair in Scopes)
            if(pair.Key.camera==null || !SplitScreenCameraContext.Active && !Eligible(pair.Key.camera)) Stale.Add(pair.Key);
        foreach(var camera in Stale) { Scopes[camera].Restore(); Scopes.Remove(camera); }
    }
    internal static void Clear() { foreach(var scope in Scopes.Values) scope.Restore(); Scopes.Clear(); }
}
