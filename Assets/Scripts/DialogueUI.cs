using System;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using TMPro;

namespace WeaponWorkshop
{
    // 항상 활성인 오브젝트에 붙이고, box만 켜고 끕니다.
    public sealed class DialogueUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject box;
        [SerializeField] private TextMeshProUGUI line;
        [SerializeField] private TextMeshProUGUI speakerLabel;
        [SerializeField] private Button answerButton;
        [SerializeField] private TextMeshProUGUI answerLabel;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        private Action _answered;
        private InputActionMap _controls;
        private InputAction _advanceAction;
        private InputAction _interactAction;
        private int _openedFrame;
        private bool _answerRequested;

        private int _currentGestureId;
        private int _consumedGestureId = -1;
        private int _openedGestureId;
        private int _lastDownFrame = -1;
        private bool _inputHeldOnOpen;
        private ConfirmButtonEventRelay _buttonEventRelay;

        private Canvas _dialogueCanvas;
        private Transform _dialogueBranch;
        private GameObject _blocker;

        public static DialogueUI Instance { get; private set; }
        public bool IsOpen => box != null && box.activeSelf;
        public RectTransform TutorialButton => answerButton != null ? answerButton.transform as RectTransform : null;

        private bool IsTransitioning
        {
            get
            {
                var ui = GameManager.Instance != null ? GameManager.Instance.UI : null;
                var transition = ui != null ? ui.GetComponent<DayTransitionUI>() : null;
                return transition != null && transition.IsTransitioning;
            }
        }

        void Awake()
        {
            Instance = this;
            if (box != null) box.SetActive(false);
            if (answerButton != null)
            {
                _buttonEventRelay = answerButton.GetComponent<ConfirmButtonEventRelay>();
                if (_buttonEventRelay == null)
                {
                    _buttonEventRelay = answerButton.gameObject.AddComponent<ConfirmButtonEventRelay>();
                }
                _buttonEventRelay.Bind(OnAnswerButtonPointerDown, OnAnswerButtonPointerClick, OnAnswerButtonSubmitted);
            }
            EnsureBlocker();
            if (inputActions != null)
            {
                _controls = inputActions.FindActionMap("Workshop", true).Clone();
                _advanceAction = _controls.FindAction("AdvanceDialogue", false);
                if (_advanceAction != null)
                {
                    _advanceAction.performed += OnAdvancePerformed;
                }
                _interactAction = _controls.FindAction("Interact", false);
                if (_interactAction != null)
                {
                    _interactAction.performed += OnAdvancePerformed;
                }
            }
        }

        void OnEnable() => _controls?.Enable();

        void LateUpdate()
        {
            if (_inputHeldOnOpen)
            {
                bool isHeld = (_advanceAction != null && _advanceAction.IsPressed()) ||
                              (_interactAction != null && _interactAction.IsPressed());
                if (!isHeld)
                {
                    _inputHeldOnOpen = false;
                }
            }

            if (!_answerRequested) return;
            _answerRequested = false;
            if (!IsOpen || IsTransitioning) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
            if (Time.frameCount <= _openedFrame) return;
            if (answerButton == null || !answerButton.isActiveAndEnabled || !answerButton.interactable) return;

            var callback = _answered;
            _answered = null;
            callback?.Invoke();
        }

        void OnDisable()
        {
            _controls?.Disable();
            _inputHeldOnOpen = false;
            Hide();
        }

