// Keep URP lighting, textures, fog and alpha; apply damage in the same forward pass.
#define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
#define LitPassFragment UndamagedLitPassFragment
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
#undef LitPassFragment
StructuredBuffer<float2> _DamageCells;
StructuredBuffer<int4> _DamagePageIndex;
float4 _DamageOrigin, _DamageStep, _DamageDimensions, _DamageTint, _FlashColor;
float4 _DamageChunkSize;
float _DamageIndexMask, _FlashDuration, _DamageStrength, _PenetrationFlashUntil;

// Matches DamagePageHash in DestructibleTarget.Rendering.cs. Missing pages are
// undamaged; no per-target volume texture or renderer split is needed.
float2 ReadCellDamage(int3 cell)
{
    int3 chunkSize = (int3)_DamageChunkSize.xyz;
    int3 key = cell / chunkSize;
    uint mask = (uint)_DamageIndexMask;
    uint bucket = ((uint)key.x * 73856093u ^ (uint)key.y * 19349663u ^ (uint)key.z * 83492791u) & mask;
    [loop] for (uint probe = 0; probe <= mask; probe++)
    {
        int4 entry = _DamagePageIndex[bucket];
        if (entry.w < 0) return 0;
        if (all(entry.xyz == key))
        {
            int3 local = cell - key * chunkSize;
            int cellsPerPage = chunkSize.x * chunkSize.y * chunkSize.z;
            int index = local.x + chunkSize.x * (local.y + chunkSize.y * local.z);
            return _DamageCells[entry.w * cellsPerPage + index];
        }
        bucket = (bucket + 1) & mask;
    }
    return 0;
}
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
    float2 damage = ReadCellDamage((int3)clamp(cell, 0, _DamageDimensions.xyz - 1));
    float tint = saturate(damage.x * _DamageStrength) * _DamageTint.a;
    float flash = damage.x > 0 ? saturate(1 - max(0, _Time.y - damage.y) / max(.01, _FlashDuration)) * _FlashColor.a : 0;
    outColor.rgb = lerp(outColor.rgb, _DamageTint.rgb, tint);
    outColor.rgb = lerp(outColor.rgb, _FlashColor.rgb, flash);
    outColor.rgb = lerp(outColor.rgb, 1, saturate((_PenetrationFlashUntil - _Time.y) / .12));
}
