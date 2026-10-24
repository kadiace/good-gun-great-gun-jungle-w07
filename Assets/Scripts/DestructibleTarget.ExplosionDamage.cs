using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed partial class DestructibleTarget
    {
        public enum ExplosionDamageResult { Ignored, Damaged, Destroyed }
        private readonly Dictionary<int, Cell> intactDamage = new Dictionary<int, Cell>();
        private bool intactGridReady;
        private Bounds intactMeshBounds;
        private bool occupancyPrepared;
        private Cell[] occupancyCells = System.Array.Empty<Cell>();

        // An intact target needs only grid dimensions and the cells that have actually been hit.
        private void PrepareIntactGrid()
        {
            if (subdivided || intactGridReady) return;
            ComputeGridLayout();
            intactGridReady = true;
        }

        private void ComputeGridLayout()
        {
            if (targetMesh == null || targetMesh.sharedMesh == null) return;
            Bounds bounds = targetMesh.sharedMesh.bounds;
            intactMeshBounds = bounds;
            divisions = GetGridDivisions();
            float size = Mathf.Max(.01f, partSize);
            Matrix4x4 matrix = CellRoot.localToWorldMatrix;
            gridOrigin = bounds.min;
            gridStep = new Vector3(size / Mathf.Max(matrix.MultiplyVector(Vector3.right).magnitude, .00001f),
                size / Mathf.Max(matrix.MultiplyVector(Vector3.up).magnitude, .00001f),
                size / Mathf.Max(matrix.MultiplyVector(Vector3.forward).magnitude, .00001f));
            totalCellCount = divisions.x * divisions.y * divisions.z;
        }

        internal bool NeedsOccupancyPrewarm => isActiveAndEnabled && !subdivided && !occupancyPrepared &&
            targetMesh != null && targetMesh.sharedMesh != null;

        // Builds the cell table before combat. Subdivide takes ownership instead of enumerating again.
        internal void PrepareOccupancyTemplate()
        {
            if (subdivided || occupancyPrepared || targetMesh == null || targetMesh.sharedMesh == null) return;
            ComputeGridLayout();
            intactGridReady = true;
            if (occupancyCells.Length != totalCellCount) occupancyCells = new Cell[totalCellCount];
            Bounds bounds = intactMeshBounds;
            int index = 0;
            for (int x = 0; x < divisions.x; x++)
                for (int y = 0; y < divisions.y; y++)
                    for (int z = 0; z < divisions.z; z++)
                    {
                        Vector3 offset = new Vector3(x * gridStep.x, y * gridStep.y, z * gridStep.z);
                        Vector3 cellSize = Vector3.Min(gridStep, bounds.size - offset);
                        occupancyCells[index] = new Cell
                        {
                            root = CellRoot, slot = index, coordinate = new Vector3Int(x, y, z),
                            explosionHealth = 1d, explosionHitTime = -1000f,
                            localPosition = gridOrigin + offset + cellSize * .5f, localScale = cellSize
                        };
                        index++;
                    }
            occupancyPrepared = true;
        }

        private void InvalidateOccupancyTemplate()
        {
            occupancyPrepared = false;
            occupancyCells = System.Array.Empty<Cell>();
            intactGridReady = false;
        }

        private bool TryGetExplosionCell(Vector3Int coordinate, out Cell cell)
        {
            if (subdivided) return TryGetCell(coordinate, out cell);
            cell = default;
            if (coordinate.x < 0 || coordinate.y < 0 || coordinate.z < 0 || coordinate.x >= divisions.x ||
                coordinate.y >= divisions.y || coordinate.z >= divisions.z) return false;
            int index = CellIndex(coordinate);
            if (intactDamage.TryGetValue(index, out cell)) return true;
            Vector3 offset = Vector3.Scale((Vector3)coordinate, gridStep);
            var size = Vector3.Min(gridStep, intactMeshBounds.size - offset);
            cell = new Cell { root = CellRoot, slot = index, coordinate = coordinate,
                localPosition = gridOrigin + offset + size * .5f, localScale = size,
                explosionHealth = 1d, explosionHitTime = -1000f };
            return true;
        }

        // Health is normalized: a resistance deficit of n requires n + 1 equal hits.
        public ExplosionDamageResult ApplyExplosionDamage(Vector3Int coordinate, float power,
            Vector3 center, float force, ProjectileTuning tuning = null)
        {
            if (DamageProtected || !isActiveAndEnabled || !(power > 0f)) return ExplosionDamageResult.Ignored;
            PrepareIntactGrid();
            if (!TryGetExplosionCell(coordinate, out var cell)) return ExplosionDamageResult.Ignored;
            double damage = 1d / (1d + System.Math.Max(0d, (double)ExplosionResistance - power));
            cell.explosionHealth -= damage;
            if (cell.explosionHealth <= 1e-9d)
            {
                DestroyExplosionPiece(coordinate, center, force);
                return ExplosionDamageResult.Destroyed;
            }
            cell.explosionHitTime = Time.time;
            if (subdivided) preparedCells[CellIndex(coordinate)] = cell;
            else intactDamage[CellIndex(coordinate)] = cell;
            ConfigureDamageAppearance(tuning);
            SetCellDamageAppearance(cell);
            return ExplosionDamageResult.Damaged;
        }

        private CellExplosionSave[] CaptureExplosionDamage()
        {
            var result = new List<CellExplosionSave>();
            if (!subdivided)
            {
                foreach (var cell in intactDamage.Values)
                    result.Add(new CellExplosionSave { coordinate = cell.coordinate, health = cell.explosionHealth });
                return result.ToArray();
            }
            foreach (int index in cells)
            {
                var cell = preparedCells[index];
                if (cell.explosionHealth < 1d)
                    result.Add(new CellExplosionSave { coordinate = cell.coordinate, health = cell.explosionHealth });
            }
            return result.ToArray();
        }

        private void RestoreExplosionDamage(CellExplosionSave[] saved)
        {
            if (saved == null) return;
            PrepareIntactGrid();
            ConfigureDamageAppearance(null);
            foreach (var entry in saved)
            {
                if (entry == null || double.IsNaN(entry.health) || entry.health <= 0d || entry.health >= 1d ||
                    !TryGetExplosionCell(entry.coordinate, out var cell)) continue;
                cell.explosionHealth = entry.health;
                cell.explosionHitTime = -1000f; // Loading restores damage, not a new hit flash.
                if (subdivided) preparedCells[CellIndex(entry.coordinate)] = cell;
                else intactDamage[CellIndex(entry.coordinate)] = cell;
                SetCellDamageAppearance(cell);
            }
        }
    }
}
