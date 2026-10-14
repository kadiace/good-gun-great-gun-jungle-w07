using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class Workbench : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform drawRoot;
        [SerializeField] private Transform cameraPosition;
        [SerializeField] private Transform rotationTarget;
        [SerializeField, Tooltip("판매 후 작업대에 새로 놓을 코어 프리팹")] private EquipableWeapon corePrefab;
        public Transform OutlineRoot => drawRoot != null ? drawRoot : transform;
        public Transform CameraPosition => cameraPosition;
        public Transform RotationTarget => rotationTarget;
        public EquipableWeapon CurrentWeapon => rotationTarget != null ? rotationTarget.GetComponentInChildren<EquipableWeapon>(true) : null;
        public PowerCore CurrentCore => rotationTarget != null ? rotationTarget.GetComponentInChildren<PowerCore>() : null;
        private void Awake()
        {
            if (rotationTarget == null) return;
            foreach (var weapon in rotationTarget.GetComponentsInChildren<EquipableWeapon>(true)) weapon.SetWorkbench(this);
        }
        public bool CanInteract() => isActiveAndEnabled && cameraPosition != null && rotationTarget != null;
        private static WorkshopSession GetSession(Interaction interactor) => interactor != null && interactor.Player != null ? interactor.Player.GetComponent<WorkshopSession>() : null;
        public bool CanInteract(Interaction interactor)
        {
            var session = GetSession(interactor);
            return CanInteract() && session != null && session.CanEnterWorkshop && session.HasCoreForWorkbench(this);
        }
        public bool Interact(Interaction interactor)
        {
            var session = GetSession(interactor);
            if (session == null || !session.Enter(this)) return false;
            if (CurrentWeapon != null && TutorialManager.Instance != null)
                TutorialManager.Instance.NotifySignal("core-placed-on-workbench", this);
            return true;
        }
        public bool PlaceWeapon(EquipableWeapon weapon, bool playPlacementSound = true)
        {
            if (weapon == null || rotationTarget == null || rotationTarget.IsChildOf(weapon.transform)) return false;
            if (CurrentWeapon != null && CurrentWeapon != weapon) return false;
            weapon.SetWorkbench(this);
            ResetRotation();
            weapon.PlaceOnWorkbench(rotationTarget);
            if (playPlacementSound) WorkshopSoundEffects.RaiseWeaponPlacedOnTable(rotationTarget.position);
            return true;
        }
        public bool ExchangeWeapon(WeaponHolder holder)
        {
            if (holder == null || holder.EquippedWeapon == null) return true;
            var incoming = holder.EquippedWeapon;
            var outgoing = CurrentWeapon;
            if (incoming.IsMoving || (outgoing != null && (outgoing.IsMoving || !holder.CanEquip))) return false;
            if (!holder.TryExchangeWeapon(outgoing, out incoming)) return false;
            return PlaceWeapon(incoming);
        }
        public bool SpawnCore()
        {
            if (corePrefab == null || rotationTarget == null || !isActiveAndEnabled) return false;
            if (CurrentWeapon != null) return false;
            EquipableWeapon core = Instantiate(corePrefab, rotationTarget);
            core.name = corePrefab.name;
            return PlaceWeapon(core);
        }

        public void ResetRotation()
        {
            if (rotationTarget != null) rotationTarget.localRotation = Quaternion.Euler(0f, 90f, 90f);
        }
    }
}
