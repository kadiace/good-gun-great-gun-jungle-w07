using System;
using UnityEngine;

namespace WeaponWorkshop
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PlayerWallet : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingBalance = 99999999;
        public int StartingBalance => startingBalance;
        public int Balance { get; private set; }
        public const int LoanAmount = 3000;
        public int OutstandingLoans { get; private set; }
        public int LastLoanDay { get; private set; } = -1;
        public int TodayConstructionCost { get; private set; }
        [SerializeField, Min(0)] private int startingModuleCredits;
        public int ModuleCredits { get; private set; }

        private void Awake()
        {
            Balance = Mathf.Max(0, startingBalance);
            OutstandingLoans = 0;
            LastLoanDay = -1;
            TodayConstructionCost = 0;
            ModuleCredits = Mathf.Max(0, startingModuleCredits);
        }
        public event Action Changed;
        public event Action<int> ModuleCreditsLost;
        // The balance is already restored; this event only presents the raid loss.
        public void ShowModuleCreditLoss(int previousAmount)
        {
            if (previousAmount > ModuleCredits) ModuleCreditsLost?.Invoke(previousAmount);
        }
        public event Action<bool> PurchaseFundsInsufficient;

        public bool CheckPurchaseFunds(int price, bool useModuleCredits)
        {
            bool affordable = useModuleCredits ? CanAffordModuleCredits(price) : CanAfford(price);
            if (!affordable && isActiveAndEnabled && price >= 0)
                PurchaseFundsInsufficient?.Invoke(useModuleCredits);
            return affordable;
        }

        public bool CheckModuleFunds(int dollars, int materials)
        {
            bool money = CheckPurchaseFunds(dollars, false);
            bool resources = CheckPurchaseFunds(materials, true);
            return money && resources;
        }
        public bool TryPurchaseModule(int dollars, int materials, Func<bool> purchase)
        {
            if (!CheckModuleFunds(dollars, materials) || purchase == null || !purchase()) return false;
            Balance -= dollars;
            TodayConstructionCost = (int)Math.Min(int.MaxValue, (long)TodayConstructionCost + dollars);
            ModuleCredits -= materials;
            Changed?.Invoke();
            return true;
        }
        public bool CanAffordModuleCredits(int price) => isActiveAndEnabled && price >= 0 && ModuleCredits >= price;

        public bool TryPurchaseWithModuleCredits(int price, Func<bool> purchase)
        {
            if (!CheckPurchaseFunds(price, true) || purchase == null || !purchase()) return false;
            ModuleCredits -= price;
            Changed?.Invoke();
            return true;
        }

        public void CreditModuleCredits(int amount)
        {
            if (amount <= 0) return;
            ModuleCredits = (int)Math.Min(int.MaxValue, (long)ModuleCredits + amount);
            Changed?.Invoke();
        }

        public void RestoreModuleCredits(int amount)
        {
            ModuleCredits = Mathf.Max(0, amount);
            Changed?.Invoke();
        }

        public bool CanAfford(int price) => isActiveAndEnabled && price >= 0 && (price == 0 || Balance >= price);

        public bool TryPurchase(int price, Func<bool> purchase, Action<int> recordPurchase = null, bool allowDebt = false)
        {
            bool canPurchase = CheckPurchaseFunds(price, false);
            if (!canPurchase || purchase == null || !purchase()) return false;
            Balance -= price;
            recordPurchase?.Invoke(price);
            Changed?.Invoke();
            return true;
        }

        public void Credit(int amount)
        {
            if (amount <= 0) return;
            Balance = (int)Math.Min(int.MaxValue, (long)Balance + amount);
            Changed?.Invoke();
        }

        public void RefundConstruction(int amount)
        {
            if (amount <= 0) return;
            TodayConstructionCost = (int)Math.Max(int.MinValue, (long)TodayConstructionCost - amount);
            Credit(amount);
        }

        public void RestoreTodayConstructionCost(int amount)
        {
            TodayConstructionCost = amount;
            Changed?.Invoke();
        }

        public bool TryTakeLoan(int day)
        {
            if (!isActiveAndEnabled || day < 0 || LastLoanDay == day ||
                Balance > int.MaxValue - LoanAmount || OutstandingLoans == int.MaxValue) return false;
            OutstandingLoans++;
            LastLoanDay = day;
            Credit(LoanAmount);
            return true;
        }

        public void RestoreLastLoanDay(int day) => LastLoanDay = day;

        public void RestoreLoans(int count)
        {
            OutstandingLoans = Mathf.Max(0, count);
            Changed?.Invoke();
        }

        public void Debit(int amount)
        {
            if (amount <= 0) return;
            Balance = (int)Math.Max(0, (long)Balance - amount);
            Changed?.Invoke();
        }

        public void RestoreBalance(int balance)
        {
            Balance = Mathf.Max(0, balance);
            Changed?.Invoke();
        }

        public static PlayerWallet ForPlayer(FirstPersonPlayer player)
        {
            if (player == null) return null;
            PlayerWallet wallet = player.GetComponent<PlayerWallet>();
            if (wallet == null)
                Debug.LogError("플레이어에 PlayerWallet 컴포넌트를 씬에서 추가해주세요.", player);
            return wallet;
        }

        public static PlayerWallet ForScene(Component owner)
        {
            if (owner == null) return null;
            FirstPersonPlayer found = null;
            foreach (FirstPersonPlayer player in FindObjectsByType<FirstPersonPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (player.gameObject.scene != owner.gameObject.scene) continue;
                if (found != null)
                {
                    Debug.LogError("같은 씬에 플레이어가 여러 명 있어 지갑을 선택할 수 없습니다.", owner);
                    return null;
                }
                found = player;
            }
            return ForPlayer(found);
        }
    }
}
