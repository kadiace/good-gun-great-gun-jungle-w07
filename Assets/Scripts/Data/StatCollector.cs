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
        private readonly List<ModuleStats> moduleScan = new List<ModuleStats>();
        private readonly List<WeaponTypeData> matchedTypes = new List<WeaponTypeData>();
        private WeaponStats currentStats;
        private bool dirty = true;
        private bool recalculating;
        private CubeAssembly assembly;
        private PowerCore core;
        private uint calculatedRevision;
        private TableManager calculatedTables;

        public WeaponStats CurrentStats { get { EnsureCurrent(); return currentStats; } }
        public CoreBaseStats BaseStats => baseStats;
        public IReadOnlyList<ModuleData> Modules { get { EnsureCurrent(); return modules; } }
        public IReadOnlyList<WeaponTypeData> MatchedTypes { get { EnsureCurrent(); return matchedTypes; } }
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

        internal void MarkDirty() => dirty = true;
        private void OnTransformChildrenChanged() => MarkDirty();
        private void OnValidate() { dirty = true; WeaponAnalysis.NotifyConfigurationChanged(); }

        private void EnsureCurrent()
        {
            if (recalculating) return;
            if (assembly == null) TryGetComponent(out assembly);
            if (dirty || (assembly != null && calculatedRevision != assembly.Analysis.Revision) ||
                !ReferenceEquals(calculatedTables, TableManager.Instance)) Recalculate();
        }

        private void LateUpdate()
        {
            EnsureCurrent();
        }

        public void Recalculate()
        {
            if (recalculating) return;
            recalculating = true;
            try
            {
                if (assembly == null) TryGetComponent(out assembly);
                if (core == null) TryGetComponent(out core);
                if (statsSource == null) TryGetComponent(out statsSource);
                currentStats = baseStats != null ? baseStats.Stats : new WeaponStats { fireRate = 1f };
                modules.Clear();
                // 배열을 새로 만들지 않도록 스캔 결과를 재사용 목록에 받는다.
                moduleScan.Clear();
                GetComponentsInChildren(false, moduleScan);
                foreach (ModuleStats module in moduleScan)
                {
                    // 삭제 대기 중인 블록은 비활성 상태이므로 GetComponentsInChildren에서 제외된다.
                    if (module.Data == null) continue;
                    modules.Add(module.Data);
                }

                // Set the base before reading real muzzle stats; path effects never come from a stale whole-weapon cache.
                if (statsSource != null && baseStats != null) statsSource.SetWeaponStats(baseStats.Stats);
                currentStats = assembly != null ? assembly.Analysis.GetWholeStats(currentStats) : currentStats;
                var snapshot = CustomerRequestInfo.GetSnapshot(core);
                matchedTypes.Clear();
                EquipableWeapon playerWeapon = GetComponentInParent<EquipableWeapon>();
                if (weaponTypes != null)
                    foreach (WeaponTypeData type in weaponTypes)
                        if (type != null && TableManager.Instance.CustomerRequest.MeetsTag(type.TableId, snapshot))
                        {
                            matchedTypes.Add(type);
                            if (playerWeapon != null) TagExperience.Mark(type.TableId);
                        }

                // Projectiles get only the core base; each muzzle adds the modules on its own route.
                if (statsSource != null && baseStats != null)
                {
                    statsSource.SetWeaponStats(baseStats.Stats);
                    statsSource.SetBaseDamage(baseStats.BaseDamage);
                }
                calculatedRevision = assembly != null ? assembly.Analysis.Revision : 0;
                calculatedTables = TableManager.Instance;
                dirty = false;
            }
            finally { recalculating = false; }
            StatsChanged?.Invoke(this);
        }
    }
}
