using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using GameNetcodeStuff;
using EnhancedSpectator.Logging;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Render-scoped first-person visibility restoration entry point.</summary>
internal static class LethalCompanyFirstPersonVisibility
{
    private static readonly List<Renderer> Renderers = new List<Renderer>();
    private static readonly FirstPersonRenderScope Scope = new FirstPersonRenderScope();
    private static PlayerControllerB? _target;
    private static int _refreshFrame;

    internal static void HideForSpectatorCamera(Camera camera)
    {
        Restore();
        var round = StartOfRound.Instance;
        var target = round != null ? round.localPlayerController?.spectatedPlayerScript : null;
        if (round == null || camera != round.spectateCamera || target == null || target.isPlayerDead) return;
        try
        {
            if (_target != target || Time.frameCount >= _refreshFrame)
            {
                _target = target;
                _refreshFrame = Time.frameCount + 30;
                Renderers.Clear();
                Add(target.thisPlayerModel);
                Add(target.thisPlayerModelLOD1);
                Add(target.thisPlayerModelLOD2);
                if (target.headCostumeContainer != null)
                    foreach (var renderer in target.headCostumeContainer.GetComponentsInChildren<Renderer>(true)) Add(renderer);
                if (Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"))
                    MoreCompanyCosmeticAdapter.CopyRenderers(target, Renderers);
            }
            Scope.Begin(Renderers, target.thisPlayerModelArms, camera);
        }
        catch (Exception ex)
        {
            Restore();
            _refreshFrame = Time.frameCount + 300;
            ModLog.Warning($"First-person visibility unavailable: {ex.GetType().Name}.");
        }
    }

    private static void Add(Renderer? renderer)
    {
        if (renderer != null && !Renderers.Contains(renderer)) Renderers.Add(renderer);
    }

    internal static void Restore() => Scope.Restore();

    internal static void Clear()
    {
        LethalCompanyFirstPersonPose.Clear();
        Restore();
        Renderers.Clear();
        _target = null;
    }
}
