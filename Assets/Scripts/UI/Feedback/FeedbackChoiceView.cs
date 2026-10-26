using System;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using TMPro;

namespace WeaponWorkshop
{
    // 확인창의 저작된 UI 참조, 표시·숨김, 버튼 입력, 캔버스 정렬, 포커스 적용을 담당한다.
    public sealed class FeedbackChoiceView : MonoBehaviour
    {
        [Header("Choice UI")]
        [SerializeField] private GameObject m_choicePanel;
        [SerializeField] private TextMeshProUGUI m_choiceMessage;
        [SerializeField] private UnityEngine.UI.Button m_yesButton;
        [SerializeField] private UnityEngine.UI.Button m_noButton;
        [SerializeField] private TextMeshProUGUI m_yesLabel;
        [SerializeField] private TextMeshProUGUI m_noLabel;

        [Header("Authored UI")]
        [SerializeField] private Canvas m_choiceCanvas;
        [SerializeField] private UnityEngine.UI.GraphicRaycaster m_graphicRaycaster;
        [SerializeField] private GameObject m_acknowledgementClickBlocker;
        [SerializeField] private FeedbackConfirmButtonEventRelay m_yesButtonRelay;

        [Header("Input")]
        [SerializeField] private InputActionAsset m_inputActions;

        private InputActionMap m_controls;
        private InputAction m_advanceAction;
        private InputAction m_interactAction;
        private RectTransform m_yesButtonRect;
        private Vector2 m_yesAnchorMin;
        private Vector2 m_yesAnchorMax;
        private bool m_hasCapturedState;
        private bool m_isInitialized;
        private bool m_hasAppliedInitialHiddenState;
        private bool m_hasLoggedReferenceError;

        public event Action InteractPerformed;
        public event Action AdvancePerformed;
        public event Action YesPointerDown;
        public event Action YesPointerClick;
        public event Action YesSubmitted;
        public event Action NoClicked;

        public bool IsInitialized => m_isInitialized;
        public bool IsInteractPressed => m_interactAction != null && m_interactAction.IsPressed();
        public bool IsAdvancePressed => m_advanceAction != null && m_advanceAction.IsPressed();
        public bool IsYesButtonAvailable => m_yesButton != null && m_yesButton.isActiveAndEnabled && m_yesButton.interactable;

        /// <summary>
        /// 확인창 View가 활성화되어 있고 확인 패널이 계층 구조상 실제로 표시 중인지 여부를 반환합니다.
        /// </summary>
        public bool IsVisible => isActiveAndEnabled && m_choicePanel != null && m_choicePanel.activeInHierarchy;
        public RectTransform TutorialConfirmButton => m_yesButton != null && m_yesButton.interactable ? m_yesButton.transform as RectTransform : null;

        private void Awake()
        {
            CaptureAuthoredState();
            ApplyInitialHiddenState();
        }

        private void OnEnable()
        {
            if (m_isInitialized)
            {
                UIInputPolicy.RequestActionMapEnabled(m_controls, true);
            }
        }

        private void OnDisable()
        {
            UIInputPolicy.RequestActionMapEnabled(m_controls, false);
        }

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// 필수 참조와 Workshop 입력 액션을 연결하고 초기 숨김 상태를 적용한다. 중복 호출에 안전하다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            CaptureAuthoredState();
            ApplyInitialHiddenState();

            if (!ValidateReferences())
            {
                return false;
            }

            InputActionMap source = m_inputActions.FindActionMap("Workshop", false);
            if (source == null)
            {
                Debug.LogError("[FeedbackChoiceView] Initialize 실패: 'Workshop' 액션 맵을 찾을 수 없습니다. (m_inputActions)", this);
                return false;
            }

            InputAction interactSource = source.FindAction("Interact", false);
            InputAction advanceSource = source.FindAction("AdvanceDialogue", false);
            if (interactSource == null || advanceSource == null)
            {
                Debug.LogError(
                    "[FeedbackChoiceView] Initialize 실패: 'Workshop' 맵에 필수 액션이 없습니다. " +
                    $"(Interact={interactSource != null}, AdvanceDialogue={advanceSource != null})",
                    this);
                return false;
            }

