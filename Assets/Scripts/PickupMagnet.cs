using System;
using UnityEngine;

namespace WeaponWorkshop
{
    // Collider-free pickup motion: ballistic fall with a downward ground ray, then a periodic
    // sphere check for the player. Replaces per-step trigger callbacks against every nearby collider.
    [Serializable]
    public sealed class PickupMagnet
    {
        [SerializeField, Min(0.1f), Tooltip("플레이어 감지 반경(월드 미터).")]
        private float attractionRadius = 3f;
        [SerializeField, Min(0.1f)] private float attractionSpeed = 8f;
        [SerializeField, Min(0.1f)] private float acceleration = 20f;
        [SerializeField, Min(0.01f)] private float collectDistance = 0.2f;
        [SerializeField, Min(0f), Tooltip("바닥 접촉 시 아이템 중심이 바닥 위에 놓이는 높이.")]
        private float restHeight = 0.09f;
        [SerializeField, Min(0.01f), Tooltip("구형 범위 감지와 바닥 재확인 주기(초).")]
        private float scanInterval = 0.1f;

        private int groundMask;
        private Rigidbody body;
        private FirstPersonPlayer target;
        private Vector3 velocity;
        private bool grounded;
        private float speed;
        private float nextScan;

        public void Initialize(Rigidbody source)
        {
            body = source;
            target = null;
            speed = 0f;
            velocity = Vector3.zero;
            grounded = false;
            nextScan = 0f;
            groundMask = ~LayerMask.GetMask("Ignore Raycast", "Weapon", "Energy");
            // No collisions: the ground ray decides where the pickup rests.
            body.useGravity = false;
            body.isKinematic = true;
            foreach (Collider collider in body.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }

        public void Launch(Vector3 initialVelocity)
        {
            velocity = initialVelocity;
            grounded = false;
        }

        // Call from FixedUpdate. Returns the player once the pickup has reached them.
        public FirstPersonPlayer Tick(Func<FirstPersonPlayer, bool> canCollect)
        {
            if (target == null)
            {
                Fall();
                Scan(canCollect);
                return null;
            }
            if (!target.isActiveAndEnabled) return null;
            var controller = target.GetComponent<CharacterController>();
            Vector3 destination = controller != null
                ? target.transform.TransformPoint(controller.center) : target.transform.position + Vector3.up;
            speed = Mathf.MoveTowards(speed, attractionSpeed, acceleration * Time.fixedDeltaTime);
            Vector3 next = Vector3.MoveTowards(body.position, destination, speed * Time.fixedDeltaTime);
            body.MovePosition(next);
            return (next - destination).sqrMagnitude <= collectDistance * collectDistance ? target : null;
        }

        private void Fall()
        {
            if (grounded) return;
            float dt = Time.fixedDeltaTime;
            velocity += Physics.gravity * dt;
            Vector3 position = body.position;
            Vector3 step = velocity * dt;
            if (velocity.y < 0f)
            {
                // Ray from the pickup centre; the hit point plus rest height is where it lands.
                float reach = -step.y + restHeight;
                if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, reach, groundMask,
                        QueryTriggerInteraction.Ignore))
                {
                    body.MovePosition(new Vector3(position.x + step.x, hit.point.y + restHeight, position.z + step.z));
                    velocity = Vector3.zero;
                    grounded = true;
                    return;
                }
            }
            body.MovePosition(position + step);
        }

        private void Scan(Func<FirstPersonPlayer, bool> canCollect)
        {
            if (Time.time < nextScan) return;
            nextScan = Time.time + scanInterval;
            Vector3 position = body.position;
            // The floor may have been destroyed under a resting pickup.
            if (grounded && !Physics.Raycast(position, Vector3.down, restHeight + 0.1f, groundMask,
                    QueryTriggerInteraction.Ignore))
                grounded = false;
            // Sphere test against the players themselves; the attraction radius can cover hundreds of
            // scene colliders, so a physics overlap would fill any fixed buffer before reaching the player.
            RefreshPlayers();
            float radiusSquared = attractionRadius * attractionRadius;
            foreach (FirstPersonPlayer player in players)
            {
                if (player == null || !player.isActiveAndEnabled) continue;
                var controller = player.GetComponent<CharacterController>();
                Vector3 center = controller != null
                    ? player.transform.TransformPoint(controller.center) : player.transform.position + Vector3.up;
                if ((center - position).sqrMagnitude > radiusSquared || !canCollect(player)) continue;
                target = player;
                velocity = Vector3.zero;
                speed = 0f;
                return;
            }
        }

        private static FirstPersonPlayer[] players = new FirstPersonPlayer[0];
        private static float playersRefreshAt;
        private static void RefreshPlayers()
        {
            if (Time.time < playersRefreshAt) return;
            playersRefreshAt = Time.time + 1f;
            players = UnityEngine.Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsSortMode.None);
        }
    }
}
