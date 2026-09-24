using UnityEngine;

namespace EnhancedSpectator.GameInterop
{
    internal static class NativeFadeRenderRegion
    {
        // Keep original viewport/pixel coordinates; only limit touched fragments.
        internal static Rect Project(Camera camera, Bounds bounds, int width, int height)
        {
            float left = float.PositiveInfinity, right = float.NegativeInfinity, low = float.PositiveInfinity, high = float.NegativeInfinity;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x,y,z));
                        Vector3 point = camera.WorldToViewportPoint(corner);
                        if (point.z <= camera.nearClipPlane || float.IsNaN(point.x) || float.IsNaN(point.y))
                            return new Rect(0, 0, width, height);
                        left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x);
                        low = Mathf.Min(low, point.y); high = Mathf.Max(high, point.y);
                    }
            // A conservative guard for shader displacement and temporal jitter. Include
            // both RT Y conventions: extra work is preferable to cropping a native model.
            float yMin = Mathf.Min(low, 1f - high), yMax = Mathf.Max(high, 1f - low);
            float padX = 8f + Mathf.Max(width * .01f, (right - left) * width * .25f);
            float padY = 8f + Mathf.Max(height * .01f, (high - low) * height * .25f);
            float x0 = Mathf.Clamp(Mathf.Floor(left * width - padX), 0, width);
            float x1 = Mathf.Clamp(Mathf.Ceil(right * width + padX), 0, width);
            float y0 = Mathf.Clamp(Mathf.Floor(yMin * height - padY), 0, height);
            float y1 = Mathf.Clamp(Mathf.Ceil(yMax * height + padY), 0, height);
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }
    }
}
