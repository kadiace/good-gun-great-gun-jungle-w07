using UnityEngine;

namespace WeaponWorkshop
{
    // 좌상단 튜토리얼 목표 HUD의 기존 연결점(브리지). 직렬화 필드와 씬 오버라이드 호환을 유지한다.
    // 표시는 TutorialQuestView, 목표 선택은 TutorialQuestPresenter가 담당하며 실행 시 원본 참조는 이 컴포넌트가 전달한다.
    public sealed class TutorialQuestUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMPro.TMP_Text description;
        [SerializeField] private CustomerRequirementsUI requirements;
        [SerializeField] private QuestInfoTextSizing textSizing;
        [SerializeField] private _DayManager dayManager;
        [Header("상태별 기본 목표")]
        [SerializeField, TextArea] private string openingObjective = "셔터를 올리고 영업을 시작하세요.";
        [SerializeField, TextArea] private string businessObjective = "손님의 주문을 받고, 요구에 맞는 총을 제작해 판매하세요.";
        [SerializeField, TextArea] private string preparationObjective = "준비를 마치고 야간 습격을 떠나거나 다음 날을 시작하세요.";
        [SerializeField, TextArea] private string raidObjective = "모듈과 재료를 수집하고 탈출 구역으로 퇴각하세요.";
        [SerializeField] private TutorialQuestView m_view;

        private TutorialQuestPresenter m_presenter;

        private void Awake()
        {
            if (dayManager == null)
            {
                dayManager = FindFirstObjectByType<_DayManager>();
            }

            if (m_view == null)
            {
                Debug.LogError(
                    "[TutorialQuestUI] Awake 실패: m_view에 TutorialQuestView 참조가 필요합니다. " +
                    "Tools/UI/Authoring/TutorialHud/Author 실행 결과를 확인하세요.",
                    this);
                if (panel != null)
                {
                    panel.SetActive(false);
                }

                return;
            }

            m_view.Configure(panel, description, textSizing);
            m_presenter = new TutorialQuestPresenter(m_view, requirements, dayManager,
                openingObjective, businessObjective, preparationObjective, raidObjective);
            if (m_presenter.Initialize())
            {
                return;
            }

            m_presenter = null;
        }

        private void OnEnable()
        {
            m_presenter?.OnEnabled();
        }

        private void OnDisable()
        {
            if (m_presenter != null)
            {
                m_presenter.OnDisabled();
                return;
            }

            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            m_presenter?.Tick();
        }

        private void OnDestroy()
        {
            if (m_presenter == null)
            {
                return;
            }

            m_presenter.Dispose();
            m_presenter = null;
        }
    }
}
