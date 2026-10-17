using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 즐겨찾기 칸(favoriteIndex >= 0) 또는 TAB 창 목록 칸(favoriteIndex < 0) 하나. 동작은 ModulePaletteUI가 처리.
    public sealed class ModuleSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private ModulePaletteUI owner;
        private Image background;
        private Image swatch;
        private TMPro.TextMeshProUGUI label;
        private TMPro.TextMeshProUGUI priceLabel;
        private TMPro.TextMeshProUGUI numberLabel;
        private ModuleSelectionBorder highlight;
        private ModuleSelectionBorder dropHighlight;
        private GameObject lockOverlay;

        public void SetContentPadding(float padding)
        {
            ApplyContentPadding(Vector2.zero, new Vector2(padding, padding));
        }

        public void SetContentPaddingRatio(Vector2 ratio)
        {
            ratio.x = Mathf.Clamp(ratio.x, 0f, .2f);
            ratio.y = Mathf.Clamp(ratio.y, 0f, .2f);
            ApplyContentPadding(ratio, Vector2.zero);
        }

        private void ApplyContentPadding(Vector2 ratio, Vector2 fixedPadding)
        {
            if (label == null || priceLabel == null) return;
            float textLeft = fixedPadding.x * 3f;
            float priceHeight = Mathf.Max(priceLabel.fontSizeMax, priceLabel.GetPreferredValues("0").y);
            float numberRowHeight = 0f;
            if (numberLabel != null)
            {
                float numberHeight = Mathf.Max(numberLabel.fontSizeMax, numberLabel.GetPreferredValues("0").y);
                numberLabel.rectTransform.anchorMin = numberLabel.rectTransform.anchorMax = Vector2.one - ratio;
                numberLabel.rectTransform.anchoredPosition = -fixedPadding;
                numberLabel.rectTransform.sizeDelta = new Vector2(Mathf.Max(28f, numberHeight), numberHeight);
                numberRowHeight = numberHeight + fixedPadding.y;
            }
            var nameRect = label.rectTransform;
            nameRect.anchorMin = new Vector2(ratio.x * 3f, ratio.y * 2f);
            nameRect.anchorMax = new Vector2(1f - ratio.x, 1f - ratio.y * (numberLabel != null ? 2f : 1f));
            nameRect.offsetMin = new Vector2(textLeft, fixedPadding.y * 2f + priceHeight);
            nameRect.offsetMax = new Vector2(-fixedPadding.x, -fixedPadding.y - numberRowHeight);
            var priceRect = priceLabel.rectTransform;
            priceRect.anchorMin = new Vector2(ratio.x * 3f, ratio.y);
            priceRect.anchorMax = new Vector2(1f - ratio.x, ratio.y);
            priceRect.offsetMin = new Vector2(textLeft, fixedPadding.y);
            priceRect.offsetMax = new Vector2(-fixedPadding.x, fixedPadding.y + priceHeight);
            var swatchRect = swatch.rectTransform;
            swatchRect.anchorMin = ratio;
            swatchRect.anchorMax = new Vector2(ratio.x * 2f, 1f - ratio.y);
            swatchRect.offsetMin = fixedPadding;
            swatchRect.offsetMax = new Vector2(fixedPadding.x * 2f, -fixedPadding.y);
        }

        public int FavoriteIndex { get; private set; } = -1;
        public int ModuleId { get; private set; }

        public void Setup(ModulePaletteUI paletteOwner, int favoriteIndex, Image backgroundImage, Image swatchImage,
            TMPro.TextMeshProUGUI labelText)
        {
            owner = paletteOwner;
            FavoriteIndex = favoriteIndex;
            background = backgroundImage;
            swatch = swatchImage;
            label = labelText;
            priceLabel = new GameObject("Price", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>();
            priceLabel.rectTransform.SetParent(transform, false);
            priceLabel.rectTransform.anchorMin = Vector2.zero;
            priceLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            priceLabel.rectTransform.pivot = new Vector2(.5f, 0f);
            priceLabel.rectTransform.anchoredPosition = new Vector2(7f, 5f);
            priceLabel.rectTransform.sizeDelta = new Vector2(-20f, 23f);
            priceLabel.font = label.font;
            priceLabel.fontSize = 18f;
            priceLabel.enableAutoSizing = true;
            priceLabel.fontSizeMin = 14f;
            priceLabel.fontSizeMax = 18f;
            priceLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            priceLabel.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            priceLabel.color = new Color(.9f, .85f, .65f);
            priceLabel.raycastTarget = false;
            highlight = CreateBorder("SelectionBorder", new Color(1f, 0.8f, 0.2f, 1f));
            dropHighlight = CreateBorder("DropBorder", new Color(.25f, .9f, 1f, 1f));
            lockOverlay = new GameObject("LockOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = lockOverlay.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var shade = lockOverlay.GetComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.8f);
            shade.raycastTarget = false;
            var icon = new GameObject("LockIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(LockPictogramGraphic)).GetComponent<LockPictogramGraphic>();
            icon.rectTransform.SetParent(rect, false);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f, .5f);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            icon.rectTransform.sizeDelta = new Vector2(40f, 40f);
            icon.color = Color.white;
            icon.raycastTarget = false;
            lockOverlay.SetActive(false);
            if (favoriteIndex >= 0)
            {
                numberLabel = new GameObject("SlotNumber", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>();
                numberLabel.rectTransform.SetParent(transform, false);
                numberLabel.rectTransform.anchorMin = numberLabel.rectTransform.anchorMax = Vector2.one;
                numberLabel.rectTransform.pivot = Vector2.one;
                numberLabel.font = label.font;
                numberLabel.text = ((favoriteIndex + 1) % 10).ToString();
                numberLabel.fontSize = 24f;
                numberLabel.enableAutoSizing = true;
                numberLabel.fontSizeMin = 20f;
                numberLabel.fontSizeMax = 24f;
                numberLabel.alignment = TMPro.TextAlignmentOptions.TopRight;
                numberLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                numberLabel.color = new Color(.9f, .9f, .95f);
                numberLabel.raycastTarget = false;
                UIFonts.ApplyWeight(numberLabel, 700);
            }
            if (favoriteIndex < 0) SetContentPadding(5f);
        }

        private ModuleSelectionBorder CreateBorder(string objectName, Color color)
        {
            var border = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(ModuleSelectionBorder)).GetComponent<ModuleSelectionBorder>();
            border.rectTransform.SetParent(transform, false);
            border.rectTransform.anchorMin = Vector2.zero;
            border.rectTransform.anchorMax = Vector2.one;
            border.rectTransform.offsetMin = border.rectTransform.offsetMax = Vector2.zero;
            border.color = color;
            border.raycastTarget = false;
            border.gameObject.SetActive(false);
            return border;
        }

        public void SetDropHighlight(bool active)
        {
            if (dropHighlight != null) dropHighlight.gameObject.SetActive(active);
        }

        public void Show(int moduleId, string text, Color color, bool selected, bool locked = false, string price = "")
        {
            ModuleId = moduleId;
            label.text = text;
            priceLabel.text = price;
            swatch.color = color;
            highlight.gameObject.SetActive(selected && !locked);
            lockOverlay.SetActive(locked);
            label.gameObject.SetActive(!locked);
            priceLabel.gameObject.SetActive(!locked);
            UIFonts.ApplyWeight(label, selected ? 700 : 400);
        }

        public void OnPointerEnter(PointerEventData eventData) => owner?.SetHoveredSlot(this);
        public void OnPointerExit(PointerEventData eventData) => owner?.ClearHoveredSlot(this);
        private void OnDisable()
        {
            SetDropHighlight(false);
            owner?.SlotDisabled(this);
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
                owner?.HandleClick(this);
        }
        public void OnBeginDrag(PointerEventData eventData) => owner?.BeginDrag(this, eventData);
        public void OnDrag(PointerEventData eventData) => owner?.Drag(eventData);
        public void OnEndDrag(PointerEventData eventData) => owner?.EndDrag(eventData);
        public void OnDrop(PointerEventData eventData) => owner?.Drop(this, eventData);
    }

    // A separate graphic keeps the selection border independent of the button's tint.
    public sealed class ModuleSelectionBorder : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            const float width = 3f;
            AddRect(mesh, new Rect(r.xMin - width, r.yMin - width, r.width + width * 2f, width));
            AddRect(mesh, new Rect(r.xMin - width, r.yMax, r.width + width * 2f, width));
            AddRect(mesh, new Rect(r.xMin - width, r.yMin, width, r.height));
            AddRect(mesh, new Rect(r.xMax, r.yMin, width, r.height));
        }

        private void AddRect(VertexHelper mesh, Rect rect)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
