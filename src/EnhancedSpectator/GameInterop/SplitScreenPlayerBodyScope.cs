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
    // The first-person helmet the gameplay camera looks out through sits on the living player's head: seen from
    // outside it is a second, larger head around theirs (camera mods that switch the visor off hide it).
    private readonly List<Renderer> _visor = new List<Renderer>();
    // The living player's held item hangs on the first-person arms' hand in front of the camera, not on the body's hand
    // other players see: in their own third-person view it floated ahead of them. For the render it is placed on the
    // body's hand the way the game places other players' items, then put back.
    private Transform? _item;
    private Vector3 _itemPosition;
    private Quaternion _itemRotation;

    internal void Begin(PlayerControllerB player, Camera camera)
    {
        Restore();
        if (!player.IsOwner) return;
        _camera = camera; _mask = camera.cullingMask;
        Borrow(player.thisPlayerModel, true);
        Borrow(player.thisPlayerModelLOD1, false);
        Borrow(player.thisPlayerModelLOD2, false);
        Borrow(player.thisPlayerModelArms, false);
        if (player.localVisor != null)
        {
            player.localVisor.GetComponentsInChildren(true, _visor);
            foreach (var renderer in _visor) Borrow(renderer, false);
        }
        var held = player.currentlyHeldObjectServer;
        if (held != null && held.parentObject == player.localItemHolder && player.serverItemHolder != null)
        {
            _item = held.transform; _itemPosition = _item.position; _itemRotation = _item.rotation;
            var hand = player.serverItemHolder;
            _item.SetPositionAndRotation(hand.position + hand.rotation * held.itemProperties.positionOffset,
                hand.rotation * Quaternion.Euler(held.itemProperties.rotationOffset));
        }
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
        if (_item != null) _item.SetPositionAndRotation(_itemPosition, _itemRotation);
        _item = null;
        if (_lods != null) _lods.ForceLOD(-1);
        _lods = null;
        if (_camera != null) _camera.cullingMask = _mask;
        _camera = null;
    }
}