            m_controls = source.Clone();
            m_interactAction = m_controls.FindAction("Interact", false);
            m_advanceAction = m_controls.FindAction("AdvanceDialogue", false);
            if (m_interactAction == null || m_advanceAction == null)
            {
                Debug.LogError("[FeedbackChoiceView] Initialize 실패: 복제한 입력 맵에서 필수 액션을 찾을 수 없습니다.", this);
                CleanupInputMap();
                return false;
            }

            m_interactAction.performed += OnInteractPerformed;
            m_advanceAction.performed += OnAdvancePerformed;
            m_noButton.onClick.AddListener(OnNoClicked);
            m_yesButtonRelay.Bind(OnYesPointerDown, OnYesPointerClick, OnYesSubmitted);

            if (isActiveAndEnabled)
            {
                UIInputPolicy.RequestActionMapEnabled(m_controls, true);
            }

            UIInputPolicy.RegisterChoiceMap(m_controls);
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 입력 액션과 버튼 리스너를 정리하고 확인창을 숨긴다. 중복 호출에 안전하다.
        /// </summary>
        public void Dispose()
        {
            if (m_isInitialized)
            {
                if (m_noButton != null)
                {
                    m_noButton.onClick.RemoveListener(OnNoClicked);
                }

                if (m_yesButtonRelay != null)
                {
                    m_yesButtonRelay.Bind(null, null, null);
                }

                m_isInitialized = false;
            }

            CleanupInputMap();
            HideImmediate();
        }

        /// <summary>
        /// 메시지, 버튼 문구, 확인 가능 여부, 단일 안내 여부, 일시정지 상태에 맞춰 확인창을 표시한다.
        /// </summary>
        public bool ShowChoice(string _message, string _yesLabel, string _noLabel, bool _canConfirm, bool _isAcknowledgement, bool _isGamePaused)
        {
            if (!m_isInitialized)
            {
                return false;
            }

            m_choiceMessage.text = _message;
            m_yesLabel.text = _yesLabel;
            m_noLabel.text = _noLabel;
            m_yesButton.interactable = _canConfirm;
            m_noButton.interactable = true;
            RestoreYesButtonPosition();
            m_noButton.gameObject.SetActive(true);

            if (_isAcknowledgement)
            {
                m_acknowledgementClickBlocker.transform.SetAsFirstSibling();
                m_acknowledgementClickBlocker.SetActive(true);
                CenterYesButton();
                m_noButton.gameObject.SetActive(false);
            }
            else
            {
                m_acknowledgementClickBlocker.SetActive(false);
            }

            m_choicePanel.SetActive(true);
            ConfigureNavigation();
            UIScreenRoot.BringToFront(m_choicePanel.transform);
            m_choiceCanvas.overrideSorting = true;
            // 일시정지 메뉴에서 새로 연 일반 선택창은 PausePanel(200) 앞인 300, 일반 단일 안내는 150으로 배치한다.
            m_choiceCanvas.sortingOrder = !_isAcknowledgement && _isGamePaused ? 300 : 150;
            m_graphicRaycaster.enabled = true;
            UIInputPolicy.SetChoiceOpen(true);
            return true;
        }

        /// <summary>
        /// 확인창 표시를 끄고 다음 표시를 위해 Yes 버튼 앵커와 No 버튼 표시 상태를 되돌린다.
        /// </summary>
        public void Hide()
        {
            HideImmediate();
        }

        /// <summary>
        /// Yes·No 버튼의 상호작용 가능 상태를 지정한다. Suspend 해제 시 원래 canConfirm 값을 복원할 때 사용한다.
        /// </summary>
        public void SetButtonInteractivity(bool _yesInteractable, bool _noInteractable)
        {
            if (m_yesButton != null)
            {
                m_yesButton.interactable = _yesInteractable;
            }

            if (m_noButton != null)
            {
                m_noButton.interactable = _noInteractable;
            }

            ConfigureNavigation();
        }

