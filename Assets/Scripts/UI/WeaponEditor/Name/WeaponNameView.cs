using System;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using TMPro;

namespace WeaponWorkshop
{
    /// <summary>
    /// 무기 이름 패널의 저작된 컨트롤 참조, 시작 숨김, 입력·버튼 이벤트 전달을 담당하는 View입니다.
    /// 이름 정리·저장 정책·튜토리얼 승인·대화 상태는 WeaponNamePresenter가 소유합니다.
    /// </summary>
    public sealed class WeaponNameView : MonoBehaviour
    {
        private const string SAVE_LABEL = "저장";

        [SerializeField] private GameObject m_panel;
        [SerializeField] private TMP_InputField m_input;
        [SerializeField] private Button m_confirm;

        private bool m_isInitialized;
        private bool m_isBound;
        private bool m_hasLoggedReferenceError;

        public event Action SaveRequested;
        public event Action<string> NameChanged;
        public event Action<string> InputSelected;
        public event Action<string> InputDeselected;

        public string InputText => m_input != null ? (m_input.text ?? "") : "";

        public bool IsInputFocused => m_input != null && m_input.isFocused;

        private void Awake()
        {
            // Bridge가 먼저 Initialize한 뒤 늦게 실행되는 Awake가 초기 상태를 다시 적용하지 않게 한다.
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
        public void BindReferences(GameObject _panel, TMP_InputField _input, Button _confirm)
        {
            Unbind();
            m_panel = _panel;
            m_input = _input;
            m_confirm = _confirm;
        }

        /// <summary>
        /// 필수 참조를 검증하고 패널 숨김·저장 버튼 비활성·"저장" 라벨과 입력 이벤트를 연결합니다.
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
        /// 입력·버튼 리스너를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            Unbind();
            m_isInitialized = false;
        }

        // 작업대에 있는 동안 계속 보이는 HUD라 기본 선택을 주지 않는다. 입력칸이 선택되면 텍스트 입력 중으로 판정돼 작업대 조작 전체가 막힌다.
        public void SetVisible(bool _visible)
        {
            if (m_panel != null)
            {
                m_panel.SetActive(_visible);
            }
        }

        public void SetName(string _name)
        {
            if (m_input != null)
            {
                m_input.SetTextWithoutNotify(_name ?? "");
            }
        }

        public void SetSaveInteractable(bool _interactable)
        {
            if (m_confirm != null)
            {
                m_confirm.interactable = _interactable;
            }
        }

        public void DeactivateInput()
        {
            if (m_input != null)
            {
                m_input.DeactivateInputField();
            }
        }

        public void ClearSelection()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void ApplyInitialHiddenState()
        {
            if (m_panel != null)
            {
                m_panel.SetActive(false);
            }

            if (m_confirm != null)
            {
                m_confirm.interactable = false;
                TMP_Text label = m_confirm.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = SAVE_LABEL;
                }
            }
        }

        private bool ValidateReferences()
        {
            bool isValid = m_panel != null && m_input != null && m_confirm != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[WeaponNameView] Initialize 실패: 필수 참조 누락 " +
                    $"(m_panel={m_panel != null}, m_input={m_input != null}, m_confirm={m_confirm != null})",
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

            if (m_confirm != null)
            {
                m_confirm.onClick.RemoveListener(OnConfirmClicked);
                m_confirm.onClick.AddListener(OnConfirmClicked);
            }

            if (m_input != null)
            {
                m_input.onValueChanged.RemoveListener(OnInputValueChanged);
                m_input.onValueChanged.AddListener(OnInputValueChanged);
                m_input.onSelect.RemoveListener(OnInputSelected);
                m_input.onSelect.AddListener(OnInputSelected);
                m_input.onDeselect.RemoveListener(OnInputDeselected);
                m_input.onDeselect.AddListener(OnInputDeselected);
            }
        }

        private void Unbind()
        {
            if (!m_isBound)
            {
                return;
            }

            m_isBound = false;

            if (m_confirm != null)
            {
                m_confirm.onClick.RemoveListener(OnConfirmClicked);
            }

            if (m_input != null)
            {
                m_input.onValueChanged.RemoveListener(OnInputValueChanged);
                m_input.onSelect.RemoveListener(OnInputSelected);
                m_input.onDeselect.RemoveListener(OnInputDeselected);
            }
        }

        private void OnConfirmClicked()
        {
            SaveRequested?.Invoke();
        }

        private void OnInputValueChanged(string _value)
        {
            NameChanged?.Invoke(_value);
        }

        private void OnInputSelected(string _value)
        {
            InputSelected?.Invoke(_value);
        }

        private void OnInputDeselected(string _value)
        {
            InputDeselected?.Invoke(_value);
        }
    }
}
