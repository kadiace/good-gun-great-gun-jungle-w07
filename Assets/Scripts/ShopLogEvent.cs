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
    [Serializable] public sealed class ShopOrderLinkLogEvent : ShopLogEvent
    { public string profileId; public string[] requestIds; }
    [Serializable] public class ShopWeaponLogEvent : ShopLogEvent
    { public string weaponCode = "", weaponCodeStatus = "no_weapon"; }
    [Serializable] public sealed class ShopPresetLogEvent : ShopWeaponLogEvent { public int slot; }
    [Serializable] public sealed class ShopOrderCompletedLogEvent : ShopLogEvent
    { public bool success, customerDead; }
    [Serializable] public sealed class ShopSaleLogEvent : ShopLogEvent
    { public int price, tagCount; public bool satisfied; }
}
