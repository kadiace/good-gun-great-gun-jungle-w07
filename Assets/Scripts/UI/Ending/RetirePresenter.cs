using System;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    /// <summary>
    /// 은퇴 화면의 열기 시각·선택 로그, 계속/로비 선택의 기존 정리 경로를 담당하는 Presenter입니다.
    /// 저장·결제는 RetireButton의 기존 경로가 원본이며, 이 클래스는 표시 상태와 씬 이동만 요청합니다.
    /// </summary>
    public sealed class RetirePresenter : IDisposable
    {
        private const string PRESENTER_NAME = "RetirePresenter";
        private const string MAIN_MENU_SCENE = "Intro";

        private readonly RetireView m_view;
        private readonly _DayManager m_dayManager;

        private bool m_firstEnding;
        private float m_openedAt;
        private bool m_leaving;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public bool IsOpen
        {
            get
            {
                return m_view != null && m_view.IsPanelSelfActive;
            }
        }

        public RetirePresenter(RetireView _view, _DayManager _dayManager)
        {
            m_view = _view;
            m_dayManager = _dayManager;
        }

        /// <summary>
        /// View를 초기화하고 계속·로비 입력을 구독합니다. 중복 호출에 안전합니다.
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
                    Debug.LogError("[" + PRESENTER_NAME + "] Initialize 실패: RetireView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.ContinueRequested -= OnContinueRequested;
            m_view.ContinueRequested += OnContinueRequested;
            m_view.LobbyRequested -= OnLobbyRequested;
            m_view.LobbyRequested += OnLobbyRequested;

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// View 이벤트 구독을 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.ContinueRequested -= OnContinueRequested;
                m_view.LobbyRequested -= OnLobbyRequested;
                m_view.Dispose();
            }

            m_isInitialized = false;
        }

        /// <summary>
        /// 은퇴 화면을 열고 대화 상태를 차단하며 계속 버튼을 기본 선택으로 지정합니다.
        /// </summary>
        public void Open(bool _firstEnding)
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            m_firstEnding = _firstEnding;
            m_openedAt = Time.unscaledTime;

            int day = m_dayManager != null ? m_dayManager.DayNumber : 0;
            m_view.Show(day + "일차부터 계속");
            m_view.FocusDefault();
            GameManager.Instance?.SetDialogue(true);
        }

        /// <summary>
        /// 계속하기: 화면을 닫고 선택을 해제한 뒤 대화 상태를 되돌립니다.
        /// </summary>
        public void Continue()
        {
            if (!IsOpen)
            {
                return;
            }

            LogChoice("continue");
            m_view.Hide();
            m_view.ClearSelection();
            GameManager.Instance?.SetDialogue(false);
        }

        /// <summary>
        /// 로비로: 기존 은퇴 종료 정리 순서(음악·습격·튜토리얼·대기 저장·시간 배율)를 그대로 실행하고 Intro로 이동합니다.
        /// Intro가 빌드에 없으면 아무것도 변경하지 않습니다.
        /// </summary>
        public void ReturnToLobby()
        {
            if (m_leaving || !IsOpen || !Application.CanStreamedLevelBeLoaded(MAIN_MENU_SCENE))
            {
                return;
            }

            m_leaving = true;
            LogChoice("lobby");
            SoundManager.RestoreBeforeRaidMusic(true);
            NightRaidTransfer.Abandon();
            TutorialManager.AbandonTraining();
            GameSaveFiles.PendingLoad = null;
            UIInputPolicy.ApplyLobbyPolicy();
            SceneManager.LoadSceneAsync(MAIN_MENU_SCENE);
        }

        private void OnContinueRequested()
        {
            Continue();
        }

        private void OnLobbyRequested()
        {
            ReturnToLobby();
        }

        private void LogChoice(string _choice)
        {
            int day = m_dayManager != null ? m_dayManager.DayNumber : 0;
            EndingLog.Choice(EndingLog.Retire, _choice, day, Time.unscaledTime - m_openedAt, m_firstEnding);
        }
    }
}
