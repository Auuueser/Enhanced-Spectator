// Synthetic offline GPU fixture only; excluded from the shipping asset bundle.
Shader "Hidden/EnhancedSpectator/NativeFadeGpuTest"
{
    SubShader
    {
        Cull Off
        HLSLINCLUDE
        #pragma target 4.5
        float4 _Color;
        float _Depth;
        float _Samples;
        float _ReversedZ;
        float4 Vertex(uint id : SV_VertexID) : SV_POSITION
        { return float4(id == 1 ? 3 : -1, id == 2 ? 3 : -1, 0, 1); }
        struct Output { float4 color : SV_Target; float depth : SV_Depth; };
        Output Fragment()
        { Output o; o.color = _Color; o.depth = _Depth; return o; }
        Output Cutout(float4 position : SV_POSITION)
        { clip(16 - position.x); return Fragment(); }
        Output Samples(uint sample : SV_SampleIndex)
        {
            Output o;
            o.color = float4(.1 + .6 * sample / max(1, _Samples - 1), .2, .6, .37);
            float distance = sample == (uint)_Samples - 1 ? .2 : .8;
            o.depth = _ReversedZ != 0 ? 1 - distance : distance;
            return o;
        }
        ENDHLSL
        Pass
        {
            ZTest Always ZWrite On Blend Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            ENDHLSL
        }
        Pass
        {
            ZTest LEqual ZWrite On Blend Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            ENDHLSL
        }
        Pass
        {
            ZTest LEqual ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            ENDHLSL
        }
        Pass
        {
            ZTest Always ZWrite On Blend Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Samples
            ENDHLSL
        }
        // Native HDRP opaque forward may omit clip, depending on its depth prepass.
        Pass
        {
            Name "CutoutDepth"
            ZTest LEqual ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Cutout
            ENDHLSL
        }
        Pass
        {
            Name "OpaqueForwardEqual"
            ZTest Equal ZWrite Off Blend Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            ENDHLSL
        }
        Pass
        {
            Name "UnclippedForwardRegression"
            ZTest LEqual ZWrite On Blend Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            ENDHLSL
        }
        Pass
        {
            Name "SolidDepth"
            ZTest LEqual ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            ENDHLSL
        }
    }
}
