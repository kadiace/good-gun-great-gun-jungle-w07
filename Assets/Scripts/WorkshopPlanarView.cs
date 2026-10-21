using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent, RequireComponent(typeof(WorkshopSession)), DefaultExecutionOrder(35)]
    public sealed class WorkshopPlanarView : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Shader overlayShader;
        [SerializeField] private RectTransform controlsRoot;
        [SerializeField, Tooltip("이 플레이어의 제작을 코어 로컬 X=0 평면에 고정합니다.")]
        private bool fixedXPlane;
        public bool FixedXPlane => fixedXPlane;
        [SerializeField, Min(.01f)] private float transitionDuration = .35f;
        [SerializeField, Range(.05f, .5f), Tooltip("작을수록 총기의 원근 왜곡이 줄어듭니다. 카메라 투영은 변경하지 않습니다.")]
        private float remainingPerspective = .15f;
        private WorkshopSession session;
        private CubeBuilder builder;
        private WorkshopPlanarRenderer display;
        private WorkshopPlanarViewUI ui;
        private InputAction point, zoom, attach, cancelDirection;
        private EquipableWeapon weapon;
        private CubeAssembly assembly;
        private bool suspended, sourceDirty, returningTo3D, ignoreAttachUntilReleased;
        private string openFailure = string.Empty;
        private WorkshopGridSelection hovered;
        private PlacedBlock hoveredRemoval;
        private readonly List<PlacedBlock> indicatorBlocks = new List<PlacedBlock>();
        private readonly HashSet<Vector3Int> indicatorCells = new HashSet<Vector3Int>();
        private Vector3Int forwardIndicatorCell;
        private bool directionPending;
        private WorkshopGridSelection pendingPlacement;
        private static readonly Vector3Int[] OutputDirections = { Vector3Int.right, Vector3Int.left,
            Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };
        public bool IsPlanar { get; private set; }
        public bool IsViewTransitioning => IsPlanar && display != null && display.IsTransitioning;
        public WorkshopPlanarAxis Axis { get; private set; } = WorkshopPlanarAxis.Z;
        public int Depth { get; private set; }
        public bool HasHoveredCell { get; private set; }
        public WorkshopGridSelection HoveredSelection => hovered;

        private void Awake()
        {
            IsPlanar = false; HasHoveredCell = false;
            Axis = fixedXPlane ? WorkshopPlanarAxis.X : WorkshopPlanarAxis.Z; Depth = 0;
            session = GetComponent<WorkshopSession>();
            builder = session.WorkshopCamera != null ? session.WorkshopCamera.GetComponent<CubeBuilder>() : null;
            if (builder == null || inputActions == null || overlayShader == null || controlsRoot == null)
            { enabled = false; return; }
            point = inputActions.FindAction("Build/Point", true).Clone();
            zoom = inputActions.FindAction("Workshop/Zoom", true).Clone();
            attach = inputActions.FindAction("Build/Attach", true).Clone();
            cancelDirection = inputActions.FindAction("Build/Rotate", true).Clone();
            ui = new WorkshopPlanarViewUI(controlsRoot, () => SetPlanar(!IsPlanar), SetAxis, offset => SetDepth(Depth + offset), fixedXPlane);
            ui.Show(false, false, false, Axis, Depth, string.Empty);
            session.WorkbenchExited += OnWorkbenchExited;
        }
        private void OnEnable() { point?.Enable(); zoom?.Enable(); attach?.Enable(); cancelDirection?.Enable(); }
        private void OnDisable()
        {
            ExitPlanar(); point?.Disable(); zoom?.Disable(); attach?.Disable(); cancelDirection?.Disable();
            ui?.Show(false, false, false, Axis, Depth, string.Empty);
        }
        private void OnDestroy()
        {
            if (session != null) session.WorkbenchExited -= OnWorkbenchExited;
            ExitPlanar(); attach?.Dispose(); point?.Dispose(); zoom?.Dispose(); cancelDirection?.Dispose();
            display?.Dispose(); display = null;
            ui?.Dispose(); ui = null;
        }
        private void OnWorkbenchExited(EquipableWeapon _) => ExitPlanar();

        internal void BeginEditingPresentation()
        {
            if (!fixedXPlane || !isActiveAndEnabled || ui == null) return;
            SetPlanar(true);
            ui.Show(!GameManager.GameplayBlocked && Application.isFocused, IsPlanar, CanOpen, Axis, Depth, openFailure);
        }

        // Lifecycle cleanup is immediate. The toolbar's 3D button uses SetPlanar for a smooth return.
        public void ExitPlanar()
        {
            CancelDirectionSelection();
            ui?.HideDirections();
            if (weapon != null) weapon.AssemblyChanged -= OnAssemblyChanged;
            display?.Bind(null);
            weapon = null; assembly = null; suspended = sourceDirty = returningTo3D = false;
            forwardIndicatorCell = Vector3Int.zero; indicatorBlocks.Clear(); indicatorCells.Clear();
            IsPlanar = false; ClearSelection(); ignoreAttachUntilReleased = true;
            if (session != null) session.RefreshWorkshopPresentation();
        }
        public void SetPlanar(bool planar)
        {
            if (fixedXPlane && !planar) return;
            if (IsViewTransitioning || IsPlanar == planar || GameManager.GameplayBlocked) return;
            if (planar)
            {
                if (!CanOpen) return;
                var sourceWeapon = session.ActiveWorkbench.CurrentWeapon;
                var source = sourceWeapon.GetComponentInChildren<CubeAssembly>(true);
                if (source == null) return;
                if (!WorkshopPlanarRenderer.Supports(source))
                { openFailure = "현재 총기에 지원하지 않는 표시 형식이 포함되어 있습니다."; return; }
                weapon = sourceWeapon; assembly = source;
                builder.ClearWorldPreview();
                display ??= new WorkshopPlanarRenderer(session.WorkshopCamera, overlayShader);
                display.Bind(assembly);
                RefreshForwardIndicatorCell();
                weapon.AssemblyChanged += OnAssemblyChanged;
                Axis = fixedXPlane ? WorkshopPlanarAxis.X : WorkshopPlanarAxis.Z;
                Depth = 0; display.SetDepth(Depth);
                IsPlanar = true; returningTo3D = false; openFailure = string.Empty;
            }
            else returningTo3D = true;
            CancelDirectionSelection();
            ClearSelection(); ignoreAttachUntilReleased = true;
            display.Transition(planar, Axis, transitionDuration, remainingPerspective);
            // Fixed-plane editing starts aligned, with no intermediate editable 3D view.
            if (fixedXPlane) display.Advance(transitionDuration);
            session.RefreshWorkshopPresentation();
        }
        private bool CanOpen => isActiveAndEnabled && session != null && session.IsEditing &&
            session.ActiveWorkbench != null && session.ActiveWorkbench.CurrentWeapon != null &&
            !session.ActiveWorkbench.CurrentWeapon.IsMoving && builder != null &&
            (fixedXPlane || (!builder.IsCustomMode && !builder.ColorPickerOpen && !builder.PaletteOpen));
        private bool CanSelect => IsPlanar && !IsViewTransitioning && !returningTo3D && !suspended &&
            session.IsEditing && assembly != null && !GameManager.GameplayBlocked && Application.isFocused;

        public void SetAxis(WorkshopPlanarAxis axis)
        {
            if (fixedXPlane || !CanSelect || Axis == axis) return;
            CancelDirectionSelection();
            Axis = axis; Depth = 0; ClearSelection(); ignoreAttachUntilReleased = true;
            display.SetDepth(Depth); display.Transition(true, Axis, transitionDuration, remainingPerspective);
        }
        public void SetDepth(int depth)
        {
            if (fixedXPlane || !CanSelect) return;
            CancelDirectionSelection();
            Depth = Mathf.Clamp(depth, -128, 128); ClearSelection(); ignoreAttachUntilReleased = true;
            display.SetDepth(Depth);
        }
        public bool TryGetGridSelection(Vector2 screenPoint, out WorkshopGridSelection selection)
        {
            selection = default;
            if (!CanSelect || builder.DeleteMode || !ui.CanPoint(screenPoint, session.WorkshopCamera) ||
                !display.TryGetPlaneCell(screenPoint, Axis, Depth, out var cell, out bool occluded) ||
                !WorkshopGridPicker.TryEvaluate(assembly, cell, builder, Axis, out selection)) return false;
            if (occluded && selection.IsValid) selection = selection.WithFailure("앞쪽 모듈에 가려진 깊이에는 설치할 수 없습니다.");
            return true;
        }
        // Future continuous placement freezes Axis/Depth from a valid GridSelection.
        // Plane intersection alone never grants attachment permission.
        public bool TryGetPlaneCell(Vector2 screenPoint, WorkshopPlanarAxis axis, int depth, out Vector3Int cell)
        {
            cell = default;
            if (fixedXPlane && (axis != WorkshopPlanarAxis.X || depth != 0)) return false;
            return CanSelect && ui.CanPoint(screenPoint, session.WorkshopCamera) &&
                display.TryGetPlaneCell(screenPoint, axis, depth, out cell, out bool occluded) && !occluded;
        }
        private void OnAssemblyChanged() { sourceDirty = true; CancelDirectionSelection(); ClearSelection(); }
        private void ClearSelection() { HasHoveredCell = false; hovered = default; hoveredRemoval = null; }

        private void CancelDirectionSelection()
        {
            if (!directionPending) return;
            directionPending = false; pendingPlacement = default;
            ignoreAttachUntilReleased = true;
        }

        private Vector3Int PickOutputDirection(Vector2 pointer)
        {
            var view = session.WorkshopCamera;
            var matrix = display.AssemblyToDisplay;
            Vector3 center = view.WorldToScreenPoint(matrix.MultiplyPoint3x4(pendingPlacement.Cell));
            Vector2 offset = pointer - (Vector2)center;
            float best = float.NegativeInfinity, shortest = float.PositiveInfinity;
            Vector3Int chosen = Vector3Int.zero;
            foreach (var direction in OutputDirections)
            {
                if (Vector3.Dot(WorkshopPlanarCoordinates.Normal(pendingPlacement.Axis), direction) != 0f) continue;
                Vector3 end = view.WorldToScreenPoint(matrix.MultiplyPoint3x4((Vector3)pendingPlacement.Cell + (Vector3)direction));
                Vector2 projected = (Vector2)(end - center);
                if (end.z <= view.nearClipPlane || projected.sqrMagnitude < .01f) continue;
                shortest = Mathf.Min(shortest, projected.magnitude);
                float score = Vector2.Dot(offset.normalized, projected.normalized);
                if (score <= best) continue;
                best = score; chosen = direction;
            }
            float deadZone = Mathf.Max(12f, shortest * .45f);
            return offset.sqrMagnitude <= deadZone * deadZone ? Vector3Int.zero : chosen;
        }

        private string UpdateDirectionSelection(Vector2 pointer)
        {
            HasHoveredCell = true;
            bool canPoint = ui.CanPoint(pointer, session.WorkshopCamera);
            Vector3Int output = canPoint ? PickOutputDirection(pointer) : Vector3Int.zero;
            hovered = pendingPlacement.WaitingForDirection();
            if (output != Vector3Int.zero)
            {
                WorkshopGridPicker.TryEvaluateDirection(assembly, pendingPlacement.Cell, builder, pendingPlacement.Axis, output, out hovered);
                if (hovered.InputDirection != pendingPlacement.InputDirection)
                    hovered = hovered.WithFailure("부착 조건이 변경되었습니다. 우클릭으로 취소해 주세요.");
            }
            string message = hovered.IsValid
                ? (hovered.ModuleId == 3
                    ? $"분할 출력: {DirectionLabel(output)} ↔ {DirectionLabel(-output)}"
                    : $"출력: {DirectionLabel(output)}") + " · 좌클릭: 확정 · 우클릭: 취소"
                : hovered.FailureReason + " · 우클릭: 취소";
            if (canPoint && !ignoreAttachUntilReleased && attach.WasPressedThisFrame() && hovered.IsValid)
            {
                if (builder.TryPlaceGrid(hovered) || GameManager.GameplayBlocked) CancelDirectionSelection();
            }
            return message;
        }

        private static string DirectionLabel(Vector3Int direction) => direction == Vector3Int.up ? "위 · UP"
            : direction == Vector3Int.down ? "아래" : direction == Vector3Int.forward ? "앞 · FRONT"
            : direction == Vector3Int.back ? "뒤" : direction == Vector3Int.right ? "오른쪽" : "왼쪽";

        private void RefreshForwardIndicatorCell()
        {
            forwardIndicatorCell = Vector3Int.zero;
            indicatorBlocks.Clear(); indicatorCells.Clear();
            if (assembly == null) return;
            assembly.GetComponentsInChildren(false, indicatorBlocks);
            foreach (var block in indicatorBlocks)
                if (block != null && block.transform.parent == assembly.transform) indicatorCells.Add(block.Cell);
            // Follow only the uninterrupted +Z run from the core, not a separate side branch.
            while (indicatorCells.Contains(forwardIndicatorCell + Vector3Int.forward))
                forwardIndicatorCell += Vector3Int.forward;
        }

        private void RefreshDisplaySource()
        {
            display.RefreshSource(); RefreshForwardIndicatorCell(); sourceDirty = false;
        }

        // Indicator positions must follow exactly the same display transform as the gun.
        public bool TryGetDisplayTransform(CubeAssembly source, out Matrix4x4 matrix)
        {
            matrix = Matrix4x4.identity;
            if (!IsPlanar || assembly == null || source != assembly) return false;
            display.PrepareView();
            matrix = display.AssemblyToDisplay * assembly.transform.worldToLocalMatrix;
            return true;
        }

        private void LateUpdate()
        {
            if (ui == null) return;
            if (IsPlanar && (!session.IsEditing || session.ActiveWorkbench == null ||
                session.ActiveWorkbench.CurrentWeapon != weapon || assembly == null)) ExitPlanar();
            if (fixedXPlane && !IsPlanar && session.IsEditing && Application.isFocused && !GameManager.GameplayBlocked)
                SetPlanar(true);
            bool visible = session.IsEditing && !GameManager.GameplayBlocked && Application.isFocused;
            if (directionPending && (!visible || !CanSelect || builder.DeleteMode || builder.PaletteOpen || builder.ColorPickerOpen ||
                builder.IsCustomMode || builder.SelectedSlot != pendingPlacement.ModuleId || pendingPlacement.Assembly != assembly ||
                pendingPlacement.Axis != Axis || pendingPlacement.Depth != Depth)) CancelDirectionSelection();
            if (IsPlanar && !visible && !suspended)
            { suspended = true; ignoreAttachUntilReleased = true; }
            else if (IsPlanar && visible && suspended)
            { suspended = false; }
            ClearSelection();
            if (!IsPlanar || !visible)
            {
                ui.Show(visible, IsPlanar, CanOpen, Axis, Depth, IsPlanar ? string.Empty : openFailure);
                if (IsPlanar)
                {
                    if (sourceDirty) RefreshDisplaySource();
                    display.Draw(false, default, false);
                }
                return;
            }
            if (sourceDirty) RefreshDisplaySource();
            display.Advance(Time.unscaledDeltaTime);
            if (returningTo3D && !display.IsTransitioning)
            {
                ExitPlanar(); ui.Show(visible, false, CanOpen, Axis, Depth, string.Empty); return;
            }
            if (!attach.IsPressed()) ignoreAttachUntilReleased = false;
            if (directionPending && cancelDirection.WasPressedThisFrame()) CancelDirectionSelection();
            // UI hit filtering and edit decisions run after the EventSystem has updated.
            ui.Show(visible, true, CanOpen, Axis, Depth, string.Empty, IsViewTransitioning);
            Vector2 pointer = point.ReadValue<Vector2>();
            if (CanSelect && ui.CanPoint(pointer, session.WorkshopCamera) && Mathf.Abs(zoom.ReadValue<Vector2>().y) > .001f &&
                TutorialManager.TryAction("zoom")) display.Zoom(zoom.ReadValue<Vector2>().y);
            string message = IsViewTransitioning ? "뷰 전환 중…" : "연장·2방향 분할: 위치 선택 → 방향 선택 · E: 삭제 모드 · 휠: 확대 · Ctrl+Z: 되돌리기";
            if (directionPending) message = UpdateDirectionSelection(pointer);
            else if (builder.DeleteMode)
            {
                if (CanSelect && !builder.PaletteOpen && !builder.ColorPickerOpen && ui.CanPoint(pointer, session.WorkshopCamera))
                    display.TryGetBlock(pointer, out hoveredRemoval);
                if (!IsViewTransitioning) message = hoveredRemoval != null
                    ? $"삭제 대상: {hoveredRemoval.Cell} · 좌클릭: 삭제 · E: 설치 모드 · Ctrl+Z: 되돌리기"
                    : "삭제할 블록을 가리키세요 · 좌클릭: 삭제 · E: 설치 모드";
                if (!ignoreAttachUntilReleased && attach.WasPressedThisFrame() && hoveredRemoval != null)
                    builder.TryRemovePlanarBlock(assembly, hoveredRemoval);
            }
            else
            {
                HasHoveredCell = TryGetGridSelection(pointer, out hovered);
                if (HasHoveredCell) message = hovered.IsValid
                    ? WorkshopGridPicker.RequiresDirectionSelection(hovered.ModuleId)
                        ? $"{(hovered.ModuleId == 3 ? "2방향 분할" : "연장")} 위치: {hovered.Cell} · 클릭 후 출력 방향 선택"
                        : fixedXPlane ? $"설치 가능: {hovered.Cell} · E: 삭제 모드"
                        : $"설치 가능: {hovered.Cell} · {Axis} 깊이 {Depth} · E: 삭제 모드"
                    : hovered.FailureReason;
                if (!ignoreAttachUntilReleased && !cancelDirection.IsPressed() && attach.WasPressedThisFrame() && HasHoveredCell && hovered.IsValid)
                {
                    if (WorkshopGridPicker.RequiresDirectionSelection(hovered.ModuleId))
                    {
                        directionPending = true; pendingPlacement = hovered;
                        hovered = hovered.WaitingForDirection(); ignoreAttachUntilReleased = true;
                        message = "위치 선택 완료 · 마우스로 방향 선택 · 좌클릭: 확정 · 우클릭: 취소";
                    }
                    else builder.TryPlaceGrid(hovered);
                }
            }
            if (sourceDirty) { RefreshDisplaySource(); ClearSelection(); }
            ui.Show(visible, true, CanOpen, Axis, Depth, message, IsViewTransitioning);
            display.Draw(HasHoveredCell, hovered, true, hoveredRemoval, directionPending);
            if (!IsViewTransitioning && !builder.PaletteOpen && !builder.ColorPickerOpen)
                ui.UpdateDirections(session.WorkshopCamera, display.AssemblyToDisplay, forwardIndicatorCell);
            else ui.HideDirections();
            if (fixedXPlane) session.RefreshWorkshopIndicators();
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) { CancelDirectionSelection(); ClearSelection(); ignoreAttachUntilReleased = true; }
        }
    }
}
