using System;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Logging;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace EnhancedSpectator.GameInterop;

internal static class LethalCompanySpectatorPresentation
{
    private static EnhancedSpectatorConfig? _config;
    private static HDCamera? _owner;
    private static SpectatorGradeScope? _scope;
    private static bool _reportedFailure;
    internal static bool ThermalRequested(Camera camera) => SpectatorCameraRules.UseMonitorInfrared(_config?.Camera.MonitorInfrared.Value==true,Eligible(camera),
        SpectatorFreecamController.Current?.State.Mode??SpectatorCameraMode.Freecam);
    internal static void Configure(EnhancedSpectatorConfig config) { Clear(); _config=config; }
    private static bool Eligible(Camera camera)
    {
        var round=StartOfRound.Instance;
        var local=round!=null ? round.localPlayerController : null;
        return _config?.EnableEnhancedSpectator.Value==true && round!=null && camera!=null
            && camera==round.spectateCamera && camera==round.activeCamera && local!=null && local.isPlayerDead
            && local.spectatedPlayerScript!=null && !local.spectatedPlayerScript.isPlayerDead;
    }
    internal static void Prepare(HDCamera camera, ref FrameSettings settings)
    {
        if(!Eligible(camera.camera)) return;
        // Respect the post-processing master graphics switch. Only enable requested per-camera subeffects.
        if(!settings.IsEnabled(FrameSettingsField.Postprocess)) return;
        var c=_config!.Camera;
        if(c.BalanceSpectatorBrightness.Value)
            settings.SetEnabled(FrameSettingsField.ColorGrading,true);
    }
    internal static void BeforeVolumeUpdate(HDCamera camera)
    { if(ReferenceEquals(_owner,camera)) _scope?.Restore(); }
    internal static void AfterVolumeUpdate(HDCamera camera)
    {
        if(!Eligible(camera.camera)) return;
        try
        {
            if(!ReferenceEquals(_owner,camera) || _scope==null || !ReferenceEquals(_scope.Stack,camera.volumeStack))
            { Clear(); _owner=camera; _scope=new SpectatorGradeScope(camera.volumeStack); }
            var c=_config!.Camera;

            _scope.Apply(!SpectatorThermalPass.Requested(camera.camera) && c.BalanceSpectatorBrightness.Value ? c.SpectatorBrightness.Value : 0,
                0, 0, 0, 5, true);
        }
        catch(Exception ex)
        {
            Clear();
            if(!_reportedFailure) { _reportedFailure=true; ModLog.Warning($"Spectator image effects unavailable: {ex.GetType().Name}."); }
        }
    }
    internal static void EndCamera(Camera camera) { if(_owner?.camera==camera) _scope?.Restore(); }
    internal static void Tick() { LethalCompanyCameraTransition.Tick(); SpectatorThermalPass.Tick(); if(_owner!=null && !Eligible(_owner.camera)) Clear(); }
    internal static void Clear() { _scope?.Restore(); _scope=null; _owner=null; }
}
