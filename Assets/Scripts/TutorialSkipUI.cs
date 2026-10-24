using System;

using UnityEngine;

namespace WeaponWorkshop
{
    // 튜토리얼 건너뛰기 패널의 기존 연결점(브리지). 직렬화 필드·공개 API·씬 오버라이드 호환을 유지한다.
    // 표시는 TutorialSkipView, 분기·날짜 전환·입력 소유는 TutorialSkipPresenter가 담당한다.
    public sealed class TutorialSkipUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Button skipButton, tutorialButton;
        [SerializeField] private TutorialSkipView m_view;

        private TutorialSkipPresenter m_presenter;

        public bool IsOpen
        {
            get
            {
                return m_view != null ? m_view.IsOpen : panel != null && panel.activeSelf;
            }
        }

        public RectTransform TutorialStartButton
        {
            get
            {
                if (m_view != null)
                {
                    return m_view.TutorialStartButton;
                }

                return tutorialButton != null && tutorialButton.interactable
                    ? tutorialButton.transform as RectTransform
                    : null;
            }
        }

        public RectTransform TutorialSkipButton
        {
            get
            {
                if (m_view != null)
                {
                    return m_view.TutorialSkipButton;
                }

                return skipButton != null && skipButton.interactable
                    ? skipButton.transform as RectTransform
                    : null;
            }
        }

        private void Awake()
        {
            if (m_view == null)
            {
                Debug.LogError(
                    "[TutorialSkipUI] Awake 실패: m_view에 TutorialSkipView 참조가 필요합니다. " +
                    "Tools/UI/Authoring/TutorialHud/Author 실행 결과를 확인하세요.",
                    this);
                HidePanel();
                return;
            }

            m_view.Configure(panel, skipButton, tutorialButton);
            m_presenter = new TutorialSkipPresenter(m_view, GetComponent<DayTransitionUI>());
            if (m_presenter.Initialize())
            {
                return;
            }

            m_presenter = null;
        }

        /// <summary>
        /// 건너뛰기 패널을 표시하고 선택 콜백을 보관합니다. 기존 공개 API를 그대로 유지합니다.
        /// </summary>
        public bool Show(Action _onSkip, Action _onTutorial)
        {
            if (!isActiveAndEnabled || m_presenter == null)
            {
                return false;
            }

            return m_presenter.Show(_onSkip, _onTutorial);
        }

        private void OnDisable()
        {
            if (m_presenter != null)
            {
                m_presenter.HandleDisabled();
                return;
            }

            HidePanel();
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

        private void HidePanel()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }
    }
}
