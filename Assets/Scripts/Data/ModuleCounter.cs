using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 개수를 세는 모듈 종류. 스탯 모듈(ModuleData.ModuleType)과 연장·2방향 분할·4방향 분할. 기본 차단 블럭은 세지 않는다.
    public enum ModuleKind
    {
        Extension, SplitTwo, SplitFour,
        Penetration, FireRate, Explosion, Ricochet, Charge, Gravity, Heat, Spin, Water, Homing, Shotgun, Scope, Speed, Color
    }

    // 조립된 모듈 개수 계산.
    //   TotalCount: 코어에 연결된 해당 종류 전체 개수(위치 무관, 막힌 경로에 있어도 셈).
    //   MaxPathCount: 실제로 발사되는 경로 하나에 있는 해당 종류 수의 최댓값(사이에 다른 블록이 있어도 누적).
    //                 예) 코어→산탄→일반→산탄→관통→발사 = 산탄 2, 관통 1. 코어 위 산탄1·앞 산탄1 = 산탄 1.
    //   LongestChain: 발사되는 경로 하나에 있는 블록 수(스탯 모듈·연장·분할 전부)의 최댓값.
    //   MaxStatModulesOnPath: 발사되는 경로 하나에 있는 스탯 모듈 수의 최댓값.
    // 차단 블럭으로 막혀 발사되지 않는 경로는 경로 기준 값에서 빠진다.
    public static class ModuleCounter
    {
        public static bool TryGetKind(PlacedBlock block, out ModuleKind kind)
        {
            kind = default;
            if (block == null) return false;
            // Stat modules are extension variants, so check their SO first.
            if (TryGetModuleType(block, out StatType stat)) return TryToKind(stat, out kind);
            switch (block.Kind)
            {
                case BlockKind.Extension: kind = ModuleKind.Extension; return true;
                case BlockKind.SplitTwo: kind = ModuleKind.SplitTwo; return true;
                case BlockKind.SplitFour: kind = ModuleKind.SplitFour; return true;
                default: return false;
            }
        }

        /// <summary>Stat module type from ModuleData.ModuleType (e.g. heat stays heat even with its fire-rate bonus).</summary>
        public static bool TryGetModuleType(PlacedBlock block, out StatType type)
        {
            type = default;
            if (block == null) return false;
            ModuleStats module = block.GetComponent<ModuleStats>();
            if (module == null || module.Data == null || module.Data.ModuleType == StatType.Weight) return false;
            type = module.Data.ModuleType;
            return true;
        }

        public static bool TryToKind(StatType stat, out ModuleKind kind)
        {
            kind = default;
            if (stat == StatType.Weight) return false;
            return System.Enum.TryParse(stat.ToString(), out kind);
        }

        public static bool IsStatModule(ModuleKind kind)
            => kind != ModuleKind.Extension && kind != ModuleKind.SplitTwo && kind != ModuleKind.SplitFour;

        public static int TotalCount(CubeAssembly assembly, ModuleKind kind)
            => TotalCounts(assembly).TryGetValue(kind, out int count) ? count : 0;

        public static int MaxPathCount(CubeAssembly assembly, ModuleKind kind)
            => MaxPathCounts(assembly).TryGetValue(kind, out int count) ? count : 0;

        public static int TotalCount(CubeAssembly assembly, StatType type)
            => TryToKind(type, out ModuleKind kind) ? TotalCount(assembly, kind) : 0;

        public static int MaxPathCount(CubeAssembly assembly, StatType type)
            => TryToKind(type, out ModuleKind kind) ? MaxPathCount(assembly, kind) : 0;

        public static int TotalCount(EquipableWeapon weapon, ModuleKind kind) => TotalCount(AssemblyOf(weapon), kind);
        public static int MaxPathCount(EquipableWeapon weapon, ModuleKind kind) => MaxPathCount(AssemblyOf(weapon), kind);
        public static int TotalCount(EquipableWeapon weapon, StatType type) => TotalCount(AssemblyOf(weapon), type);
        public static int MaxPathCount(EquipableWeapon weapon, StatType type) => MaxPathCount(AssemblyOf(weapon), type);

        /// <summary>Every kind's total count (kinds with zero modules are omitted).</summary>
        public static Dictionary<ModuleKind, int> TotalCounts(CubeAssembly assembly)
        {
            var counts = new Dictionary<ModuleKind, int>();
            if (assembly == null) return counts;
            foreach (PlacedBlock block in assembly.GetComponentsInChildren<PlacedBlock>())
            {
                if (block.transform.parent != assembly.transform || !TryGetKind(block, out ModuleKind kind)) continue;
                counts[kind] = counts.TryGetValue(kind, out int count) ? count + 1 : 1;
            }
            return counts;
        }

        /// <summary>Every kind's highest count on a single firing path (zero kinds omitted).</summary>
        public static Dictionary<ModuleKind, int> MaxPathCounts(CubeAssembly assembly)
        {
            var best = new Dictionary<ModuleKind, int>();
            if (assembly == null) return best;
            var pathCounts = new Dictionary<ModuleKind, int>();
            foreach (var muzzle in assembly.GetProjectilePaths())
            {
                pathCounts.Clear();
                foreach (PlacedBlock block in muzzle.path)
                    if (TryGetKind(block, out ModuleKind kind))
                        pathCounts[kind] = pathCounts.TryGetValue(kind, out int count) ? count + 1 : 1;
                foreach (var entry in pathCounts)
                    if (!best.TryGetValue(entry.Key, out int current) || entry.Value > current) best[entry.Key] = entry.Value;
            }
            return best;
        }

        /// <summary>Most blocks (stat modules, extensions, splits) on a single firing path.</summary>
        public static int LongestChain(CubeAssembly assembly) => MaxOnPath(assembly, false);

        /// <summary>Most stat modules on a single firing path.</summary>
        public static int MaxStatModulesOnPath(CubeAssembly assembly) => MaxOnPath(assembly, true);

        public static int LongestChain(EquipableWeapon weapon) => LongestChain(AssemblyOf(weapon));
        public static int MaxStatModulesOnPath(EquipableWeapon weapon) => MaxStatModulesOnPath(AssemblyOf(weapon));

        private static int MaxOnPath(CubeAssembly assembly, bool statOnly)
        {
            if (assembly == null) return 0;
            int best = 0;
            foreach (var muzzle in assembly.GetProjectilePaths())
            {
                int count = 0;
                foreach (PlacedBlock block in muzzle.path)
                    if (TryGetKind(block, out ModuleKind kind) && (!statOnly || IsStatModule(kind))) count++;
                best = Mathf.Max(best, count);
            }
            return best;
        }

        private static CubeAssembly AssemblyOf(EquipableWeapon weapon)
            => weapon != null ? weapon.GetComponentInChildren<CubeAssembly>(true) : null;
    }
}
