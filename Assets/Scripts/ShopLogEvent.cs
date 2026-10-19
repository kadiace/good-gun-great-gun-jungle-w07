using System;

namespace WeaponWorkshop
{
    // LogKit supplies timestampUtc, sessionId and scene outside dataJson.
    [Serializable]
    public class ShopLogEvent
    {
        public int schemaVersion = 1;
        public string eventType, eventId, orderId = "";
        public int day;
    }

    [Serializable] public sealed class ShopMoneyLogEvent : ShopLogEvent { public int balance; }
    [Serializable] public sealed class ShopLoanLogEvent : ShopLogEvent
    {
        public float time;
        public string period, profileId = "";
        public bool isNight;
        public int amount, interest, outstandingLoans, balanceAfter, completedGuests;
    }
    [Serializable] public sealed class ShopOrderLinkLogEvent : ShopLogEvent
    { public string profileId; public string[] requestIds; }
    [Serializable] public class ShopWeaponLogEvent : ShopLogEvent
    { public string weaponCode = "", weaponCodeStatus = "no_weapon"; }
    [Serializable] public sealed class ShopPresetLogEvent : ShopWeaponLogEvent { public int slot; }
    [Serializable] public sealed class ShopOrderCompletedLogEvent : ShopLogEvent
    { public bool success, customerDead; }
    [Serializable] public sealed class ShopSaleLogEvent : ShopWeaponLogEvent
    {
        public int price, tagCount; public bool satisfied;
        // price = basePrice(낸 제작비) + bonus. 불만족 판매면 bonus는 음수(할인).
        public int basePrice, bonus;
        public string[] metRequestIds, failedRequestIds;
    }
    // part_purchased / part_refunded. money·moduleCredits는 이번에 낸(돌려받은) 양.
    [Serializable] public sealed class ShopPartLogEvent : ShopLogEvent
    { public string partKind = ""; public int money, moduleCredits, balanceAfter, creditsAfter; }
    [Serializable] public sealed class ShopPurchaseFailedLogEvent : ShopLogEvent
    { public string currency, partKind = ""; public int need, have; }
    [Serializable] public sealed class ShopOrderDeferredLogEvent : ShopLogEvent
    { public string profileId, reason; public int fromDay; }
    [Serializable] public sealed class ShopClosedLogEvent : ShopLogEvent
    { public bool manual; public int balance, guestsServed, guestsRemaining; public float businessSeconds; }
    [Serializable] public sealed class ShopWeaponFiredLogEvent : ShopLogEvent
    { public float time; public UnityEngine.Vector3 position; public string weaponAssemblyId = "", weaponAssemblyIdStatus = "no_weapon"; }
    [Serializable] public sealed class ShopSlotMachineLogEvent : ShopLogEvent
    {
        public float playSeconds, secondsSinceDayNightTransition;
        public string period, oddsPool, plannedSymbol;
        public bool isNight;
        public int introductorySpinsUsed, balanceBefore, balanceAfter, price, plannedPrize;
        public int[] reelSymbols;
    }
}
