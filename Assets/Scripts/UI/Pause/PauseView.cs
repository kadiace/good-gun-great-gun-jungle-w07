using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 일시정지 화면(PausePanel)의 UI 직렬화 참조, 표시/숨김 초기화, 명시적 Navigation 구성,
    /// 기본 일시정지 버튼(Resume) 입력의 C# 이벤트 전달만 담당하는 View입니다.
    /// </summary>
    public sealed class PauseView : MonoBehaviour
    {
        private const string SCREEN_NAME = "PauseView";
        public const int DEFAULT_SORTING_ORDER = 200;

        [SerializeField] private GameObject m_pausePanel;
        [SerializeField] private GameObject m_mainPanel;
        [SerializeField] private Canvas m_pauseCanvas;
        [SerializeField] private UnityEngine.UI.GraphicRaycaster m_graphicRaycaster;
        [SerializeField] private UnityEngine.UI.Button m_resumeButton;
        [SerializeField] private UnityEngine.UI.Button[] m_navigationButtons;

        private bool m_isInitialized;
        private bool m_buttonsBound;
        private bool m_hasAppliedInitialHiddenState;
        private bool m_hasLoggedReferenceError;

        public event Action ResumeRequested;

        public bool IsVisible
        {
            get
            {
                return m_pausePanel != null && m_pausePanel.activeSelf;
            }
        }

        public RectTransform RectTransform
        {
            get
            {
                if (m_pausePanel != null)
                {
                    return m_pausePanel.transform as RectTransform;
                }

                return transform as RectTransform;
            }
        }

        public UnityEngine.UI.Selectable DefaultSelectable
        {
            get
            {
                return m_resumeButton;
            }
        }

        public UnityEngine.UI.Button[] NavigationButtons
        {
            get
            {
                return m_navigationButtons;
            }
        }

        private void Awake()
        {
            ApplyInitialHiddenState();
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        /// <summary>
        /// 필수 직렬화 참조를 검증하고 버튼 클릭 리스너·명시적 Navigation을 설정합니다.
        /// 초기 숨김은 Awake/Initialize 중 먼저 실행된 한 번만 적용하며, 중복 호출은 초기화된 상태를 유지합니다.
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

            BindButtons();
            ConfigureNavigation();
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 등록한 Unity UI 버튼 리스너를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            UnbindButtons();
            m_isInitialized = false;
        }

        /// <summary>
        /// 일시정지 루트 패널과 기본 메뉴 패널을 표시하고 Raycast 및 명시적 Navigation을 갱신합니다.
        /// </summary>
        public void Show()
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            if (m_mainPanel != null)
            {
                m_mainPanel.SetActive(true);
            }

            if (m_pauseCanvas != null)
            {
                m_pauseCanvas.overrideSorting = true;
                m_pauseCanvas.sortingOrder = DEFAULT_SORTING_ORDER;
            }

            if (m_graphicRaycaster != null)
            {
                m_graphicRaycaster.enabled = true;
            }

            if (m_pausePanel != null)
            {
                m_pausePanel.SetActive(true);
            }

            ConfigureNavigation();
        }

        /// <summary>
        /// 일시정지 루트 패널을 숨기고 입력 Raycast를 차단합니다.
        /// </summary>
        public void Hide()
        {
            HideImmediate();
        }

        /// <summary>
        /// 실제 일시정지 메뉴 버튼들 사이에 상하 명시적 Navigation을 연결하고 양 끝 순환을 금지합니다.
        /// 필수 Navigation 목록이 비어 있으면 Resume 단독 대체 없이 구성하지 않습니다.
        /// </summary>
        public void ConfigureNavigation()
        {
            List<UnityEngine.UI.Button> activeButtons = new List<UnityEngine.UI.Button>();

            if (m_navigationButtons != null)
            {
                for (int i = 0; i < m_navigationButtons.Length; i++)
                {
                    UnityEngine.UI.Button candidate = m_navigationButtons[i];
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (candidate.gameObject.activeSelf && candidate.isActiveAndEnabled && candidate.interactable)
                    {
                        activeButtons.Add(candidate);
                    }
                    else
                    {
                        UnityEngine.UI.Navigation disabledNav = new UnityEngine.UI.Navigation();
                        disabledNav.mode = UnityEngine.UI.Navigation.Mode.None;
                        disabledNav.wrapAround = false;
                        candidate.navigation = disabledNav;
                    }
                }
            }

            int count = activeButtons.Count;
            for (int i = 0; i < count; i++)
            {
                UnityEngine.UI.Navigation navigation = new UnityEngine.UI.Navigation();
                navigation.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                navigation.wrapAround = false;
                navigation.selectOnUp = i > 0 ? activeButtons[i - 1] : null;
                navigation.selectOnDown = i < count - 1 ? activeButtons[i + 1] : null;
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
                activeButtons[i].navigation = navigation;
            }
        }

        private bool ValidateReferences()
        {
            bool hasNavigationButtons = m_navigationButtons != null && m_navigationButtons.Length > 0;
            int missingNavigationIndex = -1;
            if (hasNavigationButtons)
            {
                for (int i = 0; i < m_navigationButtons.Length; i++)
                {
                    if (m_navigationButtons[i] == null)
                    {
                        missingNavigationIndex = i;
                        break;
                    }
                }
            }

            bool isValid = m_pausePanel != null &&
                           m_mainPanel != null &&
                           m_pauseCanvas != null &&
                           m_graphicRaycaster != null &&
                           m_resumeButton != null &&
                           hasNavigationButtons &&
                           missingNavigationIndex < 0;

            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;

                string navigationState = "invalid";
                if (m_navigationButtons == null)
                {
                    navigationState = "null";
                }
                else if (m_navigationButtons.Length == 0)
                {
                    navigationState = "empty";
                }
                else if (missingNavigationIndex >= 0)
                {
                    navigationState = $"null at index {missingNavigationIndex}";
                }
                else
                {
                    navigationState = m_navigationButtons.Length.ToString();
                }

                Debug.LogError(
                    $"[{SCREEN_NAME}] Initialize 실패: 필수 참조 누락 " +
                    $"(m_pausePanel={m_pausePanel != null}, m_mainPanel={m_mainPanel != null}, " +
                    $"m_pauseCanvas={m_pauseCanvas != null}, m_graphicRaycaster={m_graphicRaycaster != null}, " +
                    $"m_resumeButton={m_resumeButton != null}, m_navigationButtons={navigationState})",
                    this);
            }

            return isValid;
        }

        private void BindButtons()
        {
            if (m_buttonsBound)
            {
                return;
            }

            m_buttonsBound = true;

            if (m_resumeButton != null)
            {
                m_resumeButton.onClick.RemoveListener(OnResumeClicked);
                m_resumeButton.onClick.AddListener(OnResumeClicked);
            }
        }

        private void UnbindButtons()
        {
            if (!m_buttonsBound)
            {
                return;
            }

            m_buttonsBound = false;

            if (m_resumeButton != null)
            {
                m_resumeButton.onClick.RemoveListener(OnResumeClicked);
            }
        }

        private void ApplyInitialHiddenState()
        {
            if (m_hasAppliedInitialHiddenState)
            {
                return;
            }

            m_hasAppliedInitialHiddenState = true;
            HideImmediate();
        }

        private void HideImmediate()
        {
            if (m_graphicRaycaster != null)
            {
                m_graphicRaycaster.enabled = false;
            }

            if (m_mainPanel != null)
            {
                m_mainPanel.SetActive(true);
            }

            if (m_pausePanel != null)
            {
                m_pausePanel.SetActive(false);
            }
        }

        private void OnResumeClicked()
        {
            ResumeRequested?.Invoke();
        }
    }
}
