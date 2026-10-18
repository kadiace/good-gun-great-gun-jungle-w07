using UnityEngine;

namespace WeaponWorkshop
{
    // Snapshot of the weapon at firing time; each split projectile receives full stats.
    public sealed class EnergyCube : MonoBehaviour
    {
        private const float MaximumTravelDistance = 1000f;
        private const float BaseAlpha = 0.7f;
        [SerializeField] private Renderer drawRenderer;
        [SerializeField] private ParticleSystem impactEffect;
        [SerializeField] private LayerMask collisionLayers = Physics.DefaultRaycastLayers;
        private Vector3 direction;
        private float speed;
        private float traveledDistance;
        private Transform ignoredOwner;
        private Transform ignoredWeapon;
        private Vector3 halfExtents;
        private Quaternion shotRotation;
        private float penetration, explosion;
        private bool exploded;
        private readonly PenetrationPassages passages = new PenetrationPassages();

        public void Initialize(Vector3 travelDirection, float movementSpeed, Transform owner, Transform weapon,
            WeaponStats stats = default)
        {
            direction = travelDirection.normalized;
            speed = Mathf.Max(0f, movementSpeed);
            penetration = Mathf.Max(0f, stats.penetration);
            passages.Clear();
            explosion = Mathf.Max(0f, stats.explosion);
            exploded = false;
            traveledDistance = 0f;
            ignoredOwner = owner;
            ignoredWeapon = weapon;
            shotRotation = Quaternion.LookRotation(direction,
                Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up);
            transform.rotation = shotRotation;
            if (drawRenderer == null) drawRenderer = GetComponentInChildren<Renderer>(true);
            halfExtents = Vector3.one * 0.001f;
            if (drawRenderer == null) return;
            Bounds bounds = drawRenderer.localBounds;
            Quaternion inverse = Quaternion.Inverse(shotRotation);
            Vector3 x = inverse * drawRenderer.transform.TransformVector(Vector3.right * bounds.extents.x);
            Vector3 y = inverse * drawRenderer.transform.TransformVector(Vector3.up * bounds.extents.y);
            Vector3 z = inverse * drawRenderer.transform.TransformVector(Vector3.forward * bounds.extents.z);
            halfExtents = Vector3.Max(new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)), Vector3.one * 0.0001f);
            if (drawRenderer.sharedMaterial == null) return;
            var appearance = new MaterialPropertyBlock();
            drawRenderer.GetPropertyBlock(appearance);
            Color color = drawRenderer.sharedMaterial.GetColor("_BaseColor");
            color.a = BaseAlpha;
            appearance.SetColor("_BaseColor", color);
            appearance.SetColor("_Color", color);
            drawRenderer.SetPropertyBlock(appearance);
        }

        private void Update()
        {
            if (speed <= 0f) { PoolManager.Release(gameObject); return; }
            float distance = Mathf.Min(speed * Time.deltaTime, MaximumTravelDistance - traveledDistance);
            if (distance <= 0f) return;
            for (int step = 0; step < 128 && distance > 0f; step++)
            {
            float nearest = distance;
            bool hitObstacle = false;
            CombatPhysicsQueries.Hit nearestHit = default;
            int layers = (collisionLayers | LayerMask.GetMask("Weapon")) & ~LayerMask.GetMask("Energy");
            int hitCount = CombatPhysicsQueries.BoxCast(transform.position, halfExtents, direction,
                shotRotation, distance, layers, out var hits);
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                var hit = hits[hitIndex];
                Transform hitTransform = hit.collider.transform;
                var hitTarget = hit.collider.GetComponentInParent<DestructibleTarget>();
                if (hitTransform.IsChildOf(transform) ||
                    passages.Ignore(hitTarget) ||
                    (ignoredOwner != null && hitTransform.IsChildOf(ignoredOwner)) ||
                    (ignoredWeapon != null && hitTransform.IsChildOf(ignoredWeapon)) || hit.distance > nearest) continue;
                nearest = hit.distance;
                nearestHit = hit;
                hitObstacle = true;
            }
            CarvePassedTargets(nearest);
            transform.position += direction * nearest;
            traveledDistance += nearest;
            distance -= nearest;
            if (hitObstacle)
            {
                ShowImpact(nearestHit.point, nearestHit.normal);
                DestructibleTarget target = nearestHit.collider.GetComponentInParent<DestructibleTarget>();
                if (target != null && TutorialManager.Instance != null) TutorialManager.Instance.NotifyTargetHit(target);
                if (target != null)
                {
                    penetration = target.PenetrateAlongLine(penetration, transform.position + direction * 0.001f,
                        shotRotation, halfExtents, 0f, out bool passed, out float resistanceBeforeHit,
                        out bool destroyedBlock, allowResistanceReduction: !passages.Contains(target));
                    bool hitBlock = destroyedBlock || target.PenetrationResistance < resistanceBeforeHit;
                    if (passed && hitBlock)
                    {
                        passages.Record(target, destroyedBlock, transform.position + direction * 0.001f, shotRotation, 0f);
                        penetration = Mathf.Max(0f, penetration - 1f);
                    }
                    if (!exploded && explosion > 0f)
                    {
                        exploded = true;
                        target.Explode(explosion, nearestHit.point, nearestHit.normal, shotRotation, halfExtents);
                    }
                    if (passed)
                    {
                        float advance = Mathf.Min(0.001f, distance);
                        CarvePassedTargets(advance);
                        transform.position += direction * advance;
                        traveledDistance += advance;
                        distance -= advance;
                        continue;
                    }
                }
                Customer customer = nearestHit.collider.GetComponentInParent<Customer>();
                if (customer != null) customer.Kill();
            }
            if (hitObstacle) { PoolManager.Release(gameObject); return; }
            break;
            }
            if (traveledDistance >= MaximumTravelDistance) PoolManager.Release(gameObject);
        }

        private void CarvePassedTargets(float distance)
        {
            passages.Carve(transform.position, shotRotation, halfExtents, distance);
        }

        private void ShowImpact(Vector3 point, Vector3 normal)
        {
            if (impactEffect == null) return;
            if (normal.sqrMagnitude < 0.0001f) normal = -direction;
            GameObject effect = PoolManager.RentPrefab(impactEffect.gameObject, point + normal * 0.01f,
                Quaternion.LookRotation(normal, Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up));
            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
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
            if (drawRenderer != null && drawRenderer.sharedMaterial != null)
                foreach (ParticleSystemRenderer renderer in effect.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    renderer.sharedMaterial = drawRenderer.sharedMaterial;
                    renderer.trailMaterial = drawRenderer.sharedMaterial;
                }
            effect.GetComponent<PooledCombatObject>().PlayParticles(true);
        }
    }
}
