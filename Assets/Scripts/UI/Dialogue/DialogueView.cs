using System;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using TMPro;

namespace WeaponWorkshop
{
    // 대화 화면의 저작된 참조, 표시·숨김, 확인 버튼과 Workshop 입력 이벤트 전달을 담당한다.
    public sealed class DialogueView : MonoBehaviour
    {
        private const int AUTHORED_SORTING_ORDER = 150;
        private const int SUSPENDED_CHOICE_SORTING_ORDER = 151;
        // 선택지 문구가 길어져도 잘리지 않도록 상자 높이와 버튼 높이를 문구에 맞춰 늘린다.
        private const float ChoiceFontSize = 18f;
        private const float ChoiceSideMargin = 18f;
        private const float ChoiceTopPadding = 16f;
        private const float ChoiceBottomMargin = 16f;
        private const float ChoiceGap = 6f;
        private const float ChoiceTextPadding = 12f;
        private const float ChoiceMinHeight = 39f;
        private const float ChoiceMinLineHeight = 40f;
        private const float ChoiceMaxWidth = 620f;
        private const float ChoiceMaxBoxHeight = 520f;
        private const float ChoiceLineInset = 36f;

        [Header("Authored UI")]
        [SerializeField] private Canvas m_canvas;
        [SerializeField] private UnityEngine.UI.GraphicRaycaster m_graphicRaycaster;
        [SerializeField] private GameObject m_blocker;
        [SerializeField] private DialogueConfirmButtonEventRelay m_answerRelay;

        private GameObject m_box;
        private TextMeshProUGUI m_line;
        private TextMeshProUGUI m_speakerLabel;
        private UnityEngine.UI.Button m_answerButton;
        private TextMeshProUGUI m_answerLabel;
        private InputActionAsset m_inputActions;

        private InputActionMap m_controls;
        private InputAction m_advanceAction;
        private InputAction m_interactAction;
        // 배경 클릭 확인용 UI 맵 복제본. 대화 맵과 별도로 View 활성 상태에 맞춰 켜고 끈다.
        private InputActionMap m_uiControls;
        private InputAction m_clickAction;
        private InputAction m_scrollAction;
        private bool m_isInitialized;
        private bool m_isInputBlocked;
        private bool m_authoredRaycasterEnabled;
        private bool m_authoredAnswerInteractable;
        private bool m_hasAppliedInitialHiddenState;
        private bool m_hasLoggedInitializationError;
        private RectTransform m_boxRect;
        private RectTransform m_lineRect;
        private bool m_authoredLayoutCaptured;
        private Vector2 m_authoredBoxSize;
        private Vector2 m_authoredLineAnchorMin;
        private Vector2 m_authoredLineAnchorMax;
        private Vector2 m_authoredLinePivot;
        private Vector2 m_authoredLinePosition;
        private Vector2 m_authoredLineSize;
        private bool m_authoredLineAutoSize;
        private float m_authoredLineFontSize;
        private readonly System.Collections.Generic.List<UnityEngine.UI.Button> m_choiceButtons = new System.Collections.Generic.List<UnityEngine.UI.Button>();

        public event Action InteractPerformed;
        public event Action AdvancePerformed;
        public event Action ClickPerformed;
        public event Action AnswerPointerDown;
        public event Action AnswerPointerClick;
        public event Action AnswerSubmitted;

        public bool IsInitialized => m_isInitialized;
        public bool IsOpen => m_box != null && m_box.activeSelf;
        public bool IsInteractPressed => m_interactAction != null && m_interactAction.IsPressed();
        public bool IsAdvancePressed => m_advanceAction != null && m_advanceAction.IsPressed();
        public bool IsClickPressed => m_clickAction != null && m_clickAction.IsPressed();
        public bool IsAnswerAvailable => m_answerButton != null && m_answerButton.isActiveAndEnabled && m_answerButton.interactable;
        public RectTransform TutorialButton => m_answerButton != null ? m_answerButton.transform as RectTransform : null;
        public int ChoiceCount => m_choiceButtons.Count;
        public event Action<int> ChoiceSelected;

