using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RaidTank : MonoBehaviour
    {
        [SerializeField, Tooltip("일반 탱크와 별도의 강화 탱크 출격 정원을 사용합니다.")]
        private bool reinforced;
        public bool IsReinforced => reinforced;
        [SerializeField] private RaidEnemyCore core;
        [SerializeField] private Transform turret;
        [SerializeField, Tooltip("포신 발사구. 포신 부위가 파괴되면 포격하지 않음.")]
        private RaidWeaponMount cannon;
        [SerializeField] private RaidMissile shellPrefab;
        [SerializeField, Min(0.1f)] private float fireInterval = 4f;
        [SerializeField, Min(1f)] private float firingRange = 20f;
        [SerializeField, Min(0f)] private float moveSpeed = 4f;
        [SerializeField, Min(0.1f)] private float stoppingDistance = 8f;
        [SerializeField, Min(1f)] private float bodyTurnSpeed = 60f;
        private float fireRemaining;
        [SerializeField, Min(0f)] private float turretTurnSpeed = 45f;
        private Rigidbody body;
        private FirstPersonPlayer player;
        private bool released, landed, destroyed;
        private bool movementSoundActive;
        private RaidTankCrusher crusher;
        private RaidTankStepper stepper;
        private RaidTankUnstuck unstuck;
        private RaidTankTransport transport;
        private bool landingSoundPlayed;
        private bool climbingStep;
        private float stepHeightLimit;
        private struct GroundContact
        {
            public Collider ownCollider;
            public Vector3 localPoint;
        }
        private readonly Dictionary<Collider, List<GroundContact>> supports = new();
        private readonly HashSet<Collider> refreshedSupports = new();
        private readonly List<Collider> staleSupports = new();
        private const float GroundProbeLift = 0.08f, GroundProbeDistance = 0.16f;
        public bool IsFalling => released && !landed && !destroyed && !climbingStep;
        public bool IsLanded => landed && !destroyed;
        private void Awake() { body = GetComponent<Rigidbody>(); crusher = GetComponent<RaidTankCrusher>(); stepper = GetComponent<RaidTankStepper>(); unstuck = GetComponent<RaidTankUnstuck>(); body.isKinematic = true; }
        private void OnEnable()
        {
            if (core != null) core.Destroyed += Stop;
            IgnoreEndChests();
            if (released && !destroyed && body != null) body.WakeUp();
        }
        // 엔드 상자와는 물리적으로 부딪히지 않는다.
        private void IgnoreEndChests()
        {
            var own = GetComponentsInChildren<Collider>(true);
            foreach (var chest in EndChest.Instances)
                foreach (var chestCollider in chest.GetComponentsInChildren<Collider>(true))
                    foreach (var ownCollider in own) Physics.IgnoreCollision(ownCollider, chestCollider, true);
        }
        private void OnDisable()
        {
            if (core != null) core.Destroyed -= Stop;
            supports.Clear(); landed = false; climbingStep = false;
            SetMovementSoundActive(false);
        }
        public void Initialize(FirstPersonPlayer target, string enemyId)
        {
            if (target == null || core == null || !core.isActiveAndEnabled ||
                !core.transform.IsChildOf(transform) || body == null || !isActiveAndEnabled)
                throw new System.InvalidOperationException($"RaidTank '{name}': active tank, child Core, Rigidbody and player are required.");
            core.AssignIdentity(enemyId, RaidEnemyType.Tank);
            player = target;
            fireRemaining = fireInterval;
        }
        internal void SetTransport(RaidTankTransport source) => transport = source;

        private void IgnoreTransportCollisions()
        {
            if (transport == null) return;
            var own = GetComponentsInChildren<Collider>(true);
            foreach (var other in transport.GetComponentsInChildren<Collider>(true))
            {
                if (other.transform.IsChildOf(transform)) continue;
                foreach (var collider in own)
                    Physics.IgnoreCollision(collider, other, true);
            }
        }
        public void Release()
        {
            if (destroyed || released) return;
            released = true; transform.SetParent(null, true);
            IgnoreEndChests(); // 투하 시점에 파츠 콜라이더가 확정된다.
            IgnoreTransportCollisions();
            supports.Clear(); landed = false; climbingStep = false;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            body.isKinematic = false; body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
        private bool CanSupport(Collider collider)
        {
            return collider != null && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger &&
                collider.attachedRigidbody != body && !collider.transform.IsChildOf(transform) &&
                collider.GetComponentInParent<RaidTankTransport>() == null &&
                (crusher == null || !crusher.CanCrush(collider));
        }
        private void OnCollisionEnter(Collision collision) => RecordSupport(collision);
        private void OnCollisionStay(Collision collision) => RecordSupport(collision);
        private void OnCollisionExit(Collision collision) => supports.Remove(collision.collider);

        private void RecordSupport(Collision collision)
        {
            if (!released || destroyed) return;
            refreshedSupports.Clear();
            bool grounded = false;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                var other = contact.otherCollider;
                if (!CanSupport(other)) continue;
                if (!supports.TryGetValue(other, out var points)) supports.Add(other, points = new List<GroundContact>(4));
                if (refreshedSupports.Add(other)) points.Clear();
                if (contact.normal.y <= 0.5f || contact.thisCollider == null || contact.thisCollider.attachedRigidbody != body) continue;
                points.Add(new GroundContact { ownCollider = contact.thisCollider, localPoint = transform.InverseTransformPoint(contact.point) });
                grounded = true;
                if (!landingSoundPlayed)
                {
                    landingSoundPlayed = true;
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    WorkshopSoundEffects.RaiseTankLanded(contact.point);
                }
            }
            if (grounded) landed = true;
        }

        // Keep sleeping bodies grounded without depending on OnCollisionStay. Probe only
        // previously contacted support surfaces, not the whole scene or a distant floor.
        private bool HasGroundSupport()
        {
            bool grounded = false;
            staleSupports.Clear();
            foreach (var pair in supports)
            {
                bool valid = false;
                if (CanSupport(pair.Key))
                    foreach (var contact in pair.Value)
                    {
                        var own = contact.ownCollider;
                        if (own == null || !own.enabled || own.isTrigger || !own.gameObject.activeInHierarchy ||
                            Physics.GetIgnoreCollision(own, pair.Key)) continue;
                        Vector3 point = transform.TransformPoint(contact.localPoint);
                        var bounds = own.bounds;
                        bounds.Expand(0.04f);
                        if (!bounds.Contains(point)) continue;
                        var ray = new Ray(point + Vector3.up * GroundProbeLift, Vector3.down);
                        if (!pair.Key.Raycast(ray, out var hit, GroundProbeDistance) || hit.normal.y <= 0.5f) continue;
                        var part = pair.Key.GetComponentInParent<DestructibleTarget>();
                        if (part != null && !part.HasCellAt(hit.point - hit.normal * 0.001f)) continue;
                        valid = true;
                        break;
                    }
                if (valid) grounded = true;
                else staleSupports.Add(pair.Key);
            }
            foreach (var collider in staleSupports) supports.Remove(collider);
            return grounded;
        }
        private void Stop()
        {
            destroyed = true;
            SetMovementSoundActive(false);
            body.isKinematic = true;
        }
        private void FixedUpdate()
        {
            if (released && transform.position.y <= -100f)
            {
                Destroy(gameObject);
                return;
            }
            if (released && !destroyed)
            {
                bool hadSupport = landed;
                landed = HasGroundSupport();
                if (hadSupport && !landed) body.WakeUp();
            }
            // Continue a step begun on the ground only while rising and a real
            // climbable obstacle remains ahead. This is not a latched grounded state.
            bool continuingStep = climbingStep && body.linearVelocity.y > 0f && !GameManager.GameplayBlocked;
            if ((!landed && !continuingStep) || destroyed || player == null)
            {
                climbingStep = false;
                if (!destroyed && unstuck != null) unstuck.Tick(false, Vector3.zero, 0f, moveSpeed);
                SetMovementSoundActive(false);
                return;
            }
            Vector3 direction = player.transform.position - body.position; direction.y = 0f;
            Vector3 movement = Vector3.zero;
            if (!GameManager.GameplayBlocked && direction.sqrMagnitude > 0.01f)
            {
                Quaternion facing = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction), bodyTurnSpeed * Time.fixedDeltaTime);
                body.MoveRotation(facing);
                float stop = Mathf.Min(stoppingDistance, Mathf.Max(0.1f, firingRange - 1f));
                if (direction.magnitude > stop && Vector3.Angle(facing * Vector3.forward, direction) < 30f)
                    movement = facing * Vector3.forward * moveSpeed;
            }
            // 이동 중 낮은 턱에 막히면(실제 수평 속도가 목표의 절반 미만) 올라설 상승 속도를 받는다.
            float climb = 0f;
            float horizontalSpeed = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;
            if (stepper != null && movement != Vector3.zero && horizontalSpeed < moveSpeed * 0.5f)
                climb = stepper.ClimbVelocity(movement.normalized, continuingStep ? stepHeightLimit : -1f);
            // 걸림이 이어지면 단계적으로 푼다(몸체 영역 부수기 → 임시 높은 턱 오르기 → 막는 콜라이더 잠시 통과).
            if (unstuck != null)
                climb = Mathf.Max(climb, unstuck.Tick(landed && movement != Vector3.zero, movement.normalized, horizontalSpeed, moveSpeed));
            climbingStep = climb > 0f && stepper != null;
            if (climbingStep) stepHeightLimit = stepper.LastClimbHeightLimit;
            // Keep gravity and physical collision responses; do not move through map geometry.
            float verticalSpeed = body.linearVelocity.y;
            if (climb > 0f) verticalSpeed = Mathf.Max(verticalSpeed, climb);
            body.linearVelocity = new Vector3(movement.x, verticalSpeed, movement.z);
            body.angularVelocity = Vector3.zero;
            SetMovementSoundActive(landed && movement != Vector3.zero && horizontalSpeed > 0.1f);
        }

        private void SetMovementSoundActive(bool active)
        {
            if (movementSoundActive == active) return;
            movementSoundActive = active;
            if (active) WorkshopSoundEffects.RaiseTankMovementStarted(transform);
            else WorkshopSoundEffects.RaiseTankMovementStopped(transform);
        }

        private void Update()
        {
            if (!landed || destroyed || player == null || turret == null || GameManager.GameplayBlocked) return;
            Vector3 direction = player.transform.position - turret.position; direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
                turret.rotation = Quaternion.RotateTowards(turret.rotation, Quaternion.LookRotation(direction), turretTurnSpeed * Time.deltaTime);
            fireRemaining -= Time.deltaTime;
            if (fireRemaining > 0f || cannon == null || shellPrefab == null ||
                direction.magnitude > firingRange || Vector3.Angle(turret.forward, direction) > 5f || !cannon.IsOperational) return;
            Vector3 aim = player.transform.position + Vector3.up;
            Transform muzzle = cannon.Muzzle;
            var shell = Instantiate(shellPrefab, muzzle.position, Quaternion.LookRotation((aim-muzzle.position).normalized));
            shell.Initialize(player, transform);
            WorkshopSoundEffects.RaiseTankAttacked(muzzle.position);
            fireRemaining = fireInterval;
        }
    }
}
