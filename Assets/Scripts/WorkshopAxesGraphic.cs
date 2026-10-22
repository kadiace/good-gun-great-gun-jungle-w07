using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopAxesGraphic : MaskableGraphic
    {
        public struct ProjectedAxis
        {
            public Vector2 Point;
            public float Depth;
            public Color Tint;
        }
        [SerializeField, Range(.1f, .4f)] private float radiusRatio = .3f;
        private readonly ProjectedAxis[] projected = new ProjectedAxis[6];
        private readonly int[] drawOrder = new int[6];
        private Quaternion previousRotation;
        private Rect previousRect;
        private float previousRadius;
        private int selected = -1;
        private bool hasPose;
        public ProjectedAxis GetAxis(int index) => projected[index];
        public int GetDrawOrder(int index) => drawOrder[index];
        public static Color AxisColor(WorkshopViewAxis axis) =>
            axis == WorkshopViewAxis.Front || axis == WorkshopViewAxis.Back ? new Color(0f, .7f, 1f) :
            axis == WorkshopViewAxis.Up || axis == WorkshopViewAxis.Down ? new Color(.12f, .8f, .25f) :
            new Color(1f, .12f, .12f);

        protected override void Awake() { base.Awake(); raycastTarget = false; }

        // ViewUI가 최종 Transform을 한 번 읽는다. 메시와 버튼은 이 투영 결과를 공유한다.
        public void SetOrientation(Quaternion relativeRotation, int selectedAxis)
        {
            Rect rect = rectTransform.rect;
            if (hasPose && relativeRotation.Equals(previousRotation) && rect == previousRect &&
                selected == selectedAxis && previousRadius == radiusRatio) return;
            previousRotation = relativeRotation; previousRect = rect; previousRadius = radiusRatio;
            selected = selectedAxis; hasPose = true;
            float radius = Mathf.Min(rect.width, rect.height) * radiusRatio;
            for (int i = 0; i < 6; i++)
            {
                Vector3 direction = relativeRotation * (Vector3)WorkshopPlanarView.NormalFor((WorkshopViewAxis)i);
                Color tint = AxisColor((WorkshopViewAxis)i);
                // 양의 z는 카메라 반대편이다. 평면 성분을 정규화하지 않아 축이 짧아지는 표현을 유지한다.
                if (direction.z > .001f) tint = Color.Lerp(Color.black, tint, .5f);
                tint.a = 1f;
                projected[i] = new ProjectedAxis
                {
                    Point = rect.center + new Vector2(direction.x, direction.y) * radius,
                    Depth = direction.z,
                    Tint = tint
                };
                drawOrder[i] = i;
            }
            // 미세한 깊이 차이는 양자화하고 같은 깊이는 축 ID 순서로 고정한다.
            for (int i = 1; i < 6; i++)
            {
                int item = drawOrder[i], j = i - 1;
                while (j >= 0 && ComesBefore(item, drawOrder[j]))
                { drawOrder[j + 1] = drawOrder[j]; j--; }
                drawOrder[j + 1] = item;
            }
            SetVerticesDirty();
        }

        private bool ComesBefore(int a, int b)
        {
            int depthA = Mathf.RoundToInt(projected[a].Depth * 1000f);
            int depthB = Mathf.RoundToInt(projected[b].Depth * 1000f);
            return depthA != depthB ? depthA > depthB : a < b;
        }

        public void ClearOrientation()
        {
            if (!hasPose) return;
            hasPose = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!hasPose) return;
            Vector2 center = rectTransform.rect.center;
            float size = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height);
            for (int order = 0; order < 6; order++)
            {
                int index = drawOrder[order];
                ProjectedAxis axis = projected[index];
                Vector2 delta = axis.Point - center;
                float width = size * (index == selected ? .027f : .018f);
                if (delta.sqrMagnitude > size * size * .0004f)
                {
                    Vector2 direction = delta.normalized;
                    Vector2 side = new Vector2(-direction.y, direction.x);
                    float head = Mathf.Min(size * .065f, delta.magnitude * .35f);
                    WorkshopPlanarGridGraphic.AddLine(mesh, center, axis.Point, width, axis.Tint);
                    WorkshopPlanarGridGraphic.AddLine(mesh, axis.Point,
                        axis.Point - direction * head + side * head * .55f, width, axis.Tint);
                    WorkshopPlanarGridGraphic.AddLine(mesh, axis.Point,
                        axis.Point - direction * head - side * head * .55f, width, axis.Tint);
                }
                // 시선과 평행한 축에도 중심 표식을 남긴다. 길이가 0인 화살표는 생성하지 않는다.
                float marker = size * (index == selected ? .04f : .027f);
                WorkshopPlanarGridGraphic.AddLine(mesh, axis.Point - Vector2.right * marker,
                    axis.Point + Vector2.right * marker, marker * 2f, axis.Tint);
            }
        }
        protected override void OnDisable() { ClearOrientation(); base.OnDisable(); }
    }
}
