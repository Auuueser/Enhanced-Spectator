using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.GameInterop;

// The living local player's body is normally hidden from their gameplay camera.
// Preview borrows its visibility only for the third-person render and restores it.
// Only the full-detail body is shown, so the body's LOD group is held at it for that render: from further away
// (a third-person ghost's camera) the group would otherwise pick a lower level that is switched off here, and the
// body would vanish while thermal, which draws the full-detail body itself, still outlined it.
internal sealed class SplitScreenPlayerBodyScope
{
    private readonly List<(Renderer renderer, bool enabled, bool off, ShadowCastingMode shadows)> _saved
        = new List<(Renderer, bool, bool, ShadowCastingMode)>();
    private Camera? _camera;
    private LODGroup? _lods;
    private int _mask;

    internal void Begin(PlayerControllerB player, Camera camera)
    {
        Restore();
        if (!player.IsOwner) return;
        _camera = camera; _mask = camera.cullingMask;
        Borrow(player.thisPlayerModel, true);
        Borrow(player.thisPlayerModelLOD1, false);
        Borrow(player.thisPlayerModelLOD2, false);
        Borrow(player.thisPlayerModelArms, false);
        _lods = player.thisPlayerModel != null ? player.thisPlayerModel.GetComponentInParent<LODGroup>() : null;
        if (_lods != null) _lods.ForceLOD(0);
    }

    private void Borrow(Renderer? renderer, bool show)
    {
        if (renderer == null) return;
        _saved.Add((renderer, renderer.enabled, renderer.forceRenderingOff, renderer.shadowCastingMode));
        renderer.enabled = show; renderer.forceRenderingOff = !show;
        if (show) { renderer.shadowCastingMode = ShadowCastingMode.On; _camera!.cullingMask |= 1 << renderer.gameObject.layer; }
    }

    internal void Restore()
    {
        foreach (var entry in _saved) if (entry.renderer != null)
        {
            entry.renderer.enabled = entry.enabled; entry.renderer.forceRenderingOff = entry.off;
            entry.renderer.shadowCastingMode = entry.shadows;
        }
        _saved.Clear();
        if (_lods != null) _lods.ForceLOD(-1);
        _lods = null;
        if (_camera != null) _camera.cullingMask = _mask;
        _camera = null;
    }
}
