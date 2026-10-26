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
                    if (!tests.CanStart(testId, unlocks)) return false;
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

        public static bool IsFeasible(string[] ids, PlayerModuleUnlocks unlocks, WeaponStats core, out int dollars)
        {
            dollars = 0;
            if (unlocks == null || !Allows(ids, unlocks, core)) return false;
            var conditions = new List<CustomerRequestData.Condition>();
            var table = TableManager.Instance.CustomerRequest;
            var counts = new Dictionary<ModuleKind, int>();
            foreach (string id in ids)
            {
                var request = table.FindRequest(id);
                conditions.AddRange(table.Conditions(request));
                foreach (string testId in request.requiredTestIds ?? Array.Empty<string>())
                {
                    var test = TableManager.Instance.ShootingTestTable.Find(testId);
                    foreach (var requirement in test.requiredModules ?? Array.Empty<ShootingTestTableData.ModuleRequirement>())
                        if (ShootingTestInfo.TryGetModuleKind(requirement, out var kind)) Need(kind, requirement.count, counts);
                }
            }
            foreach (var condition in conditions)
            {
                int minimum = MinimumCount(condition);
                switch (condition.Measure)
                {
                    case GunMeasure.Shape:
                    case GunMeasure.MuzzleStat:
                        return false;
                    case GunMeasure.PathModules:
                        if (!ModuleCounter.TryToKind(condition.Stat, out var pathKind)) return false;
                        Need(pathKind, minimum, counts);
                        break;
                    case GunMeasure.ColorModules: Need(ModuleKind.Color, minimum, counts); break;
                    case GunMeasure.SplitTwoBlocks: Need(ModuleKind.SplitTwo, minimum, counts); break;
                    case GunMeasure.SplitFourBlocks: Need(ModuleKind.SplitFour, minimum, counts); break;
                    case GunMeasure.WeaponStat:
                        if (condition.Stat == StatType.Weight || MeetsRange(core.Get(condition.Stat), condition)) break;
                        if (!ModuleCounter.TryToKind(condition.Stat, out var statKind)) return false;
                        var module = unlocks.FindModule(statKind);
                        float bonus = module != null ? module.GetStats().Get(condition.Stat) : 0f;
                        if (bonus <= 0f) return false;
                        float missing = (condition.min - core.Get(condition.Stat)) / bonus;
                        Need(statKind, Mathf.Max(0, condition.minExclusive ? Mathf.FloorToInt(missing) + 1 : Mathf.CeilToInt(missing)), counts);
                        break;
                }
            }
            int requiredTotal = 0, requiredMuzzles = 1;
            foreach (var condition in conditions)
            {
                if (condition.Measure == GunMeasure.TotalModules) requiredTotal = Mathf.Max(requiredTotal, MinimumCount(condition));
                if (condition.Measure == GunMeasure.Muzzles) requiredMuzzles = Mathf.Max(requiredMuzzles, MinimumCount(condition));
            }
            int statCount = 0;
            foreach (var count in counts)
                if (ModuleCounter.IsStatModule(count.Key) && count.Key != ModuleKind.Scope) statCount += count.Value;
            if (statCount < requiredTotal)
            {
                if (!unlocks.IsUnlocked(ModuleKind.FireRate)) return false;
                Need(ModuleKind.FireRate, Count(ModuleKind.FireRate) + requiredTotal - statCount, counts);
            }
            int muzzleCount = 6 + Count(ModuleKind.SplitTwo) + 3 * Count(ModuleKind.SplitFour);
            if (muzzleCount < requiredMuzzles)
            {
                ModuleKind split = unlocks.IsUnlocked(ModuleKind.SplitFour) ? ModuleKind.SplitFour : ModuleKind.SplitTwo;
                int gain = split == ModuleKind.SplitFour ? 3 : 1;
                Need(split, Count(split) + Mathf.CeilToInt((requiredMuzzles - muzzleCount) / (float)gain), counts);
                muzzleCount = 6 + Count(ModuleKind.SplitTwo) + 3 * Count(ModuleKind.SplitFour);
            }
            var stats = core;
            statCount = 0;
            long cost = WeaponPurchasePrices.Core;
            int blocks = 0;
            foreach (var count in counts)
            {
                if (count.Value == 0) continue;
                var module = unlocks.FindModule(count.Key);
                if (module == null || !unlocks.IsUnlocked(count.Key)) return false;
                var bonus = module.GetStats();
                if (!ModuleCounter.IsStatModule(count.Key)) bonus.weight = 1f;
                foreach (var stat in WeaponStats.AllTypes) stats.Add(stat, bonus.Get(stat) * count.Value);
                blocks += count.Value;
                cost += (long)module.BuyPrice * count.Value;
                if (ModuleCounter.IsStatModule(count.Key) && count.Key != ModuleKind.Scope) statCount += count.Value;
            }
            float requiredWeight = stats.weight;
            int paintFaces = 0, stickerFaces = 0, keyrings = 0;
            var colors = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var condition in conditions)
            {
                switch (condition.Measure)
                {
                    case GunMeasure.WeaponStat:
                        if (condition.Stat == StatType.Weight)
                            requiredWeight = Mathf.Max(requiredWeight, MinimumCount(condition));
                        break;
                    case GunMeasure.PaintedFaces: paintFaces = Mathf.Max(paintFaces, MinimumCount(condition)); break;
                    case GunMeasure.PaintedColorFaces:
                        if (!ColorUtility.TryParseHtmlString(condition.colorHex, out _)) return false;
                        colors[condition.colorHex] = Mathf.Max(MinimumCount(condition),
                            colors.TryGetValue(condition.colorHex, out int current) ? current : 0);
                        break;
                    case GunMeasure.StickerFaces: stickerFaces = Mathf.Max(stickerFaces, MinimumCount(condition)); break;
                    case GunMeasure.Keyrings: keyrings = Mathf.Max(keyrings, MinimumCount(condition)); break;
                    case GunMeasure.Muzzles:
                        if (!MeetsRange(muzzleCount, condition)) return false;
                        break;
                }
            }
            int coloredFaces = 0;
            foreach (int faces in colors.Values) coloredFaces += faces;
            paintFaces = Mathf.Max(paintFaces, coloredFaces);
            if (stickerFaces > 10 || keyrings > 1) return false;
            int filler = Mathf.Max(Mathf.CeilToInt(requiredWeight - stats.weight),
                Mathf.Max(0, Mathf.CeilToInt(Mathf.Max(paintFaces, stickerFaces) / 4f) - blocks));
            if (keyrings > 0 && blocks == 0) filler = Mathf.Max(1, filler);
            var extension = unlocks.FindModule(ModuleKind.Extension);
            if (filler > 0 && (extension == null || !unlocks.IsUnlocked(ModuleKind.Extension))) return false;
            stats.weight += filler;
            cost += (long)filler * (extension != null ? extension.BuyPrice : 0);
            foreach (var condition in conditions)
                if (condition.Measure == GunMeasure.ConstructionCost && !MeetsRange(cost, condition) && cost <= condition.min)
                {
                    if (extension == null || !unlocks.IsUnlocked(ModuleKind.Extension) || extension.BuyPrice <= 0) return false;
                    int extra = Mathf.Max(0, Mathf.CeilToInt((MinimumCount(condition) - cost) / (float)extension.BuyPrice));
                    cost += (long)extra * extension.BuyPrice;
                    stats.weight += extra;
                }
            foreach (var condition in conditions)
            {
                float value;
                switch (condition.Measure)
                {
                    case GunMeasure.WeaponStat: value = stats.Get(condition.Stat); break;
                    case GunMeasure.PathModules:
                        value = ModuleCounter.TryToKind(condition.Stat, out var kind) ? Count(kind) : 0;
                        break;
                    case GunMeasure.TotalModules: value = statCount; break;
                    case GunMeasure.SplitTwoBlocks: value = Count(ModuleKind.SplitTwo); break;
                    case GunMeasure.SplitFourBlocks: value = Count(ModuleKind.SplitFour); break;
                    case GunMeasure.ColorModules: value = Count(ModuleKind.Color); break;
                    case GunMeasure.Muzzles: value = muzzleCount; break;
                    case GunMeasure.ConstructionCost: value = cost; break;
                    case GunMeasure.PaintedFaces: value = paintFaces; break;
                    case GunMeasure.PaintedColorFaces: value = colors[condition.colorHex]; break;
                    case GunMeasure.StickerFaces: value = stickerFaces; break;
                    case GunMeasure.Keyrings: value = keyrings; break;
                    case GunMeasure.BasicCore: value = 1; break;
                    default: return false;
                }
                if (!MeetsRange(value, condition)) return false;
            }
            if (cost > int.MaxValue) return false;
            dollars = (int)cost;
            return true;

            int Count(ModuleKind kind) => counts.TryGetValue(kind, out int count) ? count : 0;
        }

        private static int MinimumCount(CustomerRequestData.Condition condition)
            => Mathf.Max(0, condition.minExclusive ? Mathf.FloorToInt(condition.min) + 1 : Mathf.CeilToInt(condition.min));

        private static bool MeetsRange(float value, CustomerRequestData.Condition condition)
            => (condition.minExclusive ? value > condition.min : value >= condition.min) &&
                (condition.maxExclusive ? value < condition.max : value <= condition.max);
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
