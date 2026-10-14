Shader "WeaponWorkshop/EndChestCosmos" {
Properties {
    _DeepColor ("Deep Space", Color) = (0.01,0.005,0.04,1)
    _NebulaA ("Nebula A", Color) = (0.35,0.1,0.8,1)
    _NebulaB ("Nebula B", Color) = (0.05,0.5,0.9,1)
    _StarColor ("Star", Color) = (1,0.95,0.85,1)
    _NebulaScale ("Nebula Scale", Float) = 2.5
    _StarDensity ("Star Density", Float) = 22
    _StarSize ("Star Size", Float) = 0.18
    _StarGain ("Star Gain", Float) = 1.6
    _Scroll ("Scroll Speed", Float) = 0.03
    _RimColor ("Rim", Color) = (0.5,0.7,1,1)
    _RimPower ("Rim Power", Float) = 3
    _Alpha ("Base Alpha", Range(0,1)) = 1
    _Glow ("Glow", Float) = 1
    [HideInInspector] _SrcBlend ("Src", Float) = 1
    [HideInInspector] _DstBlend ("Dst", Float) = 0
    [HideInInspector] _ZWrite ("ZWrite", Float) = 1
    [HideInInspector] _Cull ("Cull", Float) = 2
}
SubShader {
Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
Pass {
Tags { "LightMode"="UniversalForward" }
Blend [_SrcBlend] [_DstBlend]
ZWrite [_ZWrite]
Cull [_Cull]
HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _DeepColor, _NebulaA, _NebulaB, _StarColor, _RimColor;
float _NebulaScale, _StarDensity, _StarSize, _StarGain, _Scroll, _RimPower, _Alpha, _Glow;
CBUFFER_END
struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
struct Varyings { float4 positionCS:SV_POSITION; float3 posOS:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 viewWS:TEXCOORD2; };
Varyings Vert(Attributes i) {
    Varyings o;
    o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
    o.posOS = i.positionOS.xyz;
    o.normalWS = TransformObjectToWorldNormal(i.normalOS);
    o.viewWS = GetWorldSpaceViewDir(TransformObjectToWorld(i.positionOS.xyz));
    return o;
}
float Hash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
float Noise(float3 x) {
    float3 i = floor(x); float3 f = frac(x); f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(Hash(i), Hash(i + float3(1,0,0)), f.x), lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x), f.y),
                lerp(lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x), lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), f.x), f.y), f.z);
}
float Fbm(float3 p) { float v = 0, a = 0.5; for (int k = 0; k < 4; k++) { v += a * Noise(p); p *= 2.03; a *= 0.5; } return v; }
float Stars(float3 p) {
    float3 cell = floor(p); float3 f = frac(p);
    float h = Hash(cell);
    float3 center = float3(Hash(cell + 7.1), Hash(cell + 13.7), Hash(cell + 3.3));
    float d = length(f - center);
    float twinkle = 0.6 + 0.4 * sin(_Time.y * (1.0 + h * 4.0) + h * 40.0);
    return step(0.86, h) * smoothstep(_StarSize, 0.0, d) * twinkle;
}
half4 Frag(Varyings i) : SV_Target {
    float3 p = i.posOS * _NebulaScale + float3(0, _Time.y * _Scroll, 0);
    float n = Fbm(p);
    float m = Fbm(p * 1.7 + 11.0);
    half3 col = _DeepColor.rgb;
    col += _NebulaA.rgb * smoothstep(0.35, 0.85, n) * 0.9;
    col += _NebulaB.rgb * smoothstep(0.4, 0.9, m) * 0.7;
    float stars = Stars(i.posOS * _StarDensity);
    col += _StarColor.rgb * stars * _StarGain;
    float rim = pow(1.0 - saturate(dot(normalize(i.normalWS), normalize(i.viewWS))), _RimPower);
    col += _RimColor.rgb * rim;
    col *= _Glow;
    return half4(col, saturate(_Alpha + rim * 0.5 + stars));
}
ENDHLSL
}
}
}
