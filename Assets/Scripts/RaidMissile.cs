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
        [SerializeField, Min(0f)] private float damage = 20f;
        [SerializeField, Min(0f)] private float playerDamage = 1f;
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
            HitPayload.Apply(new HitPayload.Context { stats = new WeaponStats { explosion = explosionPower },
                source = source, point = hit.point, direction = transform.forward,
                target = hit.collider.GetComponentInParent<TargetHealth>(), damage = damage,
                applyStatus = true, applyArea = true, owner = launcher, weapon = launcher });
            if (player != null && Vector3.Distance(player.transform.position + Vector3.up, hit.point) <=
                source.Tuning.explosionRadiusPerPoint * Mathf.Pow(explosionPower, 1f/3f) + 0.5f)
            {
                PlayerHit?.Invoke(player, damage);
                Vector3 push = player.transform.position - hit.point; push.y = 0f;
                var health = player.GetComponent<RaidPlayerHealth>();
                if (health != null)
                    health.ApplyHit(playerDamage, push.normalized * playerPush, hit.point - player.transform.position, enemyId, enemyType);
            }
            Destroy(gameObject);
        }
    }
}
