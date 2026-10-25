using UnityEngine;
namespace WeaponWorkshop
{
    public sealed class SaleConfirmationUI : MonoBehaviour
    {
        public static SaleConfirmationUI Instance { get; private set; }
        private Customer customer;
        private WeaponHolder holder;
        private EquipableWeapon weapon;
        private FeedbackUI feedback;
        public bool IsOpen { get; private set; }
        private void Awake() { Instance = this; IsOpen = false; }
        public bool UseCustomer(Customer target, WeaponHolder weaponHolder)
        {
            if (IsOpen || target == null) return false;
            return target.Use(weaponHolder);
        }
        public bool ShowSubmissionChoice(Customer target, WeaponHolder weaponHolder)
        {
            if (IsOpen || target == null || weaponHolder == null || weaponHolder.EquippedWeapon == null) return false;
            feedback = FeedbackUI.For(this);
            if (feedback == null || target.Profile == null) return false;
            customer = target; holder = weaponHolder; weapon = holder.EquippedWeapon;
            IsOpen = feedback.ShowChoice(this, "들고 있는 총을 제출하시겠습니까?", Confirm, Cancel,
                "제출", "취소", true, true);
            if (!IsOpen) Cancel();
            return IsOpen;
        }
        private void Confirm()
        {
            Customer target = customer; WeaponHolder seller = holder;
            bool canSell = target != null && seller != null && seller.EquippedWeapon == weapon && weapon != null && GameManager.Instance != null && !GameManager.Instance.IsNight;
            Cancel();
            if (canSell) target.Sell(seller);
        }
        private void Cancel() { IsOpen = false; customer = null; holder = null; weapon = null; }
        private void Update()
        {
            if (IsOpen && (customer == null || holder == null || weapon == null || holder.EquippedWeapon != weapon || (GameManager.Instance != null && GameManager.Instance.IsNight))) feedback?.CancelChoice(this);
        }
        private void OnDisable() { feedback?.CancelChoice(this); Cancel(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
