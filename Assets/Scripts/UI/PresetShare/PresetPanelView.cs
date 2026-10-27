using System;

using TMPro;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 프리셋 브라우저(6슬롯 관리창)의 저작된 참조, 시작 숨김, 버튼 입력 이벤트를 담당하는 View입니다.
    /// 저장소·지갑 같은 도메인은 Presenter가 다룹니다. 새 브라우저 참조(m_browser 이하)는 View 로컬
    /// 프리팹 참조이며, 구 UIManager 교차 프리팹 오버라이드를 전달하는 브리지 BindReferences는
    /// 레거시 필드(구 선택창 숨김·호환용)에만 쓰이고 새 참조를 덮지 않습니다.
    /// </summary>
    public sealed class PresetPanelView : MonoBehaviour
    {
        // 레거시 직렬화 필드. 구 선택창(choicePanel) 숨김과 교차 프리팹 오버라이드 호환용으로만 남는다.
        [SerializeField] private GameObject m_panel;
        [SerializeField] private GameObject m_choicePanel;
        [SerializeField] private Button[] m_slots;
        [SerializeField] private Button m_saveButton;
        [SerializeField] private Button m_loadButton;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TMP_Text m_choiceMessage;

        // 브라우저 모드 참조. m_panel은 기존 상단 바를 그대로 쓰고, 나머지는 View 로컬 프리팹 참조다.
        [SerializeField] private GameObject m_browser;
        [SerializeField] private Button m_openButton;
        [SerializeField] private Button[] m_browserSlots;
        [SerializeField] private TMP_Text[] m_slotNames;
        [SerializeField] private TMP_Text[] m_slotStates;
        [SerializeField] private PresetBlueprintGraphic[] m_slotPreviews;
        [SerializeField] private GameObject[] m_selectionFrames;
        [SerializeField] private PresetBlueprintGraphic m_detailPreview;
        [SerializeField] private TMP_Text m_detailTitle;
        [SerializeField] private TMP_Text m_detailStatus;
        [SerializeField] private TMP_Text m_detailCost;
        [SerializeField] private TMP_Text m_detailNote;
        [SerializeField] private Button m_browserSave;
        [SerializeField] private Button m_browserLoad;
        [SerializeField] private Button m_browserClose;
        [SerializeField] private Button m_deleteButton;
        [SerializeField] private TMP_InputField m_renameInput;
        [SerializeField] private Button m_renameButton;

        private UnityAction[] m_browserSlotHandlers;
        private GameObject m_panelSavedFocus;
        private GameObject m_browserSavedFocus;
        private GameObject m_restoredFocus;
        private bool m_isInitialized;
        private bool m_isListenerBound;
        private bool m_hasLoggedReferenceError;
        private Color m_noteColor;
        private bool m_noteColorCaptured;

        public event Action OpenRequested;
        public event Action<int> BrowserSlotSelected;
        public event Action BrowserSaveRequested;
        public event Action BrowserLoadRequested;
        public event Action BrowserDeleteRequested;
        public event Action RenameRequested;
        public event Action BrowserCloseRequested;

        /// <summary>View 로컬 참조로 브라우저가 저작되어 있으면 6슬롯 관리 모드로 동작합니다.</summary>
        public bool UsesBrowser
        {
            get
            {
                return m_browser != null;
            }
        }

        public bool IsBrowserOpen
        {
            get
            {
                return m_browser != null && m_browser.activeSelf;
            }
        }

        public bool IsOpen
        {
            get
            {
                return IsBrowserOpen;
            }
        }

        public RectTransform TutorialFirstSlot
        {
            get
            {
                // 열려 있으면 퇴장 저장 슬롯(5번), 닫혀 있으면 상단 열기 버튼(첫 클릭이 열기+선택이 된다).
                return (IsBrowserOpen ? BrowserSlotRect(TutorialManager.RaidPresetSaveSlot) : null) ?? OpenButtonRect;
            }
        }

        public RectTransform TutorialSecondSlot
        {
            get
            {
                return (IsBrowserOpen ? BrowserSlotRect(TutorialManager.RaidPresetLoadSlot) : null) ?? OpenButtonRect;
            }
        }

        public RectTransform TutorialLoadButton
        {
            get
            {
                return m_browserLoad != null ? m_browserLoad.transform as RectTransform : null;
            }
        }

        public RectTransform TutorialSaveButton
        {
            get
            {
                return m_browserSave != null ? m_browserSave.transform as RectTransform : null;
            }
        }

        private RectTransform OpenButtonRect
        {
            get
            {
                return m_openButton != null ? m_openButton.transform as RectTransform : null;
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
        /// 씬 오버라이드가 적용된 브리지의 레거시 참조를 주입합니다. 구 선택창 숨김과 호환용이며,
        /// 브라우저 참조는 View 로컬 프리팹 참조를 그대로 쓰므로 구 UIManager 교차 오버라이드 값이
        /// 새 슬롯·버튼 참조를 덜 일이 없습니다(별도 필드).
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
        /// 시작 숨김 상태와 열기·슬롯·저장·불러오기·삭제·이름변경·닫기 버튼 이벤트를 연결합니다. 중복 호출에 안전합니다.
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

            DisableNameRichText();
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
                UIRoot.SetFocus(m_openButton, m_browserClose != null ? new Selectable[] { m_browserClose } : null);
            }
            else
            {
                UIRoot.RestoreFocus(ref m_panelSavedFocus);
            }
        }

        /// <summary>구 선택창 숨김용(호환 API). 새 흐름에서는 선택창을 열지 않습니다.</summary>
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
        }

        /// <summary>구 선택창을 숨깁니다. 교차 프리팹 오버라이드로 남은 패널 정리에 사용합니다.</summary>
        public void HideChoice()
        {
            if (m_choicePanel != null)
            {
                m_choicePanel.SetActive(false);
            }

            UIRoot.RestoreFocus(ref m_restoredFocus, m_openButton);
        }

        /// <summary>
        /// 브라우저 모달을 열고 닫습니다. 열기 전 포커스를 보관해 닫을 때 되돌리며, 열림 시 첫 슬롯에 포커스를 둡니다.
        /// </summary>
        public void SetBrowserVisible(bool _visible)
        {
            if (m_browser == null || m_browser.activeSelf == _visible)
            {
                return;
            }

            if (_visible)
            {
                UIRoot.SaveFocus(ref m_browserSavedFocus, m_browser.transform);
                m_browser.SetActive(true);
                UIScreenRoot.BringToFront(m_browser.transform);
                UIRoot.SetFocus(BrowserSlotButton(0), m_browserClose != null ? new Selectable[] { m_browserClose } : null);
            }
            else
            {
                m_browser.SetActive(false);
                UIRoot.RestoreFocus(ref m_browserSavedFocus, m_openButton);
            }
        }

        /// <summary>슬롯 행의 이름·상태 텍스트, 선택 테두리, 상호작용 가능 여부를 갱신합니다.</summary>
        public void SetSlotRow(int _index, string _name, string _state, bool _selected, bool _interactable)
        {
            Button slot = BrowserSlotButton(_index);
            if (slot != null)
            {
                slot.interactable = _interactable;
            }

            SetText(ArrayText(m_slotNames, _index), _name);
            SetText(ArrayText(m_slotStates, _index), _state);

            GameObject frame = ArrayObject(m_selectionFrames, _index);
            if (frame != null && frame.activeSelf != _selected)
            {
                frame.SetActive(_selected);
            }
        }

        /// <summary>슬롯 미리보기 그래픽에 블루프린트를 전달합니다(null이면 미리보기를 비웁니다).</summary>
        public void SetSlotPreview(int _index, WeaponShareCode.Blueprint _blueprint)
        {
            PresetBlueprintGraphic preview = ArrayGraphic(m_slotPreviews, _index);
            if (preview != null)
            {
                preview.SetBlueprint(_blueprint);
            }
        }

        /// <summary>상세 영역 미리보기 그래픽에 블루프린트를 전달합니다(null이면 비웁니다).</summary>
        public void SetDetailPreview(WeaponShareCode.Blueprint _blueprint)
        {
            if (m_detailPreview != null)
            {
                m_detailPreview.SetBlueprint(_blueprint);
            }
        }

        /// <summary>상세 영역의 제목·상태·비용·안내 문구를 갱신합니다.</summary>
        public void SetDetailTexts(string _title, string _status, string _cost, string _note)
        {
            SetText(m_detailTitle, _title);
            SetText(m_detailStatus, _status);
            SetText(m_detailCost, _cost);
            SetText(m_detailNote, _note);
            SetApplyBlocked(false);
        }

        public void SetApplyBlocked(bool blocked)
        {
            if (m_detailNote == null) return;
            if (!m_noteColorCaptured)
            {
                m_noteColor = m_detailNote.color;
                m_noteColorCaptured = true;
            }
            m_detailNote.color = blocked ? new Color(1f, .42f, .32f, 1f) : m_noteColor;
        }

        public void SetApplyFailure(string message)
        {
            SetText(m_detailNote, message);
            SetApplyBlocked(true);
        }

        public void FocusBrowserSlot(int index)
        {
            UIRoot.SetFocus(BrowserSlotButton(index));
        }

        /// <summary>저장·불러오기·삭제·이름변경 버튼의 상호작용 가능 여부를 갱신합니다.</summary>
        public void SetBrowserActions(bool _saveInteractable, bool _loadInteractable, bool _deleteInteractable,
            bool _renameInteractable)
        {
            if (m_browserSave != null)
            {
                m_browserSave.interactable = _saveInteractable;
            }

            if (m_browserLoad != null)
            {
                m_browserLoad.interactable = _loadInteractable;
            }

            if (m_deleteButton != null)
            {
                m_deleteButton.interactable = _deleteInteractable;
            }

            if (m_renameButton != null)
            {
                m_renameButton.interactable = _renameInteractable;
            }
        }

        public void SetOpenButtonInteractable(bool _interactable)
        {
            if (m_openButton != null)
            {
                m_openButton.interactable = _interactable;
            }
        }

        /// <summary>선택 슬롯이 바뀔 때 이름 입력칸을 저장된 이름으로 맞춥니다(주기 갱신 때 덮지 않는다).</summary>
        public void SetRenameInputText(string _text)
        {
            if (m_renameInput == null)
            {
                return;
            }

            string value = _text ?? string.Empty;
            if (m_renameInput.text != value)
            {
                m_renameInput.text = value;
            }
        }

        public string GetRenameInputText()
        {
            return m_renameInput != null ? m_renameInput.text : string.Empty;
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

            if (m_browser != null)
            {
                m_browser.SetActive(false);
            }

            if (m_openButton != null)
            {
                m_openButton.interactable = false;
            }

            if (m_selectionFrames != null)
            {
                foreach (GameObject frame in m_selectionFrames)
                {
                    if (frame != null && frame.activeSelf)
                    {
                        frame.SetActive(false);
                    }
                }
            }

            if (m_slotPreviews != null)
            {
                foreach (PresetBlueprintGraphic preview in m_slotPreviews)
                {
                    if (preview != null)
                    {
                        preview.SetBlueprint(null);
                    }
                }
            }

            if (m_detailPreview != null)
            {
                m_detailPreview.SetBlueprint(null);
            }

            if (m_renameInput != null && m_renameInput.text.Length > 0)
            {
                m_renameInput.text = string.Empty;
            }

            m_browserSavedFocus = null;
            m_restoredFocus = null;
        }

        /// <summary>무기 이름은 사용자 입력값이므로 리치 텍스트 태그를 그대로 보이게 한다.</summary>
        private void DisableNameRichText()
        {
            if (m_slotNames != null)
            {
                foreach (TMP_Text text in m_slotNames)
                {
                    if (text != null)
                    {
                        text.richText = false;
                    }
                }
            }

            if (m_detailTitle != null)
            {
                m_detailTitle.richText = false;
            }

            if (m_detailStatus != null)
            {
                m_detailStatus.richText = false;
            }

            if (m_choiceMessage != null)
            {
                m_choiceMessage.richText = false;
            }

            if (m_renameInput != null)
            {
                m_renameInput.richText = false;
            }
        }

        private bool ValidateReferences()
        {
            bool hasSlots = m_browserSlots != null && m_browserSlots.Length >= WeaponPresets.SlotCount &&
                !HasNullEntry(m_browserSlots);
            bool isValid = m_panel != null && m_browser != null && m_openButton != null && hasSlots &&
                m_browserSave != null && m_browserLoad != null && m_browserClose != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[PresetPanelView] Initialize 실패: 브라우저 모드 필수 참조 누락 " +
                    $"(panel={m_panel != null}, browser={m_browser != null}, openButton={m_openButton != null}, " +
                    $"browserSlots[{(m_browserSlots != null ? m_browserSlots.Length : 0)}>={WeaponPresets.SlotCount}]=" +
                    $"{m_browserSlots != null && !HasNullEntry(m_browserSlots)}, " +
                    $"browserSave={m_browserSave != null}, browserLoad={m_browserLoad != null}, " +
                    $"browserClose={m_browserClose != null}). " +
                    "프리팹의 m_browser·m_openButton·m_browserSlots·m_browserSave·m_browserLoad·m_browserClose를 확인하세요.",
                    this);
            }

            return isValid;
        }

        private static bool HasNullEntry(Button[] _buttons)
        {
            foreach (Button button in _buttons)
            {
                if (button == null)
                {
                    return true;
                }
            }

            return false;
        }

        private Button BrowserSlotButton(int _index)
        {
            return m_browserSlots != null && _index >= 0 && _index < m_browserSlots.Length
                ? m_browserSlots[_index] : null;
        }

        private RectTransform BrowserSlotRect(int _index)
        {
            Button slot = BrowserSlotButton(_index);
            return slot != null ? slot.transform as RectTransform : null;
        }

        private static TMP_Text ArrayText(TMP_Text[] _texts, int _index)
        {
            return _texts != null && _index >= 0 && _index < _texts.Length ? _texts[_index] : null;
        }

        private static GameObject ArrayObject(GameObject[] _objects, int _index)
        {
            return _objects != null && _index >= 0 && _index < _objects.Length ? _objects[_index] : null;
        }

        private static PresetBlueprintGraphic ArrayGraphic(PresetBlueprintGraphic[] _graphics, int _index)
        {
            return _graphics != null && _index >= 0 && _index < _graphics.Length ? _graphics[_index] : null;
        }

        private static void SetText(TMP_Text _text, string _value)
        {
            if (_text == null)
            {
                return;
            }

            string value = _value ?? string.Empty;
            if (_text.text == value)
            {
                return;
            }

            _text.text = value;
        }

        private void Bind()
        {
            if (m_isListenerBound)
            {
                return;
            }

            m_isListenerBound = true;
            m_openButton?.onClick.AddListener(OnOpenClicked);
            if (m_browserSlots != null)
            {
                if (m_browserSlotHandlers == null || m_browserSlotHandlers.Length != m_browserSlots.Length)
                {
                    m_browserSlotHandlers = new UnityAction[m_browserSlots.Length];
                }

                for (int i = 0; i < m_browserSlots.Length; i++)
                {
                    if (m_browserSlots[i] == null)
                    {
                        continue;
                    }

                    int slot = i;
                    m_browserSlotHandlers[i] = () => BrowserSlotSelected?.Invoke(slot);
                    m_browserSlots[i].onClick.RemoveListener(m_browserSlotHandlers[i]);
                    m_browserSlots[i].onClick.AddListener(m_browserSlotHandlers[i]);
                }
            }

            m_browserSave?.onClick.AddListener(OnBrowserSaveClicked);
            m_browserLoad?.onClick.AddListener(OnBrowserLoadClicked);
            m_browserClose?.onClick.AddListener(OnBrowserCloseClicked);
            m_deleteButton?.onClick.AddListener(OnDeleteClicked);
            m_renameButton?.onClick.AddListener(OnRenameClicked);
        }

        private void Unbind()
        {
            if (!m_isListenerBound)
            {
                return;
            }

            m_isListenerBound = false;
            m_openButton?.onClick.RemoveListener(OnOpenClicked);
            if (m_browserSlots != null && m_browserSlotHandlers != null)
            {
                int count = Mathf.Min(m_browserSlots.Length, m_browserSlotHandlers.Length);
                for (int i = 0; i < count; i++)
                {
                    if (m_browserSlots[i] != null && m_browserSlotHandlers[i] != null)
                    {
                        m_browserSlots[i].onClick.RemoveListener(m_browserSlotHandlers[i]);
                    }
                }
            }

            m_browserSave?.onClick.RemoveListener(OnBrowserSaveClicked);
            m_browserLoad?.onClick.RemoveListener(OnBrowserLoadClicked);
            m_browserClose?.onClick.RemoveListener(OnBrowserCloseClicked);
            m_deleteButton?.onClick.RemoveListener(OnDeleteClicked);
            m_renameButton?.onClick.RemoveListener(OnRenameClicked);
        }

        private void OnOpenClicked()
        {
            OpenRequested?.Invoke();
        }

        private void OnBrowserSaveClicked()
        {
            BrowserSaveRequested?.Invoke();
        }

        private void OnBrowserLoadClicked()
        {
            BrowserLoadRequested?.Invoke();
        }

        private void OnBrowserCloseClicked()
        {
            BrowserCloseRequested?.Invoke();
        }

        private void OnDeleteClicked()
        {
            BrowserDeleteRequested?.Invoke();
        }

        private void OnRenameClicked()
        {
            RenameRequested?.Invoke();
        }
    }
}
