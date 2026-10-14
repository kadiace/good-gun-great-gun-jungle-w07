using UnityEngine;

namespace WeaponWorkshop
{
    // A code-native capsule that stays round as its width changes.
    public sealed class TagPillGraphic : UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float radius = Mathf.Min(r.width, r.height) * 0.5f;
            Vector2 center = r.center;
            vh.AddVert(center, color, Vector2.zero);
            const int steps = 16;
            for (int half = 0; half < 2; half++)
                for (int i = 0; i <= steps; i++)
                {
                    float angle = (-90f + half * 180f + i * 180f / steps) * Mathf.Deg2Rad;
                    float x = half == 0 ? r.xMax - radius : r.xMin + radius;
                    vh.AddVert(new Vector2(x + Mathf.Cos(angle) * radius,
                        center.y + Mathf.Sin(angle) * radius), color, Vector2.zero);
                }
            int count = (steps + 1) * 2;
            for (int i = 1; i <= count; i++)
                vh.AddTriangle(0, i, i == count ? 1 : i + 1);
        }
    }
}
