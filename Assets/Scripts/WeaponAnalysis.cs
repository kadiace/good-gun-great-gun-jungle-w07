using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace WeaponWorkshop
{
    // CubeAssembly가 소유하는 런타임 캐시. 발사 시각·충전·확산은 저장하지 않는다.
    internal sealed class WeaponAnalysis
    {
        private static readonly ProfilerMarker structureMarker = new ProfilerMarker("WeaponAnalysis.RebuildStructure");
        private static readonly ProfilerMarker snapshotMarker = new ProfilerMarker("WeaponAnalysis.RebuildSnapshot");
        private static int configurationVersion;
        internal static void NotifyConfigurationChanged() => System.Threading.Interlocked.Increment(ref configurationVersion);

        private readonly CubeAssembly assembly;
        private readonly List<PlacedBlock> blocks = new List<PlacedBlock>();
        private readonly List<PlacedBlock> scan = new List<PlacedBlock>();
        private readonly Dictionary<ModuleKind, int> counts = new Dictionary<ModuleKind, int>();
        private readonly Dictionary<PlacedBlock[], WeaponStats> muzzleStats = new Dictionary<PlacedBlock[], WeaponStats>();
        private readonly HashSet<PlacedBlock[]> ownedPaths = new HashSet<PlacedBlock[]>();
        private List<(Vector3 origin, Vector3Int direction, PlacedBlock[] path)> paths;
        private CustomerRequestInfo.WeaponSnapshot snapshot;
        private PowerCore core;
        private StatCollector collector;
        private ProjectileStatsSource source;
        private WeaponStats baseStats, wholeStats;
        private float fireRateMultiplier, emissions;
        private bool inputsReady, structureDirty = true, wholeDirty = true;
        private int observedConfiguration = -1;
        private uint revision;
        private uint structureRevision;

        internal WeaponAnalysis(CubeAssembly assembly) => this.assembly = assembly;
        internal uint Revision { get { EnsureInputs(); return revision; } }
        internal uint StructureRevision { get { EnsureStructure(); return structureRevision; } }
        internal IReadOnlyList<PlacedBlock> Blocks { get { EnsureStructure(); return blocks; } }
        internal List<(Vector3 origin, Vector3Int direction, PlacedBlock[] path)> Paths
        {
            get
            {
                EnsureStructure();
                if (paths == null)
                {
                    paths = assembly.BuildProjectilePaths(blocks);
                    foreach (var output in paths) ownedPaths.Add(output.path);
                }
                return paths;
            }
        }

        internal void InvalidateStructure()
        {
            structureRevision++;
            structureDirty = true;
            paths = null;
            ownedPaths.Clear();
            core?.InvalidateMuzzles();
            InvalidateStats();
        }

        internal void InvalidateStats()
        {
            wholeDirty = true;
            muzzleStats.Clear();
            InvalidateAppearance();
        }

        internal void InvalidateAppearance()
        {
            snapshot = null;
            revision++;
        }

        internal void InvalidateJudgement() => revision++;

        private void EnsureInputs()
        {
            bool componentsChanged = false;
            if (core == null && assembly.TryGetComponent(out core)) componentsChanged = true;
            if (collector == null && assembly.TryGetComponent(out collector)) componentsChanged = true;
            if (source == null && assembly.TryGetComponent(out source)) componentsChanged = true;
            WeaponStats next = collector != null && collector.BaseStats != null ? collector.BaseStats.Stats
                : source != null ? source.CurrentStats : new WeaponStats { fireRate = core != null ? core.EmissionsPerSecond : 1f };
            float nextEmissions = core != null ? core.EmissionsPerSecond : 1f;
            float nextMultiplier = source != null ? source.Tuning.fireRateMultiplierPerModule : 1f;
            if (inputsReady && !componentsChanged && SameStats(next, baseStats) && emissions == nextEmissions &&
                fireRateMultiplier == nextMultiplier && observedConfiguration == configurationVersion) return;
            bool configurationChanged = observedConfiguration != configurationVersion;
            inputsReady = true;
            baseStats = next;
            emissions = nextEmissions;
            fireRateMultiplier = nextMultiplier;
            observedConfiguration = configurationVersion;
            if (configurationChanged) InvalidateStructure();
            else InvalidateStats();
            if (source != null && collector != null && collector.BaseStats != null) source.SetWeaponStats(next);
        }

        internal static bool SameStats(WeaponStats a, WeaponStats b)
        {
            foreach (StatType type in WeaponStats.AllTypes)
                if (!a.Get(type).Equals(b.Get(type))) return false;
            return true;
        }

        private void EnsureStructure()
        {
            EnsureInputs();
            if (!structureDirty) return;
            using (structureMarker.Auto())
            {
                scan.Clear();
                assembly.GetComponentsInChildren(false, scan);
                blocks.Clear();
                counts.Clear();
                foreach (PlacedBlock block in scan)
                {
                    if (block == null || block.transform.parent != assembly.transform) continue;
                    blocks.Add(block);
                    if (ModuleCounter.TryGetKind(block, out var kind))
                        counts[kind] = counts.TryGetValue(kind, out int count) ? count + 1 : 1;
                }
                structureDirty = false;
            }
        }

        internal int TotalCount(ModuleKind kind)
        {
            EnsureStructure();
            return counts.TryGetValue(kind, out int count) ? count : 0;
        }

        internal Dictionary<ModuleKind, int> CopyTotalCounts()
        {
            EnsureStructure();
            return new Dictionary<ModuleKind, int>(counts);
        }

        internal WeaponStats WholeStats
        {
            get
            {
                EnsureStructure();
                if (wholeDirty)
                {
                    wholeStats = WeaponAssemblyStats.Calculate(assembly, baseStats);
                    wholeDirty = false;
                }
                return wholeStats;
            }
        }

        internal WeaponStats GetWholeStats(WeaponStats stats)
        {
            EnsureInputs();
            return SameStats(stats, baseStats) ? WholeStats : WeaponAssemblyStats.Calculate(assembly, stats);
        }

        internal WeaponStats GetMuzzleStats(PlacedBlock[] path, ProjectileStatsSource statsSource)
        {
            EnsureStructure();
            _ = Paths;
            if (path == null || !ownedPaths.Contains(path)) return statsSource.CalculateMuzzleStats(path);
            if (!muzzleStats.TryGetValue(path, out WeaponStats result))
            {
                result = statsSource.CalculateMuzzleStats(path);
                muzzleStats.Add(path, result);
            }
            return result;
        }

        internal CustomerRequestInfo.WeaponSnapshot Snapshot
        {
            get
            {
                EnsureInputs();
                if (snapshot == null)
                    using (snapshotMarker.Auto()) snapshot = new CustomerRequestInfo.WeaponSnapshot(core);
                return snapshot;
            }
        }
    }
}
