using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 요구사항 모듈(GunArchetype)을 무작위로 조합해 일반 손님을 만든다. 무한 모드에서 사용.
    [CreateAssetMenu(menuName = "Weapon Workshop/Customer Request Pool")]
    public sealed class CustomerRequestPool : ScriptableObject
    {
        [SerializeField, HideInInspector] private string poolId;
        public string PoolId => poolId;
        public string[] requestIds = System.Array.Empty<string>();
        [Tooltip("조합 재료. 각 요구사항의 request 문구가 주문 대사에 들어감")]
        [System.Obsolete("레거시 후보 목록. requestIds와 TableManager.CustomerRequest를 사용하세요."), HideInInspector]
        public GunArchetype[] requests;
        [SerializeField, Tooltip("코어 기본 스탯으로 이미 만족하는 주문 조건은 모듈 해금을 요구하지 않음")]
        private CoreBaseStats coreBaseStats;
        public WeaponStats CoreStats => coreBaseStats != null ? coreBaseStats.Stats : default;
        [Min(1)] public int minRequests = 1;
        [Min(1)] public int maxRequests = 3;
        [TextArea, Tooltip("{0} 자리에 요구사항 문구들이 ', '로 이어져 들어감")]
        public string[] orderTemplates = { "총 하나 주세요. 조건은 {0}." };
        [TextArea, Tooltip("요구사항이 2개 이상일 때만 추가로 쓰는 문구 (예: '이거 다 들어간 총 있죠?')")]
        public string[] multiRequestTemplates = new string[0];
        [TextArea] public string[] happyLines = { "딱 제가 원하던 거예요!" };
        [TextArea] public string[] angryLines = { "이거 제가 말한 거 아닌데요..." };
        public Sprite[] portraits;
        [Min(0)] public int basePrice = 150;
        [Min(0)] public int pricePerRequest = 50;

        public CustomerProfile Create() => Create(int.MaxValue, minRequests, maxRequests);
        public CustomerProfile Create(PlayerModuleUnlocks unlocks) => Create(int.MaxValue, minRequests, maxRequests, unlocks);

        // day 일차에 해금된 요구사항만 min~max개 조합.
        // moduleCredits: 보유 모듈 재화. 조합 전체의 필요 재화가 이 안에 들도록 고름.
        public CustomerProfile Create(int day, int min, int max, PlayerModuleUnlocks unlocks = null, int moduleCredits = int.MaxValue)
        {
            var table = TableManager.Instance.CustomerRequest;
            var picked = new List<string>();
            var candidates = new List<string>();
            foreach (string id in requestIds ?? System.Array.Empty<string>())
                if (CustomerModuleRequirements.Allows(new[] { id }, unlocks, CoreStats, day) &&
                    CustomerModuleRequirements.CreditCost(new[] { id }, unlocks, CoreStats) <= moduleCredits) candidates.Add(id);
            int count = Mathf.Min(Random.Range(min, Mathf.Max(min, max) + 1), candidates.Count);
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int index = Random.Range(0, candidates.Count);
                string next = candidates[index];
                picked.Add(next);
                candidates.RemoveAt(index);
                // 같이 만족할 수 없는 요구사항은 후보에서 뺀다 (예: 권총 태그 + 스코프).
                candidates.RemoveAll(c => table.Conflicts(next, c));
                candidates.RemoveAll(c => CreditCost(picked, c, unlocks) > moduleCredits);
            }

            var phrases = new List<string>();
            foreach (string request in picked) phrases.Add(table.Phrase(request));

            CustomerProfile profile = CreateInstance<CustomerProfile>();
            profile.name = "Endless_" + string.Join("+", phrases);
            profile.lines = new[] { phrases.Count == 0 ? "기본 총 하나 주세요. 특별한 조건은 없어요."
                : string.Format(Pick(picked.Count > 1 && multiRequestTemplates != null
                    ? System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(orderTemplates ?? new string[0], multiRequestTemplates))
                    : orderTemplates, "{0} 주세요."), string.Join(", ", phrases)) };
            profile.endsClear = true;
            profile.requestIds = picked.ToArray();
            profile.happyLine = Pick(happyLines, null);
            profile.angryLine = Pick(angryLines, null);
            profile.portrait = Pick(portraits, null);
            profile.price = basePrice + pricePerRequest * picked.Count;
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
