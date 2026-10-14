Shader "WeaponWorkshop/HoverOutlineFill"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 0.75, 0.15, 1)
        _OutlineWidth("Outline Width (pixels)", Float) = 3
        _OutlineOffset("Outline Offset", Vector) = (1, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil { Ref 8 ReadMask 8 WriteMask 0 Comp NotEqual Pass Keep }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(OutlineInstances)
                UNITY_DEFINE_INSTANCED_PROP(float4, _OutlineOffset)
            UNITY_INSTANCING_BUFFER_END(OutlineInstances)
            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 clipPosition = TransformObjectToHClip(input.positionOS);
                float2 offset = UNITY_ACCESS_INSTANCED_PROP(OutlineInstances, _OutlineOffset).xy;
                clipPosition.xy += offset * _OutlineWidth * 2.0 / _ScaledScreenParams.xy * clipPosition.w;
                return clipPosition;
            }
            half4 Frag() : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
