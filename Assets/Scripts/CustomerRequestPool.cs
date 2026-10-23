using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 손님 풀의 요구사항을 무작위로 조합해 일반 손님을 만든다. 무한 모드에서 사용.
    // 후보·등장 일차·조합 제외·대사 틀·가격은 주문 표(TableManager.CustomerOrder)의 손님 풀 행(poolId)에서 읽고,
    // 이 SO는 에셋 연결(초상화·코어 기본 스탯)만 갖는다.
    [CreateAssetMenu(menuName = "Weapon Workshop/Customer Request Pool")]
    public sealed class CustomerRequestPool : ScriptableObject
    {
        [SerializeField, HideInInspector] private string poolId;
        public string PoolId => poolId;
        [Tooltip("조합 재료. 각 요구사항의 request 문구가 주문 대사에 들어감")]
        [System.Obsolete("레거시 후보 목록. TableManager.CustomerOrder의 손님 풀을 사용하세요."), HideInInspector]
        public GunArchetype[] requests;
        [SerializeField, Tooltip("코어 기본 스탯으로 이미 만족하는 주문 조건은 모듈 해금을 요구하지 않음")]
        private CoreBaseStats coreBaseStats;
        public WeaponStats CoreStats => coreBaseStats != null ? coreBaseStats.Stats : default;
        public Sprite[] portraits;

        private CustomerOrderData.Pool Row => TableManager.Instance.CustomerOrder.FindPool(poolId) ?? new CustomerOrderData.Pool();
        public int MinRequests => Row.minRequests;
        public int MaxRequests => Row.maxRequests;

        // day 일차에 등장할 수 있는 요구사항만 min~max개 조합.
        // moduleCredits: 보유 모듈 재화. 조합 전체의 필요 재화가 이 안에 들도록 고름.
        public CustomerProfile Create(int day, int min, int max, PlayerModuleUnlocks unlocks = null, int moduleCredits = int.MaxValue)
        {
            var table = TableManager.Instance.CustomerRequest;
            var pool = Row;
            var picked = new List<string>();
            var candidates = new List<string>();
            foreach (var entry in pool.requests ?? System.Array.Empty<CustomerOrderData.PoolRequest>())
            {
                string id = table.FindRequest(entry?.id)?.id;
                if (id == null || entry.unlockDay > day || candidates.Contains(id)) continue;
                if (CustomerModuleRequirements.Allows(new[] { id }, unlocks, CoreStats) &&
                    CustomerModuleRequirements.CreditCost(new[] { id }, unlocks, CoreStats) <= moduleCredits) candidates.Add(id);
            }
            int count = Mathf.Min(Random.Range(min, Mathf.Max(min, max) + 1), candidates.Count);
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int index = Random.Range(0, candidates.Count);
                string next = candidates[index];
                picked.Add(next);
                candidates.RemoveAt(index);
                // 같이 만족할 수 없는 요구사항은 후보에서 뺀다 (예: 권총 태그 + 스코프).
                candidates.RemoveAll(c => pool.Conflicts(next, c));
                candidates.RemoveAll(c => CreditCost(picked, c, unlocks) > moduleCredits);
            }

            var phrases = new List<string>();
            foreach (string request in picked) phrases.Add(table.Phrase(request));

            CustomerProfile profile = CreateInstance<CustomerProfile>();
            profile.name = "Endless_" + string.Join("+", phrases);
            var templates = picked.Count > 1 && pool.multiRequestTemplates != null && pool.multiRequestTemplates.Length > 0
                ? System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(pool.orderTemplates ?? new string[0], pool.multiRequestTemplates))
                : pool.orderTemplates;
            profile.Lines = new[] { phrases.Count == 0 ? "기본 총 하나 주세요. 특별한 조건은 없어요."
                : string.Format(Pick(templates, "{0} 주세요."), string.Join(", ", phrases)) };
            profile.EndsClear = true;
            profile.RequestIds = picked.Count == 0 ? new[] { CustomerRewardInfo.BasicRequestId } : picked.ToArray();
            profile.HappyLine = Pick(pool.happyLines, null);
            profile.AngryLine = Pick(pool.angryLines, null);
            profile.portrait = Pick(portraits, null);
            return profile;
        }

        // 이미 고른 요구사항에 extra를 더했을 때의 필요 모듈 재화.
        private int CreditCost(List<string> picked, string extra, PlayerModuleUnlocks unlocks)
        {
            return CustomerModuleRequirements.CreditCost(new List<string>(picked) { extra }.ToArray(), unlocks, CoreStats);
        }

        private static T Pick<T>(T[] items, T fallback)
            => items != null && items.Length > 0 ? items[Random.Range(0, items.Length)] : fallback;
    }
}
