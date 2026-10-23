using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class QuestInfoTextSizing : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField, Min(1f)] private float preferredFontSize = 21f;
        [SerializeField, Min(1f)] private float minimumFontSize = 18f;
        [SerializeField, Min(1f)] private float maximumHeight = 750f;
        [SerializeField, Min(0f)] private float bottomMargin = 24f;
        private readonly List<TMP_Text> labels = new List<TMP_Text>();
        private readonly List<HorizontalOrVerticalLayoutGroup> layoutGroups =
            new List<HorizontalOrVerticalLayoutGroup>();
        private RectTransform panel;
        private bool dirty = true;
        private bool resetScroll;
        private float previousWidth = -1f;
        private float previousAvailableHeight = -1f;

        private void Awake() => panel = (RectTransform)transform;
        private void OnEnable()
        {
            // This component owns the scroll content geometry, including older scene overrides.
            if (content != null)
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = Vector2.one;
                content.pivot = new Vector2(0f, 1f);
                content.sizeDelta = new Vector2(0f, content.sizeDelta.y);
                content.anchoredPosition = Vector2.zero;
            }
            resetScroll = true;
            MarkDirty();
        }

        public void MarkDirty() => dirty = true;

        private void LateUpdate()
        {
            if (content == null || scrollRect == null) return;
            var parent = panel.parent as RectTransform;
            float availableHeight = maximumHeight;
            if (parent != null)
                availableHeight = Mathf.Min(availableHeight,
                    panel.localPosition.y - parent.rect.yMin - bottomMargin);
            availableHeight = Mathf.Max(1f, availableHeight);
            float width = panel.rect.width;
            if (!dirty && Mathf.Approximately(width, previousWidth) &&
                Mathf.Approximately(availableHeight, previousAvailableHeight)) return;
            if (width <= 0f) return;
            dirty = false;
            previousWidth = width;
            previousAvailableHeight = availableHeight;

            float scrollOffset = resetScroll ? 0f : content.anchoredPosition.y;
            resetScroll = false;
            labels.Clear();
            content.GetComponentsInChildren(false, labels);
            layoutGroups.Clear();
            content.GetComponentsInChildren(false, layoutGroups);
            float minimum = Mathf.Max(1f, minimumFontSize);
            float preferred = Mathf.Max(minimum, preferredFontSize);
            foreach (var label in labels)
            {
                label.enableAutoSizing = false;
                label.fontSize = preferred;
            }

            // Establish widths once. The prefab fixes the status width and stretches the
            // description, so changing font size does not change either text box's width.
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float height = Mathf.Max(1f, Mathf.Ceil(LayoutUtility.GetPreferredHeight(content)));
            bool shrink = height > availableHeight && preferred > minimum;
            if (shrink)
            {
                // Find the largest fitting common size on the existing 0.5pt scale.
                // Probe preferred heights only; let the fitter apply the chosen height once.
                int first = 1;
                int last = Mathf.CeilToInt((preferred - minimum) / .5f);
                float bestSize = minimum;
                float measuredSize = preferred;
                while (first <= last)
                {
                    int step = first + (last - first) / 2;
                    measuredSize = Mathf.Max(minimum, preferred - step * .5f);
                    height = MeasureContentHeight(measuredSize);
                    if (height <= availableHeight)
                    {
                        bestSize = measuredSize;
                        last = step - 1;
                    }
                    else first = step + 1;
                }
                if (!Mathf.Approximately(measuredSize, bestSize))
                    height = MeasureContentHeight(bestSize);
            }

            float panelHeight = Mathf.Min(height, availableHeight);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
            // The content's ContentSizeFitter owns its full height; only the viewport is capped.
            if (shrink) LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scrollRect.vertical = height > panelHeight + .5f;
            scrollRect.StopMovement();
            content.anchoredPosition = new Vector2(0f,
                Mathf.Clamp(scrollOffset, 0f, Mathf.Max(0f, height - panelHeight)));
        }

        private float MeasureContentHeight(float fontSize)
        {
            foreach (var label in labels) label.fontSize = fontSize;
            // Recalculate layout inputs from the rows up to the content, without resizing
            // any RectTransforms or overriding TMP's preferred height with a LayoutElement.
            for (int i = layoutGroups.Count - 1; i >= 0; i--)
                if (layoutGroups[i].isActiveAndEnabled)
                    layoutGroups[i].CalculateLayoutInputVertical();
            return Mathf.Max(1f, Mathf.Ceil(LayoutUtility.GetPreferredHeight(content)));
        }
    }
}
