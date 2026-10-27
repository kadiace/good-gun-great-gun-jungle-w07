using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    // 플레이어 매뉴얼 읽기 입력 브리지. 기존 Input System 액션 클론과 수명을 그대로 유지하고
    // 읽기 세션 상태·닫기 요청·줌·팬 전달은 ManualReadingSessionPresenter에 위임한다.
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
        private ManualReadingSessionPresenter m_presenter;
        public bool IsReading => m_presenter != null && m_presenter.IsReading;
        public Camera ReadingCamera { get; private set; }

        private void Awake()
        {
            manager = GameManager.Instance;
            ReadingCamera = GetComponent<FirstPersonPlayer>().ViewPoint.GetComponentInChildren<Camera>();
            workshop = GetComponent<WorkshopSession>();
            if (manager != null) manager.SetManual(false);
            if (inputActions == null) { enabled = false; return; }
            controls = inputActions.FindActionMap("Workshop", true).Clone();
            controls.FindAction("Interact", true).performed += OnInteract;
            zoomAction = controls.FindAction("Zoom", true);
            readingControls = inputActions.FindActionMap("Build", true).Clone();
            dragAction = readingControls.FindAction("Attach", true);
            dragDeltaAction = readingControls.FindAction("Look", true);
            m_presenter = new ManualReadingSessionPresenter(this, manager, workshop, readingControls,
                zoomAction, dragAction, dragDeltaAction);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
            }
        }

        private void OnEnable()
        {
            controls?.Enable();
            m_presenter?.Activate();
        }

        private void OnDisable()
        {
            controls?.Disable();
            m_presenter?.Deactivate();
        }

        private void OnDestroy()
        {
            m_presenter?.Dispose();
            m_presenter = null;
            controls?.Dispose();
            readingControls?.Dispose();
        }

        private void Update()
        {
            m_presenter?.Tick();
        }

        private void OnInteract(InputAction.CallbackContext context)
        {
            m_presenter?.HandleInteract();
        }

        public bool Open(WorkshopManual target)
        {
            return m_presenter != null && m_presenter.Open(target);
        }

        public void RequestClose()
        {
            m_presenter?.RequestClose();
        }

        private void LateUpdate()
        {
            // Keep the modal gate until all input callbacks finish; F must not also re-open the book.
            m_presenter?.LateTick();
        }

        public void Close()
        {
            m_presenter?.Close();
        }
    }
}
