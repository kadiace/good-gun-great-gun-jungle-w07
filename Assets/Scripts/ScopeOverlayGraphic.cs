using UnityEngine;

namespace WeaponWorkshop
{
    // 2배율은 짧은 중앙 조준선, 3배율 이상은 화면 끝까지 이어지는 십자 조준선을 그린다.
    public sealed class ScopeOverlayGraphic : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField, Min(0.5f)] private float crosshairWidth = 2f;
        [SerializeField] private Color crosshairColor = Color.black;
        [SerializeField, Min(2f)] private float reticleRadius = 12f;
        [SerializeField, Min(0.5f)] private float redDotRadius = 2.5f;
        [SerializeField] private Color redDotColor = Color.red;
        [SerializeField, Range(0.2f, 1f), Tooltip("고배율 십자선 길이(스코프 시야 대비).")]
        private float fullLengthReticleExtent = 0.7f;
        private Rect viewport = new Rect(0f, 0f, 1f, 1f);
        private bool fullLengthReticle;

        public void SetView(Rect normalizedViewport, bool useFullLengthReticle)
        {
            if (normalizedViewport == viewport && useFullLengthReticle == fullLengthReticle) return;
            viewport = normalizedViewport;
            fullLengthReticle = useFullLengthReticle;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Rect area = rectTransform.rect;
            Rect cell = new Rect(area.x + viewport.x * area.width, area.y + viewport.y * area.height,
                viewport.width * area.width, viewport.height * area.height);
            if (fullLengthReticle) DrawFullLengthReticle(mesh, cell);
            else DrawCenteredReticle(mesh, cell);
            DrawRedDot(mesh, cell.center);
        }

        private void DrawCenteredReticle(UnityEngine.UI.VertexHelper mesh, Rect cell)
        {
            Vector2 center = cell.center;
            float half = crosshairWidth * 0.5f;
            AddQuad(mesh, center + new Vector2(-reticleRadius, -half), center + new Vector2(reticleRadius, half));
            AddQuad(mesh, center + new Vector2(-half, -reticleRadius), center + new Vector2(half, reticleRadius));
        }

        private void DrawFullLengthReticle(UnityEngine.UI.VertexHelper mesh, Rect cell)
        {
            Vector2 center = cell.center;
            float half = crosshairWidth * 0.5f;
            float halfLengthX = cell.width * fullLengthReticleExtent * 0.5f;
            float halfLengthY = cell.height * fullLengthReticleExtent * 0.5f;
            AddQuad(mesh, new Vector2(center.x - halfLengthX, center.y - half),
                new Vector2(center.x + halfLengthX, center.y + half));
            AddQuad(mesh, new Vector2(center.x - half, center.y - halfLengthY),
                new Vector2(center.x + half, center.y + halfLengthY));
        }

        private void AddQuad(UnityEngine.UI.VertexHelper mesh, Vector2 min, Vector2 max)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector2(min.x, min.y), crosshairColor, Vector2.zero);
            mesh.AddVert(new Vector2(min.x, max.y), crosshairColor, Vector2.zero);
            mesh.AddVert(new Vector2(max.x, max.y), crosshairColor, Vector2.zero);
            mesh.AddVert(new Vector2(max.x, min.y), crosshairColor, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }

        private void DrawRedDot(UnityEngine.UI.VertexHelper mesh, Vector2 center)
        {
            const int segments = 16;
            int centerIndex = mesh.currentVertCount;
            mesh.AddVert(center, redDotColor, Vector2.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * redDotRadius;
                mesh.AddVert(point, redDotColor, Vector2.zero);
            }
            for (int i = 0; i < segments; i++)
                mesh.AddTriangle(centerIndex, centerIndex + 1 + i, centerIndex + 1 + (i + 1) % segments);
        }
    }
}
