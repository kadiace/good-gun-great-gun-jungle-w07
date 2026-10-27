using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class TagFlowLayout : UnityEngine.UI.LayoutGroup
    {
        [SerializeField] private Vector2 spacing = new Vector2(6f, 6f);
        [SerializeField] private Vector2 textPadding = new Vector2(10.5f, 4.5f);
        [SerializeField] private float minimumHeight = 24f;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            SetLayoutInputForAxis(0f, 0f, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            float height = Arrange(false);
            SetLayoutInputForAxis(height, height, -1f, 1);
        }

        public override void SetLayoutHorizontal() => Arrange(true);
        public override void SetLayoutVertical() => Arrange(true);

        private float Arrange(bool apply)
        {
            float available = Mathf.Max(1f, rectTransform.rect.width - padding.horizontal);
            float x = 0f, y = padding.top, rowHeight = 0f;
            foreach (RectTransform child in rectChildren)
            {
                var text = child.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                float naturalWidth = text != null ? text.GetPreferredValues(text.text, Mathf.Infinity, Mathf.Infinity).x : 0f;
                float width = Mathf.Min(available, naturalWidth + textPadding.x * 2f);
                float textHeight = text != null ? text.GetPreferredValues(text.text,
                    Mathf.Max(1f, width - textPadding.x * 2f), Mathf.Infinity).y : 0f;
                float height = Mathf.Max(minimumHeight, textHeight + textPadding.y * 2f);
                if (x > 0f && x + width > available)
                {
                    x = 0f;
                    y += rowHeight + spacing.y;
                    rowHeight = 0f;
                }
                if (apply)
                {
                    SetChildAlongAxis(child, 0, padding.left + x, width);
                    SetChildAlongAxis(child, 1, y, height);
                }
                x += width + spacing.x;
                rowHeight = Mathf.Max(rowHeight, height);
            }
            return y + rowHeight + padding.bottom;
        }
    }
}
