// Authored for Enhanced Spectator. No game assets or HDRP shader source embedded.
Shader "Hidden/EnhancedSpectator/ThermalComposite"
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
        Texture2D<float4> _ESThermalScene;
        Texture2D<float> _ESThermalDepth, _ESThermalSceneDepth;
        float4 _ESThermalZ;
        float4 _ESThermalViewport;
        float SceneDistance(int2 pixel)
        {
            pixel=clamp(pixel,int2(0,0),int2(_ESThermalViewport.xy)-1);
            float depth=_ESThermalSceneDepth.Load(int3(pixel,0));
            return rcp(max(.000001,_ESThermalZ.z*depth+_ESThermalZ.w));
        }
        float3 Environment(int2 pixel,float distance,float detail,bool surface)
        {
            // Fixed exposure, independent of actor count and of the brightest lamp.
            // Only visible scene depth contributes; this cannot reveal hidden surfaces.
            float separation=max(max(abs(SceneDistance(pixel+int2(1,0))-distance),abs(SceneDistance(pixel-int2(1,0))-distance)),
                max(abs(SceneDistance(pixel+int2(0,1))-distance),abs(SceneDistance(pixel-int2(0,1))-distance)));
            float edge=saturate(separation/max(.15,distance*.12));
            // Keep genuine dark regions dark; a lifted full-screen floor looks like gray fog.
            // Depth adds only local structure, while lit surface texture carries most contrast.
            float structure=surface ? .07/(1+distance*.06)-.03*edge : 0;
            float gray=clamp(.035+.42*pow(saturate(detail),.8)+structure,.02,.55);
            return gray*float3(.96,.985,1);
        }
        float3 Palette(float heat)
        {
            float3 a=float3(.015,.008,.035),b=float3(.19,.02,.32),c=float3(.7,.055,.14);
            float3 d=float3(1,.38,.025),e=float3(1,.86,.25),f=float3(1,.98,.87);
            float x=saturate(heat)*5;
            return x<1?lerp(a,b,x):x<2?lerp(b,c,x-1):x<3?lerp(c,d,x-2):x<4?lerp(d,e,x-3):lerp(e,f,x-4);
        }
        float4 Composite(float4 position : SV_POSITION) : SV_Target
        {
            int2 p=position.xy;
            float raw=_ESThermalDepth.Load(int3(p,0)),sceneDepth=_ESThermalSceneDepth.Load(int3(p,0));
            float actorDistance=rcp(max(.000001,_ESThermalZ.z*raw+_ESThermalZ.w));
            float worldDistance=rcp(max(.000001,_ESThermalZ.z*sceneDepth+_ESThermalZ.w));
            bool occupied=_ESFadeReversedZ!=0?raw>.0000001:raw<.9999999;
            bool visible=occupied && actorDistance<=worldDistance+max(.01,worldDistance*.0002);
            float3 color=_ESThermalScene.Load(int3(p,0)).rgb;
            float luminance=dot(max(0,color),float3(.2126,.7152,.0722));
            float textureDetail=luminance/(1+luminance);
            // Architecture has its own readable neutral mapping; color is reserved for actors.
            float surface=rsqrt(1+min(8,abs(ddx(actorDistance))+abs(ddy(actorDistance)))*4);
            float heat=.66+.19*surface+.055*textureDetail;
            bool worldSurface=_ESFadeReversedZ!=0?sceneDepth>.0000001:sceneDepth<.9999999;
            float environmentDetail=luminance/(.35+luminance);
            return float4(visible ? Palette(heat) : Environment(p,worldDistance,environmentDetail,worldSurface),1);
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
            Blend Off
            ColorMask RGB
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Composite
            ENDHLSL
        }
    }
    Fallback Off
}
