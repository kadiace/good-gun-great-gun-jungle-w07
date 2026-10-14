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
        private void Awake()
        {
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
                objective = step != null ? step.objective : null;
                if (step != null && step.completion == TutorialCompletion.Confirmation)
                {
                    objective = null;
                    for (int i = tutorial.StepIndex - 1; i >= 0; i--)
                    {
                        var previous = tutorial.CurrentSequence.steps[i];
                        if (previous == null || previous.completion == TutorialCompletion.Confirmation ||
                            string.IsNullOrWhiteSpace(previous.objective)) continue;
                        objective = previous.objective;
                        break;
                    }
                }
            }
            else if (NightRaidController.Instance != null && NightRaidController.Instance.IsInRaid) objective = raidObjective;
            else if (dayManager != null)
                objective = dayManager.IsNight ? preparationObjective : dayManager.IsAwaitingOpening ? openingObjective : businessObjective;
            bool visible = !string.IsNullOrWhiteSpace(objective);
            if (panel.activeSelf != visible) panel.SetActive(visible);
            if (visible && description.text != objective) description.text = objective;
        }
    }
}
