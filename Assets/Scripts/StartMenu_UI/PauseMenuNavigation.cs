using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 일시정지 메뉴의 메인 메뉴 나가기 흐름을 PauseExitPresenter로 전달하는 브리지다.
    /// 직렬화 값과 UIManager.prefab의 persistent 호출(ExitToMainMenu), 수명(비활성 시 확인 취소),
    /// FeedbackUI 탐색·로그 문맥을 유지하며 확인·중복 나가기 정책은 Presenter가 소유한다.
    /// </summary>
    public sealed class PauseMenuNavigation : MonoBehaviour
    {
        [SerializeField] private string mainMenuScene = "Intro";

        private PauseExitPresenter m_presenter;

        private void Awake()
        {
            m_presenter = new PauseExitPresenter(mainMenuScene, this);
        }

        public void ExitToMainMenu()
        {
            m_presenter?.RequestExit(this, FeedbackUI.For(this));
        }

        private void OnDisable()
        {
            m_presenter?.CancelConfirmation(this, FeedbackUI.For(this));
        }
    }
}
