Shader "WeaponWorkshop/GravityLens"
{
    Properties
    {
        _Strength ("Distortion Strength", Range(0, 0.1)) = 0.025
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Tint ("Edge Tint", Color) = (0.25, 0.12, 0.45, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _Opacity;
                float4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float4 screenPos : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / input.screenPos.w;
                float3 normalVS = normalize(TransformWorldToViewDir(normalize(input.normalWS)));
                float radial = saturate(length(normalVS.xy));
                float pulse = 1 + 0.12 * sin(radial * 18 - _Time.y * 8);
                float2 offset = normalVS.xy * (1 - radial * radial) * _Strength * pulse;
                offset.x *= _ScreenParams.y / _ScreenParams.x;
                half3 scene = SampleSceneColor(saturate(uv + offset));
                float edge = pow(radial, 10) * 0.12;
                return half4(lerp(scene, _Tint.rgb, edge), _Opacity);
            }
            ENDHLSL
        }
    }
}
