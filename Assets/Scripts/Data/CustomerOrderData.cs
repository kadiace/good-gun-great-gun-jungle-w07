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
            // 일반 주문에서 요청을 하나도 충족하지 못했을 때(대실패) 제작비에 곱하는 지급 비율. 클리어 배율은 적용하지 않는다.
            public float failedSaleRatio = 0.5f;
        }
        [Serializable] public sealed class PoolRequest
        {
            public string id;
            public int unlockDay = 1;
            public string requiresTagId;
            public string[] excludes = Array.Empty<string>();
            // 이 요청을 뽑으면 함께 붙이는 요청. 각 항목의 일차·해금·비용도 함께 적용한다.
            public string[] requiredRequestIds = Array.Empty<string>();
        }
        [Serializable] public sealed class PortraitRule
        {
            // 손님 풀 SO에 연결한 초상화 에셋 GUID. 같은 값을 SO의 PortraitAsset.portraitId에 둔다.
            public string portraitId;
            // 이 태그가 주문에 있을 때만 후보가 된다. 비우면 태그로 제한하지 않는다.
            public string[] tagIds = Array.Empty<string>();
            // 이 태그가 주문에 있으면 해당 초상화만 후보가 된다. 다른 초상화는 같은 태그에서 노출되지 않는다.
            public string[] exclusiveTagIds = Array.Empty<string>();
            public string[] requiresRequestIds = Array.Empty<string>();
        }
        [Serializable] public sealed class Pool
        {
            public string id;
            public int minRequests = 1, maxRequests = 3;
            public PoolRequest[] requests = Array.Empty<PoolRequest>();
            public PortraitRule[] portraitRules = Array.Empty<PortraitRule>();
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
            public string tutorialRejectionLine;
            public string[] tutorialFollowupRequestIds = Array.Empty<string>();
            public int unlockDay;
        }
        public Sale sale = new Sale();
        [Serializable] public sealed class MandatoryDialogue
        {
            public string requirementId, intro, introResponse, explicitRequest;
        }
        [Serializable] public sealed class OptionalDialogueVariant
        {
            public string hint, question, explicitRequest;
        }
        [Serializable] public sealed class OptionalDialogue
        {
            public string requirementId;
            public OptionalDialogueVariant[] variants = Array.Empty<OptionalDialogueVariant>();
        }
        public MandatoryDialogue[] mandatoryDialogues = Array.Empty<MandatoryDialogue>();
        public OptionalDialogue[] optionalDialogues = Array.Empty<OptionalDialogue>();
        public Pool[] pools = Array.Empty<Pool>();
        public Day[] days = Array.Empty<Day>();
        public Guest[] guests = Array.Empty<Guest>();
    }
}
