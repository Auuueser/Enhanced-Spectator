#nullable enable
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.GameInterop
{
    // Shared with the offline GPU fixture. Half-resolution coverage of visible actor pixels, then a separable
    // Gaussian, give the composite a hotter core, cooler rim and a soft warm halo. Unlike the per-pixel depth
    // derivatives this replaces, the result changes smoothly while the camera moves.
    internal static class SpectatorThermalGlow
    {
        private const int CoveragePass = 2, BlurPass = 3;

        internal static RenderTexture Create(int width, int height)
        {
            var texture = new RenderTexture(width, height, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear)
            { name = "ES.Thermal.Glow", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.Create();
            return texture;
        }

        internal static int Half(int size) => (size + 1) / 2;

        /// <summary>Expects the scene/actor depth, viewport, depth-linearisation and reversed-Z globals already bound.</summary>
        internal static void Record(CommandBuffer cmd, Material composite, RenderTexture cover, RenderTexture blur, RenderTexture glow, int width, int height)
        {
            var half = new Rect(0, 0, Half(width), Half(height));
            cmd.SetGlobalVector("_ESThermalHalf", new Vector4(half.width, half.height, 1f / cover.width, 1f / cover.height));
            cmd.SetRenderTarget(cover); cmd.SetViewport(half);
            cmd.DrawProcedural(Matrix4x4.identity, composite, CoveragePass, MeshTopology.Triangles, 3);
            cmd.SetGlobalTexture("_ESThermalBlurSource", cover); cmd.SetGlobalVector("_ESThermalBlur", new Vector4(1, 0, 0, 0));
            cmd.SetRenderTarget(blur); cmd.SetViewport(half);
            cmd.DrawProcedural(Matrix4x4.identity, composite, BlurPass, MeshTopology.Triangles, 3);
            cmd.SetGlobalTexture("_ESThermalBlurSource", blur); cmd.SetGlobalVector("_ESThermalBlur", new Vector4(0, 1, 0, 0));
            cmd.SetRenderTarget(glow); cmd.SetViewport(half);
            cmd.DrawProcedural(Matrix4x4.identity, composite, BlurPass, MeshTopology.Triangles, 3);
            cmd.SetGlobalTexture("_ESThermalGlow", glow);
        }
    }
}
