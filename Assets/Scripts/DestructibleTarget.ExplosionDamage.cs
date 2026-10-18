using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed partial class DestructibleTarget
    {
        public enum ExplosionDamageResult { Ignored, Damaged, Destroyed }

        // Health is normalized: a resistance deficit of n requires n + 1 equal hits.
        public ExplosionDamageResult ApplyExplosionDamage(Vector3Int coordinate, float power,
            Vector3 center, float force, ProjectileTuning tuning = null)
        {
            if (DamageProtected || !isActiveAndEnabled || !(power > 0f) ||
                !TryGetCell(coordinate, out var cell)) return ExplosionDamageResult.Ignored;
            double damage = 1d / (1d + System.Math.Max(0d, (double)ExplosionResistance - power));
            cell.explosionHealth -= damage;
            if (cell.explosionHealth <= 1e-9d)
            {
                DestroyExplosionPiece(coordinate, center, force);
                return ExplosionDamageResult.Destroyed;
            }
            cell.explosionHitTime = Time.time;
            preparedCells[CellIndex(coordinate)] = cell;
            ConfigureDamageAppearance(tuning);
            if (renderChunks.TryGetValue(RenderKey(coordinate), out var chunk)) chunk.damageDirty = true;
            return ExplosionDamageResult.Damaged;
        }

        private CellExplosionSave[] CaptureExplosionDamage()
        {
            var result = new List<CellExplosionSave>();
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
            foreach (var entry in saved)
            {
                if (entry == null || double.IsNaN(entry.health) || entry.health <= 0d || entry.health >= 1d ||
                    !TryGetCell(entry.coordinate, out var cell)) continue;
                cell.explosionHealth = entry.health;
                cell.explosionHitTime = -1000f; // Loading restores damage, not a new hit flash.
                preparedCells[CellIndex(entry.coordinate)] = cell;
            }
            ConfigureDamageAppearance(null);
            foreach (var chunk in renderChunks.Values) chunk.damageDirty = true;
        }
    }
}
