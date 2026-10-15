using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // Request definitions come from TableManager. Module pricing/unlocks keep their existing API.
    public static class CustomerModuleRequirements
    {
        // 고정 손님은 주문 표의 unlockDay(기본 0)부터 받는다. 무한 모드 조합은 생성 때 손님 풀 unlockDay로 이미 거른다.
        public static bool Allows(CustomerProfile profile, PlayerModuleUnlocks unlocks, WeaponStats core, int day)
            => profile != null && profile.UnlockDay <= day && Allows(profile.RequestIds, unlocks, core);
        // 필요한 모듈 해금·사격 테스트는 요청 조건에서 계산한다.
        public static bool Allows(string[] ids, PlayerModuleUnlocks unlocks, WeaponStats core)
        {
            var table = TableManager.Instance.CustomerRequest;
            if (!table.ContainsOrder(ids)) return false;
            foreach (string id in ids)
            {
                var row = table.FindRequest(id);
                foreach (string testId in row.requiredTestIds ?? Array.Empty<string>())
                    if (!TableManager.Instance.ShootingTestTable.IsAvailable(testId)) return false;
                foreach (var condition in table.Conditions(row))
                {
                    if (condition.min <= 0f) continue;
                    switch (condition.Measure)
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
                            if (condition.min > 6 && (unlocks == null ||
                                (!unlocks.IsUnlocked(ModuleKind.SplitTwo) && !unlocks.IsUnlocked(ModuleKind.SplitFour)))) return false;
                            break;
                        default:
                            if (condition.Measure != GunMeasure.PathModules && condition.min <= core.Get(condition.Stat)) break;
                            if (condition.Stat == StatType.Weight)
                            { if (unlocks == null || !unlocks.HasUnlockedStatModule()) return false; }
                            else if (unlocks == null || !ModuleCounter.TryToKind(condition.Stat, out var kind) || !unlocks.IsUnlocked(kind)) return false;
                            break;
                    }
                }
            }
            return true;
        }
        public static int CreditCost(CustomerProfile profile, PlayerModuleUnlocks unlocks, WeaponStats core)
            => profile == null ? 0 : CreditCost(profile.RequestIds, unlocks, core);
        public static int CreditCost(string[] ids, PlayerModuleUnlocks unlocks, WeaponStats core)
        {
            var need = new Dictionary<ModuleKind, int>();
            var table = TableManager.Instance.CustomerRequest;
            foreach (string id in ids ?? Array.Empty<string>())
            {
                var row = table.FindRequest(id);
                if (row == null) continue;
                foreach (var condition in table.Conditions(row))
                {
                    if (condition.min <= 0f) continue;
                    switch (condition.Measure)
                    {
                        case GunMeasure.SplitTwoBlocks: Need(ModuleKind.SplitTwo, Mathf.CeilToInt(condition.min), need); break;
                        case GunMeasure.SplitFourBlocks: Need(ModuleKind.SplitFour, Mathf.CeilToInt(condition.min), need); break;
                        case GunMeasure.TotalModules: case GunMeasure.Muzzles: break;
                        default:
                            if (!ModuleCounter.TryToKind(condition.Stat, out var kind)) break;
                            float missing = condition.Measure == GunMeasure.PathModules ? condition.min : condition.min - core.Get(condition.Stat);
                            if (missing <= 0f) break;
                            var module = unlocks != null ? unlocks.FindModule(kind) : null;
                            float amount = condition.Measure == GunMeasure.PathModules ? 1f : module != null ? module.GetStats().Get(condition.Stat) : 1f;
                            Need(kind, Mathf.CeilToInt(missing / Mathf.Max(.0001f, amount)), need);
                            break;
                    }
                }
            }
            long total = 0;
            foreach (var item in need)
            {
                var module = unlocks != null ? unlocks.FindModule(item.Key) : null;
                if (module != null) total += (long)item.Value * Mathf.Max(0, module.ModuleCreditCost);
            }
            return (int)Math.Min(total, int.MaxValue);
        }
        private static void Need(ModuleKind kind, int count, Dictionary<ModuleKind, int> need)
            => need[kind] = Mathf.Max(count, need.TryGetValue(kind, out int current) ? current : 0);
    }
}
