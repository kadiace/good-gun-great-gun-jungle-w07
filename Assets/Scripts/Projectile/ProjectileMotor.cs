using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using LogKit;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class ProjectileMotor : MonoBehaviour
    {
        private static readonly ProfilerMarker CollisionQueryMarker = new ProfilerMarker("ProjectileMotor.CollisionQuery");
        private static readonly ProfilerMarker ImpactEffectMarker = new ProfilerMarker("ProjectileMotor.ImpactEffect");
        private static readonly HashSet<ProjectileMotor> activeProjectiles = new HashSet<ProjectileMotor>();
        // LayerMask APIs throw inside a type initializer when Unity first touches this type from a
        // serialization/constructor context (e.g. prefab import), which poisons the type. Resolve lazily.
        private static bool layersResolved;
        private static int projectileExtraCollisionLayers, gunBlockLayers, unusedCastLayers;
        private static int ProjectileExtraCollisionLayers { get { ResolveLayers(); return projectileExtraCollisionLayers; } }
        private static int GunBlockLayers { get { ResolveLayers(); return gunBlockLayers; } }
        private static int UnusedCastLayers { get { ResolveLayers(); return unusedCastLayers; } }
        private static void ResolveLayers()
        {
            if (layersResolved) return;
            projectileExtraCollisionLayers = LayerMask.GetMask("Weapon", "EndChest", "EndChestShield") & ~LayerMask.GetMask("Energy");
            gunBlockLayers = LayerMask.GetMask("GunBlock");
            // 탄이 맞을 일 없는 레이어(투과 FX·물·UI)는 캐스트 전체에서 뺀다. 현재 씬들의 콜라이더는
            // Default·EndChest 계열뿐이고, 트리거는 조회가 이미 무시하므로 후보 수만 늘었다.
            unusedCastLayers = LayerMask.GetMask("TransparentFX", "Water", "UI");
            layersResolved = true;
        }
        [SerializeField] private Renderer drawRenderer;
        [SerializeField] private ParticleSystem impactEffect;

        private readonly HashSet<TargetHealth> hitTargets = new HashSet<TargetHealth>();
        private readonly HashSet<DestructibleTarget> hitStructures = new HashSet<DestructibleTarget>();
        private readonly PenetrationPassages passages = new PenetrationPassages();
        // 통과 구조물 필터는 관통 구간이 처음 기록된 뒤에만 의미를 갖는다. 그 전에는 표적 조회 자체를 건너뛴다.
        private bool passagesRecorded;
        private readonly HashSet<Collider> ignoredColliders = new HashSet<Collider>();
        private readonly List<Collider> staleIgnoredColliders = new List<Collider>();
        private readonly List<Collider> targetColliderBuffer = new List<Collider>();
        private MaterialPropertyBlock debrisTint;
        private ProjectileStatsSource source;
        private ProjectileEffects effects;
        private Vector3 velocity;
        private float launchSpeed;
        private float traveledDistance;
        private const float MaximumLifetime = 5f;
        private float lifetime;
        private Component homingTarget;
        private Vector3 homingLocalAim;
        private Transform ignoredOwner;
        private Transform ignoredWeapon;
        // 예외 판정용 뿌리는 업데이트당 한 번만 다시 본다. 뿌리가 다른 후보는 부모 탐색 없이 예외가 아니다.
        private Transform selfRoot, ownerRoot, weaponRoot;
        private bool ignoreRootsCached;
        public Transform WeaponSource => ignoredWeapon;
        internal Vector3 CurrentDirection => velocity.normalized;
        private Vector3 halfExtents;
        private float penetrationPower;
        private bool hitCounted;
        private bool exploded;
        private int ricochetsRemaining;
        private bool terminal;
        private int launchId;
        private float structureSweepLimit;
        internal int LaunchId => launchId;
        internal ParticleSystem ImpactEffectPrefab => impactEffect;

        private static T GetComponentInParents<T>(Collider collider) where T : Component
        {
            for (Transform current = collider != null ? collider.transform : null; current != null; current = current.parent)
                if (current.TryGetComponent(out T component)) return component;
            return null;
        }
        // 코어·방어막 소속은 콜라이더마다 부모를 타지 않고 공용 인덱스가 캐시한 결과만 쓴다.
        private static void ResolveEndChest(Collider collider, out EndChestCore core, out EndChestShield shield)
            => ColliderTargetIndex.ResolveEndChest(collider, out core, out shield);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveProjectiles() => activeProjectiles.Clear();

        internal static void CopyActiveProjectiles(List<ProjectileMotor> destination)
        {
            destination.Clear();
            destination.AddRange(activeProjectiles);
        }

        public void Initialize(ProjectileStatsSource statsSource, Vector3 direction, float speed,
            Transform owner, Transform weapon)
        {
            source = statsSource;
            launchId++;
            launchSpeed = Mathf.Max(0f, speed);
            effects = GetComponent<ProjectileEffects>();
            terminal = false;
            exploded = false;
            hitCounted = false;
            GameLogger.Count("Attack");
            penetrationPower = effects != null ? Mathf.Max(0f, effects.Stats.penetration) : 0f;
            ignoredOwner = owner;
            ignoredWeapon = weapon;
            selfRoot = ownerRoot = weaponRoot = null; ignoreRootsCached = false;
            traveledDistance = 0f;
            lifetime = 0f;
            ricochetsRemaining = effects != null
                ? Mathf.Max(0, Mathf.FloorToInt(effects.Stats.ricochet + 0.5f)) : 0;
            var targeting = owner != null ? owner.GetComponent<PlayerHomingLock>() : null;
            homingTarget = targeting != null ? targeting.LockedTarget : null;
            homingLocalAim = targeting != null ? targeting.LocalAim : Vector3.zero;
            hitTargets.Clear();
            hitStructures.Clear();
            passages.Clear(); passagesRecorded = false;
            ignoredColliders.Clear();

            if (drawRenderer == null) drawRenderer = GetComponentInChildren<Renderer>(true);
            halfExtents = Vector3.one * 0.001f;
            if (drawRenderer != null)
            {
                Vector3 extents = drawRenderer.localBounds.extents;
                Quaternion inverse = Quaternion.Inverse(RotationForDirection(direction));
                Vector3 x = inverse * drawRenderer.transform.TransformVector(Vector3.right * extents.x);
                Vector3 y = inverse * drawRenderer.transform.TransformVector(Vector3.up * extents.y);
                Vector3 z = inverse * drawRenderer.transform.TransformVector(Vector3.forward * extents.z);
                halfExtents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                    Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                    Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            }
            halfExtents = Vector3.Max(halfExtents, Vector3.one * 0.0001f);
            SetDirection(direction);
            if (isActiveAndEnabled) activeProjectiles.Add(this);
        }

        private void OnEnable()
        {
            if (source != null) activeProjectiles.Add(this);
        }

        internal void DetonateFromExplosion()
            => DetonateFromExplosion(transform.position, velocity.normalized);

        internal void DetonateFromExplosion(Vector3 point, Vector3 direction)
        {
            if (terminal || !isActiveAndEnabled) return;
            terminal = true;
            exploded = true;
            if (effects != null)
                effects.ApplyStoppedArea(point, direction, null, ignoredOwner, ignoredWeapon);
            PoolManager.Release(gameObject);
        }

        private void Update()
        {
            if (source == null || velocity.sqrMagnitude <= 0.0001f)
            {
                Terminate();
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;
            lifetime += deltaTime;
            if (lifetime >= MaximumLifetime)
            {
                Terminate();
                return;
            }

            UpdateIgnoredColliders();
            ApplyHoming(deltaTime);
            // 이번 업데이트의 예외 판정에서 쓸 뿌리를 다시 잡는다.
            ignoreRootsCached = false;

            float distance = Mathf.Min(velocity.magnitude * deltaTime,
                source.MaximumTravelDistance - traveledDistance);
            if (distance <= 0f)
            {
                Terminate();
                return;
            }

            Travel(distance);
            if (!terminal && traveledDistance >= source.MaximumTravelDistance) Terminate();
        }

        private void ApplyHoming(float deltaTime)
        {
            if (effects == null || effects.Stats.homing <= 0f) return;
            if (!PlayerHomingLock.IsAlive(homingTarget)) { homingTarget = null; return; }
            Vector3 offset = PlayerHomingLock.GetAimPoint(homingTarget, homingLocalAim) - transform.position;
            float distance = offset.magnitude;
            if (distance <= 0.001f) return;
            float turn = source.Tuning.homingTurnRatePerPoint * effects.Stats.homing * Mathf.Deg2Rad * deltaTime;
            Vector3 direction = Vector3.RotateTowards(velocity.normalized, offset / distance, turn, 0f);
            float speed = Mathf.Min(velocity.magnitude + source.Tuning.homingAccelerationPerPoint * effects.Stats.homing * deltaTime,
                launchSpeed * source.Tuning.homingMaxSpeedMultiplier);
            velocity = direction * speed;
        }

        private void Travel(float distance)
        {
            float remaining = distance;
            int collisionCount = 0;
            while (remaining > 0.0001f && collisionCount < 128)
            {
                Vector3 direction = velocity.normalized;
                Quaternion rotation = RotationForDirection(direction);
                if (!TryFindHit(direction, rotation, remaining, out CombatPhysicsQueries.Hit hit))
                {
                    CarvePassedStructures(transform.position, rotation, remaining);
                    transform.position += direction * remaining;
                    traveledDistance += remaining;
                    return;
                }

                collisionCount++;
                float advanced = Mathf.Clamp(hit.distance, 0f, remaining);
                CarvePassedStructures(transform.position, rotation, advanced);
                transform.position += direction * advanced;
                traveledDistance += advanced;
                remaining -= advanced;
                HandleImpact(hit, direction, ref remaining);
                if (terminal || this == null || !gameObject.activeInHierarchy) return;
            }

            // Stop this frame at the collision budget instead of skipping unchecked obstacles.
        }

        // BoxCast 후보 판정(거리·예외·소속)을 후보당 한 번만 계산해 모아 두는 버퍼.
        // 종전의 두 번째 순회는 같은 후보의 예외·소속을 다시 물었으므로 버퍼 한 개로 대신한다.
        private struct CastCandidate
        {
            public Collider collider;
            public float distance;
            public DestructibleTarget structure;
            public bool ignored;
        }
        private CastCandidate[] castCandidates = new CastCandidate[64];

        private System.Predicate<Collider> skipPassedStructure;
        private System.Func<Collider, DestructibleTarget> resolveStructure;
        private bool TryFindHit(Vector3 direction, Quaternion rotation, float distance, out CombatPhysicsQueries.Hit nearestHit)
        {
            skipPassedStructure ??= HasPassedStructure;
            resolveStructure ??= FindStructure;
            nearestHit = default;
            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            int layers = (source.CollisionLayers | ProjectileExtraCollisionLayers) & ~GunBlockLayers & ~UnusedCastLayers;
            int count;
            CombatPhysicsQueries.Hit[] hits;
            using (CollisionQueryMarker.Auto())
                count = CombatPhysicsQueries.BoxCast(transform.position, halfExtents, direction, rotation, distance, layers, out hits, skipPassedStructure, resolveStructure);
            if (castCandidates.Length < count) castCandidates = new CastCandidate[Mathf.NextPowerOfTwo(count)];
            // 가장 싼 거리 비교를 앞세워 예외·소속 조회를 줄이되, 판정 결과는 후보당 한 번만 낸다.
            for (int i = 0; i < count; i++)
            {
                CombatPhysicsQueries.Hit hit = hits[i];
                Collider collider = hit.collider;
                castCandidates[i].collider = collider;
                castCandidates[i].distance = hit.distance;
                castCandidates[i].ignored = IsIgnoredBody(collider);
                castCandidates[i].structure = FindStructure(collider);
            }
            int nearestIndex = -1;
            for (int i = 0; i < count; i++)
            {
                ref CastCandidate candidate = ref castCandidates[i];
                if (candidate.distance >= nearestDistance || candidate.ignored || !PassesEndChestGate(candidate.collider))
                    continue;
                nearestHit = new CombatPhysicsQueries.Hit
                {
                    collider = candidate.collider, distance = candidate.distance,
                    point = hits[i].point, normal = hits[i].normal
                };
                nearestDistance = candidate.distance;
                nearestIndex = i;
                found = true;
            }
            structureSweepLimit = 0f;
            if (found)
            {
                var hitStructure = castCandidates[nearestIndex].structure;
                if (hitStructure != null)
                {
                    structureSweepLimit = Mathf.Max(0f, distance - nearestDistance);
                    // Reuse the buffered judgments: never clear past the first foreign obstacle.
                    for (int i = 0; i < count; i++)
                    {
                        ref CastCandidate candidate = ref castCandidates[i];
                        if (candidate.ignored || candidate.structure == hitStructure) continue;
                        structureSweepLimit = Mathf.Min(structureSweepLimit,
                            Mathf.Max(0f, candidate.distance - nearestDistance));
                    }
                }
            }
            return found;
        }

        // 탄 자체·쏘는 주체·무기, 그리고 관통·도탄으로 이미 지나친 콜라이더.
        private bool IsIgnoredBody(Collider collider)
        {
            if (!ignoreRootsCached)
            {
                selfRoot = transform.root;
                ownerRoot = ignoredOwner != null ? ignoredOwner.root : null;
                weaponRoot = ignoredWeapon != null ? ignoredWeapon.root : null;
                ignoreRootsCached = true;
            }
            Transform hitTransform = collider.transform;
            // 예외 대상과 뿌리가 다르면 자손일 수 없다. 흔한 벽 히트는 루트 비교만으로 세 번의 부모 탐색을 건너뛴다.
            Transform root = hitTransform.root;
            if (!ReferenceEquals(root, selfRoot) &&
                (ownerRoot == null || !ReferenceEquals(root, ownerRoot)) &&
                (weaponRoot == null || !ReferenceEquals(root, weaponRoot)))
                return ignoredColliders.Count > 0 && ignoredColliders.Contains(collider);
            // 같은 뿌리 안에서만 자손 판정이 의미가 있다.
            if (hitTransform.IsChildOf(transform)) return true;
            if (ignoredOwner != null && hitTransform.IsChildOf(ignoredOwner)) return true;
            if (ignoredWeapon != null && hitTransform.IsChildOf(ignoredWeapon)) return true;
            // 예외 목록이 비어 있는 경우가 대부분이라 해시 조회조차 건너뛴다.
            return ignoredColliders.Count > 0 && ignoredColliders.Contains(collider);
        }

        // 엔드 상자: 코어는 중력·유도 조건을 갖춘 탄만 맞고, 방어막은 조건을 못 채운 탄만 막는다. 나머지는 통과한다.
        private bool PassesEndChestGate(Collider collider)
        {
            ResolveEndChest(collider, out var endChestCore, out var shield);
            if (endChestCore != null) return effects != null && endChestCore.AcceptsHit(effects.Stats);
            return shield == null || effects == null || shield.Deflects(effects.Stats);
        }

        private void HandleImpact(CombatPhysicsQueries.Hit hit, Vector3 incomingDirection, ref float remaining)
        {
            ResolveEndChest(hit.collider, out var endChestCore, out var endChestShield);
            if (endChestShield != null)
            {
                ShowImpact(hit.point, hit.normal, incomingDirection);
                velocity = Vector3.Reflect(velocity, hit.normal);
                transform.position += hit.normal * 0.05f;
                transform.rotation = RotationForDirection(velocity.normalized);
                remaining = Mathf.Max(0f, remaining - 0.05f);
                return;
            }
            if (endChestCore != null) endChestCore.NotifyHit(effects != null ? effects.Stats : default);
            var slotMachine = GetComponentInParents<SlotMachineReels>(hit.collider);
            if (slotMachine != null && effects != null)
                slotMachine.ReceiveAttack(effects.Stats.penetration, effects.Stats.explosion, false);
            bool water = effects != null && effects.IsWater;
            DestructibleTarget structure = FindStructure(hit.collider);
            if (structure != null && TutorialManager.Instance != null) TutorialManager.Instance.NotifyTargetHit(structure);
            TargetHealth target = GetComponentInParents<TargetHealth>(hit.collider);
            bool firstHitOnTarget = target != null && !hitTargets.Contains(target);
            bool firstHitOnStructure = structure != null && hitStructures.Add(structure);
            // 명중률 지표: 탄 하나당 처음 맞은 순간만 센다.
            if (!hitCounted && (structure != null || target != null))
            {
                hitCounted = true;
                GameLogger.Count("AttackHit");
            }
            // Internal voxel crossings do not need their own particle systems.
            if (structure != null ? firstHitOnStructure : target != null ? firstHitOnTarget : true)
                ShowImpact(hit.point, hit.normal, incomingDirection);
            if (effects != null && (target == null || firstHitOnTarget) &&
                (structure == null || firstHitOnStructure))
            {
                if (structure == null && target != null) hitTargets.Add(target);
                effects.ApplyHit(hit.point, incomingDirection, structure == null ? target : null, ignoredOwner, ignoredWeapon, false);
            }
            if (structure != null)
            {
                // Evaluate incoming power at this wall. A bullet reduced to 1 leaves the previous
                // block, then stops when it reaches this next wall.
                if (penetrationPower <= 1f)
                {
                    if (!water && (effects == null || structure.AllowsImpact(effects.Stats)))
                    {
                        var lootCrate = structure.GetComponent<SurvivalLootCrate>();
                        if (lootCrate != null && lootCrate.isActiveAndEnabled && !lootCrate.IsClaimed)
                        {
                            Quaternion crateRotation = RotationForDirection(incomingDirection);
                            float crateLineDistance = Mathf.Min(structure.PenetrationStepDistance, Mathf.Min(remaining, structureSweepLimit));
                            lootCrate.TakeBasicBulletHit(transform.position, crateRotation, halfExtents, crateLineDistance);
                        }
                    }
                    StopAtImpact(hit.point, incomingDirection, target);
                    return;
                }
                Quaternion rotation = RotationForDirection(incomingDirection);
                bool passed = false;
                bool penetratedMaterial = false;
                bool hitBlock = false;
                if (effects != null) structure.BreakAttributeImpact(effects.Stats, transform.position + incomingDirection * 0.001f, rotation, halfExtents);
                float lineDistance = Mathf.Min(structure.PenetrationStepDistance, Mathf.Min(remaining, structureSweepLimit));
                if (!water && (effects == null || structure.AllowsImpact(effects.Stats)))
                {
                    penetrationPower = structure.PenetrateAlongLine(penetrationPower,
                        transform.position, rotation, halfExtents, lineDistance,
                        out passed, out float resistanceBeforeHit, out penetratedMaterial,
                        allowResistanceReduction: !passages.Contains(structure));
                    hitBlock = penetratedMaterial || structure.PenetrationResistance < resistanceBeforeHit;
                    bool blockedByResistance = !passed && resistanceBeforeHit > penetrationPower;
                    if (hitBlock) penetrationPower = Mathf.Max(0f, penetrationPower - 1f);
                    if (blockedByResistance)
                    {
                        // Stronger armor consumes the bullet at this impact, before
                        // ricochet or any passage/health-target handling can run.
                        StopAtImpact(hit.point, incomingDirection, target);
                        return;
                    }
                    if (passed && hitBlock)
                    {
                        passagesRecorded = true;
                        passages.Record(structure, penetratedMaterial, transform.position, rotation, lineDistance);
                    }
                }
                if (passed && target != null && firstHitOnTarget && penetratedMaterial && effects != null)
                {
                    hitTargets.Add(target);
                    effects.ApplyHit(hit.point, incomingDirection, target, ignoredOwner, ignoredWeapon, false);
                }
                if (passed)
                {
                    if (penetratedMaterial && effects != null)
                        effects.ApplyPenetrationExplosion(hit.point, incomingDirection, ignoredOwner, ignoredWeapon);
                    float advance = Mathf.Min(0.001f, Mathf.Min(remaining, structureSweepLimit));
                    CarvePassedStructures(transform.position, rotation, advance);
                    transform.position += incomingDirection * advance;
                    traveledDistance += advance;
                    remaining -= advance;
                    return;
                }
            }
            // 손님은 한 발에 죽음 (예전 EnergyCube 동작). 물총은 살상력 없음.
            Customer customer = GetComponentInParents<Customer>(hit.collider);
            if (customer != null && (effects == null || !effects.IsWater)) customer.Kill();

            bool canPenetrate = structure == null && target != null && penetrationPower > 1f &&
                                (!water || !source.Tuning.waterStopsPenetration);
            if (canPenetrate)
            {
                if (firstHitOnTarget)
                {
                    penetrationPower = Mathf.Max(0f, penetrationPower - 1f);
                    if (effects != null)
                        effects.ApplyPenetrationExplosion(hit.point, incomingDirection, ignoredOwner, ignoredWeapon);
                }
                AddTargetCollidersToIgnore(target);
                CarvePassedStructures(transform.position, RotationForDirection(incomingDirection), Mathf.Min(0.002f, remaining));
                transform.position += incomingDirection * 0.002f;
                traveledDistance += 0.002f;
                remaining = Mathf.Max(0f, remaining - 0.002f);
                return;
            }

            if (target != null && penetrationPower <= 1f)
            {
                StopAtImpact(hit.point, incomingDirection, target);
                return;
            }
            bool canRicochet = ricochetsRemaining > 0 && hit.normal.sqrMagnitude > 0.0001f;
            if (canRicochet)
            {
                ricochetsRemaining--;
                velocity = Vector3.Reflect(velocity, hit.normal).normalized * velocity.magnitude;
                ignoredColliders.Add(hit.collider);
                transform.position += hit.normal * 0.002f;
                traveledDistance += 0.002f;
                remaining = Mathf.Max(0f, remaining - 0.002f);
                transform.rotation = RotationForDirection(velocity.normalized);
                return;
            }

            StopAtImpact(hit.point, incomingDirection, target);
        }

        // A terminal wall impact triggers area effects once, without ricocheting at low power.
        private void StopAtImpact(Vector3 point, Vector3 direction, TargetHealth target)
        {
            if (!exploded && effects != null)
            {
                exploded = true;
                effects.ApplyStoppedArea(point, direction, target, ignoredOwner, ignoredWeapon);
            }
            Terminate();
        }

        private void AddTargetCollidersToIgnore(TargetHealth target)
        {
            target.GetComponentsInChildren(true, targetColliderBuffer);
            foreach (Collider collider in targetColliderBuffer)
                if (collider != null) ignoredColliders.Add(collider);
            targetColliderBuffer.Clear();
        }

        private bool HasPassedStructure(Collider collider)
        {
            // 관통 구간이 하나도 없으면 통과한 구조물은 없다. 표적 조회를 하지 않는다.
            if (!passagesRecorded) return false;
            return passages.Ignore(FindStructure(collider));
        }

        private void CarvePassedStructures(Vector3 start, Quaternion rotation, float distance)
        {
            passages.Carve(start, rotation, halfExtents, distance);
        }

        // 콜라이더→표적 매핑은 공용 인덱스가 게임 전체에서 한 번만 부모를 타고 찾는다.
        private static DestructibleTarget FindStructure(Collider collider) => ColliderTargetIndex.Structure(collider);

        private void UpdateIgnoredColliders()
        {
            staleIgnoredColliders.Clear();
            foreach (Collider collider in ignoredColliders)
                if (collider == null || !collider.bounds.Contains(transform.position)) staleIgnoredColliders.Add(collider);
            for (int i = 0; i < staleIgnoredColliders.Count; i++) ignoredColliders.Remove(staleIgnoredColliders[i]);
            staleIgnoredColliders.Clear();
        }

        private void SetDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f) return;
            direction.Normalize();
            velocity = direction * launchSpeed;
            transform.rotation = RotationForDirection(direction);
        }

        private void Terminate()
        {
            if (terminal) return;
            terminal = true;
            PoolManager.Release(gameObject);
        }

        private static Quaternion RotationForDirection(Vector3 direction)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f
                ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(direction, up);
        }

        private void ShowImpact(Vector3 point, Vector3 normal, Vector3 direction)
        {
            using (ImpactEffectMarker.Auto())
                ShowImpactEffect(point, normal, direction);
        }

        private void ShowImpactEffect(Vector3 point, Vector3 normal, Vector3 direction)
        {
            if (impactEffect == null) return;
            if (normal.sqrMagnitude < 0.0001f) normal = -direction;
            GameObject effect = PoolManager.RentPrefab(impactEffect.gameObject, point + normal * 0.01f,
                RotationForDirection(normal));
            if (!effect.TryGetComponent(out PooledCombatObject pooledEffect))
            {
                PoolManager.Release(effect);
                return;
            }
            foreach (ParticleSystem particles in pooledEffect.Particles)
            {
                var main = particles.main;
                main.startColor = Color.white;
                var lifetimeColor = particles.colorOverLifetime;
                lifetimeColor.enabled = false;
                var speedColor = particles.colorBySpeed;
                speedColor.enabled = false;
                var trails = particles.trails;
                trails.colorOverLifetime = Color.white;
                trails.colorOverTrail = Color.white;
            }
            // Impact debris matches the shot body: color-module color first, then water color.
            MaterialPropertyBlock debrisTint = null;
            bool tinted = effects != null && effects.IsWater && source != null;
            if (tinted)
            {
                Color debrisColor = source.Tuning.waterColor;
                if (debrisTint == null) debrisTint = new MaterialPropertyBlock();
                debrisTint.Clear();
                debrisTint.SetColor("_BaseColor", debrisColor);
                debrisTint.SetColor("_Color", debrisColor);
            }
            if (drawRenderer != null && drawRenderer.sharedMaterial != null)
                foreach (ParticleSystemRenderer renderer in pooledEffect.ParticleRenderers)
                {
                    renderer.sharedMaterial = drawRenderer.sharedMaterial;
                    renderer.trailMaterial = drawRenderer.sharedMaterial;
                    if (debrisTint != null) renderer.SetPropertyBlock(debrisTint);
                }
            pooledEffect.PlayParticles(true);
        }

        private void OnDisable()
        {
            activeProjectiles.Remove(this);
            terminal = true; velocity = Vector3.zero; source = null; effects = null;
            ignoredOwner = ignoredWeapon = null; homingTarget = null;
            selfRoot = ownerRoot = weaponRoot = null; ignoreRootsCached = false;
            hitTargets.Clear(); hitStructures.Clear(); ignoredColliders.Clear();
            passages.Clear(); passagesRecorded = false;
            staleIgnoredColliders.Clear(); targetColliderBuffer.Clear();
        }

        private void OnValidate()
        {
            if (drawRenderer == null) drawRenderer = GetComponentInChildren<Renderer>(true);
        }
    }
}
