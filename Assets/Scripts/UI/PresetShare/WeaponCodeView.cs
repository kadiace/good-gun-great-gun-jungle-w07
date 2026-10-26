using System;

using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 공유 코드 패널과 열기 버튼의 저작된 참조, 시작 숨김, 버튼 입력 이벤트,
    /// 코드 표시와 포커스 복원을 담당하는 View입니다. 코드 내보내기·가져오기 판단은 Presenter가 맡습니다.
    /// </summary>
    public sealed class WeaponCodeView : MonoBehaviour
    {
        [SerializeField] private GameObject m_panel;
        [SerializeField] private Button m_openButton;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private Button m_copyButton;
        [SerializeField] private Button m_importButton;
        [SerializeField] private TMP_InputField m_codeInput;

        private GameObject m_restoredFocus;
        private bool m_isInitialized;
        private bool m_isListenerBound;
        private bool m_hasLoggedReferenceError;

        public event Action OpenRequested;
        public event Action CloseRequested;
        public event Action CopyRequested;
        public event Action ImportRequested;

        public bool IsOpen
        {
            get
            {
                return m_panel != null && m_panel.activeSelf;
            }
        }

        public string Code
        {
            get
            {
                return m_codeInput != null ? m_codeInput.text ?? string.Empty : string.Empty;
            }
        }

        private void Awake()
        {
            // 브리지가 먼저 Initialize한 경우 늦게 실행되는 Awake가 초기 상태를 다시 덮지 않게 한다.
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
        /// 씬 오버라이드가 적용된 브리지의 유효 참조를 Initialize 전에 주입합니다.
        /// </summary>
        public void BindReferences(GameObject _panel, Button _openButton, Button _closeButton,
            Button _copyButton, Button _importButton, TMP_InputField _codeInput)
        {
            Unbind();
            m_panel = _panel;
            m_openButton = _openButton;
            m_closeButton = _closeButton;
            m_copyButton = _copyButton;
            m_importButton = _importButton;
            m_codeInput = _codeInput;
        }

        /// <summary>
        /// 시작 숨김 상태와 열기·닫기·복사·가져오기 버튼 이벤트를 연결합니다. 중복 호출에 안전합니다.
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

        public void SetOpenButtonVisible(bool _visible)
        {
            if (m_openButton != null)
            {
                m_openButton.gameObject.SetActive(_visible);
            }
        }

        public void ShowPanel()
        {
            if (m_panel == null)
            {
                return;
            }

            UIRoot.SaveFocus(ref m_restoredFocus, m_panel.transform);
            m_panel.SetActive(true);
            UIScreenRoot.BringToFront(m_panel.transform);
            UIRoot.SetFocus(m_copyButton, m_importButton != null ? new Selectable[] { m_importButton, m_closeButton } : null);
        }

        public void HidePanel()
        {
            if (m_panel != null)
            {
                m_panel.SetActive(false);
            }

            Selectable fallback = m_openButton != null ? (Selectable)m_openButton : null;
            UIRoot.RestoreFocus(ref m_restoredFocus, fallback);
        }

        public void SetCode(string _code)
        {
            if (m_codeInput != null)
            {
                m_codeInput.SetTextWithoutNotify(_code ?? string.Empty);
            }
        }

        public void HideAll()
        {
            SetOpenButtonVisible(false);
            HidePanel();
        }

        private void ApplyInitialHiddenState()
        {
            if (m_panel != null)
            {
                m_panel.SetActive(false);
            }

            if (m_openButton != null)
            {
                m_openButton.gameObject.SetActive(false);
            }

            m_restoredFocus = null;
        }

        private bool ValidateReferences()
        {
            bool isValid = m_panel != null && m_openButton != null && m_closeButton != null &&
                m_copyButton != null && m_importButton != null && m_codeInput != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[WeaponCodeView] Initialize 실패: 필수 참조 누락 " +
                    $"(panel={m_panel != null}, openButton={m_openButton != null}, " +
                    $"closeButton={m_closeButton != null}, copyButton={m_copyButton != null}, " +
                    $"importButton={m_importButton != null}, codeInput={m_codeInput != null}). " +
                    "Tools/UI/Authoring/PresetShare/Author 실행 결과를 확인하세요.",
                    this);
            }

            return isValid;
        }

        private void Bind()
        {
            if (m_isListenerBound)
            {
                return;
            }

            m_isListenerBound = true;
            m_openButton?.onClick.AddListener(OnOpenClicked);
            m_closeButton?.onClick.AddListener(OnCloseClicked);
            m_copyButton?.onClick.AddListener(OnCopyClicked);
            m_importButton?.onClick.AddListener(OnImportClicked);
        }

        private void Unbind()
        {
            if (!m_isListenerBound)
            {
                return;
            }

            m_isListenerBound = false;
            m_openButton?.onClick.RemoveListener(OnOpenClicked);
            m_closeButton?.onClick.RemoveListener(OnCloseClicked);
            m_copyButton?.onClick.RemoveListener(OnCopyClicked);
            m_importButton?.onClick.RemoveListener(OnImportClicked);
        }

        private void OnOpenClicked()
        {
            OpenRequested?.Invoke();
        }

        private void OnCloseClicked()
        {
            CloseRequested?.Invoke();
        }

        private void OnCopyClicked()
        {
            CopyRequested?.Invoke();
        }

        private void OnImportClicked()
        {
            ImportRequested?.Invoke();
        }
    }
}
