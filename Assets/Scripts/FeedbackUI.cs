using System;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using TMPro;

namespace WeaponWorkshop
{
    // Authored NotificationText and YesNoUI are shared by all callers on this canvas.
    public sealed class FeedbackUI : MonoBehaviour
    {
        [Header("Notification UI")]
        [SerializeField] private TextMeshProUGUI notificationText;
        [SerializeField] private CanvasGroup notificationGroup;
        [SerializeField] private float enterDuration = .18f, holdDuration = 2f, exitDuration = .4f;

        [Header("Choice UI")]
        [SerializeField] private GameObject choicePanel;
        [SerializeField] private TextMeshProUGUI choiceMessage;
        [SerializeField] private Button yesButton, noButton;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        private InputActionMap _controls;
        private InputAction _advanceAction;
        private InputAction _interactAction;
        private Action _yes, _no;
        private object _owner;
        private float _elapsed = -1f;
        private int _openedFrame;
        private bool _confirmRequested;
        private bool _choiceInputSuspended;
        private bool _isAcknowledgement;
        private RectTransform _yesButtonRect;
        private Vector2 _yesAnchorMin, _yesAnchorMax;

        private int _currentGestureId;
        private int _consumedGestureId = -1;
        private int _openedGestureId;
        private int _lastDownFrame = -1;
        private bool _inputHeldOnOpen;
        private ConfirmButtonEventRelay _yesButtonRelay;

        private Canvas _choiceCanvas;
        private GameObject _blocker;

        public bool IsChoiceOpen => _owner != null;
        public bool IsChoiceInputSuspended => _choiceInputSuspended;
        public RectTransform TutorialConfirmButton => yesButton != null && yesButton.interactable ? yesButton.transform as RectTransform : null;

        /// <summary>
        /// 호출 컴포넌트의 부모 캔버스 또는 전역 GameManager에서 FeedbackUI 인스턴스를 찾습니다.
        /// caller를 기준으로 검색하며 유효한 FeedbackUI를 반환합니다.
        /// </summary>
        public static FeedbackUI For(Component caller)
        {
            var local = caller != null ? caller.GetComponentInParent<FeedbackUI>(true) : null;
            if (local != null) return local;
            return GameManager.Instance != null ? GameManager.Instance.UI?.Feedback : null;
        }

        void Awake()
        {
            _yesButtonRect = yesButton != null ? yesButton.transform as RectTransform : null;
            if (_yesButtonRect != null)
            {
                _yesAnchorMin = _yesButtonRect.anchorMin;
                _yesAnchorMax = _yesButtonRect.anchorMax;
            }
            HideNotification();
            if (choicePanel != null) choicePanel.SetActive(false);
            EnsureBlocker();
            if (yesButton != null)
            {
                _yesButtonRelay = yesButton.GetComponent<ConfirmButtonEventRelay>();
                if (_yesButtonRelay == null)
                {
                    _yesButtonRelay = yesButton.gameObject.AddComponent<ConfirmButtonEventRelay>();
                }
                _yesButtonRelay.Bind(OnYesPointerDown, OnYesPointerClick, OnYesSubmitted);
            }
            if (noButton != null) noButton.onClick.AddListener(Cancel);
            if (inputActions != null)
            {
                _controls = inputActions.FindActionMap("Workshop", true).Clone();
                _interactAction = _controls.FindAction("Interact", false);
                if (_interactAction != null)
                {
                    _interactAction.performed += OnInteractPerformed;
                }
                _advanceAction = _controls.FindAction("AdvanceDialogue", false);
                if (_advanceAction != null)
                {
                    _advanceAction.performed += OnAdvancePerformed;
                }
            }
        }

        void OnEnable() => _controls?.Enable();

        void OnDisable()
        {
            _controls?.Disable();
            _inputHeldOnOpen = false;
            Cancel();
            HideNotification();
        }

        void OnDestroy()
        {
            if (_interactAction != null) _interactAction.performed -= OnInteractPerformed;
            if (_advanceAction != null) _advanceAction.performed -= OnAdvancePerformed;
            if (_yesButtonRelay != null) _yesButtonRelay.Bind(null, null, null);
            if (noButton != null) noButton.onClick.RemoveListener(Cancel);
            _controls?.Dispose();
        }