        private void Awake()
        {
            ApplyInitialHiddenState();
        }

        private void OnEnable()
        {
            if (m_isInitialized)
            {
                UIInputPolicy.RequestActionMapEnabled(m_controls, true);
                m_uiControls?.Enable();
            }
        }

        private void OnDisable()
        {
            UIInputPolicy.RequestActionMapEnabled(m_controls, false);
            m_uiControls?.Disable();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// 기존 DialogueUI의 직렬화 참조를 옮겨 받고 화면을 명시적으로 숨긴다.
        /// </summary>
        public void Configure(GameObject _box, TextMeshProUGUI _line, TextMeshProUGUI _speakerLabel,
            UnityEngine.UI.Button _answerButton, TextMeshProUGUI _answerLabel, InputActionAsset _inputActions)
        {
            m_box = _box;
            m_line = _line;
            m_speakerLabel = _speakerLabel;
            m_answerButton = _answerButton;
            m_answerLabel = _answerLabel;
            m_inputActions = _inputActions;
            m_boxRect = m_box != null ? m_box.transform as RectTransform : null;
            m_lineRect = m_line != null ? m_line.rectTransform : null;
            CaptureAuthoredLayout();

            if (m_box != null)
            {
                m_box.SetActive(false);
            }

            if (m_blocker != null)
            {
                m_blocker.SetActive(false);
            }
        }

        /// <summary>
        /// 필수 참조와 Workshop 입력 액션을 연결한다. 실패는 한 번만 기록하고 재시도에서도 거부한다.
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

            m_authoredRaycasterEnabled = m_graphicRaycaster.enabled;
            m_authoredAnswerInteractable = m_answerButton.interactable;
            ApplySortingOrder(AUTHORED_SORTING_ORDER);

            InputActionMap source = m_inputActions.FindActionMap("Workshop", false);
            if (source == null)
            {
                LogInitializationError("[DialogueView] Initialize 실패: 'Workshop' 액션 맵을 찾을 수 없습니다. (m_inputActions)");
                return false;
            }

            InputAction interactSource = source.FindAction("Interact", false);
            InputAction advanceSource = source.FindAction("AdvanceDialogue", false);
            if (interactSource == null || advanceSource == null)
            {
                LogInitializationError(
                    "[DialogueView] Initialize 실패: 'Workshop' 맵에 필수 액션이 없습니다. " +
                    $"(Interact={interactSource != null}, AdvanceDialogue={advanceSource != null})");
                return false;
            }

            m_controls = source.Clone();
            m_interactAction = m_controls.FindAction("Interact", false);
            m_advanceAction = m_controls.FindAction("AdvanceDialogue", false);
            if (m_interactAction == null || m_advanceAction == null)
            {
                LogInitializationError("[DialogueView] Initialize 실패: 복제한 입력 맵에서 필수 액션을 찾을 수 없습니다.");
                CleanupInputMap();
                return false;
            }

            InputActionMap uiSource = m_inputActions.FindActionMap("UI", false);
            if (uiSource == null || uiSource.FindAction("Click", false) == null)
            {
                LogInitializationError("[DialogueView] Initialize 실패: 'UI' 맵의 'Click' 액션을 찾을 수 없습니다. (m_inputActions)");
                CleanupInputMap();
                return false;
            }

            m_uiControls = uiSource.Clone();
            m_clickAction = m_uiControls.FindAction("Click", false);
            m_scrollAction = m_uiControls.FindAction("ScrollWheel", false);
            m_interactAction.performed += OnInteractPerformed;
            m_advanceAction.performed += OnAdvancePerformed;
            m_clickAction.performed += OnClickPerformed;
            if (m_scrollAction != null) m_scrollAction.performed += OnScrollPerformed;
            m_answerRelay.Bind(OnAnswerPointerDown, OnAnswerPointerClick, OnAnswerSubmitted);
            ConfigureNavigation();

            if (isActiveAndEnabled)
            {
                UIInputPolicy.RequestActionMapEnabled(m_controls, true);
                m_uiControls.Enable();
            }

            UIInputPolicy.RegisterDialogueMap(m_controls);
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 입력 연결과 버튼 콜백을 해제하고 화면을 숨긴다. 중복 호출에 안전하다.
        /// </summary>
        public void Dispose()
        {
            if (m_isInitialized)
            {
                if (m_answerRelay != null)
                {
                    m_answerRelay.Bind(null, null, null);
                }

                m_isInitialized = false;
            }

            CleanupInputMap();
            HideImmediate();
        }

        public bool Show(string _text, string _answer, string _speaker)
        {
            if (!m_isInitialized || !isActiveAndEnabled)
            {
                return false;
            }

            ClearChoices();
            m_answerButton.gameObject.SetActive(true);
            m_line.text = _text;
            if (m_speakerLabel != null)
            {
                m_speakerLabel.text = _speaker;
            }

            m_answerLabel.text = _answer;

            if (m_blocker != null)
            {
                m_blocker.SetActive(true);
            }

            m_box.SetActive(true);
            return true;
        }

        public bool ShowChoices(string _text, string _speaker, string[] _labels)
        {
            if (!Show(_text, string.Empty, _speaker) || _labels == null || _labels.Length == 0)
                return false;

            m_answerButton.gameObject.SetActive(false);
            RectTransform original = (RectTransform)m_answerButton.transform;
            CaptureAuthoredLayout();
            float boxWidth = m_authoredBoxSize.x;
            float buttonWidth = Mathf.Min(ChoiceMaxWidth, boxWidth - ChoiceSideMargin * 2f);
            var heights = new float[_labels.Length];
            for (int i = 0; i < _labels.Length; i++)
            {
                int index = i;
                UnityEngine.UI.Button button = Instantiate(m_answerButton, original.parent);
                button.gameObject.name = "DialogueChoice_" + i;
                button.gameObject.SetActive(true);
                button.GetComponent<DialogueConfirmButtonEventRelay>().enabled = false;
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.text = _labels[i];
                label.enableAutoSizing = false;
                label.fontSize = ChoiceFontSize;
                label.horizontalAlignment = HorizontalAlignmentOptions.Left;
                label.verticalAlignment = VerticalAlignmentOptions.Middle;
                float textWidth = buttonWidth + label.rectTransform.sizeDelta.x;
                heights[i] = Mathf.Max(ChoiceMinHeight,
                    label.GetPreferredValues(_labels[i], textWidth, 0f).y + ChoiceTextPadding);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ChoiceSelected?.Invoke(index));
                m_choiceButtons.Add(button);
            }
            ApplyChoiceLayout(buttonWidth, heights);
            ConfigureChoiceNavigation();
            return true;
        }

