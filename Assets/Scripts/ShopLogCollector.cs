using System;
using LogKit;
using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class ShopLogCollector : MonoBehaviour
    {
        [SerializeField] private _DayManager dayManager;
        [SerializeField] private Bell customerBell;
        [SerializeField] private FirstPersonPlayer player;
        private WorkshopSession workshop;
        private WeaponHolder holder;
        private WeaponPresets presets;
        private bool bound;

        private bool CanRecord => dayManager != null && dayManager.CanCollectShopLogs;
        private bool CanRecordAction => CanRecord && !dayManager.IsNight;
        private CustomerProfile ActiveOrder => customerBell != null && customerBell.Current != null &&
            customerBell.Current.HasActiveOrder ? customerBell.Current.Profile : null;

        private void OnEnable()
        {
            if (dayManager == null) dayManager = GetComponent<_DayManager>();
            if (dayManager == null || customerBell == null || player == null ||
                player.gameObject.scene != gameObject.scene) return;
            workshop = player.GetComponent<WorkshopSession>();
            holder = player.GetComponent<WeaponHolder>();
            presets = player.GetComponent<WeaponPresets>();
            dayManager.BusinessOpened += OnBusinessOpened;
            dayManager.ManualCloseRequested += OnManualCloseRequested;
            dayManager.OrderRegistered += OnOrderRegistered;
            customerBell.CustomerInteracted += OnCustomerInteracted;
            customerBell.OrderFinished += OnOrderFinished;
            customerBell.WeaponSold += OnWeaponSold;
            if (workshop != null)
            {
                workshop.WorkbenchEntered += OnWorkbenchEntered;
                workshop.WorkbenchExited += OnWorkbenchExited;
            }
            if (holder != null) holder.AttackInput += OnAttackInput;
            if (holder != null) holder.ShotFired += OnWeaponFired;
            if (presets != null) presets.Stored += OnPresetStored;
            bound = true;
        }

        private void OnDisable()
        {
            if (!bound) return;
            dayManager.BusinessOpened -= OnBusinessOpened;
            dayManager.ManualCloseRequested -= OnManualCloseRequested;
            dayManager.OrderRegistered -= OnOrderRegistered;
            customerBell.CustomerInteracted -= OnCustomerInteracted;
            customerBell.OrderFinished -= OnOrderFinished;
            customerBell.WeaponSold -= OnWeaponSold;
            if (workshop != null)
            {
                workshop.WorkbenchEntered -= OnWorkbenchEntered;
                workshop.WorkbenchExited -= OnWorkbenchExited;
            }
            if (holder != null) holder.AttackInput -= OnAttackInput;
            if (holder != null) holder.ShotFired -= OnWeaponFired;
            if (presets != null) presets.Stored -= OnPresetStored;
            bound = false;
        }

        private bool Write(ShopLogEvent entry, string type, CustomerProfile order = null, int day = -1)
        {
            if (!CanRecord) return false;
            entry.eventType = type;
            entry.day = day >= 0 ? day : dayManager.DayNumber;
            entry.orderId = order?.OrderState?.orderId ?? "";
            entry.eventId = !string.IsNullOrEmpty(entry.orderId) &&
                (entry is ShopOrderLinkLogEvent || entry is ShopOrderCompletedLogEvent)
                ? entry.orderId + "/" + type : Guid.NewGuid().ToString("N");
            try
            {
                GameLogger.Data("Info", "Shop", type, entry);
                return true;
            }
            catch (Exception exception)
            {
                // Telemetry must never interrupt an accepted game action.
                Debug.LogWarning("상점 로그 기록 실패: " + exception.Message);
                return false;
            }
        }

        private void OnOrderRegistered(CustomerProfile order, int day)
        {
            var state = order != null ? order.OrderState : null;
            if (!CanRecord || state == null || state.creationLogged) return;
            var entry = new ShopOrderLinkLogEvent
            {
                profileId = order.ProfileId ?? "",
                requestIds = TableManager.Instance.CustomerRequest.CanonicalRequestIds(order.RequestIds) ?? Array.Empty<string>()
            };
            if (Write(entry, state.restored ? "order_restored" : "order_created", order, day)) state.creationLogged = true;
        }

        private void OnBusinessOpened(int day, int balance) =>
            Write(new ShopMoneyLogEvent { balance = balance }, "shop_opened", day: day);
        private void OnManualCloseRequested(int day, int balance) =>
            Write(new ShopMoneyLogEvent { balance = balance }, "shop_close_requested", day: day);

        private void OnCustomerInteracted(CustomerProfile order)
        {
            if (!CanRecordAction) return;
            OnOrderRegistered(order, dayManager.DayNumber);
            Write(new ShopLogEvent(), "customer_interacted", order);
        }

        private void OnOrderFinished(CustomerProfile order, bool success, bool dead)
        {
            var state = order != null ? order.OrderState : null;
            if (!CanRecordAction || state == null || state.completionLogged) return;
            OnOrderRegistered(order, dayManager.DayNumber);
            if (Write(new ShopOrderCompletedLogEvent { success = success && !dead, customerDead = dead }, "order_completed", order))
                state.completionLogged = true;
        }

        private void OnWeaponSold(CustomerProfile order, EquipableWeapon weapon, int price, bool satisfied, int tagCount)
        {
            if (!CanRecordAction) return;
            OnOrderRegistered(order, dayManager.DayNumber);
            var capturedWeapon = CaptureWeapon(weapon);
            Write(new ShopSaleLogEvent
            {
                price = price, satisfied = satisfied, tagCount = tagCount,
                weaponCode = capturedWeapon.weaponCode, weaponCodeStatus = capturedWeapon.weaponCodeStatus
            }, "weapon_sold", order);
        }

        private void OnWorkbenchEntered()
        {
            if (CanRecordAction) Write(new ShopLogEvent(), "workbench_entered", ActiveOrder);
        }
        private void OnWorkbenchExited(EquipableWeapon weapon)
        {
            if (CanRecordAction) Write(CaptureWeapon(weapon), "workbench_exited", ActiveOrder);
        }
        private void OnAttackInput(EquipableWeapon weapon)
        {
            if (CanRecordAction) Write(CaptureWeapon(weapon), "attack_input", ActiveOrder);
        }
        private void OnWeaponFired(EquipableWeapon weapon)
        {
            if (!CanRecordAction) return;
            string assemblyId = CaptureAssemblyId(weapon, out string assemblyIdStatus);
            var entry = new ShopWeaponFiredLogEvent
            {
                time = Time.timeSinceLevelLoad,
                position = player != null ? player.transform.position : Vector3.zero,
                weaponAssemblyId = assemblyId,
                weaponAssemblyIdStatus = assemblyIdStatus
            };
            Write(entry, "weapon_fired", ActiveOrder);
        }
        private void OnPresetStored(int slot, string code)
        {
            if (CanRecordAction) Write(new ShopPresetLogEvent
            { slot = slot + 1, weaponCode = code, weaponCodeStatus = "ok" }, "preset_saved", ActiveOrder);
        }

        private static ShopWeaponLogEvent CaptureWeapon(EquipableWeapon weapon)
        {
            var entry = new ShopWeaponLogEvent();
            if (weapon == null) return entry;
            try { entry.weaponCode = WeaponShareCode.Export(weapon); entry.weaponCodeStatus = "ok"; }
            catch (Exception) { entry.weaponCodeStatus = "export_failed"; }
            return entry;
        }

        private static string CaptureAssemblyId(EquipableWeapon weapon, out string status)
        {
            if (weapon == null) { status = "no_weapon"; return ""; }
            try
            {
                string key = weapon.TestAssemblyKey;
                status = string.IsNullOrEmpty(key) ? "assembly_id_failed" : "ok";
                return key ?? "";
            }
            catch (Exception) { status = "assembly_id_failed"; return ""; }
        }
    }
}
