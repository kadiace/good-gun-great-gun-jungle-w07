using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 코어 기본 스탯 + 에너지 연결 모듈 스탯(무게는 전체)로 총 종류(태그)와 판매가를 판정한다.
    // 투사체에는 코어 기본 스탯만 전달하고, 모듈 효과는 총구(경로)별로 PowerCore가 적용한다.
    // CubeAssembly와 같은 오브젝트에 붙인다. 조립 변경은 자식 변화로 감지한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerCore))]
    public sealed class StatCollector : MonoBehaviour
    {
        [SerializeField] private CoreBaseStats baseStats;
        [SerializeField] private WeaponTypeData[] weaponTypes = new WeaponTypeData[0];
        [SerializeField] private ProjectileStatsSource statsSource;

        private readonly List<ModuleData> modules = new List<ModuleData>();
        private readonly List<WeaponTypeData> matchedTypes = new List<WeaponTypeData>();
        private WeaponStats currentStats;
        private bool dirty = true;

        public WeaponStats CurrentStats => currentStats;
        public CoreBaseStats BaseStats => baseStats;
        public IReadOnlyList<ModuleData> Modules => modules;
        public IReadOnlyList<WeaponTypeData> MatchedTypes => matchedTypes;
        public event System.Action<StatCollector> StatsChanged;

        private void Awake()
        {
            if (statsSource == null) statsSource = GetComponent<ProjectileStatsSource>();
        }

        private void OnEnable()
        {
            dirty = true;
            Recalculate();
        }

        private void OnTransformChildrenChanged() => dirty = true;

        private void LateUpdate()
        {
            if (dirty) Recalculate();
        }

        public void Recalculate()
        {
            dirty = false;
            currentStats = baseStats != null ? baseStats.Stats : new WeaponStats { fireRate = 1f };
            modules.Clear();
            foreach (ModuleStats module in GetComponentsInChildren<ModuleStats>())
            {
                // 삭제 대기 중인 블록은 비활성 상태이므로 GetComponentsInChildren에서 제외된다.
                if (module.Data == null) continue;
                modules.Add(module.Data);

            }

            var assembly = GetComponent<CubeAssembly>();
            currentStats = WeaponAssemblyStats.Calculate(assembly, currentStats);
            // Set the base before reading real muzzle stats; path effects never come from a stale whole-weapon cache.
            if (statsSource != null && baseStats != null) statsSource.SetWeaponStats(baseStats.Stats);
            var snapshot = new CustomerRequestInfo.WeaponSnapshot(GetComponent<PowerCore>());
            matchedTypes.Clear();
            if (weaponTypes != null)
                foreach (WeaponTypeData type in weaponTypes)
                    if (type != null && TableManager.Instance.CustomerRequest.MeetsTag(type.TableId, snapshot)) matchedTypes.Add(type);

            // Projectiles get only the core base; each muzzle adds the modules on its own route.
            if (statsSource != null && baseStats != null)
            {
                statsSource.SetWeaponStats(baseStats.Stats);
                statsSource.SetBaseDamage(baseStats.BaseDamage);
            }
            StatsChanged?.Invoke(this);
        }
    }
}
