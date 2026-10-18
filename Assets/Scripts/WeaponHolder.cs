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
        public bool CanEquip => isActiveAndEnabled && handPoint != null;

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
            attack?.Disable();
            drop?.Disable();
            EquippedWeapon?.CancelCharge();
            ResetRecoil();
        }
        private void OnDestroy() => controls?.Dispose();
        private void Update()
        {
            if (GameManager.GameplayBlocked) return;
            recoilOffset = Mathf.MoveTowards(recoilOffset, 0f, recoilReturnSpeed * Time.deltaTime);
            ApplyRecoil();
            UpdateAim();
            if (attack == null || !attack.IsPressed()) return;
            // Normal muzzles keep firing while held; charge muzzles charge until release.
            TryAttack();
            if (EquippedWeapon != null && EquippedWeapon.UsesCharge) EquippedWeapon.HoldCharge(Time.deltaTime);
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

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            if (GameManager.GameplayBlocked || !HasFocus ||
                player == null || !player.isActiveAndEnabled || EquippedWeapon == null) return;
            if (!TutorialManager.TryAction("fire")) return;
            AttackInput?.Invoke(EquippedWeapon);
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
            if (GameManager.GameplayBlocked || !HasFocus || player == null || !player.isActiveAndEnabled)
            {
                EquippedWeapon.CancelCharge();
                return;
            }
            UpdateAim();
            EquipableWeapon weapon = EquippedWeapon;
            if (weapon.ReleaseCharge())
            {
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

        public bool Equip(EquipableWeapon weapon)
        {
            if (weapon == null || weapon.IsEquipped || handPoint == null) return false;
            ReturnWeapon();
            if (!weapon.Equip(handPoint)) return false;
            SetEquippedWeapon(weapon);
            WorkshopSoundEffects.RaiseWeaponEquipped(handPoint.position);
            return true;
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
