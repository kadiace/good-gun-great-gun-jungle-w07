using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace WeaponWorkshop
{
    // 파편 루프에서 이번 프레임에 파편이 거친 분기. Job 이 파편마다 하나를 기록하고 메인 스레드의
    // 제출 통과(VisualDebrisCloud.SubmitAndDraw)가 이 플래그로 원래 루프와 같은 규칙을 적용한다.
    internal enum DebrisPathFlag : byte
    {
        Expired = 0,    // age >= lifetime. 제출 없이 만료 정리에서 목록에서 뺀다.
        Absorbing = 1,  // 흡수 연출. 물리 없이 보간 위치만 알파 15 버킷으로 나간다.
        Resting = 2,    // 정지 파편. cachedMatrix를 다시 제출한다(원거리면 표본 상한 적용).
        Distant = 3,    // 원거리 파편. 교대 프레임 적분, 표본 해시로 그릴지 결정.
        Near = 4        // 근거리 파편. 굴림·낙하·바닥 접촉까지 전체 물리. 벽 검사만 메인에 남는다.
    }

    // 파편 상태. VisualDebrisCloud 의 NativeList 와 Burst Job 이 같은 구조를 공유한다.
    // pose·bucket·pathFlag·sampled 는 Job 이 매 프레임 다시 기록하는 결과 필드다.
    internal struct VisualDebrisFragment
    {
        public Vector3 position, velocity, scale, spin;
        public Vector3 wallPosition;
        public float wallElapsed;
        public Quaternion rotation;
        public float age, lifetime, fade;
        public bool grounded;
        public bool absorbing;
        public bool resting;            // 착지·무속도·무회전. 수명 종료까지 자세가 그대로다.
        public bool restedDistant;      // 멀리서 정지한 파편. 가까워지면 근거리 물리로 복귀해 납작 정렬을 이어간다.
        public Matrix4x4 cachedMatrix;  // resting 동안 재계산 없이 다시 쓰는 최종 행렬.
        public bool hasCachedMatrix;    // cachedMatrix가 유효한지. 먼 파편의 생략 프레임이 같은 행렬을 다시 쓴다.
        public Vector3 absorptionStart, absorptionCenter, spreadPosition;
        public float spreadFraction;
        public float distantElapsed;
        public uint visualId;
        public Matrix4x4 pose;          // 이번 프레임 결과 행렬. 벽 해석을 통과한 파편만 메인 스레드에서 다시 만든다.
        public byte bucket;             // 알파 버킷(0~15). 흡수 파편은 항상 15.
        public DebrisPathFlag pathFlag; // 이번 프레임 분기 기록.
        public byte sampled;            // 원거리 표본 해시 통과 여부. 상한 집계는 메인 스레드가 원래 순서로 적용한다.
    }

    // 프레임 공통 파라미터. 매니저가 한 번 읽은 시선·중력·바닥값을 구름 Job 에 함께 넘긴다.
    internal struct DebrisJobParams
    {
        public float deltaTime;
        public float floorHeight;
        public float floorBounce;
        public float floorFriction;
        public float fullDetailDistance;
        public float3 viewPosition;
        public float3 gravity;
        public int frameParity;
        public byte hasView;
    }

    // 파편 적분(위치·속도·회전·바닥 접촉·굴림·정지 판정·알파 버킷 결정·행렬 생성)을 병렬로 돌린다.
    // 물리 조회가 섞인 벽 검사(DebrisWallCache)와 그리기는 메인 스레드에 남는다.
    // 수식과 판정 순서는 기존 관리 코드와 같게 옮겼다. 다만 쿼터니언 유틸 구현 차이로
    // 부동소수점 최하위 비트 수준의 오차는 있을 수 있다. 계산은 Unity.Mathematics 타입 지역 변수로
    // 하고 구조체 필드와는 경계에서만 주고받는다(Vector3+float3 혼합 연산은 해석이 모호해지므로).
    [BurstCompile]
    internal struct DebrisIntegrationJob : IJobParallelFor
    {
        public NativeArray<VisualDebrisFragment> states;  // 구름 파편 버퍼(NativeList.AsDeferredJobArray).
        [ReadOnly] public NativeArray<byte> wallSelected; // 이번 프레임에 벽 검사를 받을 파편(SelectWallChecks 결과).
        public DebrisJobParams p;

        public void Execute(int index)
        {
            VisualDebrisFragment part = states[index];
            part.age += p.deltaTime;
            if (part.age >= part.lifetime)
            {
                part.pathFlag = DebrisPathFlag.Expired;
                states[index] = part;
                return;
            }
            float alpha = math.saturate((part.lifetime - part.age) / part.fade);
            part.bucket = (byte)math.clamp((int)math.ceil(alpha * 15f), 0, 15);
            if (part.absorbing)
            {
                IntegrateAbsorbing(ref part);
                states[index] = part;
                return;
            }
            if (part.resting)
            {
                // 멀리서 정지한 파편이 근거리로 들어오면 정지를 풀어 납작 정렬·높이 보정을 이어간다.
                if (part.restedDistant && !IsDistant(part.position))
                {
                    part.resting = false;
                    part.restedDistant = false;
                    part.hasCachedMatrix = false;  // 다시 멀어지면 첫 계산에서 새 행렬을 만들게 한다.
                }
                else
                {
                    // 바닥에 앉은 파편은 수명 종료까지 자세가 고정: 물리·행렬 재계산 없이 캐시만 다시 제출한다.
                    part.pathFlag = DebrisPathFlag.Resting;
                    part.sampled = SamplePass(part.position, part.visualId);
                    states[index] = part;
                    return;
                }
            }
            if (IsDistant(part.position))
            {
                IntegrateDistant(ref part);
                states[index] = part;
                return;
            }
            IntegrateNear(index, ref part);
            states[index] = part;
        }

        private void IntegrateAbsorbing(ref VisualDebrisFragment part)
        {
            float t = math.saturate(part.age / part.lifetime);
            float absorbT = math.saturate((t - part.spreadFraction) / (1f - part.spreadFraction));
            float eased = absorbT * absorbT * (3f - 2f * absorbT);
            float3 position = part.position;
            if (t < part.spreadFraction)
            {
                float spreadT = t / part.spreadFraction;
                float outward = 1f - (1f - spreadT) * (1f - spreadT);
                position = math.lerp(part.absorptionStart, part.spreadPosition, outward);
            }
            else position = math.lerp(part.spreadPosition, part.absorptionCenter, eased);
            part.position = position;
            part.pose = ToUnityMatrix(PoseMatrix(RotationColumns(part.rotation), part.scale * (1f - eased), position));
            part.bucket = 15;
            part.pathFlag = DebrisPathFlag.Absorbing;
        }

        private void IntegrateDistant(ref VisualDebrisFragment part)
        {
            part.distantElapsed += p.deltaTime;
            bool advancing = ((p.frameParity + (int)part.visualId) & 1) == 0;
            if (advancing)
            {
                AdvanceDistant(ref part, part.distantElapsed);
                part.distantElapsed = 0f;
                // 멀리 있는 파편은 회전 갱신이 없으므로 바닥 접촉 후 속도가 0이면 더는 움직이지 않는다. 여기서 정지로 본다.
                // 가까워지면 납작 정렬을 이어가도록 먼 정지로 표시한다.
                if (part.grounded && math.lengthsq(part.velocity) == 0f)
                {
                    part.resting = true;
                    part.restedDistant = true;
                }
            }
            // 움직이지 않는 교대 프레임은 지난 행렬을 다시 쓴다. 정지 파편의 cachedMatrix가 곧 최종 자세다.
            if (advancing || !part.hasCachedMatrix)
            {
                part.pose = ToUnityMatrix(PoseMatrix(RotationColumns(part.rotation), part.scale, part.position));
                part.cachedMatrix = part.pose;
                part.hasCachedMatrix = true;
            }
            else part.pose = part.cachedMatrix;
            part.pathFlag = DebrisPathFlag.Distant;
            part.sampled = SamplePass(part.position, part.visualId);
        }

        private void AdvanceDistant(ref VisualDebrisFragment part, float dt)
        {
            float3 position = part.position;
            float3 velocity = part.velocity;
            if (part.grounded)
            {
                velocity.y = 0f;
                float3 previous = velocity;
                velocity = MoveTowardsZero(velocity, p.floorFriction * dt);
                if (math.lengthsq(velocity) < 0.0225f) velocity = float3.zero;
                position += (previous + velocity) * (0.5f * dt);
            }
            else
            {
                position += velocity * dt + p.gravity * (0.5f * dt * dt);
                velocity += p.gravity * dt;
                float restingY = p.floorHeight + HalfHeight(part.rotation, part.scale);
                if (position.y <= restingY)
                {
                    position.y = restingY;
                    if (velocity.y < 0f)
                    {
                        float rebound = -velocity.y * p.floorBounce;
                        part.grounded = rebound < 0.45f;
                        velocity.y = part.grounded ? 0f : rebound;
                    }
                }
            }
            part.position = position;
            part.velocity = velocity;
            part.wallPosition = part.position;
            part.wallElapsed = 0f;
        }

        private void IntegrateNear(int index, ref VisualDebrisFragment part)
        {
            float step = p.deltaTime + part.distantElapsed;
            part.distantElapsed = 0f;
            float3 position = part.position;
            float3 velocity = part.velocity;
            float3 scale = part.scale;
            float3 spin = part.spin;
            quaternion rotation = part.rotation;
            if (part.grounded)
            {
                velocity.y = 0f;
                float3 previousVelocity = velocity;
                velocity = MoveTowardsZero(velocity, p.floorFriction * step);
                if (math.lengthsq(velocity) < 0.0225f) velocity = float3.zero;
                float3 movement = (previousVelocity + velocity) * (0.5f * step);
                position += movement;
                float rollingRadius = math.max(0.01f, math.max(scale.x, scale.z) * 0.5f);
                if (math.lengthsq(movement) > 0.00000001f)
                {
                    float3 axis = math.normalizesafe(math.cross(math.up(), movement));
                    rotation = math.normalize(math.mul(
                        quaternion.AxisAngle(axis, math.length(movement) / rollingRadius), rotation));
                }
                else
                {
                    // Settle onto the nearest cube face without a collider/contact solver.
                    quaternion previousRotation = rotation;
                    float3 bestAxis = math.mul(rotation, math.up());
                    float best = math.abs(bestAxis.y);
                    float3 right = math.mul(rotation, math.right());
                    float3 forward = math.mul(rotation, math.forward());
                    if (math.abs(right.y) > best) { bestAxis = right; best = math.abs(right.y); }
                    if (math.abs(forward.y) > best) bestAxis = forward;
                    if (bestAxis.y < 0f) bestAxis = -bestAxis;
                    quaternion flat = math.mul(FromToUp(bestAxis), rotation);
                    rotation = RotateTowards(rotation, flat, 180f * step);
                    // 속도가 0이고 회전 변화가 사실상 사라지면 이후 수명 종료까지 제자리에 있다.
                    part.resting = math.lengthsq(velocity) == 0f &&
                        math.dot(previousRotation, rotation) > 0.999999f;
                }
                spin = float3.zero;
            }
            else
            {
                position += velocity * step + p.gravity * (0.5f * step * step);
                velocity += p.gravity * step;
            }
            if (!part.grounded)
                rotation = math.normalize(math.mul(quaternion.Euler(math.radians(spin * step)), rotation));
            // 회전 행렬을 한 번 만들어 높이 검사와 최종 행렬이 함께 쓴다.
            float3x3 rotationColumns = RotationColumns(rotation);
            // Cube extent along world Y prevents rotating corners from sinking below the plane.
            float halfHeight = 0.5f * (math.abs(rotationColumns.c0.y) * scale.x +
                math.abs(rotationColumns.c1.y) * scale.y + math.abs(rotationColumns.c2.y) * scale.z);
            float restingY = p.floorHeight + halfHeight;
            if (part.grounded || position.y <= restingY)
            {
                position.y = restingY;
                if (!part.grounded && velocity.y < 0f)
                {
                    float impactSpeed = -velocity.y;
                    if (velocity.x * velocity.x + velocity.z * velocity.z < 0.04f)
                    {
                        float3 scatter = math.normalizesafe(new float3(spin.z, 0f, -spin.x));
                        velocity += scatter * math.min(1.2f, impactSpeed * 0.15f);
                    }
                    float rebound = impactSpeed * p.floorBounce;
                    part.grounded = rebound < 0.45f;
                    velocity.y = part.grounded ? 0f : rebound;
                }
            }
            part.wallElapsed += step;
            bool fast = math.lengthsq(velocity) > 0.0225f;
            if (wallSelected[index] != 0 && fast)
            {
                // 허용 분(TryWallCheck)과 실제 벽 해석은 물리 조회 캐시를 쓰므로 메인 스레드가 한다.
                // 앵커·누적 시간은 해석 입력이라 여기서 갱신하지 않는다.
            }
            else if (!fast)
            {
                part.wallPosition = position;
                part.wallElapsed = 0f;
            }
            part.pose = ToUnityMatrix(PoseMatrix(rotationColumns, scale, position));
            if (part.resting) { part.cachedMatrix = part.pose; part.hasCachedMatrix = true; }
            part.position = position;
            part.velocity = velocity;
            part.scale = scale;
            part.spin = spin;
            part.rotation = rotation;
            part.pathFlag = DebrisPathFlag.Near;
        }

        private bool IsDistant(float3 position)
        {
            return p.hasView != 0 &&
                math.lengthsq(position - p.viewPosition) > p.fullDetailDistance * p.fullDetailDistance;
        }

        // DrawDistant 의 해시 표본 판정. 상한 집계는 메인 스레드가 원래 순서(역순)로 적용한다.
        private byte SamplePass(float3 position, uint visualId)
        {
            float distance = math.distance(position, p.viewPosition);
            float fraction = math.lerp(1f, 0.25f, math.saturate((distance - p.fullDetailDistance) / 18f));
            return (visualId * 2654435761u) / (float)uint.MaxValue < fraction ? (byte)1 : (byte)0;
        }

        private static float HalfHeight(quaternion rotation, float3 scale)
        {
            float3x3 r = RotationColumns(rotation);
            return 0.5f * (math.abs(r.c0.y) * scale.x + math.abs(r.c1.y) * scale.y + math.abs(r.c2.y) * scale.z);
        }

        // Vector3.MoveTowards(current, zero, maxDelta)와 같은 수식.
        private static float3 MoveTowardsZero(float3 current, float maxDelta)
        {
            float sqrmag = math.lengthsq(current);
            if (sqrmag == 0f || (maxDelta >= 0f && sqrmag <= maxDelta * maxDelta)) return float3.zero;
            float dist = math.sqrt(sqrmag);
            return current - current * (maxDelta / dist);
        }

        // Vector3.FromToRotation(bestAxis, up)에 해당. bestAxis.y 는 0 이상으로 보정되어
        // 정반대 케이스는 오지 않는다. 축이 퇴화하면 회전 없이 통과한다.
        private static quaternion FromToUp(float3 from)
        {
            float3 f = math.normalizesafe(from);
            float d = math.clamp(math.dot(f, math.up()), -1f, 1f);
            if (d > 0.999999f) return quaternion.identity;
            float3 axis = math.normalizesafe(math.cross(f, math.up()));
            if (math.lengthsq(axis) == 0f) return quaternion.identity;
            return quaternion.AxisAngle(axis, math.acos(d));
        }

        // Quaternion.RotateTowards 와 같은 규칙: 남은 각도가 한도보다 작으면 목표, 아니면 남은 한도만 slerp.
        private static quaternion RotateTowards(quaternion from, quaternion to, float maxDegreesDelta)
        {
            float angle = math.degrees(2f * math.acos(math.clamp(math.abs(math.dot(from, to)), 0f, 1f)));
            if (angle <= 0f) return to;
            return math.slerp(from, to, math.min(1f, maxDegreesDelta / angle));
        }

        // 쿼터니언→회전 행렬(열 저장). 기존 RotationMatrix 와 같은 수식으로 메인 스레드 결과와 맞춘다.
        private static float3x3 RotationColumns(quaternion q)
        {
            float x = q.value.x, y = q.value.y, z = q.value.z, w = q.value.w;
            float xx = x * x, yy = y * y, zz = z * z;
            float xy = x * y, xz = x * z, yz = y * z;
            float wx = w * x, wy = w * y, wz = w * z;
            return new float3x3(
                new float3(1f - 2f * (yy + zz), 2f * (xy + wz), 2f * (xz - wy)),
                new float3(2f * (xy - wz), 1f - 2f * (xx + zz), 2f * (yz + wx)),
                new float3(2f * (xz + wy), 2f * (yz - wx), 1f - 2f * (xx + yy)));
        }

        // 회전 행렬에 스케일·이동을 합친다. 기존 ScaleTranslate 와 같은 결과다.
        private static float4x4 PoseMatrix(float3x3 rotation, float3 scale, float3 position)
        {
            return new float4x4(
                new float4(rotation.c0 * scale.x, 0f),
                new float4(rotation.c1 * scale.y, 0f),
                new float4(rotation.c2 * scale.z, 0f),
                new float4(position, 1f));
        }

        // float4x4→Matrix4x4. 두 형식 모두 열 우선 저장이라 필드만 옮긴다.
        private static Matrix4x4 ToUnityMatrix(float4x4 m)
        {
            var u = new Matrix4x4();
            u.m00 = m.c0.x; u.m01 = m.c1.x; u.m02 = m.c2.x; u.m03 = m.c3.x;
            u.m10 = m.c0.y; u.m11 = m.c1.y; u.m12 = m.c2.y; u.m13 = m.c3.y;
            u.m20 = m.c0.z; u.m21 = m.c1.z; u.m22 = m.c2.z; u.m23 = m.c3.z;
            u.m30 = m.c0.w; u.m31 = m.c1.w; u.m32 = m.c2.w; u.m33 = m.c3.w;
            return u;
        }
    }
}
