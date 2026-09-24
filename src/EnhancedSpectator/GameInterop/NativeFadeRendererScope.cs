using UnityEngine;

namespace EnhancedSpectator.GameInterop
{

    // Unity-only state boundary, also exercised in the offline editor fixture.
    internal sealed class NativeFadeRendererScope
    {
        private struct Saved
        {
            internal Renderer Renderer;
            internal GameObject Object;
            internal int Layer;
            internal uint RenderingLayers;
        }
        private readonly Renderer[] _renderers;
        private readonly Saved[] _saved;
        private int _count;
        internal Bounds FirstBounds => _saved[0].Renderer.bounds;
        internal NativeFadeRendererScope(Renderer[] renderers) { _renderers = renderers; _saved = new Saved[renderers.Length]; }
        internal bool Begin(int cameraMask, int isolationLayer, uint tag)
        {
            Restore();
            // Capture all layers before writing any: renderers can share a GameObject.
            foreach (var renderer in _renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || (cameraMask & (1 << renderer.gameObject.layer)) == 0) continue;
                _saved[_count++] = new Saved { Renderer = renderer, Object = renderer.gameObject, Layer = renderer.gameObject.layer, RenderingLayers = renderer.renderingLayerMask };
            }
            for (int i = 0; i < _count; i++)
            {
                var saved = _saved[i];
                saved.Renderer.gameObject.layer = isolationLayer;
                saved.Renderer.renderingLayerMask = NativeFadeSlots.Tagged(saved.RenderingLayers, tag);
            }
            return _count != 0;
        }
        internal void Restore()
        {
            for (int i = 0; i < _count; i++)
            {
                var saved = _saved[i];
                if (saved.Object != null) saved.Object.layer = saved.Layer;
                if (saved.Renderer != null)
                {
                    saved.Renderer.renderingLayerMask = saved.RenderingLayers;
                }
                _saved[i] = default;
            }
            _count = 0;
        }
    }
}
