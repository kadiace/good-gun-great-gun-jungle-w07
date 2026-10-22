using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopAxesGraphic : MaskableGraphic
    {
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            Vector2 start = rect.min + Vector2.Scale(rect.size, new Vector2(.43f, .43f));
            Arrow(mesh, start, rect.min + Vector2.Scale(rect.size, new Vector2(.43f, .87f)), new Color(.12f, .8f, .25f));
            Arrow(mesh, start, rect.min + Vector2.Scale(rect.size, new Vector2(.88f, .43f)), new Color(0f, .7f, 1f));
            Arrow(mesh, start, rect.min + Vector2.Scale(rect.size, new Vector2(.12f, .12f)), new Color(1f, .12f, .12f));
        }
        private void Arrow(VertexHelper mesh, Vector2 a, Vector2 b, Color tint)
        {
            float size = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height);
            WorkshopPlanarGridGraphic.AddLine(mesh, a, b, size * .025f, tint);
            Vector2 direction = (b - a).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            WorkshopPlanarGridGraphic.AddLine(mesh, b, b - direction * size * .12f + normal * size * .07f, size * .025f, tint);
            WorkshopPlanarGridGraphic.AddLine(mesh, b, b - direction * size * .12f - normal * size * .07f, size * .025f, tint);
        }
    }
}
