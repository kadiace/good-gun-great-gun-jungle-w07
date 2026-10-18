// Keep URP lighting, textures, fog and alpha; apply damage in the same forward pass.
#define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
#define LitPassFragment UndamagedLitPassFragment
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
#undef LitPassFragment
TEXTURE3D(_CellDamage); SAMPLER(sampler_CellDamage);
float4 _DamageOrigin, _DamageStep, _DamageDimensions, _DamageTint, _FlashColor;
float _FlashDuration, _DamageStrength, _PenetrationFlashUntil;
void LitPassFragment(Varyings input, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UndamagedLitPassFragment(input, outColor
#ifdef _WRITE_RENDERING_LAYERS
        , outRenderingLayers
#endif
    );
    UNITY_SETUP_INSTANCE_ID(input);
    float3 positionOS = TransformWorldToObject(input.positionWS);
    float3 normalOS = normalize(TransformWorldToObjectDir(input.normalWS));
    float3 cell = floor((positionOS - _DamageOrigin.xyz) / _DamageStep.xyz - normalOS * .001);
    float3 uvw = (clamp(cell, 0, _DamageDimensions.xyz - 1) + .5) / _DamageDimensions.xyz;
    float2 damage = SAMPLE_TEXTURE3D(_CellDamage, sampler_CellDamage, uvw).rg;
    float tint = saturate(damage.x * _DamageStrength) * _DamageTint.a;
    float flash = damage.x > 0 ? saturate(1 - max(0, _Time.y - damage.y) / max(.01, _FlashDuration)) * _FlashColor.a : 0;
    outColor.rgb = lerp(outColor.rgb, _DamageTint.rgb, tint);
    outColor.rgb = lerp(outColor.rgb, _FlashColor.rgb, flash);
    outColor.rgb = lerp(outColor.rgb, 1, saturate((_PenetrationFlashUntil - _Time.y) / .12));
}
