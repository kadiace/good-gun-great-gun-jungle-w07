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
        private ModuleSelectionBorder highlight;
        private GameObject lockOverlay;

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
            priceLabel.fontSize = 11;
            priceLabel.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            priceLabel.color = new Color(.9f, .85f, .65f);
            priceLabel.raycastTarget = false;
            highlight = new GameObject("SelectionBorder", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(ModuleSelectionBorder)).GetComponent<ModuleSelectionBorder>();
            highlight.rectTransform.SetParent(transform, false);
            highlight.rectTransform.anchorMin = Vector2.zero;
            highlight.rectTransform.anchorMax = Vector2.one;
            highlight.rectTransform.offsetMin = highlight.rectTransform.offsetMax = Vector2.zero;
            highlight.color = new Color(1f, 0.8f, 0.2f, 1f);
            highlight.raycastTarget = false;
            highlight.gameObject.SetActive(false);
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
        private void OnDisable() => owner?.ClearHoveredSlot(this);
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
                owner?.HandleClick(this);
        }
        public void OnBeginDrag(PointerEventData eventData) => owner?.BeginDrag(this, eventData);
        public void OnDrag(PointerEventData eventData) => owner?.Drag(eventData);
        public void OnEndDrag(PointerEventData eventData) => owner?.EndDrag();
        public void OnDrop(PointerEventData eventData) => owner?.Drop(this);
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
