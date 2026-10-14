using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class GunRack : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform displayPoint;

        public Transform OutlineRoot => transform;

        public bool CanInteract(Interaction interactor)
        {
            if (!isActiveAndEnabled || displayPoint == null || interactor == null || interactor.Player == null)
                return false;

            WeaponHolder holder = interactor.Player.GetComponent<WeaponHolder>();
            if (holder == null || !holder.CanEquip)
                return false;

            EquipableWeapon displayedWeapon = GetDisplayedWeapon();
            if (displayedWeapon != null)
                return !displayedWeapon.IsMoving && holder.EquippedWeapon == null;

            EquipableWeapon heldWeapon = holder.EquippedWeapon;
            return heldWeapon != null && heldWeapon.IsEquipped && !heldWeapon.IsMoving;
        }

        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor))
                return false;

            WeaponHolder holder = interactor.Player.GetComponent<WeaponHolder>();
            EquipableWeapon displayedWeapon = GetDisplayedWeapon();
            if (displayedWeapon != null)
                return holder.Equip(displayedWeapon);

            EquipableWeapon heldWeapon = holder.TakeWeapon();
            if (heldWeapon == null)
                return false;

            heldWeapon.PlaceOnWorkbench(displayPoint);
            WorkshopSoundEffects.RaiseWeaponDisplayed(displayPoint.position);
            return true;
        }

        public Transform DisplayPoint => displayPoint;
        public EquipableWeapon DisplayedWeapon => GetDisplayedWeapon();
        public bool RestoreWeapon(EquipableWeapon weapon)
        {
            if (displayPoint == null || weapon == null) return false;
            weapon.RestorePlacement(displayPoint, Vector3.zero, Quaternion.identity, Vector3.one, false, false, Vector3.zero, Vector3.zero);
            return true;
        }
        private EquipableWeapon GetDisplayedWeapon()
        {
            return displayPoint != null ? displayPoint.GetComponentInChildren<EquipableWeapon>(true) : null;
        }
    }
}
