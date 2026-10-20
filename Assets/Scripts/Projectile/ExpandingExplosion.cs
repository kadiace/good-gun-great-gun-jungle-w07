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
        private struct SlotHit { public SlotMachineReels target; public float distance; }
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
        // 한 레이어의 조각 8개마다 예산을 본다. 조각당 비용이 큰 구간에서 확인 사이에
        // 몰리는 초과폭을 줄여 30ms 상한을 지킨다(Stopwatch 호출 비용은 무시할 수준).
        private const int PieceBudgetCheckInterval = 8;
        // Destruction work one frame may spend on all live waves when nothing is deferred.
        // Tunable in one place only: these static fields, never the inspector or a tuning asset.
        internal static float StepBudgetMilliseconds = 4f;
        // 적응형 프레임 예산의 상한. 큐 헤어가 대기하는 동안 예산을 StepBudgetMilliseconds에서
        // StepMaxDeferredFrames프레임에 걸쳐 이 값까지 선형으로 올린다. 밀린 일을 예산 없이
        // 한 프레임에 처리하던 과거 따라잡기 프레임 대신, 밀린 일이 상한 있는 여러 프레임에 나누인다.
        internal static float StepBudgetMaxMilliseconds = 30f;
        // Extra slice the single most-deferred wave still gets on a frame whose budget is already
        // spent, so deferred waves rotate through the queue instead of starving in a large storm.
        internal static float StepWaveBudgetMilliseconds = 1f;
        private static int stepBudgetFrame = -1;
        private static long stepFrameUsedTicks, stepFrameBudgetTicks;
        // Waves the budget stops mid-work re-queue at the tail and take turns in FIFO order; while
        // the frame budget remains every queued wave keeps working, and once it is spent only the
        // queue head still gets one guaranteed slice per frame (FIFO stays as the tie-break).
        // Released or disabled waves leave dead tickets that are skipped lazily so the queue
        // cannot stall.
        private static long stepServeTicket = 1, stepTicketTail = 1;
        // Live ticket -> frame it joined the queue.
        private static readonly Dictionary<long, int> stepLiveTickets = new Dictionary<long, int>(1024);
        private static int stepSliceFrame = -1;
        // 큐 헤어가 이만큼 대기하면 프레임 예산이 StepBudgetMaxMilliseconds에 도달한다. 헤어진
        // 보장 슬라이스까지 매 턴을 놓지 않으므로, 예산이 상한에 닿기 전에도 파괴가 폭발에 눈에
        // 보이게 뒤처지지는 않는다.
        internal static int StepMaxDeferredFrames = 6;
        private static readonly List<ExplosionRequest> pending = new List<ExplosionRequest>(2048);
        private static readonly Dictionary<DestructibleTarget, float> collectTargetDistances = new Dictionary<DestructibleTarget, float>(256);
        private static readonly Dictionary<TargetHealth, float> collectNearest = new Dictionary<TargetHealth, float>(512);
        private static readonly Dictionary<SlotMachineReels, float> collectSlots = new Dictionary<SlotMachineReels, float>();
        private static readonly Stack<Layer> sharedLayerCache = new Stack<Layer>(8192);
        private static int collectionBudgetFrame = -1;
        private static readonly List<ProjectileMotor> activeProjectileBuffer = new List<ProjectileMotor>(2048);
        // 체인 기폭 검사용 프레임 스냅샷. 프레임당 1회 활성 탄환 목록·위치·활성 여부를 배열로
        // 모아 웨이브마다 탄환 전체를 다시 조회하지 않고 배열 구 판정만 한다. 탄환은 웨이브
        // Update(실행 순서 -1000)보다 뒤에서 움직이므로 프레임 중 위치가 변하지 않는다.
        private static ProjectileMotor[] chainProjectiles = Array.Empty<ProjectileMotor>();
        private static Vector3[] chainPositions = Array.Empty<Vector3>();
        private static bool[] chainAlive = Array.Empty<bool>();
        // 같은 프레임에 반납 후 재대여된 탄을 옛 위치로 기폭하지 않도록 발사 ID를 함께 담는다.
        private static int[] chainLaunchIds = Array.Empty<int>();
        private static int chainProjectileCount;
        private static int activeProjectileFrame = -1;
        private static readonly HashSet<DestructibleTarget> dirtyTargets = new HashSet<DestructibleTarget>(256);
        private static readonly Queue<ChainRequest> pendingChainDetonations = new Queue<ChainRequest>(2048);
        private static readonly HashSet<ProjectileMotor> pendingChainSet = new HashSet<ProjectileMotor>(2048);
        private static int pendingFrame = -1;

        private readonly List<Layer> layers = new List<Layer>();
        private readonly List<Piece> pieces = new List<Piece>();
        private readonly HashSet<(DestructibleTarget target, Vector3Int coordinate)> damagedPieces = new();
        private readonly List<TargetCandidate> targetCandidates = new List<TargetCandidate>();
        private ProjectileStats impactStats;
        private readonly List<HealthHit> healthHits = new List<HealthHit>();
        private readonly List<SlotHit> slotHits = new List<SlotHit>();
        private readonly List<DestructibleTarget> blastOccluders = new List<DestructibleTarget>();
        private float[] energy;
        private float radius, maximumRadius, speed, baseDamage;
        private WeaponStats stats;
        private ProjectileTuning tuning;
        private ParticleSystem explosionEffect;
        private int nextLayer, nextHealth, nextSlot, nextTargetCandidate;
        // Layer group left half-applied when the frame budget stopped the wave mid-group;
        // groupPieceIndex/groupChanged/groupResistance carry a layer broken in the middle of its
        // piece list (the target may be gone by the time the layer completes).
        private bool groupOpen, groupBlocked, groupChanged;
        private int groupEnd, groupDirection, groupPieceIndex = -1;
        private float groupIncoming, groupCost, groupResistance;
        // Deferred-work queue ticket (0 = no deferred work) and the running turn's time marks.
        private long stepTicket, stepTurnMarkTicks, stepTurnUsedTicks;
        private bool stepSliceTurn;
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
        private static readonly Comparison<SlotHit> slotHitComparison = (a, b) => a.distance.CompareTo(b.distance);
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
            collectTargetDistances.Clear(); collectNearest.Clear(); collectSlots.Clear();
            sharedLayerCache.Clear(); activeProjectileBuffer.Clear(); dirtyTargets.Clear();
            pendingChainDetonations.Clear(); pendingChainSet.Clear();
            chainProjectiles = Array.Empty<ProjectileMotor>();
            chainPositions = Array.Empty<Vector3>();
            chainAlive = Array.Empty<bool>();
            chainLaunchIds = Array.Empty<int>();
            chainProjectileCount = 0;
            collectionBudgetFrame = -1;
            activeProjectileFrame = -1;
            pendingFrame = -1;
            stepBudgetFrame = -1;
            stepFrameUsedTicks = 0;
            stepServeTicket = 1; stepTicketTail = 1;
            stepLiveTickets.Clear();
            stepSliceFrame = -1;
            stepFrameBudgetTicks = 0;
        }

        private static long BudgetTicks(float milliseconds) =>
            (long)(Mathf.Max(0f, milliseconds) * System.Diagnostics.Stopwatch.Frequency / 1000f);

        // Opens one frame's budget window: retires dead tickets (a released wave cannot stall the
        // queue) and sizes the frame budget by how long the queue head has waited. Deep deferral
        // therefore drains over several capped frames instead of one unbudgeted catch-up frame.
        private static void BeginStepFrame()
        {
            if (stepBudgetFrame == Time.frameCount) return;
            stepBudgetFrame = Time.frameCount;
            stepFrameUsedTicks = 0;
            // Skip tickets whose wave was released or disabled before its turn came up.
            while (stepServeTicket < stepTicketTail && !stepLiveTickets.ContainsKey(stepServeTicket)) stepServeTicket++;
            int deferredFrames = stepLiveTickets.TryGetValue(stepServeTicket, out int queuedFrame)
                ? Mathf.Max(0, Time.frameCount - queuedFrame) : 0;
            stepFrameBudgetTicks = BudgetTicks(Mathf.Lerp(StepBudgetMilliseconds, StepBudgetMaxMilliseconds,
                Mathf.Clamp01(deferredFrames / (float)Mathf.Max(1, StepMaxDeferredFrames))));
        }

        // Decides whether this wave may start heavy work now. Any wave — fresh or queued — starts
        // while the frame budget remains, so leftover budget is spent on waiting waves instead of
        // being thrown away while they age in the queue. The FIFO order only breaks ties once the
        // budget is spent: the queue head alone still gets one guaranteed wave slice per frame,
        // and the head's wait length drives the adaptive budget. The ticket dictionary is touched
        // once per frame here and on deferral, not on every Step.
        private bool BeginStepTurn(out bool sliceTurn)
        {
            sliceTurn = false;
            BeginStepFrame();
            bool budgetLeft = stepFrameUsedTicks < stepFrameBudgetTicks;
            if (budgetLeft) return true;
            if (stepTicket == 0) return false;
            if (stepTicket != stepServeTicket) return false;
            if (stepSliceFrame != Time.frameCount)
            {
                stepSliceFrame = Time.frameCount;
                sliceTurn = true;
                return true;
            }
            return false;
        }

        // Charges the time since the previous check to the frame budget and to the running turn.
        // A budget-paid turn stops at the (adaptive) frame budget; a guaranteed slice stops at the
        // wave share.
        private bool StepBudgetExhausted()
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            long elapsed = now - stepTurnMarkTicks;
            stepTurnMarkTicks = now;
            stepTurnUsedTicks += elapsed;
            stepFrameUsedTicks += elapsed;
            if (stepSliceTurn)
                return stepTurnUsedTicks >= BudgetTicks(Mathf.Max(0.05f, StepWaveBudgetMilliseconds));
            return stepFrameUsedTicks >= stepFrameBudgetTicks;
        }

        // Closes the turn: retires the queue ticket (advancing the head past it) and, while work
        // remains, re-queues at the tail so the next most-deferred wave becomes the head.
        private void EndStepTurn(bool workPending)
        {
            if (stepTicket != 0)
            {
                if (stepTicket == stepServeTicket) stepServeTicket++;
                stepLiveTickets.Remove(stepTicket);
                stepTicket = 0;
            }
            if (workPending)
            {
                stepTicket = stepTicketTail++;
                stepLiveTickets[stepTicket] = Time.frameCount;
            }
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
            collectTargetDistances.Clear(); collectNearest.Clear(); collectSlots.Clear();
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
                var slot = collider.GetComponentInParent<SlotMachineReels>();
                if (slot != null)
                {
                    float distance = Vector3.Distance(center, collider.ClosestPoint(center));
                    if (!collectSlots.TryGetValue(slot, out float old) || distance < old) collectSlots[slot] = distance;
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
            foreach (var entry in collectSlots) wave.slotHits.Add(new SlotHit { target = entry.Key, distance = entry.Value });
            collectTargetDistances.Clear(); collectNearest.Clear(); collectSlots.Clear();
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
                // Spawn runs this outside a Step turn; open the frame budget and the marks so the
                // first budget check charges real time against the frame budget.
                BeginStepFrame();
                if (stepTurnMarkTicks == 0) stepTurnMarkTicks = System.Diagnostics.Stopwatch.GetTimestamp();
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
                        // Collection is the wave's first heavy pass; the frame budget carries the
                        // remaining candidates to the next frame instead of stalling this one.
                        if (StepBudgetExhausted()) break;
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
                slotHits.Sort(slotHitComparison);
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
            layers.Clear(); pieces.Clear(); targetCandidates.Clear(); healthHits.Clear(); slotHits.Clear(); blastOccluders.Clear(); radius = 0f;
            damagedPieces.Clear();
            nextLayer = nextHealth = nextSlot = nextTargetCandidate = 0;
            groupOpen = false; groupBlocked = false; groupChanged = false;
            groupEnd = groupDirection = 0; groupPieceIndex = -1; groupIncoming = groupCost = groupResistance = 0f;
            if (stepTicket != 0)
            {
                if (stepTicket == stepServeTicket) stepServeTicket++;
                stepLiveTickets.Remove(stepTicket);
                stepTicket = 0;
            }
            stepTurnMarkTicks = 0; stepTurnUsedTicks = 0; stepSliceTurn = false;
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

        // Time-based work that keeps running even when this wave's destruction budget is spent:
        // the radius keeps expanding with time, chain detonations keep being checked and the
        // wave still releases once every queued piece of work is done.
        private void LightStep()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            // 조각 수집을 기다리는 웨이브는 원래처럼 반경도 키우지 않고 연쇄 검사도 하지 않는다.
            if (collectionPending) return;
            GrowRadiusAndCheckChain(dt);
            if (!collectionPending && radius >= maximumRadius &&
                nextLayer >= layers.Count && nextHealth >= healthHits.Count)
                PoolManager.Release(gameObject);
        }

        private void GrowRadiusAndCheckChain(float dt)
        {
            ProcessPendingChainDetonations();
            // 원래 웨이브는 최대 반경에 닿은 프레임에 반납돼 그 뒤로는 연쇄 검사가 없었다. 예산으로 남아 있는
            // 웨이브가 최대 반경 상태로 계속 검사하면 나중에 그 자리를 지나는 탄이 공중에서 터진다.
            bool expanding = radius < maximumRadius;
            radius = Mathf.Min(maximumRadius, radius + speed * dt);
            if (!expanding) return;
            if (activeProjectileFrame != Time.frameCount)
            {
                // 프레임당 1회만 목록을 훑어 위치와 활성 여부를 배열로 모은다.
                activeProjectileBuffer.Clear();
                ProjectileMotor.CopyActiveProjectiles(activeProjectileBuffer);
                int count = activeProjectileBuffer.Count;
                if (chainProjectiles.Length < count)
                {
                    int size = Mathf.NextPowerOfTwo(Mathf.Max(64, count));
                    chainProjectiles = new ProjectileMotor[size];
                    chainPositions = new Vector3[size];
                    chainAlive = new bool[size];
                    chainLaunchIds = new int[size];
                }
                for (int i = 0; i < count; i++)
                {
                    var projectile = activeProjectileBuffer[i];
                    chainProjectiles[i] = projectile;
                    chainPositions[i] = projectile.transform.position;
                    chainAlive[i] = projectile.isActiveAndEnabled;
                    chainLaunchIds[i] = projectile.LaunchId;
                }
                chainProjectileCount = count;
                activeProjectileFrame = Time.frameCount;
            }
            float radiusSqr = radius * radius;
            Vector3 center = transform.position;
            for (int i = 0; i < chainProjectileCount; i++)
            {
                // 스냅샷 때 죽은 탄은 위치 연산 없이 건너뛴다. 스냅샷 뒤 이번 프레임에 기폭되거나
                // 꺼진 탄은 아래 실제 상태 검사에서 걸러 기존과 같은 기폭 결과를 낸다.
                if (!chainAlive[i] ||
                    Vector3.SqrMagnitude(chainPositions[i] - center) > radiusSqr) continue;
                var projectile = chainProjectiles[i];
                if (projectile == null || !projectile.isActiveAndEnabled || projectile.LaunchId != chainLaunchIds[i] ||
                    (projectile == originatingProjectile && projectile.LaunchId == originatingLaunchId)) continue;
                if (PoolManager.TryConsumeChainDetonation()) projectile.DetonateFromExplosion();
                else QueueChainDetonation(projectile, chainPositions[i], projectile.CurrentDirection);
            }
        }

        private void Step()
        {
            if (!BeginStepTurn(out bool sliceTurn))
            {
                // Frame budget spent and not this wave's turn: join the queue, then only run
                // the time-based work so no wave starves and the frame keeps its budget.
                if (stepTicket == 0)
                {
                    stepTicket = stepTicketTail++;
                    stepLiveTickets[stepTicket] = Time.frameCount;
                }
                // 라이트 경로가 쓴 실제 시간도 프레임 예산에 가산해 30ms 상한이 프레임 비용을
                // 반영하게 한다. 가산해도 라이트 작업(반경 확장·체인 검사) 자체는 예산과 무관하게
                // 항상 돈다.
                long lightStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                LightStep();
                stepFrameUsedTicks += System.Diagnostics.Stopwatch.GetTimestamp() - lightStartTicks;
                return;
            }
            stepTurnMarkTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            stepTurnUsedTicks = 0;
            stepSliceTurn = sliceTurn;
            if (collectionPending && !ContinuePieceCollection()) { EndStepTurn(true); return; }
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                EndStepTurn(collectionPending || nextLayer < layers.Count || nextHealth < healthHits.Count);
                return;
            }
            GrowRadiusAndCheckChain(dt);
            dirtyTargets.Clear();
            // Equal distance layers use the same incoming energy, then pay all material costs.
            // Layers after the frame budget are left untouched and resume at the same layer next turn.
            while (nextLayer < layers.Count)
            {
                if (!groupOpen)
                {
                    int end = nextLayer + 1;
                    var first = layers[nextLayer];
                    while (end < layers.Count && layers[end].shell == first.shell && layers[end].direction == first.direction) end++;
                    float arrival = 0f;
                    for (int i = nextLayer; i < end; i++) arrival = Mathf.Max(arrival, layers[i].distance);
                    if (arrival > radius && radius < maximumRadius) break;
                    groupEnd = end; groupDirection = first.direction; groupIncoming = energy[first.direction];
                    groupCost = 0f; groupBlocked = false; groupChanged = false; groupPieceIndex = -1; groupResistance = 0f; groupOpen = true;
                }
                if (StepBudgetExhausted()) break;
                var layer = layers[nextLayer];
                // Seeded from the previous turn when this exact layer was broken mid-piece list.
                bool changed = groupPieceIndex >= 0 ? groupChanged : false;
                bool layerDeferred = false;
                if (layer.target != null && layer.target.isActiveAndEnabled)
                {
                    float resistance = layer.target.ExplosionResistance;
                    groupResistance = resistance;
                    if (groupIncoming <= 0f ||
                        !layer.target.AllowsImpact(impactStats)) groupBlocked = true;
                    else
                    {
                        if (groupIncoming < resistance) groupBlocked = true;
                        int pieceStart = layer.pieceStart;
                        int pieceEnd = pieceStart + layer.pieceCount;
                        int pieceIndex = groupPieceIndex >= 0 ? groupPieceIndex : pieceStart;
                        while (pieceIndex < pieceEnd)
                        {
                            if (pieceIndex > pieceStart && (pieceIndex - pieceStart) % PieceBudgetCheckInterval == 0 &&
                                StepBudgetExhausted())
                            {
                                groupPieceIndex = pieceIndex;
                                layerDeferred = true;
                                break;
                            }
                            var coordinate = pieces[pieceIndex].coordinate;
                            if (!damagedPieces.Add((layer.target, coordinate))) { pieceIndex++; continue; }
                            changed |= layer.target.ApplyExplosionDamage(coordinate, groupIncoming, transform.position,
                                groupIncoming * tuning.explosionDebrisPushPerPower, tuning)
                                == DestructibleTarget.ExplosionDamageResult.Destroyed;
                            pieceIndex++;
                        }
                    }
                }
                if (layerDeferred)
                {
                    // 이번 턴에 깎은 조각 결과를 보존해 다음 턴 이어짐이 같아지게 한다.
                    groupChanged = changed;
                    break;
                }
                if (changed)
                {
                    // 조각을 깎은 레이어만 저항만큼 비용을 낸다. 레이어 도중에 밀린 뒤
                    // 대상이 사라졌어도 시작 때 읽은 저항으로 그룹 에너지 의미를 유지한다.
                    groupCost += groupResistance;
                    if (layer.target != null && layer.target.isActiveAndEnabled) dirtyTargets.Add(layer.target);
                }
                nextLayer++;
                groupPieceIndex = -1;
                groupChanged = false;
                if (nextLayer >= groupEnd)
                {
                    energy[groupDirection] = groupBlocked ? 0f : Mathf.Max(0f, groupIncoming - groupCost);
                    groupOpen = false;
                }
            }
            PoolManager.QueueExplosionFinish(dirtyTargets);
            dirtyTargets.Clear();
            // Health damage keeps the flush-before-query contract; the budget only moves it a frame later.
            if (nextHealth < healthHits.Count && healthHits[nextHealth].distance <= radius && !StepBudgetExhausted())
            {
                PoolManager.FlushExplosionFinish();
                while (nextHealth < healthHits.Count && healthHits[nextHealth].distance <= radius)
                {
                    if (StepBudgetExhausted()) break;
                    var hit = healthHits[nextHealth++];
                    if (hit.target == null || hit.target.IsDead || IsHealthShielded(hit.target)) continue;
                    float falloff = 1f - Mathf.Clamp01(hit.distance / maximumRadius);
                    HitPayload.ApplyToTarget(hit.target, stats, tuning, baseDamage * stats.explosion * falloff,
                        hit.target.transform.position - transform.position, true);
                }
            }
            while (nextSlot < slotHits.Count)
            {
                var hit = slotHits[nextSlot];
                if (hit.distance > radius) break;
                nextSlot++;
                if (hit.target != null) hit.target.ReceiveAttack(stats.penetration, stats.explosion, true);
            }
            // Releasing early would drop the layers and health hits the budget has not reached yet.
            if (radius >= maximumRadius && nextLayer >= layers.Count && nextHealth >= healthHits.Count)
                PoolManager.Release(gameObject);
            EndStepTurn(collectionPending || nextLayer < layers.Count || nextHealth < healthHits.Count);
        }
    }
}