        /// <summary>
        /// 현재 EventSystem 선택 객체가 유효한 Selectable이면 반환하고 아니면 null을 반환한다.
        /// </summary>
        public GameObject GetValidSelectedObject()
        {
            if (EventSystem.current == null)
            {
                return null;
            }

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            return IsValidSelectable(selected) ? selected : null;
        }

        /// <summary>
        /// 단일 안내는 Yes, 일반 선택창은 No를 기본 포커스로 적용한다.
        /// </summary>
        public void ApplyDefaultFocus(bool _isAcknowledgement)
        {
            if (EventSystem.current == null)
            {
                return;
            }

            UnityEngine.UI.Selectable target = _isAcknowledgement ? m_yesButton : m_noButton;
            EventSystem.current.SetSelectedGameObject(null);
            if (target != null && IsValidSelectable(target.gameObject))
            {
                EventSystem.current.SetSelectedGameObject(target.gameObject);
            }
        }

        /// <summary>
        /// 확인창을 열기 전에 보관한 선택 객체를 유효한 Selectable일 때만 복원하고, 아니면 선택을 비운다.
        /// </summary>
        public void RestoreFocus(GameObject _previous)
        {
            if (!TryRestoreFocus(_previous) && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        /// <summary>
        /// 입력 중단 전에 보관한 선택을 복원하고, 유효하지 않으면 화면 종류별 기본 선택을 적용한다.
        /// </summary>
        public void RestoreFocusOrApplyDefault(GameObject _previous, bool _isAcknowledgement)
        {
            if (TryRestoreFocus(_previous))
            {
                return;
            }

            ApplyDefaultFocus(_isAcknowledgement);
        }

        private bool TryRestoreFocus(GameObject _previous)
        {
            if (EventSystem.current == null || !IsValidSelectable(_previous))
            {
                return false;
            }

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(_previous);
            return true;
        }

        private static bool IsValidSelectable(GameObject _target)
        {
            if (_target == null || !_target.activeInHierarchy)
            {
                return false;
            }

            UnityEngine.UI.Selectable selectable = _target.GetComponent<UnityEngine.UI.Selectable>();
            return selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable();
        }

        private void CaptureAuthoredState()
        {
            if (m_hasCapturedState)
            {
                return;
            }

            m_hasCapturedState = true;
            m_yesButtonRect = m_yesButton != null ? m_yesButton.transform as RectTransform : null;
            if (m_yesButtonRect != null)
            {
                m_yesAnchorMin = m_yesButtonRect.anchorMin;
                m_yesAnchorMax = m_yesButtonRect.anchorMax;
            }
        }

        private void HideImmediate()
        {
            if (m_acknowledgementClickBlocker != null)
            {
                m_acknowledgementClickBlocker.SetActive(false);
            }

            if (m_choicePanel != null)
            {
                m_choicePanel.SetActive(false);
            }

            UIInputPolicy.SetChoiceOpen(false);
            RestoreYesButtonPosition();
            if (m_noButton != null)
            {
                m_noButton.gameObject.SetActive(true);
            }
        }

        // Awake와 Initialize 중 먼저 실행된 쪽만 초기 숨김을 적용해 늦은 Awake가 표시 상태를 끄지 않게 한다.
        private void ApplyInitialHiddenState()
        {
            if (m_hasAppliedInitialHiddenState)
            {
                return;
            }

            m_hasAppliedInitialHiddenState = true;
            HideImmediate();
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

            UIInputPolicy.UnregisterChoiceMap(m_controls);
            UIInputPolicy.RequestActionMapEnabled(m_controls, false);
            m_controls?.Dispose();
            m_controls = null;
        }

        private void CenterYesButton()
        {
            if (m_yesButtonRect == null)
            {
                return;
            }

            float anchorWidth = m_yesAnchorMax.x - m_yesAnchorMin.x;
            m_yesButtonRect.anchorMin = new Vector2(.5f - anchorWidth * .5f, m_yesAnchorMin.y);
            m_yesButtonRect.anchorMax = new Vector2(.5f + anchorWidth * .5f, m_yesAnchorMax.y);
        }

        private void RestoreYesButtonPosition()
        {
            if (m_yesButtonRect == null)
            {
                return;
            }

            m_yesButtonRect.anchorMin = m_yesAnchorMin;
            m_yesButtonRect.anchorMax = m_yesAnchorMax;
        }

        // 유효한 상대 버튼끼리만 Yes(왼쪽)·No(오른쪽)로 연결하고 양 끝에서 순환하지 않는다.
        // 단일 안내·입력 중단·확인 불가 상태에서는 이동 대상을 남기지 않는다.
        private void ConfigureNavigation()
        {
            if (m_yesButton == null || m_noButton == null)
            {
                return;
            }

            bool canLinkButtons = IsValidSelectable(m_yesButton.gameObject) && IsValidSelectable(m_noButton.gameObject);

            UnityEngine.UI.Navigation yesNavigation = new UnityEngine.UI.Navigation();
            yesNavigation.mode = canLinkButtons ? UnityEngine.UI.Navigation.Mode.Explicit : UnityEngine.UI.Navigation.Mode.None;
            yesNavigation.wrapAround = false;
            yesNavigation.selectOnLeft = null;
            yesNavigation.selectOnRight = canLinkButtons ? m_noButton : null;
            m_yesButton.navigation = yesNavigation;

            UnityEngine.UI.Navigation noNavigation = new UnityEngine.UI.Navigation();
            noNavigation.mode = canLinkButtons ? UnityEngine.UI.Navigation.Mode.Explicit : UnityEngine.UI.Navigation.Mode.None;
            noNavigation.wrapAround = false;
            noNavigation.selectOnLeft = canLinkButtons ? m_yesButton : null;
            noNavigation.selectOnRight = null;
            m_noButton.navigation = noNavigation;
        }

        private bool ValidateReferences()
        {
            bool isValid = m_choicePanel != null &&
                           m_choiceMessage != null &&
                           m_yesButton != null &&
                           m_noButton != null &&
                           m_yesLabel != null &&
                           m_noLabel != null &&
                           m_choiceCanvas != null &&
                           m_graphicRaycaster != null &&
                           m_acknowledgementClickBlocker != null &&
                           m_yesButtonRelay != null &&
                           m_inputActions != null;

            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[FeedbackChoiceView] Initialize 실패: 필수 참조 누락 " +
                    $"(m_choicePanel={m_choicePanel != null}, m_choiceMessage={m_choiceMessage != null}, " +
                    $"m_yesButton={m_yesButton != null}, m_noButton={m_noButton != null}, " +
                    $"m_yesLabel={m_yesLabel != null}, m_noLabel={m_noLabel != null}, " +
                    $"m_choiceCanvas={m_choiceCanvas != null}, m_graphicRaycaster={m_graphicRaycaster != null}, " +
                    $"m_acknowledgementClickBlocker={m_acknowledgementClickBlocker != null}, " +
                    $"m_yesButtonRelay={m_yesButtonRelay != null}, m_inputActions={m_inputActions != null})",
                    this);
            }

            return isValid;
        }

        private void OnInteractPerformed(InputAction.CallbackContext _context)
        {
            InteractPerformed?.Invoke();
        }

        private void OnAdvancePerformed(InputAction.CallbackContext _context)
        {
            AdvancePerformed?.Invoke();
        }

        private void OnYesPointerDown()
        {
            YesPointerDown?.Invoke();
        }

        private void OnYesPointerClick()
        {
            YesPointerClick?.Invoke();
        }

        private void OnYesSubmitted()
        {
            YesSubmitted?.Invoke();
        }

        private void OnNoClicked()
        {
            NoClicked?.Invoke();
        }
    }
}
