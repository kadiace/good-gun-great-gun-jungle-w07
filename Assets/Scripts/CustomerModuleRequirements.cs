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
                {
                    var tests = TableManager.Instance.ShootingTestTable;
                    if (!tests.IsAvailable(testId)) return false;
                    foreach (var requirement in tests.Find(testId).requiredModules ?? Array.Empty<ShootingTestTableData.ModuleRequirement>())
                        if (!ShootingTestInfo.TryGetModuleKind(requirement, out var kind) ||
                            unlocks == null || !unlocks.IsUnlocked(kind) || unlocks.FindModule(kind) == null) return false;
                }
                foreach (var condition in table.Conditions(row))
                {
                    if (condition.min <= 0f) continue;
                    switch (condition.Measure)
                    {
                        case GunMeasure.ConstructionCost:
                        case GunMeasure.Shape:
                        case GunMeasure.PaintedFaces:
                        case GunMeasure.PaintedColorFaces:
                        case GunMeasure.StickerFaces:
                        case GunMeasure.Keyrings:
                        case GunMeasure.BasicCore:
                            break; // 제작비·영역·도색은 특정 스탯 모듈 해금을 요구하지 않는다.
                        case GunMeasure.ColorModules:
                            if (unlocks == null || !unlocks.IsUnlocked(ModuleKind.Color)) return false;
                            break;
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
        // 필요 모듈 구매액을 추정한다. 코어·연결용 연장 비용이나 기존 무기 재사용은 포함하지 않는다.
        public static int DollarCost(CustomerProfile profile, PlayerModuleUnlocks unlocks, WeaponStats core)
            => profile == null ? 0 : DollarCost(profile.RequestIds, unlocks, core);
        public static int DollarCost(string[] ids, PlayerModuleUnlocks unlocks, WeaponStats core)
        {
            var need = new Dictionary<ModuleKind, int>();
            int constructionMinimum = 0;
            var table = TableManager.Instance.CustomerRequest;
            foreach (string id in ids ?? Array.Empty<string>())
            {
                var row = table.FindRequest(id);
                if (row == null) continue;
                foreach (string testId in row.requiredTestIds ?? Array.Empty<string>())
                {
                    var test = TableManager.Instance.ShootingTestTable.Find(testId);
                    if (test == null) continue;
                    foreach (var requirement in test.requiredModules ?? Array.Empty<ShootingTestTableData.ModuleRequirement>())
                        if (ShootingTestInfo.TryGetModuleKind(requirement, out var kind)) Need(kind, requirement.count, need);
                }
                foreach (var condition in table.Conditions(row))
                {
                    if (condition.min <= 0f) continue;
                    switch (condition.Measure)
                    {
                        case GunMeasure.SplitTwoBlocks: Need(ModuleKind.SplitTwo, Mathf.CeilToInt(condition.min), need); break;
                        case GunMeasure.SplitFourBlocks: Need(ModuleKind.SplitFour, Mathf.CeilToInt(condition.min), need); break;
                        case GunMeasure.ConstructionCost:
                            constructionMinimum = Mathf.Max(constructionMinimum, Mathf.FloorToInt(condition.min) +
                                (condition.minExclusive ? 1 : condition.min % 1f > 0 ? 1 : 0));
                            break;
                        case GunMeasure.TotalModules: case GunMeasure.Muzzles:
                        case GunMeasure.Shape:
                         case GunMeasure.PaintedFaces: case GunMeasure.PaintedColorFaces:
                         case GunMeasure.StickerFaces: case GunMeasure.Keyrings: case GunMeasure.BasicCore: break;
                        case GunMeasure.ColorModules: Need(ModuleKind.Color, Mathf.CeilToInt(condition.min), need); break;
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
                if (module != null)
                {
                    long count = item.Value;
                    total += (long)Mathf.Max(0, module.BuyPrice) * count;
                    if (total >= int.MaxValue) return int.MaxValue;
                }
            }
            return Mathf.Max((int)total, constructionMinimum);
        }
        private static void Need(ModuleKind kind, int count, Dictionary<ModuleKind, int> need)
            => need[kind] = Mathf.Max(count, need.TryGetValue(kind, out int current) ? current : 0);
    }
}
