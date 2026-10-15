using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    public static class CustomerRequestInfo
    {
        public sealed class WeaponSnapshot
        {
            private readonly Dictionary<StatType, float> muzzleStats = new Dictionary<StatType, float>();
            private readonly Dictionary<ModuleKind, int> pathModules = new Dictionary<ModuleKind, int>();
            private readonly WeaponStats whole;
            private readonly WeaponStats firingStats;
            private readonly int muzzles, totalOnPath;
            private readonly int splitTwoCount, splitFourCount;
            public bool HasWeapon { get; }
            private readonly EquipableWeapon weapon;
            public bool HasPassedTest(string id) => weapon != null && weapon.HasPassedTest(id);
            public WeaponSnapshot(EquipableWeapon weapon) : this(weapon != null ? weapon.GetComponentInChildren<PowerCore>(true) : null) { }
            public WeaponSnapshot(PowerCore core)
            {
                if (core == null) return;
                weapon = core.GetComponentInParent<EquipableWeapon>();
                HasWeapon = true;
                var assembly = core.GetComponent<CubeAssembly>();
                var collector = core.GetComponent<StatCollector>();
                var baseStats = collector != null && collector.BaseStats != null
                    ? collector.BaseStats.Stats : new WeaponStats { fireRate = core.EmissionsPerSecond };
                whole = WeaponAssemblyStats.Calculate(assembly, baseStats);
                var effective = baseStats;
                var seen = new HashSet<PlacedBlock>();
                if (assembly == null) return;
                var paths = assembly.GetProjectilePaths(); muzzles = paths.Count;
                foreach (var path in paths)
                {
                    var stats = core.GetMuzzleStats(path.path);
                    foreach (StatType stat in WeaponStats.AllTypes)
                        if (!muzzleStats.TryGetValue(stat, out var old) || stats.Get(stat) > old) muzzleStats[stat] = stats.Get(stat);
                    var counts = new Dictionary<ModuleKind, int>(); int total = 0;
                    foreach (var block in path.path)
                        if (ModuleCounter.TryGetKind(block, out var kind))
                        {
                            counts[kind] = counts.TryGetValue(kind, out int count) ? count + 1 : 1;
                            if (ModuleCounter.IsStatModule(kind)) total++;
                            if (seen.Add(block))
                            {
                                // Count each firing split once, including splits on different branches.
                                if (kind == ModuleKind.SplitTwo) splitTwoCount++;
                                else if (kind == ModuleKind.SplitFour) splitFourCount++;
                                var module = block.GetComponent<ModuleStats>();
                                var bonus = module != null && module.Data != null ? module.Data.GetStats() : block.GetStatContribution();
                                bonus.weight = 0f; effective.Add(bonus);
                            }
                        }
                    totalOnPath = Mathf.Max(totalOnPath, total);
                    foreach (var count in counts)
                        if (!pathModules.TryGetValue(count.Key, out int old) || count.Value > old) pathModules[count.Key] = count.Value;
                }
                effective.weight = whole.weight; effective.scope = whole.scope;
                firingStats = effective.Clamped();
            }
            public float Value(CustomerRequestData.Condition condition)
            {
                if (!HasWeapon || condition == null) return 0f;
                switch (condition.measure)
                {
                    case GunMeasure.WeaponStat:
                        if (condition.stat == StatType.Weight || condition.stat == StatType.Scope) return whole.Get(condition.stat);
                        return firingStats.Get(condition.stat);
                    case GunMeasure.PathModules:
                        if (condition.stat == StatType.Scope) return whole.scope; // Explicitly agreed sight exception.
                        return ModuleCounter.TryToKind(condition.stat, out var kind) && pathModules.TryGetValue(kind, out var count) ? count : 0;
                    case GunMeasure.SplitTwoBlocks: return splitTwoCount;
                    case GunMeasure.SplitFourBlocks: return splitFourCount;
                    case GunMeasure.TotalModules: return totalOnPath;
                    case GunMeasure.Muzzles: return muzzles;
                    default: return muzzleStats.TryGetValue(condition.stat, out var value) ? value : 0f;
                }
            }
            public bool Meets(CustomerRequestData.Condition condition)
            {
                if (!HasWeapon || !ValidCondition(condition)) return false;
                float value = Value(condition);
                return value >= condition.min && (condition.maxExclusive ? value < condition.max : value <= condition.max);
            }
        }

        public static CustomerRequestData.Request FindRequest(this CustomerRequestData table, string id)
        {
            if (!string.IsNullOrEmpty(id) && table?.requests != null)
            {
                foreach (var row in table.requests) if (row != null && row.id == id) return row;
                foreach (var row in table.requests)
                    if (row != null && Array.IndexOf(row.legacyIds ?? Array.Empty<string>(), id) >= 0) return row;
            }
            return null;
        }
        public static string[] CanonicalRequestIds(this CustomerRequestData table, string[] ids)
        {
            if (ids == null) return null;
            return Array.ConvertAll(ids, id => table.FindRequest(id)?.id ?? id);
        }
        public static CustomerRequestData.Tag FindTag(this CustomerRequestData table, string id)
        {
            if (!string.IsNullOrEmpty(id) && table?.tags != null)
                foreach (var row in table.tags) if (row != null && row.id == id) return row;
            return null;
        }
        public static bool MeetsTag(this CustomerRequestData table, string id, WeaponSnapshot weapon)
        {
            var tag = table.FindTag(id);
            return tag != null && tag.conditions != null && tag.conditions.Length > 0 && MeetsAll(tag.conditions, weapon);
        }
        public static bool MeetsRequest(this CustomerRequestData table, string id, WeaponSnapshot weapon)
        {
            var request = table.FindRequest(id);
            return request != null && weapon.HasWeapon && (string.IsNullOrEmpty(request.tagId) || table.MeetsTag(request.tagId, weapon))
                && MeetsAll(request.conditions, weapon) && MeetsTests(request.requiredTestIds, weapon);
        }
        private static bool MeetsTests(string[] ids, WeaponSnapshot weapon)
        {
            foreach (string id in ids ?? Array.Empty<string>()) if (!weapon.HasPassedTest(id)) return false;
            return true;
        }
        private static bool MeetsAll(CustomerRequestData.Condition[] conditions, WeaponSnapshot weapon)
        {
            if (conditions == null) return false;
            foreach (var condition in conditions) if (!weapon.Meets(condition)) return false;
            return true;
        }
        public static bool MeetsOrder(this CustomerRequestData table, string[] ids, EquipableWeapon weapon)
        {
            if (weapon == null || ids == null) return false;
            var snapshot = new WeaponSnapshot(weapon);
            foreach (string id in ids) if (!table.MeetsRequest(id, snapshot)) return false;
            return true;
        }
        public static bool ContainsOrder(this CustomerRequestData table, string[] ids)
        {
            if (ids == null) return false;
            foreach (string id in ids)
            {
                var row = table.FindRequest(id);
                if (row == null || row.conditions == null || (!string.IsNullOrEmpty(row.tagId) && table.FindTag(row.tagId) == null)) return false;
                foreach (var condition in table.Conditions(row)) if (!ValidCondition(condition)) return false;
                foreach (string testId in row.requiredTestIds ?? Array.Empty<string>())
                    if (TableManager.Instance.ShootingTestTable.Find(testId) == null) return false;
            }
            return true;
        }
        private static bool ValidCondition(CustomerRequestData.Condition condition) => condition != null &&
            Enum.IsDefined(typeof(GunMeasure), condition.measure) && Enum.IsDefined(typeof(StatType), condition.stat) &&
            condition.min <= condition.max && !float.IsNaN(condition.min) && !float.IsNaN(condition.max) &&
            !float.IsInfinity(condition.min) && !float.IsInfinity(condition.max);
        public static int SaleBonusCount(this CustomerRequestData table, string[] ids)
        {
            int count = 0;
            foreach (string id in ids ?? Array.Empty<string>()) if (table.FindRequest(id)?.saleBonus == true) count++;
            return count;
        }
        public static string Phrase(this CustomerRequestData table, string id)
        {
            var row = table.FindRequest(id);
            if (row == null) return "등록되지 않은 요청";
            var tag = table.FindTag(row.tagId);
            return tag != null ? tag.name + " 태그" : string.IsNullOrEmpty(row.phrase) ? row.name : row.phrase;
        }
        public static bool Conflicts(this CustomerRequestData table, string first, string second)
        {
            var a = table.FindRequest(first); var b = table.FindRequest(second);
            return a == null || b == null || Excludes(table, a, b.id) || Excludes(table, b, a.id);
        }
        private static bool Excludes(CustomerRequestData table, CustomerRequestData.Request request, string id)
        {
            foreach (string excluded in request.excludes ?? Array.Empty<string>())
                if (table.FindRequest(excluded)?.id == id) return true;
            return false;
        }
        public static string[] RestoreLegacyOrder(this CustomerRequestData table, string poolId, int[] indices)
        {
            if (indices == null) return null;
            var ids = new List<string>();
            foreach (int index in indices)
            {
                string id = null;
                foreach (var row in table.legacySlots ?? Array.Empty<CustomerRequestData.LegacySlot>())
                    if (row.poolId == poolId && row.index == index) { id = row.requestId; break; }
                var request = table.FindRequest(id);
                if (request == null) return null;
                ids.Add(request.id);
            }
            return ids.ToArray();
        }
        public static string Describe(CustomerRequestData.Condition condition, float currentValue)
        {
            string current = currentValue.ToString("0.##");
            if ((condition.measure == GunMeasure.SplitTwoBlocks || condition.measure == GunMeasure.SplitFourBlocks)
                && condition.max == float.MaxValue)
                return "발사 경로 전체의 " + condition.label + "  " + current + " / " + condition.min.ToString("0.##");
            return Describe(condition) + "  (현재 " + current + ")";
        }
        public static string Describe(CustomerRequestData.Condition condition, bool includePathScope = true)
        {
            string label = condition.label;
            bool splitModule = condition.measure == GunMeasure.SplitTwoBlocks || condition.measure == GunMeasure.SplitFourBlocks;
            if (includePathScope && splitModule) label = "발사 경로 전체의 " + label;
            bool scopedModule = condition.measure == GunMeasure.PathModules || condition.measure == GunMeasure.TotalModules;
            if (includePathScope && scopedModule && condition.stat != StatType.Scope) label = "한 발사 경로의 " + label;
            string lower = condition.min.ToString("0.##"), upper = condition.max.ToString("0.##");
            if (condition.min == condition.max && !condition.maxExclusive) return label + " 정확히 " + lower;
            if (condition.max == float.MaxValue) return label + " " + lower + " 이상";
            if (condition.min <= 0f) return label + " " + upper + (condition.maxExclusive ? " 미만" : " 이하");
            return label + " " + lower + " 이상 · " + upper + (condition.maxExclusive ? " 미만" : " 이하");
        }
        public static IEnumerable<CustomerRequestData.Condition> Conditions(this CustomerRequestData table, CustomerRequestData.Request request)
        {
            var tag = table.FindTag(request?.tagId);
            foreach (var condition in tag?.conditions ?? Array.Empty<CustomerRequestData.Condition>()) yield return condition;
            foreach (var condition in request?.conditions ?? Array.Empty<CustomerRequestData.Condition>()) yield return condition;
        }
        public static void Validate(CustomerRequestData table, ShootingTestTableData tests)
        {
            var ids = new HashSet<string>();
            foreach (var tag in table.tags ?? Array.Empty<CustomerRequestData.Tag>())
                if (tag == null || string.IsNullOrEmpty(tag.id) || !ids.Add(tag.id)) Debug.LogError("요청 표에 빈/중복 태그 ID가 있습니다.");
            ids.Clear();
            foreach (var row in table.requests ?? Array.Empty<CustomerRequestData.Request>())
            {
                if (row == null || string.IsNullOrEmpty(row.id) || !ids.Add(row.id)) { Debug.LogError("요청 표에 빈/중복 요청 ID가 있습니다."); continue; }
                if (!string.IsNullOrEmpty(row.tagId) && table.FindTag(row.tagId) == null) Debug.LogError("요청 표의 태그 ID가 없습니다: " + row.id);
                foreach (string testId in row.requiredTestIds ?? Array.Empty<string>())
                    if (tests.Find(testId) == null) Debug.LogError("요청 표의 사격 테스트 ID가 없습니다: " + row.id);
                foreach (var condition in table.Conditions(row))
                    if (!ValidCondition(condition))
                        Debug.LogError("요청 표의 조건 범위를 확인하세요: " + row.id);
            }
            foreach (var row in table.requests ?? Array.Empty<CustomerRequestData.Request>())
            {
                if (row == null) continue;
                foreach (string alias in row.legacyIds ?? Array.Empty<string>())
                    if (string.IsNullOrEmpty(alias) || !ids.Add(alias)) Debug.LogError("요청 표에 빈/중복 이전 요청 ID가 있습니다: " + row.id);
                foreach (string excluded in row.excludes ?? Array.Empty<string>())
                    if (table.FindRequest(excluded) == null) Debug.LogError("요청 표의 제외 요청 ID가 없습니다: " + row.id);
            }
        }
    }
}
