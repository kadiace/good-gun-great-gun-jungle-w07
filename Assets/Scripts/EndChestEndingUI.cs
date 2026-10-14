using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 엔드 상자 연출용 전체 화면 UI: 하얀 화면, END 글자, 계속하기/그만하기 버튼. EndChest가 알파를 구동한다.
    [DisallowMultipleComponent]
    public sealed class EndChestEndingUI : MonoBehaviour
    {
        [SerializeField] private EndChest chest;
        [SerializeField] private CanvasGroup whiteGroup;
        [SerializeField] private CanvasGroup endGroup;
        [SerializeField] private CanvasGroup buttonsGroup;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private string mainMenuScene = "Start_Scene";
        private bool leaving;

        // 씬에 저장된 활성 상태와 무관하게 전부 숨긴 상태로 시작한다.
        private void Awake()
        {
            leaving = false;
            SetWhite(0f); SetEnd(0f);
            if (continueButton != null) continueButton.onClick.AddListener(Continue);
            if (quitButton != null) quitButton.onClick.AddListener(ExitToMainMenu);
        }

        private void OnDestroy()
        {
            if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
            if (quitButton != null) quitButton.onClick.RemoveListener(ExitToMainMenu);
        }

        public void SetWhite(float alpha) { if (whiteGroup != null) whiteGroup.alpha = Mathf.Clamp01(alpha); }

        // END 글자가 완전히 나타난 뒤에만 버튼이 눌린다.
        public void SetEnd(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (endGroup != null) endGroup.alpha = alpha;
            if (buttonsGroup == null) return;
            buttonsGroup.alpha = alpha;
            buttonsGroup.interactable = buttonsGroup.blocksRaycasts = alpha >= 1f;
        }

        private void Continue()
        {
            if (!leaving && chest != null) chest.Continue();
        }

        // 일시정지 메뉴의 '메인 메뉴로 나가기'와 같은 정리(확인 창만 생략).
        private void ExitToMainMenu()
        {
            if (leaving) return;
            if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
            {
                Debug.LogError($"빌드 목록에 메인 메뉴 씬을 등록하세요: {mainMenuScene}", this);
                return;
            }
            leaving = true;
            NightRaidTransfer.Abandon();
            GameSaveFiles.PendingLoad = null;
            Time.timeScale = 1f;
            SceneManager.LoadSceneAsync(mainMenuScene);
        }
    }
}
