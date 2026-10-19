using UnityEngine;

namespace WeaponWorkshop
{
    // 파편 생성 전역 예산. VisualDebrisCloud 공개 API 는 그대로 두고 DestructibleTarget 의
    // 낱개 Emit 호출 지점에서만 걸러서 시각 파편 수를 제한한다. 칸 제거·체력·붕괴 판정은
    // 이 클래스와 무관하게 원래대로 진행된다.
    public static class DebrisEmissionBudget
    {
        // 동시에 살아 있는 낱개 파편 수의 전역 근사 상한. 초과분은 카메라에서 먼 파편부터 생략한다.
        public static int MaxAliveDebris = 6000;
        // 한 프레임에 새로 만들 파편 수 상한. 덩어리(EmitGroup) 칸 수도 함께 센다(덩어리 자체는 생략하지 않는다).
        public static int MaxNewDebrisPerFrame = 600;
        // 이 거리 안의 낱개 파편은 예산이 남지 않았어도 항상 만든다(가까운 파괴 연출 유지).
        public static float AlwaysEmitDistance = 20f;
        // 이 거리부터 부서진 칸의 절반만, ThinHardDistance 부터 1/4만 파편으로 남긴다.
        public static float ThinStartDistance = 40f;
        public static float ThinHardDistance = 80f;

        // 거리 판정에 쓰는 카메라 위치는 프레임당 1회만 조회한다.
        private static int cachedFrame = -1;
        private static bool hasCamera;
        private static Vector3 cameraPosition;
        private static int emittedThisFrame;
        // 먼 거리 절약(1/2·1/4) 비율용 카운터.
        private static int thinningCounter;

        // VisualDebrisCloud 를 고치지 않기 위해 살아 있는 수는 (생성 수, 전달한 lifetime) 기록으로 근사한다.
        private struct DeathBatch
        {
            public float deathTime;
            public int count;
        }
        private static readonly DeathBatch[] deathBatches = new DeathBatch[16];
        private static int deathBatchCount;

        // 낱개 파편(Emit) 1개를 만들지 판정한다. false 면 이 파편 하나만 생략한다.
        public static bool TryEmitPiece(Vector3 position, float lifetime)
        {
            BeginFrame();
            // 카메라가 없으면 판정 없이 원래 동작을 유지한다.
            if (!hasCamera) return true;
            float distance = Vector3.Distance(cameraPosition, position);
            int stride = DistanceStride(distance);
            if (stride > 1 && ++thinningCounter % stride != 0) return false;
            if (distance > AlwaysEmitDistance && BudgetExhausted) return false;
            emittedThisFrame++;
            RecordEmitted(1, lifetime);
            return true;
        }

        // 덩어리(EmitGroup)는 줄이지 않는다(낙하 중 칸이 RemainingCellCount·IsDestroyed 판정에 들어간다).
        // 착지 후 낱개 파편이 되므로 살아 있는 수 추정에만 더한다.
        public static void NoteGroup(int pieces, float lifetime)
        {
            BeginFrame();
            if (pieces <= 0) return;
            emittedThisFrame += pieces;
            RecordEmitted(pieces, lifetime);
        }

        private static bool BudgetExhausted => emittedThisFrame >= MaxNewDebrisPerFrame || AliveEstimate >= MaxAliveDebris;

        // 근사 살아 있는 수: 아직 만료 기록이 남은 배치의 파편 수 합계.
        public static int AliveEstimate
        {
            get
            {
                float now = Time.time;
                int alive = 0;
                for (int i = 0; i < deathBatchCount; i++)
                {
                    if (deathBatches[i].deathTime <= now)
                    {
                        deathBatches[i] = deathBatches[deathBatchCount - 1];
                        deathBatchCount--;
                        i--;
                        continue;
                    }
                    alive += deathBatches[i].count;
                }
                return alive;
            }
        }

        private static void BeginFrame()
        {
            int frame = Time.frameCount;
            if (cachedFrame == frame) return;
            cachedFrame = frame;
            emittedThisFrame = 0;
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                hasCamera = true;
                cameraPosition = mainCamera.transform.position;
            }
            else hasCamera = false;
        }

        // 40m 부터 1/2, 80m 부터 1/4.
        private static int DistanceStride(float distance)
        {
            if (distance >= ThinHardDistance) return 4;
            return distance >= ThinStartDistance ? 2 : 1;
        }

        // 배치 수를 적게 유지하려고 만료 시각이 비슷한 생성을 한 배치로 합친다.
        private static void RecordEmitted(int count, float lifetime)
        {
            const float mergeWindow = 0.25f;
            float deathTime = Time.time + Mathf.Max(0.05f, lifetime);
            for (int i = 0; i < deathBatchCount; i++)
            {
                if (Mathf.Abs(deathBatches[i].deathTime - deathTime) > mergeWindow) continue;
                DeathBatch merged = deathBatches[i];
                merged.count += count;
                deathBatches[i] = merged;
                return;
            }
            if (deathBatchCount < deathBatches.Length)
            {
                deathBatches[deathBatchCount++] = new DeathBatch { deathTime = deathTime, count = count };
                return;
            }
            // 배치가 가득 차면 가장 이르게 만료되는 배치에 얹어 근사 오차를 감수한다.
            int earliest = 0;
            for (int i = 1; i < deathBatchCount; i++)
                if (deathBatches[i].deathTime < deathBatches[earliest].deathTime) earliest = i;
            DeathBatch overflow = deathBatches[earliest];
            overflow.count += count;
            deathBatches[earliest] = overflow;
        }
    }
}
