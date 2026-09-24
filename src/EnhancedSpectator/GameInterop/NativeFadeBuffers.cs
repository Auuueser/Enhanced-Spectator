#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.GameInterop
{
    // Shared with the offline GPU fixture so resource selection is tested too.
    // Single-view array slice zero is supported; this does not implement XR.
    internal static class NativeFadeBuffers
    {
        private static readonly int SceneColor = Shader.PropertyToID("_ESFadeSceneColor");
        private static readonly int SceneDepth = Shader.PropertyToID("_ESFadeSceneDepth");
        private static readonly int ColorSamples = Shader.PropertyToID("_ESFadeColorSamples");
        private static readonly int DepthSamples = Shader.PropertyToID("_ESFadeDepthSamples");
        private static readonly int ReversedZ = Shader.PropertyToID("_ESFadeReversedZ");

        internal static RenderTexture CreateModelBuffer(int width, int height)
        {
            var texture = new RenderTexture(width, height, 32, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = "ES.NativeFade.Model", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point
            };
            if (texture.Create()) return texture;
            UnityEngine.Object.Destroy(texture);
            throw new InvalidOperationException("Native fade buffer allocation failed.");
        }

        internal static string? Validate(RenderTexture? color, RenderTexture? depth, int width, int height)
        {
            string? problem = ValidateTexture(color, "color", width, height);
            if (problem != null) return problem;
            problem = ValidateTexture(depth, "depth", width, height);
            if (problem != null) return problem;
            if (depth!.depth == 0) return "depth: no depth attachment";
            if (depth.antiAliasing > 1 && !depth.bindTextureMS)
                return "depth: multisampled attachment without explicit MS binding is not verified";
            return null;
        }

        private static string? ValidateTexture(RenderTexture? texture, string role, int width, int height)
        {
            if (texture == null) return role + ": RTHandle has no inspectable RenderTexture";
            if (!texture.IsCreated()) return role + ": RenderTexture is not created";
            if (texture.dimension != TextureDimension.Tex2D && texture.dimension != TextureDimension.Tex2DArray)
                return role + ": unsupported dimension " + texture.dimension;
            if (texture.volumeDepth < 1) return role + ": missing slice zero";
            if (width <= 0 || height <= 0 || width > texture.width || height > texture.height)
                return role + ": actual viewport is outside allocation";
            int samples = texture.antiAliasing;
            if (samples != 1 && samples != 2 && samples != 4 && samples != 8)
                return role + ": unsupported sample count " + samples;
            return null;
        }

        internal static bool Bind(CommandBuffer cmd, Material material, RenderTexture color, RenderTexture depth,
            RenderTargetIdentifier colorId, RenderTargetIdentifier depthId)
        {
            bool changed = SetKeyword(material, "ES_COLOR_ARRAY", color.dimension == TextureDimension.Tex2DArray);
            changed |= SetKeyword(material, "ES_DEPTH_ARRAY", depth.dimension == TextureDimension.Tex2DArray);
            changed |= SetKeyword(material, "ES_COLOR_MS", color.antiAliasing > 1 && color.bindTextureMS);
            changed |= SetKeyword(material, "ES_DEPTH_MS", depth.antiAliasing > 1);
            cmd.SetGlobalTexture(SceneColor, colorId);
            cmd.SetGlobalTexture(SceneDepth, depthId, RenderTextureSubElement.Depth);
            cmd.SetGlobalInt(ColorSamples, color.antiAliasing);
            cmd.SetGlobalInt(DepthSamples, depth.antiAliasing);
            cmd.SetGlobalInt(ReversedZ, SystemInfo.usesReversedZBuffer ? 1 : 0);
            return changed;
        }

        private static bool SetKeyword(Material material, string keyword, bool enabled)
        {
            if (material.IsKeywordEnabled(keyword) == enabled) return false;
            if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
            return true;
        }
    }
}
