using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 3D/2D 작업대의 코어 정면과 실제 발사 출구를 하나의 UI 메시로 투영한다.
    [DefaultExecutionOrder(115)]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopPlanarDirectionsGraphic : MaskableGraphic
    {
        [SerializeField] private Color muzzleColor = Color.red;
        [SerializeField] private Color forwardColor = Color.white;
        [SerializeField] private TMP_Text forwardLabel;
        [SerializeField, Range(.001f, .025f), Tooltip("시선과 평행한 정면 표식 크기 / 화면의 짧은 변")]
        private float forwardDiamondSizeRatio = .006f;
        [SerializeField, Min(.1f), Tooltip("장착 정면 화살표 길이(코어 셀 단위)")]
        private float forwardArrowLength = 1.5f;
        [SerializeField, Min(0f), Tooltip("가장 앞선 모듈 끝과 정면 화살표 사이 간격(셀 단위)")]
        private float forwardArrowGap = .12f;
        [SerializeField, Min(.1f), Tooltip("실제 총구의 직선 방향 안내 길이(셀 단위). 탄도 시뮬레이션은 하지 않습니다.")]
        private float muzzleLineLength = 4f;
        private struct Marker
        {
            public Vector3 origin;
            public Vector3Int direction;
            public int count;
            public string countLabel;
        }

        private readonly List<Marker> markers = new List<Marker>();
        private readonly Dictionary<(Vector3Int, Vector3Int), int> projectedMuzzles =
            new Dictionary<(Vector3Int, Vector3Int), int>();
        private WorkshopPlanarView view;
        private Camera viewCamera;
        private CubeAssembly assembly;
        private WorkshopViewAxis cachedAxis;
        private uint cachedStructureRevision;
        private bool visible, structureDirty, cachedPlanar;
        private float forwardDepth;
        private bool IsPlanar => view != null && view.Is2D;
        // 시작점은 코어 중심 축에 두고, 조립 전체의 가장 앞선 셀 끝을 지난다.
        private Vector3 ForwardMarkerWorld => assembly.transform.TransformPoint(Vector3.forward * (forwardDepth + .5f + forwardArrowGap));
        private Vector3 ForwardTipWorld => assembly.transform.TransformPoint(Vector3.forward * (forwardDepth + .5f + forwardArrowGap + forwardArrowLength));
        private float ScreenSize => Mathf.Min(viewCamera.pixelWidth, viewCamera.pixelHeight);
        private float DiamondPixels => ScreenSize * forwardDiamondSizeRatio;
        // Seven-segment digits keep overlap counts in the same batched UI mesh.
        private static readonly byte[] Digits = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f };

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            visible = false;
            if (forwardLabel != null)
            {
                forwardLabel.text = "장착 정면";
                forwardLabel.color = forwardColor;
                forwardLabel.raycastTarget = false;
                forwardLabel.enableAutoSizing = true;
                forwardLabel.fontSizeMin = 9f;
                forwardLabel.fontSizeMax = 18f;
                forwardLabel.gameObject.SetActive(false);
            }
            SetVerticesDirty();
        }

        public void Bind(WorkshopPlanarView planarView, bool show)
            => Bind(planarView, planarView != null ? planarView.Assembly : null,
                planarView != null ? planarView.Camera : null, show);

        public void Bind(WorkshopPlanarView planarView, CubeAssembly target, Camera camera, bool show)
        {
            view = planarView;
            viewCamera = camera;
            if (assembly != target)
            {
                ReleaseAssembly();
                assembly = target;
                structureDirty = true;
            }
            visible = show && assembly != null && viewCamera != null;
            if (visible) return;
            HideForwardLabel();
            ReleaseAssembly();
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            if (!visible || assembly == null || viewCamera == null)
            {
                HideForwardLabel();
                ReleaseAssembly();
                SetVerticesDirty();
                return;
            }
            if (structureDirty || cachedStructureRevision != assembly.Analysis.StructureRevision ||
                cachedPlanar != IsPlanar || (IsPlanar && cachedAxis != view.Axis)) RebuildMarkers();
            UpdateForwardLabel();
            SetVerticesDirty();
        }

        private void ReleaseAssembly()
        {
            assembly = null;
            markers.Clear();
            projectedMuzzles.Clear();
        }

        private void RebuildMarkers()
        {
            markers.Clear();
            projectedMuzzles.Clear();
            forwardDepth = 0f;
            foreach (PlacedBlock block in assembly.Analysis.Blocks) forwardDepth = Mathf.Max(forwardDepth, block.Cell.z);
            foreach (var muzzle in assembly.Analysis.Paths)
            {
                // 직접 코어 출력의 발사 원점은 중심이지만, 표식만 코어의 해당 면에 붙인다.
                Vector3 origin = muzzle.path.Length == 0 ? (Vector3)muzzle.direction * .5f : muzzle.origin;
                Vector3Int position = IsPlanar ? new Vector3Int(
                    Mathf.RoundToInt(Vector3.Dot(origin, view.Horizontal) * 2f),
                    Mathf.RoundToInt(Vector3.Dot(origin, view.Vertical) * 2f), 0) : Vector3Int.RoundToInt(origin * 2f);
                var key = (position, muzzle.direction);
                if (projectedMuzzles.TryGetValue(key, out int index))
                {
                    Marker marker = markers[index];
                    marker.count++;
                    marker.countLabel = marker.count.ToString();
                    markers[index] = marker;
                }
                else
                {
                    projectedMuzzles.Add(key, markers.Count);
                    markers.Add(new Marker { origin = origin, direction = muzzle.direction, count = 1 });
                }
            }
            if (view != null) cachedAxis = view.Axis;
            cachedPlanar = IsPlanar;
            cachedStructureRevision = assembly.Analysis.StructureRevision;
            structureDirty = false;
        }

        private void HideForwardLabel()
        {
            if (forwardLabel != null && forwardLabel.gameObject.activeSelf) forwardLabel.gameObject.SetActive(false);
        }

        private void UpdateForwardLabel()
        {
            if (forwardLabel == null || canvas == null)
            { HideForwardLabel(); return; }
            Vector3 anchor = IsOnScreen(ForwardTipWorld) ? ForwardTipWorld : ForwardMarkerWorld;
            if (!IsOnScreen(anchor)) { HideForwardLabel(); return; }
            Vector2 point = Project(anchor);
            float gapPixels = DiamondPixels * .5f + ScreenSize * .004f;
            Vector2 gap = ScreenSizeInLocal(new Vector2(gapPixels, 0f));
            RectTransform labelRect = forwardLabel.rectTransform;
            labelRect.pivot = new Vector2(0f, .5f);
            forwardLabel.alignment = TextAlignmentOptions.Left;
            Vector2 local = point + gap;
            labelRect.localPosition = new Vector3(local.x, local.y, 0f);
            labelRect.sizeDelta = ScreenSizeInLocal(new Vector2(viewCamera.pixelWidth * .14f, viewCamera.pixelHeight * .03f));
            forwardLabel.color = DirectionTint(forwardColor, Vector3Int.forward);
            if (!forwardLabel.gameObject.activeSelf) forwardLabel.gameObject.SetActive(true);
        }

        private Vector2 ScreenSizeInLocal(Vector2 size)
        {
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, Vector2.zero, uiCamera, out Vector2 a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, size, uiCamera, out Vector2 b);
            return new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!visible || assembly == null || canvas == null || viewCamera == null) return;
            Transform root = assembly.transform;
            Vector2 lineSize = ScreenSizeInLocal(new Vector2(Mathf.Max(1.5f, ScreenSize * .0015f), ScreenSize * .007f));
            Vector2 countSize = ScreenSizeInLocal(Vector2.one * ScreenSize * .055f);
            foreach (Marker marker in markers)
            {
                Vector3 start = root.TransformPoint(marker.origin);
                Vector3 end = root.TransformPoint(marker.origin + (Vector3)marker.direction * muzzleLineLength);
                if (!TryProjectSegment(start, end, out Vector2 a, out Vector2 b)) continue;
                Vector2 delta = b - a;
                float length = delta.magnitude;
                if (length < lineSize.x) continue;
                Vector2 unit = delta / length;
                Color tint = DirectionTint(muzzleColor, marker.direction);
                float dash = Mathf.Max(lineSize.y, lineSize.x * 3f);
                for (float distance = 0f; distance < length; distance += dash * 1.6f)
                {
                    Color faded = tint;
                    faded.a *= Mathf.Lerp(1f, .25f, distance / length);
                    Line(mesh, a + unit * distance, a + unit * Mathf.Min(distance + dash, length), lineSize.x, faded);
                }
                if (marker.countLabel != null && IsOnScreen(start))
                    Count(mesh, marker.countLabel, Project(start), unit, countSize.x, countSize.y, tint);
            }
            Color forwardTint = DirectionTint(forwardColor, Vector3Int.forward);
            if (TryProjectSegment(ForwardMarkerWorld, ForwardTipWorld, out Vector2 arrowStart, out Vector2 arrowEnd))
            {
                Vector2 delta = arrowEnd - arrowStart;
                Vector2 width = ScreenSizeInLocal(Vector2.one * ScreenSize * .014f);
                if (delta.magnitude > width.x)
                    Arrow(mesh, arrowStart, delta.normalized, delta.magnitude, width.x, forwardTint);
                else if (IsOnScreen(ForwardMarkerWorld))
                    Diamond(mesh, Project(ForwardMarkerWorld), ScreenSizeInLocal(Vector2.one * DiamondPixels), forwardTint);
            }
        }

        private Color DirectionTint(Color tint, Vector3Int direction)
        {
            if (Vector3.Dot(viewCamera.transform.forward, assembly.transform.TransformDirection(direction)) > .05f) tint.a *= .2f;
            return tint;
        }

        private static void Arrow(VertexHelper mesh, Vector2 start, Vector2 direction, float length, float width, Color tint)
        {
            Vector2 side = new Vector2(-direction.y, direction.x);
            Vector2 neck = start + direction * (length * .58f);
            int i = mesh.currentVertCount;
            mesh.AddVert(start - side * (width * .17f), tint, Vector2.zero);
            mesh.AddVert(start + side * (width * .17f), tint, Vector2.zero);
            mesh.AddVert(neck + side * (width * .17f), tint, Vector2.zero);
            mesh.AddVert(neck - side * (width * .17f), tint, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
            i = mesh.currentVertCount;
            mesh.AddVert(neck - side * (width * .5f), tint, Vector2.zero);
            mesh.AddVert(neck + side * (width * .5f), tint, Vector2.zero);
            mesh.AddVert(start + direction * length, tint, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2);
        }

        private static void Count(VertexHelper mesh, string text, Vector2 start, Vector2 direction,
            float cellWidth, float cellHeight, Color tint)
        {
            Vector2 side = new Vector2(-direction.y, direction.x);
            Vector2 anchor = start + side * (Mathf.Abs(side.x) * cellWidth + Mathf.Abs(side.y) * cellHeight) * .29f;
            float height = cellHeight * .18f;
            float digitWidth = Mathf.Min(cellWidth * .09f, cellWidth * .3f / text.Length);
            float thickness = Mathf.Min(digitWidth * .18f, height * .1f);
            for (int digit = 0; digit < text.Length; digit++)
            {
                Vector2 center = anchor + Vector2.right * (digit * digitWidth * 1.25f);
                byte bits = Digits[text[digit] - '0'];
                for (int segment = 0; segment < 7; segment++)
                {
                    if ((bits & (1 << segment)) == 0) continue;
                    Vector2 a, b;
                    switch (segment)
                    {
                        case 0: a = new Vector2(0f, 1f); b = new Vector2(1f, 1f); break;
                        case 1: a = new Vector2(1f, 1f); b = new Vector2(1f, .5f); break;
                        case 2: a = new Vector2(1f, .5f); b = new Vector2(1f, 0f); break;
                        case 3: a = new Vector2(1f, 0f); b = Vector2.zero; break;
                        case 4: a = Vector2.zero; b = new Vector2(0f, .5f); break;
                        case 5: a = new Vector2(0f, .5f); b = Vector2.up; break;
                        default: a = new Vector2(0f, .5f); b = new Vector2(1f, .5f); break;
                    }
                    Line(mesh, center + Vector2.Scale(a, new Vector2(digitWidth, height)),
                        center + Vector2.Scale(b, new Vector2(digitWidth, height)), thickness, tint);
                }
            }
        }

        private static void Diamond(VertexHelper mesh, Vector2 center, Vector2 size, Color tint)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(center + Vector2.up * size.y * .5f, tint, Vector2.zero);
            mesh.AddVert(center + Vector2.right * size.x * .5f, tint, Vector2.zero);
            mesh.AddVert(center - Vector2.up * size.y * .5f, tint, Vector2.zero);
            mesh.AddVert(center - Vector2.right * size.x * .5f, tint, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2);
            mesh.AddTriangle(i, i + 2, i + 3);
        }

        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
            => WorkshopPlanarGridGraphic.AddLine(mesh, a, b, width, tint);

        private Vector2 Project(Vector3 world)
        {
            Vector2 screen = viewCamera.WorldToScreenPoint(world);
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, uiCamera, out Vector2 local);
            return local;
        }

        private bool IsOnScreen(Vector3 world)
        {
            Vector3 screen = viewCamera.WorldToScreenPoint(world);
            return screen.z >= viewCamera.nearClipPlane && screen.z <= viewCamera.farClipPlane &&
                viewCamera.pixelRect.Contains(new Vector2(screen.x, screen.y));
        }

        private bool TryProjectSegment(Vector3 start, Vector3 end, out Vector2 a, out Vector2 b)
        {
            a = b = Vector2.zero;
            Vector3 cameraForward = viewCamera.transform.forward;
            Vector3 cameraPosition = viewCamera.transform.position;
            float near = viewCamera.nearClipPlane + .0001f;
            float startDepth = Vector3.Dot(start - cameraPosition, cameraForward);
            float endDepth = Vector3.Dot(end - cameraPosition, cameraForward);
            if (startDepth < near && endDepth < near) return false;
            if (startDepth < near) start = Vector3.Lerp(start, end, (near - startDepth) / (endDepth - startDepth));
            else if (endDepth < near) end = Vector3.Lerp(start, end, (near - startDepth) / (endDepth - startDepth));
            a = Project(start);
            b = Project(end);
            Vector2 delta = b - a;
            Rect bounds = rectTransform.rect;
            float enter = 0f, exit = 1f;
            if (!Clip(-delta.x, a.x - bounds.xMin, ref enter, ref exit) ||
                !Clip(delta.x, bounds.xMax - a.x, ref enter, ref exit) ||
                !Clip(-delta.y, a.y - bounds.yMin, ref enter, ref exit) ||
                !Clip(delta.y, bounds.yMax - a.y, ref enter, ref exit)) return false;
            b = a + delta * exit;
            a += delta * enter;
            return true;
        }

        private static bool Clip(float direction, float distance, ref float enter, ref float exit)
        {
            if (Mathf.Abs(direction) < .00001f) return distance >= 0f;
            float t = distance / direction;
            if (direction < 0f) { if (t > exit) return false; enter = Mathf.Max(enter, t); }
            else { if (t < enter) return false; exit = Mathf.Min(exit, t); }
            return true;
        }

        protected override void OnDisable()
        {
            visible = false;
            HideForwardLabel();
            ReleaseAssembly();
            base.OnDisable();
        }
    }
}
