using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // One candidate query, then data-only processing as the wave grows.
    public sealed class ExpandingExplosion : MonoBehaviour
    {
        public struct Piece
        {
            public DestructibleTarget target;
            public Vector3Int coordinate;
            public Vector3 position;
            public float distance;
            public int direction, shell;
        }
        private sealed class Layer
        {
            public DestructibleTarget target;
            public int direction, shell;
            public float distance;
            public readonly List<Piece> pieces = new List<Piece>();
        }
        private struct HealthHit { public TargetHealth target; public float distance; }
        private readonly List<Layer> layers = new List<Layer>();
        private readonly Stack<Layer> layerCache = new Stack<Layer>();
        private readonly HashSet<DestructibleTarget> targets = new HashSet<DestructibleTarget>();
        private readonly Dictionary<TargetHealth, float> nearest = new Dictionary<TargetHealth, float>();
        private readonly List<Piece> pieces = new List<Piece>();
        private readonly Dictionary<(DestructibleTarget, int, int), Layer> grouped = new Dictionary<(DestructibleTarget, int, int), Layer>();
        private ProjectileStats impactStats;
        private readonly List<HealthHit> healthHits = new List<HealthHit>();
        private readonly float[] energy = new float[384];
        private readonly HashSet<DestructibleTarget> dirty = new HashSet<DestructibleTarget>();
        private float radius, maximumRadius, speed, baseDamage;
        private WeaponStats stats;
        private ProjectileTuning tuning;
        private int nextLayer, nextHealth;
        private float checkDistance;

        // Fixed angular bins, independent of target dimensions and voxel size.
        public static int DirectionBin(Vector3 offset)
        {
            if (offset.sqrMagnitude < 0.000001f) return 0;
            Vector3 a = new Vector3(Mathf.Abs(offset.x), Mathf.Abs(offset.y), Mathf.Abs(offset.z));
            int face; float u, v, extent;
            if (a.x >= a.y && a.x >= a.z) { face = offset.x >= 0 ? 0 : 1; extent = a.x; u = offset.y; v = offset.z; }
            else if (a.y >= a.z) { face = offset.y >= 0 ? 2 : 3; extent = a.y; u = offset.x; v = offset.z; }
            else { face = offset.z >= 0 ? 4 : 5; extent = a.z; u = offset.x; v = offset.y; }
            int x = Mathf.Clamp(Mathf.FloorToInt((u / extent + 1f) * 4f), 0, 7);
            int y = Mathf.Clamp(Mathf.FloorToInt((v / extent + 1f) * 4f), 0, 7);
            return face * 64 + y * 8 + x;
        }

        public static void Create(Vector3 center, float maximumRadius, WeaponStats stats,
            ProjectileStatsSource source, Transform owner, Transform weapon)
        {
            if (source == null || stats.explosion <= 0f || maximumRadius <= 0f) return;
            var wave = PoolManager.RentComponent<ExpandingExplosion>(center);
            var root = wave.gameObject;
            wave.ResetWave();
            wave.maximumRadius = maximumRadius;
            wave.stats = stats; wave.impactStats = ProjectileStats.FromWeapon(stats); wave.tuning = source.Tuning; wave.baseDamage = source.BaseDamage;
            wave.speed = Mathf.Max(0.01f, wave.tuning.explosionExpansionSpeed);
            for (int i = 0; i < wave.energy.Length; i++) wave.energy[i] = stats.explosion;
            var targets = wave.targets;
            var nearest = wave.nearest;
            DestructibleTarget settingsTarget = null;
            float closest = float.PositiveInfinity;
            int count = CombatPhysicsQueries.OverlapSphere(center, maximumRadius, source.CollisionLayers, out var overlaps);
            for (int hitIndex = 0; hitIndex < count; hitIndex++)
            {
                var collider = overlaps[hitIndex];
                if ((owner != null && collider.transform.IsChildOf(owner)) ||
                    (weapon != null && collider.transform.IsChildOf(weapon))) continue;
                var target = collider.GetComponentInParent<DestructibleTarget>();
                // 적이 쏜 폭발은 RaidEnemyImmune 파츠(금고 등)를 부수지 않는다. 플레이어 무기는 그대로 부순다.
                if (RaidEnemyImmune.Protects(target, owner)) continue;
                if (target != null)
                {
                    targets.Add(target);
                    float distance = (collider.ClosestPoint(center) - center).sqrMagnitude;
                    if (distance < closest || (distance == closest && settingsTarget != null && target.GetInstanceID() < settingsTarget.GetInstanceID()))
                    { closest = distance; settingsTarget = target; }
                }
                var health = collider.GetComponentInParent<TargetHealth>();
                if (health != null && !health.IsDead)
                {
                    float distance = Vector3.Distance(center, collider.ClosestPoint(center));
                    if (!nearest.TryGetValue(health, out float old) || distance < old) nearest[health] = distance;
                }
            }
            // The closest struck material supplies wave settings; empty-space impacts use projectile tuning.
            wave.speed = settingsTarget != null ? settingsTarget.ExplosionExpansionSpeed : Mathf.Max(0.01f,wave.tuning.explosionExpansionSpeed);
            wave.checkDistance = settingsTarget != null ? settingsTarget.ExplosionCheckDistance : Mathf.Max(0.01f,wave.tuning.explosionCheckDistance);
            var pieces = wave.pieces;
            foreach (var target in targets)
                if (stats.water <= 0f) target.CollectExplosionPieces(center, maximumRadius, wave.checkDistance, pieces);
            // Group cost by target, direction and fixed world-distance layer, never by voxel count.
            var grouped = wave.grouped;
            foreach (var piece in pieces)
            {
                var key = (piece.target, piece.direction, piece.shell);
                if (!grouped.TryGetValue(key, out var layer))
                {
                    layer = wave.layerCache.Count > 0 ? wave.layerCache.Pop() : new Layer();
                    layer.target = piece.target; layer.direction = piece.direction;
                    layer.shell = piece.shell; layer.distance = Mathf.Min(maximumRadius, (piece.shell + 1) * wave.checkDistance);
                    grouped.Add(key, layer); wave.layers.Add(layer);
                }
                layer.pieces.Add(piece);
            }
            wave.layers.Sort((a,b) => { int shell = a.shell.CompareTo(b.shell); return shell != 0 ? shell : a.direction.CompareTo(b.direction); });
            foreach (var entry in nearest) wave.healthHits.Add(new HealthHit { target = entry.Key, distance = entry.Value });
            wave.healthHits.Sort((a,b) => a.distance.CompareTo(b.distance));
            root.SetActive(true);
            if (wave.tuning.explosionEffect != null)
            {
                var effect = PoolManager.RentPrefab(wave.tuning.explosionEffect.gameObject, center, Quaternion.identity);
                effect.transform.localScale = Vector3.one * Mathf.Max(0.001f, maximumRadius * wave.tuning.explosionVisualScaleMultiplier);
                effect.GetComponent<PooledCombatObject>().PlayParticles(returnWhenFinished: true);
            }
        }

        private void ResetWave()
        {
            foreach (var layer in layers)
            {
                layer.target = null; layer.pieces.Clear();
                if (layerCache.Count < 2048) layerCache.Push(layer);
            }
            layers.Clear(); healthHits.Clear(); dirty.Clear(); targets.Clear(); nearest.Clear();
            pieces.Clear(); grouped.Clear(); radius = 0f; nextLayer = nextHealth = 0;
            tuning = null; stats = default; impactStats = default;
        }
        private void OnDisable() => ResetWave();

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            radius = Mathf.Min(maximumRadius, radius + speed * dt);
            dirty.Clear();
            // Equal distance layers use the same incoming energy, then pay all material costs.
            while (nextLayer < layers.Count)
            {
                int end = nextLayer + 1;
                var first = layers[nextLayer];
                while (end < layers.Count && layers[end].shell == first.shell && layers[end].direction == first.direction) end++;
                float arrival = 0f;
                for (int i = nextLayer; i < end; i++) arrival = Mathf.Max(arrival, layers[i].distance);
                if (arrival > radius && radius < maximumRadius) break;
                float incoming = energy[first.direction], cost = 0f;
                bool blocked = false;
                for (int i = nextLayer; i < end; i++)
                {
                    var layer = layers[i];
                    if (layer.target == null || !layer.target.isActiveAndEnabled) continue;
                    float resistance = layer.target.ExplosionResistance;
                    if (incoming <= 0f || incoming < resistance ||
                        !layer.target.AllowsImpact(impactStats)) { blocked = true; continue; }
                    bool changed = false;
                    foreach (var piece in layer.pieces)
                        changed |= layer.target.DestroyExplosionPiece(piece.coordinate, transform.position, incoming * tuning.explosionDebrisPushPerPower);
                    if (changed) { cost += resistance; dirty.Add(layer.target); }
                }
                energy[first.direction] = blocked ? 0f : Mathf.Max(0f, incoming - cost);
                nextLayer = end;
            }
            foreach (var target in dirty) target.FinishExplosionStep();
            while (nextHealth < healthHits.Count && healthHits[nextHealth].distance <= radius)
            {
                var hit = healthHits[nextHealth++];
                if (hit.target == null || hit.target.IsDead) continue;
                float falloff = 1f - Mathf.Clamp01(hit.distance / maximumRadius);
                HitPayload.ApplyToTarget(hit.target, stats, tuning, baseDamage * stats.explosion * falloff,
                    hit.target.transform.position - transform.position, true);
            }
            if (radius >= maximumRadius) PoolManager.Release(gameObject);
        }
    }
}