        private void ApplyChoiceLayout(float _buttonWidth, float[] _heights)
        {
            if (m_boxRect == null || m_lineRect == null || m_line == null) return;
            CaptureAuthoredLayout();
            float stackHeight = ChoiceGap * Mathf.Max(0, _heights.Length - 1);
            foreach (float height in _heights) stackHeight += height;
            float lineWidth = Mathf.Max(80f, m_authoredBoxSize.x - ChoiceLineInset);
            float lineHeight = Mathf.Max(ChoiceMinLineHeight,
                m_line.GetPreferredValues(m_line.text, lineWidth, 0f).y + ChoiceTextPadding);
            float boxHeight = Mathf.Min(ChoiceMaxBoxHeight, Mathf.Max(m_authoredBoxSize.y,
                ChoiceTopPadding + lineHeight + ChoiceGap + stackHeight + ChoiceBottomMargin));
            m_boxRect.sizeDelta = new Vector2(m_authoredBoxSize.x, boxHeight);
            m_lineRect.anchorMin = new Vector2(0f, 1f);
            m_lineRect.anchorMax = new Vector2(1f, 1f);
            m_lineRect.pivot = new Vector2(0.5f, 1f);
            m_lineRect.sizeDelta = new Vector2(m_authoredLineSize.x, lineHeight);
            m_lineRect.anchoredPosition = new Vector2(0f, -ChoiceTopPadding);
            float top = ChoiceBottomMargin + stackHeight;
            for (int i = 0; i < m_choiceButtons.Count && i < _heights.Length; i++)
            {
                RectTransform rect = (RectTransform)m_choiceButtons[i].transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.sizeDelta = new Vector2(_buttonWidth, _heights[i]);
                top -= _heights[i];
                rect.anchoredPosition = new Vector2(-ChoiceSideMargin, top);
                top -= ChoiceGap;
            }
        }

