#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop
{

/// <summary>Render-boundary culling with exact restoration; retains the game's RawImage and its masks.</summary>
internal sealed class SpectatorCanvasRenderScope
{
    private readonly Dictionary<CanvasRenderer, bool> _saved = new Dictionary<CanvasRenderer, bool>();
    private readonly List<CanvasRenderer> _scratch = new List<CanvasRenderer>();
    private readonly Dictionary<Graphic, bool> _raycasts = new Dictionary<Graphic, bool>();
    // The original game surface includes opaque backing graphics on its ancestors, not just the RawImage.
    internal void HideSurface(Transform surface, Transform canvasRoot)
    {
        for (var part = surface; part != null && part != canvasRoot; part = part.parent)
            if (part.TryGetComponent<CanvasRenderer>(out var renderer)) Cull(renderer);
    }
    /// <summary>Hides every renderer under <paramref name="root"/> outside the kept branches, and inside <paramref name="butNot"/>.</summary>
    internal void HideExcept(Canvas root, Transform[] keep, Transform butNot)
    {
        _scratch.Clear(); root.GetComponentsInChildren(false, _scratch);
        foreach (var renderer in _scratch)
        {
            var part = renderer.transform;
            bool kept = false;
            foreach (var branch in keep) if (part.IsChildOf(branch)) { kept = true; break; }
            if (!kept || part.IsChildOf(butNot)) Cull(renderer);
        }
    }
    internal void HideBranch(Transform root)
    {
        _scratch.Clear(); root.GetComponentsInChildren(false, _scratch);
        foreach (var renderer in _scratch)
        {
            Cull(renderer);
            var graphic = renderer.GetComponent<Graphic>();
            if (graphic != null && graphic.raycastTarget)
            {
                if (!_raycasts.ContainsKey(graphic)) _raycasts.Add(graphic, true);
                graphic.raycastTarget = false;
            }
        }
    }
    private void Cull(CanvasRenderer renderer)
    {
        if (!_saved.ContainsKey(renderer)) _saved.Add(renderer, renderer.cull);
        renderer.cull = true;
    }
    internal void Hide(Canvas root, Transform? gameSurface)
    {
        _scratch.Clear(); root.GetComponentsInChildren(false, _scratch);
        foreach (var renderer in _scratch)
        {
            if (gameSurface != null && gameSurface.IsChildOf(renderer.transform)) continue;
            Cull(renderer);
        }
    }
    internal void Restore()
    {
        foreach (var saved in _saved) if (saved.Key != null) saved.Key.cull = saved.Value;
        foreach (var saved in _raycasts) if (saved.Key != null) saved.Key.raycastTarget = saved.Value;
        _saved.Clear(); _scratch.Clear(); _raycasts.Clear();
    }
}
}
