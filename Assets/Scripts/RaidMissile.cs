using UnityEngine;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(ProjectileStatsSource))]
    public sealed class RaidMissile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 18f;
        [SerializeField, Min(0f)] private float turnRate = 45f;
        [SerializeField, Min(0.1f)] private float lifetime = 8f;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.15f;
        [SerializeField, Min(0f)] private float explosionPower = 3f;
        [SerializeField, Min(0.01f), Tooltip("폭발력은 유지하고 실제 폭발 반경만 배율 적용.")]
        private float explosionRadiusMultiplier = 1f;
        [SerializeField, Tooltip("??? ?? ?? ??? ??. ?? ??? ??? ?? ??.")]
        private ParticleSystem explosionEffectOverride;
        [SerializeField, Min(0f)] private float damage = 20f;
        [SerializeField, Min(0f)] private float playerDamage = 1f;
        [SerializeField, Tooltip("켜면 폭심지에서 Player Damage, 가장자리에서 지정 비율까지 선형 감소.")]
        private bool playerDamageFalloff;
        [SerializeField, Range(0f, 1f)] private float playerEdgeDamageMultiplier = 0.2f;
        [SerializeField, Min(0f)] private float playerPush = 2f;
        private FirstPersonPlayer player;
        private Transform launcher;
        private string enemyId;
        private RaidEnemyType enemyType;
        private ProjectileStatsSource source;
        private float elapsed;
        private bool exploded;
        // Player health can subscribe here without treating the player as a disposable TargetHealth.
        public static event System.Action<FirstPersonPlayer, float> PlayerHit;
        private void Awake() { source = GetComponent<ProjectileStatsSource>(); source.SetBaseDamage(damage); }
        public void Initialize(FirstPersonPlayer target, Transform owner)
        {
            player = target; launcher = owner;
            var enemy = owner != null ? RaidEnemyCore.FindForPart(owner) : null;
            enemyId = enemy != null ? enemy.EnemyId : "";
            enemyType = enemy != null ? enemy.EnemyType : RaidEnemyType.None;
        }
        private void Update()
        {
            if (exploded || GameManager.GameplayBlocked) return;
            elapsed += Time.deltaTime;
            if (elapsed >= lifetime) { Destroy(gameObject); return; }
            if (player != null)
            {
                Vector3 aim = player.transform.position + Vector3.up;
                Vector3 direction = Vector3.RotateTowards(transform.forward, (aim-transform.position).normalized,
                    turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
                if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
            }
            float distance = speed * Time.deltaTime;
            RaycastHit closest = default; bool found = false;
            int count = RaidPhysicsQueries.SphereCast(transform.position, collisionRadius, transform.forward, distance, out var hits);
            for (int i = 0; i < count && !found; i++)
            {
                var hit = hits[i];
                if (hit.transform.IsChildOf(transform) || (launcher != null && hit.transform.IsChildOf(launcher)) ||
                    hit.collider.GetComponentInParent<RaidMissile>() != null) continue;
                closest = hit; found = true;
            }
            if (found) Explode(closest);
            else transform.position += transform.forward * distance;
        }
        private void Explode(RaycastHit hit)
        {
            exploded = true;
            float explosionRadius = source.Tuning.explosionRadiusPerPoint * Mathf.Pow(explosionPower, 1f / 3f) *
                Mathf.Max(0.01f, explosionRadiusMultiplier);
            HitPayload.Apply(new HitPayload.Context { stats = new WeaponStats { explosion = explosionPower },
                source = source, point = hit.point, direction = transform.forward,
                target = hit.collider.GetComponentInParent<TargetHealth>(), damage = damage,
                explosionRadiusOverride = explosionRadius, explosionEffectOverride = explosionEffectOverride,
                applyStatus = true, applyArea = true, owner = launcher, weapon = launcher });
            float playerDistance = player != null ? Vector3.Distance(player.transform.position + Vector3.up, hit.point) : float.PositiveInfinity;
            float playerHitRadius = explosionRadius + 0.5f;
            if (playerDamageFalloff && player != null)
            {
                // Surface distance makes a direct hit deal full damage even with a large projectile.
                var controller = player.GetComponent<CharacterController>();
                if (controller != null && controller.enabled)
                    playerDistance = Vector3.Distance(controller.ClosestPoint(hit.point), hit.point);
                playerHitRadius = explosionRadius;
            }
            if (player != null && playerDistance <= playerHitRadius)
            {
                float damageMultiplier = playerDamageFalloff ? Mathf.Lerp(1f, playerEdgeDamageMultiplier,
                    Mathf.Clamp01(playerDistance / Mathf.Max(0.001f, playerHitRadius))) : 1f;
                PlayerHit?.Invoke(player, damage * damageMultiplier);
                Vector3 push = player.transform.position - hit.point; push.y = 0f;
                var health = player.GetComponent<RaidPlayerHealth>();
                if (health != null)
                    health.ApplyHit(playerDamage * damageMultiplier, push.normalized * playerPush, hit.point - player.transform.position, enemyId, enemyType);
            }
            Destroy(gameObject);
        }
    }
}
