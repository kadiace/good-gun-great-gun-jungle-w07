using System;

using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    /// <summary>
    /// 매뉴얼 읽기 세션(열기·닫기 요청·프레임 가드·줌·팬 전달)과 입력 맵 점유를 담당하는 Presenter입니다.
    /// 입력 액션 해석과 수명은 PlayerManualReader 브리지가 소유하고, 이 클래스는 상태와 요청만 다룹니다.
    /// </summary>
    public sealed class ManualReadingSessionPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "ManualReadingSessionPresenter";

        private readonly PlayerManualReader m_owner;
        private readonly GameManager m_manager;
        private readonly WorkshopSession m_workshop;
        private readonly InputActionMap m_readingControls;
        private readonly InputAction m_zoomAction;
        private readonly InputAction m_dragAction;
        private readonly InputAction m_dragDeltaAction;

        private WorkshopManual m_manual;
        private int m_openedFrame;
        private bool m_closeRequested;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public bool IsReading
        {
            get
            {
                return m_manual != null;
            }
        }

        public ManualReadingSessionPresenter(PlayerManualReader _owner, GameManager _manager,
            WorkshopSession _workshop, InputActionMap _readingControls, InputAction _zoomAction,
            InputAction _dragAction, InputAction _dragDeltaAction)
        {
            m_owner = _owner;
            m_manager = _manager;
            m_workshop = _workshop;
            m_readingControls = _readingControls;
            m_zoomAction = _zoomAction;
            m_dragAction = _dragAction;
            m_dragDeltaAction = _dragDeltaAction;
        }

        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_owner == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError(
                        $"[{PRESENTER_NAME}] Initialize 실패: PlayerManualReader 참조가 없습니다.");
                }

                return false;
            }

            m_isInitialized = true;
            return true;
        }

        public void Activate()
        {
            if (!m_isInitialized)
            {
                return;
            }

            if (m_manager != null)
            {
                m_manager.ManualCloseRequested -= RequestClose;
                m_manager.ManualCloseRequested += RequestClose;
            }
        }

        public void Deactivate()
        {
            if (m_manager != null)
            {
                m_manager.ManualCloseRequested -= RequestClose;
            }

            Close();
        }

        /// <summary>
        /// 읽는 동안 기존 Zoom·Build/Attach·Build/Look 입력으로 줌·팬을 전달합니다.
        /// </summary>
        public void Tick()
        {
            if (!IsReading || m_closeRequested || !Application.isFocused || Time.frameCount == m_openedFrame)
            {
                return;
            }

            m_manual.AdjustReading(m_zoomAction.ReadValue<Vector2>().y,
                m_dragAction.IsPressed() ? m_dragDeltaAction.ReadValue<Vector2>() : Vector2.zero);
        }

        public void LateTick()
        {
            // 입력 콜백이 모두 끝날 때까지 모달 차단을 유지해 F가 책을 다시 열지 않게 한다.
            if (m_closeRequested)
            {
                Close();
            }
        }

        public void HandleInteract()
        {
            // 열기와 닫기는 같은 기존 Interact 액션을 쓰며 콜백 순서와 무관하게 프레임 가드를 둔다.
            if (IsReading && Time.frameCount != m_openedFrame)
            {
                RequestClose();
            }
        }

        public bool Open(WorkshopManual _target)
        {
            if (!m_owner.isActiveAndEnabled || _target == null || m_owner.ReadingCamera == null || m_manager == null ||
                IsReading || GameManager.GameplayBlocked ||
                (m_workshop != null && (m_workshop.IsInWorkshop || m_workshop.IsTransitioning)))
            {
                return false;
            }

            m_manual = _target;
            m_openedFrame = Time.frameCount;
            m_closeRequested = false;
            m_manager.SetManual(true);
            m_readingControls.Enable();
            m_manual.Show(m_owner);
            return true;
        }

        public void RequestClose()
        {
            m_closeRequested = IsReading;
        }

        public void Close()
        {
            bool wasReading = IsReading;
            WorkshopManual previous = m_manual;
            m_manual = null;
            m_closeRequested = false;
            m_readingControls?.Disable();
            if (previous != null)
            {
                previous.PutDown();
            }

            if (wasReading && m_manager != null)
            {
                m_manager.SetManual(false);
            }
        }

        public void Dispose()
        {
            if (m_manager != null)
            {
                m_manager.ManualCloseRequested -= RequestClose;
            }

            m_manual = null;
            m_closeRequested = false;
            m_isInitialized = false;
        }
    }
}
