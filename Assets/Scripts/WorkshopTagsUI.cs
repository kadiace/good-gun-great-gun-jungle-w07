using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class WorkshopTagsUI : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform grid;
        [SerializeField] private RectTransform tagTemplate;
        [SerializeField] private WeaponTypeData[] tags = new WeaponTypeData[0];
        [SerializeField] private TMPro.TextMeshProUGUI weightText;
        [SerializeField] private TMPro.TextMeshProUGUI salePriceText;
        [SerializeField] private ShopCatalog shopCatalog;
        private readonly System.Collections.Generic.List<WeaponTypeData> matchedTags = new System.Collections.Generic.List<WeaponTypeData>();
        public RectTransform TagTemplate => tagTemplate;
        public void GetMatchedTags(PowerCore core, System.Collections.Generic.List<WeaponTypeData> result)
        {
            result.Clear();
            if (core == null) return;
            var powered = WeaponAssemblyStats.PoweredCounts(core.GetComponent<CubeAssembly>());
            // Hover uses the current assembly even when a display's behaviours are disabled or a cache is dirty.
            var collector = core.GetComponent<StatCollector>();
            var stats = WeaponAssemblyStats.Calculate(core.GetComponent<CubeAssembly>(),
                collector != null && collector.BaseStats != null ? collector.BaseStats.Stats : default);
            foreach (var tag in tags)
                if (tag != null && tag.Matches(stats, powered, powered)) result.Add(tag);
        }
        private TMPro.TextMeshProUGUI[] labels;
        private RectTransform[] pills;

        private void Awake()
        {
            panel?.SetActive(false);
            if (uiManager == null) uiManager = GetComponentInParent<UIManager>(true);
            if (tagTemplate == null || grid == null) return;
            tagTemplate.gameObject.SetActive(false);
            labels = new TMPro.TextMeshProUGUI[tags.Length];
            pills = new RectTransform[tags.Length];
            for (int i = 0; i < tags.Length; i++)
            {
                pills[i] = Instantiate(tagTemplate, grid);
                pills[i].name = "Tag" + i;
                labels[i] = pills[i].GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                if (tags[i] != null)
                {
                    labels[i].text = tags[i].DisplayName;
                    pills[i].GetComponent<TagPillGraphic>().color = tags[i].TagColor;
                }
                pills[i].gameObject.SetActive(false);
            }
        }

        private void OnDisable() => panel?.SetActive(false);

        private void LateUpdate()
        {
            var player = uiManager != null ? uiManager.Player : null;
            var session = player != null ? player.GetComponent<WorkshopSession>() : null;
            var bench = session != null ? session.ActiveWorkbench : null;
            var core = bench != null ? bench.CurrentCore : null;
            bool visible = session != null && session.IsEditing && core != null;
            if (panel != null && panel.activeSelf != visible) panel.SetActive(visible);
            if (!visible || labels == null) return;
            var assembly = core.GetComponent<CubeAssembly>();
            var stats = core.GetCurrentStats();
            var powered = WeaponAssemblyStats.PoweredCounts(assembly);
            var counts = powered;
            if (weightText != null) weightText.text = $"무게  {stats.weight:0.##}";
            matchedTags.Clear();
            for (int i = 0; i < tags.Length; i++)
            {
                bool matched = tags[i] != null && tags[i].Matches(stats, counts, powered);
                if (matched) matchedTags.Add(tags[i]);
                if (pills[i].gameObject.activeSelf != matched)
                {
                    pills[i].gameObject.SetActive(matched);
                    UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(grid);
                }
                if (matched)
                {
                    labels[i].text = tags[i].DisplayName;
                    pills[i].GetComponent<TagPillGraphic>().color = tags[i].TagColor;
                }
            }
            if (salePriceText != null)
            {
                var collector = core.GetComponent<StatCollector>();
                int price = shopCatalog != null && collector != null
                    ? shopCatalog.GetSellPrice(collector.Modules, matchedTags) : 0;
                salePriceText.text = $"판매 가격  ${price:N0}";
            }
        }
    }
}
