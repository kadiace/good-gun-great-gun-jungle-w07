using TMPro;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 기존 직렬화 필드와 Bind·Unbind·CloseRetry·CancelTest API를 유지하며 사격 시험 HUD의
    /// View·Presenter를 연결하는 브리지입니다. HUD 컴포넌트는 HudLayer/ShootingTestScreen 묶음 루트에 둡니다.
    /// </summary>
    public sealed class ShootingTestHUD : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private ShootingTestHudView m_view;

        private ShootingTestHudPresenter m_presenter;

        public ShootingTestController Controller
        {
            get
            {
                return m_presenter != null ? m_presenter.Controller : null;
            }
        }

        private void Awake()
        {
            if (m_view == null)
            {
                Debug.LogError(
                    "[ShootingTestHUD] Initialize 실패: m_view 참조 누락. 같은 오브젝트의 ShootingTestHudView 연결이 필요합니다.",
                    this);
                return;
            }

            CreatePresenter();
        }

        public void Bind(ShootingTestController controller)
        {
            if (EnsurePresenter())
            {
                m_presenter.Bind(controller);
            }
        }

        public void Unbind(ShootingTestController controller)
        {
            if (m_presenter != null)
            {
                m_presenter.Unbind(controller);
            }
        }

        public bool CloseRetry()
        {
            return m_presenter != null && m_presenter.CloseRetry();
        }

        public void CancelTest()
        {
            if (m_presenter != null)
            {
                m_presenter.CancelTest();
            }
        }

        private void LateUpdate()
        {
            if (m_presenter != null)
            {
                m_presenter.Tick();
            }
        }

        private void OnDisable()
        {
            if (m_presenter != null)
            {
                m_presenter.HandleDisabled();
            }
        }

        private void OnDestroy()
        {
            if (m_presenter != null)
            {
                m_presenter.Dispose();
                m_presenter = null;
            }
        }

        private bool EnsurePresenter()
        {
            if (m_presenter != null)
            {
                return true;
            }

            if (m_view == null)
            {
                return false;
            }

            CreatePresenter();
            return m_presenter != null;
        }

        private void CreatePresenter()
        {
            m_presenter = new ShootingTestHudPresenter(m_view);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
            }
        }
    }
}
