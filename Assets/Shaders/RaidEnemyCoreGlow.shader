Shader "WeaponWorkshop/RaidEnemyCoreGlow"
{
    Properties
    {
        [HDR] _EmissionColor ("Core Emission", Color) = (8, 2, 0.6, 1)
        _EdgeBrightness ("Edge Brightness", Range(0, 1)) = 0.6
        _PulseAmount ("Pulse Amount", Range(0, 0.5)) = 0.15
        _PulseFrequency ("Pulse Frequency (Hz)", Range(0, 2)) = 0.6
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Unlit" }
        ZWrite On
        ZTest LEqual
        Cull Back

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _EmissionColor;
            half _EdgeBrightness;
            half _PulseAmount;
            float _PulseFrequency;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half pulse : TEXCOORD2;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            // Always luminous; the shader supplies the slow pulse without per-enemy Update or material copies.
            output.pulse = 1.0h + _PulseAmount * sin(_Time.y * _PulseFrequency * 6.2831853);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "CoreGlow"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half facing = saturate(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS)));
                half brightness = lerp(_EdgeBrightness, 1.0h, facing * facing);
                return half4(_EmissionColor.rgb * brightness * input.pulse, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragDepth
            #pragma multi_compile_instancing
            half4 FragDepth(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        // The PC renderer uses deferred rendering and SSAO. Supply normals for this forward-only opaque surface.
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            half4 FragNormals(Varyings input) : SV_Target
            {
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(normalWS);
                    half3 packedNormal = PackFloat2To888(saturate(octNormal * 0.5 + 0.5));
                    return half4(packedNormal, 0.0h);
                #else
                    return half4(normalWS, 0.0h);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
