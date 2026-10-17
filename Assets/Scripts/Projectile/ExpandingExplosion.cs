using System.Collections.Generic;
using System;
using Unity.Profiling;
using UnityEngine;

namespace WeaponWorkshop
{
    // One candidate query, then data-only processing as the wave grows.
    [DefaultExecutionOrder(-1000)]
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
            public int pieceStart, pieceCount;
        }
        private struct HealthHit { public TargetHealth target; public float distance; }
        private struct TargetCandidate { public DestructibleTarget target; public float distanceSqr; }
        private struct ExplosionRequest
        {
            public Vector3 center;
            public float maximumRadius, strongestPower, totalPower;
            public WeaponStats stats;
            public ProjectileTuning tuning;
            public float baseDamage;
            public int collisionLayers;
            public ParticleSystem effectOverride;
            public Transform owner, weapon;
            public ProjectileMotor projectile;
            public int projectileLaunchId;
        }
        private struct ChainRequest
        {
            public ProjectileMotor projectile;
            public int launchId;
            public Vector3 point, direction;
        }

        private const float MaximumMergedPowerMultiplier = 4f;
        private static readonly List<ExplosionRequest> pending = new List<ExplosionRequest>(2048);
        private static readonly Dictionary<DestructibleTarget, float> collectTargetDistances = new Dictionary<DestructibleTarget, float>(256);
        private static readonly Dictionary<TargetHealth, float> collectNearest = new Dictionary<TargetHealth, float>(512);
        private static readonly Stack<Layer> sharedLayerCache = new Stack<Layer>(8192);
        private static int collectionBudgetFrame = -1;
        private static readonly List<ProjectileMotor> activeProjectileBuffer = new List<ProjectileMotor>(2048);
        private static int activeProjectileFrame = -1;
        private static readonly HashSet<DestructibleTarget> dirtyTargets = new HashSet<DestructibleTarget>(256);
        private static readonly Queue<ChainRequest> pendingChainDetonations = new Queue<ChainRequest>(2048);
        private static readonly HashSet<ProjectileMotor> pendingChainSet = new HashSet<ProjectileMotor>(2048);
        private static int pendingFrame = -1;

        private readonly List<Layer> layers = new List<Layer>();
        private readonly List<Piece> pieces = new List<Piece>();
        private readonly List<TargetCandidate> targetCandidates = new List<TargetCandidate>();
        private ProjectileStats impactStats;
        private readonly List<HealthHit> healthHits = new List<HealthHit>();
        private readonly List<DestructibleTarget> blastOccluders = new List<DestructibleTarget>();
        private float[] energy;
        private float radius, maximumRadius, speed, baseDamage;
        private WeaponStats stats;
        private ProjectileTuning tuning;
        private ParticleSystem explosionEffect;
        private int nextLayer, nextHealth, nextTargetCandidate;
        private bool collectionPending;
        private float checkDistance;
        private ProjectileMotor originatingProjectile;
        private int originatingLaunchId;
        public Transform WeaponSource { get; private set; }
        private static readonly ProfilerMarker createMarker = new ProfilerMarker("ExpandingExplosion.Create");
        private static readonly ProfilerMarker mergeMarker = new ProfilerMarker("ExpandingExplosion.Merge");
        private static readonly ProfilerMarker buildMarker = new ProfilerMarker("ExpandingExplosion.Build");
        private static readonly ProfilerMarker collectMarker = new ProfilerMarker("ExpandingExplosion.CollectPieces");
        private static readonly ProfilerMarker collectionWaitMarker = new ProfilerMarker("ExpandingExplosion.CollectionWait");
        private static readonly ProfilerMarker stepMarker = new ProfilerMarker("ExpandingExplosion.Step");
        private static readonly Comparison<Layer> layerComparison = CompareLayers;
        private static readonly Comparison<HealthHit> healthHitComparison = CompareHealthHits;
        private static readonly Comparison<Piece> pieceComparison = ComparePieces;
        private static readonly Comparison<TargetCandidate> targetCandidateComparison = CompareTargetCandidates;

        private static int CompareLayers(Layer a, Layer b)
        {
            int shell = a.shell.CompareTo(b.shell);
            return shell != 0 ? shell : a.direction.CompareTo(b.direction);
        }

        private static int CompareHealthHits(HealthHit a, HealthHit b) => a.distance.CompareTo(b.distance);

        private static int ComparePieces(Piece a, Piece b)
        {
            int aTargetId = a.target != null ? a.target.GetInstanceID() : 0;
            int bTargetId = b.target != null ? b.target.GetInstanceID() : 0;
            int target = aTargetId.CompareTo(bTargetId);
            if (target != 0) return target;
            int shell = a.shell.CompareTo(b.shell);
            return shell != 0 ? shell : a.direction.CompareTo(b.direction);
        }

        private static int CompareTargetCandidates(TargetCandidate a, TargetCandidate b)
        {
            int distance = a.distanceSqr.CompareTo(b.distanceSqr);
            if (distance != 0) return distance;
            int aTargetId = a.target != null ? a.target.GetInstanceID() : 0;
            int bTargetId = b.target != null ? b.target.GetInstanceID() : 0;
            return aTargetId.CompareTo(bTargetId);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pending.Clear();
            collectTargetDistances.Clear(); collectNearest.Clear();
            sharedLayerCache.Clear(); activeProjectileBuffer.Clear(); dirtyTargets.Clear();
            pendingChainDetonations.Clear(); pendingChainSet.Clear();
            collectionBudgetFrame = -1;
            activeProjectileFrame = -1;
            pendingFrame = -1;
        }

        private static void BeginCollectionBudget(float timeBudgetMilliseconds)
        {
            float requested = Mathf.Max(0f, timeBudgetMilliseconds);
            if (collectionBudgetFrame != Time.frameCount)
            {
                collectionBudgetFrame = Time.frameCount;
                DestructibleTarget.SubdivisionBudgetMilliseconds = requested;
            }
            else DestructibleTarget.SubdivisionBudgetMilliseconds =
                Mathf.Min(DestructibleTarget.SubdivisionBudgetMilliseconds, requested);
        }

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
            ProjectileStatsSource source, Transform owner, Transform weapon, ProjectileMotor projectile, ParticleSystem effectOverride = null)
        {
            using (createMarker.Auto())
            {
                if (source == null || stats.explosion <= 0f || maximumRadius <= 0f) return;
                using (mergeMarker.Auto()) QueueRequest(center, maximumRadius, stats, source, owner, weapon, projectile, effectOverride);
            }
        }

        private static void QueueRequest(Vector3 center, float maximumRadius, WeaponStats stats,
            ProjectileStatsSource source, Transform owner, Transform weapon, ProjectileMotor projectile, ParticleSystem effectOverride = null)
        {
            if (pendingFrame != Time.frameCount)
            {
                if (pending.Count > 0) FlushPending();
                pendingFrame = Time.frameCount;
            }

            var request = new ExplosionRequest
            {
                center = center, maximumRadius = maximumRadius, strongestPower = stats.explosion,
                totalPower = stats.explosion, stats = stats, tuning = source.Tuning,
                baseDamage = source.BaseDamage, collisionLayers = source.CollisionLayers, effectOverride = effectOverride,
                owner = owner, weapon = weapon, projectile = projectile,
                projectileLaunchId = projectile != null ? projectile.LaunchId : 0
            };
            if (weapon != null)
            {
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    var existing = pending[i];
                    if (existing.weapon != weapon || existing.effectOverride != effectOverride) continue;
                    float mergeDistance = Mathf.Min(existing.maximumRadius, maximumRadius) *
                        Mathf.Max(0f, source.Tuning.explosionMergeRadiusMultiplier);
                    if ((existing.center - center).sqrMagnitude > mergeDistance * mergeDistance) continue;

                    float previousStrongest = existing.strongestPower;
                    existing.totalPower += stats.explosion;
                    if (stats.explosion > previousStrongest)
                    {
                        existing.strongestPower = stats.explosion;
                        existing.stats = stats;
                        existing.tuning = source.Tuning;
                        existing.baseDamage = source.BaseDamage;
                        existing.collisionLayers = source.CollisionLayers;
                        existing.owner = owner;
                        existing.projectile = projectile;
                        existing.projectileLaunchId = projectile != null ? projectile.LaunchId : 0;
                    }
                    existing.maximumRadius = Mathf.Max(existing.maximumRadius, maximumRadius);
                    float mergedPower = Mathf.Min(existing.totalPower,
                        existing.strongestPower * MaximumMergedPowerMultiplier);
                    existing.stats.explosion = mergedPower;
                    existing.maximumRadius = Mathf.Max(existing.maximumRadius,
                        source.Tuning.explosionRadiusPerPoint * Mathf.Pow(mergedPower, 1f / 3f));
                    pending[i] = existing;
                    return;
                }
            }
            pending.Add(request);
        }

        internal static void FlushPending()
        {
            if (pending.Count == 0) { pendingFrame = -1; return; }
            for (int i = 0; i < pending.Count; i++)
            {
                var request = pending[i];
                if (request.tuning == null) continue;
                using (buildMarker.Auto())
                    CreateWave(request.center, request.maximumRadius, request.stats, request.tuning, request.baseDamage, request.collisionLayers,
                        request.owner, request.weapon, request.projectile, request.projectileLaunchId, request.effectOverride);
            }
            pending.Clear();
            pendingFrame = -1;
        }

        internal static void ClearPendingRequests()
        {
            pending.Clear(); pendingFrame = -1;
            pendingChainDetonations.Clear(); pendingChainSet.Clear();
        }

        internal static void ProcessPendingChainDetonations()
        {
            if (Time.deltaTime <= 0f) return;
            while (pendingChainDetonations.Count > 0)
            {
                var request = pendingChainDetonations.Dequeue();
                pendingChainSet.Remove(request.projectile);
                if (request.projectile == null || !request.projectile.isActiveAndEnabled ||
                    request.projectile.LaunchId != request.launchId) continue;
                if (!PoolManager.TryConsumeChainDetonation())
                {
                    pendingChainDetonations.Enqueue(request);
                    pendingChainSet.Add(request.projectile);
                    break;
                }
                request.projectile.DetonateFromExplosion(request.point, request.direction);
            }
        }

        private static void QueueChainDetonation(ProjectileMotor projectile, Vector3 point, Vector3 direction)
        {
            if (projectile == null || !pendingChainSet.Add(projectile)) return;
            pendingChainDetonations.Enqueue(new ChainRequest
            {
                projectile = projectile, launchId = projectile.LaunchId, point = point, direction = direction
            });
        }

        private static void CreateWave(Vector3 center, float maximumRadius, WeaponStats stats,
            ProjectileTuning tuning, float baseDamage, int collisionLayers, Transform owner, Transform weapon, ProjectileMotor projectile, int projectileLaunchId, ParticleSystem effectOverride)
        {
            var wave = PoolManager.RentComponent<ExpandingExplosion>(center);
            var root = wave.gameObject;
            wave.ResetWave();
            wave.WeaponSource = weapon;
            wave.originatingProjectile = projectile;
            wave.originatingLaunchId = projectileLaunchId;
            wave.maximumRadius = maximumRadius;
            wave.stats = stats; wave.impactStats = ProjectileStats.FromWeapon(stats); wave.tuning = tuning; wave.baseDamage = baseDamage;
            wave.explosionEffect = effectOverride != null ? effectOverride : wave.tuning.explosionEffect;
            wave.speed = Mathf.Max(0.01f, wave.tuning.explosionExpansionSpeed);
            if (wave.energy == null) wave.energy = new float[384];
            for (int i = 0; i < wave.energy.Length; i++) wave.energy[i] = stats.explosion;
            collectTargetDistances.Clear(); collectNearest.Clear();
            DestructibleTarget settingsTarget = null;
            float closest = float.PositiveInfinity;
            int count = CombatPhysicsQueries.OverlapSphere(center, maximumRadius, collisionLayers, out var overlaps);
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
                    float distance = (collider.ClosestPoint(center) - center).sqrMagnitude;
                    if (stats.water <= 0f &&
                        (!collectTargetDistances.TryGetValue(target, out float oldDistance) || distance < oldDistance))
                        collectTargetDistances[target] = distance;
                    if (distance < closest || (distance == closest && settingsTarget != null && target.GetInstanceID() < settingsTarget.GetInstanceID()))
                    { closest = distance; settingsTarget = target; }
                }
                var health = collider.GetComponentInParent<TargetHealth>();
                if (health != null && !health.IsDead)
                {
                    float distance = Vector3.Distance(center, collider.ClosestPoint(center));
                    if (!collectNearest.TryGetValue(health, out float old) || distance < old) collectNearest[health] = distance;
                }
            }
            // The closest struck material supplies wave settings; empty-space impacts use projectile tuning.
            wave.speed = settingsTarget != null ? settingsTarget.ExplosionExpansionSpeed : Mathf.Max(0.01f,wave.tuning.explosionExpansionSpeed);
            wave.checkDistance = settingsTarget != null ? settingsTarget.ExplosionCheckDistance : Mathf.Max(0.01f,wave.tuning.explosionCheckDistance);
            foreach (var entry in collectTargetDistances)
            {
                wave.targetCandidates.Add(new TargetCandidate { target = entry.Key, distanceSqr = entry.Value });
                wave.blastOccluders.Add(entry.Key);
            }
            wave.targetCandidates.Sort(targetCandidateComparison);
            foreach (var entry in collectNearest) wave.healthHits.Add(new HealthHit { target = entry.Key, distance = entry.Value });
            collectTargetDistances.Clear(); collectNearest.Clear();
            wave.collectionPending = true;
            root.SetActive(true);
            wave.PlayExplosionEffect();
            wave.ContinuePieceCollection();
        }

        private bool ContinuePieceCollection()
        {
            if (!collectionPending) return true;
            using (collectionWaitMarker.Auto())
            {
                BeginCollectionBudget(tuning != null ? tuning.subdivisionBudgetMilliseconds : 4f);
                using (collectMarker.Auto())
                {
                    while (nextTargetCandidate < targetCandidates.Count)
                    {
                        var target = targetCandidates[nextTargetCandidate].target;
                        if (target == null)
                        {
                            nextTargetCandidate++;
                            continue;
                        }
                        if (!target.CanCollectExplosionPiecesNow(transform.position, maximumRadius)) break;
                        if (!target.TryCollectExplosionPieces(transform.position, maximumRadius, checkDistance, pieces)) break;
                        nextTargetCandidate++;
                    }
                }

                if (nextTargetCandidate < targetCandidates.Count) return false;
                CompletePieceCollection();
                return true;
            }
        }

        private void CompletePieceCollection()
        {
            using (buildMarker.Auto())
            {
                // Keep each wave's pieces in one reusable buffer until every target has been collected.
                pieces.Sort(pieceComparison);
                for (int pieceIndex = 0; pieceIndex < pieces.Count;)
                {
                    var piece = pieces[pieceIndex];
                    int end = pieceIndex + 1;
                    while (end < pieces.Count)
                    {
                        var next = pieces[end];
                        if (next.target != piece.target || next.direction != piece.direction || next.shell != piece.shell) break;
                        end++;
                    }
                    var layer = sharedLayerCache.Count > 0 ? sharedLayerCache.Pop() : new Layer();
                    layer.target = piece.target; layer.direction = piece.direction; layer.shell = piece.shell;
                    layer.distance = Mathf.Min(maximumRadius, (piece.shell + 1) * checkDistance);
                    layer.pieceStart = pieceIndex; layer.pieceCount = end - pieceIndex;
                    layers.Add(layer);
                    pieceIndex = end;
                }
                layers.Sort(layerComparison);
                healthHits.Sort(healthHitComparison);
            }
            targetCandidates.Clear();
            nextTargetCandidate = 0;
            collectionPending = false;
        }

        private void PlayExplosionEffect()
        {
            if (explosionEffect != null && PoolManager.TryUseExplosionEffectSlot())
            {
                PoolManager.EnsureExplosionEffectPrewarm(explosionEffect.gameObject);
                var effect = PoolManager.RentPrefab(explosionEffect.gameObject, transform.position, Quaternion.identity);
                effect.transform.localScale = Vector3.one * Mathf.Max(0.001f, maximumRadius * tuning.explosionVisualScaleMultiplier);
                effect.GetComponent<PooledCombatObject>().PlayParticles(returnWhenFinished: true);
            }
        }

        private void ResetWave()
        {
            foreach (var layer in layers)
            {
                layer.target = null; layer.pieceStart = layer.pieceCount = 0;
                if (sharedLayerCache.Count < 8192) sharedLayerCache.Push(layer);
            }
            layers.Clear(); pieces.Clear(); targetCandidates.Clear(); healthHits.Clear(); blastOccluders.Clear(); radius = 0f;
            nextLayer = nextHealth = nextTargetCandidate = 0;
            collectionPending = false;
            tuning = null; explosionEffect = null; stats = default; impactStats = default;
            WeaponSource = null;
            originatingProjectile = null; originatingLaunchId = 0;
        }
        private void OnDisable() => ResetWave();

        private void Update()
        {
            using (stepMarker.Auto()) Step();
        }

        // Query surviving voxels after this wave's destruction has updated the geometry.
        // Unlike a Physics ray, this also detects armor containing the impact point.
        private bool IsHealthShielded(TargetHealth target)
        {
            Vector3 end = target.AimPoint;
            foreach (var armor in blastOccluders)
            {
                if (armor == null || !armor.isActiveAndEnabled ||
                    armor.transform.IsChildOf(target.transform) || target.transform.IsChildOf(armor.transform)) continue;
                if (armor.SweepDebris(transform.position, end, 0f, out float fraction, out _, out _) && fraction < .999f)
                    return true;
            }
            return false;
        }

        private void Step()
        {
            if (collectionPending && !ContinuePieceCollection()) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            ProcessPendingChainDetonations();
            radius = Mathf.Min(maximumRadius, radius + speed * dt);
            if (activeProjectileFrame != Time.frameCount)
            {
                activeProjectileBuffer.Clear();
                ProjectileMotor.CopyActiveProjectiles(activeProjectileBuffer);
                activeProjectileFrame = Time.frameCount;
            }
            foreach (var projectile in activeProjectileBuffer)
            {
                if (projectile == null || !projectile.isActiveAndEnabled ||
                    (projectile == originatingProjectile && projectile.LaunchId == originatingLaunchId) ||
                    Vector3.SqrMagnitude(projectile.transform.position - transform.position) > radius * radius) continue;
                if (PoolManager.TryConsumeChainDetonation()) projectile.DetonateFromExplosion();
                else QueueChainDetonation(projectile, projectile.transform.position, projectile.CurrentDirection);
            }
            dirtyTargets.Clear();
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
                    int pieceEnd = layer.pieceStart + layer.pieceCount;
                    for (int pieceIndex = layer.pieceStart; pieceIndex < pieceEnd; pieceIndex++)
                        changed |= layer.target.DestroyExplosionPiece(pieces[pieceIndex].coordinate, transform.position,
                            incoming * tuning.explosionDebrisPushPerPower);
                    if (changed) { cost += resistance; dirtyTargets.Add(layer.target); }
                }
                energy[first.direction] = blocked ? 0f : Mathf.Max(0f, incoming - cost);
                nextLayer = end;
            }
            PoolManager.QueueExplosionFinish(dirtyTargets);
            dirtyTargets.Clear();
            if (nextHealth < healthHits.Count && healthHits[nextHealth].distance <= radius)
                PoolManager.FlushExplosionFinish();
            while (nextHealth < healthHits.Count && healthHits[nextHealth].distance <= radius)
            {
                var hit = healthHits[nextHealth++];
                if (hit.target == null || hit.target.IsDead || IsHealthShielded(hit.target)) continue;
                float falloff = 1f - Mathf.Clamp01(hit.distance / maximumRadius);
                HitPayload.ApplyToTarget(hit.target, stats, tuning, baseDamage * stats.explosion * falloff,
                    hit.target.transform.position - transform.position, true);
            }
            if (radius >= maximumRadius) PoolManager.Release(gameObject);
        }
    }
}
