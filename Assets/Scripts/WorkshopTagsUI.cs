using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class WorkshopTagsUI : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private GameObject panel;
        [SerializeField] private WeaponTypeData[] tags = new WeaponTypeData[0];
        [SerializeField] private TMPro.TextMeshProUGUI weightText;
        [SerializeField] private TMPro.TextMeshProUGUI salePriceText;
        [SerializeField] private TMPro.TextMeshProUGUI tagsText;
        [SerializeField] private RectTransform tagContainer;
        [SerializeField] private UnityEngine.UI.Image tagTemplate;
        [SerializeField] private TMPro.TMP_Text noTagsText;
        private readonly System.Collections.Generic.List<(UnityEngine.UI.Image image, TMPro.TMP_Text label)> tagBadges =
            new System.Collections.Generic.List<(UnityEngine.UI.Image, TMPro.TMP_Text)>();
        [SerializeField] private ShopCatalog shopCatalog;
        private readonly System.Collections.Generic.List<WeaponTypeData> matchedTags = new System.Collections.Generic.List<WeaponTypeData>();
        // 스냅샷은 조립이 바뀌면 값이 달라지므로 캐시하지 않고 갱신 주기마다 새로 만들어 태그 판정과 배지에 함께 쓴다.
        public void GetMatchedTags(PowerCore core, System.Collections.Generic.List<WeaponTypeData> result)
        {
            result.Clear();
            if (core == null || tags.Length == 0) return;
            var snapshot = new CustomerRequestInfo.WeaponSnapshot(core);
            GetMatchedTags(snapshot, result);
        }

        private void GetMatchedTags(CustomerRequestInfo.WeaponSnapshot snapshot, System.Collections.Generic.List<WeaponTypeData> result)
        {
            result.Clear();
            if (!snapshot.HasWeapon) return;
            foreach (var tag in tags)
                if (tag != null && TableManager.Instance.CustomerRequest.MeetsTag(tag.TableId, snapshot)) result.Add(tag);
        }
        private PowerCore lastCore;
        private float nextRefresh;
        private float lastWeight = float.NaN;
        private int lastPrice = int.MinValue;
        private readonly System.Collections.Generic.List<CustomerRequestData.Tag> appliedTags = new System.Collections.Generic.List<CustomerRequestData.Tag>();
        private readonly System.Collections.Generic.List<CustomerRequestData.Tag> badgeTags = new System.Collections.Generic.List<CustomerRequestData.Tag>();
        private bool badgesDrawn;

        private void Awake()
        {
            panel?.SetActive(false);
            if (tagsText != null) tagsText.text = "활성 태그";
            if (tagTemplate != null) tagTemplate.gameObject.SetActive(false);
            if (tagContainer != null) tagContainer.gameObject.SetActive(false);
            if (noTagsText != null) noTagsText.gameObject.SetActive(false);
            if (uiManager == null) uiManager = GetComponentInParent<UIManager>(true);
        }

        private void OnDisable() => panel?.SetActive(false);

        private void LateUpdate()
        {
            var player = uiManager != null ? uiManager.Player : null;
            var session = player != null ? player.GetComponent<WorkshopSession>() : null;
            var bench = session != null && session.IsEditing ? session.ActiveWorkbench : null;
            var holder = uiManager != null ? uiManager.PlayerWeaponHolder : null;
            var weapon = bench != null ? bench.CurrentWeapon : holder != null ? holder.EquippedWeapon : null;
            var core = weapon != null ? weapon.GetComponentInChildren<PowerCore>(true) : null;
            bool visible = core != null;
            if (panel != null && panel.activeSelf != visible) panel.SetActive(visible);
            // 패널이 꺼져 있거나 공방 모드가 아니면 0.1초 갱신 자체를 건너뛴다.
            if (panel == null || !panel.activeSelf || session == null || !session.IsEditing) return;
            if (!visible)
            {
                lastCore = null; nextRefresh = 0f;
                // 마지막 표시 값도 지워 다시 보일 때 현재 값으로 다시 그리도록 한다.
                lastWeight = float.NaN; lastPrice = int.MinValue;
                appliedTags.Clear(); badgesDrawn = false;
                return;
            }
            if (lastCore == core && Time.unscaledTime < nextRefresh) return;
            lastCore = core; nextRefresh = Time.unscaledTime + .1f;
            var stats = core.GetCurrentStats();
            if (weightText != null && lastWeight != stats.weight)
            {
                lastWeight = stats.weight;
                weightText.text = $"무게  {stats.weight:0.##}";
            }
            GetMatchedTags(core, matchedTags);
            if (tagContainer != null && tagTemplate != null)
            {
                // 태그는 갱신마다 판정하고(조립이 바뀌면 달라진다), 목록이 바뀔 때만 배지를 다시 그린다.
                var snapshot = new CustomerRequestInfo.WeaponSnapshot(core);
                var table = TableManager.Instance.CustomerRequest;
                badgeTags.Clear();
                foreach (var tag in table.tags)
                    if (tag != null && table.MeetsTag(tag.id, snapshot)) badgeTags.Add(tag);
                if (!badgesDrawn || !SameTags(badgeTags, appliedTags))
                {
                    badgesDrawn = true;
                    appliedTags.Clear();
                    foreach (var tag in badgeTags) appliedTags.Add(tag);
                    int used = 0;
                    foreach (var tag in badgeTags)
                    {
                        if (used == tagBadges.Count)
                        {
                            var image = Instantiate(tagTemplate, tagContainer);
                            tagBadges.Add((image, image.GetComponentInChildren<TMPro.TMP_Text>(true)));
                        }
                        var badge = tagBadges[used++];
                        badge.image.gameObject.SetActive(true);
                        badge.image.color = tag.color;
                        badge.label.text = tag.name;
                        Color linear = tag.color.linear;
                        float luminance = .2126f * linear.r + .7152f * linear.g + .0722f * linear.b;
                        badge.label.color = luminance > .179f ? Color.black : Color.white;
                    }
                    for (int i = used; i < tagBadges.Count; i++) tagBadges[i].image.gameObject.SetActive(false);
                    tagContainer.gameObject.SetActive(used > 0);
                    if (noTagsText != null) noTagsText.gameObject.SetActive(used == 0);
                }
            }
            if (salePriceText != null)
            {
                var collector = core.GetComponent<StatCollector>();
                int price = shopCatalog != null && collector != null
                    ? shopCatalog.GetSellPrice(collector.Modules, matchedTags) : 0;
                if (price != lastPrice)
                {
                    lastPrice = price;
                    salePriceText.text = $"총기 가치  <nobr>${price:N0}</nobr>";
                }
            }
        }

        // 마지막에 그린 태그 목록과 비교해 배지 갱신 필요 여부를 판정한다.
        private static bool SameTags(System.Collections.Generic.List<CustomerRequestData.Tag> current,
            System.Collections.Generic.List<CustomerRequestData.Tag> applied)
        {
            if (current.Count != applied.Count) return false;
            for (int i = 0; i < current.Count; i++)
                if (!ReferenceEquals(current[i], applied[i])) return false;
            return true;
        }
    }
}
