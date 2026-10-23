using System;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 습격 사망 시 컨트롤러 결과 화면으로 넘기거나, 컨트롤러가 없을 때 레거시 패널을 표시하는 Presenter입니다.
    /// 체력 구독·해제 시점과 귀환 요청은 기존 RaidDeathUI 동작을 그대로 유지합니다.
    /// </summary>
    public sealed class RaidDeathPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "RaidDeathPresenter";

        private readonly RaidDeathView m_view;
        private readonly UIManager m_uiManager;

        private RaidPlayerHealth m_health;
        private bool m_hasStarted;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public RaidDeathPresenter(RaidDeathView _view, UIManager _uiManager)
        {
            m_view = _view;
            m_uiManager = _uiManager;
        }

        /// <summary>
        /// View를 초기화하고 귀환 입력을 구독합니다. 중복 호출에 안전합니다.
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
                    Debug.LogError("[" + PRESENTER_NAME + "] Initialize 실패: RaidDeathView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.ReturnRequested -= OnReturnRequested;
            m_view.ReturnRequested += OnReturnRequested;

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 체력 구독과 View 이벤트 구독을 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            UnbindHealth();
            if (m_view != null)
            {
                m_view.ReturnRequested -= OnReturnRequested;
                m_view.Dispose();
            }

            m_isInitialized = false;
        }

        /// <summary>
        /// 브리지 OnEnable에 대응합니다. 플레이어 체력이 바뀌었으면 구독을 옮깁니다.
        /// </summary>
        public void HandleEnabled()
        {
            BindHealth();
        }

        /// <summary>
        /// 브리지 Start에 대응합니다. 시작 이후에는 이미 사망한 체력도 표시 대상으로 삼습니다.
        /// </summary>
        public void MarkStarted()
        {
            m_hasStarted = true;
            BindHealth();
        }

        /// <summary>
        /// 브리지 OnDisable에 대응합니다. 체력 구독을 해제하고 레거시 패널을 숨깁니다.
        /// </summary>
        public void HandleDisabled()
        {
            UnbindHealth();
            if (m_view != null)
            {
                m_view.Hide();
            }
        }

        private void BindHealth()
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            FirstPersonPlayer player = m_uiManager != null ? m_uiManager.Player : null;
            RaidPlayerHealth nextHealth = player != null ? player.GetComponent<RaidPlayerHealth>() : null;
            if (m_health != nextHealth)
            {
                if (m_health != null)
                {
                    m_health.Died -= Show;
                }

                m_health = nextHealth;
                if (m_health != null)
                {
                    m_health.Died += Show;
                }
            }

            if (m_hasStarted && m_health != null && m_health.IsDead)
            {
                Show();
            }
        }

        private void UnbindHealth()
        {
            if (m_health != null)
            {
                m_health.Died -= Show;
            }

            m_health = null;
        }

        private void Show()
        {
            if (NightRaidController.Instance != null)
            {
                if (m_view != null)
                {
                    m_view.Hide();
                }

                NightRaidController.Instance.ShowDeathResult();
                return;
            }

            GameManager.Instance?.SetDialogue(true);
            GameManager.Instance?.SetNight(true);
            FirstPersonPlayer player = m_uiManager != null ? m_uiManager.Player : null;
            player?.GetComponent<WeaponHolder>()?.EquippedWeapon?.CancelCharge();
            if (m_view != null)
            {
                m_view.Show();
            }
        }

        private void OnReturnRequested()
        {
            NightRaidController.Instance?.ReturnAfterDeath();
        }
    }
}
