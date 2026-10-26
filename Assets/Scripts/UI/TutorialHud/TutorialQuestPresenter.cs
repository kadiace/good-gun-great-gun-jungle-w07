using System;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 튜토리얼 단계·날짜·습격 상태에서 좌상단 목표 문구와 표시 여부를 결정해 TutorialQuestView에 전달하는 Presenter입니다.
    /// 상태를 복제하지 않고 기존 도메인을 매 프레임 조회합니다.
    /// </summary>
    public sealed class TutorialQuestPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "TutorialQuestPresenter";

        private readonly TutorialQuestView m_view;
        private readonly CustomerRequirementsUI m_requirements;
        private readonly _DayManager m_dayManager;
        private readonly string m_openingObjective;
        private readonly string m_businessObjective;
        private readonly string m_preparationObjective;
        private readonly string m_raidObjective;

        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public TutorialQuestPresenter(TutorialQuestView _view, CustomerRequirementsUI _requirements,
            _DayManager _dayManager, string _openingObjective, string _businessObjective,
            string _preparationObjective, string _raidObjective)
        {
            m_view = _view;
            m_requirements = _requirements;
            m_dayManager = _dayManager;
            m_openingObjective = _openingObjective;
            m_businessObjective = _businessObjective;
            m_preparationObjective = _preparationObjective;
            m_raidObjective = _raidObjective;
        }

        public bool IsInitialized
        {
            get
            {
                return m_isInitialized;
            }
        }

        /// <summary>
        /// View를 초기화합니다. 필수 의존성이 없으면 화면만 초기화 실패로 남깁니다.
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
                    Debug.LogError("[" + PRESENTER_NAME + "] Initialize 실패: TutorialQuestView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// View 표시 참조를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.Dispose();
            }

            m_isInitialized = false;
        }

        public void OnEnabled()
        {
            Refresh();
        }

        public void OnDisabled()
        {
            if (m_view != null)
            {
                m_view.SetPanelVisible(false);
            }
        }

        public void Tick()
        {
            Refresh();
        }

        /// <summary>
        /// 기존 Refresh와 같은 순서로 목표 문구를 고르고 표시를 갱신합니다.
        /// </summary>
        private void Refresh()
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            string objective = null;
            TutorialManager tutorial = TutorialManager.Instance;
            if (tutorial != null && tutorial.IsTutorialActive)
            {
                TutorialStep step = tutorial.CurrentStep;
                GameManager game = GameManager.Instance;
                bool choiceOpen = game != null && game.UI != null && UIInputPolicy.IsChoiceOpen;
                if (step != null && step.completion != TutorialCompletion.Confirmation &&
                    (game == null || (!game.InDialogue && !game.IsPaused && !game.InManual &&
                        !game.InTargetSelection && (!choiceOpen || !string.IsNullOrEmpty(step.objectiveWhileChoiceOpen)))))
                {
                    objective = choiceOpen ? step.objectiveWhileChoiceOpen : step.objective;
                }
            }
            else if (OnboardingManager.IsShootingGuideVisible)
            {
                GameManager game = GameManager.Instance;
                if (game != null && game.UI != null && !game.InDialogue && !game.IsPaused && !game.InManual && !UIInputPolicy.IsChoiceOpen)
                    objective = OnboardingManager.ShootingObjective;
            }
            else if (NightRaidController.Instance != null && NightRaidController.Instance.IsInRaid)
            {
                objective = m_raidObjective;
            }
            else if (m_dayManager != null)
            {
                objective = m_dayManager.IsNight
                    ? m_preparationObjective
                    : m_dayManager.IsAwaitingOpening ? m_openingObjective : m_businessObjective;
            }

            bool tutorialObjective = tutorial != null && tutorial.IsTutorialActive || OnboardingManager.IsShootingGuideVisible;
            m_view.SetCompact(tutorialObjective);

            bool hasObjective = !string.IsNullOrWhiteSpace(objective);
            m_view.SetDescriptionVisible(hasObjective);

            bool visible = hasObjective || (m_requirements != null && m_requirements.HasActiveOrder) ||
                HasActiveTagRequirement();
            m_view.SetPanelVisible(visible);

            if (hasObjective)
            {
                m_view.SetObjective(objective);
            }
        }

        /// <summary>
        /// 튜토리얼이 활성화되어 있고 현재 단계에 유효한 태그 요구사항이 설정되어 있는지 확인합니다.
        /// </summary>
        private static bool HasActiveTagRequirement()
        {
            TutorialManager tutorial = TutorialManager.Instance;
            if (tutorial == null || !tutorial.IsTutorialActive)
            {
                return false;
            }

            TutorialStep step = tutorial.CurrentStep;
            if (step == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(step.requestDescription)) return true;
            if (!step.showTagRequirement || string.IsNullOrEmpty(step.requiredTagId)) return false;

            CustomerRequestData table = TableManager.Instance != null ? TableManager.Instance.CustomerRequest : null;
            return table != null && table.FindTag(step.requiredTagId) != null;
        }
    }
}
