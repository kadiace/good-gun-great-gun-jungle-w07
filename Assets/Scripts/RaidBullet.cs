using UnityEngine;

namespace WeaponWorkshop
{
    // Enemy machine-gun round: flies straight, hurts only the player, never breaks blocks.
    public sealed class RaidBullet : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 60f;
        [SerializeField, Min(0.1f)] private float lifetime = 2f;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.05f;
        [SerializeField, Min(0f)] private float playerDamage = 0.5f;
        [SerializeField, Min(0f)] private float playerPush = 0.3f;
        private Transform launcher;
        private float elapsed;

        // Rented from PoolManager, so every per-shot field is reset here.
        public void Initialize(Transform owner) { launcher = owner; elapsed = 0f; }

        private void Update()
        {
            if (GameManager.GameplayBlocked) return;
            elapsed += Time.deltaTime;
            if (elapsed >= lifetime) { PoolManager.Release(gameObject); return; }
            float distance = speed * Time.deltaTime;
            RaycastHit closest = default;
            bool found = false;
            int count = RaidPhysicsQueries.SphereCast(transform.position, collisionRadius, transform.forward, distance, out var hits);
            for (int i = 0; i < count && !found; i++)
            {
                var hit = hits[i];
                if (hit.transform.IsChildOf(transform) || (launcher != null && hit.transform.IsChildOf(launcher)) ||
                    hit.collider.GetComponentInParent<RaidBullet>() != null) continue;
                closest = hit; found = true;
            }
            if (!found) { transform.position += transform.forward * distance; return; }
            var health = closest.collider.GetComponentInParent<RaidPlayerHealth>();
            if (health != null)
            {
                Vector3 push = transform.forward; push.y = 0f;
                health.ApplyHit(playerDamage, push.normalized * playerPush, -transform.forward);
            }
            PoolManager.Release(gameObject);
        }
    }
}
