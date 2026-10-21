using System;

namespace WeaponWorkshop
{
    /// <summary>
    /// 생존 모드의 휴대 무게, 감속 임계치, 상자 드롭 확률 및 모듈별 무게 테이블 데이터입니다.
    /// </summary>
    [Serializable]
    public sealed class SurvivalLootWeightData
    {
        [Serializable]
        public sealed class ModuleWeightEntry
        {
            public string codeId;
            public int moduleId;
            public string category;
            public float weightKg;
        }

        public float baseCarryCapacityKg = 30f;
        public float equippedWeightRatio = 0.5f;
        public float slowdownThresholdKg = 30f;
        public float slowdownPerKg = 0.033f;
        public float sprintBlockThresholdKg = 40f;
        public float immobilizeThresholdKg = 60f;
        public int structureDropCount = 2;
        public int functionDropCount = 1;
        public float normalFunctionProbability = 0.75f;
        public float heavyFunctionProbability = 0.25f;
        public float coreWeightKg = 0f;
        public ModuleWeightEntry[] modules = Array.Empty<ModuleWeightEntry>();
    }
}