        void LateUpdate()
        {
            if (_inputHeldOnOpen)
            {
                bool isHeld = (_isAcknowledgement && _advanceAction != null && _advanceAction.IsPressed()) ||
                              (_interactAction != null && _interactAction.IsPressed());
                if (!isHeld)
                {
                    _inputHeldOnOpen = false;
                }
            }

            if (_confirmRequested)
            {
                _confirmRequested = false;
                Confirm();
            }
            if (_elapsed < 0f || notificationGroup == null) return;
            _elapsed += Time.unscaledDeltaTime;
            float enter = Mathf.Max(.01f, enterDuration), exit = Mathf.Max(.01f, exitDuration);
            if (_elapsed < enter)
            {
                float t = Mathf.SmoothStep(0f, 1f, _elapsed / enter);
                notificationGroup.alpha = t;
                notificationGroup.transform.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, t);
            }
            else if (_elapsed < enter + holdDuration)
            {
                notificationGroup.alpha = 1f;
                notificationGroup.transform.localScale = Vector3.one;
            }
            else if (_elapsed < enter + holdDuration + exit)
            {
                notificationGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, (_elapsed - enter - holdDuration) / exit);
            }
            else HideNotification();
        }

        /// <summary>
        /// 현재 프레임에 새 확인 제스처가 시작되었으면 제스처 식별자를 갱신하고 반환합니다.
        /// 동일 프레임의 중복 입력을 하나의 제스처 ID로 병합합니다.
        /// </summary>
        private int ObtainFrameGestureId()
        {
            if (Time.frameCount != _lastDownFrame)
            {
                _lastDownFrame = Time.frameCount;
                _currentGestureId++;
            }
            return _currentGestureId;
        }

        /// <summary>
        /// 상호작용(F) 입력 발생 시 제스처 ID를 발급하고 선택창 확인을 요청합니다.
        /// context로부터 입력 이벤트를 받아 제스처 기반 확인 절차를 호출합니다.
        /// </summary>
        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            RequestConfirmFromGesture(ObtainFrameGestureId());
        }

        /// <summary>
        /// 범용 넘김 입력 발생 시 단일 안내 상태에서만 제스처 ID를 발급하고 확인을 요청합니다.
        /// context로부터 입력 이벤트를 받아 단일 안내 여부를 판정한 뒤 확인을 요청합니다.
        /// </summary>
        private void OnAdvancePerformed(InputAction.CallbackContext context)
        {
            if (!_isAcknowledgement) return;
            RequestConfirmFromGesture(ObtainFrameGestureId());
        }

        /// <summary>
        /// 확인 버튼 포인터 누름 시점에 프레임 제스처 식별자를 발급합니다.
        /// 단일 안내의 마우스 좌클릭 범용 넘김과는 동일 프레임 제스처로 병합하고 일반 선택 및 터치·펜 버튼 포인터 입력은 클릭 시 소비할 제스처를 준비합니다.
        /// </summary>
        private void OnYesPointerDown()
        {
            if (!IsChoiceOpen || _choiceInputSuspended) return;
            if (Time.frameCount <= _openedFrame) return;
            if (_inputHeldOnOpen) return;
            if (yesButton == null || !yesButton.isActiveAndEnabled || !yesButton.interactable) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused && _isAcknowledgement) return;

            ObtainFrameGestureId();
        }

        /// <summary>
        /// 확인 버튼 포인터 클릭 시 포인터 누름에서 발급된 현재 제스처로 선택창 또는 단일 안내 확인을 요청합니다.
        /// 이미 소비된 제스처이거나 오픈 시점 이전 제스처이면 이어진 클릭을 무시합니다.
        /// </summary>
        private void OnYesPointerClick()
        {
            RequestConfirmFromGesture(_currentGestureId);
        }

        /// <summary>
        /// 포커스된 확인 버튼에서 UI Submit 이벤트가 발생했을 때 프레임 제스처를 발급하고 확인을 요청합니다.
        /// 단일 안내와 일반 선택창 모두에서 동일 프레임 중복 실행 없이 확인을 예약합니다.
        /// </summary>
        private void OnYesSubmitted()
        {
            RequestConfirmFromGesture(ObtainFrameGestureId());
        }

        /// <summary>
        /// 지정된 제스처 식별자에 대해 선택창 및 단일 안내의 확인 요건을 검증하고 확인 요청을 예약합니다.
        /// gestureId를 전달받아 일시정지, 입력 중단, 중복 소비 여부를 검사합니다.
        /// </summary>
        private void RequestConfirmFromGesture(int gestureId)
        {
            if (!IsChoiceOpen || _choiceInputSuspended) return;
            if (Time.frameCount <= _openedFrame) return;
            if (_inputHeldOnOpen) return;
            if (gestureId <= _openedGestureId) return;
            if (gestureId == _consumedGestureId) return;
            if (yesButton == null || !yesButton.isActiveAndEnabled || !yesButton.interactable) return;

            // 일시정지 중에는 뒤에 깔린 단일 안내의 입력을 차단한다.
            if (GameManager.Instance != null && GameManager.Instance.IsPaused && _isAcknowledgement) return;

            _consumedGestureId = gestureId;
            _confirmRequested = true;
        }

        /// <summary>
        /// 단일 안내 표시 중 빈 화면 클릭이 뒤편 UI로 전달되지 않도록 투명 클릭 차단기를 준비합니다.
        /// </summary>
        private void EnsureBlocker()
        {
            if (_blocker != null || choicePanel == null) return;
            Transform existing = choicePanel.transform.Find("AcknowledgementClickBlocker");
            if (existing != null)
            {
                _blocker = existing.gameObject;
            }
            else
            {
                var blockerGo = new GameObject("AcknowledgementClickBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                blockerGo.transform.SetParent(choicePanel.transform, false);
                var rect = blockerGo.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;

                var image = blockerGo.GetComponent<Image>();
                image.color = Color.clear;
                image.raycastTarget = true;
                _blocker = blockerGo;
            }
            _blocker.transform.SetAsFirstSibling();
            _blocker.SetActive(false);
        }

        /// <summary>
        /// 선택창 계층을 최상위로 올리고 일시정지 및 안내 종류에 맞는 캔버스 정렬 순서를 갱신합니다.
        /// </summary>
        private void UpdateCanvasSorting()
        {
            if (choicePanel == null) return;
            choicePanel.transform.SetAsLastSibling();
            if (_choiceCanvas == null)
            {
                _choiceCanvas = choicePanel.GetComponent<Canvas>();
                if (_choiceCanvas == null)
                {
                    _choiceCanvas = choicePanel.AddComponent<Canvas>();
                    choicePanel.AddComponent<GraphicRaycaster>();
                }
            }
            _choiceCanvas.overrideSorting = true;
            bool isPaused = GameManager.Instance != null && GameManager.Instance.IsPaused;
            // 일시정지 메뉴에서 새로 연 일반 선택창은 PausePanel(200) 앞인 300, 일반 단일 안내는 150으로 배치한다.
            _choiceCanvas.sortingOrder = (!_isAcknowledgement && isPaused) ? 300 : 150;
        }

        /// <summary>
        /// 상단 알림 텍스트를 연출과 함께 화면에 표시합니다.
        /// message가 비어 있으면 즉시 알림을 숨깁니다.
        /// </summary>
        public void ShowNotification(string message)
        {
            if (notificationText == null || notificationGroup == null) return;
            if (string.IsNullOrEmpty(message)) { HideNotification(); return; }
            notificationText.text = message;
            _elapsed = 0f;
            notificationGroup.alpha = 0f;
            notificationGroup.transform.localScale = Vector3.one * .92f;
            notificationGroup.gameObject.SetActive(true);
            notificationGroup.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 상단 알림 텍스트 표시를 숨기고 상태를 초기화합니다.
        /// </summary>
        private void HideNotification()
        {
            _elapsed = -1f;
            if (notificationGroup == null) return;
            notificationGroup.alpha = 0f;
            notificationGroup.blocksRaycasts = notificationGroup.interactable = false;
            notificationGroup.transform.localScale = Vector3.one;
            notificationGroup.gameObject.SetActive(false);
        }

        /// <summary>
        /// 예/아니오 선택지를 포함한 확인창을 표시하고 확인 및 취소 콜백을 등록합니다.
        /// requester, message, onYes, onNo, yesLabel, noLabel, canConfirm을 입력받아 성공 여부를 반환합니다.
        /// </summary>
        public bool ShowChoice(object requester, string message, Action onYes, Action onNo = null,
            string yesLabel = "예", string noLabel = "아니오", bool canConfirm = true)
        {
            if (!isActiveAndEnabled || IsChoiceOpen || requester == null || choicePanel == null || choiceMessage == null || yesButton == null || noButton == null) return false;
            _owner = requester;
            _yes = onYes;
            _no = onNo;
            _isAcknowledgement = false;
            _choiceInputSuspended = false;
            _openedFrame = Time.frameCount;
            _openedGestureId = _currentGestureId;
            _inputHeldOnOpen = _interactAction != null && _interactAction.IsPressed();
            _confirmRequested = false;
            choiceMessage.text = message;
            yesButton.interactable = canConfirm;
            RestoreYesButtonPosition();
            noButton.gameObject.SetActive(true);
            yesButton.GetComponentInChildren<TextMeshProUGUI>(true).text = yesLabel;
            noButton.GetComponentInChildren<TextMeshProUGUI>(true).text = noLabel;
            EnsureBlocker();
            if (_blocker != null) _blocker.SetActive(false);
            choicePanel.SetActive(true);
            UpdateCanvasSorting();
            GameManager.Instance?.RefreshInputState();
            return true;
        }

        /// <summary>
        /// 상위 대사 표시 등을 위해 현재 선택창의 입력을 임시 중단합니다.
        /// </summary>
        public void SuspendChoiceInput()
        {
            if (!IsChoiceOpen) return;
            _choiceInputSuspended = true;
            _confirmRequested = false;
            if (yesButton != null) yesButton.interactable = false;
            if (noButton != null) noButton.interactable = false;
        }

        /// <summary>
        /// 중단되었던 선택창 입력을 다시 활성화합니다.
        /// </summary>
        public void ResumeChoiceInput()
        {
            if (!IsChoiceOpen || !_choiceInputSuspended) return;
            _choiceInputSuspended = false;
            _openedFrame = Time.frameCount;
            _openedGestureId = _currentGestureId;
            _inputHeldOnOpen = _interactAction != null && _interactAction.IsPressed();
            if (yesButton != null) yesButton.interactable = true;
            if (noButton != null) noButton.interactable = true;
        }

        /// <summary>
        /// 확인 문구와 단일 버튼 안내를 공용 선택창으로 표시합니다.
        /// requester, message, buttonLabel을 사용하며 표시 성공 여부를 반환합니다.
        /// </summary>
        public bool ShowAcknowledgement(object requester, string message, string buttonLabel = "확인했습니다")
        {
            if (!ShowChoice(requester, message, null, null, buttonLabel)) return false;

            _isAcknowledgement = true;
            _openedGestureId = _currentGestureId;
            _inputHeldOnOpen = (_advanceAction != null && _advanceAction.IsPressed()) ||
                               (_interactAction != null && _interactAction.IsPressed());
            EnsureBlocker();
            if (_blocker != null)
            {
                _blocker.transform.SetAsFirstSibling();
                _blocker.SetActive(true);
            }
            noButton.gameObject.SetActive(false);
            if (_yesButtonRect != null)
            {
                float anchorWidth = _yesAnchorMax.x - _yesAnchorMin.x;
                _yesButtonRect.anchorMin = new Vector2(.5f - anchorWidth * .5f, _yesAnchorMin.y);
                _yesButtonRect.anchorMax = new Vector2(.5f + anchorWidth * .5f, _yesAnchorMax.y);
            }
            UpdateCanvasSorting();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            return true;
        }

        /// <summary>
        /// 요청자가 일치하는 경우 활성화된 선택창을 취소합니다.
        /// requester를 검사하여 현재 소유자와 일치할 때 Cancel을 호출합니다.
        /// </summary>
        public void CancelChoice(object requester)
        {
            if (ReferenceEquals(_owner, requester)) Cancel();
        }

        /// <summary>
        /// 현재 선택창을 취소하고 취소 콜백을 실행합니다.
        /// </summary>
        public void Cancel() => Complete(false);

        /// <summary>
        /// 확인 버튼이 활성화된 상태에서 선택을 승인하고 확인 콜백을 실행합니다.
        /// </summary>
        private void Confirm()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused && _isAcknowledgement) return;
            if (!_choiceInputSuspended && yesButton != null && yesButton.interactable) Complete(true);
        }

        /// <summary>
        /// 선택창 표시를 종료하고 결과에 따라 등록된 콜백을 실행합니다.
        /// accepted가 참이면 yes, 거짓이면 no 콜백을 호출합니다.
        /// </summary>
        private void Complete(bool accepted)
        {
            if (!IsChoiceOpen) return;
            bool wasAcknowledgement = _isAcknowledgement;
            Action callback = accepted ? _yes : _no;
            _owner = null;
            _yes = _no = null;
            _confirmRequested = false;
            _choiceInputSuspended = false;
            _isAcknowledgement = false;
            _inputHeldOnOpen = false;
            if (_blocker != null) _blocker.SetActive(false);
            if (choicePanel != null) choicePanel.SetActive(false);
            RestoreYesButtonPosition();
            if (noButton != null) noButton.gameObject.SetActive(true);
            if (wasAcknowledgement && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            callback?.Invoke();
            GameManager.Instance?.RefreshInputState();
        }

        /// <summary>
        /// 확인 버튼의 앵커 위치를 원래의 2선택 위치로 복원합니다.
        /// </summary>
        private void RestoreYesButtonPosition()
        {
            if (_yesButtonRect == null) return;
            _yesButtonRect.anchorMin = _yesAnchorMin;
            _yesButtonRect.anchorMax = _yesAnchorMax;
        }
    }
}