        private void CaptureAuthoredLayout()
        {
            if (m_authoredLayoutCaptured || m_boxRect == null || m_lineRect == null || m_line == null) return;
            m_authoredLayoutCaptured = true;
            m_authoredBoxSize = m_boxRect.sizeDelta;
            m_authoredLineAnchorMin = m_lineRect.anchorMin;
            m_authoredLineAnchorMax = m_lineRect.anchorMax;
            m_authoredLinePivot = m_lineRect.pivot;
            m_authoredLinePosition = m_lineRect.anchoredPosition;
            m_authoredLineSize = m_lineRect.sizeDelta;
            m_authoredLineAutoSize = m_line.enableAutoSizing;
            m_authoredLineFontSize = m_line.fontSize;
        }

        private void RestoreAuthoredLayout()
        {
            if (!m_authoredLayoutCaptured || m_boxRect == null || m_lineRect == null || m_line == null) return;
            m_boxRect.sizeDelta = m_authoredBoxSize;
            m_lineRect.anchorMin = m_authoredLineAnchorMin;
            m_lineRect.anchorMax = m_authoredLineAnchorMax;
            m_lineRect.pivot = m_authoredLinePivot;
            m_lineRect.anchoredPosition = m_authoredLinePosition;
            m_lineRect.sizeDelta = m_authoredLineSize;
            m_line.enableAutoSizing = m_authoredLineAutoSize;
            m_line.fontSize = m_authoredLineFontSize;
        }

        public void FocusChoice(int _index)
        {
            if (_index >= 0 && _index < m_choiceButtons.Count && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(m_choiceButtons[_index].gameObject);
        }

        public void SelectFocusedChoice()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            for (int i = 0; i < m_choiceButtons.Count; i++)
                if (m_choiceButtons[i].gameObject == selected) { ChoiceSelected?.Invoke(i); return; }
            if (m_choiceButtons.Count > 0) ChoiceSelected?.Invoke(0);
        }

        private void ConfigureChoiceNavigation()
        {
            for (int i = 0; i < m_choiceButtons.Count; i++)
            {
                var navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit };
                navigation.selectOnUp = m_choiceButtons[Mathf.Max(0, i - 1)];
                navigation.selectOnDown = m_choiceButtons[Mathf.Min(m_choiceButtons.Count - 1, i + 1)];
                m_choiceButtons[i].navigation = navigation;
            }
        }

        private void ClearChoices()
        {
            RestoreAuthoredLayout();
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            foreach (var button in m_choiceButtons)
            {
                if (button != null && button.gameObject == selected) EventSystem.current.SetSelectedGameObject(null);
                if (button != null)
                {
                    button.gameObject.SetActive(false);
                    Destroy(button.gameObject);
                }
            }
            m_choiceButtons.Clear();
        }

        public void Hide()
        {
            HideImmediate();
        }

