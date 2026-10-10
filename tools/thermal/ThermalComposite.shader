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
        // Half-resolution blurred visible-actor coverage, and the blur input.
        Texture2D<float> _ESThermalGlow, _ESThermalBlurSource;
        SamplerState sampler_linear_clamp;
        float4 _ESThermalZ;
        float4 _ESThermalViewport;
        float4 _ESThermalHalf; // half viewport width, height; 1 / half texture width, height
        float4 _ESThermalBlur; // blur axis: (1,0) or (0,1)
        int _ESThermalPalette; // 0 ironbow, 1 white hot, 2 rainbow
        float _ESThermalStrength; // 1 full thermal, lower values let the normal image show through
        float Distance(float depth) { return rcp(max(.000001,_ESThermalZ.z*depth+_ESThermalZ.w)); }
        float SceneDistance(int2 pixel)
        {
            pixel=clamp(pixel,int2(0,0),int2(_ESThermalViewport.xy)-1);
            return Distance(_ESThermalSceneDepth.Load(int3(pixel,0)));
        }
        // An actor pixel counts only where its own depth is not behind the visible scene.
        float VisibleActor(int2 pixel)
        {
            pixel=clamp(pixel,int2(0,0),int2(_ESThermalViewport.xy)-1);
            float raw=_ESThermalDepth.Load(int3(pixel,0));
            bool occupied=_ESFadeReversedZ!=0?raw>.0000001:raw<.9999999;
            float world=Distance(_ESThermalSceneDepth.Load(int3(pixel,0)));
            return occupied && Distance(raw)<=world+max(.01,world*.0002) ? 1 : 0;
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
            float x=saturate(heat)*5;
            float3 a=float3(.015,.008,.035),b=float3(.19,.02,.32),c=float3(.7,.055,.14);
            float3 d=float3(1,.38,.025),e=float3(1,.86,.25),f=float3(1,.98,.87);
            if(_ESThermalPalette==1)
            {
                // White hot: bodies brighten toward white with only a faint warm tint.
                a=float3(.01,.01,.012);b=float3(.12,.12,.13);c=float3(.3,.3,.31);d=float3(.58,.57,.55);e=float3(.84,.83,.8);f=float3(1,1,.98);
            }
            else if(_ESThermalPalette==2)
            {
                // Rainbow: cold blue through green and yellow to hot red and white.
                a=float3(.02,.0,.12);b=float3(.0,.18,.75);c=float3(.0,.7,.55);d=float3(.55,.9,.08);e=float3(1,.55,.02);f=float3(1,.12,.06);
            }
            return x<1?lerp(a,b,x):x<2?lerp(b,c,x-1):x<3?lerp(c,d,x-2):x<4?lerp(d,e,x-3):lerp(e,f,x-4);
        }
        // Each half-resolution texel averages its 2x2 full-resolution actor tests: anti-aliased coverage.
        float Coverage(float4 position : SV_POSITION) : SV_Target
        {
            int2 p=int2(position.xy)*2;
            return (VisibleActor(p)+VisibleActor(p+int2(1,0))+VisibleActor(p+int2(0,1))+VisibleActor(p+int2(1,1)))*.25;
        }
        // One direction of a 17-texel Gaussian (sigma 4 half-resolution texels). Each bilinear tap weighs a
        // pair of neighbouring texels, so every texel contributes and thin bodies leave no banded copies.
        // Taps clamp to the half-resolution viewport.
        float Blur(float4 position : SV_POSITION) : SV_Target
        {
            const float offsets[5]={0,1.4766,3.4455,5.415,7.3848};
            const float weights[5]={.1032,.191,.1404,.0807,.0363};
            float2 low=.5*_ESThermalHalf.zw, high=(_ESThermalHalf.xy-.5)*_ESThermalHalf.zw;
            float2 uv=position.xy*_ESThermalHalf.zw, axis=_ESThermalBlur.xy*_ESThermalHalf.zw;
            float sum=_ESThermalBlurSource.SampleLevel(sampler_linear_clamp,uv,0)*weights[0];
            [unroll] for(int i=1;i<5;i++)
            {
                sum+=_ESThermalBlurSource.SampleLevel(sampler_linear_clamp,clamp(uv+axis*offsets[i],low,high),0)*weights[i];
                sum+=_ESThermalBlurSource.SampleLevel(sampler_linear_clamp,clamp(uv-axis*offsets[i],low,high),0)*weights[i];
            }
            return sum;
        }
        float4 Composite(float4 position : SV_POSITION) : SV_Target
        {
            int2 p=position.xy;
            float sceneDepth=_ESThermalSceneDepth.Load(int3(p,0));
            float worldDistance=Distance(sceneDepth);
            float3 color=_ESThermalScene.Load(int3(p,0)).rgb;
            float luminance=dot(max(0,color),float3(.2126,.7152,.0722));
            bool worldSurface=_ESFadeReversedZ!=0?sceneDepth>.0000001:sceneDepth<.9999999;
            float3 environment=Environment(p,worldDistance,luminance/(.35+luminance),worldSurface);
            // 3x3 tent over the visible-actor test: a one-pixel soft silhouette edge that moves in quarter
            // steps instead of popping whole pixels while the camera moves. Anything at least half covered
            // stays solid, so thin limbs and distant bodies keep full contrast.
            float tent=4*VisibleActor(p)
                +2*(VisibleActor(p+int2(1,0))+VisibleActor(p-int2(1,0))+VisibleActor(p+int2(0,1))+VisibleActor(p-int2(0,1)))
                +VisibleActor(p+int2(1,1))+VisibleActor(p-int2(1,1))+VisibleActor(p+int2(1,-1))+VisibleActor(p-int2(1,-1));
            float body=smoothstep(0,8,tent);
            float2 low=.5*_ESThermalHalf.zw, high=(_ESThermalHalf.xy-.5)*_ESThermalHalf.zw;
            float glow=saturate(_ESThermalGlow.SampleLevel(sampler_linear_clamp,clamp(position.xy*.5*_ESThermalHalf.zw,low,high),0)*1.25);
            // The core reads hottest (light yellow, never blown out to white), the rim and small or distant
            // bodies cooler orange. Lit surface texture adds a little detail.
            float heat=.55+.3*glow+.04*luminance/(1+luminance);
            // A faint warm halo around visible bodies, fading into the unchanged architecture.
            float3 warmed=lerp(environment,Palette(.36+.3*glow),saturate(glow*1.6)*(1-body)*.3);
            return float4(lerp(color,lerp(warmed,Palette(heat),body),saturate(_ESThermalStrength)),1);
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
        Pass
        {
            Name "ActorCoverage"
            ZWrite Off ZTest Always Blend Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Coverage
            ENDHLSL
        }
        Pass
        {
            Name "SeparableBlur"
            ZWrite Off ZTest Always Blend Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Blur
            ENDHLSL
        }
    }
    Fallback Off
}
