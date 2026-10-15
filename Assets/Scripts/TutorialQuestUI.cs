using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class TutorialQuestUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMPro.TMP_Text description;
        [SerializeField] private _DayManager dayManager;
        [Header("상태별 기본 목표")]
        [SerializeField, TextArea] private string openingObjective = "셔터를 올리고 영업을 시작하세요.";
        [SerializeField, TextArea] private string businessObjective = "손님의 주문을 받고, 요구에 맞는 총을 제작해 판매하세요.";
        [SerializeField, TextArea] private string preparationObjective = "준비를 마치고 야간 습격을 떠나거나 다음 날을 시작하세요.";
        [SerializeField, TextArea] private string raidObjective = "모듈과 재료를 수집하고 탈출 구역으로 퇴각하세요.";
        private RectTransform panelRect;
        private Vector2 originalAnchorMin;
        private Vector2 originalAnchorMax;
        private Vector2 originalPosition;
        private Vector2 originalSize;
        private Vector2 originalPivot;
        private Vector3 originalScale;
        private bool compact;
        private void Awake()
        {
            panelRect = panel.GetComponent<RectTransform>();
            originalAnchorMin = panelRect.anchorMin;
            originalAnchorMax = panelRect.anchorMax;
            originalPosition = panelRect.anchoredPosition;
            originalSize = panelRect.sizeDelta;
            originalPivot = panelRect.pivot;
            originalScale = panelRect.localScale;
            panel.SetActive(false);
            if (dayManager == null) dayManager = FindFirstObjectByType<_DayManager>();
        }
        private void OnEnable() => Refresh();
        private void OnDisable() => panel.SetActive(false);
        private void LateUpdate() => Refresh();
        private void Refresh()
        {
            string objective = null;
            var tutorial = TutorialManager.Instance;
            if (tutorial != null && tutorial.IsTutorialActive)
            {
                var step = tutorial.CurrentStep;
                var game = GameManager.Instance;
                if (step != null && step.completion != TutorialCompletion.Confirmation &&
                    (game == null || (!game.InDialogue && !game.IsPaused && !game.InManual &&
                        !game.InTargetSelection && (game.UI == null || !game.UI.IsChoiceOpen))))
                    objective = step.objective;
            }
            else if (NightRaidController.Instance != null && NightRaidController.Instance.IsInRaid) objective = raidObjective;
            else if (dayManager != null)
                objective = dayManager.IsNight ? preparationObjective : dayManager.IsAwaitingOpening ? openingObjective : businessObjective;
            bool tutorialObjective = tutorial != null && tutorial.IsTutorialActive;
            if (compact != tutorialObjective)
            {
                compact = tutorialObjective;
                panelRect.anchorMin = compact ? new Vector2(0f, .5f) : originalAnchorMin;
                panelRect.anchorMax = compact ? new Vector2(0f, .5f) : originalAnchorMax;
                panelRect.pivot = compact ? new Vector2(0f, .5f) : originalPivot;
                panelRect.anchoredPosition = compact ? new Vector2(54f, 0f) : originalPosition;
                panelRect.sizeDelta = compact ? new Vector2(300f, originalSize.y) : originalSize;
                panelRect.localScale = compact ? originalScale * .85f : originalScale;
            }
            bool visible = !string.IsNullOrWhiteSpace(objective);
            if (panel.activeSelf != visible) panel.SetActive(visible);
            if (visible && description.text != objective) description.text = objective;
        }
    }
}
