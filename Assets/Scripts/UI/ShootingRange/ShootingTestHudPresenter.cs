using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// ShootingTestHudView와 ShootingTestController 도메인을 연결해 표시 상태·문구·색,
    /// 합격 후 4초 자동 종료와 재시도·취소 요청을 담당하는 Presenter입니다.
    /// </summary>
    public sealed class ShootingTestHudPresenter
    {
        private const string PRESENTER_NAME = "ShootingTestHudPresenter";
        private const float PASSED_AUTO_CANCEL_SECONDS = 4f;
        private const float TIMER_WARNING_SECONDS = 5f;

        private readonly ShootingTestHudView m_view;

        private ShootingTestController m_controller;
        private float m_completedAt = -1f;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public ShootingTestHudPresenter(ShootingTestHudView _view)
        {
            m_view = _view;
        }

        public ShootingTestController Controller
        {
            get
            {
                return m_controller;
            }
        }

        /// <summary>
        /// View를 초기화하고 시작 숨김 상태를 적용합니다. 중복 호출에 안전합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError("[" + PRESENTER_NAME + "] Initialize 실패: ShootingTestHudView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 표시를 숨기고 컨트롤러 참조를 정리합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            m_controller = null;
            m_completedAt = -1f;

            if (m_view != null)
            {
                m_view.Dispose();
            }

            m_isInitialized = false;
        }

        /// <summary>
        /// 표시할 시험 컨트롤러를 연결하고 자동 종료 기준 시간을 초기화합니다.
        /// </summary>
        public void Bind(ShootingTestController _controller)
        {
            m_controller = _controller;
            m_completedAt = -1f;
        }

        /// <summary>
        /// 같은 컨트롤러의 연결 해제 요청에만 반응해 표시를 숨깁니다.
        /// </summary>
        public void Unbind(ShootingTestController _controller)
        {
            if (m_controller != _controller)
            {
                return;
            }

            m_controller = null;
            if (m_view != null)
            {
                m_view.SetVisible(false);
            }
        }

        /// <summary>
        /// 실패 상태에서만 취소하고 처리 여부를 돌려줍니다.
        /// </summary>
        public bool CloseRetry()
        {
            if (m_controller == null || m_controller.State != ShootingTestController.TestState.Failed)
            {
                return false;
            }

            m_controller.Cancel();
            return true;
        }

        public void CancelTest()
        {
            if (m_controller != null)
            {
                m_controller.Cancel();
            }
        }

        /// <summary>
        /// 시험 상태·남은 시간·상세·재시도 안내를 표시하고 합격 후 4초가 지나면 자동 종료합니다.
        /// </summary>
        public void Tick()
        {
            if (m_view == null)
            {
                return;
            }

            ShootingTestController controller = m_controller;
            bool visible = controller != null && controller.Definition != null &&
                controller.State != ShootingTestController.TestState.Idle;
            if (visible && controller.State == ShootingTestController.TestState.Passed)
            {
                if (m_completedAt < 0f)
                {
                    m_completedAt = Time.unscaledTime;
                }

                if (Time.unscaledTime - m_completedAt > PASSED_AUTO_CANCEL_SECONDS)
                {
                    controller.Cancel();
                    visible = false;
                }
            }

            m_view.SetVisible(visible);
            if (!visible)
            {
                return;
            }

            bool failed = controller.State == ShootingTestController.TestState.Failed;
            string timer = (failed ? "다음 시도 " : "남은 시간 ") + controller.RemainingSeconds.ToString("0.0") + "초";
            Color timerColor = !failed && controller.RemainingSeconds <= TIMER_WARNING_SECONDS
                ? new Color(1f, .35f, .25f)
                : Color.white;

            string details = controller.StatusText ?? "";
            if (controller.NonForwardMuzzles > 0)
            {
                details += "\n* 현재 일부 총구가 전방을 향해 있지 않습니다 (" + controller.NonForwardMuzzles + "개).";
            }

            string retry = failed
                ? (controller.CanRetryWithWeapon
                    ? "F: 같은 테스트 즉시 재시도 · Esc: 종료"
                    : controller.CanRetryNearby
                        ? "테스트할 총을 손에 든 뒤 F: 재시도 · Esc: 종료"
                        : "테이블 근처로 돌아와 F: 재시도 · Esc: 종료")
                : "공격 종류와 관계없이 모든 표적을 각각 기준 이상 파괴하세요.";

            m_view.SetContent(controller.Definition.DisplayName, timer, timerColor, details, retry);
        }

        /// <summary>
        /// 비활성화 시 진행 중인 시험을 취소하고 표시를 숨깁니다.
        /// </summary>
        public void HandleDisabled()
        {
            CancelTest();
            if (m_view != null)
            {
                m_view.SetVisible(false);
            }
        }
    }
}
