// Authored for Enhanced Spectator. No game assets or HDRP shader source embedded.
Shader "Hidden/EnhancedSpectator/NativeFadeComposite"
{
    SubShader
    {
        Cull Off
        HLSLINCLUDE
        #pragma target 4.5
        // HDRP uses arrays on D3D11 even for non-XR cameras. Color and depth
        // can independently be resolved/MSAA; never infer one from the other.
        #if defined(ES_COLOR_ARRAY)
            #if defined(ES_COLOR_MS)
                Texture2DMSArray<float4> _ESFadeSceneColor;
            #else
                Texture2DArray<float4> _ESFadeSceneColor;
            #endif
        #else
            #if defined(ES_COLOR_MS)
                Texture2DMS<float4> _ESFadeSceneColor;
            #else
                Texture2D<float4> _ESFadeSceneColor;
            #endif
        #endif
        #if defined(ES_DEPTH_ARRAY)
            #if defined(ES_DEPTH_MS)
                Texture2DMSArray<float> _ESFadeSceneDepth;
            #else
                Texture2DArray<float> _ESFadeSceneDepth;
            #endif
        #else
            #if defined(ES_DEPTH_MS)
                Texture2DMS<float> _ESFadeSceneDepth;
            #else
                Texture2D<float> _ESFadeSceneDepth;
            #endif
        #endif
        Texture2D<float4> _ESFadeModelColor;
        float _ESFadeOpacity;
        int _ESFadeColorSamples, _ESFadeDepthSamples, _ESFadeReversedZ;
        float4 ReadColor(int2 pixel)
        {
            #if defined(ES_COLOR_MS)
                float4 color = 0;
                for (int sample = 0; sample < _ESFadeColorSamples; ++sample)
                {
                    #if defined(ES_COLOR_ARRAY)
                        color += _ESFadeSceneColor.Load(int3(pixel, 0), sample);
                    #else
                        color += _ESFadeSceneColor.Load(pixel, sample);
                    #endif
                }
                return color / _ESFadeColorSamples;
            #elif defined(ES_COLOR_ARRAY)
                return _ESFadeSceneColor.Load(int4(pixel, 0, 0));
            #else
                return _ESFadeSceneColor.Load(int3(pixel, 0));
            #endif
        }
        float ReadDepth(int2 pixel)
        {
            #if defined(ES_DEPTH_MS)
                // Conservative visibility: the closest covered sample wins.
                // Averaging depth would create surfaces that do not exist.
                float depth = _ESFadeReversedZ != 0 ? 0 : 1;
                for (int sample = 0; sample < _ESFadeDepthSamples; ++sample)
                {
                    #if defined(ES_DEPTH_ARRAY)
                        float value = _ESFadeSceneDepth.Load(int3(pixel, 0), sample);
                    #else
                        float value = _ESFadeSceneDepth.Load(pixel, sample);
                    #endif
                    depth = _ESFadeReversedZ != 0 ? max(depth, value) : min(depth, value);
                }
                return depth;
            #elif defined(ES_DEPTH_ARRAY)
                return _ESFadeSceneDepth.Load(int4(pixel, 0, 0));
            #else
                return _ESFadeSceneDepth.Load(int3(pixel, 0));
            #endif
        }
        float4 Vertex(uint id : SV_VertexID) : SV_POSITION
        {
            return float4((id == 1 ? 3 : -1), (id == 2 ? 3 : -1), 0, 1);
        }
        struct CopyOutput { float4 color : SV_Target; float depth : SV_Depth; };
        CopyOutput Copy(float4 position : SV_POSITION)
        {
            CopyOutput o;
            o.color = ReadColor(int2(position.xy));
            o.depth = ReadDepth(int2(position.xy));
            return o;
        }
        float4 Composite(float4 position : SV_POSITION) : SV_Target
        {
            return float4(_ESFadeModelColor.Load(int3(position.xy, 0)).rgb, saturate(_ESFadeOpacity));
        }
        ENDHLSL
        Pass
        {
            Name "CopySceneAndDepth"
            ZWrite On ZTest Always Blend Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Copy
            #pragma multi_compile_local _ ES_COLOR_ARRAY
            #pragma multi_compile_local _ ES_DEPTH_ARRAY
            #pragma multi_compile_local _ ES_COLOR_MS
            #pragma multi_compile_local _ ES_DEPTH_MS
            ENDHLSL
        }
        Pass
        {
            Name "CompositeWholeModel"
            ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Composite
            ENDHLSL
        }
    }
    Fallback Off
}
