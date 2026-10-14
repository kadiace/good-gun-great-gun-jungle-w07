using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent, RequireComponent(typeof(FirstPersonPlayer)), DefaultExecutionOrder(40)]
    public sealed class PlayerManualReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        private InputActionMap controls;
        private InputActionMap readingControls;
        private InputAction zoomAction;
        private InputAction dragAction;
        private InputAction dragDeltaAction;
        private GameManager manager;
        private WorkshopSession workshop;
        private WorkshopManual manual;
        private int openedFrame;
        private bool closeRequested;
        public bool IsReading => manual != null;
        public Camera ReadingCamera { get; private set; }

        private void Awake()
        {
            manager = GameManager.Instance;
            ReadingCamera = GetComponent<FirstPersonPlayer>().ViewPoint.GetComponentInChildren<Camera>();
            workshop = GetComponent<WorkshopSession>();
            manual = null;
            closeRequested = false;
            if (manager != null) manager.SetManual(false);
            if (inputActions == null) { enabled = false; return; }
            controls = inputActions.FindActionMap("Workshop", true).Clone();
            controls.FindAction("Interact", true).performed += OnInteract;
            zoomAction = controls.FindAction("Zoom", true);
            readingControls = inputActions.FindActionMap("Build", true).Clone();
            dragAction = readingControls.FindAction("Attach", true);
            dragDeltaAction = readingControls.FindAction("Look", true);
        }

        private void OnEnable()
        {
            controls?.Enable();
            if (manager != null) manager.ManualCloseRequested += RequestClose;
        }

        private void OnDisable()
        {
            controls?.Disable();
            readingControls?.Disable();
            if (manager != null) manager.ManualCloseRequested -= RequestClose;
            Close();
        }

        private void OnDestroy()
        {
            controls?.Dispose();
            readingControls?.Dispose();
        }

        private void Update()
        {
            if (!IsReading || closeRequested || !Application.isFocused || Time.frameCount == openedFrame) return;
            manual.AdjustReading(zoomAction.ReadValue<Vector2>().y,
                dragAction.IsPressed() ? dragDeltaAction.ReadValue<Vector2>() : Vector2.zero);
        }

        private void OnInteract(InputAction.CallbackContext context)
        {
            // Opening and closing share the existing Interact action, regardless of callback order.
            if (IsReading && Time.frameCount != openedFrame) RequestClose();
        }

        public bool Open(WorkshopManual target)
        {
            if (!isActiveAndEnabled || target == null || ReadingCamera == null || manager == null || IsReading || GameManager.GameplayBlocked ||
                (workshop != null && (workshop.IsInWorkshop || workshop.IsTransitioning))) return false;
            manual = target;
            openedFrame = Time.frameCount;
            closeRequested = false;
            manager.SetManual(true);
            readingControls.Enable();
            manual.Show(this);
            return true;
        }

        public void RequestClose() => closeRequested = IsReading;

        private void LateUpdate()
        {
            // Keep the modal gate until all input callbacks finish; F must not also re-open the book.
            if (closeRequested) Close();
        }

        public void Close()
        {
            bool wasReading = IsReading;
            var previous = manual;
            manual = null;
            closeRequested = false;
            readingControls?.Disable();
            if (previous != null) previous.PutDown();
            if (wasReading && manager != null) manager.SetManual(false);
        }
    }
}
