using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    public sealed class WeaponRecipeUI : MonoBehaviour
    {
        [Serializable] public sealed class RecipeEntry
        {
            public string tagId;
            public TMP_Text title;
            public TagPillGraphic badge;
            public TMP_Text conditions;
        }
        [SerializeField] private GameObject panel;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private Button openButton;
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private RectTransform weaponInfo;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private LayoutElement inlineLayout;
        [SerializeField] private RecipeEntry[] recipes = new RecipeEntry[0];
        private readonly Vector3[] canvasCorners = new Vector3[4];
        private bool layoutDirty;
        public bool IsOpen => panel != null && panel.activeSelf;
        public RectTransform TutorialButton => openButton != null ? openButton.transform as RectTransform : null;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (openButton != null) openButton.gameObject.SetActive(false);
            SetButtonLabel();
            if (openButton != null) openButton.onClick.AddListener(Toggle);
        }
        private void OnDisable() => Close();
        private void OnDestroy()
        {
            if (openButton != null) openButton.onClick.RemoveListener(Toggle);
        }
        private void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }
        public void Open()
        {
            if (GameManager.GameplayBlocked || IsOpen ||
                panel == null || weaponInfo == null || !weaponInfo.gameObject.activeInHierarchy) return;
            if (!TutorialManager.TryAction("recipe")) return;
            var table = TableManager.Instance.CustomerRequest;
            foreach (var recipe in recipes)
            {
                var tag = table.FindTag(recipe.tagId);
                if (tag == null) continue;
                recipe.title.text = tag.name;
                recipe.badge.color = tag.color;
                var text = new StringBuilder();
                foreach (var condition in tag.conditions ?? Array.Empty<CustomerRequestData.Condition>())
                    if (condition != null) text.Append("• ").Append(CustomerRequestInfo.Describe(condition)).AppendLine();
                recipe.conditions.text = text.Length > 0 ? text.ToString().TrimEnd() : "조건이 설정되지 않았습니다.";
            }
            panel.SetActive(true);
            SetButtonLabel();
            layoutDirty = true;
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f;
            // Inline inspection can finish while the recipe stays visible during construction.
            if (TutorialManager.Instance != null) TutorialManager.Instance.NotifySignal("recipe-inspected");
        }
        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            SetButtonLabel();
        }
        private void SetButtonLabel()
        {
            if (buttonLabel != null) buttonLabel.text = IsOpen ? "레시피 접기" : "레시피 보기";
        }
        private void LateUpdate()
        {
            var player = uiManager != null ? uiManager.Player : null;
            var session = player != null ? player.GetComponent<WorkshopSession>() : null;
            bool canBrowse = session != null && session.IsEditing;
            if (openButton != null && openButton.gameObject.activeSelf != canBrowse)
                openButton.gameObject.SetActive(canBrowse);
            if (!canBrowse) { Close(); return; }
            if (!IsOpen) return;
            if (weaponInfo == null || !weaponInfo.gameObject.activeInHierarchy) { Close(); return; }
            if (layoutDirty)
            {
                // Resolve wrapped content at the sidebar width before measuring its height.
                LayoutRebuilder.ForceRebuildLayoutImmediate(weaponInfo);
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                layoutDirty = false;
            }
            var canvas = weaponInfo.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
            canvas.GetWorldCorners(canvasCorners);
            float bottom = weaponInfo.InverseTransformPoint(canvasCorners[0]).y + 24f;
            // Exclude the old recipe height when calculating remaining screen space.
            float headerHeight = weaponInfo.rect.height - ((RectTransform)panel.transform).rect.height;
            float available = Mathf.Max(0f, weaponInfo.rect.yMax - headerHeight - bottom);
            float preferred = LayoutUtility.GetPreferredHeight(scroll.content);
            float height = Mathf.Min(preferred, available);
            if (!Mathf.Approximately(inlineLayout.preferredHeight, height)) inlineLayout.preferredHeight = height;
        }
    }
}
