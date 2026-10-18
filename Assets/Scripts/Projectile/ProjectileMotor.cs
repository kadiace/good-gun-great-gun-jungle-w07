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
        private static int endChestLayer, endChestShieldLayer, projectileExtraCollisionLayers;
        private static int EndChestLayer { get { ResolveLayers(); return endChestLayer; } }
        private static int EndChestShieldLayer { get { ResolveLayers(); return endChestShieldLayer; } }
        private static int ProjectileExtraCollisionLayers { get { ResolveLayers(); return projectileExtraCollisionLayers; } }
        private static void ResolveLayers()
        {
            if (layersResolved) return;
            endChestLayer = LayerMask.NameToLayer("EndChest");
            endChestShieldLayer = LayerMask.NameToLayer("EndChestShield");
            projectileExtraCollisionLayers = LayerMask.GetMask("Weapon", "EndChest", "EndChestShield") & ~LayerMask.GetMask("Energy");
            layersResolved = true;
        }
        [SerializeField] private Renderer drawRenderer;
        [SerializeField] private ParticleSystem impactEffect;

        private readonly HashSet<TargetHealth> hitTargets = new HashSet<TargetHealth>();
        private readonly HashSet<DestructibleTarget> hitStructures = new HashSet<DestructibleTarget>();
        private readonly HashSet<Collider> ignoredColliders = new HashSet<Collider>();
        private readonly HashSet<Collider> _ignoredForLifetime = new HashSet<Collider>();
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
        public Transform WeaponSource => ignoredWeapon;
        internal Vector3 CurrentDirection => velocity.normalized;
        private Vector3 halfExtents;
        private int penetrationsRemaining;
        private float penetrationPower;
        private bool hitCounted;
        private bool exploded;
        private int ricochetsRemaining;
        private bool terminal;
        private int launchId;
        private float structureSweepLimit;
        internal int LaunchId => launchId;

        private static T GetComponentInParents<T>(Collider collider) where T : Component
        {
            for (Transform current = collider != null ? collider.transform : null; current != null; current = current.parent)
                if (current.TryGetComponent(out T component)) return component;
            return null;
        }
        private static bool HasLayerInParents(Collider collider, int layer)
        {
            for (Transform current = collider != null ? collider.transform : null; current != null; current = current.parent)
                if (current.gameObject.layer == layer) return true;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveProjectiles() => activeProjectiles.Clear();

        internal static void CopyActiveProjectiles(List<ProjectileMotor> destination)
        {
            destination.Clear();
            destination.AddRange(activeProjectiles);
        }

        /// <summary>
        /// Initializes a pooled projectile and resets its per-shot collision and redirection state.
        /// statsSource, direction, speed, owner, and weapon describe the newly fired shot.
        /// </summary>
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
            traveledDistance = 0f;
            lifetime = 0f;
            penetrationsRemaining = effects != null
                ? Mathf.Max(0, Mathf.FloorToInt(effects.Stats.penetration + 0.5f)) : 0; // charged values round half up
            ricochetsRemaining = effects != null
                ? Mathf.Max(0, Mathf.FloorToInt(effects.Stats.ricochet + 0.5f)) : 0;
            var targeting = owner != null ? owner.GetComponent<PlayerHomingLock>() : null;
            homingTarget = targeting != null ? targeting.LockedTarget : null;
            homingLocalAim = targeting != null ? targeting.LocalAim : Vector3.zero;
            hitTargets.Clear();
            hitStructures.Clear();
            ignoredColliders.Clear();
            _ignoredForLifetime.Clear();

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

        /// <summary>
        /// Applies the existing homing turn to the projectile.
        /// deltaTime is the elapsed time used by the homing turn calculation.
        /// </summary>
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
                if (!TryFindHit(direction, rotation, remaining, out RaycastHit hit))
                {
                    transform.position += direction * remaining;
                    traveledDistance += remaining;
                    return;
                }

                collisionCount++;
                float advanced = Mathf.Clamp(hit.distance, 0f, remaining);
                transform.position += direction * advanced;
                traveledDistance += advanced;
                remaining -= advanced;
                HandleImpact(hit, direction, ref remaining);
                if (terminal || this == null || !gameObject.activeInHierarchy) return;
            }

            // Stop this frame at the collision budget instead of skipping unchecked obstacles.
        }

        private bool TryFindHit(Vector3 direction, Quaternion rotation, float distance, out RaycastHit nearestHit)
        {
            nearestHit = default;
            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            int layers = source.CollisionLayers | ProjectileExtraCollisionLayers;
            int count;
            RaycastHit[] hits;
            using (CollisionQueryMarker.Auto())
                count = CombatPhysicsQueries.BoxCast(transform.position, halfExtents, direction, rotation, distance, layers, out hits);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                Transform hitTransform = hit.collider.transform;
                if (hitTransform.IsChildOf(transform) ||
                    (ignoredOwner != null && hitTransform.IsChildOf(ignoredOwner)) ||
                    (ignoredWeapon != null && hitTransform.IsChildOf(ignoredWeapon)) ||
                    ignoredColliders.Contains(hit.collider) || _ignoredForLifetime.Contains(hit.collider) || hit.distance >= nearestDistance ||
                    !PassesEndChestGate(hit.collider))
                    continue;
                nearestHit = hit;
                nearestDistance = hit.distance;
                found = true;
            }
            structureSweepLimit = 0f;
            if (found)
            {
                var hitStructure = GetComponentInParents<DestructibleTarget>(nearestHit.collider);
                if (hitStructure != null)
                {
                    structureSweepLimit = Mathf.Max(0f, distance - nearestHit.distance);
                    // Reuse the cast results: never clear past the first foreign obstacle.
                    for (int i = 0; i < count; i++)
                    {
                        var other = hits[i];
                        Transform t = other.collider.transform;
                        if (t.IsChildOf(transform) ||
                            (ignoredOwner != null && t.IsChildOf(ignoredOwner)) ||
                            (ignoredWeapon != null && t.IsChildOf(ignoredWeapon)) ||
                            ignoredColliders.Contains(other.collider) || _ignoredForLifetime.Contains(other.collider) ||
                            GetComponentInParents<DestructibleTarget>(other.collider) == hitStructure) continue;
                        structureSweepLimit = Mathf.Min(structureSweepLimit,
                            Mathf.Max(0f, other.distance - nearestHit.distance));
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// Filters EndChest colliders using the projectile's gravity threshold.
        /// collider is the candidate collision; false excludes it from this projectile's sweep.
        /// </summary>
        private bool PassesEndChestGate(Collider collider)
        {
            var endChestCore = HasLayerInParents(collider, EndChestLayer)
                ? GetComponentInParents<EndChestCore>(collider) : null;
            if (endChestCore != null) return effects != null && endChestCore.AcceptsHit(effects.Stats);
            var shield = HasLayerInParents(collider, EndChestShieldLayer)
                ? GetComponentInParents<EndChestShield>(collider) : null;
            return shield == null || effects == null || shield.Deflects(effects.Stats);
        }

        /// <summary>
        /// Applies the existing impact behavior or redirects a gravity-only EndChest projectile upward.
        /// hit is the detected collision and remaining is this frame's unused travel distance.
        /// </summary>
        private void HandleImpact(RaycastHit hit, Vector3 incomingDirection, ref float remaining)
        {
            if (HasLayerInParents(hit.collider, EndChestShieldLayer) && GetComponentInParents<EndChestShield>(hit.collider) != null)
            {
                GetComponentInParents<EndChestShield>(hit.collider).NotifyHit();
                ShowImpact(hit.point, hit.normal, incomingDirection);
                velocity = Vector3.Reflect(velocity, hit.normal);
                transform.position += hit.normal * 0.05f;
                transform.rotation = RotationForDirection(velocity.normalized);
                remaining = Mathf.Max(0f, remaining - 0.05f);
                return;
            }
            var endChestCore = HasLayerInParents(hit.collider, EndChestLayer)
                ? GetComponentInParents<EndChestCore>(hit.collider) : null;
            if (endChestCore != null)
            {
                ProjectileStats stats = effects != null ? effects.Stats : default;
                if (!endChestCore.QualifiesForDiscovery(stats))
                {
                    endChestCore.NotifyRedirectedHit();
                    _ignoredForLifetime.Add(hit.collider);
                    velocity = Vector3.up * velocity.magnitude;
                    transform.rotation = RotationForDirection(Vector3.up);
                    return;
                }
                endChestCore.NotifyHit();
            }
            bool water = effects != null && effects.IsWater;
            DestructibleTarget structure = GetComponentInParents<DestructibleTarget>(hit.collider);
            if (structure != null && TutorialManager.Instance != null) TutorialManager.Instance.NotifyTargetHit(structure);
            TargetHealth target = GetComponentInParents<TargetHealth>(hit.collider);
            bool firstHitOnTarget = target != null && hitTargets.Add(target);
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
                effects.ApplyHit(hit.point, incomingDirection, structure == null ? target : null, ignoredOwner, ignoredWeapon, false);
            if (structure != null)
            {
                Quaternion rotation = RotationForDirection(incomingDirection);
                bool passed = false;
                bool penetratedMaterial = false;
                if (effects != null) structure.BreakAttributeImpact(effects.Stats, transform.position + incomingDirection * 0.001f, rotation, halfExtents);
                float lineDistance = Mathf.Min(structure.PenetrationStepDistance, Mathf.Min(remaining, structureSweepLimit));
                if (!water && (effects == null || structure.AllowsImpact(effects.Stats)))
                    penetrationPower = structure.PenetrateAlongLine(penetrationPower,
                        transform.position, rotation, halfExtents, lineDistance,
                        out passed, out _, out penetratedMaterial);
                if (passed)
                {
                    if (target != null && firstHitOnTarget && penetratedMaterial && effects != null)
                        effects.ApplyHit(hit.point, incomingDirection, target, ignoredOwner, ignoredWeapon, false);
                    if (penetratedMaterial && effects != null)
                        effects.ApplyPenetrationExplosion(hit.point, incomingDirection, ignoredOwner, ignoredWeapon);
                    float advance = Mathf.Min(0.001f, Mathf.Min(remaining, structureSweepLimit));
                    transform.position += incomingDirection * advance;
                    traveledDistance += advance;
                    remaining -= advance;
                    return;
                }
            }
            // 손님은 한 발에 죽음 (예전 EnergyCube 동작). 물총은 살상력 없음.
            Customer customer = GetComponentInParents<Customer>(hit.collider);
            if (customer != null && (effects == null || !effects.IsWater)) customer.Kill();

            bool canPenetrate = structure == null && target != null && penetrationsRemaining > 0 &&
                                (!water || !source.Tuning.waterStopsPenetration);
            if (canPenetrate)
            {
                if (firstHitOnTarget)
                {
                    penetrationsRemaining--;
                    if (effects != null)
                        effects.ApplyPenetrationExplosion(hit.point, incomingDirection, ignoredOwner, ignoredWeapon);
                }
                AddTargetCollidersToIgnore(target);
                transform.position += incomingDirection * 0.002f;
                traveledDistance += 0.002f;
                remaining = Mathf.Max(0f, remaining - 0.002f);
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

            // Terminal collisions retain the full explosion/gravity payload.
            if (!exploded && effects != null)
            {
                exploded = true;
                effects.ApplyStoppedArea(hit.point, incomingDirection, target, ignoredOwner, ignoredWeapon);
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
            hitTargets.Clear(); hitStructures.Clear(); ignoredColliders.Clear(); _ignoredForLifetime.Clear();
            staleIgnoredColliders.Clear(); targetColliderBuffer.Clear();
        }

        private void OnValidate()
        {
            if (drawRenderer == null) drawRenderer = GetComponentInChildren<Renderer>(true);
        }
    }
}
