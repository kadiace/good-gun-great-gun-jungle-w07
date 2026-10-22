using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 조립 셀의 외곽 표면을 화면에 연결한다. 배치·연결·입력 규칙에는 관여하지 않는다.
    [DefaultExecutionOrder(115)]
    public sealed class WorkshopPlanarExtentUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text[] labels;
        [SerializeField] private Image[] markers;
        [SerializeField] private Image[] connectors;
        [SerializeField, Range(.005f, .05f)] private float gapHeightRatio = .012f;
        [SerializeField, Range(.01f, .08f)] private float labelHeightRatio = .03f;
        [SerializeField, Range(.03f, .2f)] private float labelWidthRatio = .085f;
        [SerializeField, Range(.0001f, .005f)] private float lineWidthRatio = .0015f;
        [SerializeField, Range(.001f, .02f)] private float markerSizeRatio = .006f;
        private readonly Vector3Int[] directions = new Vector3Int[4];
        private readonly Vector3Int[] extremes = new Vector3Int[4];
        private WorkshopPlanarView view;
        private RectTransform overlay;
        private Canvas ownerCanvas;
        private bool visible;

        private void Awake()
        {
            overlay = (RectTransform)transform;
            ownerCanvas = GetComponentInParent<Canvas>();
            for (int i = 0; i < 4; i++)
            {
                labels[i].raycastTarget = false;
                markers[i].raycastTarget = connectors[i].raycastTarget = false;
            }
            HideAll();
        }

        public void Bind(WorkshopPlanarView planarView, bool show)
        {
            view = planarView;
            visible = show;
            if (!show || planarView == null) HideAll();
        }

        private void LateUpdate()
        {
            if (!visible || view == null || !view.Is2D || view.Assembly == null || view.Camera == null || ownerCanvas == null)
            { HideAll(); return; }
            directions[0] = view.Horizontal;
            directions[1] = -view.Horizontal;
            directions[2] = view.Vertical;
            directions[3] = -view.Vertical;
            FindExtremes();
            UpdateLabels();
        }

        private void FindExtremes()
        {
            // 코어를 깊이 0의 단위 셀 후보로 포함하고 확정 모듈만 한 번 순회한다.
            for (int i = 0; i < 4; i++) extremes[i] = Vector3Int.zero;
            Transform assembly = view.Assembly.transform;
            for (int index = 0; index < assembly.childCount; index++)
            {
                Transform child = assembly.GetChild(index);
                if (!child.gameObject.activeInHierarchy || !child.TryGetComponent(out PlacedBlock block)) continue;
                for (int i = 0; i < 4; i++)
                    if (IsBetter(block.Cell, extremes[i], i)) extremes[i] = block.Cell;
            }
        }

        private bool IsBetter(Vector3Int candidate, Vector3Int current, int index)
        {
            Vector3Int direction = directions[index];
            int extent = Dot(candidate, direction), previousExtent = Dot(current, direction);
            // 모든 셀의 외측 면은 +0.5로 동일하므로 비교에는 정수 셀 좌표를 사용한다.
            if (extent != previousExtent) return extent > previousExtent;
            Vector3Int transverse = index < 2 ? view.Vertical : view.Horizontal;
            int offset = Mathf.Abs(Dot(candidate, transverse)), previousOffset = Mathf.Abs(Dot(current, transverse));
            if (offset != previousOffset) return offset < previousOffset;
            int depth = Dot(candidate, view.Normal), previousDepth = Dot(current, view.Normal);
            if (depth != previousDepth) return depth > previousDepth;
            if (candidate.x != current.x) return candidate.x < current.x;
            if (candidate.y != current.y) return candidate.y < current.y;
            return candidate.z < current.z;
        }

        private static int Dot(Vector3Int a, Vector3Int b) => a.x * b.x + a.y * b.y + a.z * b.z;

        private void UpdateLabels()
        {
            Camera camera = view.Camera;
            Transform assembly = view.Assembly.transform;
            Camera uiCamera = ownerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : ownerCanvas.worldCamera;
            Quaternion relative = Quaternion.Inverse(camera.transform.rotation) * assembly.rotation;
            float height = camera.pixelHeight;
            for (int i = 0; i < 4; i++)
            {
                Vector3Int direction = directions[i];
                Vector3 surface = assembly.TransformPoint((Vector3)extremes[i] + (Vector3)direction * .5f);
                Vector3 screen = camera.WorldToScreenPoint(surface);
                Vector3 projectedDirection = relative * (Vector3)direction;
                Vector2 outward = new Vector2(projectedDirection.x, projectedDirection.y);
                bool show = screen.z >= camera.nearClipPlane && screen.z <= camera.farClipPlane &&
                    camera.pixelRect.Contains(new Vector2(screen.x, screen.y)) && outward.sqrMagnitude > .0001f;
                SetVisible(i, show);
                if (!show) continue;
                outward.Normalize();
                Vector2 surfaceScreen = new Vector2(screen.x, screen.y);
                Vector2 textScreen = surfaceScreen + outward * (height * gapHeightRatio);
                Vector2 surfaceLocal = ToLocal(surfaceScreen, uiCamera);
                Vector2 textLocal = ToLocal(textScreen, uiCamera);
                Vector2 localDirection = (textLocal - surfaceLocal).normalized;
                Color tint = WorkshopAxesGraphic.AxisColor(AxisFor(direction));
                TMP_Text label = labels[i];
                string caption = CaptionFor(direction);
                if (label.text != caption) label.text = caption;
                label.color = tint;
                var textRect = label.rectTransform;
                textRect.pivot = Mathf.Abs(localDirection.x) > Mathf.Abs(localDirection.y)
                    ? new Vector2(localDirection.x > 0f ? 0f : 1f, .5f)
                    : new Vector2(.5f, localDirection.y > 0f ? 0f : 1f);
                textRect.anchoredPosition = textLocal - overlay.rect.center;
                textRect.sizeDelta = LocalSize(textScreen,
                    new Vector2(camera.pixelWidth * labelWidthRatio, height * labelHeightRatio), uiCamera);
                label.fontSize = textRect.sizeDelta.y * .72f;
                var markerRect = markers[i].rectTransform;
                markerRect.anchoredPosition = surfaceLocal - overlay.rect.center;
                markerRect.sizeDelta = LocalSize(surfaceScreen, Vector2.one * (height * markerSizeRatio), uiCamera);
                markers[i].color = tint;
                var lineRect = connectors[i].rectTransform;
                Vector2 delta = textLocal - surfaceLocal;
                lineRect.anchoredPosition = (textLocal + surfaceLocal) * .5f - overlay.rect.center;
                float thickness = LocalSize(surfaceScreen, Vector2.one * (height * lineWidthRatio), uiCamera).y;
                lineRect.sizeDelta = new Vector2(delta.magnitude, thickness);
                lineRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                connectors[i].color = tint;
            }
        }

        private Vector2 ToLocal(Vector2 screen, Camera uiCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen, uiCamera, out Vector2 local);
            return local;
        }
        private Vector2 LocalSize(Vector2 position, Vector2 screenSize, Camera uiCamera)
        {
            Vector2 delta = ToLocal(position + screenSize * .5f, uiCamera) - ToLocal(position - screenSize * .5f, uiCamera);
            return new Vector2(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
        }
        private static WorkshopViewAxis AxisFor(Vector3Int direction) => direction.x != 0 ? WorkshopViewAxis.Right :
            direction.y != 0 ? WorkshopViewAxis.Up : WorkshopViewAxis.Front;
        private static string CaptionFor(Vector3Int direction) => direction.x > 0 ? "+RIGHT" : direction.x < 0 ? "-RIGHT" :
            direction.y > 0 ? "+UP" : direction.y < 0 ? "-UP" : direction.z > 0 ? "+FRONT" : "-FRONT";

        private void SetVisible(int index, bool show)
        {
            if (labels[index].gameObject.activeSelf != show) labels[index].gameObject.SetActive(show);
            if (markers[index].gameObject.activeSelf != show) markers[index].gameObject.SetActive(show);
            if (connectors[index].gameObject.activeSelf != show) connectors[index].gameObject.SetActive(show);
        }
        public void HideAll()
        {
            if (labels == null || markers == null || connectors == null || labels.Length != 4 || markers.Length != 4 || connectors.Length != 4) return;
            for (int i = 0; i < 4; i++) SetVisible(i, false);
        }
        private void OnDisable() { visible = false; HideAll(); }
    }
}
