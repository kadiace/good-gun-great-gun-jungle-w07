using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class RetireButton : MonoBehaviour, IInteractable
    {
        [SerializeField] private RetireUI retireUI;
        public Transform OutlineRoot => transform;

        public bool CanInteract(Interaction interactor) => isActiveAndEnabled &&
            !GameManager.GameplayBlocked && retireUI != null && !retireUI.IsOpen &&
            interactor != null && interactor.Player != null &&
            PlayerWallet.ForPlayer(interactor.Player)?.CanAfford(RetireUI.Cost) == true;

        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor)) return false;
            PlayerWallet wallet = PlayerWallet.ForPlayer(interactor.Player);
            GameSaveController save = FindFirstObjectByType<GameSaveController>();
            string error = null;
            if (save == null || !save.SaveAuto(out error))
            {
                GameManager.Instance?.UI?.ShowNotification(save == null
                    ? "자동 저장 관리자를 찾을 수 없습니다." : error);
                return false;
            }
            if (!wallet.TryPurchase(RetireUI.Cost, () => true)) return false;
            retireUI.Open();
            return true;
        }
    }
}