        /// <summary>
        /// 상위 UI가 입력을 소유하는 동안 자체 Raycast와 확인 버튼 상호작용을 끄고,
        /// 해제 시 저작된 상태로 되돌린다.
        /// </summary>
        public void SetInputBlocked(bool _isBlocked)
        {
            if (!m_isInitialized || m_isInputBlocked == _isBlocked)
            {
                return;
            }

            m_isInputBlocked = _isBlocked;
            UIInputPolicy.SetDialogueInputBlocked(_isBlocked);

            if (m_graphicRaycaster != null)
            {
                m_graphicRaycaster.enabled = _isBlocked ? false : m_authoredRaycasterEnabled;
            }

            if (m_answerButton != null)
            {
                m_answerButton.interactable = _isBlocked ? false : m_authoredAnswerInteractable;
            }
            foreach (var button in m_choiceButtons)
                if (button != null) button.interactable = !_isBlocked;
        }

        /// <summary>
        /// 입력이 중단된 선택창 위에 대화가 겹칠 때만 저작된 정렬 순서보다 한 단계 높인다.
        /// </summary>
        public void SetSuspendedChoiceOverlay(bool _isOverlay)
        {
            ApplySortingOrder(_isOverlay ? SUSPENDED_CHOICE_SORTING_ORDER : AUTHORED_SORTING_ORDER);
        }

        /// <summary>
        /// 특정 튜토리얼 구간에서 배경 클릭 차단 블로커의 활성화 여부를 임시로 제어합니다.
        /// 블로커가 비활성화되어도 대화 상자 자체의 상호작용과 레이캐스트는 유지됩니다.
        /// </summary>
        /// <param name="_active">블로커를 활성화할지 여부입니다.</param>
        public void SetClickBlockerActive(bool _active)
        {
            if (m_blocker != null)
            {
                m_blocker.SetActive(_active);
            }
        }

