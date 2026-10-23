using System;

using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 은퇴 화면(추출된 Retire 프리팹)의 직렬화 참조, 시작 숨김, 계속·로비 버튼 입력 전달,
    /// 표시 문구와 기본 선택만 담당하는 View입니다. 은퇴 선저장·결제·로그·씬 이동은 Presenter가 소유합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetireView : MonoBehaviour
    {
        private const string SCREEN_NAME = "RetireView";

        [SerializeField] private GameObject m_panel;
        [SerializeField] private TMP_Text m_continueText;
        [SerializeField] private Button m_lobbyButton;
        [SerializeField] private Button m_continueButton;

        private bool m_isInitialized;
        private bool m_isBound;
        private GameObject m_savedFocus;
        private bool m_hasAppliedInitialHiddenState;
        private bool m_hasLoggedReferenceError;

        public event Action ContinueRequested;
        public event Action LobbyRequested;

        public bool IsPanelSelfActive
        {
            get
            {
                return m_panel != null && m_panel.activeSelf;
            }
        }

        private void Awake()
        {
            // 브리지가 먼저 Initialize한 뒤 늦게 실행되는 Awake가 초기 상태를 다시 적용하지 않게 한다.
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
        /// 브리지가 보유한 실효 참조(프리팹 값과 씬 오버라이드 반영)를 View 직렬화 참조로 전달합니다.
        /// Initialize 전에 한 번 호출해야 합니다.
        /// </summary>
        public void Configure(GameObject _panel, TMP_Text _continueText, Button _lobbyButton, Button _continueButton)
        {
            Unbind();
            m_panel = _panel;
            m_continueText = _continueText;
            m_lobbyButton = _lobbyButton;
            m_continueButton = _continueButton;
            // 프리팹 참조가 비어 있던 채로 Awake가 먼저 실행됐더라도 실효 참조로 시작 상태를 다시 적용한다.
            m_hasAppliedInitialHiddenState = false;
        }

        /// <summary>
        /// 시작 숨김을 적용하고 필수 참조를 검증한 뒤 버튼 리스너를 연결합니다. 중복 호출은 초기화된 상태를 유지합니다.
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
        /// 버튼 리스너를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            Unbind();
            m_isInitialized = false;
        }

        public void Show(string _continueLabel)
        {
            if (m_continueText != null)
            {
                m_continueText.text = _continueLabel ?? "";
            }

            if (m_panel == null)
            {
                return;
            }

            UIRoot.SaveFocus(ref m_savedFocus, transform);
            m_panel.SetActive(true);
            m_panel.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (m_panel != null)
            {
                m_panel.SetActive(false);
            }

            UIRoot.RestoreFocus(ref m_savedFocus);
        }

        /// <summary>
        /// 화면을 열 때 계속 버튼을 기본 선택으로 지정합니다.
        /// </summary>
        public void FocusDefault()
        {
            UIRoot.SetFocus(m_continueButton, m_lobbyButton != null ? new Selectable[] { m_lobbyButton } : null);
        }

        public void ClearSelection()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        private void ApplyInitialHiddenState()
        {
            if (m_hasAppliedInitialHiddenState)
            {
                return;
            }

            m_hasAppliedInitialHiddenState = true;
            Hide();
        }

        private bool ValidateReferences()
        {
            bool isValid = m_panel != null &&
                           m_continueText != null &&
                           m_lobbyButton != null &&
                           m_continueButton != null;

            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[" + SCREEN_NAME + "] Initialize 실패: 필수 참조 누락 " +
                    $"(m_panel={m_panel != null}, m_continueText={m_continueText != null}, " +
                    $"m_lobbyButton={m_lobbyButton != null}, m_continueButton={m_continueButton != null})",
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
            m_lobbyButton.onClick.RemoveListener(OnLobbyClicked);
            m_lobbyButton.onClick.AddListener(OnLobbyClicked);
            m_continueButton.onClick.RemoveListener(OnContinueClicked);
            m_continueButton.onClick.AddListener(OnContinueClicked);
        }

        private void Unbind()
        {
            if (!m_isBound)
            {
                return;
            }

            m_isBound = false;
            if (m_lobbyButton != null)
            {
                m_lobbyButton.onClick.RemoveListener(OnLobbyClicked);
            }

            if (m_continueButton != null)
            {
                m_continueButton.onClick.RemoveListener(OnContinueClicked);
            }
        }

        private void OnLobbyClicked()
        {
            LobbyRequested?.Invoke();
        }

        private void OnContinueClicked()
        {
            ContinueRequested?.Invoke();
        }

        private static void Focus(Selectable _selectable)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || _selectable == null || !_selectable.isActiveAndEnabled ||
                !_selectable.IsInteractable())
            {
                return;
            }

            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(_selectable.gameObject);
        }
    }
}
