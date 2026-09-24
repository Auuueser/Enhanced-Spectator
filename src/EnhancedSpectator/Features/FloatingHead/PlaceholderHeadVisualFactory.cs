using System;
using EnhancedSpectator.Features.SpectatorPresence;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.Features.FloatingHead;

/// <summary>
/// Creates runtime-only placeholder visuals without external assets.
/// </summary>
public sealed class PlaceholderHeadVisualFactory : IDisposable
{
    private const string RootName = "Enhanced Spectator Visuals";

    private GameObject? _root;

    /// <summary>
    /// Creates a placeholder visual for a remote spectator.
    /// </summary>
    public FloatingHeadVisual Create(
        RemoteSpectatorInfo spectator,
        float sphereScale,
        FloatingHeadVisualStyle style,
        float billboardSize,
        float baseAlpha,
        bool useUnlitMaterial,
        bool enableDepthTest,
        bool showNameTag,
        float nameTagScale,
        float nameTagHeightOffset,
        float nameTagMaxDistance,
        string nameTagText)
    {
        GameObject root = GetOrCreateRoot();
        GameObject visualObject = CreateVisualObject(style, out Mesh? runtimeMesh);
        visualObject.name = $"Enhanced Spectator Placeholder {spectator.SpectatorClientId}";
        visualObject.SetActive(false);
        visualObject.transform.SetParent(root.transform, false);
        float initialScale = style == FloatingHeadVisualStyle.Sphere ? sphereScale : billboardSize;
        visualObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, initialScale);

        Collider collider = visualObject.GetComponent<Collider>();
        bool colliderRemoved = collider != null;
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        MeshRenderer renderer = visualObject.GetComponent<MeshRenderer>();
        MeshFilter meshFilter = visualObject.GetComponent<MeshFilter>();
        Color color = ColorForSpectator(spectator.SpectatorClientId, Mathf.Clamp01(baseAlpha));
        Material material = CreateRuntimeMaterial(
            spectator.SpectatorClientId,
            renderer != null ? renderer.sharedMaterial : null,
            color,
            useUnlitMaterial,
            enableDepthTest);
        if (renderer != null)
        {
            renderer.enabled = true;
            renderer.forceRenderingOff = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterial = material;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.sortingOrder = short.MaxValue;
        }

        NameTagVisual? nameTag = showNameTag
            ? NameTagVisual.Create(
                root.transform,
                nameTagText,
                Color.white,
                nameTagScale,
                nameTagHeightOffset,
                nameTagMaxDistance)
            : null;