        /// <summary>
        /// 표시 성공 후 유효한 확인 버튼을 선택한다. 버튼이 없거나 사용할 수 없으면 선택을 비운다.
        /// </summary>
        public void ApplyDefaultFocus()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            eventSystem.SetSelectedGameObject(null);
            if (m_choiceButtons.Count > 0) { FocusChoice(0); return; }
            if (IsValidSelectable(m_answerButton))
            {
                eventSystem.SetSelectedGameObject(m_answerButton.gameObject);
            }
        }

        /// <summary>
        /// 상위 화면이 선택을 소유하지 않았을 때만 대화 버튼 포커스를 되살린다.
        /// </summary>
        public void RestoreFocusIfUnowned()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected != null)
            {
                foreach (var choice in m_choiceButtons)
                    if (choice != null && selected == choice.gameObject) return;
                if (m_answerButton != null && selected == m_answerButton.gameObject)
                {
                    return;
                }

                UnityEngine.UI.Selectable selectable = selected.GetComponent<UnityEngine.UI.Selectable>();
                if (IsValidSelectable(selectable))
                {
                    return;
                }
            }

            ApplyDefaultFocus();
        }

        // Awake와 Initialize/Configure 중 먼저 실행된 쪽만 초기 숨김을 확정해 늦은 Awake가 표시 상태를 끄지 않게 한다.
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
            ClearChoices();
            if (m_answerButton != null) m_answerButton.gameObject.SetActive(true);
            ApplySortingOrder(AUTHORED_SORTING_ORDER);

            if (m_blocker != null)
            {
                m_blocker.SetActive(false);
            }

            if (m_box != null)
            {
                m_box.SetActive(false);
            }

            ClearDialogueFocus();
        }

        private void ApplySortingOrder(int _sortingOrder)
        {
            if (m_canvas != null && m_canvas.sortingOrder != _sortingOrder)
            {
                m_canvas.sortingOrder = _sortingOrder;
            }
        }

        // 일시정지 메뉴 등 대화 밖 선택은 건드리지 않고 대화 버튼 선택만 해제한다.
        private void ClearDialogueFocus()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || m_answerButton == null)
            {
                return;
            }

            if (eventSystem.currentSelectedGameObject == m_answerButton.gameObject)
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        private bool ValidateReferences()
        {
            bool isValid = m_box != null &&
                           m_line != null &&
                           m_answerButton != null &&
                           m_answerLabel != null &&
                           m_canvas != null &&
                           m_graphicRaycaster != null &&
                           m_blocker != null &&
                           m_answerRelay != null &&
                           m_inputActions != null;

            if (!isValid)
            {
                LogInitializationError(
                    "[DialogueView] Initialize 실패: 필수 참조 누락 " +
                    $"(m_box={m_box != null}, m_line={m_line != null}, m_speakerLabel={m_speakerLabel != null}, " +
                    $"m_answerButton={m_answerButton != null}, m_answerLabel={m_answerLabel != null}, " +
                    $"m_canvas={m_canvas != null}, m_graphicRaycaster={m_graphicRaycaster != null}, " +
                    $"m_blocker={m_blocker != null}, m_answerRelay={m_answerRelay != null}, " +
                    $"m_inputActions={m_inputActions != null})");
            }

            return isValid;
        }

        private void LogInitializationError(string _message)
        {
            if (m_hasLoggedInitializationError)
            {
                return;
            }

            m_hasLoggedInitializationError = true;
            Debug.LogError(_message, this);
        }

        private void CleanupInputMap()
        {
            if (m_interactAction != null)
            {
                m_interactAction.performed -= OnInteractPerformed;
                m_interactAction = null;
            }

            if (m_advanceAction != null)
            {
                m_advanceAction.performed -= OnAdvancePerformed;
                m_advanceAction = null;
            }

            if (m_clickAction != null)
            {
                m_clickAction.performed -= OnClickPerformed;
                m_clickAction = null;
            }

            if (m_scrollAction != null)
            {
                m_scrollAction.performed -= OnScrollPerformed;
                m_scrollAction = null;
            }

            m_uiControls?.Disable();
            m_uiControls?.Dispose();
            m_uiControls = null;

            UIInputPolicy.UnregisterDialogueMap(m_controls);
            UIInputPolicy.RequestActionMapEnabled(m_controls, false);
            m_controls?.Dispose();
            m_controls = null;
        }

        // 단일 확인 버튼이 방향 입력 등으로 다른 UI로 자동 이동하지 않게 한다.
        private void ConfigureNavigation()
        {
            if (m_answerButton == null)
            {
                return;
            }

            UnityEngine.UI.Navigation navigation = new UnityEngine.UI.Navigation();
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            navigation.wrapAround = false;
            m_answerButton.navigation = navigation;
        }

        private static bool IsValidSelectable(UnityEngine.UI.Selectable _selectable)
        {
            return _selectable != null &&
                   _selectable.gameObject.activeInHierarchy &&
                   _selectable.isActiveAndEnabled &&
                   _selectable.IsInteractable();
        }

        private void OnInteractPerformed(InputAction.CallbackContext _context)
        {
            InteractPerformed?.Invoke();
        }

        private void OnAdvancePerformed(InputAction.CallbackContext _context)
        {
            AdvancePerformed?.Invoke();
        }

        private void OnClickPerformed(InputAction.CallbackContext _context)
        {
            ClickPerformed?.Invoke();
        }

        private void OnScrollPerformed(InputAction.CallbackContext _context)
        {
            if (m_isInputBlocked || m_choiceButtons.Count == 0) return;
            float y = _context.ReadValue<Vector2>().y;
            if (Mathf.Abs(y) < 0.01f) return;
            MoveChoiceFocus(y > 0f ? -1 : 1);
        }

        private void MoveChoiceFocus(int _direction)
        {
            if (m_choiceButtons.Count == 0) return;
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            int current = -1;
            for (int i = 0; i < m_choiceButtons.Count; i++)
                if (m_choiceButtons[i].gameObject == selected) { current = i; break; }
            if (current < 0) { FocusChoice(0); return; }
            int next = Mathf.Clamp(current + _direction, 0, m_choiceButtons.Count - 1);
            FocusChoice(next);
        }

        private void OnAnswerPointerDown()
        {
            AnswerPointerDown?.Invoke();
        }

        private void OnAnswerPointerClick()
        {
            AnswerPointerClick?.Invoke();
        }

        private void OnAnswerSubmitted()
        {
            AnswerSubmitted?.Invoke();
        }
    }
}
