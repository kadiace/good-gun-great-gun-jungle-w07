using System;
using System.Collections.Generic;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 생존 모드 전리품 및 무게 테이블의 검증, 조회 및 상자 드롭 추첨 기능을 제공하는 정적 클래스입니다.
    /// </summary>
    public static class SurvivalLootWeightInfo
    {
        private static readonly int[] StructureModuleIds = { 1, 2, 3, 4, 18 };
        private static readonly int[] NormalModuleIds = { 5, 6, 11, 12, 13, 14, 15, 17 };
        private static readonly int[] HeavyModuleIds = { 7, 8, 9, 10, 16 };

        private static readonly Dictionary<int, float> WeightByModuleId = new Dictionary<int, float>();
        private static readonly Dictionary<string, float> WeightByCodeId = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<int> DisabledModuleIds = new HashSet<int>();

        /// <summary>
        /// 생존 모드 전리품 및 무게 테이블 데이터를 검증하고 캐시를 구성합니다.
        /// </summary>
        /// <param name="data">검증할 테이블 데이터 인스턴스입니다.</param>
        public static void Validate(SurvivalLootWeightData data)
        {
            WeightByModuleId.Clear();
            WeightByCodeId.Clear();
            DisabledModuleIds.Clear();

            if (data == null || data.modules == null)
            {
                Debug.LogWarning("SurvivalLootWeightData 데이터가 null이므로 기본 캐시가 비어 있습니다.");
                return;
            }

            for (int i = 0; i < data.modules.Length; i++)
            {
                SurvivalLootWeightData.ModuleWeightEntry entry = data.modules[i];
                if (entry == null) continue;

                WeightByModuleId[entry.moduleId] = entry.weightKg;
                if (!string.IsNullOrEmpty(entry.codeId))
                {
                    WeightByCodeId[entry.codeId] = entry.weightKg;
                }

                if (string.Equals(entry.category, "Disabled", StringComparison.OrdinalIgnoreCase))
                {
                    DisabledModuleIds.Add(entry.moduleId);
                }
            }
        }

        /// <summary>
        /// 모듈 슬롯 번호(1~19)에 대응하는 무게(kg)를 조회합니다.
        /// </summary>
        /// <param name="moduleId">모듈 고유 슬롯 ID입니다.</param>
        /// <returns>설정된 무게(kg)를 반환하며, 등록되지 않은 경우 0.5kg 기본값을 반환합니다.</returns>
        public static float GetWeightKg(int moduleId)
        {
            if (WeightByModuleId.TryGetValue(moduleId, out float weight))
            {
                return weight;
            }
            return 0.5f;
        }

        /// <summary>
        /// 모듈의 에셋 식별 코드(GUID)에 대응하는 무게(kg)를 조회합니다.
        /// </summary>
        /// <param name="codeId">모듈 에셋 식별 문자열입니다.</param>
        /// <returns>설정된 무게(kg)를 반환하며, 조회 실패 시 0.5kg을 반환합니다.</returns>
        public static float GetWeightKg(string codeId)
        {
            if (!string.IsNullOrEmpty(codeId) && WeightByCodeId.TryGetValue(codeId, out float weight))
            {
                return weight;
            }
            return 0.5f;
        }

        /// <summary>
        /// 해당 모듈이 생존 모드에서 비활성화(컬러 등)되어 있는지 확인합니다.
        /// </summary>
        /// <param name="moduleId">검사할 모듈 슬롯 ID입니다.</param>
        /// <returns>생존 모드 비활성 대상이면 true를 반환합니다.</returns>
        public static bool IsDisabledInSurvival(int moduleId)
        {
            return DisabledModuleIds.Contains(moduleId);
        }

        /// <summary>
        /// 생존 상자 파괴 시 방출할 3개 블록(구조 2종 균등 독립 추첨 + 일반/중화기 확률 추첨 1종)을 결정합니다.
        /// </summary>
        /// <param name="rng">난수 생성기 인스턴스입니다. null이면 기본 UnityEngine.Random을 사용합니다.</param>
        /// <param name="droppedModuleIds">추첨된 3개의 모듈 슬롯 ID 배열입니다.</param>
        public static void RollCrateDrops(System.Random rng, out int[] droppedModuleIds)
        {
            droppedModuleIds = new int[3];

            // 구조 블록 5종 중 독립 균등 추첨으로 2개를 선택합니다.
            for (int i = 0; i < 2; i++)
            {
                int index = rng != null ? rng.Next(StructureModuleIds.Length) : UnityEngine.Random.Range(0, StructureModuleIds.Length);
                droppedModuleIds[i] = StructureModuleIds[index];
            }

            // 일반 기능 모듈(75%) 또는 중화기 기능 모듈(25%) 중 1개를 추첨합니다.
            double roll = rng != null ? rng.NextDouble() : UnityEngine.Random.value;
            var table = TableManager.Instance.SurvivalLootWeight;
            float normalProb = table != null ? table.normalFunctionProbability : 0.75f;

            if (roll < normalProb)
            {
                int index = rng != null ? rng.Next(NormalModuleIds.Length) : UnityEngine.Random.Range(0, NormalModuleIds.Length);
                droppedModuleIds[2] = NormalModuleIds[index];
            }
            else
            {
                int index = rng != null ? rng.Next(HeavyModuleIds.Length) : UnityEngine.Random.Range(0, HeavyModuleIds.Length);
                droppedModuleIds[2] = HeavyModuleIds[index];
            }
        }
    }
}
