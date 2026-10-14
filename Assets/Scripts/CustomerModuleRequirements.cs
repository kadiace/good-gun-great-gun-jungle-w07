using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // Checks required module families, not whether the player can currently afford the order.
    public static class CustomerModuleRequirements
    {
        public static bool Allows(CustomerProfile profile, PlayerModuleUnlocks unlocks, WeaponStats core, int day)
        {
            if (profile == null || !Allows(profile.conditions, unlocks, core)) return false;
            if (profile.archetypes != null)
                foreach (var archetype in profile.archetypes)
                    if (archetype != null && (archetype.unlockDay > day || !archetype.HasUnlockedModules(unlocks) ||
                        !Allows(archetype, unlocks, core))) return false;
            return true;
        }

        public static bool Allows(GunArchetype archetype, PlayerModuleUnlocks unlocks, WeaponStats core)
        {
            if (archetype == null || !Allows(archetype.conditions, unlocks, core)) return false;
            if (archetype.weaponType != null)
                foreach (var condition in archetype.weaponType.Conditions)
                    if (condition.comparison == WeaponTypeData.Comparison.AtLeast &&
                        !AllowsStat(condition.stat, condition.value,
                            condition.source == WeaponTypeData.ValueSource.Stat, unlocks, core)) return false;
            return true;
        }

        // 주문을 만족하는 데 필요한 최소 모듈 재화: 종류별 필요 모듈 수(요구 중 최댓값) × 모듈 재화 가격.
        public static int CreditCost(CustomerProfile profile, PlayerModuleUnlocks unlocks, WeaponStats core)
        {
            var need = new Dictionary<ModuleKind, int>();
            if (profile == null) return 0;
            Collect(profile.conditions, unlocks, core, need);
            if (profile.archetypes != null)
                foreach (var archetype in profile.archetypes) Collect(archetype, unlocks, core, need);
            return Sum(need, unlocks);
        }

        public static int CreditCost(GunArchetype archetype, PlayerModuleUnlocks unlocks, WeaponStats core)
        {
            var need = new Dictionary<ModuleKind, int>();
            Collect(archetype, unlocks, core, need);
            return Sum(need, unlocks);
        }

        private static void Collect(GunArchetype archetype, PlayerModuleUnlocks unlocks, WeaponStats core, Dictionary<ModuleKind, int> need)
        {
            if (archetype == null) return;
            Collect(archetype.conditions, unlocks, core, need);
            if (archetype.weaponType != null)
                foreach (var condition in archetype.weaponType.Conditions)
                    if (condition.comparison == WeaponTypeData.Comparison.AtLeast)
                        NeedStat(condition.stat, condition.value, condition.source != WeaponTypeData.ValueSource.Stat, unlocks, core, need);
        }

        private static void Collect(GunCondition[] conditions, PlayerModuleUnlocks unlocks, WeaponStats core, Dictionary<ModuleKind, int> need)
        {
            if (conditions == null) return;
            foreach (var condition in conditions)
            {
                if (condition == null || condition.min <= 0) continue;
                switch (condition.measure)
                {
                    case GunMeasure.SplitTwoBlocks: Need(ModuleKind.SplitTwo, Mathf.CeilToInt(condition.min), need); break;
                    case GunMeasure.SplitFourBlocks: Need(ModuleKind.SplitFour, Mathf.CeilToInt(condition.min), need); break;
                    // 총구 수·전체 모듈 수는 재화 없는 모듈로도 맞출 수 있어 제외.
                    case GunMeasure.TotalModules: case GunMeasure.Muzzles: break;
                    default: NeedStat(condition.stat, condition.min, condition.measure == GunMeasure.PathModules, unlocks, core, need); break;
                }
            }
        }

        private static void NeedStat(StatType stat, float min, bool countsModules, PlayerModuleUnlocks unlocks, WeaponStats core, Dictionary<ModuleKind, int> need)
        {
            if (!ModuleCounter.TryToKind(stat, out var kind)) return;
            if (countsModules) { Need(kind, Mathf.CeilToInt(min), need); return; }
            float missing = min - core.Get(stat);
            if (missing <= 0f) return;
            var module = unlocks != null ? unlocks.FindModule(kind) : null;
            float perModule = module != null ? module.GetStats().Get(stat) : 1f;
            Need(kind, Mathf.CeilToInt(missing / Mathf.Max(0.0001f, perModule)), need);
        }

        private static void Need(ModuleKind kind, int count, Dictionary<ModuleKind, int> need)
            => need[kind] = Mathf.Max(count, need.TryGetValue(kind, out int current) ? current : 0);

        private static int Sum(Dictionary<ModuleKind, int> need, PlayerModuleUnlocks unlocks)
        {
            long total = 0;
            foreach (var entry in need)
            {
                var module = unlocks != null ? unlocks.FindModule(entry.Key) : null;
                if (module != null) total += (long)entry.Value * Mathf.Max(0, module.ModuleCreditCost);
            }
            return (int)Mathf.Min(total, int.MaxValue);
        }

        private static bool Allows(GunCondition[] conditions, PlayerModuleUnlocks unlocks, WeaponStats core)
        {
            if (conditions == null) return true;
            foreach (var condition in conditions)
            {
                if (condition == null) return false;
                // Zero/upper-bound requirements do not require installing a locked module.
                if (condition.min <= 0) continue;
                switch (condition.measure)
                {
                    case GunMeasure.SplitTwoBlocks:
                        if (unlocks == null || !unlocks.IsUnlocked(ModuleKind.SplitTwo)) return false;
                        break;
                    case GunMeasure.SplitFourBlocks:
                        if (unlocks == null || !unlocks.IsUnlocked(ModuleKind.SplitFour)) return false;
                        break;
                    case GunMeasure.TotalModules:
                        if (unlocks == null || !unlocks.HasUnlockedStatModule()) return false;
                        break;
                    case GunMeasure.Muzzles:
                        // The core already has six outputs; larger counts need a splitter.
                        if (condition.min > 6 && (unlocks == null ||
                            (!unlocks.IsUnlocked(ModuleKind.SplitTwo) && !unlocks.IsUnlocked(ModuleKind.SplitFour)))) return false;
                        break;
                    default:
                        if (!AllowsStat(condition.stat, condition.min, condition.measure != GunMeasure.PathModules, unlocks, core)) return false;
                        break;
                }
            }
            return true;
        }

        private static bool AllowsStat(StatType stat, float min, bool includesCore, PlayerModuleUnlocks unlocks, WeaponStats core)
        {
            if (min <= 0 || (includesCore && min <= core.Get(stat))) return true;
            if (unlocks == null) return false;
            if (stat == StatType.Weight) return unlocks.HasUnlockedStatModule();
            return ModuleCounter.TryToKind(stat, out var kind) && unlocks.IsUnlocked(kind);
        }
    }
}
