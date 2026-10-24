using System;

namespace WeaponWorkshop
{
    // Resources/CustomerOrderData.json을 그대로 옮긴 직렬화 자료다. 여기에 로직을 두지 않는다.
    // 조회·검증은 CustomerOrderInfo, 로딩과 보유는 TableManager가 담당한다.
    // 일차의 손님 순서와 손님 에셋(프리팹·초상화·안내)은 CustomerDay·CustomerProfile SO가 연결하고, 값은 이 표가 정한다.
    [Serializable]
    public sealed class CustomerOrderData
    {
        [Serializable] public sealed class Sale
        {
            // 진상에게 총을 제출할 때의 제작비 회수 비율. 일반 주문은 요청별 정액을 합산한다.
            public float difficultCustomerRatio = 0.5f;
        }
        [Serializable] public sealed class PoolRequest
        {
            public string id;
            public int unlockDay = 1;
            public string[] excludes = Array.Empty<string>();
        }
        [Serializable] public sealed class Pool
        {
            public string id;
            public int minRequests = 1, maxRequests = 3;
            public PoolRequest[] requests = Array.Empty<PoolRequest>();
            public string[] orderTemplates = Array.Empty<string>();
            public string[] multiRequestTemplates = Array.Empty<string>();
            public string[] happyLines = Array.Empty<string>();
            public string[] angryLines = Array.Empty<string>();
        }
        [Serializable] public sealed class Day
        {
            public int day;
            public int rent;
            public bool useRequestPool;
            public int extraGenerated;
            public int minRequests = 1, maxRequests = 2;
        }
        [Serializable] public sealed class Guest
        {
            // 손님 SO의 profileId(에셋 GUID).
            public string id, name;
            public string[] requestIds = Array.Empty<string>();
            public bool isDifficult;
            public string[] lines = Array.Empty<string>();
            public bool endsClear = true;
            public string happyLine, angryLine;
            public int unlockDay;
        }
        public Sale sale = new Sale();
        public Pool[] pools = Array.Empty<Pool>();
        public Day[] days = Array.Empty<Day>();
        public Guest[] guests = Array.Empty<Guest>();
    }
}
