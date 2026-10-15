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
        public void GetMatchedTags(PowerCore core, System.Collections.Generic.List<WeaponTypeData> result)
        {
            result.Clear();
            if (core == null) return;
            var snapshot = new CustomerRequestInfo.WeaponSnapshot(core);
            foreach (var tag in tags)
                if (tag != null && TableManager.Instance.CustomerRequest.MeetsTag(tag.TableId, snapshot)) result.Add(tag);
        }
        private PowerCore lastCore;
        private float nextRefresh;

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
            if (!visible) { lastCore = null; nextRefresh = 0f; return; }
            if (lastCore == core && Time.unscaledTime < nextRefresh) return;
            lastCore = core; nextRefresh = Time.unscaledTime + .1f;
            var stats = core.GetCurrentStats();
            if (weightText != null) weightText.text = $"무게  {stats.weight:0.##}";
            GetMatchedTags(core, matchedTags);
            if (tagContainer != null && tagTemplate != null)
            {
                var table = TableManager.Instance.CustomerRequest;
                var snapshot = new CustomerRequestInfo.WeaponSnapshot(core);
                int used = 0;
                foreach (var tag in table.tags)
                    if (tag != null && table.MeetsTag(tag.id, snapshot))
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
            if (salePriceText != null)
            {
                var collector = core.GetComponent<StatCollector>();
                int price = shopCatalog != null && collector != null
                    ? shopCatalog.GetSellPrice(collector.Modules, matchedTags) : 0;
                salePriceText.text = $"총기 가치  <nobr>${price:N0}</nobr>";
            }
        }
    }
}
