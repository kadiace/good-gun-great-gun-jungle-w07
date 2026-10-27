using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    /// <summary>
    /// 일시정지 메뉴의 메인 메뉴 나가기 확인·중복 실행 정책을 소유하는 일반 C# Presenter다.
    /// 확인창 표시에 실패하면 아무 상태도 남기지 않고, 한 번 나가기로 확정되면 이후 요청·응답을 무시한다.
    /// FeedbackUI 탐색과 수명·로그 문맥은 브리지(PauseMenuNavigation)가 담당한다.
    /// </summary>
    public sealed class PauseExitPresenter
    {
        private const string CONFIRM_MESSAGE = "저장하지 않은 진행 상황은 사라집니다.\n메인 메뉴로 나가시겠습니까?";

        private readonly string m_mainMenuScene;
        private readonly UnityEngine.Object m_logContext;

        private bool m_isLeaving;

        public PauseExitPresenter(string _mainMenuScene, UnityEngine.Object _logContext)
        {
            m_mainMenuScene = _mainMenuScene;
            m_logContext = _logContext;
        }

        /// <summary>
        /// 나가기 버튼 요청을 확인창 표시로 전달한다. 이미 떠나는 중이거나 Feedback이 없으면 아무것도 하지 않고,
        /// Feedback이 다른 확인창으로 바쁠 때는 표시만 실패하며 대기·나가기 상태를 만들지 않는다.
        /// </summary>
        public void RequestExit(object _requester, FeedbackUI _feedback)
        {
            if (m_isLeaving || _feedback == null)
            {
                return;
            }

            _feedback.ShowChoice(_requester, CONFIRM_MESSAGE, ConfirmExit);
        }

        /// <summary>
        /// 이 Presenter가 연 확인창을 취소로 닫는다. 취소·비활성 경로는 확인 행동을 부르지 않는다.
        /// </summary>
        public void CancelConfirmation(object _requester, FeedbackUI _feedback)
        {
            _feedback?.CancelChoice(_requester);
        }

        private void ConfirmExit()
        {
            if (m_isLeaving)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(m_mainMenuScene))
            {
                Debug.LogError($"빌드 목록에 메인 메뉴 씬을 등록하세요: {m_mainMenuScene}", m_logContext);
                return;
            }

            m_isLeaving = true;
            // 타이틀에는 음악이 없고 다음 Main 진입은 세이브 진행도로 다시 계산한다.
            SoundManager.RestoreBeforeRaidMusic(true);
            NightRaidTransfer.Abandon();
            TutorialManager.AbandonTraining();
            GameSaveFiles.PendingLoad = null;
            UIInputPolicy.ApplyLobbyPolicy();
            SceneManager.LoadSceneAsync(m_mainMenuScene);
        }
    }
}
