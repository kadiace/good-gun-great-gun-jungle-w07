using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 크로스헤어 아래 차지 게이지. 장착 무기가 충전 중일 때만 표시하고, 충전 단계마다 칸이 찬다.
    [RequireComponent(typeof(RectTransform))]
    public sealed class ChargeGaugeUI : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private TMPro.TMP_FontAsset font;
        [SerializeField] private Vector2 offset = new Vector2(0f, -36f);
        [SerializeField] private Vector2 size = new Vector2(180f, 10f);
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color fillColor = new Color(0.45f, 0.85f, 1f, 0.95f);
        [SerializeField] private Color fullColor = new Color(1f, 0.6f, 0.15f, 1f);

        private RectTransform root;
        private RectTransform segmentRow;
        private TMPro.TextMeshProUGUI label;
        private readonly List<Image> segments = new List<Image>();
        private readonly List<Image> fills = new List<Image>();
        private static Sprite fillSprite;

        private void Awake()
        {
            ResolveUIManager();
            if (font == null) font = TMPro.TMP_Settings.defaultFontAsset;
            Build();
            root.gameObject.SetActive(false);
        }

        private void ResolveUIManager()
        {
            if (uiManager != null) return;
            uiManager = GetComponentInParent<UIManager>(true);
            if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();
        }

        private void OnDisable()
        {
            if (root != null) root.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            ResolveUIManager();
            WeaponHolder holder = uiManager != null ? uiManager.PlayerWeaponHolder : null;
            EquipableWeapon weapon = holder != null ? holder.EquippedWeapon : null;
            bool visible = weapon != null && weapon.IsCharging && weapon.MaxChargeLevel > 0;
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
            if (!visible) return;

            int max = weapon.MaxChargeLevel;
            if (segments.Count != max) RebuildSegments(max);
            float filled = weapon.ChargeProgress * max;
            bool full = weapon.ChargeLevel >= max;
            for (int i = 0; i < max; i++)
            {
                float amount = Mathf.Clamp01(filled - i);
                fills[i].fillAmount = amount;
                fills[i].color = full ? fullColor : amount >= 1f ? fillColor : fillColor * new Color(1f, 1f, 1f, 0.6f);
            }
            label.text = full ? $"차지 MAX ({max})" : $"차지 {weapon.ChargeLevel}/{max}";
            label.color = full ? fullColor : Color.white;
        }

        private void Build()
        {
            root = CreateRect("ChargeGauge", transform);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = offset;
            root.sizeDelta = new Vector2(size.x, size.y + 22f);

            segmentRow = CreateRect("Segments", root);
            segmentRow.anchorMin = new Vector2(0f, 1f);
            segmentRow.anchorMax = new Vector2(1f, 1f);
            segmentRow.pivot = new Vector2(0.5f, 1f);
            segmentRow.sizeDelta = new Vector2(0f, size.y);
            segmentRow.anchoredPosition = Vector2.zero;
            var row = segmentRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 3f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;

            label = CreateRect("Label", root).gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 18;
            label.alignment = TMPro.TextAlignmentOptions.Top;
            label.raycastTarget = false;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(0f, 18f);
            labelRect.anchoredPosition = Vector2.zero;
        }

        private void RebuildSegments(int count)
        {
            foreach (Image segment in segments) Destroy(segment.gameObject);
            segments.Clear();
            fills.Clear();
            for (int i = 0; i < count; i++)
            {
                RectTransform rect = CreateRect($"Segment{i + 1}", segmentRow);
                var background = rect.gameObject.AddComponent<Image>();
                background.color = emptyColor;
                background.raycastTarget = false;
                RectTransform fillRect = CreateRect("Fill", rect);
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
                var fill = fillRect.gameObject.AddComponent<Image>();
                fill.raycastTarget = false;
                // Filled images need a sprite; a plain white one keeps it a solid bar.
                if (fillSprite == null)
                    fillSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
                fill.sprite = fillSprite;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillAmount = 0f;
                segments.Add(background);
                fills.Add(fill);
            }
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
