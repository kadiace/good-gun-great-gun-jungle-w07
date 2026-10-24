using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    [DefaultExecutionOrder(10)]
    public sealed class WeaponHolder : MonoBehaviour
    {
        [SerializeField] private Transform handPoint;
        [SerializeField, Min(0f)] private float recoilDistance = 0.04f;
        [SerializeField, Min(0.01f)] private float recoilReturnSpeed = 0.25f;
        private Vector3 handRestPosition;
        private float recoilOffset;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Camera aimCamera;
        [SerializeField, Min(1f)] private float aimDistance = 100f;
        [SerializeField] private LayerMask aimLayers = Physics.DefaultRaycastLayers;
        private readonly RaycastHit[] aimHits = new RaycastHit[32];
        private FirstPersonPlayer player;
        private WeaponScope scope;
        private bool aimHeld;
        private Vector3 heldAimPoint;
        private Vector3 heldViewPosition;
        private Quaternion heldViewRotation;
        private InputActionMap controls;
        private InputAction attack;
        private InputAction drop;
        private bool _attackStartedWhileBlocked;
        private bool exchangingWeapon;
        [SerializeField, Min(0f), InspectorName("던지는 힘"), Tooltip("총을 던질 때 즉시 적용할 초기 속도(m/s)")] private float throwSpeed = 5f;
        public EquipableWeapon EquippedWeapon { get; private set; }
        public event System.Action<EquipableWeapon, EquipableWeapon> EquippedWeaponChanged;
        public event System.Action<EquipableWeapon> AttackInput;
        public event System.Action<EquipableWeapon> ShotFired;
        private void SetEquippedWeapon(EquipableWeapon next)
        {
            var previous = EquippedWeapon;
            if (previous == next) return;
            EquippedWeapon = next;
            EquippedWeaponChanged?.Invoke(previous, next);
        }
        public Camera AimCamera => aimCamera;
        public bool CanEquip => isActiveAndEnabled && handPoint != null && !exchangingWeapon;

        private void Awake()
        {
            if (handPoint != null) handRestPosition = handPoint.localPosition;
            player = GetComponent<FirstPersonPlayer>();
            scope = GetComponent<WeaponScope>();
            if (aimCamera == null && player != null && player.ViewPoint != null)
                aimCamera = player.ViewPoint.GetComponentInChildren<Camera>();
            if (inputActions == null) return;
            controls = inputActions.FindActionMap("Player", true).Clone();
            attack = controls.FindAction("Attack", true);
            attack.performed += OnAttackPerformed;
            drop = controls.FindAction("DropWeapon", true);
            drop.performed += _ => TryDropWeapon();
            attack.canceled += _ => ReleaseCharge();
        }
        private void OnEnable() { attack?.Enable(); drop?.Enable(); }
        private void OnDisable()
        {
            // Cancel before disabling Attack: its canceled callback must not release a charged shot.
            EquippedWeapon?.CancelCharge();
            attack?.Disable();
            drop?.Disable();
            _attackStartedWhileBlocked = false;
            ResetRecoil();
        }
        private void OnDestroy() => controls?.Dispose();
        private void Update()
        {
            if (!HasFocus || player == null || !player.isActiveAndEnabled)
            {
                EquippedWeapon?.CancelCharge();
                return;
            }
            if (GameManager.GameplayBlocked)
            {
                if (attack != null && attack.IsPressed()) _attackStartedWhileBlocked = true;
                return;
            }
            if (_attackStartedWhileBlocked)
            {
                if (attack != null && attack.IsPressed()) return;
                _attackStartedWhileBlocked = false;
            }
            recoilOffset = Mathf.MoveTowards(recoilOffset, 0f, recoilReturnSpeed * Time.deltaTime);
            ApplyRecoil();
            UpdateAim();
            if (attack == null || !attack.IsPressed()) return;
            // Normal muzzles keep firing while held; charge muzzles charge until release.
            TryAttack();
            if (TutorialManager.ActionAllowed("fire") && EquippedWeapon != null && EquippedWeapon.UsesCharge)
                EquippedWeapon.HoldCharge(Time.deltaTime);
        }

        private void UpdateAim()
        {
            if (EquippedWeapon == null || aimCamera == null || player == null || !player.isActiveAndEnabled) return;
            bool scoped = scope != null && scope.IsAiming;
            if (!scoped) aimHeld = false;   // each new zoom starts from a fresh aim ray
            EquippedWeapon.AimAt(scoped ? HeldAimPoint() : GetAimPoint(), AimView.up);
        }

        // The gun bends toward the aim ray's hit point so shots meet the screen center. While scoped the view rides
        // on the gun, so a surface carved away by the shots would push the hit point back and swing the view.
        // Keep the scoped aim point until the look itself moves.
        private Vector3 HeldAimPoint()
        {
            Transform view = AimView;
            if (!aimHeld || Vector3.SqrMagnitude(view.position - heldViewPosition) > 0.000001f ||
                Quaternion.Angle(view.rotation, heldViewRotation) > 0.01f)
            {
                heldAimPoint = GetAimPoint();
                heldViewPosition = view.position;
                heldViewRotation = view.rotation;
                aimHeld = true;
            }
            return heldAimPoint;
        }

        // Aim follows the player's look (ViewPoint), not the camera, so the scope can move the camera freely.
        private Transform AimView => player != null && player.ViewPoint != null ? player.ViewPoint : aimCamera.transform;

        private Vector3 GetAimPoint()
        {
            if (aimCamera == null) return transform.position + transform.forward * aimDistance;
            return TryGetAimHit(out var hit) ? hit.point : new Ray(AimView.position, AimView.forward).GetPoint(aimDistance);
        }

        public Vector3 GetScopeAimPoint() => scope != null && scope.IsAiming ? HeldAimPoint() : GetAimPoint();

        public bool TryGetAimHit(out RaycastHit result)
        {
            result = default;
            if (aimCamera == null) return false;
            Ray ray = new Ray(AimView.position, AimView.forward);
            float nearest = aimDistance;
            DestructionCollisionBatch.Flush();
            int count = Physics.RaycastNonAlloc(ray, aimHits, aimDistance, aimLayers | LayerMask.GetMask("EndChest"), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = aimHits[i].collider.transform;
                if (hit.IsChildOf(transform) || (EquippedWeapon != null && hit.IsChildOf(EquippedWeapon.transform)) || aimHits[i].distance >= nearest) continue;
                nearest = aimHits[i].distance;
                result = aimHits[i];
            }
            return result.collider != null;
        }

        // Benchmark replay drives fire through the Input System even when the window is not focused.
        private static bool HasFocus => Application.isFocused || BenchmarkSession.ReplayActive;

        private void TryAttack()
        {
            if (!TutorialManager.ActionAllowed("fire")) return;
            if (GameManager.GameplayBlocked) return;
            if (!HasFocus || player == null || !player.isActiveAndEnabled || EquippedWeapon == null) return;
            UpdateAim();
            EquipableWeapon weapon = EquippedWeapon;
            if (weapon.TryFire())
            {
                ShotFired?.Invoke(weapon);
                recoilOffset = recoilDistance;
                ApplyRecoil();
            }
        }

        /// <summary>
        /// 사격 입력 액션 수행 시 조준 및 게임 차단 상태를 점검하고 사격을 시도합니다.
        /// context 정보를 확인하여 차단 상태이면 누름 상태를 기록하고 반환합니다.
        /// </summary>
        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            if (GameManager.GameplayBlocked || !HasFocus ||
                player == null || !player.isActiveAndEnabled || EquippedWeapon == null)
            {
                _attackStartedWhileBlocked = true;
                return;
            }
            if (_attackStartedWhileBlocked) return;
            if (!TutorialManager.TryAction("fire")) return;
            AttackInput?.Invoke(EquippedWeapon);
            // Register even sub-frame taps. Locked charge muzzles are ignored inside PowerCore.
            if (EquippedWeapon != null && EquippedWeapon.UsesCharge) EquippedWeapon.HoldCharge(0f);
            TryAttack();
        }

        public bool TryDropWeapon()
        {
            if (!TutorialManager.TryAction("drop")) return false;
            if (GameManager.GameplayBlocked || !Application.isFocused || player == null ||
                !player.isActiveAndEnabled || EquippedWeapon == null || EquippedWeapon.IsMoving) return false;
            EquipableWeapon weapon = EquippedWeapon;
            if (!weapon.Drop(GetAimPoint(), throwSpeed)) return false;
            TakeWeapon();
            WorkshopSoundEffects.RaiseWeaponDropped(weapon.transform.position);
            return true;
        }

        private void ReleaseCharge()
        {
            if (EquippedWeapon == null || !EquippedWeapon.UsesCharge) return;
            if (!isActiveAndEnabled || _attackStartedWhileBlocked || !TutorialManager.ActionAllowed("fire") ||
                GameManager.GameplayBlocked || !HasFocus || player == null || !player.isActiveAndEnabled)
            {
                EquippedWeapon.CancelCharge();
                return;
            }
            UpdateAim();
            EquipableWeapon weapon = EquippedWeapon;
            if (weapon.ReleaseCharge())
            {
                // One notification/hand kick for the release; deferred burst rounds do not repeat it.
                ShotFired?.Invoke(weapon);
                recoilOffset = recoilDistance;
                ApplyRecoil();
            }
        }

        private void ApplyRecoil()
        {
            if (handPoint == null) return;
            // Scoped, the view rides on the gun: any hand kick or charge tremble would jerk the whole screen.
            if (scope != null && scope.IsAiming)
            {
                recoilOffset = 0f;
                handPoint.localPosition = handRestPosition;
                return;
            }
            Vector3 shake = Vector3.zero;
            // Charging trembles the weapon more as the charge fills.
            if (EquippedWeapon != null && EquippedWeapon.IsCharging)
                shake = Random.insideUnitSphere * EquippedWeapon.ChargeShakeDistance * EquippedWeapon.ChargeProgress;
            handPoint.localPosition = handRestPosition + handPoint.localRotation * Vector3.back * recoilOffset + shake;
        }

        private void ResetRecoil()
        {
            recoilOffset = 0f;
            ApplyRecoil();
        }

        // 일반 장착의 작업대 반환 정책은 프리셋 교체와 분리한다.
        public bool Equip(EquipableWeapon weapon)
        {
            if (exchangingWeapon || weapon == null || weapon.IsEquipped || handPoint == null) return false;
            ReturnWeapon();
            if (!weapon.Equip(handPoint)) return false;
            SetEquippedWeapon(weapon);
            WorkshopSoundEffects.RaiseWeaponEquipped(handPoint.position);
            return true;
        }

        // 새 총의 장착 준비가 실패하면 기존 총은 손에 남는다. 전시 총이 아닌 새로 제작한 총을 전달한다.
        public bool TryEquipWithDrop(EquipableWeapon weapon)
        {
            if (!CanEquip || GameManager.GameplayBlocked || player == null || !player.isActiveAndEnabled ||
                weapon == null || !weapon.isActiveAndEnabled || weapon.IsEquipped || weapon.IsMoving) return false;
            var previous = EquippedWeapon;
            if (previous != null && (!previous.IsEquipped || previous.IsMoving)) return false;

            var parent = weapon.transform.parent;
            var position = weapon.transform.localPosition;
            var rotation = weapon.transform.localRotation;
            var scale = weapon.transform.localScale;
            var body = weapon.GetComponent<Rigidbody>();
            bool dynamicBody = body != null && !body.isKinematic;
            bool gravity = body != null && body.useGravity;
            var velocity = dynamicBody ? body.linearVelocity : Vector3.zero;
            var angularVelocity = dynamicBody ? body.angularVelocity : Vector3.zero;
            bool prepared = false, committed = false;
            exchangingWeapon = true;
            try
            {
                // EquipableWeapon.Equip은 기존 총을 반환하지 않는다. 실패할 수 있는 준비를 먼저 끝낸다.
                if (!weapon.Equip(handPoint)) return false;
                prepared = true;
                // 손 위치에서 초속 0으로 떨어뜨린다. 투척 속도나 벽/선반 안으로의 강제 배치는 사용하지 않는다.
                if (previous != null && !previous.Drop(previous.transform.position + transform.forward, 0f)) return false;
                previous?.CancelCharge();
                committed = true;
                SetEquippedWeapon(weapon);
                ResetRecoil();
                if (previous != null) WorkshopSoundEffects.RaiseWeaponDropped(previous.transform.position);
                WorkshopSoundEffects.RaiseWeaponEquipped(handPoint.position);
                return true;
            }
            finally
            {
                if (prepared && !committed)
                    weapon.RestorePlacement(parent, position, rotation, scale, dynamicBody, gravity, velocity, angularVelocity);
                exchangingWeapon = false;
            }
        }

        // 반납 없이 손에서 떼어 넘김 (판매용).
        public bool TryExchangeWeapon(EquipableWeapon replacement, out EquipableWeapon incoming)
        {
            incoming = EquippedWeapon;
            if (incoming == null || incoming.IsMoving || !CanEquip ||
                (replacement != null && (replacement.IsEquipped || replacement.IsMoving))) return false;
            TakeWeapon();
            if (replacement == null || Equip(replacement)) return true;
            SetEquippedWeapon(incoming);
            return false;
        }

        public EquipableWeapon TakeWeapon()
        {
            EquipableWeapon weapon = EquippedWeapon;
            weapon?.CancelCharge();
            SetEquippedWeapon(null);
            ResetRecoil();
            return weapon;
        }

        public void ReturnWeapon()
        {
            EquippedWeapon?.CancelCharge();
            if (EquippedWeapon != null) EquippedWeapon.ReturnToWorkbench();
            SetEquippedWeapon(null);
            ResetRecoil();
        }
    }
}
