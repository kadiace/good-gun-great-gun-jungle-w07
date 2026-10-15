using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    [CreateAssetMenu(fileName = "WeaponType", menuName = "Weapon Workshop/Weapon Type")]
    [Obsolete("레거시 태그 에셋 참조. 태그 정의는 TableManager.CustomerRequest에서 조회하세요.")]
    public sealed class WeaponTypeData : ScriptableObject
    {
        [SerializeField, HideInInspector] private string tableId;
        public string TableId => tableId;
        private CustomerRequestData.Tag Definition => TableManager.Instance.CustomerRequest.FindTag(tableId);
        public enum Comparison { AtLeast, LessThan, AtMost }
        public enum ValueSource { Stat, ModuleCount, PoweredModuleCount }

        [Serializable]
        public struct Condition
        {
            public StatType stat;
            public Comparison comparison;
            public float value;
            [Tooltip("Stat: 연결 스탯(무게는 전체), ModuleCount: 연결 모듈 개수, PoweredModuleCount: 코어 에너지가 닿는 모듈 개수")]
            public ValueSource source;

            public bool IsMet(WeaponStats stats, IReadOnlyDictionary<ModuleKind, int> counts = null,
                IReadOnlyDictionary<ModuleKind, int> powered = null)
            {
                float actual = stats.Get(stat);
                if (source != ValueSource.Stat)
                {
                    var selected = source == ValueSource.ModuleCount ? counts : powered;
                    actual = selected != null && ModuleCounter.TryToKind(stat, out ModuleKind kind)
                        && selected.TryGetValue(kind, out int count) ? count : 0;
                }
                switch (comparison)
                {
                    case Comparison.AtLeast: return actual >= value;
                    case Comparison.AtMost: return actual <= value;
                    default: return actual < value;
                }
            }
        }

        [SerializeField] private string displayName;
        [SerializeField] private Color tagColor = new Color(0.16f, 0.32f, 0.42f, 1f);
        public Color TagColor => Definition?.color ?? Color.white;
        [SerializeField, Tooltip("모든 조건을 만족하면 태그 부여. 범위는 같은 항목의 이상/미만 조건 두 개로 설정.")]
        private Condition[] conditions = new Condition[0];
        [SerializeField, Min(0f)] private float sellMultiplier = 1f;
        public string DisplayName => Definition?.name ?? "미등록 태그";
        public float SellMultiplier => Definition?.sellMultiplier ?? 1f;
        // Compatibility adapter for recipes/resistance sources; definitions are not serialized twice.
        public System.Collections.Generic.IReadOnlyList<Condition> Conditions
        {
            get
            {
                var result = new System.Collections.Generic.List<Condition>();
                foreach (var condition in Definition?.conditions ?? Array.Empty<CustomerRequestData.Condition>())
                {
                    bool modules = condition.measure == GunMeasure.PathModules;
                    if (condition.min > 0f) result.Add(new Condition { stat = condition.stat,
                        source = modules ? ValueSource.ModuleCount : ValueSource.Stat, comparison = Comparison.AtLeast, value = condition.min });
                    if (condition.max < float.MaxValue) result.Add(new Condition { stat = condition.stat,
                        source = modules ? ValueSource.ModuleCount : ValueSource.Stat,
                        comparison = condition.maxExclusive ? Comparison.LessThan : Comparison.AtMost, value = condition.max });
                }
                return result;
            }
        }

        public bool Matches(WeaponStats stats, IReadOnlyDictionary<ModuleKind, int> counts = null,
            IReadOnlyDictionary<ModuleKind, int> powered = null)
        {
            var current = Conditions;
            if (current.Count == 0) return false;
            foreach (Condition condition in current)
                if (!condition.IsMet(stats, counts, powered)) return false;
            return true;
        }

        public static Dictionary<ModuleKind, int> PoweredCounts(CubeAssembly assembly)
            => WeaponAssemblyStats.PoweredCounts(assembly);
    }
}
