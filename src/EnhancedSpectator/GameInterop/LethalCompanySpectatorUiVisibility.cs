using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Logging;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Suppresses submitted HUD geometry after every Canvas callback, without altering third-party logic.</summary>
internal sealed class LethalCompanySpectatorUiVisibility : IGameSpectatorHudAdapter
{
    private static LethalCompanySpectatorUiVisibility? _current;
    private readonly EnhancedSpectatorConfig _config;
    private readonly SpectatorHudVisibilityState _state = new SpectatorHudVisibilityState();
    private readonly LethalCompanySpectatorAdapter _input = new LethalCompanySpectatorAdapter();
    private readonly SpectatorCanvasRenderScope _scope = new SpectatorCanvasRenderScope();
    private readonly HashSet<Canvas> _roots = new HashSet<Canvas>();
    internal static bool Hidden => _current?._state.Effective == true;
    internal static bool Requested => _current?._state.Requested == true;
    internal static void SetRequested(bool value) => _current?._state.SetRequested(value);
    internal LethalCompanySpectatorUiVisibility(EnhancedSpectatorConfig config) { _config = config; }

    public void UpdateLayout()
    {
        _current = this;
        RestoreRenderers();
        var round = StartOfRound.Instance;
        var player = round != null ? round.localPlayerController : null;
        bool spectating = _config.EnableEnhancedSpectator.Value && player != null && player.isPlayerDead
            && player.isInGameOverAnimation <= 0 && !round!.overrideSpectateCamera
            && player.spectatedPlayerScript != null && !player.spectatedPlayerScript.isPlayerDead;
        bool blocked = !spectating || _input.IsCameraInputBlocked();
        bool wasRequested = _state.Requested;
        _state.Update(spectating, blocked, !blocked && SpectatorInputService.IsKeyPressedThisFrame(_config.Camera.ToggleHudKey.Value));
        if (wasRequested != _state.Requested) ModLog.Info("Spectator HUD hidden: " + _state.Requested);
        if (!blocked && SpectatorInputService.IsKeyPressedThisFrame(_config.Camera.ToggleKeyHintsKey.Value))
            _config.Camera.ShowKeyHints.Value = !_config.Camera.ShowKeyHints.Value;
    }

    internal static void RestoreRenderers()
    {
        _current?._scope.Restore();
    }

    internal static void BeforeSubmit()
    {
        if (!Hidden || _current == null) return;
        var self = _current;
        try
        {
            var hud = HUDManager.Instance;
            if (self._input.IsCameraInputBlocked()) return;
            Canvas? nativeRoot = hud?.HUDContainer != null ? hud.HUDContainer.GetComponentInParent<Canvas>()?.rootCanvas : null;
            self._roots.Clear();
            // Discover current roots at submission so newly created third-party overlays cannot flash for a cache interval.
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
            {
                var root = canvas.rootCanvas;
                if (root == null || !self._roots.Add(root)) continue;
                if (root.renderMode == RenderMode.WorldSpace && root != nativeRoot
                    && (hud == null || hud.UICamera == null || root.worldCamera != hud.UICamera)) continue;
                self._scope.Hide(root, hud != null ? hud.playerScreen : null);
            }
        }
        catch (Exception ex)
        {
            RestoreRenderers(); self._state.Update(false, false, false);
            ModLog.Warning("Spectator HUD suppression restored after failure: " + ex.Message);
        }
    }
    internal static void Clear() => _current?.Dispose();
    public void Dispose()
    {
        RestoreRenderers(); _state.Update(false, false, false);
        _roots.Clear();
        if (_current == this) _current = null;
    }
}
