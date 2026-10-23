using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    public enum WorkshopViewAxis { Front, Up, Right, Back, Down, Left }

    // 카메라 자세는 작업대에 고정하고 ObjRotate로 총 전체의 표시 자세만 바꾼다.
    [DisallowMultipleComponent]
    public sealed class WorkshopPlanarView : MonoBehaviour
    {
        private const WorkshopViewAxis Initial2DAxis = WorkshopViewAxis.Right;
        [SerializeField, Min(.01f)] private float initialSize = 1f;
        [SerializeField, Min(.01f)] private float minimumSize = .2f;
        [SerializeField, Min(.01f)] private float maximumSize = 8f;
        [SerializeField, Min(0f)] private float zoomSpeed = .15f;
        private WorkshopSession session;
        private Camera view;
        private CubeBuilder builder;
        private CubeAssembly assembly;
        private Transform displayRoot;
        private Vector3 savedPosition, centeredCorePosition;
        private Quaternion savedRotation;
        private bool savedOrthographic;
        private float savedSize, savedFieldOfView;
        private Vector3 panOffset;
        private bool panDragging;
        private Vector2 previousPointer;
        public bool Is2D { get; private set; }
        public bool IsPanning => Is2D && panDragging;
        public WorkshopViewAxis Axis { get; private set; } = Initial2DAxis;
        public CubeAssembly Assembly => assembly;
        public Camera Camera => view;
        public int SelectedDepth { get; private set; }
        public event System.Action LayerChanged;
        public int DepthOf(Vector3Int cell) => CubePlacementState.Dot(cell, Normal);
        public Vector3 LayerOrigin => assembly != null
            ? assembly.transform.TransformPoint((Vector3)Normal * SelectedDepth) : Vector3.zero;
        public void SetSelectedDepth(int depth)
        {
            if (!Is2D || !CanChangeView || SelectedDepth == depth) return;
            builder?.CancelPlacementPreview();
            SelectedDepth = depth;
            LayerChanged?.Invoke();
        }
        public Vector3Int Normal => NormalFor(Axis);
        public static Vector3Int NormalFor(WorkshopViewAxis axis) => axis switch
        {
            WorkshopViewAxis.Front => Vector3Int.forward,
            WorkshopViewAxis.Back => Vector3Int.back,
            WorkshopViewAxis.Up => Vector3Int.up,
            WorkshopViewAxis.Down => Vector3Int.down,
            WorkshopViewAxis.Left => Vector3Int.left,
            _ => Vector3Int.right
        };
        public Vector3Int Vertical => VerticalFor(Axis);
        public static Vector3Int VerticalFor(WorkshopViewAxis axis) => axis == WorkshopViewAxis.Up || axis == WorkshopViewAxis.Down
            ? Vector3Int.forward : Vector3Int.up;
        public static Quaternion CoreRotationInView(WorkshopViewAxis axis) =>
            Quaternion.Inverse(Quaternion.LookRotation(-(Vector3)NormalFor(axis), VerticalFor(axis)));
        public Vector3Int Horizontal => Vector3Int.RoundToInt(Vector3.Cross(Vertical, -(Vector3)Normal));
        public bool CanChangeView => session != null && session.IsEditing && !GameManager.GameplayBlocked &&
            builder != null && !builder.IsCustomMode && !SlotDragActive;
        private bool SlotDragActive => builder != null && builder.IsFavoriteDragging;

        public void Initialize(WorkshopSession owner, Camera camera, CubeBuilder cubeBuilder)
        {
            session = owner;
            view = camera;
            builder = cubeBuilder;
            Is2D = panDragging = false;
            Axis = Initial2DAxis;
            SelectedDepth = 0;
            if (view != null) view.orthographic = false;
        }

        public void ToggleMode()
        {
            if (!CanChangeView || !TutorialManager.TryAction("view-mode")) return;
            if (Is2D) { Restore3D(); return; }
            var core = session.ActiveWorkbench.CurrentCore;
            assembly = core != null ? core.GetComponent<CubeAssembly>() : null;
            if (assembly == null) return;
            displayRoot = session.ActiveWorkbench.RotationTarget;
            if (displayRoot == null) return;
            savedPosition = displayRoot.localPosition;
            savedRotation = displayRoot.localRotation;
            centeredCorePosition = displayRoot.parent != null
                ? displayRoot.parent.InverseTransformPoint(assembly.transform.position) : assembly.transform.position;
            savedOrthographic = view.orthographic;
            savedSize = view.orthographicSize;
            savedFieldOfView = view.fieldOfView;
            Axis = Initial2DAxis;
            SelectedDepth = 0;
            panOffset = Vector3.zero;
            panDragging = false;
            builder.CancelPlacementPreview();
            Is2D = true;
            view.orthographic = true;
            view.orthographicSize = initialSize;
            ApplyDisplayPose();
        }

        public void SelectAxis(WorkshopViewAxis axis)
        {
            if (!CanChangeView || !TutorialManager.TryAction("view-mode")) return;
            if (!Is2D && !TutorialManager.TryAction("rotate")) return;
            var core = session.ActiveWorkbench.CurrentCore;
            var target = session.ActiveWorkbench.RotationTarget;
            if (core == null || target == null) return;
            session.CancelRotationDrag();
            builder.CancelPlacementPreview();
            if (Is2D)
            {
                Axis = axis;
                SelectedDepth = 0;
                LayerChanged?.Invoke();
                panOffset = Vector3.zero;
                panDragging = false;
                ApplyDisplayPose();
            }
            else AlignCore(target, core.transform, view.transform.rotation, axis, core.transform.position);
        }

        // 3D/2Dとも同じ姿勢計算を使う。回転の中心はコアで、モジュールのローカル座標は触らない。
        private static void AlignCore(Transform target, Transform core, Quaternion cameraRotation,
            WorkshopViewAxis axis, Vector3 center)
        {
            Quaternion desired = cameraRotation * CoreRotationInView(axis);
            target.rotation = desired * Quaternion.Inverse(core.rotation) * target.rotation;
            target.position += center - core.position;
        }

        public void Restore3D()
        {
            if (Is2D && view != null)
            {
                view.orthographic = savedOrthographic;
                view.orthographicSize = savedSize;
                view.fieldOfView = savedFieldOfView;

                builder?.CancelPlacementPreview();
            }
            if (Is2D && displayRoot != null)
            {
                displayRoot.localPosition = savedPosition;
                displayRoot.localRotation = savedRotation;
            }
            displayRoot = null;
            Is2D = panDragging = false;
            panOffset = Vector3.zero;
            assembly = null;
            Axis = Initial2DAxis;
            SelectedDepth = 0;
        }

        public void StopPan() => panDragging = false;

        public void ResetFraming()
        {
            if (!Is2D) return;
            panOffset = Vector3.zero;
            panDragging = false;
            view.orthographicSize = initialSize;
            builder?.CancelPlacementPreview();
            ApplyDisplayPose();
        }

        public void UpdateInput(InputAction pan, InputAction pointer, InputAction zoom, InputAction layerModifier)
        {
            if (!Is2D) return;
            if (!CanChangeView || !Application.isFocused) { StopPan(); return; }
            Vector2 position = pointer.ReadValue<Vector2>();
            bool overUI = builder.IsPointerOverInterface(position);
            if (pan.WasPressedThisFrame() && !overUI && view.pixelRect.Contains(position) &&
                TutorialManager.TryAction("pan"))
            {
                panDragging = true;
                previousPointer = position;
                builder.CancelPlacementPreview();
            }
            if (!pan.IsPressed()) panDragging = false;
            if (panDragging && assembly != null)
            {
                Vector2 delta = position - previousPointer;
                float unitsPerPixel = 2f * view.orthographicSize / Mathf.Max(1f, view.pixelHeight);
                panOffset += (view.transform.right * delta.x + view.transform.up * delta.y) * unitsPerPixel;
                previousPointer = position;
            }
            Vector2 scroll = zoom.ReadValue<Vector2>();
            // Ctrl은 입력 에셋의 LayerModifier 액션으로만 읽는다. 같은 휠 입력을 줌에 중복 사용하지 않는다.
            if (layerModifier.IsPressed())
            {
                if (!panDragging && !overUI && view.pixelRect.Contains(position) && Mathf.Abs(scroll.y) > .01f &&
                    TutorialManager.TryAction("view-mode"))
                {
                    int step = scroll.y > 0f ? 1 : -1;
                    if ((step > 0 && SelectedDepth < int.MaxValue) || (step < 0 && SelectedDepth > int.MinValue))
                        SetSelectedDepth(SelectedDepth + step);
                }
                return;
            }
            if (!panDragging && !overUI && view.pixelRect.Contains(position) && Mathf.Abs(scroll.y) > .01f &&
                TutorialManager.TryAction("zoom"))
                view.orthographicSize = Mathf.Clamp(view.orthographicSize * Mathf.Exp(-Mathf.Clamp(scroll.y, -1f, 1f) * zoomSpeed),
                    minimumSize, Mathf.Max(minimumSize, maximumSize));
        }

        public void ApplyDisplayPose()
        {
            if (!Is2D || session == null || session.ActiveWorkbench == null || view == null) return;
            var core = session.ActiveWorkbench.CurrentCore;
            var current = core != null ? core.GetComponent<CubeAssembly>() : null;
            if (current != assembly)
            {
                builder?.CancelPlacementPreview();
                assembly = current;
                SelectedDepth = 0;
                LayerChanged?.Invoke();
                panOffset = Vector3.zero;
            }
            if (assembly == null) { Restore3D(); return; }
            if (displayRoot == null) { Restore3D(); return; }
            Vector3 center = displayRoot.parent != null
                ? displayRoot.parent.TransformPoint(centeredCorePosition) : centeredCorePosition;
            AlignCore(displayRoot, assembly.transform, view.transform.rotation, Axis, center + panOffset);
        }

        // 포인터를 선택한 층의 평면과 교차시킨다. 설치·호버·삭제가 이 좌표를 공유한다.
        public bool TryGetCell(Vector2 pointer, out Vector3Int cell)
        {
            cell = default;
            if (!Is2D || assembly == null || view == null || !view.pixelRect.Contains(pointer)) return false;
            var plane = new Plane(assembly.transform.TransformDirection(Normal), LayerOrigin);
            Ray ray = view.ScreenPointToRay(pointer);
            if (!plane.Raycast(ray, out float distanceToPlane)) return false;
            Vector3 local = assembly.transform.InverseTransformPoint(ray.GetPoint(distanceToPlane));
            cell = Vector3Int.RoundToInt(local);
            if (Normal.x != 0) cell.x = Normal.x * SelectedDepth;
            if (Normal.y != 0) cell.y = Normal.y * SelectedDepth;
            if (Normal.z != 0) cell.z = Normal.z * SelectedDepth;
            return true;
        }

        public bool ContainsCell(Vector3Int cell) => DepthOf(cell) == SelectedDepth;
        public bool IsDirectionInViewPlane(Vector3Int direction) => CubePlacementState.Dot(direction, Normal) == 0;

        private void OnDisable() => Restore3D();
    }
}
