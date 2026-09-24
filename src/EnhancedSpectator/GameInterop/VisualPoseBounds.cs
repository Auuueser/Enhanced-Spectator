using UnityEngine;

namespace EnhancedSpectator.GameInterop
{
    // One-time capture, not a per-frame skinning/readback path. Renderer culling bounds
    // may describe a different form, bone frame or animation envelope.
    internal static class VisualPoseBounds
    {
        internal static bool TryCapture(Renderer[] renderers, Matrix4x4 worldToFrame, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            Mesh? scratch = null;
            try
            {
                foreach (var renderer in renderers)
                {
                    if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy) continue;
                    Mesh? mesh;
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        if (skin.sharedMesh == null) continue;
                        if (scratch == null) scratch = new Mesh();
                        scratch.Clear();
                        skin.BakeMesh(scratch, false);
                        if (scratch.vertexCount == 0) return false;
                        // BakeMesh's stored bounds are not proof of the posed vertex envelope.
                        scratch.RecalculateBounds();
                        mesh = scratch;
                    }
                    else
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        mesh = filter != null ? filter.sharedMesh : null;
                        if (mesh == null) continue;
                    }
                    Include(mesh.bounds, worldToFrame * renderer.transform.localToWorldMatrix, ref bounds, ref found);
                }
                return found && bounds.size.sqrMagnitude > .000001f;
            }
            catch (System.Exception) { return false; }
            finally { if (scratch != null) Object.Destroy(scratch); }
        }

        private static void Include(Bounds local, Matrix4x4 matrix, ref Bounds combined, ref bool found)
        {
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3(x,y,z)));
                if (float.IsNaN(corner.x) || float.IsInfinity(corner.x) || float.IsNaN(corner.y) || float.IsInfinity(corner.y)
                    || float.IsNaN(corner.z) || float.IsInfinity(corner.z)) throw new System.InvalidOperationException("Non-finite visual pose");
                if (!found) { combined = new Bounds(corner, Vector3.zero); found = true; }
                else combined.Encapsulate(corner);
            }
        }
    }
}
