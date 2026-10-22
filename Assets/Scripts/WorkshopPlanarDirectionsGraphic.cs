using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 총구 목록은 발사 경로를 공유하고, 표시는 최종 작업대 자세를 Canvas에 투영한다.
    [DefaultExecutionOrder(115)]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopPlanarDirectionsGraphic : MaskableGraphic
    {
        [SerializeField] private Color muzzleColor = Color.red;
        [SerializeField] private Color forwardColor = Color.white;
        [SerializeField] private TMP_Text forwardLabel;
        private struct Marker
        {
            public Vector3 origin;
            public Vector3Int direction;
            public int count;
            public string countLabel;
        }

        private readonly List<Marker> markers = new List<Marker>();
        private readonly Dictionary<(Vector2Int, Vector3Int), int> projectedMuzzles =
            new Dictionary<(Vector2Int, Vector3Int), int>();
        private WorkshopPlanarView view;
        private CubeAssembly assembly;
        private EquipableWeapon subscribedWeapon;
        private WorkshopViewAxis cachedAxis;
        private bool visible, structureDirty;
        // Seven-segment digits keep overlap counts in the same batched UI mesh.
        private static readonly byte[] Digits = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f };

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            visible = false;
            if (forwardLabel != null)
            {
                forwardLabel.text = "장착 전방 (+Forward)";
                forwardLabel.color = forwardColor;
                forwardLabel.raycastTarget = false;
                forwardLabel.enableAutoSizing = true;
                forwardLabel.fontSizeMin = 12f;
                forwardLabel.fontSizeMax = 24f;
                forwardLabel.gameObject.SetActive(false);
            }
            SetVerticesDirty();
        }

        public void Bind(WorkshopPlanarView planarView, bool show)
        {
            view = planarView;
            visible = show && view != null;
            if (visible) return;
            HideForwardLabel();
            ReleaseAssembly();
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            if (!visible || view == null || !view.Is2D || view.Assembly == null || view.Camera == null)
            {
                HideForwardLabel();
                ReleaseAssembly();
                SetVerticesDirty();
                return;
            }
            if (assembly != view.Assembly)
            {
                ReleaseAssembly();
                assembly = view.Assembly;
                subscribedWeapon = assembly.GetComponentInParent<EquipableWeapon>();
                if (subscribedWeapon != null) subscribedWeapon.AssemblyChanged += MarkStructureDirty;
                structureDirty = true;
            }
            if (structureDirty || cachedAxis != view.Axis) RebuildMarkers();
            UpdateForwardLabel();
            SetVerticesDirty();
        }

        private void MarkStructureDirty() => structureDirty = true;

        private void ReleaseAssembly()
        {
            if (!ReferenceEquals(subscribedWeapon, null)) subscribedWeapon.AssemblyChanged -= MarkStructureDirty;
            subscribedWeapon = null;
            assembly = null;
            markers.Clear();
            projectedMuzzles.Clear();
        }

        private void RebuildMarkers()
        {
            markers.Clear();
            projectedMuzzles.Clear();
            foreach (var muzzle in assembly.GetProjectilePaths())
            {
                // 최종 발사 방향이 아니라 코어 +Forward 면에서 시작한 경로를 선택한다.
                // 분할·꺾임 후 다른 방향을 향해도 이 경로에서 파생된 총구면 포함한다.
                bool fromForward = muzzle.path.Length == 0 ? muzzle.direction == Vector3Int.forward
                    : muzzle.path[0].Cell == Vector3Int.forward;
                if (!fromForward) continue;
                // 직접 코어 출력의 발사 원점은 중심이지만, 표식만 코어의 해당 면에 붙인다.
                Vector3 origin = muzzle.path.Length == 0 ? (Vector3)muzzle.direction * .5f : muzzle.origin;
                Vector2Int position = new Vector2Int(
                    Mathf.RoundToInt(Vector3.Dot(origin, view.Horizontal) * 2f),
                    Mathf.RoundToInt(Vector3.Dot(origin, view.Vertical) * 2f));
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
            cachedAxis = view.Axis;
            structureDirty = false;
        }

        private void HideForwardLabel()
        {
            if (forwardLabel != null && forwardLabel.gameObject.activeSelf) forwardLabel.gameObject.SetActive(false);
        }

        private void UpdateForwardLabel()
        {
            if (forwardLabel == null || canvas == null || !IsOnScreen(assembly.transform.position))
            { HideForwardLabel(); return; }
            Transform root = assembly.transform;
            Vector2 core = Project(root.position);
            Vector2 cellSize = new Vector2(
                Vector2.Distance(core, Project(root.TransformPoint(view.Horizontal))),
                Vector2.Distance(core, Project(root.TransformPoint(view.Vertical))));
            Vector2 direction = new Vector2(Vector3.Dot(Vector3.forward, view.Horizontal),
                Vector3.Dot(Vector3.forward, view.Vertical));
            // 정면·후면 보기에서는 글자를 옆에 배치해 깊이 방향 화살표를 덮지 않는다.
            if (direction.sqrMagnitude < .5f) direction = Vector2.right;
            Vector2 gap = ScreenSizeInLocal(new Vector2(view.Camera.pixelHeight * .008f, view.Camera.pixelHeight * .008f));
            RectTransform labelRect = forwardLabel.rectTransform;
            labelRect.pivot = Mathf.Abs(direction.x) > .5f
                ? new Vector2(direction.x > 0f ? 0f : 1f, .5f)
                : new Vector2(.5f, direction.y > 0f ? 0f : 1f);
            Vector2 local = core + Vector2.Scale(direction, cellSize * .6f + gap);
            labelRect.localPosition = new Vector3(local.x, local.y, 0f);
            labelRect.sizeDelta = ScreenSizeInLocal(new Vector2(view.Camera.pixelWidth * .14f, view.Camera.pixelHeight * .03f));
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
            if (!visible || view == null || !view.Is2D || assembly == null || canvas == null || view.Camera == null) return;
            Transform root = assembly.transform;
            Vector2 core = Project(root.position);
            float cellWidth = Vector2.Distance(core, Project(root.TransformPoint(view.Horizontal)));
            float cellHeight = Vector2.Distance(core, Project(root.TransformPoint(view.Vertical)));
            if (cellWidth <= .001f || cellHeight <= .001f) return;

            // 흰 화살표는 코어 안, 붉은 화살표는 출력면 바깥에 두고 각각 폭 1셀 미만을 유지한다.
            if (IsOnScreen(root.position))
                DrawDirection(mesh, core, Vector3Int.forward, cellWidth, cellHeight, forwardColor, true, null);
            foreach (Marker marker in markers)
            {
                Vector3 world = root.TransformPoint(marker.origin);
                if (!IsOnScreen(world)) continue;
                DrawDirection(mesh, Project(world), marker.direction, cellWidth, cellHeight,
                    muzzleColor, false, marker.countLabel);
            }
        }

        private void DrawDirection(VertexHelper mesh, Vector2 origin, Vector3Int direction, float cellWidth,
            float cellHeight, Color tint, bool coreForward, string count)
        {
            Vector2 planar = new Vector2(Vector3.Dot(direction, view.Horizontal), Vector3.Dot(direction, view.Vertical));
            if (planar.sqrMagnitude > .5f)
            {
                Vector2 unit = planar.normalized;
                float length = (Mathf.Abs(unit.x) > .5f ? cellWidth : cellHeight) * (coreForward ? .46f : .85f);
                float width = (Mathf.Abs(unit.x) > .5f ? cellHeight : cellWidth) * (coreForward ? .32f : .4f);
                Arrow(mesh, origin, unit, length, width, tint);
                if (count != null) Count(mesh, count, origin, unit, cellWidth, cellHeight, tint);
                return;
            }

            // 화면 법선 방향은 위/아래 화살표에 꼬리의 점(관찰자 쪽)/X(화면 안쪽)를 더한다.
            // 두 깊이 방향은 좌우로 나누고 흰 전방은 중앙에 둬 같은 위치에서도 함께 읽힌다.
            bool towardViewer = Vector3.Dot(direction, view.Normal) > 0f;
            Vector2 depthUnit = towardViewer ? Vector2.up : Vector2.down;
            Vector2 center = origin + Vector2.right * (coreForward ? 0f : towardViewer ? -.29f : .29f) * cellWidth;
            Vector2 start = center - depthUnit * cellHeight * .23f;
            Arrow(mesh, start, depthUnit, cellHeight * .64f, cellWidth * .22f, tint);
            Vector2 tail = start - depthUnit * cellHeight * .09f;
            if (towardViewer)
                Quad(mesh, tail, new Vector2(cellWidth * .045f, cellHeight * .045f), tint);
            else
            {
                Vector2 half = new Vector2(cellWidth * .035f, cellHeight * .035f);
                Line(mesh, tail - half, tail + half, Mathf.Min(cellWidth, cellHeight) * .024f, tint);
                half.x = -half.x;
                Line(mesh, tail - half, tail + half, Mathf.Min(cellWidth, cellHeight) * .024f, tint);
            }
            if (count != null) Count(mesh, count, start, depthUnit, cellWidth * .55f, cellHeight, tint);
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

        private static void Quad(VertexHelper mesh, Vector2 center, Vector2 size, Color tint)
            => Line(mesh, center - Vector2.right * size.x * .5f, center + Vector2.right * size.x * .5f, size.y, tint);

        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
            => WorkshopPlanarGridGraphic.AddLine(mesh, a, b, width, tint);

        private Vector2 Project(Vector3 world)
        {
            Vector2 screen = view.Camera.WorldToScreenPoint(world);
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, uiCamera, out Vector2 local);
            return local;
        }

        private bool IsOnScreen(Vector3 world)
        {
            Vector3 screen = view.Camera.WorldToScreenPoint(world);
            return screen.z >= view.Camera.nearClipPlane && screen.z <= view.Camera.farClipPlane &&
                view.Camera.pixelRect.Contains(new Vector2(screen.x, screen.y));
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
