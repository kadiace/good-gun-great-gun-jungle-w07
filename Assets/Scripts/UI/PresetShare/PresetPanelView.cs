using System;

using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 프리셋 패널과 저장·불러오기 선택창의 저작된 참조, 시작 숨김, 버튼 입력 이벤트,
    /// 선택창 표시와 포커스 복원을 담당하는 View입니다. 저장소·지갑 같은 도메인은 Presenter가 다룹니다.
    /// </summary>
    public sealed class PresetPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject m_panel;
        [SerializeField] private GameObject m_choicePanel;
        [SerializeField] private Button[] m_slots;
        [SerializeField] private Button m_saveButton;
        [SerializeField] private Button m_loadButton;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TMP_Text m_choiceMessage;

        private UnityAction[] m_slotHandlers;
        private GameObject m_panelSavedFocus;
        private GameObject m_restoredFocus;
        private bool m_isInitialized;
        private bool m_isListenerBound;
        private bool m_hasLoggedReferenceError;

        public event Action<int> SlotSelected;
        public event Action SaveRequested;
        public event Action LoadRequested;
        public event Action CloseRequested;

        public bool IsOpen
        {
            get
            {
                return m_choicePanel != null && m_choicePanel.activeSelf;
            }
        }

        public RectTransform TutorialFirstSlot
        {
            get
            {
                return m_slots != null && m_slots.Length > 0 && m_slots[0] != null
                    ? m_slots[0].transform as RectTransform : null;
            }
        }

        public RectTransform TutorialSecondSlot
        {
            get
            {
                return m_slots != null && m_slots.Length > 1 && m_slots[1] != null
                    ? m_slots[1].transform as RectTransform : null;
            }
        }

        public RectTransform TutorialLoadButton
        {
            get
            {
                return m_loadButton != null ? m_loadButton.transform as RectTransform : null;
            }
        }

        public RectTransform TutorialSaveButton
        {
            get
            {
                return m_saveButton != null ? m_saveButton.transform as RectTransform : null;
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
        public void BindReferences(GameObject _panel, GameObject _choicePanel, Button[] _slots,
            Button _saveButton, Button _loadButton, Button _closeButton, TMP_Text _choiceMessage)
        {
            Unbind();
            m_panel = _panel;
            m_choicePanel = _choicePanel;
            m_slots = _slots;
            m_saveButton = _saveButton;
            m_loadButton = _loadButton;
            m_closeButton = _closeButton;
            m_choiceMessage = _choiceMessage;
        }

        /// <summary>
        /// 시작 숨김 상태와 슬롯·저장·불러오기·닫기 버튼 이벤트를 연결합니다. 중복 호출에 안전합니다.
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

        public void SetPanelVisible(bool _visible)
        {
            if (m_panel == null || m_panel.activeSelf == _visible)
            {
                return;
            }

            if (_visible)
            {
                UIRoot.SaveFocus(ref m_panelSavedFocus, transform);
            }

            m_panel.SetActive(_visible);

            if (_visible)
            {
                Selectable defaultSelectable = (m_slots != null && m_slots.Length > 0)
                    ? m_slots[0]
                    : (Selectable)m_closeButton;
                UIRoot.SetFocus(defaultSelectable, m_closeButton != null ? new Selectable[] { m_closeButton } : null);
            }
            else
            {
                UIRoot.RestoreFocus(ref m_panelSavedFocus);
            }
        }

        public void SetSlotsInteractable(bool _interactable)
        {
            if (m_slots == null)
            {
                return;
            }

            foreach (Button slot in m_slots)
            {
                if (slot != null)
                {
                    slot.interactable = _interactable;
                }
            }
        }

        /// <summary>
        /// 선택창을 열고 안내 문구를 표시합니다. 열기 전 선택을 기억해 닫을 때 되돌립니다.
        /// </summary>
        public void ShowChoice(string _message, bool _canLoad = true)
        {
            UIRoot.SaveFocus(ref m_restoredFocus, m_choicePanel != null ? m_choicePanel.transform : null);
            if (m_loadButton != null)
            {
                m_loadButton.interactable = _canLoad;
            }

            if (m_choiceMessage != null)
            {
                m_choiceMessage.text = _message ?? string.Empty;
            }

            if (m_choicePanel == null)
            {
                return;
            }

            m_choicePanel.SetActive(true);
            UIScreenRoot.BringToFront(m_choicePanel.transform);
            UIRoot.SetFocus(m_saveButton, m_loadButton != null ? new Selectable[] { m_loadButton, m_closeButton } : null);
        }

        public void HideChoice()
        {
            if (m_choicePanel != null)
            {
                m_choicePanel.SetActive(false);
            }

            Selectable fallback = (m_slots != null && m_slots.Length > 0) ? m_slots[0] : null;
            UIRoot.RestoreFocus(ref m_restoredFocus, fallback);
        }

        public void HideAll()
        {
            SetPanelVisible(false);
            HideChoice();
        }

        private void ApplyInitialHiddenState()
        {
            if (m_panel != null)
            {
                m_panel.SetActive(false);
            }

            if (m_choicePanel != null)
            {
                m_choicePanel.SetActive(false);
            }

            m_restoredFocus = null;
        }

        private bool ValidateReferences()
        {
            bool isValid = m_panel != null && m_choicePanel != null && m_slots != null &&
                m_slots.Length > 0 && m_saveButton != null && m_loadButton != null &&
                m_closeButton != null && m_choiceMessage != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[PresetPanelView] Initialize 실패: 필수 참조 누락 " +
                    $"(panel={m_panel != null}, choicePanel={m_choicePanel != null}, " +
                    $"slots={m_slots != null && m_slots.Length > 0}, saveButton={m_saveButton != null}, " +
                    $"loadButton={m_loadButton != null}, closeButton={m_closeButton != null}, " +
                    $"choiceMessage={m_choiceMessage != null}). " +
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
            if (m_slots != null)
            {
                if (m_slotHandlers == null || m_slotHandlers.Length != m_slots.Length)
                {
                    m_slotHandlers = new UnityAction[m_slots.Length];
                }

                for (int i = 0; i < m_slots.Length; i++)
                {
                    if (m_slots[i] == null)
                    {
                        continue;
                    }

                    int slot = i;
                    m_slotHandlers[i] = () => SlotSelected?.Invoke(slot);
                    m_slots[i].onClick.RemoveListener(m_slotHandlers[i]);
                    m_slots[i].onClick.AddListener(m_slotHandlers[i]);
                }
            }

            m_saveButton?.onClick.AddListener(OnSaveClicked);
            m_loadButton?.onClick.AddListener(OnLoadClicked);
            m_closeButton?.onClick.AddListener(OnCloseClicked);
        }

        private void Unbind()
        {
            if (!m_isListenerBound)
            {
                return;
            }

            m_isListenerBound = false;
            if (m_slots != null && m_slotHandlers != null)
            {
                int count = Mathf.Min(m_slots.Length, m_slotHandlers.Length);
                for (int i = 0; i < count; i++)
                {
                    if (m_slots[i] != null && m_slotHandlers[i] != null)
                    {
                        m_slots[i].onClick.RemoveListener(m_slotHandlers[i]);
                    }
                }
            }

            m_saveButton?.onClick.RemoveListener(OnSaveClicked);
            m_loadButton?.onClick.RemoveListener(OnLoadClicked);
            m_closeButton?.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnSaveClicked()
        {
            SaveRequested?.Invoke();
        }

        private void OnLoadClicked()
        {
            LoadRequested?.Invoke();
        }

        private void OnCloseClicked()
        {
            CloseRequested?.Invoke();
        }
    }
}
