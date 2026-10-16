using System;

namespace WeaponWorkshop
{
    // One runtime order, independent of its reusable customer profile and LogKit session.
    [Serializable]
    public sealed class CustomerOrderState
    {
        public string orderId;
        // 생성 로그는 세션마다 다시 보낸다. 세이브에 남기면 불러온 세션이 order_restored를 보내지 않아 주문의 손님 정보가 끊긴다.
        // 같은 주문을 다시 보내도 eventId가 orderId/order_restored로 고정이라 서버에서 중복을 거를 수 있다.
        [NonSerialized] public bool creationLogged;
        public bool completionLogged;
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
