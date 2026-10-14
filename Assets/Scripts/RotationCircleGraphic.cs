using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class RotationCircleGraphic : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField, Min(0.5f)] private float lineWidth = 2f;
        private Rect previousViewport;
        private float previousRadius;
        private float previousScale;
        private TutorialRotationGuide tutorialGuide;

        public void SetGeometry(Rect viewport, float radius)
        {
            if (viewport == previousViewport && radius == previousRadius) return;
            previousViewport = viewport;
            previousRadius = radius;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            tutorialGuide = TutorialRotationGuide.None;
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            if (canvas == null) return;
            var tutorial = TutorialManager.Instance;
            var guide = tutorial != null && tutorial.IsEditingTutorialWorkbench && tutorial.CurrentStep != null
                ? tutorial.CurrentStep.rotationGuide : TutorialRotationGuide.None;
            if (previousScale == canvas.scaleFactor && tutorialGuide == guide) return;
            previousScale = canvas.scaleFactor;
            tutorialGuide = guide;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            if (canvas == null || previousRadius <= 0f) return;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,
                previousViewport.center, uiCamera, out Vector2 center);
            float scale = Mathf.Max(canvas.scaleFactor, 0.001f);
            float radius = previousRadius / scale;
            float halfWidth = lineWidth * 0.5f / scale;
            const int segments = 128;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + radial * (radius - halfWidth), color, Vector2.zero);
                mesh.AddVert(center + radial * (radius + halfWidth), color, Vector2.zero);
                if (i == 0) continue;
                int index = i * 2;
                mesh.AddTriangle(index - 2, index - 1, index);
                mesh.AddTriangle(index, index - 1, index + 1);
            }
            DrawTutorialArrows(mesh, center, radius, scale);
        }
        private void DrawTutorialArrows(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius, float scale)
        {
            if (tutorialGuide == TutorialRotationGuide.None) return;
            Color tint = new Color(1f, .8f, .15f, .95f);
            float width = 4f / scale, head = 14f / scale;
            if (tutorialGuide == TutorialRotationGuide.Tilt)
            {
                foreach (var direction in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                {
                    Vector2 start = center + direction * (radius * .6f);
                    Vector2 end = start + direction * (48f / scale);
                    AddLine(mesh, start, end, width, tint);
                    AddHead(mesh, end, direction, head, tint);
                }
                return;
            }
            float outerRadius = radius + 28f / scale;
            // Side arcs stay visible even when the circle touches the viewport's top and bottom.
            foreach (float offset in new[] { 0f, 180f })
            {
                const int segments = 24;
                for (int i = 0; i < segments; i++)
                    AddLine(mesh, ArcPoint(center, outerRadius, offset - 35f + 70f * i / segments),
                        ArcPoint(center, outerRadius, offset - 35f + 70f * (i + 1) / segments), width, tint);
                float startAngle = (offset - 35f) * Mathf.Deg2Rad;
                float endAngle = (offset + 35f) * Mathf.Deg2Rad;
                AddHead(mesh, ArcPoint(center, outerRadius, offset - 35f),
                    new Vector2(Mathf.Sin(startAngle), -Mathf.Cos(startAngle)), head, tint);
                AddHead(mesh, ArcPoint(center, outerRadius, offset + 35f),
                    new Vector2(-Mathf.Sin(endAngle), Mathf.Cos(endAngle)), head, tint);
            }
        }
        private static Vector2 ArcPoint(Vector2 center, float radius, float degrees)
            => center + new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * radius;
        private static void AddLine(UnityEngine.UI.VertexHelper mesh, Vector2 start, Vector2 end, float width, Color tint)
        {
            Vector2 delta = (end - start).normalized;
            Vector2 normal = new Vector2(-delta.y, delta.x) * width * .5f;
            int index = mesh.currentVertCount;
            mesh.AddVert(start - normal, tint, Vector2.zero); mesh.AddVert(start + normal, tint, Vector2.zero);
            mesh.AddVert(end + normal, tint, Vector2.zero); mesh.AddVert(end - normal, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }
        private static void AddHead(UnityEngine.UI.VertexHelper mesh, Vector2 tip, Vector2 direction, float size, Color tint)
        {
            Vector2 normal = new Vector2(-direction.y, direction.x) * size * .55f;
            int index = mesh.currentVertCount;
            mesh.AddVert(tip, tint, Vector2.zero);
            mesh.AddVert(tip - direction * size + normal, tint, Vector2.zero);
            mesh.AddVert(tip - direction * size - normal, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
        }
    }
}
