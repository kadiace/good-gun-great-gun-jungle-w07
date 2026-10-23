using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 저장 슬롯 패널의 직렬화 참조 보관, 카드·삭제 버튼 클릭의 C# 이벤트 전달, 슬롯 요약·가능 상태 표시와
    /// 시작 숨김 초기화만 담당하는 View입니다. 저장·삭제 판정과 파일 입출력은 SaveSlotsPresenter가 담당합니다.
    /// 수동 슬롯 카드 버튼과 닫기 버튼은 프리팹 persistent 호출(SelectSlot·Close)이 그대로 소유하며,
    /// 런타임에 리스너를 달던 자동저장 카드·삭제 버튼만 이 View가 바인딩합니다.
    /// </summary>
    public sealed class SaveSlotsView : MonoBehaviour
    {
        private const string VIEW_NAME = "SaveSlotsView";

        [SerializeField] private GameObject m_panel;
        [SerializeField] private GameObject m_mainPanel;
        [SerializeField] private GameObject m_openButton;
        [SerializeField] private TextMeshProUGUI m_title;
        [SerializeField] private Button m_autoSaveButton;
        [SerializeField] private TextMeshProUGUI m_autoSaveText;
        [SerializeField] private Button[] m_slotButtons;
        [SerializeField] private TextMeshProUGUI[] m_slotTexts;
        [SerializeField] private Button[] m_deleteButtons;
        [SerializeField] private Button m_autoSaveDeleteButton;

        private SaveSlotCardView m_autoSaveCard;
        private SaveSlotCardView[] m_manualCards;
        private readonly System.Collections.Generic.List<Button> m_boundDeleteButtons = new System.Collections.Generic.List<Button>();
        private readonly System.Collections.Generic.List<UnityAction> m_boundDeleteHandlers = new System.Collections.Generic.List<UnityAction>();

        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        public event Action AutoSaveClicked;
        public event Action<int> SlotDeleteRequested;
        public event Action AutoSaveDeleteRequested;

        public bool IsOpen
        {
            get
            {
                return m_panel != null && m_panel.activeInHierarchy;
            }
        }

        /// <summary>ESC로 닫은 뒤 포커스를 돌려줄 여는 버튼(없으면 null)입니다.</summary>
        public GameObject OpenButton
        {
            get
            {
                return m_openButton;
            }
        }

        /// <summary>
        /// 브리지 SaveSlotsUI가 보유한 실효 직렬화 참조(씬 오버라이드 반영)를 Initialize 전에 받아
        /// 같은 대상을 가리키게 합니다. null 인자는 해당 필드의 저작된 기본 참조를 유지합니다.
        /// </summary>
        public void BindReferences(GameObject _panel, GameObject _mainPanel, GameObject _openButton,
            TextMeshProUGUI _title, Button _autoSaveButton, TextMeshProUGUI _autoSaveText,
            Button[] _slotButtons, TextMeshProUGUI[] _slotTexts, Button[] _deleteButtons, Button _autoSaveDeleteButton)
        {
            if (_panel != null)
            {
                m_panel = _panel;
            }

            if (_mainPanel != null)
            {
                m_mainPanel = _mainPanel;
            }

            if (_openButton != null)
            {
                m_openButton = _openButton;
            }

            if (_title != null)
            {
                m_title = _title;
            }

            if (_autoSaveButton != null)
            {
                m_autoSaveButton = _autoSaveButton;
            }

            if (_autoSaveText != null)
            {
                m_autoSaveText = _autoSaveText;
            }

            if (_slotButtons != null && _slotButtons.Length > 0)
            {
                m_slotButtons = _slotButtons;
            }

            if (_slotTexts != null && _slotTexts.Length > 0)
            {
                m_slotTexts = _slotTexts;
            }

            if (_deleteButtons != null && _deleteButtons.Length > 0)
            {
                m_deleteButtons = _deleteButtons;
            }

            if (_autoSaveDeleteButton != null)
            {
                m_autoSaveDeleteButton = _autoSaveDeleteButton;
            }
        }

        /// <summary>
        /// 카드·삭제 버튼 참조를 SaveSlotCardView로 해석하고 필수 참조를 검증한 뒤 클릭 리스너를 연결합니다.
        /// 패널은 닫힌 상태로 시작합니다. 중복 호출은 초기화된 상태를 유지합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (!ResolveCards())
            {
                return false;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            BindAutoSaveCard();
            BindDeleteButtons();
            SetPanelVisible(false);
            m_isInitialized = true;
            return true;
        }

        /// <summary>바인딩한 리스너를 해제합니다. 중복 호출에 안전합니다.</summary>
        public void Dispose()
        {
            if (m_autoSaveCard != null)
            {
                m_autoSaveCard.Clicked -= OnAutoSaveCardClicked;
                m_autoSaveCard.UnbindMainClick();
            }

            UnbindDeleteButtons();
            m_isInitialized = false;
        }

        /// <summary>패널 제목(게임 저장/게임 불러오기)을 갱신합니다.</summary>
        public void SetTitle(string _text)
        {
            if (m_title != null)
            {
                m_title.text = _text;
            }
        }

        public void SetPanelVisible(bool _visible)
        {
            if (m_panel != null)
            {
                m_panel.SetActive(_visible);
            }
        }

        public void SetMainPanelVisible(bool _visible)
        {
            if (m_mainPanel != null)
            {
                m_mainPanel.SetActive(_visible);
            }
        }

        public void SetOpenerVisible(bool _visible)
        {
            if (m_openButton != null)
            {
                m_openButton.SetActive(_visible);
            }
        }

        /// <summary>자동저장 카드의 요약 문장과 카드·삭제 버튼 상태를 갱신합니다.</summary>
        public void SetAutoSaveState(string _summary, bool _cardVisible, bool _cardInteractable,
            bool _deleteVisible, bool _deleteInteractable)
        {
            if (m_autoSaveText != null)
            {
                m_autoSaveText.text = _summary;
            }

            if (m_autoSaveCard != null)
            {
                m_autoSaveCard.gameObject.SetActive(_cardVisible);
                m_autoSaveCard.SetInteractable(_cardInteractable);
            }

            SetDeleteState(m_autoSaveDeleteButton, _deleteVisible, _deleteInteractable);
        }

        /// <summary>수동 슬롯 카드의 요약 문장과 카드·삭제 버튼 상태를 갱신합니다.</summary>
        public void SetSlotState(int _slot, string _summary, bool _cardInteractable,
            bool _deleteVisible, bool _deleteInteractable)
        {
            SaveSlotCardView card = GetManualCard(_slot);
            if (card != null)
            {
                card.SetLabel(_summary);
                card.SetInteractable(_cardInteractable);
            }

            SetDeleteState(GetDeleteButton(_slot), _deleteVisible, _deleteInteractable);
        }

        private void SetDeleteState(Button _deleteButton, bool _visible, bool _interactable)
        {
            if (_deleteButton == null)
            {
                return;
            }

            _deleteButton.gameObject.SetActive(_visible);
            _deleteButton.interactable = _interactable;
        }

        private SaveSlotCardView GetManualCard(int _slot)
        {
            if (m_manualCards == null || _slot < 0 || _slot >= m_manualCards.Length)
            {
                return null;
            }

            return m_manualCards[_slot];
        }

        private Button GetDeleteButton(int _slot)
        {
            if (m_deleteButtons == null || _slot < 0 || _slot >= m_deleteButtons.Length)
            {
                return null;
            }

            return m_deleteButtons[_slot];
        }

        /// <summary>버튼 참조에서 카드 View를 해석합니다. 저작되지 않은 카드는 화면명·필드명으로 기록합니다.</summary>
        private bool ResolveCards()
        {
            int manualCount = m_slotButtons != null ? m_slotButtons.Length : 0;
            m_manualCards = new SaveSlotCardView[manualCount];
            for (int i = 0; i < manualCount; i++)
            {
                m_manualCards[i] = m_slotButtons[i] != null ? m_slotButtons[i].GetComponent<SaveSlotCardView>() : null;
            }

            m_autoSaveCard = m_autoSaveButton != null ? m_autoSaveButton.GetComponent<SaveSlotCardView>() : null;
            return true;
        }

        private bool ValidateReferences()
        {
            bool hasManualCards = m_manualCards != null;
            int invalidSlotIndex = -1;
            string invalidSlotMember = null;
            if (hasManualCards)
            {
                for (int i = 0; i < m_manualCards.Length; i++)
                {
                    if (m_manualCards[i] == null)
                    {
                        invalidSlotIndex = i;
                        invalidSlotMember = "SaveSlotCardView";
                        break;
                    }

                    if (m_slotButtons[i] == null)
                    {
                        invalidSlotIndex = i;
                        invalidSlotMember = "m_slotButtons";
                        break;
                    }

                    if (m_slotTexts == null || i >= m_slotTexts.Length || m_slotTexts[i] == null)
                    {
                        invalidSlotIndex = i;
                        invalidSlotMember = "m_slotTexts";
                        break;
                    }
                }
            }

            bool hasDeletes = m_deleteButtons != null;
            int invalidDeleteIndex = -1;
            if (hasDeletes)
            {
                for (int i = 0; i < m_deleteButtons.Length; i++)
                {
                    if (m_deleteButtons[i] == null)
                    {
                        invalidDeleteIndex = i;
                        break;
                    }
                }
            }

            bool isValid = m_panel != null &&
                           m_title != null &&
                           m_autoSaveButton != null &&
                           m_autoSaveText != null &&
                           m_autoSaveCard != null &&
                           hasManualCards &&
                           m_manualCards.Length == 3 &&
                           invalidSlotIndex < 0 &&
                           hasDeletes &&
                           m_deleteButtons.Length >= 3 &&
                           invalidDeleteIndex < 0 &&
                           m_autoSaveDeleteButton != null;

            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    $"[{VIEW_NAME}] Initialize 실패: 필수 참조 누락 " +
                    $"(m_panel={m_panel != null}, m_title={m_title != null}, " +
                    $"m_autoSaveButton={m_autoSaveButton != null}, m_autoSaveText={m_autoSaveText != null}, " +
                    $"m_autoSaveCard={m_autoSaveCard != null}, " +
                    $"m_slotButtons={(hasManualCards ? m_manualCards.Length.ToString() : "null")}, " +
                    (invalidSlotIndex >= 0 ? $"슬롯 {invalidSlotIndex + 1}의 {invalidSlotMember} 누락, " : "") +
                    $"m_deleteButtons={(hasDeletes ? m_deleteButtons.Length.ToString() : "null")}" +
                    (invalidDeleteIndex >= 0 ? $"(슬롯 {invalidDeleteIndex + 1} 누락)" : "") +
                    $", m_autoSaveDeleteButton={m_autoSaveDeleteButton != null}) " +
                    "SaveSlots 저작 명령(Tools/UI/Authoring/SaveSlots/Author)으로 프리팹을 먼저 저작하세요.",
                    this);
            }

            return isValid;
        }

        private void BindAutoSaveCard()
        {
            if (m_autoSaveCard == null)
            {
                return;
            }

            m_autoSaveCard.Clicked -= OnAutoSaveCardClicked;
            m_autoSaveCard.Clicked += OnAutoSaveCardClicked;
            m_autoSaveCard.BindMainClick();
        }

        private void BindDeleteButtons()
        {
            if (m_deleteButtons != null)
            {
                for (int i = 0; i < m_deleteButtons.Length; i++)
                {
                    Button deleteButton = m_deleteButtons[i];
                    if (deleteButton == null)
                    {
                        continue;
                    }

                    int slot = i;
                    UnityAction handler = () => SlotDeleteRequested?.Invoke(slot);
                    BindDeleteButton(deleteButton, handler);
                    m_boundDeleteButtons.Add(deleteButton);
                    m_boundDeleteHandlers.Add(handler);
                }
            }

            if (m_autoSaveDeleteButton != null)
            {
                BindDeleteButton(m_autoSaveDeleteButton, OnAutoSaveDeleteClicked);
                m_boundDeleteButtons.Add(m_autoSaveDeleteButton);
                m_boundDeleteHandlers.Add(OnAutoSaveDeleteClicked);
            }
        }

        /// <summary>
        /// 삭제 버튼 하나에 클릭 리스너를 연결합니다. 기존 EnsureDeleteButtons와 같이 프리팹·씬에 남아 있는
        /// persistent 호출(예: Intro 씬에 추가된 DeleteSlot 버튼)을 먼저 꺼서 이중 삭제를 막습니다.
        /// </summary>
        private void BindDeleteButton(Button _button, UnityAction _handler)
        {
            for (int i = 0; i < _button.onClick.GetPersistentEventCount(); i++)
            {
                _button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            }

            _button.onClick.RemoveListener(_handler);
            _button.onClick.AddListener(_handler);
        }

        private void UnbindDeleteButtons()
        {
            for (int i = 0; i < m_boundDeleteButtons.Count && i < m_boundDeleteHandlers.Count; i++)
            {
                Button deleteButton = m_boundDeleteButtons[i];
                if (deleteButton != null)
                {
                    deleteButton.onClick.RemoveListener(m_boundDeleteHandlers[i]);
                }
            }

            m_boundDeleteButtons.Clear();
            m_boundDeleteHandlers.Clear();
        }

        private void OnAutoSaveCardClicked()
        {
            AutoSaveClicked?.Invoke();
        }

        private void OnAutoSaveDeleteClicked()
        {
            AutoSaveDeleteRequested?.Invoke();
        }
    }
}
