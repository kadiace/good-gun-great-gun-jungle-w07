using System;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 레거시 습격 사망 패널의 직렬화 참조, 시작 숨김, 귀환 버튼 입력 전달과 기본 선택만 담당하는 View입니다.
    /// 사망 원인 판정·컨트롤러 전달·게임 상태 전환은 Presenter가 소유합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaidDeathView : MonoBehaviour
    {
        private const string SCREEN_NAME = "RaidDeathView";

        [SerializeField] private GameObject m_panel;
        [SerializeField] private Button m_returnButton;

        private bool m_isInitialized;
        private bool m_isBound;
        private bool m_hasAppliedInitialHiddenState;
        private bool m_hasLoggedReferenceError;

        public event Action ReturnRequested;

        private void Awake()
        {
            // 브리지가 먼저 Initialize한 뒤 늦게 실행되는 Awake가 시작 상태를 다시 적용하지 않게 한다.
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
        /// 브리지가 보유한 실효 참조를 View 직렬화 참조로 전달합니다. Initialize 전에 한 번 호출해야 합니다.
        /// </summary>
        public void Configure(GameObject _panel, Button _returnButton)
        {
            Unbind();
            m_panel = _panel;
            m_returnButton = _returnButton;
            // 프리팹 참조가 비어 있던 채로 Awake가 먼저 실행됐더라도 실효 참조로 시작 상태를 다시 적용한다.
            m_hasAppliedInitialHiddenState = false;
        }

        /// <summary>
        /// 패널을 숨긴 시작 상태로 만들고 필수 참조를 검증한 뒤 귀환 버튼 리스너를 연결합니다.
        /// 중복 호출은 초기화된 상태를 유지합니다.
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

        /// <summary>
        /// 레거시 사망 패널을 맨 앞에 표시하고 귀환 버튼을 기본 선택으로 지정합니다.
        /// </summary>
        public void Show()
        {
            if (m_panel == null)
            {
                return;
            }

            m_panel.SetActive(true);
            m_panel.transform.SetAsLastSibling();
            if (m_returnButton != null)
            {
                m_returnButton.interactable = true;
            }

            FocusDefault();
        }

        public void Hide()
        {
            if (m_panel != null)
            {
                m_panel.SetActive(false);
            }
        }

        /// <summary>
        /// 패널을 열 때 귀환 버튼을 기본 선택으로 지정합니다.
        /// </summary>
        public void FocusDefault()
        {
            Focus(m_returnButton);
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
            bool isValid = m_panel != null && m_returnButton != null;

            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[" + SCREEN_NAME + "] Initialize 실패: 필수 참조 누락 " +
                    $"(m_panel={m_panel != null}, m_returnButton={m_returnButton != null})",
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
            m_returnButton.onClick.RemoveListener(OnReturnClicked);
            m_returnButton.onClick.AddListener(OnReturnClicked);
        }

        private void Unbind()
        {
            if (!m_isBound)
            {
                return;
            }

            m_isBound = false;
            if (m_returnButton != null)
            {
                m_returnButton.onClick.RemoveListener(OnReturnClicked);
            }
        }

        private void OnReturnClicked()
        {
            ReturnRequested?.Invoke();
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
