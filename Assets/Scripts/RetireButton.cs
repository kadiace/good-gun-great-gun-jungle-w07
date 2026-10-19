using System;
using LogKit;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class RetireButton : MonoBehaviour, IInteractable
    {
        [SerializeField] private RetireUI retireUI;
        public Transform OutlineRoot => transform;

        public bool CanInteract(Interaction interactor) => isActiveAndEnabled &&
            !GameManager.GameplayBlocked && retireUI != null && !retireUI.IsOpen &&
            FindFirstObjectByType<GameSaveController>()?.IsRetired != true &&
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
                Log("retire_failed", new RetireFailedLog
                { reason = save == null ? "save_controller_missing" : "autosave_failed", balance = wallet.Balance });
                GameManager.Instance?.UI?.ShowNotification(save == null
                    ? "자동 저장 관리자를 찾을 수 없습니다." : error);
                return false;
            }
            int balanceBefore = wallet.Balance;
            if (!wallet.TryPurchase(RetireUI.Cost, () => true))
            {
                Log("retire_failed", new RetireFailedLog { reason = "purchase_failed", balance = wallet.Balance });
                return false;
            }
            bool firstEnding = !save.EndChestOpened;
            save.CompleteRetirement();
            Log("retire_completed", new RetireCompletedLog
            {
                balanceBefore = balanceBefore, balanceAfter = wallet.Balance, cost = RetireUI.Cost,
                outstandingLoans = wallet.OutstandingLoans, moduleCredits = wallet.ModuleCredits,
                endless = retireUI.DayManager != null && retireUI.DayManager.IsEndlessDay, firstEnding = firstEnding
            });
            retireUI.Open(firstEnding);
            return true;
        }

        private void Log(string type, RetireLog entry)
        {
            var day = retireUI.DayManager;
            entry.day = day != null ? day.DayNumber : 0;
            entry.period = day != null ? day.CurrentPeriod.ToString() : "unknown";
            entry.isNight = day != null && day.IsNight;
            entry.playSeconds = Time.time;
            entry.installId = PlayerIdentity.InstallId;
            entry.saveId = PlayerIdentity.SaveId;
            try { GameLogger.Data("Info", "Game", type, entry); }
            catch (Exception exception) { Debug.LogWarning("은퇴 로그 기록 실패: " + exception.Message, this); }
        }
    }
}
