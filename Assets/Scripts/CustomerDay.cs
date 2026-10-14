using UnityEngine;

namespace WeaponWorkshop
{
    // 하루치 손님 순서. 날짜 시스템에서 bell.SetCustomers(day.customers)로 사용.
    [CreateAssetMenu(menuName = "Weapon Workshop/Customer Day")]
    public sealed class CustomerDay : ScriptableObject
    {
        public int day = 1;
        [Tooltip("해당 일차 시작 시 실행할 안내. 0일차는 빈 데이터부터 직접 작성할 수 있습니다.")]
        public TutorialSequence startTutorial;
        [Min(0), Tooltip("그날 밤 정산 때 빠져나가는 월세")]
        public int rent;
        public CustomerProfile[] customers;
        [Tooltip("끄면 손님 목록 그대로 (1일차 튜토리얼 등)")]
        public bool useRequestPool = true;
        [Min(0), Tooltip("목록에 더해 끼워 넣을 조합 손님 수. '예?' 손님은 그 사이에 고르게 배치됨")]
        public int extraGenerated;
        [Min(1), Tooltip("일반 손님 자리는 요구사항 조합 손님으로 바뀜. 조합 개수 범위")]
        public int minRequests = 1;
        [Min(1)] public int maxRequests = 2;
    }
}
