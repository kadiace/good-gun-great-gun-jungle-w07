Shader "WeaponWorkshop/HoverOutlineMask"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay-1" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask 0
            Stencil { Ref 8 ReadMask 8 WriteMask 8 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS);
            }
            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
