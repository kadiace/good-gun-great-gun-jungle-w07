using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 즐겨찾기 칸(favoriteIndex >= 0) 또는 TAB 목록 칸(favoriteIndex < 0) 하나.
    // 표시와 포인터 이벤트 전달만 담당하고, 선택·드래그 판정은 View 이벤트를 받은 Presenter가 처리한다.
    public sealed class ModuleSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private ModulePaletteView m_view;
        [SerializeField, Tooltip("즐겨찾기 칸이면 0~9, 목록 칸이면 -1.")]
        private int m_favoriteIndex = -1;
        [SerializeField] private Button m_button;
        [SerializeField] private Image m_swatch;
        [SerializeField] private TMPro.TextMeshProUGUI m_label;
        [SerializeField] private TMPro.TextMeshProUGUI m_priceLabel;
        [SerializeField] private TMPro.TextMeshProUGUI m_numberLabel;
        [SerializeField] private ModuleSelectionBorder m_selectionBorder;
        [SerializeField] private ModuleSelectionBorder m_dropBorder;
        [SerializeField] private GameObject m_lockOverlay;

        public int FavoriteIndex => m_favoriteIndex;

        public int ModuleId { get; private set; }

        public Button Button => m_button;

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
            if (m_label == null || m_priceLabel == null || m_swatch == null) return;
            float textLeft = fixedPadding.x * 3f;
            float priceHeight = Mathf.Max(m_priceLabel.fontSizeMax, m_priceLabel.GetPreferredValues("0").y);
            float numberRowHeight = 0f;
            if (m_numberLabel != null)
            {
                float numberHeight = Mathf.Max(m_numberLabel.fontSizeMax, m_numberLabel.GetPreferredValues("0").y);
                m_numberLabel.rectTransform.anchorMin = m_numberLabel.rectTransform.anchorMax = Vector2.one - ratio;
                m_numberLabel.rectTransform.anchoredPosition = -fixedPadding;
                m_numberLabel.rectTransform.sizeDelta = new Vector2(Mathf.Max(21f, numberHeight), numberHeight);
                numberRowHeight = numberHeight + fixedPadding.y;
            }
            var nameRect = m_label.rectTransform;
            nameRect.anchorMin = new Vector2(ratio.x * 3f, ratio.y * 2f);
            nameRect.anchorMax = new Vector2(1f - ratio.x, 1f - ratio.y * (m_numberLabel != null ? 2f : 1f));
            nameRect.offsetMin = new Vector2(textLeft, fixedPadding.y * 2f + priceHeight);
            nameRect.offsetMax = new Vector2(-fixedPadding.x, -fixedPadding.y - numberRowHeight);
            var priceRect = m_priceLabel.rectTransform;
            priceRect.anchorMin = new Vector2(ratio.x * 3f, ratio.y);
            priceRect.anchorMax = new Vector2(1f - ratio.x, ratio.y);
            priceRect.offsetMin = new Vector2(textLeft, fixedPadding.y);
            priceRect.offsetMax = new Vector2(-fixedPadding.x, fixedPadding.y + priceHeight);
            var swatchRect = m_swatch.rectTransform;
            swatchRect.anchorMin = ratio;
            swatchRect.anchorMax = new Vector2(ratio.x * 2f, 1f - ratio.y);
            swatchRect.offsetMin = fixedPadding;
            swatchRect.offsetMax = new Vector2(fixedPadding.x * 2f, -fixedPadding.y);
        }

        public void SetDropHighlight(bool active)
        {
            if (m_dropBorder != null) m_dropBorder.gameObject.SetActive(active);
        }

        public void Show(int moduleId, string text, Color color, bool selected, bool locked = false, string price = "")
        {
            ModuleId = moduleId;
            if (m_label == null || m_priceLabel == null || m_swatch == null) return;
            m_label.text = text;
            m_priceLabel.text = price;
            m_swatch.color = color;
            if (m_selectionBorder != null) m_selectionBorder.gameObject.SetActive(selected && !locked);
            if (m_lockOverlay != null) m_lockOverlay.SetActive(locked);
            m_label.gameObject.SetActive(!locked);
            m_priceLabel.gameObject.SetActive(!locked);
            UIFonts.ApplyWeight(m_label, selected ? 700 : 400);
        }

        public void OnPointerEnter(PointerEventData eventData) => m_view?.NotifySlotHovered(this);
        public void OnPointerExit(PointerEventData eventData) => m_view?.NotifySlotUnhovered(this);
        private void OnDisable()
        {
            SetDropHighlight(false);
            m_view?.NotifySlotDisabled(this);
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
            {
                m_view?.NotifySlotClicked(this);
            }
        }
        public void OnBeginDrag(PointerEventData eventData) => m_view?.NotifySlotDragBegan(this, eventData);
        public void OnDrag(PointerEventData eventData) => m_view?.NotifySlotDragged(eventData);
        public void OnEndDrag(PointerEventData eventData) => m_view?.NotifySlotDragEnded(eventData);
        public void OnDrop(PointerEventData eventData) => m_view?.NotifySlotDropped(this, eventData);
    }
}
