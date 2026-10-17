using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class TutorialQuestUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMPro.TMP_Text description;
        [SerializeField] private CustomerRequirementsUI requirements;
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
            description.fontSize = 20f;
            description.enableAutoSizing = false;
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
                bool choiceOpen = game != null && game.UI != null && game.UI.IsChoiceOpen;
                if (step != null && step.completion != TutorialCompletion.Confirmation &&
                    (game == null || (!game.InDialogue && !game.IsPaused && !game.InManual &&
                        !game.InTargetSelection && (!choiceOpen || !string.IsNullOrEmpty(step.objectiveWhileChoiceOpen)))))
                    objective = choiceOpen ? step.objectiveWhileChoiceOpen : step.objective;
            }
            else if (NightRaidController.Instance != null && NightRaidController.Instance.IsInRaid) objective = raidObjective;
            else if (dayManager != null)
                objective = dayManager.IsNight ? preparationObjective : dayManager.IsAwaitingOpening ? openingObjective : businessObjective;
            bool tutorialObjective = tutorial != null && tutorial.IsTutorialActive;
            if (compact != tutorialObjective)
            {
                compact = tutorialObjective;
                panelRect.anchorMin = originalAnchorMin;
                panelRect.anchorMax = originalAnchorMax;
                panelRect.pivot = originalPivot;
                panelRect.anchoredPosition = originalPosition;
                panelRect.sizeDelta = compact ? new Vector2(300f, originalSize.y) : originalSize;
                panelRect.localScale = originalScale;
            }
            bool hasObjective = !string.IsNullOrWhiteSpace(objective);
            if (description.gameObject.activeSelf != hasObjective) description.gameObject.SetActive(hasObjective);
            bool visible = hasObjective || (requirements != null && requirements.HasActiveOrder);
            if (panel.activeSelf != visible) panel.SetActive(visible);
            if (hasObjective && description.text != objective) description.text = objective;
        }
    }
}
