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
        [SerializeField, Min(1f)] private float preferredFontSize = 24f;
        [SerializeField, Min(1f)] private float minimumFontSize = 20f;
        [SerializeField, Min(1f)] private float maximumHeight = 1000f;
        [SerializeField, Min(0f)] private float bottomMargin = 32f;
        private readonly List<TMP_Text> labels = new List<TMP_Text>();
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
            float minimum = Mathf.Max(1f, minimumFontSize);
            float size = Mathf.Max(minimum, preferredFontSize);
            float height;
            // Fit the entire list together, preserving equal sizes for every row and its status.
            // Independent TMP Auto Size would make longer requirements smaller than short ones.
            while (true)
            {
                foreach (var label in labels)
                {
                    label.enableAutoSizing = false;
                    label.fontSize = size;
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                height = Mathf.Max(1f, LayoutUtility.GetPreferredHeight(content));
                if (height <= availableHeight || size <= minimum) break;
                size = Mathf.Max(minimum, size - .5f);
            }

            float panelHeight = Mathf.Min(height, availableHeight);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scrollRect.vertical = height > panelHeight + .5f;
            scrollRect.StopMovement();
            content.anchoredPosition = new Vector2(0f,
                Mathf.Clamp(scrollOffset, 0f, Mathf.Max(0f, height - panelHeight)));
        }
    }
}
