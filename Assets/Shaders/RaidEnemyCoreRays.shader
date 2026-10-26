Shader "WeaponWorkshop/RaidEnemyCoreRays"
{
    Properties
    {
        [HDR] _RayColor ("Ray Color", Color) = (4, 1.2, 0.3, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.6
        _RayLength ("Ray Length (Metres)", Range(0.1, 6)) = 2
        _RayCount ("Ray Count", Range(3, 16)) = 8
        _RayWidth ("Ray Width", Range(0.03, 0.6)) = 0.18
        _RotationSpeed ("Rotation Speed (Radians/sec)", Range(-0.3, 0.3)) = 0.06
        _PulseAmount ("Pulse Amount", Range(0, 0.5)) = 0.15
        _PulseFrequency ("Pulse Frequency (Hz)", Range(0, 2)) = 0.6
        _DepthBias ("Source Depth Tolerance (Metres)", Range(0.001, 0.02)) = 0.004
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "CoreRays"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RayColor;
                float _RayLength;
                float _RayCount;
                float _RayWidth;
                float _RotationSpeed;
                float _PulseFrequency;
                float _DepthBias;
                half _Intensity;
                half _PulseAmount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 rayUV : TEXCOORD0;
                half strength : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // All three authored cores use Unity's Y cylinder (radius .5, half-height 1).
            // Intersect in core-local space to compare against its actual surface, not its hidden centre or AABB.
            float CoreSurfaceDistance(float3 origin, float3 direction)
            {
                float nearest = 1e8;
                float a = dot(direction.xz, direction.xz);
                float b = dot(origin.xz, direction.xz);
                float c = dot(origin.xz, origin.xz) - 0.25;
                float discriminant = b * b - a * c;
                if (a > 1e-10 && discriminant >= 0.0)
                {
                    float root = sqrt(discriminant);
                    float nearSide = (-b - root) / a;
                    float farSide = (-b + root) / a;
                    if (nearSide > 0.0 && abs(origin.y + direction.y * nearSide) <= 1.0) nearest = nearSide;
                    if (farSide > 0.0 && abs(origin.y + direction.y * farSide) <= 1.0) nearest = min(nearest, farSide);
                }
                if (abs(direction.y) > 1e-8)
                {
                    float bottom = (-1.0 - origin.y) / direction.y;
                    float top = (1.0 - origin.y) / direction.y;
                    float2 bottomXZ = origin.xz + direction.xz * bottom;
                    float2 topXZ = origin.xz + direction.xz * top;
                    if (bottom > 0.0 && dot(bottomXZ, bottomXZ) <= 0.25) nearest = min(nearest, bottom);
                    if (top > 0.0 && dot(topXZ, topXZ) <= 0.25) nearest = min(nearest, top);
                }
                return nearest;
            }

            float ProjectedCoreRadius(float3 axis)
            {
                float3 x = TransformObjectToWorldDir(float3(1, 0, 0), false);
                float3 y = TransformObjectToWorldDir(float3(0, 1, 0), false);
                float3 z = TransformObjectToWorldDir(float3(0, 0, 1), false);
                return 0.5 * length(float2(dot(x, axis), dot(z, axis))) + abs(dot(y, axis));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 centre = TransformObjectToWorld(float3(0, 0, 0));
                float3 right = UNITY_MATRIX_I_V._m00_m10_m20;
                float3 up = UNITY_MATRIX_I_V._m01_m11_m21;
                float3 backward = UNITY_MATRIX_I_V._m02_m12_m22;
                float radiusX = ProjectedCoreRadius(right);
                float radiusY = ProjectedCoreRadius(up);
                float3 anchor = 0;
                float total = 0;

                // 7x7 checks in the four vertices only. No CPU visibility rays, readback or particles.
                // A centre-only check would miss the narrow gaps this effect needs to reveal.
                [loop] for (int y = 0; y < 7; y++)
                {
                    [loop] for (int x = 0; x < 7; x++)
                    {
                        float2 offset = (float2(x, y) / 6.0 * 2.0 - 1.0) * 0.92;
                        float3 samplePositionWS = centre + right * offset.x * radiusX + up * offset.y * radiusY;
                        float3 originWS = lerp(GetCameraPositionWS(), samplePositionWS + backward * _ProjectionParams.z, unity_OrthoParams.w);
                        float3 directionWS = lerp(normalize(samplePositionWS - GetCameraPositionWS()), -backward, unity_OrthoParams.w);
                        float3 originOS = TransformWorldToObject(originWS);
                        float3 directionOS = mul((float3x3)GetWorldToObjectMatrix(), directionWS);
                        float distance = CoreSurfaceDistance(originOS, directionOS);
                        if (distance >= 1e7) continue;
                        float3 surfaceWS = originWS + directionWS * distance;
                        float4 surfaceCS = TransformWorldToHClip(surfaceWS);
                        if (surfaceCS.w <= 0.0) continue;
                        float4 screen = ComputeScreenPos(surfaceCS);
                        float2 uv = screen.xy / screen.w;
                        if (any(uv < 0.0) || any(uv > 1.0)) continue;
                        uv = UnityStereoTransformScreenSpaceTex(uv);
                        float rawDepth = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, uv, 0).r;
                        float sceneDepth = lerp(LinearEyeDepth(rawDepth, _ZBufferParams), LinearDepthToEyeDepth(rawDepth), unity_OrthoParams.w);
                        float sourceDepth = -TransformWorldToView(surfaceWS).z;
                        float visible = smoothstep(-_DepthBias, 0.0, sceneDepth - sourceDepth);
                        anchor += surfaceWS * visible;
                        total += visible;
                    }
                }

                anchor = total > 0.0 ? anchor / total : centre;
                // The quad starts at the visible part of the core and still depth-tests against the shell.
                float2 rayUV = input.uv * 2.0 - 1.0;
                // Fully hidden cores collapse the quad instead of shading an invisible large rectangle.
                float3 positionWS = anchor + (right * rayUV.x + up * rayUV.y) * _RayLength * step(0.0001, total);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.rayUV = rayUV;
                output.strength = saturate(total * 0.25) * _Intensity *
                    (1.0h + _PulseAmount * sin(_Time.y * _PulseFrequency * 6.2831853));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float radius = length(input.rayUV);
                clip(min(1.0 - radius, input.strength - 0.001));
                float angle = atan2(input.rayUV.y, input.rayUV.x) - _Time.y * _RotationSpeed;
                float spokeDistance = abs(frac(angle * round(_RayCount) / 6.2831853 + 0.5) * 2.0 - 1.0);
                float spoke = saturate(1.0 - spokeDistance / _RayWidth);
                float falloff = (1.0 - radius) * (1.0 - radius);
                float mask = spoke * spoke * falloff * smoothstep(0.0, 0.04, radius);
                clip(mask - 0.001);
                return half4(_RayColor.rgb, mask * input.strength * _RayColor.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
