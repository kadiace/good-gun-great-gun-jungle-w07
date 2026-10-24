using UnityEngine;

namespace WeaponWorkshop
{
    // 플레이어 체력 도메인의 변경을 구독해 표시 비율만 View에 전달한다.
    public sealed class RaidHealthPresenter
    {
        private const string PRESENTER_NAME = "RaidHealthPresenter";

        private readonly RaidHealthUI m_view;
        private readonly UIManager m_uiManager;

        private RaidPlayerHealth m_health;
        private bool m_isInitialized;
        private bool m_hasLoggedWiringError;

        public RaidHealthPresenter(RaidHealthUI _view, UIManager _uiManager)
        {
            m_view = _view;
            m_uiManager = _uiManager;
        }

        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null || m_uiManager == null)
            {
                if (!m_hasLoggedWiringError)
                {
                    m_hasLoggedWiringError = true;
                    Debug.LogError(
                        $"[{PRESENTER_NAME}] 초기화 실패: view={m_view != null}, uiManager={m_uiManager != null}. " +
                        "HealthHUD의 RaidHealthUI 참조를 확인하세요.");
                }

                return false;
            }

            FirstPersonPlayer player = m_uiManager.Player;
            m_health = player != null ? player.GetComponent<RaidPlayerHealth>() : null;
            if (m_health != null)
            {
                m_health.Changed += OnHealthChanged;
            }

            m_isInitialized = true;
            Refresh();
            return true;
        }

        public void Refresh()
        {
            if (!m_isInitialized)
            {
                return;
            }

            if (m_health == null || !m_view.isActiveAndEnabled)
            {
                m_view.HideDisplay();
                return;
            }

            m_view.ShowHealth(m_health.CurrentHealth, m_health.MaximumHealth);
        }

        public void Dispose()
        {
            if (m_health != null)
            {
                m_health.Changed -= OnHealthChanged;
            }

            m_health = null;
            m_isInitialized = false;
        }

        private void OnHealthChanged()
        {
            Refresh();
        }
    }
}
