using UnityEngine;

namespace WeaponWorkshop
{
    // 하루치 손님 순서. 날짜 시스템에서 bell.SetCustomers(day.customers)로 사용.
    // 손님 순서·에셋 연결만 이 SO가 갖고, 자리세·조합 손님 설정은 주문 표(TableManager.CustomerOrder)의 일차 행(day)에서 읽는다.
    [CreateAssetMenu(menuName = "Weapon Workshop/Customer Day")]
    public sealed class CustomerDay : ScriptableObject
    {
        public int day = 1;
        [Tooltip("해당 일차 시작 시 실행할 안내. 0일차는 빈 데이터부터 직접 작성할 수 있습니다.")]
        public TutorialSequence startTutorial;
        public CustomerProfile[] customers;

        // 무한 모드 일차·저장 복원 일차는 표 행이 없거나 값이 달라 런타임 값을 쓴다.
        [System.NonSerialized] private CustomerOrderData.Day runtimeDay;
        private CustomerOrderData.Day Row => runtimeDay ?? TableManager.Instance.CustomerOrder.FindDay(day) ?? NoDay;
        private static readonly CustomerOrderData.Day NoDay = new CustomerOrderData.Day();
        private CustomerOrderData.Day Writable => runtimeDay ??= CustomerOrderInfo.Copy(Row == NoDay ? null : Row);
        /// <summary>그날 밤 정산 때 빠져나가는 자리세.</summary>
        public int Rent { get => Row.rent; set => Writable.rent = value; }
        /// <summary>끄면 손님 목록 그대로 (1일차 튜토리얼 등).</summary>
        public bool UseRequestPool => Row.useRequestPool;
        /// <summary>목록에 더해 끼워 넣을 조합 손님 수.</summary>
        public int ExtraGenerated => Row.extraGenerated;
        public int MinRequests { get => Row.minRequests; set => Writable.minRequests = value; }
        public int MaxRequests { get => Row.maxRequests; set => Writable.maxRequests = value; }
        // Instantiate는 직렬화 필드만 복사하므로 런타임 값은 여기서 옮긴다.
        public void CopyRuntimeFrom(CustomerDay source)
            => runtimeDay = source != null && source.runtimeDay != null ? CustomerOrderInfo.Copy(source.runtimeDay) : null;
    }
}
