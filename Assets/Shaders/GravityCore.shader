Shader "WeaponWorkshop/GravityCore" {
Properties { _BaseMap ("Texture", 2D) = "white" {} _BaseColor ("Color", Color) = (1,1,1,1) }
SubShader {
Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
Pass {
Blend SrcAlpha OneMinusSrcAlpha
ZWrite Off
ZTest Always
Cull Off
HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST; half4 _BaseColor;
CBUFFER_END
struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
Varyings Vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=TRANSFORM_TEX(i.uv,_BaseMap); o.color=i.color*_BaseColor; return o; }
half4 Frag(Varyings i):SV_Target { return SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*i.color; }
ENDHLSL
}
}
}