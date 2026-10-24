using UnityEngine;

namespace WeaponWorkshop
{
    // 화면 중앙 조준점 주위 4개의 눈금. 받은 탄퍼짐 각도(가장 넓은 총구 기준)만큼 벌어진다.
    // 퍼짐 각도를 카메라 시야각으로 화면 픽셀 반지름으로 바꿔, 눈금 안쪽이 실제로 탄이 퍼지는 범위와 같다.
    // 무기 조회·숨김 판정은 AimHudPresenter가 맡고, 이 그래픽은 전달받은 각도만 화면 크기로 바꿔 그린다.
    // UIManager 프리팹에 전체 화면으로 둔다.
    public sealed class SpreadCrosshair : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField, Min(1f)] private float tickLength = 6f;
        [SerializeField, Min(0.5f)] private float tickWidth = 1.5f;
        [SerializeField, Min(0f), Tooltip("퍼짐이 0이어도 남기는 중앙 점과의 간격(px).")]
        private float minimumGap = 3f;
        [SerializeField, Min(0.1f), Tooltip("벌어짐/좁혀짐 속도(1/초).")]
        private float followSpeed = 12f;

        private bool visible;
        private float radius;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            visible = false;
            radius = minimumGap;
        }

        public void SetState(bool show, float spreadDegrees, Camera view)
        {
            if (show)
            {
                float halfView = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float pixels = Mathf.Tan(spreadDegrees * Mathf.Deg2Rad) / halfView * view.pixelHeight * 0.5f;
                float scale = canvas != null ? Mathf.Max(canvas.scaleFactor, 0.001f) : 1f;
                float target = minimumGap + pixels / scale;
                radius = visible ? Mathf.Lerp(radius, target, 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime)) : target;
            }
            if (show != visible || show) SetVerticesDirty();
            visible = show;
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            if (!visible) return;
            Vector2 center = rectTransform.rect.center;
            float half = tickWidth * 0.5f;
            AddQuad(mesh, center + new Vector2(radius, -half), center + new Vector2(radius + tickLength, half));
            AddQuad(mesh, center + new Vector2(-radius - tickLength, -half), center + new Vector2(-radius, half));
            AddQuad(mesh, center + new Vector2(-half, radius), center + new Vector2(half, radius + tickLength));
            AddQuad(mesh, center + new Vector2(-half, -radius - tickLength), center + new Vector2(half, -radius));
        }

        private void AddQuad(UnityEngine.UI.VertexHelper mesh, Vector2 min, Vector2 max)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector2(min.x, min.y), color, Vector2.zero);
            mesh.AddVert(new Vector2(min.x, max.y), color, Vector2.zero);
            mesh.AddVert(new Vector2(max.x, max.y), color, Vector2.zero);
            mesh.AddVert(new Vector2(max.x, min.y), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
