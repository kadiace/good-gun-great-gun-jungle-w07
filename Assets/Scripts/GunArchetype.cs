using UnityEngine;

namespace WeaponWorkshop
{
    // 총 종류(저격소총, 산탄총...)의 공통 조건. 수치 X는 여기 한 곳에서 조정.
    [CreateAssetMenu(menuName = "Weapon Workshop/Gun Archetype")]
    public sealed class GunArchetype : ScriptableObject
    {
        public string displayName;
        [Tooltip("무한 모드 손님 주문 대사에 들어갈 요구사항 문구. 예) 관통 모듈(저격총)")]
        public string request;
        public GunCondition[] conditions;
        [Tooltip("총에 이 태그(총 종류)가 붙어야 함. 비우면 무시")]
        public WeaponTypeData weaponType;
        [Tooltip("같이 요구하면 만족 불가능한 요구사항. 무한 모드 조합에서 함께 뽑히지 않음 (양방향)")]
        public GunArchetype[] excludes;
        [Min(1), Tooltip("이 일차부터 조합 손님이 요구할 수 있음 (필요 모듈 해금일)")]
        public int unlockDay = 1;
        [Tooltip("이 랜덤 주문에 필요한 모듈. 하나라도 잠겨 있으면 조합 후보에서 제외")]
        public ModuleData[] requiredModules;

        public bool HasUnlockedModules(PlayerModuleUnlocks unlocks)
        {
            if (requiredModules == null) return true;
            foreach (ModuleData module in requiredModules)
                if (module == null || (unlocks != null ? !unlocks.IsUnlocked(module) : module.InitiallyLocked)) return false;
            return true;
        }

        public bool Check(EquipableWeapon weapon)
        {
            if (weaponType != null)
            {
                PowerCore core = weapon != null ? weapon.GetComponentInChildren<PowerCore>(true) : null;
                if (core == null) return false;
                var powered = WeaponTypeData.PoweredCounts(core.GetComponent<CubeAssembly>());
                if (!weaponType.Matches(core.GetCurrentStats(), powered, powered)) return false;
            }
            return GunCondition.AllMet(conditions, weapon);
        }

        public bool ConflictsWith(GunArchetype other)
            => other != null && (System.Array.IndexOf(excludes ?? new GunArchetype[0], other) >= 0
                || System.Array.IndexOf(other.excludes ?? new GunArchetype[0], this) >= 0);
    }
}
