using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.GameInterop
{
    // Ordered command-buffer readback, never a synchronous GPU wait or a scene render.
    internal static class NativeFadePixelProbe
    {
        internal const int Size = 16;
        internal static void Enqueue(CommandBuffer cmd, RenderTexture texture, Rect region, Action<Color[], string> done)
        {
            if (!SystemInfo.supportsAsyncGPUReadback || texture.antiAliasing != 1)
            { done(Array.Empty<Color>(), "readback-unsupported-or-MSAA"); return; }
            int width = Math.Min(Size, texture.width), height = Math.Min(Size, texture.height);
            int x = Mathf.Clamp(Mathf.FloorToInt(region.center.x) - width / 2, 0, texture.width - width);
            int y = Mathf.Clamp(Mathf.FloorToInt(region.center.y) - height / 2, 0, texture.height - height);
            cmd.RequestAsyncReadback(texture, 0, x, width, y, height, 0, 1, TextureFormat.RGBAFloat, request =>
            {
                if (request.hasError) { done(Array.Empty<Color>(), "GPU-readback-error"); return; }
                var data = request.GetData<Color>();
                var pixels = new Color[data.Length];
                for (int i = 0; i < data.Length; i++) pixels[i] = data[i];
                done(pixels, string.Empty);
            });
        }
    }
}
