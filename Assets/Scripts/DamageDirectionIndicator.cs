using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class DamageDirectionIndicator : Graphic
    {
        private const int ArcSegments = 24;
        private const float DirectionLookbackSeconds = 0.5f;
        private const int ViewHistoryCapacity = 128;

        private struct ViewSample
        {
            public float time;
            public Quaternion rotation;
        }

        [SerializeField] private UIManager uiManager;
        [SerializeField, Min(1f)] private float radiusPixels = 54f;
        [SerializeField, Min(1f)] private float thicknessPixels = 6f;
        [SerializeField, Range(5f, 180f)] private float arcAngleDegrees = 48f;
        [SerializeField, Min(0.1f)] private float displayDuration = 1.1f;
        [SerializeField] private Color indicatorColor = new Color(1f, 0.06f, 0.08f, 1f);

        private RaidPlayerHealth health;
        private readonly ViewSample[] viewHistory = new ViewSample[ViewHistoryCapacity];
        private Quaternion referenceViewRotation = Quaternion.identity;
        private Vector3 incomingDirectionInReferenceView;
        private Vector2 screenDirection = Vector2.up;
        private float remaining;
        private int viewHistoryCount;
        private int nextViewSample;

        public void Initialize(UIManager owner)
        {
            uiManager = owner;
            ClearIndicator();
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            BindHealth();
        }

        public void Hide()
        {
            ClearIndicator();
            if (gameObject.activeSelf) gameObject.SetActive(false);
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
            BindHealth();
            RecordViewSample();
        }

        protected override void OnDisable()
        {
            UnbindHealth();
            ClearIndicator();
            base.OnDisable();
        }

        private void LateUpdate()
        {
            RecordViewSample();
            BindHealth();
            if (remaining <= 0f) return;
            if (GameManager.GameplayBlocked || health == null || health.IsDead)
            {
                ClearIndicator();
                return;
            }

            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f)
            {
                ClearIndicator();
                return;
            }

            UpdateScreenDirection();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (remaining <= 0f) return;

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

        private void BindHealth()
        {
            FirstPersonPlayer player = uiManager != null ? uiManager.Player : null;
            RaidPlayerHealth nextHealth = player != null ? player.GetComponent<RaidPlayerHealth>() : null;
            if (health == nextHealth) return;

            UnbindHealth();
            health = nextHealth;
            if (isActiveAndEnabled && health != null)
                health.HitReceived += OnHitReceived;
        }

        private void UnbindHealth()
        {
            if (health != null) health.HitReceived -= OnHitReceived;
            health = null;
        }

        private void OnHitReceived(Vector3 sourceDirection)
        {
            if (!isActiveAndEnabled || GameManager.GameplayBlocked || uiManager == null) return;
            FirstPersonPlayer player = uiManager.Player;
            if (player == null) return;

            Transform view = player.ViewPoint;
            bool hasReference = TryGetReferenceView(out ViewSample reference);
            referenceViewRotation = hasReference ? reference.rotation : view != null ? view.rotation : player.transform.rotation;
            if (sourceDirection.sqrMagnitude < 0.001f) sourceDirection = -player.transform.forward;
            incomingDirectionInReferenceView = Quaternion.Inverse(referenceViewRotation) * sourceDirection.normalized;
            remaining = Mathf.Max(0.1f, displayDuration);
            UpdateScreenDirection();
            SetVerticesDirty();
        }

        private void UpdateScreenDirection()
        {
            FirstPersonPlayer player = uiManager != null ? uiManager.Player : null;
            Transform view = player != null ? player.ViewPoint : null;
            if (view == null || incomingDirectionInReferenceView.sqrMagnitude < 0.001f)
            {
                screenDirection = Vector2.up;
                return;
            }

            Vector3 worldDirection = referenceViewRotation * incomingDirectionInReferenceView;
            Vector3 localDirection = view.InverseTransformDirection(worldDirection);
            Vector2 projected = new Vector2(localDirection.x, localDirection.y);
            if (projected.sqrMagnitude < 0.001f)
                projected = localDirection.z < 0f ? Vector2.down : Vector2.up;
            screenDirection = projected.normalized;
        }

        private void ClearIndicator()
        {
            remaining = 0f;
            referenceViewRotation = Quaternion.identity;
            incomingDirectionInReferenceView = Vector3.zero;
            screenDirection = Vector2.up;
            SetVerticesDirty();
        }

        private void RecordViewSample()
        {
            FirstPersonPlayer player = uiManager != null ? uiManager.Player : null;
            Transform view = player != null ? player.ViewPoint : null;
            if (view == null) return;

            ViewSample sample = new ViewSample
            {
                time = Time.unscaledTime,
                rotation = view.rotation
            };
            int latest = (nextViewSample - 1 + ViewHistoryCapacity) % ViewHistoryCapacity;
            if (viewHistoryCount > 0 && sample.time - viewHistory[latest].time < 0.001f)
            {
                viewHistory[latest] = sample;
                return;
            }

            viewHistory[nextViewSample] = sample;
            nextViewSample = (nextViewSample + 1) % ViewHistoryCapacity;
            viewHistoryCount = Mathf.Min(viewHistoryCount + 1, ViewHistoryCapacity);
        }

        private bool TryGetReferenceView(out ViewSample reference)
        {
            reference = default;
            if (viewHistoryCount == 0) return false;

            float targetTime = Time.unscaledTime - DirectionLookbackSeconds;
            float closestDifference = float.PositiveInfinity;
            int oldest = (nextViewSample - viewHistoryCount + ViewHistoryCapacity) % ViewHistoryCapacity;
            for (int i = 0; i < viewHistoryCount; i++)
            {
                ViewSample sample = viewHistory[(oldest + i) % ViewHistoryCapacity];
                float difference = Mathf.Abs(sample.time - targetTime);
                if (difference >= closestDifference) continue;
                closestDifference = difference;
                reference = sample;
            }

            return true;
        }

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
