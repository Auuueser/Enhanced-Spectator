// Synthetic visible-surface depth fixture; never included in the shipping bundle.
Shader "Hidden/EnhancedSpectator/ThermalArchitectureTest"
{
    SubShader
    {
        Pass
        {
            Cull Off ZTest Always ZWrite On Blend Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            float4 _ESThermalViewport,_ESThermalZ;
            float _FixtureLight;
            float4 Vertex(uint id:SV_VertexID):SV_POSITION
            { return float4(id==1?3:-1,id==2?3:-1,0,1); }
            struct Output { float4 color:SV_Target; float depth:SV_Depth; };
            Output Fragment(float4 pixel:SV_POSITION)
            {
                float2 uv=pixel.xy/_ESThermalViewport.xy;
                // Near wall, recessed door, distant wall and four stair treads.
                float distance=uv.x<.3?3:uv.x<.55?12:6;
                if(uv.y<.4) distance=4+floor(uv.y*10)*2;
                float albedo=uv.x<.3?.1:uv.x<.55?.025:.2;
                Output o; o.color=float4((_FixtureLight*albedo).xxx,1);
                o.depth=(rcp(distance)-_ESThermalZ.w)/_ESThermalZ.z;
                return o;
            }
            ENDHLSL
        }
    }
}
