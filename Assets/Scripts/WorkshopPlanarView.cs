using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    public enum WorkshopViewAxis { Front, Up, Right }

    // 카메라 자세는 작업대에 고정하고 ObjRotate로 총 전체의 표시 자세만 바꾼다.
    [DisallowMultipleComponent]
    public sealed class WorkshopPlanarView : MonoBehaviour
    {
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
        public WorkshopViewAxis Axis { get; private set; }
        public CubeAssembly Assembly => assembly;
        public Camera Camera => view;
        public Vector3Int Normal => Axis == WorkshopViewAxis.Front ? Vector3Int.forward :
            Axis == WorkshopViewAxis.Up ? Vector3Int.up : Vector3Int.right;
        public Vector3Int Horizontal => Axis == WorkshopViewAxis.Front ? Vector3Int.left :
            Axis == WorkshopViewAxis.Right ? Vector3Int.forward : Vector3Int.right;
        public Vector3Int Vertical => Axis == WorkshopViewAxis.Up ? Vector3Int.forward : Vector3Int.up;
        public bool CanChangeView => session != null && session.IsEditing && !GameManager.GameplayBlocked &&
            builder != null && !builder.IsCustomMode;

        public void Initialize(WorkshopSession owner, Camera camera, CubeBuilder cubeBuilder)
        {
            session = owner;
            view = camera;
            builder = cubeBuilder;
            Is2D = panDragging = false;
            Axis = WorkshopViewAxis.Front;
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
            Axis = WorkshopViewAxis.Front;
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
            if (!CanChangeView || !Is2D || Axis == axis || !TutorialManager.TryAction("view-mode")) return;
            builder.CancelPlacementPreview();
            Axis = axis;
            panOffset = Vector3.zero;
            panDragging = false;
            ApplyDisplayPose();
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
            Axis = WorkshopViewAxis.Front;
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

        public void UpdateInput(InputAction pan, InputAction pointer, InputAction zoom)
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
                panOffset = Vector3.zero;
            }
            if (assembly == null) { Restore3D(); return; }
            if (displayRoot == null) { Restore3D(); return; }
            Transform coreTransform = assembly.transform;
            Quaternion localView = Quaternion.LookRotation(-((Vector3)Normal), Vertical);
            Quaternion desiredCoreRotation = view.transform.rotation * Quaternion.Inverse(localView);
            displayRoot.rotation = desiredCoreRotation * Quaternion.Inverse(coreTransform.rotation) * displayRoot.rotation;
            Vector3 center = displayRoot.parent != null
                ? displayRoot.parent.TransformPoint(centeredCorePosition) : centeredCorePosition;
            displayRoot.position += center + panOffset - coreTransform.position;
        }

        // 깊이는 항상 코어 좌표 0. 다른 깊이의 모듈은 이 선택에 끼어들지 않는다.
        public bool TryGetCell(Vector2 pointer, out Vector3Int cell)
        {
            cell = default;
            if (!Is2D || assembly == null || view == null || !view.pixelRect.Contains(pointer)) return false;
            var plane = new Plane(assembly.transform.TransformDirection(Normal), assembly.transform.position);
            Ray ray = view.ScreenPointToRay(pointer);
            if (!plane.Raycast(ray, out float distanceToPlane)) return false;
            Vector3 local = assembly.transform.InverseTransformPoint(ray.GetPoint(distanceToPlane));
            cell = Vector3Int.RoundToInt(local);
            if (Normal.x != 0) cell.x = 0;
            if (Normal.y != 0) cell.y = 0;
            if (Normal.z != 0) cell.z = 0;
            return true;
        }

        public bool ContainsCell(Vector3Int cell) => cell.x * Normal.x + cell.y * Normal.y + cell.z * Normal.z == 0;

        private void OnDisable() => Restore3D();
    }
}