        void OnDestroy()
        {
            if (_advanceAction != null) _advanceAction.performed -= OnAdvancePerformed;
            if (_interactAction != null) _interactAction.performed -= OnAdvancePerformed;
            if (_buttonEventRelay != null) _buttonEventRelay.Bind(null, null, null);
            _controls?.Dispose();
            if (Instance == this) Instance = null;
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
        /// 범용 넘김 또는 상호작용 입력 발생 시 제스처 ID를 부여하고 대사 확인을 요청합니다.
        /// context로부터 입력 이벤트를 전달받아 동일 프레임 중복을 합산하고 확인을 요청합니다.
        /// </summary>
        private void OnAdvancePerformed(InputAction.CallbackContext context)
        {
            RequestAnswerFromGesture(ObtainFrameGestureId());
        }

        /// <summary>
        /// 확인 버튼 포인터 누름 시점에 프레임 제스처 식별자를 발급합니다.
        /// 마우스 좌클릭 범용 넘김과는 동일 프레임 제스처로 병합하고 터치·펜 등 버튼 포인터 입력은 클릭 시 소비할 제스처를 준비합니다.
        /// </summary>
        private void OnAnswerButtonPointerDown()
        {
            if (!IsOpen || IsTransitioning) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
            if (Time.frameCount <= _openedFrame) return;
            if (_inputHeldOnOpen) return;
            if (answerButton == null || !answerButton.isActiveAndEnabled || !answerButton.interactable) return;

            ObtainFrameGestureId();
        }

        /// <summary>
        /// 확인 버튼 포인터 클릭 시 포인터 누름에서 발급된 현재 제스처로 대사 확인을 요청합니다.
        /// mouse down에서 이미 이전 대사를 넘긴 제스처는 이어진 mouse up 클릭에서 중복 소비되지 않습니다.
        /// </summary>
        private void OnAnswerButtonPointerClick()
        {
            RequestAnswerFromGesture(_currentGestureId);
        }

        /// <summary>
        /// 포커스된 확인 버튼에서 UI Submit 이벤트가 발생했을 때 프레임 제스처를 발급하고 대사 확인을 요청합니다.
        /// Enter나 패드 Submit은 새 제스처로 처리하고 Space Submit은 같은 프레임의 Advance 제스처와 병합합니다.
        /// </summary>
        private void OnAnswerButtonSubmitted()
        {
            RequestAnswerFromGesture(ObtainFrameGestureId());
        }

        /// <summary>
        /// 지정된 제스처 식별자에 대해 대사 확인 요건을 검증하고 소비 상태를 갱신합니다.
        /// gestureId를 전달받아 대사창 활성 및 일시정지 상태를 확인한 뒤 확인 요청 플래그를 설정합니다.
        /// </summary>
        private void RequestAnswerFromGesture(int gestureId)
        {
            if (!IsOpen || IsTransitioning) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
            if (Time.frameCount <= _openedFrame) return;
            if (_inputHeldOnOpen) return;
            if (gestureId <= _openedGestureId) return;
            if (gestureId == _consumedGestureId) return;
            if (answerButton == null || !answerButton.isActiveAndEnabled || !answerButton.interactable) return;

            _consumedGestureId = gestureId;
            _answerRequested = true;
        }

        /// <summary>
        /// 빈 화면 클릭이 뒤편 UI로 전달되지 않도록 전체 화면 투명 클릭 차단기를 준비합니다.
        /// </summary>
        private void EnsureBlocker()
        {
            if (_blocker != null) return;
            Transform existing = transform.Find("DialogueClickBlocker");
            if (existing != null)
            {
                _blocker = existing.gameObject;
            }
            else
            {
                var blockerGo = new GameObject("DialogueClickBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                blockerGo.transform.SetParent(transform, false);
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
        /// 대사 UI 가지를 탐색하여 상위 캔버스를 캐시하고 최상위 정렬 순서(150)를 부여합니다.
        /// </summary>
        private void EnsureCanvasSorting()
        {
            if (_dialogueCanvas != null)
            {
                if (_dialogueBranch != null) _dialogueBranch.SetAsLastSibling();
                _dialogueCanvas.overrideSorting = true;
                _dialogueCanvas.sortingOrder = 150;
                return;
            }

            var canvases = (box != null ? box.transform : transform).GetComponentsInParent<Canvas>(true);
            if (canvases == null || canvases.Length == 0) return;

            Canvas rootCanvas = canvases[canvases.Length - 1];
            Transform branch = box != null ? box.transform : transform;
            while (branch.parent != null && branch.parent != rootCanvas.transform)
            {
                branch = branch.parent;
            }

            _dialogueBranch = branch;
            if (_dialogueBranch != rootCanvas.transform)
            {
                _dialogueBranch.SetAsLastSibling();
                _dialogueCanvas = _dialogueBranch.GetComponent<Canvas>();
                if (_dialogueCanvas == null)
                {
                    _dialogueCanvas = _dialogueBranch.gameObject.AddComponent<Canvas>();
                    _dialogueBranch.gameObject.AddComponent<GraphicRaycaster>();
                }
                _dialogueCanvas.overrideSorting = true;
                _dialogueCanvas.sortingOrder = 150;
            }
        }

        /// <summary>
        /// 대사 본문과 확인 버튼 문구를 지정해 화면에 표시하고 완료 콜백을 등록합니다.
        /// text, answer, onAnswer, speaker를 입력받아 대사창을 열고 성공 여부를 반환합니다.
        /// </summary>
        public bool Show(string text, string answer, Action onAnswer, string speaker = "손님")
        {
            if (box == null || line == null || answerButton == null || answerLabel == null)
            {
                Debug.LogError("DialogueUI의 Box/Line/Answer/Label 연결을 확인하세요.", this);
                return false;
            }
            line.text = text;
            if (speakerLabel != null) speakerLabel.text = speaker;
            answerLabel.text = answer;
            _answered = onAnswer;
            _answerRequested = false;
            _openedFrame = Time.frameCount;
            _openedGestureId = _currentGestureId;
            _inputHeldOnOpen = (_advanceAction != null && _advanceAction.IsPressed()) ||
                               (_interactAction != null && _interactAction.IsPressed());

            EnsureBlocker();
            if (_blocker != null)
            {
                _blocker.transform.SetAsFirstSibling();
                _blocker.SetActive(true);
            }
            box.SetActive(true);
            EnsureCanvasSorting();

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            GameManager.Instance?.SetDialogue(true);
            return true;
        }

        /// <summary>
        /// 대사 표시를 닫고 등록된 콜백과 대화 상태를 정리합니다.
        /// </summary>
        public void Hide()
        {
            _answered = null;
            _answerRequested = false;
            _inputHeldOnOpen = false;
            bool wasOpen = IsOpen;
            if (_blocker != null) _blocker.SetActive(false);
            box?.SetActive(false);
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            if (wasOpen) GameManager.Instance?.SetDialogue(false);
        }
    }

    // 확인 버튼의 포인터 누름·클릭과 UI Submit 이벤트를 분리해 전달합니다.
    internal sealed class ConfirmButtonEventRelay : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, ISubmitHandler
    {
        private Action _onPointerDown;
        private Action _onPointerClick;
        private Action _onSubmit;

        /// <summary>
        /// 포인터 누름, 포인터 클릭, UI Submit 콜백을 등록합니다.
        /// onPointerDown, onPointerClick, onSubmit을 전달받아 내부 핸들러에 연결합니다.
        /// </summary>
        public void Bind(Action onPointerDown, Action onPointerClick, Action onSubmit)
        {
            _onPointerDown = onPointerDown;
            _onPointerClick = onPointerClick;
            _onSubmit = onSubmit;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            _onPointerDown?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            _onPointerClick?.Invoke();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            _onSubmit?.Invoke();
        }
    }
}
