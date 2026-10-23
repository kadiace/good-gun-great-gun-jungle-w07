using System;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 엔드 상자 엔딩 화면(추출된 EndChestEnding 프리팹)의 CanvasGroup 알파 표시, 버튼 입력 전달,
    /// 시작 상태 초기화와 기본 선택만 담당하는 View입니다. 엔딩 로그·씬 이동은 Presenter가 소유합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndChestEndingView : MonoBehaviour
    {
        private const string SCREEN_NAME = "EndChestEndingView";

        [SerializeField] private CanvasGroup m_whiteGroup;
        [SerializeField] private CanvasGroup m_endGroup;
        [SerializeField] private CanvasGroup m_buttonsGroup;
        [SerializeField] private Button m_continueButton;
        [SerializeField] private Button m_quitButton;

        private bool m_isInitialized;
        private bool m_isBound;
        private bool m_hasAppliedInitialState;
        private bool m_hasLoggedReferenceError;

        public event Action ContinueRequested;
        public event Action QuitRequested;

        private void Awake()
        {
            // 브리지가 먼저 Initialize한 뒤 늦게 실행되는 Awake가 시작 상태를 다시 적용하지 않게 한다.
            if (!m_isInitialized)
            {
                ApplyInitialState();
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// 브리지가 보유한 실효 참조를 View 직렬화 참조로 전달합니다. Initialize 전에 한 번 호출해야 합니다.
        /// </summary>
        public void Configure(CanvasGroup _whiteGroup, CanvasGroup _endGroup, CanvasGroup _buttonsGroup,
            Button _continueButton, Button _quitButton)
        {
            Unbind();
            m_whiteGroup = _whiteGroup;
            m_endGroup = _endGroup;
            m_buttonsGroup = _buttonsGroup;
            m_continueButton = _continueButton;
            m_quitButton = _quitButton;
            // 프리팹 참조가 비어 있던 채로 Awake가 먼저 실행됐더라도 실효 참조로 시작 상태를 다시 적용한다.
            m_hasAppliedInitialState = false;
        }

        /// <summary>
        /// 흰 화면·END·버튼을 모두 숨긴 시작 상태로 만들고 필수 참조를 검증한 뒤 버튼 리스너를 연결합니다.
        /// 중복 호출은 초기화된 상태를 유지합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            ApplyInitialState();
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

        public void SetWhite(float _alpha)
        {
            if (m_whiteGroup != null)
            {
                m_whiteGroup.alpha = Mathf.Clamp01(_alpha);
            }
        }

        /// <summary>
        /// END 글자와 버튼 그룹의 알파를 함께 갱신합니다. 버튼은 완전히 나타난 뒤에만 눌립니다.
        /// </summary>
        public void SetEnd(float _alpha)
        {
            float alpha = Mathf.Clamp01(_alpha);
            if (m_endGroup != null)
            {
                m_endGroup.alpha = alpha;
            }

            if (m_buttonsGroup == null)
            {
                return;
            }

            m_buttonsGroup.alpha = alpha;
            m_buttonsGroup.interactable = alpha >= 1f;
            m_buttonsGroup.blocksRaycasts = alpha >= 1f;
        }

        /// <summary>
        /// 버튼이 활성화됐을 때 계속 버튼을 기본 선택으로 지정합니다.
        /// </summary>
        public void FocusDefault()
        {
            Focus(m_continueButton);
        }

        public void ClearSelection()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        private void ApplyInitialState()
        {
            if (m_hasAppliedInitialState)
            {
                return;
            }

            m_hasAppliedInitialState = true;
            SetWhite(0f);
            SetEnd(0f);
        }

        private bool ValidateReferences()
        {
            bool isValid = m_whiteGroup != null &&
                           m_endGroup != null &&
                           m_buttonsGroup != null &&
                           m_continueButton != null &&
                           m_quitButton != null;

            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[" + SCREEN_NAME + "] Initialize 실패: 필수 참조 누락 " +
                    $"(m_whiteGroup={m_whiteGroup != null}, m_endGroup={m_endGroup != null}, " +
                    $"m_buttonsGroup={m_buttonsGroup != null}, m_continueButton={m_continueButton != null}, " +
                    $"m_quitButton={m_quitButton != null})",
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
            m_continueButton.onClick.RemoveListener(OnContinueClicked);
            m_continueButton.onClick.AddListener(OnContinueClicked);
            m_quitButton.onClick.RemoveListener(OnQuitClicked);
            m_quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void Unbind()
        {
            if (!m_isBound)
            {
                return;
            }

            m_isBound = false;
            if (m_continueButton != null)
            {
                m_continueButton.onClick.RemoveListener(OnContinueClicked);
            }

            if (m_quitButton != null)
            {
                m_quitButton.onClick.RemoveListener(OnQuitClicked);
            }
        }

        private void OnContinueClicked()
        {
            ContinueRequested?.Invoke();
        }

        private void OnQuitClicked()
        {
            QuitRequested?.Invoke();
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
