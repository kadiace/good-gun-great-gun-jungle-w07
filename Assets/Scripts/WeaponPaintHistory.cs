using System.Collections.Generic;

using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class WeaponPaintHistory : MonoBehaviour
    {
        private struct Entry
        {
            public PlacedBlock block;
            public Vector3Int face;
            public Material previousMaterial;
        }

        private sealed class UndoAction
        {
            public readonly List<Entry> entries = new List<Entry>();
        }

        private static readonly Vector3Int[] NeighborOffsets =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up,
            Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };

        private readonly List<UndoAction> undoHistory = new List<UndoAction>();

        public bool Apply(PlacedBlock block, Vector3Int face, Material material)
        {
            // Color modules show their picked bullet color; painting would hide it.
            if (block == null || material == null || block.GetComponent<ColorModule>() != null) return false;
            ModuleFacePaint paint = block.GetComponent<ModuleFacePaint>();
            if (paint == null) paint = block.gameObject.AddComponent<ModuleFacePaint>();
            Material previous = paint.GetMaterial(face);
            if (!paint.SetMaterial(face, material)) return false;

            var action = new UndoAction();
            action.entries.Add(new Entry { block = block, face = face, previousMaterial = previous });
            undoHistory.Add(action);
            return true;
        }

        /// <summary>Applies paint to every exposed face using the existing paint exclusions.</summary>
        /// <param name="assembly">Weapon assembly whose exposed faces receive the material.</param>
        /// <param name="material">Material assigned to each changed face.</param>
        /// <returns>True when at least one face changed.</returns>
        public bool ApplyToAllExposedFaces(CubeAssembly assembly, Material material)
            => ApplyToAllExposedFaces(assembly, material, null);

        /// <summary>Applies paint to exposed faces while optionally excluding one module type.</summary>
        /// <param name="assembly">Weapon assembly whose exposed faces receive the material.</param>
        /// <param name="material">Material assigned to each changed face.</param>
        /// <param name="excludedModuleType">Module type left unchanged, or null to include all normal paint targets.</param>
        /// <returns>True when at least one face changed.</returns>
        public bool ApplyToAllExposedFaces(CubeAssembly assembly, Material material, StatType? excludedModuleType)
        {
            if (assembly == null || material == null) return false;

            var blocksByCell = new Dictionary<Vector3Int, PlacedBlock>();
            foreach (PlacedBlock block in assembly.GetComponentsInChildren<PlacedBlock>())
                if (block != null && block.transform.parent == assembly.transform && block.gameObject.activeSelf)
                    blocksByCell[block.Cell] = block;

            var action = new UndoAction();
            foreach (PlacedBlock block in blocksByCell.Values)
            {
                if (block == null || block.GetComponent<ColorModule>() != null ||
                    block.GetComponent<PowerCore>() != null) continue;
                if (excludedModuleType.HasValue)
                {
                    ModuleStats stats = block.GetComponent<ModuleStats>();
                    if (stats != null && stats.Data != null && stats.Data.ModuleType == excludedModuleType.Value) continue;
                }
                ModuleFacePaint paint = block.GetComponent<ModuleFacePaint>();
                foreach (Vector3Int surfaceNormal in NeighborOffsets)
                {
                    if (blocksByCell.ContainsKey(block.Cell + surfaceNormal) ||
                        !TryGetLocalFace(assembly, block, surfaceNormal, out Vector3Int face)) continue;

                    Material previous = GetMaterial(block, face);
                    if (previous == material) continue;
                    if (paint == null) paint = block.gameObject.AddComponent<ModuleFacePaint>();
                    if (paint.SetMaterial(face, material))
                        action.entries.Add(new Entry { block = block, face = face, previousMaterial = previous });
                }
            }

            if (action.entries.Count == 0) return false;
            undoHistory.Add(action);
            return true;
        }

        public bool FillSurfaceRegion(CubeAssembly assembly, PlacedBlock startBlock, Vector3Int startFace,
            Material material)
        {
            if (assembly == null || startBlock == null || material == null ||
                startBlock.transform.parent != assembly.transform) return false;

            Vector3Int surfaceNormal = Vector3Int.RoundToInt(assembly.transform.InverseTransformDirection(
                startBlock.transform.TransformDirection((Vector3)startFace)));
            if (Mathf.Abs(surfaceNormal.x) + Mathf.Abs(surfaceNormal.y) + Mathf.Abs(surfaceNormal.z) != 1)
                return false;

            var blocksByCell = new Dictionary<Vector3Int, PlacedBlock>();
            foreach (PlacedBlock block in assembly.GetComponentsInChildren<PlacedBlock>())
                if (block != null && block.transform.parent == assembly.transform && block.gameObject.activeSelf)
                    blocksByCell[block.Cell] = block;

            if (!blocksByCell.TryGetValue(startBlock.Cell, out PlacedBlock foundStart) || foundStart != startBlock ||
                blocksByCell.ContainsKey(startBlock.Cell + surfaceNormal)) return false;
            if (!TryGetLocalFace(assembly, startBlock, surfaceNormal, out Vector3Int startLocalFace)) return false;

            Material targetMaterial = GetMaterial(startBlock, startLocalFace);
            if (targetMaterial == material) return false;

            var region = new List<Entry>();
            var visited = new HashSet<Vector3Int>();
            var pending = new Queue<Vector3Int>();
            pending.Enqueue(startBlock.Cell);

            while (pending.Count > 0)
            {
                Vector3Int cell = pending.Dequeue();
                if (!visited.Add(cell) || !blocksByCell.TryGetValue(cell, out PlacedBlock block) ||
                    block.GetComponent<ColorModule>() != null ||
                    blocksByCell.ContainsKey(cell + surfaceNormal) ||
                    !TryGetLocalFace(assembly, block, surfaceNormal, out Vector3Int face)) continue;

                Material previous = GetMaterial(block, face);
                if (previous != targetMaterial) continue;
                region.Add(new Entry { block = block, face = face, previousMaterial = previous });

                foreach (Vector3Int offset in NeighborOffsets)
                    if (offset.x * surfaceNormal.x + offset.y * surfaceNormal.y + offset.z * surfaceNormal.z == 0)
                        pending.Enqueue(cell + offset);
            }

            var action = new UndoAction();
            foreach (Entry entry in region)
            {
                if (entry.block == null) continue;
                ModuleFacePaint paint = entry.block.GetComponent<ModuleFacePaint>();
                if (paint == null) paint = entry.block.gameObject.AddComponent<ModuleFacePaint>();
                if (paint.SetMaterial(entry.face, material)) action.entries.Add(entry);
            }

            if (action.entries.Count == 0) return false;
            undoHistory.Add(action);
            return true;
        }

        public bool Undo()
        {
            while (undoHistory.Count > 0)
            {
                int last = undoHistory.Count - 1;
                UndoAction action = undoHistory[last];
                undoHistory.RemoveAt(last);
                bool changed = false;
                for (int i = action.entries.Count - 1; i >= 0; i--)
                {
                    Entry entry = action.entries[i];
                    if (entry.block == null) continue;
                    ModuleFacePaint paint = entry.block.GetComponent<ModuleFacePaint>();
                    if (paint != null && paint.SetMaterial(entry.face, entry.previousMaterial)) changed = true;
                }
                if (changed) return true;
            }
            return false;
        }

        public void ClearAllPaint(CubeAssembly assembly)
        {
            if (assembly != null)
                foreach (PlacedBlock block in assembly.GetComponentsInChildren<PlacedBlock>(true))
                    if (block != null && block.transform.parent == assembly.transform)
                        block.GetComponent<ModuleFacePaint>()?.ClearAll();
            undoHistory.Clear();
        }

        private static Material GetMaterial(PlacedBlock block, Vector3Int face)
            => block.GetComponent<ModuleFacePaint>()?.GetMaterial(face);

        private static bool TryGetLocalFace(CubeAssembly assembly, PlacedBlock block,
            Vector3Int surfaceNormal, out Vector3Int face)
        {
            Vector3 localNormal = block.transform.InverseTransformDirection(
                assembly.transform.TransformDirection((Vector3)surfaceNormal));
            face = Mathf.Abs(localNormal.x) >= Mathf.Abs(localNormal.y) && Mathf.Abs(localNormal.x) >= Mathf.Abs(localNormal.z)
                ? new Vector3Int(localNormal.x >= 0f ? 1 : -1, 0, 0)
                : Mathf.Abs(localNormal.y) >= Mathf.Abs(localNormal.z)
                    ? new Vector3Int(0, localNormal.y >= 0f ? 1 : -1, 0)
                    : new Vector3Int(0, 0, localNormal.z >= 0f ? 1 : -1);
            Vector3 aligned = block.transform.TransformDirection((Vector3)face);
            Vector3 expected = assembly.transform.TransformDirection((Vector3)surfaceNormal);
            return Vector3.Dot(aligned.normalized, expected.normalized) > 0.999f;
        }
    }
}
