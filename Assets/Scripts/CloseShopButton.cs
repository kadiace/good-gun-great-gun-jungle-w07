using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class CloseShopButton : MonoBehaviour, IInteractable
    {
        [SerializeField] private _DayManager dayManager;
        public Transform OutlineRoot => transform;
        private void Awake()
        {
            if (dayManager == null) dayManager = FindFirstObjectByType<_DayManager>();
        }
        public bool CanInteract(Interaction interactor) => isActiveAndEnabled &&
            !GameManager.GameplayBlocked && dayManager != null && !dayManager.IsNight && !dayManager.IsAwaitingOpening &&
            interactor != null && interactor.Player != null;
        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor)) return false;
            dayManager.CloseShop(manual: true);
            return true;
        }
    }
}
