using System;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    /// <summary>
    /// 엔드 상자 엔딩의 알파 전달·버튼 노출 시각 기록·엔딩 로그와 계속/Intro 선택의 기존 정리 경로를 담당하는 Presenter입니다.
    /// 상자 상태 판정은 EndChest가 원본이며, 이 클래스는 표시와 씬 이동만 요청합니다.
    /// </summary>
    public sealed class EndChestEndingPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "EndChestEndingPresenter";

        private readonly EndChestEndingView m_view;
        private readonly EndChest m_chest;
        private readonly string m_mainMenuScene;

        private bool m_leaving;
        // 버튼이 눌리기 시작한 시각(실시간). 엔딩 로그의 panelSeconds용.
        private float m_buttonsShownAt = -1f;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public EndChestEndingPresenter(EndChestEndingView _view, EndChest _chest, string _mainMenuScene)
        {
            m_view = _view;
            m_chest = _chest;
            m_mainMenuScene = string.IsNullOrEmpty(_mainMenuScene) ? "Intro" : _mainMenuScene;
        }

        /// <summary>
        /// View를 초기화하고 계속·그만하기 입력을 구독합니다. 중복 호출에 안전합니다.
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
                    Debug.LogError("[" + PRESENTER_NAME + "] Initialize 실패: EndChestEndingView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.ContinueRequested -= OnContinueRequested;
            m_view.ContinueRequested += OnContinueRequested;
            m_view.QuitRequested -= OnQuitRequested;
            m_view.QuitRequested += OnQuitRequested;

            m_leaving = false;
            m_buttonsShownAt = -1f;
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
                m_view.QuitRequested -= OnQuitRequested;
                m_view.Dispose();
            }

            m_isInitialized = false;
        }

        public void SetWhite(float _alpha)
        {
            if (!EnsureInitialized())
            {
                return;
            }

            m_view.SetWhite(_alpha);
        }

        /// <summary>
        /// END 글자·버튼 알파를 전달하고, 버튼이 완전히 나타난 첫 순간을 기록하며 기본 선택을 지정합니다.
        /// </summary>
        public void SetEnd(float _alpha)
        {
            if (!EnsureInitialized())
            {
                return;
            }

            float alpha = Mathf.Clamp01(_alpha);
            m_view.SetEnd(alpha);

            if (alpha >= 1f && m_buttonsShownAt < 0f)
            {
                m_buttonsShownAt = Time.unscaledTime;
                m_view.FocusDefault();
            }
            else if (alpha <= 0f)
            {
                m_buttonsShownAt = -1f;
            }
        }

        private bool EnsureInitialized()
        {
            return m_isInitialized || Initialize();
        }

        private void OnContinueRequested()
        {
            Continue();
        }

        private void OnQuitRequested()
        {
            ExitToMainMenu();
        }

        private void LogChoice(string _choice)
        {
            NightRaidController raid = NightRaidController.Instance;
            GameSaveData workshop = NightRaidTransfer.Workshop;
            EndingLog.Choice(EndingLog.EndChest, _choice, workshop != null ? workshop.day : -1,
                m_buttonsShownAt >= 0f ? Time.unscaledTime - m_buttonsShownAt : 0f,
                raid != null && raid.EndChestFirstEnding);
        }

        private void Continue()
        {
            if (m_leaving || m_chest == null || !m_chest.AwaitingChoice)
            {
                return;
            }

            LogChoice("continue");
            m_chest.Continue();
            m_view.ClearSelection();
        }

        // 일시정지 메뉴의 '메인 메뉴로 나가기'와 같은 정리(확인 창만 생략).
        private void ExitToMainMenu()
        {
            if (m_leaving)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(m_mainMenuScene))
            {
                Debug.LogError($"빌드 목록에 메인 메뉴 씬을 등록하세요: {m_mainMenuScene}", m_view);
                return;
            }

            m_leaving = true;
            LogChoice("lobby");
            NightRaidController.Instance?.NotifyEndingQuit();
            NightRaidTransfer.Abandon();
            GameSaveFiles.PendingLoad = null;
            UIInputPolicy.ApplyLobbyPolicy();
            SceneManager.LoadSceneAsync(m_mainMenuScene);
        }
    }
}
