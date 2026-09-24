using UnityEngine;

namespace EnhancedSpectator.Features.FearMode
{
    // The normalized local envelope stays oriented with the visual: rotating a long model
    // cannot inflate a world AABB and trigger fading at an unrelated distant corner.
    internal static class ModelFadeGeometry
    {
        internal static float Distance(Bounds localEnvelope, Matrix4x4 toWorld, Matrix4x4 toLocal, Bounds body)
        {
            Vector3 onBody = body.center, onModel = onBody;
            // Alternating projections of two convex boxes. The body box is small; eight
            // bounded iterations converge without allocation, mesh reads or physics queries.
            for (int i = 0; i < 8; i++)
            {
                onModel = toWorld.MultiplyPoint3x4(localEnvelope.ClosestPoint(toLocal.MultiplyPoint3x4(onBody)));
                Vector3 next = body.ClosestPoint(onModel);
                if ((next - onBody).sqrMagnitude < .00000001f) { onBody = next; break; }
                onBody = next;
            }
            return Vector3.Distance(onModel, onBody);
        }
    }
}
