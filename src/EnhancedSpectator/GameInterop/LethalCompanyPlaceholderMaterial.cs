using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EnhancedSpectator.GameInterop;

/// <summary>Configures only the authored placeholder glyphs; never converts source models for fading.</summary>
internal static class LethalCompanyPlaceholderMaterial
{
    internal static void Configure(Material material, bool depthTest = true)
    {
        Set(material, "_SurfaceType", 1f);
        Set(material, "_BlendMode", 0f);
        Set(material, "_AlphaCutoffEnable", 0f);
        Set(material, "_TransparentZWrite", 0f);
        Set(material, "_TransparentDepthPrepassEnable", 0f);
        Set(material, "_TransparentDepthPostpassEnable", 0f);
        Set(material, "_ReceivesSSRTransparent", 0f);
        Set(material, "_EnableBlendModePreserveSpecularLighting", 0f);
        Set(material, "_RefractionModel", 0f);
        Set(material, "_AlphaRemapMin", 0f);
        Set(material, "_AlphaRemapMax", 1f);
        Set(material, "_AlphaToMask", 0f);
        Set(material, "_AlphaToMaskInspectorValue", 0f);
        Set(material, "_TransparentBackfaceEnable", 0f);
        Set(material, "_UseEmissiveIntensity", 0f);
        Set(material, "_ZTestTransparent", (float)(depthTest ? CompareFunction.LessEqual : CompareFunction.Always));
        material.renderQueue = 3000;
        // Changing SurfaceType alone leaves the opaque Forward pass's Equal depth test behind.
        // HDRP also uses premultiplied shader output, so its validator must select the blend factors.
        HDMaterial.ValidateMaterial(material);
        material.SetShaderPassEnabled("TransparentDepthPrepass", false);
        material.SetShaderPassEnabled("TransparentDepthPostpass", false);
        material.SetShaderPassEnabled("DepthOnly", false);
        material.SetShaderPassEnabled("DepthForwardOnly", false);
        material.SetShaderPassEnabled("ShadowCaster", false);
        if (!depthTest) material.renderQueue = 5000;
    }

    private static void Set(Material material, string key, float value)
    {
        if (material.HasProperty(key)) material.SetFloat(key, value);
    }
}
