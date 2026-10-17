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
        public bool IsFalling => released && !landed && !destroyed;
        public bool IsLanded => landed && !destroyed;
        private void Awake() { body = GetComponent<Rigidbody>(); crusher = GetComponent<RaidTankCrusher>(); stepper = GetComponent<RaidTankStepper>(); unstuck = GetComponent<RaidTankUnstuck>(); body.isKinematic = true; }
        private void OnEnable() { if (core != null) core.Destroyed += Stop; IgnoreEndChests(); }
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
            SetMovementSoundActive(false);
        }
        public void Initialize(FirstPersonPlayer target, string enemyId)
        {
            core.AssignIdentity(enemyId, RaidEnemyType.Tank);
            player = target;
            fireRemaining = fireInterval;
        }
        public void Release()
        {
            if (destroyed || released) return;
            released = true; transform.SetParent(null, true);
            IgnoreEndChests(); // 투하 시점에 파츠 콜라이더가 확정된다.
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            body.isKinematic = false; body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
        private void OnCollisionEnter(Collision collision)
        {
            if (!released || landed || destroyed) return;
            // 부술 수 있는 파츠(건물 등)는 추락 중 크러셔가 부수므로 착지로 보지 않고, 정적 바닥·부술 수 없는 파츠에서만 착지한다.
            foreach (var contact in collision.contacts)
                if (contact.normal.y > 0.5f && (crusher == null || !crusher.CanCrush(contact.otherCollider)))
                {
                    landed = true;
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    WorkshopSoundEffects.RaiseTankLanded(contact.point);
                    break;
                }
        }
        private void Stop()
        {
            destroyed = true;
            SetMovementSoundActive(false);
            body.isKinematic = true;
        }
        private void FixedUpdate()
        {
            if (!landed || destroyed || player == null)
            {
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
                climb = stepper.ClimbVelocity(movement.normalized);
            // 걸림이 이어지면 단계적으로 푼다(몸체 영역 부수기 → 임시 높은 턱 오르기 → 막는 콜라이더 잠시 통과).
            if (unstuck != null)
                climb = Mathf.Max(climb, unstuck.Tick(movement != Vector3.zero, movement.normalized, horizontalSpeed, moveSpeed));
            // Keep gravity and physical collision responses; do not move through map geometry.
            body.linearVelocity = new Vector3(movement.x, Mathf.Max(body.linearVelocity.y, climb), movement.z);
            body.angularVelocity = Vector3.zero;
            SetMovementSoundActive(movement != Vector3.zero && horizontalSpeed > 0.1f);
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
