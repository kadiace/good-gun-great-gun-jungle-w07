using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    public sealed class PauseMenuNavigation : MonoBehaviour
    {
        [SerializeField] private string mainMenuScene = "Intro";
        private bool leaving;

        public void ExitToMainMenu()
        {
            if (leaving) return;
            FeedbackUI.For(this)?.ShowChoice(this,
                "저장하지 않은 진행 상황은 사라집니다.\n메인 메뉴로 나가시겠습니까?", ConfirmExit);
        }

        private void OnDisable() => FeedbackUI.For(this)?.CancelChoice(this);

        private void ConfirmExit()
        {
            if (leaving) return;
            if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
            {
                Debug.LogError($"빌드 목록에 메인 메뉴 씬을 등록하세요: {mainMenuScene}", this);
                return;
            }
            leaving = true;
            // 타이틀에는 음악이 없고 다음 Main 진입은 세이브 진행도로 다시 계산한다.
            SoundManager.RestoreBeforeRaidMusic(true);
            NightRaidTransfer.Abandon();
            TutorialManager.AbandonTraining();
            GameSaveFiles.PendingLoad = null;
            Time.timeScale = 1f;
            SceneManager.LoadSceneAsync(mainMenuScene);
        }
    }
}
