using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.GameInterop
{
    // Independent of HDRP's shared custom-pass cull, which optimizers may skip or reuse.
    // Results belong to the current render context only; never cache them across frames.
    internal static class NativeFadeCulling
    {
        internal const int IsolationLayer = 31;

        internal static bool TryCull(ScriptableRenderContext context, Camera camera, out CullingResults results)
        {
            results = default;
            if (camera == null || camera.stereoEnabled || !camera.TryGetCullingParameters(out var parameters)) return false;
            // Only our isolated model surfaces: no terrain, shadow/light/probe work,
            // or baked scene occlusion. Preserve this camera's frustum and LOD parameters.
            parameters.cullingMask = 1u << IsolationLayer;
            parameters.cullingOptions = CullingOptions.None;
            results = context.Cull(ref parameters);
            return true;
        }
    }
}
