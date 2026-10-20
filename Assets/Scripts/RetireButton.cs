using System;
using LogKit;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class RetireButton : MonoBehaviour, IInteractable
    {
        [SerializeField] private RetireUI retireUI;
        public Transform OutlineRoot => transform;

        // 은퇴 조건 판정은 RetirementInfo가 기준 테이블(RetirementData)로 한다.
        // 레거시 세이브는 잔액만, v9 세이브는 만족 판매·누적 매출·잔액을 모두 본다.
        public bool CanInteract(Interaction interactor) => isActiveAndEnabled &&
            !GameManager.GameplayBlocked && retireUI != null && !retireUI.IsOpen &&
            interactor != null && interactor.Player != null &&
            RetirementInfo.Meets(FindFirstObjectByType<GameSaveController>(),
                PlayerWallet.ForPlayer(interactor.Player));

        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor)) return false;
            PlayerWallet wallet = PlayerWallet.ForPlayer(interactor.Player);
            GameSaveController save = FindFirstObjectByType<GameSaveController>();
            if (save == null || wallet == null)
            {
                Log("retire_failed", new RetireFailedLog
                { reason = "save_controller_missing", balance = wallet != null ? wallet.Balance : 0 });
                GameManager.Instance?.UI?.ShowNotification("자동 저장 관리자를 찾을 수 없습니다.");
                return false;
            }
            int cost = RetirementInfo.Cost;
            int balanceBefore = wallet.Balance;
            // 1) 차감. 2) 은퇴 상태 확정과 함께 3) 은퇴 시점 저장을 강제로 기록한다.
            if (!wallet.TryPurchase(cost, () => true))
            {
                Log("retire_failed", new RetireFailedLog { reason = "purchase_failed", balance = wallet.Balance });
                return false;
            }
            bool firstEnding = !save.EndChestOpened;
            if (!save.CompleteRetirement(out string error))
            {
                // 저장 실패: 차감을 복구한다(은퇴 상태는 CompleteRetirement가 되돌린다).
                wallet.RestoreBalance(balanceBefore);
                Log("retire_failed", new RetireFailedLog { reason = "autosave_failed", balance = wallet.Balance });
                GameManager.Instance?.UI?.ShowNotification(error);
                return false;
            }
            Log("retire_completed", new RetireCompletedLog
            {
                balanceBefore = balanceBefore, balanceAfter = wallet.Balance, cost = cost,
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
