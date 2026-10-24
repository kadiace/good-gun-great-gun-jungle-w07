using System;
using System.Collections.Generic;
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
        private PlayerWallet wallet;
        private bool bound;
        // Time.time at shop_opened; -1 when the opening was not seen (e.g. a mid-day load).
        private float businessOpenedAt = -1f;

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
            wallet = player.GetComponent<PlayerWallet>();
            dayManager.BusinessOpened += OnBusinessOpened;
            dayManager.ManualCloseRequested += OnManualCloseRequested;
            dayManager.ShopClosing += OnShopClosing;
            dayManager.OrderDeferred += OnOrderDeferred;
            if (wallet != null)
            {
                wallet.PartPurchased += OnPartPurchased;
                wallet.PartRefunded += OnPartRefunded;
                wallet.PurchaseFailed += OnPurchaseFailed;
                wallet.LoanTaken += OnLoanTaken;
            }
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
            dayManager.ShopClosing -= OnShopClosing;
            dayManager.OrderDeferred -= OnOrderDeferred;
            if (wallet != null)
            {
                wallet.PartPurchased -= OnPartPurchased;
                wallet.PartRefunded -= OnPartRefunded;
                wallet.PurchaseFailed -= OnPurchaseFailed;
                wallet.LoanTaken -= OnLoanTaken;
            }
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
                requestIds = TableManager.Instance.CustomerRequest.CanonicalRequestIds(order.RequestIds) ?? Array.Empty<string>(),
                generatedDay = state.generatedDay, requestRewards = state.rewards, schemaVersion = 2
            };
            if (Write(entry, state.restored ? "order_restored" : "order_created", order, day)) state.creationLogged = true;
        }

        private void OnBusinessOpened(int day, int balance)
        {
            businessOpenedAt = Time.time;
            Write(new ShopMoneyLogEvent { balance = balance }, "shop_opened", day: day);
        }
        private void OnManualCloseRequested(int day, int balance) =>
            Write(new ShopMoneyLogEvent { balance = balance }, "shop_close_requested", day: day);
        private void OnShopClosing(bool manual)
        {
            Write(new ShopClosedLogEvent
            {
                manual = manual, balance = dayManager.Money, guestsServed = dayManager.CompletedGuests,
                guestsRemaining = Mathf.Max(0, dayManager.DailyGuestCount - dayManager.CompletedGuests),
                businessSeconds = businessOpenedAt >= 0f ? Time.time - businessOpenedAt : -1f
            }, "shop_closed");
            businessOpenedAt = -1f;
        }
        private void OnOrderDeferred(CustomerProfile order, int fromDay, string reason) =>
            Write(new ShopOrderDeferredLogEvent { profileId = order != null ? order.ProfileId ?? "" : "", fromDay = fromDay, reason = reason },
                "order_deferred", order, fromDay);

        // Purchases count only during the day (tutorial day 0 included; the day field tells them apart).
        private void OnPartPurchased(string partKind, int money, int credits) => WritePart("part_purchased", partKind, money, credits);
        private void OnPartRefunded(string partKind, int money, int credits) => WritePart("part_refunded", partKind, money, credits);
        private void WritePart(string type, string partKind, int money, int credits)
        {
            if (CanRecordAction) Write(new ShopPartLogEvent
            {
                partKind = partKind, money = money, moduleCredits = credits,
                balanceAfter = wallet.Balance, creditsAfter = wallet.ModuleCredits
            }, type, ActiveOrder);
        }
        private void OnPurchaseFailed(bool moduleCredits, int need, int have, string partKind)
        {
            if (CanRecordAction) Write(new ShopPurchaseFailedLogEvent
            { currency = moduleCredits ? "moduleCredits" : "money", need = need, have = have, partKind = partKind }, "purchase_failed", ActiveOrder);
        }

        private void OnLoanTaken(int day)
        {
            if (!CanRecord) return;
            CustomerProfile order = ActiveOrder;
            Write(new ShopLoanLogEvent
            {
                time = Time.realtimeSinceStartup,
                period = dayManager.CurrentPeriod.ToString(), isNight = dayManager.IsNight,
                profileId = order != null ? order.ProfileId ?? "" : "",
                amount = PlayerWallet.LoanAmount,
                interest = (int)((long)dayManager.NextSettlementRent * wallet.OutstandingLoans / 5),
                outstandingLoans = wallet.OutstandingLoans,
                balanceAfter = wallet.Balance, completedGuests = dayManager.CompletedGuests
            }, "loan_taken", order, day);
        }

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
            int basePrice = weapon != null ? weapon.PaidConstructionCost : 0;
            CaptureRequests(order, weapon, out string[] met, out string[] failed);
            var quote = CustomerRewardInfo.Calculate(order, weapon,
                CustomerRewardInfo.SaleMultiplier(FindFirstObjectByType<GameSaveController>()));
            Write(new ShopSaleLogEvent
            {
                schemaVersion = 2, generatedDay = order.OrderState?.generatedDay ?? dayManager.DayNumber,
                rewardBeforeMultiplier = quote.baseReward, saleMultiplier = quote.multiplier,
                difficultCustomer = order.IsDifficult,
                price = price, satisfied = satisfied, tagCount = tagCount, basePrice = basePrice, bonus = price - basePrice,
                metRequestIds = met, failedRequestIds = failed,
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

        // Same canonical ids as order_created.requestIds.
        private static void CaptureRequests(CustomerProfile order, EquipableWeapon weapon, out string[] met, out string[] failed)
        {
            var metList = new List<string>();
            var failedList = new List<string>();
            try
            {
                var table = TableManager.Instance.CustomerRequest;
                var snapshot = weapon != null ? new CustomerRequestInfo.WeaponSnapshot(weapon) : null;
                foreach (string id in order != null ? order.RequestIds : Array.Empty<string>())
                {
                    string canonical = table.FindRequest(id)?.id ?? id;
                    (snapshot != null && table.MeetsRequest(id, snapshot) ? metList : failedList).Add(canonical);
                }
            }
            catch (Exception) { metList.Clear(); failedList.Clear(); }
            met = metList.ToArray();
            failed = failedList.ToArray();
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
