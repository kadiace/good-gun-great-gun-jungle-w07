using System;
using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 구체적인 PauseView와 기존 GameManager·UIRoot를 명시적으로 연결해 일시정지 표시·레이어 순서·포커스 복원 및
    /// 기본 일시정지 요청(Resume)을 전달하는 Presenter입니다.
    /// </summary>
    public sealed class PausePresenter : IDisposable
    {
        private const string PRESENTER_NAME = "PausePresenter";

        private readonly PauseView m_view;
        private readonly GameManager m_gameManager;
        private readonly UIRoot m_uiRoot;

        private bool m_isInitialized;
        private bool m_isOpen;
        private bool m_hasLoggedDependencyError;

        public bool IsInitialized
        {
            get
            {
                return m_isInitialized;
            }
        }

        public bool IsOpen
        {
            get
            {
                return m_isOpen;
            }
        }

        public PausePresenter(PauseView _view, GameManager _gameManager, UIRoot _uiRoot)
        {
            m_view = _view;
            m_gameManager = _gameManager;
            m_uiRoot = _uiRoot;
        }

        public bool IsBoundTo(GameManager _gameManager)
        {
            return m_gameManager == _gameManager;
        }

        /// <summary>
        /// View·GameManager·UIRoot 필수 참조와 레이어 구성을 확인하고 Resume 버튼 이벤트를 구독합니다.
        /// 하나라도 누락되면 해당 화면 초기화를 실패로 처리합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null || m_gameManager == null || m_uiRoot == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError(
                        $"[{PRESENTER_NAME}] Initialize 실패: 필수 의존성 누락 " +
                        $"(_view={m_view != null}, _gameManager={m_gameManager != null}, _uiRoot={m_uiRoot != null})");
                }

                return false;
            }

            if (!m_uiRoot.ValidateLayers())
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError($"[{PRESENTER_NAME}] Initialize 실패: UIRoot 레이어 참조 누락");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.ResumeRequested -= OnResumeRequested;
            m_view.ResumeRequested += OnResumeRequested;

            m_isOpen = false;
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// View 이벤트 구독과 버튼 리스너를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.ResumeRequested -= OnResumeRequested;
                m_view.Dispose();
            }

            m_isOpen = false;
            m_isInitialized = false;
        }

        /// <summary>
        /// 생성자에 전달된 GameManager의 일시정지 상태에 맞춰 표시 전환 시에만
        /// PauseView 표시, 레이어 내 순서, 포커스 보관·복원을 적용합니다.
        /// </summary>
        public void Refresh()
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            bool shouldShow = m_gameManager.IsPaused;
            bool pauseChanged = m_isOpen != shouldShow || m_view.IsVisible != shouldShow;

            if (shouldShow)
            {
                if (!pauseChanged)
                {
                    return;
                }

                m_uiRoot.SaveCurrentFocus(m_view.RectTransform);
                m_uiRoot.BringToLayerFront(m_view.RectTransform, m_uiRoot.ScreenLayer);
                m_view.Show();
                m_isOpen = true;
                m_uiRoot.ApplyFocus(m_view.DefaultSelectable, m_view.NavigationButtons);
                return;
            }

            if (!pauseChanged)
            {
                return;
            }

            m_view.Hide();
            m_isOpen = false;
            m_uiRoot.RestorePreviousFocus();
        }

        private void OnResumeRequested()
        {
            if (m_gameManager != null)
            {
                m_gameManager.Resume();
            }
        }
    }
}
