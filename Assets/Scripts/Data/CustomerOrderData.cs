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
            // 만족 판매: 제작비 × (1 + tagBonusRatio × 보너스 요청 수) + 손님 보상. 불만족: 제작비 × dissatisfiedRatio.
            public float tagBonusRatio = 0.2f;
            public float dissatisfiedRatio = 0.8f;
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
            public int basePrice, pricePerRequest;
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
            public string[] noBonusRequestIds = Array.Empty<string>();
            public string[] bonusRequestIds = Array.Empty<string>();
            public int price;
            public string[] lines = Array.Empty<string>();
            public bool endsClear = true;
            public string happyLine, angryLine;
            public string tutorialRejectionLine;
            public string[] tutorialFollowupRequestIds = Array.Empty<string>();
            public int unlockDay;
        }
        public Sale sale = new Sale();
        public Pool[] pools = Array.Empty<Pool>();
        public Day[] days = Array.Empty<Day>();
        public Guest[] guests = Array.Empty<Guest>();
    }
}
