using UnityEngine;

namespace WeaponWorkshop
{
    // 영업 종료 버튼 상호작용 컴포넌트.
    // UI 표시 로직이 없으며 월드 오브젝트 상호작용으로 DayManager.CloseShop을 호출한다.
    public sealed class CloseShopButton : MonoBehaviour, IInteractable
    {
        [SerializeField] private _DayManager dayManager;

        public Transform OutlineRoot => transform;

        private void Awake()
        {
            if (dayManager == null)
            {
                dayManager = FindFirstObjectByType<_DayManager>();
            }
        }

        public bool CanInteract(Interaction interactor)
        {
            return isActiveAndEnabled &&
                !GameManager.GameplayBlocked &&
                dayManager != null &&
                !dayManager.IsNight &&
                !dayManager.IsAwaitingOpening &&
                interactor != null &&
                interactor.Player != null;
        }

        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor))
            {
                return false;
            }

            dayManager.CloseShop(manual: true);
            return true;
        }
    }
}
