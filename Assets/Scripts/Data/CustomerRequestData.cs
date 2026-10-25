using System;
using UnityEngine;

namespace WeaponWorkshop
{
    // Serialization only. TableManager owns the instance; CustomerRequestInfo owns queries and rules.
    // 요청 행은 무기 조건만 담는다. 등장 일차·조합 제외는 주문 표(CustomerOrderData)의 손님 풀, 판매 보너스 예외는 손님 행이 정한다.
    [Serializable]
    public sealed class CustomerRequestData
    {
        [Serializable] public sealed class Condition
        {
            public string id, label;
            // enum 이름(GunMeasure·StatType). 로드 때 CustomerRequestInfo.Validate가 아래 값으로 해석한다.
            public string measure, stat;
            public string shapeId, colorHex;
            public float colorTolerance;
            public float min, max = float.MaxValue;
            public bool maxExclusive, minExclusive;
            [NonSerialized] public GunMeasure Measure;
            [NonSerialized] public StatType Stat;
            [NonSerialized] public bool Parsed;
        }
        [Serializable] public sealed class Request
        {
            public string id, name, phrase, tagId;
            public string[] legacyIds = Array.Empty<string>();
            // 기획자가 직접 편집하는 정액. 초기 최소 제작비 × 1.5는 자동 재계산하지 않는다.
            public int reward;
            // 테스트 요청은 주문 생성 일차 × 이 값. 일반 요청은 0.
            public int rewardPerDay;
            public string[] requiredTestIds = Array.Empty<string>();
            public Condition[] conditions = Array.Empty<Condition>();
        }
        [Serializable] public sealed class Tag
        {
            public string id, name;
            public Color color;
            public float sellMultiplier = 1f;
            public Condition[] conditions = Array.Empty<Condition>();
        }
        [Serializable] public sealed class LegacySlot
        {
            public string poolId, requestId;
            public int index;
        }
        public Request[] requests = Array.Empty<Request>();
        public Tag[] tags = Array.Empty<Tag>();
        public LegacySlot[] legacySlots = Array.Empty<LegacySlot>();
    }
}
