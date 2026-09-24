#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.GameInterop
{

/// <summary>Hides head/body/cosmetics and reveals dedicated arms for only the borrowed view.</summary>
internal sealed class FirstPersonRenderScope
{
    private readonly List<(Renderer renderer, bool off)> _hidden = new List<(Renderer, bool)>();
    private SkinnedMeshRenderer? _arms;
    private bool _enabled, _off, _update;
    private ShadowCastingMode _shadows;
    private Camera? _camera;
    private int _mask;
    internal void Begin(List<Renderer> hide, SkinnedMeshRenderer? arms, Camera camera)
    {
        Restore();
        foreach (var renderer in hide)
        {
            if (renderer == null || renderer == arms) continue;
            _hidden.Add((renderer, renderer.forceRenderingOff)); renderer.forceRenderingOff = true;
        }
        if (arms == null || !arms.gameObject.activeInHierarchy) return;
        _arms = arms; _enabled = arms.enabled; _off = arms.forceRenderingOff;
        _shadows = arms.shadowCastingMode; _update = arms.updateWhenOffscreen;
        _camera = camera; _mask = camera.cullingMask;
        camera.cullingMask |= 1 << arms.gameObject.layer;
        arms.enabled = true; arms.forceRenderingOff = false;
        arms.shadowCastingMode = ShadowCastingMode.Off; arms.updateWhenOffscreen = true;
    }
    internal void Restore()
    {
        if (_arms != null)
        {
            _arms.enabled = _enabled; _arms.forceRenderingOff = _off;
            _arms.shadowCastingMode = _shadows; _arms.updateWhenOffscreen = _update;
        }
        if (_camera != null) _camera.cullingMask = _mask;
        _arms = null; _camera = null;
        foreach (var entry in _hidden) if (entry.renderer != null) entry.renderer.forceRenderingOff = entry.off;
        _hidden.Clear();
    }
}
}
