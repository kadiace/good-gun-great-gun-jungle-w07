using System;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 튜토리얼 건너뛰기 패널의 저작된 참조, 시작 숨김, 버튼 입력 이벤트와 표시 상태만 담당하는 View입니다.
    /// 건너뛰기·시작 분기와 대화 입력 소유는 TutorialSkipPresenter가 맡습니다.
    /// </summary>
    public sealed class TutorialSkipView : MonoBehaviour
    {
        private const string VIEW_NAME = "TutorialSkipView";

        private GameObject m_panel;
        private Button m_skipButton;
        private Button m_tutorialButton;

        private bool m_isInitialized;
        private bool m_isBound;
        private GameObject m_savedFocus;
        private bool m_hasAppliedInitialState;
        private bool m_hasLoggedReferenceError;

        public event Action SkipRequested;
        public event Action TutorialRequested;

        public bool IsInitialized
        {
            get
            {
                return m_isInitialized;
            }
        }

        public bool IsOpen
        {
            get
            {
                return m_panel != null && m_panel.activeSelf;
            }
        }

        public RectTransform TutorialStartButton
        {
            get
            {
                return m_tutorialButton != null && m_tutorialButton.interactable
                    ? m_tutorialButton.transform as RectTransform
                    : null;
            }
        }

        public RectTransform TutorialSkipButton
        {
            get
            {
                return m_skipButton != null && m_skipButton.interactable
                    ? m_skipButton.transform as RectTransform
                    : null;
            }
        }

        private void Awake()
        {
            if (!m_isInitialized)
            {
                ApplyInitialHiddenState();
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// 브리지가 보유한 실효 참조(프리팹 값과 씬 오버라이드 반영)를 Initialize 전에 전달합니다.
        /// </summary>
        public void Configure(GameObject _panel, Button _skipButton, Button _tutorialButton)
        {
            Unbind();
            m_panel = _panel;
            m_skipButton = _skipButton;
            m_tutorialButton = _tutorialButton;
            ApplyInitialHiddenState();
        }

        /// <summary>
        /// 필수 참조를 검증하고 시작 숨김과 버튼 리스너를 적용합니다. 중복 호출에 안전합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            ApplyInitialHiddenState();
            if (!ValidateReferences())
            {
                return false;
            }

            Bind();
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 버튼 리스너와 표시 참조를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            Unbind();
            m_isInitialized = false;
        }

        /// <summary>
        /// 패널을 표시하고 입력 레이어 맨 위로 올립니다.
        /// </summary>
        public void ShowPanel()
        {
            if (m_panel == null)
            {
                return;
            }

            UIRoot.SaveFocus(ref m_savedFocus, transform);
            m_panel.SetActive(true);
            m_panel.transform.SetAsLastSibling();
            UIRoot.SetFocus(m_tutorialButton, m_skipButton != null ? new Selectable[] { m_skipButton } : null);
        }

        public void HidePanel()
        {
            if (m_panel != null)
            {
                m_panel.SetActive(false);
            }

            UIRoot.RestoreFocus(ref m_savedFocus);
        }

        public void SetInteractable(bool _interactable)
        {
            if (m_skipButton != null)
            {
                m_skipButton.interactable = _interactable;
            }

            if (m_tutorialButton != null)
            {
                m_tutorialButton.interactable = _interactable;
            }
        }

        private void ApplyInitialHiddenState()
        {
            if (m_hasAppliedInitialState || m_panel == null)
            {
                return;
            }

            m_hasAppliedInitialState = true;
            m_panel.SetActive(false);
        }

        private bool ValidateReferences()
        {
            bool isValid = m_panel != null && m_skipButton != null && m_tutorialButton != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[" + VIEW_NAME + "] Initialize 실패: 필수 참조 누락 " +
                    "(m_panel=" + (m_panel != null) + ", m_skipButton=" + (m_skipButton != null) +
                    ", m_tutorialButton=" + (m_tutorialButton != null) + ")",
                    this);
            }

            return isValid;
        }

        private void Bind()
        {
            if (m_isBound)
            {
                return;
            }

            m_isBound = true;
            m_skipButton.onClick.RemoveListener(OnSkipClicked);
            m_skipButton.onClick.AddListener(OnSkipClicked);
            m_tutorialButton.onClick.RemoveListener(OnTutorialClicked);
            m_tutorialButton.onClick.AddListener(OnTutorialClicked);
        }

        private void Unbind()
        {
            if (!m_isBound)
            {
                return;
            }

            m_isBound = false;
            if (m_skipButton != null)
            {
                m_skipButton.onClick.RemoveListener(OnSkipClicked);
            }

            if (m_tutorialButton != null)
            {
                m_tutorialButton.onClick.RemoveListener(OnTutorialClicked);
            }
        }

        private void OnSkipClicked()
        {
            SkipRequested?.Invoke();
        }

        private void OnTutorialClicked()
        {
            TutorialRequested?.Invoke();
        }
    }
}
