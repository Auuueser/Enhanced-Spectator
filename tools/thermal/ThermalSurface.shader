// Authored fallback for materials without a native depth pass. Never assigned to scene renderers.
Shader "Hidden/EnhancedSpectator/ThermalSurface" {
Properties { _MainTex("Alpha",2D)="white"{} _Cutoff("Cutoff",Float)=0 _Opacity("Opacity",Float)=1 }
SubShader { Pass { Cull Off ZWrite On ZTest LEqual ColorMask 0
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex; float4 _MainTex_ST; float _Cutoff,_Opacity; float4x4 _ESThermalVP;
struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
v2f vert(appdata_base v) { v2f o; o.pos=mul(_ESThermalVP,mul(unity_ObjectToWorld,v.vertex)); o.uv=TRANSFORM_TEX(v.texcoord,_MainTex); return o; }
float4 frag(v2f i):SV_Target { if(_Cutoff>=0) clip(tex2D(_MainTex,i.uv).a*_Opacity-max(.001,_Cutoff)); return 0; }
ENDCG
} } Fallback Off }
