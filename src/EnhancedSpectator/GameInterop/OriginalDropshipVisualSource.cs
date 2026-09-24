using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EnhancedSpectator.Logging;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.GameInterop;

// Only builds Transform/MeshFilter/MeshRenderer objects. Never loads or activates a moon scene.
internal sealed class OriginalDropshipVisualSource : IDisposable
{
    private Task<V81DropshipData>? _read;
    private bool _finished;
    private GameObject? _root;
    private FearVisualSource? _source;
    private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();

    internal FearVisualSource? Poll()
    {
        if (_finished) return _source;
        if (_read == null)
        {
            string directory = Application.dataPath;
            _read = Task.Run(() => V81DropshipData.Read(directory));
            return null;
        }
        if (!_read.IsCompleted) return null;
        _finished = true;
        try
        {
            var data = _read.GetAwaiter().GetResult();
            Build(data);
            ModLog.Info("Mini dropship available before moon landing: verified local V81 visual ranges, 9 renderers; no delivery components.");
        }
        catch (Exception ex)
        {
            Dispose();
            ModLog.Warning("Pre-landing dropship source unavailable; using loaded scene fallback. " + ex.Message);
        }
        _read = null;
        return _source;
    }

    private void Build(V81DropshipData data)
    {
        Shader shader = Shader.Find("HDRP/Lit") ?? throw new InvalidOperationException("HDRP/Lit unavailable");
        var textures = new Texture2D[data.textures.Length];
        for (int i = 0; i < textures.Length; i++)
        {
            var info = data.textures[i];
            bool linear = false;
            foreach (var material in data.materials) if (material.normal == i) linear = true;
            var texture = new Texture2D(info.width, info.height, (TextureFormat)info.format, info.mips, linear)
            { name = "Enhanced Spectator original dropship texture " + i };
            _owned.Add(texture);
            texture.LoadRawTextureData(data.Bytes[info.chunk]);
            texture.Apply(false, true);
            textures[i] = texture;
        }
        var materials = new Material[data.materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            var info = data.materials[i];
            var material = new Material(shader) { name = "Enhanced Spectator original dropship material " + i };
            _owned.Add(material);
            Color color = ReadColor(data.Bytes[info.color]);
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissiveColor", ReadColor(data.Bytes[info.emission]));
            if (info.texture >= 0) material.SetTexture("_BaseColorMap", textures[info.texture]);
            if (info.normal >= 0)
            {
                material.SetTexture("_NormalMap", textures[info.normal]);
                material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                material.EnableKeyword("_NORMALMAP");
            }
            byte[] uv = data.Bytes[info.uv];
            var scale = new Vector2(BitConverter.ToSingle(uv, 0), BitConverter.ToSingle(uv, 4));
            var offset = new Vector2(BitConverter.ToSingle(uv, 8), BitConverter.ToSingle(uv, 12));
            material.SetTextureScale("_BaseColorMap", scale); material.SetTextureOffset("_BaseColorMap", offset);
            material.SetTextureScale("_NormalMap", scale); material.SetTextureOffset("_NormalMap", offset);
            material.SetFloat("_Smoothness", .35f);
            if (color.a < .99f)
            {
                material.SetFloat("_SurfaceType", 1); material.SetFloat("_ZWrite", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_AlphaSrcBlend", (float)BlendMode.One);
                material.SetFloat("_AlphaDstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_TransparentZWrite", 0);
                material.SetFloat("_ZTestTransparent", (float)CompareFunction.LessEqual);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.EnableKeyword("_BLENDMODE_ALPHA");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            materials[i] = material;
        }
        _root = new GameObject("Enhanced Spectator original dropship source");
        _root.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(_root);
        Transform? hull = null;
        var renderers = new HashSet<Renderer>();
        foreach (var info in data.meshes)
        {
            var part = new GameObject(info.name);
            part.transform.SetParent(hull ?? _root.transform, false);
            if (hull == null) hull = part.transform;
            else ApplyTransform(part.transform, data.Bytes[info.transform]);
            var mesh = new Mesh { name = "Enhanced Spectator original dropship " + info.name };
            _owned.Add(mesh);
            byte[] bytes = data.Bytes[info.vertices];
            var vertices = new Vector3[info.vertexCount];
            var normals = new Vector3[info.vertexCount];
            var tangents = new Vector4[info.vertexCount];
            var uv = new Vector2[info.vertexCount];
            for (int i = 0; i < vertices.Length; i++)
            {
                int start = i * 32;
                vertices[i] = new Vector3(BitConverter.ToSingle(bytes, start), BitConverter.ToSingle(bytes, start + 4), BitConverter.ToSingle(bytes, start + 8));
                normals[i] = new Vector3(V81DropshipData.Half(bytes, start + 12), V81DropshipData.Half(bytes, start + 14), V81DropshipData.Half(bytes, start + 16));
                tangents[i] = new Vector4(V81DropshipData.Half(bytes, start + 20), V81DropshipData.Half(bytes, start + 22), V81DropshipData.Half(bytes, start + 24), V81DropshipData.Half(bytes, start + 26));
                uv[i] = new Vector2(V81DropshipData.Half(bytes, start + 28), V81DropshipData.Half(bytes, start + 30));
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.tangents = tangents; mesh.uv = uv;
            mesh.subMeshCount = info.submeshes.Length;
            byte[] indices = data.Bytes[info.indices];
            for (int i = 0; i < info.submeshes.Length; i++)
            {
                var sub = info.submeshes[i];
                var triangles = new int[sub.count];
                for (int j = 0; j < triangles.Length; j++) triangles[j] = BitConverter.ToUInt16(indices, (sub.start + j) * 2);
                mesh.SetTriangles(triangles, i, false);
            }
            mesh.RecalculateBounds();
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            var slots = new Material[info.materials.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = materials[info.materials[i]];
            renderer.sharedMaterials = slots;
            renderers.Add(renderer);
        }
        if (hull == null) throw new InvalidOperationException("Dropship hull missing");
        _source = new FearVisualSource(hull, hull, normalizeRootPose: true,
            forceRendererVisibility: true, includedRenderers: renderers, miniature: true);
    }

    private static Color ReadColor(byte[] bytes) => new Color(BitConverter.ToSingle(bytes, 0),
        BitConverter.ToSingle(bytes, 4), BitConverter.ToSingle(bytes, 8), BitConverter.ToSingle(bytes, 12));

    private static void ApplyTransform(Transform transform, byte[] bytes)
    {
        transform.localRotation = new Quaternion(BitConverter.ToSingle(bytes, 0), BitConverter.ToSingle(bytes, 4), BitConverter.ToSingle(bytes, 8), BitConverter.ToSingle(bytes, 12));
        transform.localPosition = new Vector3(BitConverter.ToSingle(bytes, 16), BitConverter.ToSingle(bytes, 20), BitConverter.ToSingle(bytes, 24));
        transform.localScale = new Vector3(BitConverter.ToSingle(bytes, 28), BitConverter.ToSingle(bytes, 32), BitConverter.ToSingle(bytes, 36));
    }

    public void Dispose()
    {
        _finished = true;
        _source = null;
        if (_root != null) UnityEngine.Object.Destroy(_root);
        foreach (var value in _owned) if (value != null) UnityEngine.Object.Destroy(value);
        _owned.Clear();
        _root = null;
        // Observe background I/O errors even when shutdown precedes completion. No Unity work runs there.
        if (_read != null) _ = _read.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        _read = null;
    }
}
