using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(UIManager))]
    public sealed class CustomerRequirementsUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private RectTransform tagGrid;
        private UIManager ui;
        private Bell bell;
        private RectTransform panel, content;
        private ScrollRect scroll;
        private readonly List<TMP_Text> rows = new List<TMP_Text>();
        private int used;
        private float nextUpdate;
        private Customer previousCustomer;
        private Vector2 gridAnchorMin, gridAnchorMax, gridPivot, gridPosition, gridSize;
        private bool movedGrid;
        private readonly Vector3[] canvasCorners = new Vector3[4];
        private void Awake()
        {
            ui = GetComponent<UIManager>();
            if (tagGrid != null)
            {
                gridAnchorMin = tagGrid.anchorMin; gridAnchorMax = tagGrid.anchorMax; gridPivot = tagGrid.pivot;
                gridPosition = tagGrid.anchoredPosition; gridSize = tagGrid.sizeDelta;
            }
            Build();
            if (panel != null) panel.gameObject.SetActive(false);
        }
        public void Bind(Bell source) => bell = source;
        private void Build()
        {
            if (panel != null || priceLabel == null) return;
            panel = Rect("CustomerRequirements", priceLabel.rectTransform.parent);
            panel.pivot = new Vector2(.5f, 1f);
            panel.sizeDelta = new Vector2(Mathf.Max(240f, priceLabel.rectTransform.rect.width), 340f);
            scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24f;
            var viewport = Rect("Viewport", panel);
            Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
            // Invisible raycast surface lets the existing UI scroll action reach this list.
            var surface = viewport.gameObject.AddComponent<Image>(); surface.color = new Color(0f, 0f, 0f, .015f);
            content = Rect("Requests", viewport);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1f); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5f; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            panel.gameObject.SetActive(false);
        }
        private void LateUpdate()
        {
            if (panel == null) return;
            var player = ui.Player;
            var session = player != null ? player.GetComponent<WorkshopSession>() : null;
            var weapon = session != null && session.IsEditing && session.ActiveWorkbench != null ? session.ActiveWorkbench.CurrentWeapon : null;
            var customer = bell != null ? bell.Current : null;
            bool visible = weapon != null && customer != null && customer.HasActiveOrder;
            panel.gameObject.SetActive(visible);
            if (!visible) { RestoreGrid(); previousCustomer = null; nextUpdate = 0f; return; }
            PositionPanel();
            if (Time.unscaledTime < nextUpdate && previousCustomer == customer) return;
            nextUpdate = Time.unscaledTime + .12f;
            bool changedCustomer = previousCustomer != customer; previousCustomer = customer;
            var table = TableManager.Instance.CustomerRequest;
            var snapshot = new CustomerRequestInfo.WeaponSnapshot(weapon);
            used = 0; Row("손님 요청사항", true, 0, true);
            var ids = customer.Profile.requestIds;
            if (ids == null) Row("요청 데이터를 확인할 수 없습니다.", false, 0);
            else if (ids.Length == 0) Row("추가 요청 없음", true, 0);
            else foreach (string id in ids)
            {
                var request = table.FindRequest(id);
                if (request == null) { Row("등록되지 않은 요청", false, 0); continue; }
                var tag = table.FindTag(request.tagId);
                bool grouped = tag != null || request.conditions == null || request.conditions.Length != 1;
                if (grouped) Row(tag != null ? tag.name + " 태그" : request.name, table.MeetsRequest(id, snapshot), 0);
                foreach (var condition in table.Conditions(request))
                    if (condition == null) Row("요청 조건을 확인할 수 없습니다.", false, grouped ? 1 : 0);
                    else Row(CustomerRequestInfo.Describe(condition, snapshot.Value(condition)),
                        snapshot.Meets(condition), grouped ? 1 : 0);
            }
            for (int i = used; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            PositionPanel();
            if (changedCustomer) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f; }
        }
        private void PositionPanel()
        {
            var price = priceLabel.rectTransform;
            panel.position = price.TransformPoint(new Vector3(price.rect.center.x, price.rect.yMin - 12f));
            var canvas = price.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
            canvas.GetWorldCorners(canvasCorners);
            float available = -panel.InverseTransformPoint(canvasCorners[0]).y - 20f;
            float matchedHeight = tagGrid != null ? LayoutUtility.GetPreferredHeight(tagGrid) : 0f;
            float preferred = LayoutUtility.GetPreferredHeight(content);
            float height = Mathf.Min(Mathf.Max(70f, preferred), Mathf.Clamp(available - matchedHeight - 12f, 70f, 420f));
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(240f, price.rect.width));
            if (tagGrid != null)
            {
                movedGrid = true;
                var parent = (RectTransform)tagGrid.parent;
                tagGrid.anchorMin = new Vector2(0f, 1f); tagGrid.anchorMax = Vector2.one; tagGrid.pivot = new Vector2(.5f, 1f);
                tagGrid.sizeDelta = new Vector2(gridSize.x, matchedHeight);
                tagGrid.anchoredPosition = new Vector2(gridPosition.x, parent.InverseTransformPoint(panel.position).y - parent.rect.yMax - height - 12f);
            }
        }
        private void Row(string text, bool met, int depth, bool heading = false)
        {
            TMP_Text label;
            if (used == rows.Count)
            {
                label = Rect("RequestRow", content).gameObject.AddComponent<TextMeshProUGUI>();
                label.font = priceLabel.font; label.raycastTarget = false; label.richText = true;
                rows.Add(label);
            }
            else label = rows[used];
            used++;
            label.gameObject.SetActive(true);
            label.fontSize = heading ? 19f : 16f;
            label.color = heading ? Color.white : met ? new Color(.5f, .88f, .65f) : new Color(1f, .63f, .56f);
            label.margin = new Vector4(depth * 20f, 0f, 0f, 0f);
            label.text = heading ? text : (met ? "[O] " : "[ ] ") + text;
        }
        private static RectTransform Rect(string label, Transform parent)
        {
            var rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); return rect;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private void RestoreGrid()
        {
            if (!movedGrid || tagGrid == null) return;
            tagGrid.anchorMin = gridAnchorMin; tagGrid.anchorMax = gridAnchorMax; tagGrid.pivot = gridPivot;
            tagGrid.anchoredPosition = gridPosition; tagGrid.sizeDelta = gridSize; movedGrid = false;
        }
        private void OnDisable() { RestoreGrid(); previousCustomer = null; if (panel != null) panel.gameObject.SetActive(false); }
    }
}
