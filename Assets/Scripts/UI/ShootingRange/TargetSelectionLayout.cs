using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // Window의 앵커는 그대로 두고 저작된 하위 오프셋·셀 크기만 같은 비율로 계산한다.
    internal sealed class TargetSelectionLayout
    {
        private struct RectState
        {
            public RectTransform rect;
            public Vector2 size, position;
        }

        private struct TextState
        {
            public TMP_Text text;
            public float size, min, max;
            public Vector4 margin;
        }

        private struct GridState
        {
            public GridLayoutGroup grid;
            public Vector2 cell, spacing;
            public Vector4 padding;
        }

        private readonly RectTransform window, popup, canvasRect;
        private readonly Vector2 referenceSize;
        private readonly List<RectState> rects = new List<RectState>();
        private readonly List<TextState> texts = new List<TextState>();
        private readonly List<GridState> grids = new List<GridState>();
        private readonly Vector3[] popupCorners = new Vector3[4];
        private Vector2 appliedScale = Vector2.one;
        private float appliedTextScale = 1f;
        private bool dirty = true;

        internal TargetSelectionLayout(RectTransform window, Vector2 referenceSize, RectTransform popup)
        {
            this.window = window;
            this.popup = popup;
            this.referenceSize = new Vector2(Mathf.Max(1f, referenceSize.x), Mathf.Max(1f, referenceSize.y));
            var canvas = window != null ? window.GetComponentInParent<Canvas>() : null;
            canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (window != null) Register(window);
        }

        // 이미 확대된 템플릿에서 만든 버튼도 현재 배율을 나눠 원래 크기로 기록한다.
        internal void RegisterRows(Transform root)
        {
            Register(root);
            dirty = true;
        }

        private void Register(Transform root)
        {
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == window || rect.parent != null && rect.parent.GetComponent<LayoutGroup>() != null) continue;
                rects.Add(new RectState
                {
                    rect = rect,
                    size = Divide(rect.sizeDelta, appliedScale),
                    position = Divide(rect.anchoredPosition, appliedScale)
                });
            }
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                texts.Add(new TextState
                {
                    text = text,
                    size = text.fontSize / appliedTextScale,
                    min = text.fontSizeMin / appliedTextScale,
                    max = text.fontSizeMax / appliedTextScale,
                    margin = ScaleMargins(text.margin, new Vector2(1f / appliedScale.x, 1f / appliedScale.y))
                });
            foreach (var grid in root.GetComponentsInChildren<GridLayoutGroup>(true))
                grids.Add(new GridState
                {
                    grid = grid,
                    cell = Divide(grid.cellSize, appliedScale),
                    spacing = Divide(grid.spacing, appliedScale),
                    padding = new Vector4(grid.padding.left / appliedScale.x, grid.padding.top / appliedScale.y,
                        grid.padding.right / appliedScale.x, grid.padding.bottom / appliedScale.y)
                });
        }

        internal void Apply()
        {
            if (window == null || window.rect.width <= 0f || window.rect.height <= 0f) return;
            Vector2 scale = Divide(window.rect.size, referenceSize);
            if (dirty || !Mathf.Approximately(scale.x, appliedScale.x) || !Mathf.Approximately(scale.y, appliedScale.y))
            {
                ApplyScale(scale);
                dirty = false;
            }
            KeepPopupOnScreen();
        }

        private void ApplyScale(Vector2 scale)
        {
            float textScale = Mathf.Min(scale.x, scale.y);
            foreach (var state in rects)
            {
                if (state.rect == null) continue;
                state.rect.sizeDelta = Vector2.Scale(state.size, scale);
                state.rect.anchoredPosition = Vector2.Scale(state.position, scale);
            }
            foreach (var state in grids)
            {
                if (state.grid == null) continue;
                state.grid.cellSize = Vector2.Scale(state.cell, scale);
                state.grid.spacing = Vector2.Scale(state.spacing, scale);
                state.grid.padding = new RectOffset(Mathf.RoundToInt(state.padding.x * scale.x),
                    Mathf.RoundToInt(state.padding.z * scale.x), Mathf.RoundToInt(state.padding.y * scale.y),
                    Mathf.RoundToInt(state.padding.w * scale.y));
            }
            foreach (var state in texts)
            {
                if (state.text == null) continue;
                state.text.fontSizeMin = state.min * textScale;
                state.text.fontSizeMax = state.max * textScale;
                state.text.fontSize = state.size * textScale;
                state.text.margin = ScaleMargins(state.margin, scale);
            }
            appliedScale = scale;
            appliedTextScale = textScale;
        }

        private void KeepPopupOnScreen()
        {
            if (popup == null || !popup.gameObject.activeInHierarchy || canvasRect == null) return;
            popup.GetWorldCorners(popupCorners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 corner in popupCorners)
            {
                Vector2 local = canvasRect.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            Rect bounds = canvasRect.rect;
            Vector2 shift = new Vector2(
                FitShift(min.x, max.x, bounds.xMin, bounds.xMax),
                FitShift(min.y, max.y, bounds.yMin, bounds.yMax));
            if (shift.sqrMagnitude > .001f) popup.position += canvasRect.TransformVector(shift);
        }

        private static float FitShift(float min, float max, float lower, float upper)
        {
            if (max - min > upper - lower) return (lower + upper - min - max) * .5f;
            return min < lower ? lower - min : max > upper ? upper - max : 0f;
        }

        internal void Restore()
        {
            ApplyScale(Vector2.one);
            rects.Clear();
            texts.Clear();
            grids.Clear();
        }

        private static Vector2 Divide(Vector2 value, Vector2 scale) => new Vector2(value.x / scale.x, value.y / scale.y);
        private static Vector4 ScaleMargins(Vector4 margin, Vector2 scale)
            => new Vector4(margin.x * scale.x, margin.y * scale.y, margin.z * scale.x, margin.w * scale.y);
    }
}
