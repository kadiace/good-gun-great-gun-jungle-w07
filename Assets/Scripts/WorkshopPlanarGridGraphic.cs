using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 조립 평면을 화면에 투영한다. UI 입력을 받지 않고 셀 크기·팬·줌을 그대로 따른다.
    [DefaultExecutionOrder(110)]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopPlanarGridGraphic : MaskableGraphic
    {
        [SerializeField, Range(.0001f, .02f)] private float lineWidthRatio = .005f;
        [SerializeField] private Color validColor = new Color(.15f, 1f, .3f, 1f);
        [SerializeField] private Color invalidColor = new Color(1f, .18f, .14f, 1f);
        private WorkshopPlanarView view;
        private CubeBuilder builder;
        public void Bind(WorkshopPlanarView planarView, CubeBuilder cubeBuilder)
        { view = planarView; builder = cubeBuilder; }
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        private void LateUpdate() { if (view != null && view.Is2D) SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (view == null || !view.Is2D || view.Assembly == null || canvas == null) return;
            var root = view.Assembly.transform;
            Vector2 origin = Project(root.position);
            float stepX = Mathf.Abs(Project(root.TransformPoint(view.Horizontal)).x - origin.x);
            float stepY = Mathf.Abs(Project(root.TransformPoint(view.Vertical)).y - origin.y);
            if (stepX <= .001f || stepY <= .001f) return;
            Rect bounds = rectTransform.rect;
            float width = view.Camera.pixelHeight * lineWidthRatio / Mathf.Max(.001f, canvas.scaleFactor);
            int firstX = Mathf.FloorToInt((bounds.xMin - origin.x) / stepX - .5f);
            int firstY = Mathf.FloorToInt((bounds.yMin - origin.y) / stepY - .5f);
            int countX = Mathf.Min(512, Mathf.CeilToInt(bounds.width / stepX) + 3);
            int countY = Mathf.Min(512, Mathf.CeilToInt(bounds.height / stepY) + 3);
            for (int i = 0; i < countX; i++)
            {
                float x = origin.x + (firstX + i + .5f) * stepX;
                if (x >= bounds.xMin && x <= bounds.xMax)
                    AddLine(mesh, new Vector2(x, bounds.yMin), new Vector2(x, bounds.yMax), width, color);
            }
            for (int i = 0; i < countY; i++)
            {
                float y = origin.y + (firstY + i + .5f) * stepY;
                if (y >= bounds.yMin && y <= bounds.yMax)
                    AddLine(mesh, new Vector2(bounds.xMin, y), new Vector2(bounds.xMax, y), width, color);
            }
            if (builder == null || !builder.TryGetPlanarHover(out Vector3Int cell, out bool valid)) return;
            Vector2 center = Project(root.TransformPoint(cell));
            Color tint = valid ? validColor : invalidColor;
            Vector2 a = center + new Vector2(-stepX, -stepY) * .5f;
            Vector2 b = center + new Vector2(stepX, -stepY) * .5f;
            Vector2 c = center + new Vector2(stepX, stepY) * .5f;
            Vector2 d = center + new Vector2(-stepX, stepY) * .5f;
            AddLine(mesh, a, b, width, tint); AddLine(mesh, b, c, width, tint);
            AddLine(mesh, c, d, width, tint); AddLine(mesh, d, a, width, tint);
        }

        private Vector2 Project(Vector3 world)
        {
            Vector2 screen = view.Camera.WorldToScreenPoint(world);
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, uiCamera, out Vector2 local);
            return local;
        }

        internal static void AddLine(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 delta = (b - a).normalized;
            Vector2 normal = new Vector2(-delta.y, delta.x) * width * .5f;
            int i = mesh.currentVertCount;
            mesh.AddVert(a - normal, tint, Vector2.zero); mesh.AddVert(a + normal, tint, Vector2.zero);
            mesh.AddVert(b + normal, tint, Vector2.zero); mesh.AddVert(b - normal, tint, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