        return new FloatingHeadVisual(
            spectator.SpectatorClientId,
            spectator.SpectatorSlotId,
            FloatingHeadVisualSourceKind.Placeholder,
            visualObject,
            material,
            runtimeMesh,
            color,
            Mathf.Clamp01(baseAlpha),
            renderer,
            meshFilter,
            colliderRemoved,
            nameTag);
    }

    /// <summary>
    /// Creates a runtime-only detached-head visual clone for a remote spectator.
    /// </summary>
    public FloatingHeadVisual CreateFromDetachedHead(
        RemoteSpectatorInfo spectator,
        Transform source,
        float visualScale,
        bool showNameTag,
        float nameTagScale,
        float nameTagHeightOffset,
        float nameTagMaxDistance,
        string nameTagText)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (!TryGetDetachedHeadRendererSource(source, out MeshFilter? sourceMeshFilter, out MeshRenderer? sourceRenderer))
        {
            throw new InvalidOperationException("Detached-head source does not contain a supported MeshFilter and MeshRenderer.");
        }

        GameObject root = GetOrCreateRoot();
        GameObject visualObject = new GameObject($"Enhanced Spectator Detached Head {spectator.SpectatorClientId}");
        MeshFilter meshFilter = visualObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = visualObject.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = sourceMeshFilter!.sharedMesh;
        meshRenderer.sharedMaterials = sourceRenderer!.sharedMaterials;
        visualObject.name = $"Enhanced Spectator Detached Head {spectator.SpectatorClientId}";
        visualObject.SetActive(false);
        visualObject.transform.SetParent(root.transform, false);
        visualObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, visualScale);

        meshRenderer.enabled = true;
        meshRenderer.forceRenderingOff = false;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.allowOcclusionWhenDynamic = false;
        NameTagVisual? nameTag = showNameTag
            ? NameTagVisual.Create(
                root.transform,
                nameTagText,
                Color.white,
                nameTagScale,
                nameTagHeightOffset,
                nameTagMaxDistance)
            : null;

        return new FloatingHeadVisual(
            spectator.SpectatorClientId,
            spectator.SpectatorSlotId,
            FloatingHeadVisualSourceKind.RuntimeDetachedHead,
            visualObject,
            material: null,
            runtimeMesh: null,
            Color.white,
            1f,
            meshRenderer,
            meshFilter,
            colliderRemoved: false,
            nameTag);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_root != null)
        {
            UnityEngine.Object.Destroy(_root);
            _root = null;
        }
    }

    private GameObject GetOrCreateRoot()
    {
        if (_root != null)
        {
            return _root;
        }

        _root = new GameObject(RootName);
        UnityEngine.Object.DontDestroyOnLoad(_root);
        return _root;
    }

    private static GameObject CreateVisualObject(FloatingHeadVisualStyle style, out Mesh? runtimeMesh)
    {
        runtimeMesh = null;
        if (style == FloatingHeadVisualStyle.Sphere)
        {
            return GameObject.CreatePrimitive(PrimitiveType.Sphere);
        }

        GameObject visualObject = new GameObject($"Enhanced Spectator {style}");
        MeshFilter meshFilter = visualObject.AddComponent<MeshFilter>();
        MeshRenderer _ = visualObject.AddComponent<MeshRenderer>();
        runtimeMesh = style == FloatingHeadVisualStyle.Ring
            ? CreateRingMesh()
            : CreateBillboardMesh();
        meshFilter.sharedMesh = runtimeMesh;
        return visualObject;
    }

    private static Material CreateRuntimeMaterial(
        ulong spectatorClientId,
        Material? sourceMaterial,
        Color color,
        bool useUnlitMaterial,
        bool enableDepthTest)
    {
        Shader? shader = FindPlaceholderShader(useUnlitMaterial, enableDepthTest);
        Material material;
        if (shader != null)
        {
            material = new Material(shader);
        }
        else if (sourceMaterial != null)
        {
            material = new Material(sourceMaterial);
        }
        else
        {
            throw new InvalidOperationException("No placeholder shader or source material is available.");
        }
        material.name = $"Enhanced Spectator Placeholder Material {spectatorClientId}";
        ApplyMaterialColor(material, color);
        ConfigureMaterial(material, enableDepthTest);

        return material;
    }

    private static Shader? FindPlaceholderShader(bool useUnlitMaterial, bool enableDepthTest)
    {
        string[] shaderNames = useUnlitMaterial
            ? new[]
            {
                "HDRP/Unlit",
                "HDRP/Lit",
                "Universal Render Pipeline/Unlit",
                "Universal Render Pipeline/Lit",
                "Unlit/Color",
                "Sprites/Default",
                "Standard",
            }
            : new[]
            {
                "HDRP/Lit",
                "Universal Render Pipeline/Lit",
                "Standard",
                "HDRP/Unlit",
                "Universal Render Pipeline/Unlit",
                "Unlit/Color",
                "Sprites/Default",
            };

        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
            {
                return shader;
            }
        }

        return null;
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        SetMaterialColor(material, "_BaseColor", color);
        SetMaterialColor(material, "_UnlitColor", color);
        SetMaterialColor(material, "_Color", color);
        SetMaterialColor(material, "_EmissionColor", color);
        SetMaterialColor(material, "_EmissiveColor", color);
        SetMaterialColor(material, "_TintColor", color);
    }

    private static void SetMaterialColor(Material material, string propertyName, Color color)
    {
        if (!material.HasProperty(propertyName))
        {
            return;
        }

        material.SetColor(propertyName, color);
        if (propertyName.Contains("Emission") || propertyName.Contains("Emissive"))
        {
            material.EnableKeyword("_EMISSION");
        }
    }

    private static void ConfigureMaterial(Material material, bool enableDepthTest)
    {
        material.renderQueue = enableDepthTest ? 3000 : 5000;
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZTest"))
        {
            material.SetFloat("_ZTest", enableDepthTest ? (float)CompareFunction.LessEqual : (float)CompareFunction.Always);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        if (material.HasProperty("_SurfaceType"))
        {
            material.SetFloat("_SurfaceType", 1f);
        }

        if (material.HasProperty("_BlendMode"))
        {
            material.SetFloat("_BlendMode", 0f);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)CullMode.Off);
        }

        if (material.HasProperty("_CullMode"))
        {
            material.SetFloat("_CullMode", (float)CullMode.Off);
        }

        if (material.HasProperty("_TransparentCullMode"))
        {
            material.SetFloat("_TransparentCullMode", (float)CullMode.Off);
        }

        if (material.HasProperty("_DoubleSidedEnable"))
        {
            material.SetFloat("_DoubleSidedEnable", 1f);
        }

        material.EnableKeyword("_DOUBLESIDED_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        if (material.shader != null && material.shader.name.StartsWith("HDRP/", StringComparison.Ordinal))
            EnhancedSpectator.GameInterop.LethalCompanyPlaceholderMaterial.Configure(material, enableDepthTest);
    }

    private static Mesh CreateBillboardMesh()
    {
        const float halfSize = 0.5f;
        Mesh mesh = new Mesh();
        mesh.name = "Enhanced Spectator Runtime Billboard";
        mesh.vertices = new[]
        {
            new Vector3(-halfSize, -halfSize, 0f),
            new Vector3(halfSize, -halfSize, 0f),
            new Vector3(-halfSize, halfSize, 0f),
            new Vector3(halfSize, halfSize, 0f),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
        };
        mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh CreateRingMesh()
    {
        const int segments = 32;
        const float outerRadius = 0.5f;
        float innerRadius = outerRadius * 0.58f;
        Vector3[] vertices = new Vector3[segments * 2];
        int[] triangles = new int[segments * 6];

        for (int index = 0; index < segments; index++)
        {
            float angle = (Mathf.PI * 2f * index) / segments;
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);
            vertices[index * 2] = new Vector3(cos * outerRadius, sin * outerRadius, 0f);
            vertices[(index * 2) + 1] = new Vector3(cos * innerRadius, sin * innerRadius, 0f);

            int next = (index + 1) % segments;
            int tri = index * 6;
            int outer = index * 2;
            int inner = outer + 1;
            int nextOuter = next * 2;
            int nextInner = nextOuter + 1;
            triangles[tri] = outer;
            triangles[tri + 1] = nextOuter;
            triangles[tri + 2] = inner;
            triangles[tri + 3] = inner;
            triangles[tri + 4] = nextOuter;
            triangles[tri + 5] = nextInner;
        }

        Mesh mesh = new Mesh();
        mesh.name = "Enhanced Spectator Runtime Ring";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Color ColorForSpectator(ulong spectatorClientId, float alpha)
    {
        float hue = ((spectatorClientId * 0.173f) + 0.52f) % 1f;
        Color color = Color.HSVToRGB(hue, 0.65f, 1f);
        color.a = alpha;
        return color;
    }

    private static bool TryGetDetachedHeadRendererSource(
        Transform source,
        out MeshFilter? meshFilter,
        out MeshRenderer? meshRenderer)
    {
        meshRenderer = source.GetComponentInChildren<MeshRenderer>(true);
        if (meshRenderer == null)
        {
            meshFilter = null;
            return false;
        }

        meshFilter = meshRenderer.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            meshFilter = null;
            meshRenderer = null;
            return false;
        }

        return true;
    }
}
