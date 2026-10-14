using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ScanLoadingGraphic : MaskableGraphic
    {
        private float progress;
        private float thickness = 3f;
        public void SetProgress(float value, float width)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(progress, value) && Mathf.Approximately(thickness, width)) return;
            progress = value;
            thickness = width;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            float inner = Mathf.Max(0f, radius - thickness);
            var background = color;
            background.a *= 0.18f;
            AddArc(vh, rect.center, radius, inner, 1f, background);
            AddArc(vh, rect.center, radius, inner, progress, color);
        }
        private static void AddArc(VertexHelper vh, Vector2 center, float outer, float inner, float amount, Color tint)
        {
            int segments = Mathf.CeilToInt(amount * 96f);
            for (int i = 0; i < segments; i++)
            {
                float a = (0.25f - amount * i / segments) * Mathf.PI * 2f;
                float b = (0.25f - amount * (i + 1) / segments) * Mathf.PI * 2f;
                Vector2 start = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 end = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                int index = vh.currentVertCount;
                vh.AddVert(center + start * outer, tint, Vector2.zero);
                vh.AddVert(center + start * inner, tint, Vector2.zero);
                vh.AddVert(center + end * inner, tint, Vector2.zero);
                vh.AddVert(center + end * outer, tint, Vector2.zero);
                vh.AddTriangle(index, index + 1, index + 2);
                vh.AddTriangle(index, index + 2, index + 3);
            }
        }
    }
}
