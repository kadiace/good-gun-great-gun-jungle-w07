using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // Powered modules contribute effects; scopes count without energy, and every block adds weight.
    public static class WeaponAssemblyStats
    {
        public static HashSet<PlacedBlock> PoweredBlocks(CubeAssembly assembly)
        {
            var powered = new HashSet<PlacedBlock>();
            if (assembly == null) return powered;
            var cells = new Dictionary<Vector3Int, PlacedBlock>();
            foreach (var block in assembly.Analysis.Blocks) cells[block.Cell] = block;
            var pending = new Queue<(Vector3Int cell, Vector3Int direction)>();
            foreach (var output in CubeAssembly.GetCoreOutputs())
                pending.Enqueue((output.direction, output.direction));
            while (pending.Count > 0)
            {
                var next = pending.Dequeue();
                if (next.cell == Vector3Int.zero || !cells.TryGetValue(next.cell, out var block)
                    || !block.AcceptsEnergy(next.direction) || !powered.Add(block)) continue;
                foreach (var direction in block.GetOutputDirections())
                    pending.Enqueue((next.cell + direction, direction));
            }
            return powered;
        }

        public static Dictionary<ModuleKind, int> PoweredCounts(CubeAssembly assembly)
        {
            var counts = new Dictionary<ModuleKind, int>();
            foreach (var block in PoweredBlocks(assembly))
                if (ModuleCounter.TryGetKind(block, out var kind))
                    counts[kind] = counts.TryGetValue(kind, out int count) ? count + 1 : 1;
            return counts;
        }

        public static WeaponStats Calculate(CubeAssembly assembly, WeaponStats coreStats)
        {
            if (assembly == null) return coreStats.Clamped();
            var powered = PoweredBlocks(assembly);
            foreach (var block in assembly.Analysis.Blocks)
            {
                block.TryGetComponent<ModuleStats>(out var module);
                bool hasData = module != null && module.Data != null;
                var contribution = hasData ? module.Data.GetStats() : block.GetStatContribution();
                // Basic/extension/split blocks without a stats SO weigh one each.
                float weight = hasData ? contribution.weight : 1f;
                if (powered.Contains(block))
                {
                    contribution.weight = 0f;
                    coreStats.Add(contribution);
                }
                else if (block.IsSight)
                {
                    // A mounted sight is usable even when its supporting blocks carry no energy.
                    coreStats.scope += contribution.scope;
                }
                coreStats.weight += weight;
            }
            return coreStats.Clamped();
        }
    }
}
