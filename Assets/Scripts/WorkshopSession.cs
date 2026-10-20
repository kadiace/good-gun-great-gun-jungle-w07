using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(FirstPersonPlayer)), DefaultExecutionOrder(25)]
    public sealed class WorkshopSession : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Camera playerCamera;
        private CubeBuilder builder;
        [SerializeField, Min(0f)] private float rotationSensitivity = 0.3f;
        [SerializeField, Min(0f)] private float zoomDegreesPerScroll = 5f;
        [SerializeField, Range(1f, 179f)] private float minWorkshopFieldOfView = 20f;
        [SerializeField, Range(1f, 179f)] private float maxWorkshopFieldOfView = 80f;
        private InputActionMap editingControls;
        private InputAction rotate, look, point, zoom;
        private bool rotationDrag, rollDrag;
        private float rotationDragDegrees;
        private Vector2 previousRollOffset;
        public bool IsEditing => IsInWorkshop && !IsTransitioning;
        public Rect RotationViewport => playerCamera.pixelRect;
        public Camera WorkshopCamera => playerCamera;
        private UIManager workshopUI;
        public void BindWorkshopUI(UIManager owner) => workshopUI = owner;
        private float RotationRadius => workshopUI != null ? workshopUI.GetWorkshopRotationRadius(RotationViewport) : 0f;
        public bool CanEnterWorkshop => isActiveAndEnabled && controls != null &&
            playerCamera != null && builder != null && !IsInWorkshop && !IsTransitioning;
        private FirstPersonPlayer player;
        private WeaponHolder weaponHolder;
        private Workbench activeWorkbench;
        public event System.Action WorkbenchEntered;
        public event System.Action<EquipableWeapon> WorkbenchExited;
        public Workbench ActiveWorkbench => activeWorkbench;
        private InputAction exit;
        private InputActionMap controls;
        private Coroutine cursorRestore;
        private Coroutine cameraMove;
        private float originalFieldOfView;
        private bool hasOriginalFieldOfView;
        public bool IsTransitioning => cameraMove != null;
        private Renderer[] playerRenderers;
        private ShadowCastingMode[] originalShadows;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        public bool IsInWorkshop => activeWorkbench != null;

        private void Awake()
        {
            player = GetComponent<FirstPersonPlayer>();
            weaponHolder = GetComponent<WeaponHolder>();
            builder = playerCamera != null ? playerCamera.GetComponent<CubeBuilder>() : null;
            if (builder != null) builder.enabled = false;
            workshopUI?.SetWorkshopUI(false);
            if (inputActions == null || playerCamera == null) { enabled = false; return; }
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            playerRenderers = GetComponentsInChildren<Renderer>();
            originalShadows = new ShadowCastingMode[playerRenderers.Length];
            for (int i = 0; i < playerRenderers.Length; i++) originalShadows[i] = playerRenderers[i].shadowCastingMode;
            controls = inputActions.FindActionMap("Workshop", true).Clone();
            editingControls = inputActions.FindActionMap("Build", true).Clone();
            rotate = editingControls.FindAction("Rotate", true);
            look = editingControls.FindAction("Look", true);
            point = editingControls.FindAction("Point", true);
            rotate.performed += _ => BeginRotation();
            rotate.canceled += _ => EndRotation();
            editingControls.FindAction("ResetRotation", true).performed += _ =>
            {
                if (!IsEditing || GameManager.GameplayBlocked) return;
                if (!TutorialManager.TryAction("reset-rotation")) return;
                rotationDrag = false;
                activeWorkbench.ResetRotation();
                if (TutorialManager.Instance != null) TutorialManager.Instance.NotifySignal("rotation-reset", activeWorkbench);
            };
            exit = controls.FindAction("Exit", true);
            zoom = controls.FindAction("Zoom", true);
            exit.performed += _ => { if (GameManager.Instance == null) Leave(); };
            controls.FindAction("Interact", true).performed += _ =>
            {
                if (IsInWorkshop && !IsTransitioning && !GameManager.GameplayBlocked) Leave();
            };
        }

        private void OnEnable() => controls?.Enable();
        private void Start() => SetMode(false);
        private void OnDisable()
        {
            controls?.Disable();
            SetEditing(false);
            if (cursorRestore != null) StopCoroutine(cursorRestore);
            cursorRestore = null;
            if (activeWorkbench != null || cameraMove != null) SetMode(false);
            if (playerRenderers != null)
                for (int i = 0; i < playerRenderers.Length; i++)
                    if (playerRenderers[i] != null) playerRenderers[i].shadowCastingMode = originalShadows[i];
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }
        private void OnDestroy() { controls?.Dispose(); editingControls?.Dispose(); }

        public bool HasCoreForWorkbench(Workbench bench)
        {
            var carried = weaponHolder != null ? weaponHolder.EquippedWeapon : null;
            if (carried != null) return carried.GetComponentInChildren<PowerCore>(true) != null;
            return bench != null && bench.CurrentCore != null;
        }

        public bool Enter(Workbench bench, bool animate = true)
        {
            if (!CanEnterWorkshop || bench == null ||
                !bench.CanInteract() || !HasCoreForWorkbench(bench)) return false;
            if (!bench.ExchangeWeapon(weaponHolder)) return false;
            activeWorkbench = bench;
            originalFieldOfView = playerCamera.fieldOfView;
            hasOriginalFieldOfView = true;
            SetMode(true, animate);
            WorkbenchEntered?.Invoke();
            return true;
        }

        public void Leave()
        {
            if (!TutorialManager.TryAction("exit")) return;
            if (!IsInWorkshop) return;
            SetMode(false, true);
            if (cursorRestore != null) StopCoroutine(cursorRestore);
            cursorRestore = StartCoroutine(RestoreCursorAfterEscape());
        }
        public void LeaveImmediately()
        {
            if (cursorRestore != null) StopCoroutine(cursorRestore);
            cursorRestore = null;
            SetMode(false);
            SetCursor();
        }

        private IEnumerator RestoreCursorAfterEscape()
        {
            while (exit != null && exit.IsPressed()) yield return null;
            yield return null;
            if (!IsInWorkshop) SetCursor();
            yield return new WaitForEndOfFrame();
            if (!IsInWorkshop) SetCursor();
            cursorRestore = null;
        }

        private void SetMode(bool workshop, bool animate = false)
        {
            if (!workshop && activeWorkbench != null) WorkbenchExited?.Invoke(activeWorkbench.CurrentWeapon);
            if (cameraMove != null) StopCoroutine(cameraMove);
            cameraMove = null;
            if (!workshop) RestoreWorkshopFieldOfView();
            player.enabled = !workshop && !animate;
            SetEditing(workshop && !animate);
            // The camera always stays under the player's ViewPoint; the workbench view only moves its world pose.
            if (!animate) SnapCamera(workshop);
            if (!workshop) activeWorkbench = null;
            for (int i = 0; i < playerRenderers.Length; i++)
                if (playerRenderers[i] != null)
                    playerRenderers[i].shadowCastingMode = workshop ? originalShadows[i] : ShadowCastingMode.ShadowsOnly;
            SetCursor();
            if (animate) cameraMove = StartCoroutine(MoveCamera(workshop));
        }

        private IEnumerator MoveCamera(bool workshop)
        {
            Transform cameraTransform = playerCamera.transform;
            Vector3 startPosition = cameraTransform.position;
            Quaternion startRotation = cameraTransform.rotation;
            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.5f));
                // Re-read the target every frame so a moving workbench or ViewPoint is still followed.
                Transform target = CameraTarget(workshop);
                cameraTransform.SetPositionAndRotation(Vector3.Lerp(startPosition, target.position, t),
                    Quaternion.Slerp(startRotation, target.rotation, t));
                yield return null;
            }
            SnapCamera(workshop);
            cameraMove = null;
            player.enabled = !workshop;
            if (workshop && activeWorkbench != null) SetEditing(true);
        }

        private Transform CameraTarget(bool workshop) =>
            workshop && activeWorkbench != null ? activeWorkbench.CameraPosition : player.ViewPoint;

        private void SnapCamera(bool workshop)
        {
            Transform cameraTransform = playerCamera.transform;
            if (workshop && activeWorkbench != null)
            {
                Transform target = activeWorkbench.CameraPosition;
                cameraTransform.SetPositionAndRotation(target.position, target.rotation);
                return;
            }
            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;
        }

        private void SetEditing(bool editing)
        {
            rotationDrag = false;
            if (builder != null)
            {
                builder.SetWorkspace(editing && activeWorkbench != null ? activeWorkbench.RotationTarget : null);
                builder.enabled = editing;
            }
            if (editingControls != null)
                foreach (string name in new[] { "Rotate", "Look", "Point", "ResetRotation" })
                {
                    var action = editingControls.FindAction(name, true);
                    if (editing) action.Enable(); else action.Disable();
                }
            workshopUI?.SetWorkshopUI(editing);
        }

        private void BeginRotation()
        {
            rotationDragDegrees = 0f;
            if (!IsEditing || GameManager.GameplayBlocked) return;
            if (!TutorialManager.TryAction("rotate")) return;
            Vector2 pointer = point.ReadValue<Vector2>();
            rotationDrag = RotationViewport.Contains(pointer);
            previousRollOffset = pointer - RotationViewport.center;
            rollDrag = previousRollOffset.sqrMagnitude > RotationRadius * RotationRadius;
        }

        private void EndRotation()
        {
            bool completedDrag = rotationDrag;
            rotationDrag = false;
            if (!completedDrag || !IsEditing || GameManager.GameplayBlocked || !Application.isFocused) return;
            if (TutorialManager.Instance != null) TutorialManager.Instance.NotifyRotation(rollDrag, activeWorkbench, rotationDragDegrees);
        }

        private void Update()
        {
            if (IsInWorkshop && !activeWorkbench.CanInteract())
            {
                if (TutorialManager.ActionAllowed("exit")) Leave();
                return;
            }
            if (!IsEditing || GameManager.GameplayBlocked || !Application.isFocused)
            { rotationDrag = false; return; }
            ApplyWorkshopZoom();
            if (!TutorialManager.ActionAllowed("rotate")) { rotationDrag = false; return; }
            if (!rotationDrag || !rotate.IsPressed()) return;
            Transform target = activeWorkbench.RotationTarget;
            if (rollDrag)
            {
                Vector2 offset = point.ReadValue<Vector2>() - RotationViewport.center;
                if (offset.sqrMagnitude > 16f && previousRollOffset.sqrMagnitude > 16f)
                {
                    float angle = Vector2.SignedAngle(previousRollOffset, offset);
                    target.Rotate(playerCamera.transform.forward, angle, Space.World);
                    rotationDragDegrees += Mathf.Abs(angle);
                }
                previousRollOffset = offset;
                return;
            }
            Vector2 delta = look.ReadValue<Vector2>();
            Vector3 rotation = playerCamera.transform.up * -delta.x + playerCamera.transform.right * delta.y;
            if (rotation.sqrMagnitude > 0f)
            {
                float angle = rotation.magnitude * rotationSensitivity;
                target.Rotate(rotation.normalized, angle, Space.World);
                rotationDragDegrees += Mathf.Abs(angle);
            }
        }

        private void SetCursor()
        {
            if (GameManager.GameplayBlocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }
            CursorLockMode desired = IsInWorkshop ? CursorLockMode.None : CursorLockMode.Locked;
            if (Cursor.lockState != desired) Cursor.lockState = desired;
            Cursor.visible = IsInWorkshop;
        }

        private void ApplyWorkshopZoom()
        {
            Vector2 scroll = zoom.ReadValue<Vector2>();
            if (Mathf.Abs(scroll.y) < 0.01f ||
                (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;
            if (!TutorialManager.TryAction("zoom")) return;

            float minFov = Mathf.Clamp(minWorkshopFieldOfView, 1f, 179f);
            float maxFov = Mathf.Clamp(maxWorkshopFieldOfView, minFov, 179f);
            playerCamera.fieldOfView = Mathf.Clamp(playerCamera.fieldOfView -
                scroll.y * zoomDegreesPerScroll, minFov, maxFov);
        }

        private void RestoreWorkshopFieldOfView()
        {
            if (!hasOriginalFieldOfView) return;
            playerCamera.fieldOfView = originalFieldOfView;
            hasOriginalFieldOfView = false;
        }

        private void LateUpdate()
        {
            if (controls == null) return;
            // Hold the workbench view while editing (the camera is not parented to the workbench).
            if (IsInWorkshop && cameraMove == null) SnapCamera(true);
            Cursor.visible = IsInWorkshop;
            if (Application.isFocused) SetCursor();
        }
        private void OnApplicationFocus(bool focused) { rotationDrag = false; if (focused && controls != null) SetCursor(); }
    }
}
