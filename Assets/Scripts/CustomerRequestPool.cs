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
        [System.Serializable]
        public sealed class PortraitAsset
        {
            [Tooltip("에셋 GUID. 주문 표의 초상화 규칙 키와 같다.")]
            public string portraitId;
            public Sprite sprite;
        }

        [SerializeField, HideInInspector] private string poolId;
        public string PoolId => poolId;
        [Tooltip("조합 재료. 각 요구사항의 request 문구가 주문 대사에 들어감")]
        [System.Obsolete("레거시 후보 목록. TableManager.CustomerOrder의 손님 풀을 사용하세요."), HideInInspector]
        public GunArchetype[] requests;
        [SerializeField, Tooltip("코어 기본 스탯으로 이미 만족하는 주문 조건은 모듈 해금을 요구하지 않음")]
        private CoreBaseStats coreBaseStats;
        public WeaponStats CoreStats => coreBaseStats != null ? coreBaseStats.Stats : default;
        public PortraitAsset[] portraits;

        private CustomerOrderData.Pool Row => TableManager.Instance.CustomerOrder.FindPool(poolId) ?? new CustomerOrderData.Pool();
        public int MinRequests => Row.minRequests;
        public int MaxRequests => Row.maxRequests;

        public CustomerProfile Create(int day, int min, int max, PlayerModuleUnlocks unlocks = null,
            int money = int.MaxValue, bool canTakeLoan = false)
        {
            money = Mathf.Max(0, money);
            var table = TableManager.Instance.CustomerRequest;
            var pool = Row;
            var candidates = new List<string>();
            var bundles = new Dictionary<string, List<string>>();
            foreach (var entry in pool.requests ?? System.Array.Empty<CustomerOrderData.PoolRequest>())
            {
                string id = table.FindRequest(entry?.id)?.id;
                if (id == null || entry.unlockDay > day || candidates.Contains(id)) continue;
                if (!string.IsNullOrEmpty(entry.requiresTagId) && !TagExperience.Has(entry.requiresTagId)) continue;
                if (!pool.TryRequestBundle(id, day, out var bundle)) continue;
                if (CustomerModuleRequirements.Allows(bundle.ToArray(), unlocks, CoreStats))
                { candidates.Add(id); bundles.Add(id, bundle); }
            }
            var tags = candidates.FindAll(id => !string.IsNullOrEmpty(table.FindRequest(id).tagId));
            var optional = candidates.FindAll(id => string.IsNullOrEmpty(table.FindRequest(id).tagId));
            // 모든 조합을 섞은 뒤 처음 만족하는 조합을 고른다. 전체 조합을 매번 검사하면 손님 한 명을 만드는 데 수 초가 걸리므로,
            // 유효 조합이 앞에 나오면 조기 종료하고 무작위 순서 덕분에 유효 조합 사이의 분포는 균등하게 유지된다.
            var combinations = new List<string[]>(tags.Count * optional.Count * optional.Count);
            foreach (string tag in tags)
                for (int first = 0; first < optional.Count; first++)
                    for (int second = first + 1; second < optional.Count; second++)
                        combinations.Add(new[] { tag, optional[first], optional[second] });
            for (int index = combinations.Count - 1; index > 0; index--)
            {
                int swapIndex = Random.Range(0, index + 1);
                var swap = combinations[index];
                combinations[index] = combinations[swapIndex];
                combinations[swapIndex] = swap;
            }
            string[] picked = null;
            foreach (var ids in combinations)
            {
                if (!CanCombine(ids)) continue;
                picked = ids;
                break;
            }
            if (picked == null)
            {
                Debug.LogError($"Customer request pool '{poolId}' has no feasible tag + two distinct optional requests for day {day}.", this);
                return null;
            }

            bool CanCombine(string[] ids)
            {
                var mandatory = table.FindRequest(ids[0]);
                var firstOptional = table.FindRequest(ids[1]);
                var secondOptional = table.FindRequest(ids[2]);
                if (mandatory == null || firstOptional == null || secondOptional == null) return false;
                bool firstIsTest = firstOptional.IsShootingTestRequest();
                bool secondIsTest = secondOptional.IsShootingTestRequest();
                if (firstIsTest && secondIsTest) return false;
                bool firstHasTarget = table.HasMuzzleOrSplitCondition(firstOptional);
                bool secondHasTarget = table.HasMuzzleOrSplitCondition(secondOptional);
                if (table.HasMuzzleOrSplitCondition(mandatory) || firstHasTarget || secondHasTarget)
                {
                    if (!firstIsTest && !secondIsTest) return false;
                    if (firstHasTarget && secondHasTarget) return false;
                }
                foreach (string id in ids)
                    foreach (string required in bundles[id])
                        if (System.Array.IndexOf(ids, required) < 0) return false;
                for (int index = 1; index < ids.Length; index++)
                {
                    var optionalRequest = table.FindRequest(ids[index]);
                    foreach (var optionalCondition in table.Conditions(optionalRequest))
                        foreach (var tagCondition in table.Conditions(mandatory))
                            if (optionalCondition.Measure == tagCondition.Measure &&
                                optionalCondition.Stat == tagCondition.Stat &&
                                optionalCondition.min <= tagCondition.min &&
                                optionalCondition.max >= tagCondition.max &&
                                optionalRequest.conditions.Length == 1) return false;
                }
                if (firstOptional.conditions.Length == 1 && secondOptional.conditions.Length == 1)
                {
                    var firstCondition = firstOptional.conditions[0];
                    var secondCondition = secondOptional.conditions[0];
                    if (firstCondition.Measure == secondCondition.Measure && firstCondition.Stat == secondCondition.Stat &&
                        firstCondition.colorHex == secondCondition.colorHex &&
                        ((firstCondition.min <= secondCondition.min && firstCondition.max >= secondCondition.max) ||
                         (secondCondition.min <= firstCondition.min && secondCondition.max >= firstCondition.max))) return false;
                }
                for (int first = 0; first < ids.Length; first++)
                    for (int second = first + 1; second < ids.Length; second++)
                    {
                        if (pool.Conflicts(ids[first], ids[second])) return false;
                        if (!ConditionsOverlap(table.FindRequest(ids[first]), table.FindRequest(ids[second]))) return false;
                    }
                return CustomerModuleRequirements.IsFeasible(ids, unlocks, CoreStats, out int dollars) &&
                    (canTakeLoan || dollars <= money);
            }

            // 같은 측정값의 조건 범위가 겹치지 않으면 동시에 만족할 수 없는 주문이다.
            // 예: 제작비 3000 초과 + 1500 이하. 색 조건은 부위별로 따로 칠할 수 있어 제외한다.
            bool ConditionsOverlap(CustomerRequestData.Request first, CustomerRequestData.Request second)
            {
                foreach (var a in first.conditions ?? System.Array.Empty<CustomerRequestData.Condition>())
                    foreach (var b in second.conditions ?? System.Array.Empty<CustomerRequestData.Condition>())
                    {
                        if (a == null || b == null || a.Measure != b.Measure || a.Stat != b.Stat) continue;
                        if (a.Measure == GunMeasure.PaintedColorFaces || a.Measure == GunMeasure.Shape) continue;
                        double lower = System.Math.Max(a.min, b.min);
                        double upper = System.Math.Min(a.max, b.max);
                        bool lowerExclusive = (a.min >= b.min && a.minExclusive) || (b.min >= a.min && b.minExclusive);
                        bool upperExclusive = (a.max <= b.max && a.maxExclusive) || (b.max <= a.max && b.maxExclusive);
                        if (lower < upper || (lower == upper && !lowerExclusive && !upperExclusive)) continue;
                        return false;
                    }
                return true;
            }

            var phrases = new List<string>();
            foreach (string request in picked)
                phrases.Add("<b>" + (string.IsNullOrEmpty(table.FindRequest(request).tagId)
                    ? table.FindRequest(request).name : table.Phrase(request)) + "</b>");

            CustomerProfile profile = CreateInstance<CustomerProfile>();
            profile.name = "Endless_" + string.Join("+", phrases);
            var templates = pool.multiRequestTemplates != null && pool.multiRequestTemplates.Length > 0
                ? System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(pool.orderTemplates ?? new string[0], pool.multiRequestTemplates))
                : pool.orderTemplates;
            profile.Lines = new[] { phrases.Count == 0 ? "기본 총 하나 주세요. 특별한 조건은 없어요."
                : string.Format(Pick(templates, "{0} 주세요."), string.Join(", ", phrases)) };
            profile.EndsClear = true;
            profile.RequestIds = picked;
            profile.HappyLine = Pick(pool.happyLines, null);
            profile.AngryLine = Pick(pool.angryLines, null);
            profile.portrait = PickPortrait(pool, table.FindRequest(picked[0]).tagId, picked);
            return profile;
        }

        public int IndexOfPortrait(Sprite sprite)
        {
            if (portraits == null) return -1;
            for (int index = 0; index < portraits.Length; index++)
                if (portraits[index] != null && portraits[index].sprite == sprite) return index;
            return -1;
        }

        public Sprite PortraitAt(int index)
            => portraits != null && index >= 0 && index < portraits.Length && portraits[index] != null
                ? portraits[index].sprite : null;

        private Sprite PickPortrait(CustomerOrderData.Pool pool, string tagId, string[] picked)
        {
            var rules = pool?.portraitRules ?? System.Array.Empty<CustomerOrderData.PortraitRule>();
            bool exclusive = false;
            foreach (var rule in rules)
                if (rule != null && Contains(rule.exclusiveTagIds, tagId)) { exclusive = true; break; }

            var candidates = new List<Sprite>();
            foreach (var asset in portraits ?? System.Array.Empty<PortraitAsset>())
            {
                if (asset == null || asset.sprite == null) continue;
                var rule = FindRule(rules, asset.portraitId);
                // 이 태그를 독점하는 초상화가 있으면 그 초상화만 쓴다. 예: 화염방사기 → 마법사·불장난.
                if (exclusive)
                {
                    if (rule != null && Contains(rule.exclusiveTagIds, tagId)) candidates.Add(asset.sprite);
                }
                else if (rule == null || Matches(rule, tagId, picked)) candidates.Add(asset.sprite);
            }
            if (candidates.Count == 0)
                foreach (var asset in portraits ?? System.Array.Empty<PortraitAsset>())
                    if (asset != null && asset.sprite != null) candidates.Add(asset.sprite);
            return Pick(candidates.ToArray(), null);
        }

        private static CustomerOrderData.PortraitRule FindRule(CustomerOrderData.PortraitRule[] rules, string portraitId)
        {
            if (string.IsNullOrEmpty(portraitId)) return null;
            foreach (var rule in rules)
                if (rule != null && rule.portraitId == portraitId) return rule;
            return null;
        }

        private static bool Matches(CustomerOrderData.PortraitRule rule, string tagId, string[] picked)
        {
            if (rule.tagIds != null && rule.tagIds.Length > 0 && !Contains(rule.tagIds, tagId)) return false;
            foreach (string required in rule.requiresRequestIds ?? System.Array.Empty<string>())
                if (System.Array.IndexOf(picked, required) < 0) return false;
            return true;
        }

        private static bool Contains(string[] ids, string id)
        {
            foreach (string candidate in ids ?? System.Array.Empty<string>())
                if (candidate == id) return true;
            return false;
        }

        private static T Pick<T>(T[] items, T fallback)
            => items != null && items.Length > 0 ? items[Random.Range(0, items.Length)] : fallback;
    }
}
