using System;
using UnityEngine;

namespace WeaponWorkshop
{
    // 공용 타입: 수정 전 팀 합의 필요. 모듈 하나당 에셋 하나.
    [CreateAssetMenu(fileName = "Module", menuName = "Weapon Workshop/Module Data")]
    public sealed class ModuleData : ScriptableObject
    {
        [SerializeField, HideInInspector] private string codeId;
        public string CodeId => codeId;
#if UNITY_EDITOR
        private void OnValidate()
        {
            var path = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(path)) codeId = UnityEditor.AssetDatabase.AssetPathToGUID(path);
        }
#endif
        [Serializable]
        public struct StatBonus
        {
            public StatType type;
            public float amount;
        }

        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 8)] private string description;
        [SerializeField] private Color color = UnityEngine.Color.white;
        [SerializeField, Tooltip("특별 모듈: 돈 대신 모듈 재화로 결제합니다.")]
        private bool useModuleCredits;
        [SerializeField, Min(0)] private int moduleCreditCost;
        [SerializeField, Tooltip("이 모듈의 종류(개수 계산·회전 연사 보너스 기준). 보너스 스탯이 여러 개여도 종류는 하나.")]
        private StatType moduleType;
        [SerializeField, Min(0), Tooltip("상점 구매가. 판매가 계산은 상점에서 담당.")]
        private int buyPrice = 100;
        [SerializeField, Tooltip("이 모듈을 달면 더해지는 스탯. 무게도 여기서 Weight로 지정.")]
        private StatBonus[] bonuses = new StatBonus[0];
        [SerializeField, Tooltip("조립에 사용할 모듈 프리팹.")]
        private GameObject prefab;

        public string DisplayName => displayName;
        public string Description => description;
        public Color Color => color;
        public bool UseModuleCredits => useModuleCredits;
        // 해금 규칙은 습격 표가 단일 출처다. 보상 목록에 없는 모듈은 처음부터 사용할 수 있다.
        public bool InitiallyLocked => TableManager.Instance.RaidMap.RequiresUnlock(codeId);
        public int ModuleCreditCost => moduleCreditCost;
        public StatType ModuleType => moduleType;
        public int BuyPrice => buyPrice;
        public GameObject Prefab => prefab;

        public WeaponStats GetStats()
        {
            var stats = new WeaponStats();
            if (bonuses != null)
                foreach (StatBonus bonus in bonuses) stats.Add(bonus.type, bonus.amount);
            return stats;
        }
    }
}
