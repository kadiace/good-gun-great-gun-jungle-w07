using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent, RequireComponent(typeof(WorkshopSession)), DefaultExecutionOrder(35)]
    public sealed class WorkshopPlanarView : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Shader overlayShader;
        private WorkshopSession session;
        private CubeBuilder builder;
        private WorkshopPlanarRenderer display;
        private WorkshopPlanarViewUI ui;
        private InputAction point, zoom, attach;
        private EquipableWeapon weapon;
        private CubeAssembly assembly;
        private bool suspended;
        private bool sourceDirty;
        private WorkshopGridSelection hovered;
        public bool IsPlanar { get; private set; }
        public WorkshopPlanarAxis Axis { get; private set; } = WorkshopPlanarAxis.Z;
        public int Depth { get; private set; }
        public bool HasHoveredCell { get; private set; }
        public WorkshopGridSelection HoveredSelection => hovered;

        private void Awake()
        {
            IsPlanar = false; HasHoveredCell = false;
            session = GetComponent<WorkshopSession>();
            builder = session.WorkshopCamera != null ? session.WorkshopCamera.GetComponent<CubeBuilder>() : null;
            if (builder == null || inputActions == null || overlayShader == null) { enabled = false; return; }
            point = inputActions.FindAction("Build/Point", true).Clone();
            zoom = inputActions.FindAction("Workshop/Zoom", true).Clone();
            attach = inputActions.FindAction("Build/Attach", true).Clone();
            attach.performed += OnAttach;
            ui = new WorkshopPlanarViewUI(transform, () => SetPlanar(!IsPlanar), SetAxis, offset => SetDepth(Depth + offset));
            session.WorkbenchExited += OnWorkbenchExited;
        }

        private void OnEnable() { point?.Enable(); zoom?.Enable(); attach?.Enable(); }
        private void OnDisable()
        {
            ExitPlanar(); point?.Disable(); zoom?.Disable(); attach?.Disable();
            ui?.Show(false, false, false, Axis, Depth, string.Empty);
        }
        private void OnDestroy()
        {
            if (session != null) session.WorkbenchExited -= OnWorkbenchExited;
            if (attach != null) { attach.performed -= OnAttach; attach.Dispose(); }
            point?.Dispose(); zoom?.Dispose(); display?.Dispose(); ui?.Dispose();
        }
        private void OnWorkbenchExited(EquipableWeapon _) => ExitPlanar();

        public void ExitPlanar() => SetPlanar(false);

        public void SetPlanar(bool planar)
        {
            if (IsPlanar == planar) return;
            if (planar)
            {
                if (!CanOpen || GameManager.GameplayBlocked) return;
                weapon = session.ActiveWorkbench.CurrentWeapon;
                assembly = weapon.GetComponentInChildren<CubeAssembly>(true);
                if (assembly == null) return;
                builder.ClearWorldPreview();
                display ??= new WorkshopPlanarRenderer(transform, overlayShader);
                display.Bind(assembly);
                weapon.AssemblyChanged += OnAssemblyChanged;
                Axis = WorkshopPlanarAxis.Z; Depth = 0; display.SetView(Axis, Depth);
            }
            else
            {
                if (weapon != null) weapon.AssemblyChanged -= OnAssemblyChanged;
                display?.Bind(null);
                weapon = null; assembly = null; suspended = false;
            }
            IsPlanar = planar; HasHoveredCell = false; hovered = default; sourceDirty = false;
            session.RefreshWorkshopPresentation();
            if (!planar) ui?.Show(session.IsEditing && !GameManager.GameplayBlocked, false, CanOpen, Axis, Depth, string.Empty);
        }

        private bool CanOpen => isActiveAndEnabled && session != null && session.IsEditing &&
            session.ActiveWorkbench != null && session.ActiveWorkbench.CurrentWeapon != null &&
            !session.ActiveWorkbench.CurrentWeapon.IsMoving && builder != null &&
            !builder.IsCustomMode && !builder.DeleteMode && !builder.ColorPickerOpen;

        public void SetAxis(WorkshopPlanarAxis axis)
        {
            if (!IsPlanar || GameManager.GameplayBlocked) return;
            Axis = axis; Depth = 0; HasHoveredCell = false; hovered = default;
            display.SetView(Axis, Depth);
        }

        public void SetDepth(int depth)
        {
            if (!IsPlanar || GameManager.GameplayBlocked) return;
            Depth = Mathf.Clamp(depth, -128, 128); HasHoveredCell = false; hovered = default;
            display.SetView(Axis, Depth);
        }

        // Screen -> active depth plane -> full 3D cell -> existing adjacency and energy rules.
        public bool TryGetGridSelection(Vector2 screenPoint, out WorkshopGridSelection selection)
        {
            selection = default;
            if (!IsPlanar || !session.IsEditing || assembly == null || GameManager.GameplayBlocked || !Application.isFocused ||
                display.Texture == null || !ui.TryGetViewportPoint(screenPoint, out var uv)) return false;
            display.PrepareView();
            return display.TryGetPlaneCell(uv, Axis, Depth, out var cell) &&
                WorkshopGridPicker.TryEvaluate(assembly, cell, builder, Axis, out selection);
        }

        // Stage two must lock a valid adjacent grid selection before extending a drag.
        public bool TryGetPlaneCell(Vector2 screenPoint, WorkshopPlanarAxis axis, int depth, out Vector3Int cell)
        {
            cell = default;
            return IsPlanar && session.IsEditing && !GameManager.GameplayBlocked && Application.isFocused &&
                ui.TryGetViewportPoint(screenPoint, out var uv) && display.TryGetPlaneCell(uv, axis, depth, out cell);
        }

        private void OnAssemblyChanged() => sourceDirty = true;

        private void OnAttach(InputAction.CallbackContext context)
        {
            if (!IsPlanar || !TryGetGridSelection(point.ReadValue<Vector2>(), out var selection)) return;
            if (selection.IsValid) builder.TryPlaceGrid(selection);
        }

        private void LateUpdate()
        {
            if (ui == null) return;
            if (IsPlanar && (!session.IsEditing || session.ActiveWorkbench.CurrentWeapon != weapon || assembly == null)) ExitPlanar();
            bool visible = session.IsEditing && !GameManager.GameplayBlocked && Application.isFocused;
            if (IsPlanar && !visible && !suspended)
            {
                display.SetVisible(false); display.Suspend(); suspended = true;
            }
            else if (IsPlanar && visible && suspended)
            {
                display.Resume(); suspended = false;
            }
            HasHoveredCell = false; hovered = default;
            string message = "인접한 격자 셀 클릭: 설치 · 휠: 확대 · Ctrl+Z: 되돌리기";
            ui.Show(visible, IsPlanar, CanOpen, Axis, Depth, message);
            if (!IsPlanar || !visible) return;
            if (sourceDirty) { display.RefreshSource(); sourceDirty = false; }

            var canvas = ui.Viewport.GetComponentInParent<Canvas>();
            float scale = canvas.scaleFactor;
            display.Resize(Mathf.RoundToInt(ui.Viewport.rect.width * scale), Mathf.RoundToInt(ui.Viewport.rect.height * scale));
            ui.SetTexture(display.Texture);
            display.SetVisible(true);
            Vector2 pointer = point.ReadValue<Vector2>();
            if (ui.TryGetViewportPoint(pointer, out _) && Mathf.Abs(zoom.ReadValue<Vector2>().y) > .001f &&
                TutorialManager.TryAction("zoom")) display.Zoom(zoom.ReadValue<Vector2>().y);
            display.PrepareView();
            HasHoveredCell = TryGetGridSelection(pointer, out hovered);
            if (HasHoveredCell)
                message = hovered.IsValid
                    ? $"설치 가능: {hovered.Cell} · {Axis} 깊이 {Depth}\n클릭: 설치 · Ctrl+Z: 되돌리기"
                    : hovered.FailureReason;
            ui.Show(visible, true, CanOpen, Axis, Depth, message);
            display.Draw(HasHoveredCell, hovered);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            HasHoveredCell = false; hovered = default;
        }
    }
}
