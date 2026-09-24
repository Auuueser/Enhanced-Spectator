#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace EnhancedSpectator.GameInterop
{

/// <summary>Render-boundary culling with exact restoration; retains the game's RawImage and its masks.</summary>
internal sealed class SpectatorCanvasRenderScope
{
    private readonly Dictionary<CanvasRenderer, bool> _saved = new Dictionary<CanvasRenderer, bool>();
    private readonly List<CanvasRenderer> _scratch = new List<CanvasRenderer>();
    internal void Hide(Canvas root, Transform? gameSurface)
    {
        _scratch.Clear(); root.GetComponentsInChildren(false, _scratch);
        foreach (var renderer in _scratch)
        {
            if (gameSurface != null && gameSurface.IsChildOf(renderer.transform)) continue;
            if (!_saved.ContainsKey(renderer)) _saved.Add(renderer, renderer.cull);
            renderer.cull = true;
        }
    }
    internal void Restore()
    {
        foreach (var saved in _saved) if (saved.Key != null) saved.Key.cull = saved.Value;
        _saved.Clear(); _scratch.Clear();
    }
}
}
