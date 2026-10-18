Shader "WeaponWorkshop/DestructibleDamage"
{
    Properties
    {
        _DamageTint("Damage Tint", Color) = (0,0,0,1)
        _FlashColor("Hit Flash", Color) = (1,1,1,1)
        _FlashDuration("Flash Duration", Float) = 0.12
        _DamageStrength("Damage Tint Strength", Range(0,1)) = 0.45
        [HideInInspector] _PenetrationFlashUntil("Penetration Flash End", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _DamageTint, _FlashColor;
                float _FlashDuration, _DamageStrength, _PenetrationFlashUntil;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 damage : TEXCOORD3; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 damage : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.damage = input.damage;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.damage.x - 0.000001);
                float flash = saturate(1 - max(0, _Time.y - input.damage.y) / max(0.01, _FlashDuration));
                float tint = saturate(input.damage.x * _DamageStrength) * _DamageTint.a;
                float flashAlpha = flash * _FlashColor.a;
                float alpha = flashAlpha + tint * (1 - flashAlpha);
                half3 premultiplied = _FlashColor.rgb * flashAlpha + _DamageTint.rgb * tint * (1 - flashAlpha);
                // Penetration flashes white above the explosion tint, then restores its accumulated damage.
                float penetrationFlash = saturate((_PenetrationFlashUntil - _Time.y) / 0.12);
                premultiplied = penetrationFlash.xxx + premultiplied * (1 - penetrationFlash);
                alpha = penetrationFlash + alpha * (1 - penetrationFlash);
                half3 color = premultiplied / max(alpha, 0.00001);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
