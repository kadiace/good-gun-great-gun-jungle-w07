using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 화면 중심 주위에 피격 방향을 호(arc)로 그리는 그래픽. 전달받은 방향·남은 시간만 화면에 반영한다.
    // 피격 구독·시점 이력·표시 시간 계산은 DamageDirectionPresenter가 맡는다. UIManager 프리팹에 전체 화면으로 둔다.
    public sealed class DamageDirectionIndicator : Graphic
    {
        private const int ArcSegments = 24;

        [SerializeField] private UIManager uiManager;
        [SerializeField, Min(1f)] private float radiusPixels = 54f;
        [SerializeField, Min(1f)] private float thicknessPixels = 6f;
        [SerializeField, Range(5f, 180f)] private float arcAngleDegrees = 48f;
        [SerializeField, Min(0.1f)] private float displayDuration = 1.1f;
        [SerializeField] private Color indicatorColor = new Color(1f, 0.06f, 0.08f, 1f);

        private Vector2 screenDirection = Vector2.up;
        private float remaining;

        public UIManager Owner => uiManager;
        public float DisplayDuration => displayDuration;

        public void Initialize(UIManager owner)
        {
            uiManager = owner;
            ClearIndicator();
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            ClearIndicator();
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        public void SetIndicator(Vector2 direction, float remainingSeconds)
        {
            screenDirection = direction;
            remaining = remainingSeconds;
            SetVerticesDirty();
        }

        public void ClearIndicator()
        {
            remaining = 0f;
            screenDirection = Vector2.up;
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            ClearIndicator();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ClearIndicator();
        }

        protected override void OnDisable()
        {
            ClearIndicator();
            base.OnDisable();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (remaining <= 0f)
            {
                return;
            }

            float fadeDuration = Mathf.Min(0.28f, displayDuration * 0.35f);
            float alpha = remaining < fadeDuration ? Mathf.Clamp01(remaining / fadeDuration) : 1f;
            Color vertexColor = indicatorColor;
            vertexColor.a *= alpha;

            Canvas ownerCanvas = canvas;
            float scale = ownerCanvas != null ? Mathf.Max(0.001f, ownerCanvas.scaleFactor) : 1f;
            float radius = Mathf.Max(1f, radiusPixels) / scale;
            float halfThickness = Mathf.Max(1f, thicknessPixels) / (2f * scale);
            float innerRadius = Mathf.Max(0f, radius - halfThickness);
            float outerRadius = radius + halfThickness;
            float halfArc = Mathf.Clamp(arcAngleDegrees, 5f, 180f) * 0.5f;
            float centerAngle = Mathf.Atan2(screenDirection.x, screenDirection.y) * Mathf.Rad2Deg;
            float startAngle = (centerAngle - halfArc) * Mathf.Deg2Rad;
            float arcRadians = Mathf.Clamp(arcAngleDegrees, 5f, 180f) * Mathf.Deg2Rad;

            for (int i = 0; i < ArcSegments; i++)
            {
                float angleA = startAngle + arcRadians * i / ArcSegments;
                float angleB = startAngle + arcRadians * (i + 1) / ArcSegments;
                Vector2 innerA = PointOnArc(angleA, innerRadius);
                Vector2 outerA = PointOnArc(angleA, outerRadius);
                Vector2 innerB = PointOnArc(angleB, innerRadius);
                Vector2 outerB = PointOnArc(angleB, outerRadius);

                int first = vertexHelper.currentVertCount;
                vertexHelper.AddVert(innerA, vertexColor, Vector2.zero);
                vertexHelper.AddVert(outerA, vertexColor, Vector2.zero);
                vertexHelper.AddVert(innerB, vertexColor, Vector2.zero);
                vertexHelper.AddVert(outerB, vertexColor, Vector2.zero);
                vertexHelper.AddTriangle(first, first + 2, first + 1);
                vertexHelper.AddTriangle(first + 1, first + 2, first + 3);
            }
        }

        private static Vector2 PointOnArc(float angle, float radius)
            => new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            radiusPixels = Mathf.Max(1f, radiusPixels);
            thicknessPixels = Mathf.Max(1f, thicknessPixels);
            displayDuration = Mathf.Max(0.1f, displayDuration);
            arcAngleDegrees = Mathf.Clamp(arcAngleDegrees, 5f, 180f);
            raycastTarget = false;
            SetVerticesDirty();
        }
#endif
    }
}
