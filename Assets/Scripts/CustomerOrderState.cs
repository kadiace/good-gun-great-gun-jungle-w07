using System;

namespace WeaponWorkshop
{
    // One runtime order, independent of its reusable customer profile and LogKit session.
    [Serializable]
    public sealed class CustomerOrderState
    {
        public string orderId;
        public bool creationLogged, completionLogged;
        [NonSerialized] public bool restored;

        public static CustomerOrderState Create(bool restored = false) => new CustomerOrderState
        { orderId = Guid.NewGuid().ToString("N"), restored = restored };

        public CustomerOrderState Copy(bool fromSave = false) => new CustomerOrderState
        {
            orderId = orderId, creationLogged = creationLogged, completionLogged = completionLogged,
            restored = fromSave || restored
        };
    }
}
