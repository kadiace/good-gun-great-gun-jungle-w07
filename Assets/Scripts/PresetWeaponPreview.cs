using UnityEngine;

namespace WeaponWorkshop
{
    // 전시 총은 그대로 두고 소유 지급기에 제작/장착을 위임한다. 외곽선은 총의 메시만 사용한다.
    [DisallowMultipleComponent]
    public sealed class PresetWeaponPreview : MonoBehaviour, IInteractable
    {
        private CoreDispenser dispenser;
        private CoreDispenser Dispenser
        {
            get
            {
                if (dispenser == null) dispenser = GetComponentInParent<CoreDispenser>();
                return dispenser;
            }
        }
        public Transform OutlineRoot => transform;

        public bool CanInteract(Interaction interactor) => isActiveAndEnabled && Dispenser != null &&
            Dispenser.OwnsSavedPreview(this) && Dispenser.CanInteract(interactor);

        public bool Interact(Interaction interactor) => CanInteract(interactor) && Dispenser.Interact(interactor);
    }
}
