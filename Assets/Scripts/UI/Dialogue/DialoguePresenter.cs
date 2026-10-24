using System;

using UnityEngine;

namespace WeaponWorkshop
{
    // 대화 콜백, 제스처 중복 방지, 열린 프레임·입력 유지 가드와 게임 대화 상태 요청을 담당한다.
    public sealed class DialoguePresenter : IDisposable
    {
        private const string PRESENTER_NAME = "DialoguePresenter";

        private readonly DialogueView m_view;
        private readonly Func<GameManager> m_gameManagerProvider;

        private Action m_answered;
        private bool m_isInitialized;
        private bool m_hasLoggedViewError;
        private bool m_answerRequested;
        private bool m_inputHeldOnOpen;
        // 튜토리얼 확인 단계처럼 화면 어디를 클릭해도 확인하는 대화(develop confirmOnBackgroundClick).
        private bool m_confirmOnAnyClick;
        private bool m_wasInputBlocked;
        private int m_openedFrame;
        private float m_inputLockedUntil;
        private int m_currentGestureId;
        private int m_consumedGestureId = -1;
        private int m_openedGestureId;
        private int m_lastDownFrame = -1;

        public bool IsInitialized => m_isInitialized;
        public bool IsOpen => m_view != null && m_view.IsOpen;

        public DialoguePresenter(DialogueView _view, Func<GameManager> _gameManagerProvider)
        {
            m_view = _view;
            m_gameManagerProvider = _gameManagerProvider;
        }

        /// <summary>
        /// View를 초기화하고 입력·버튼 이벤트를 연결한다. 중복 호출에 안전하다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null)
            {
                if (!m_hasLoggedViewError)
                {
                    m_hasLoggedViewError = true;
                    Debug.LogError($"[{PRESENTER_NAME}] Initialize 실패: DialogueView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.InteractPerformed -= OnInteractPerformed;
            m_view.InteractPerformed += OnInteractPerformed;
            m_view.AdvancePerformed -= OnAdvancePerformed;
            m_view.AdvancePerformed += OnAdvancePerformed;
            m_view.ClickPerformed -= OnClickPerformed;
            m_view.ClickPerformed += OnClickPerformed;
            m_view.AnswerPointerDown -= OnAnswerPointerDown;
            m_view.AnswerPointerDown += OnAnswerPointerDown;
            m_view.AnswerPointerClick -= OnAnswerPointerClick;
            m_view.AnswerPointerClick += OnAnswerPointerClick;
            m_view.AnswerSubmitted -= OnAnswerSubmitted;
            m_view.AnswerSubmitted += OnAnswerSubmitted;

            m_isInitialized = true;
            m_wasInputBlocked = RefreshViewState();
            return true;
        }

        /// <summary>
        /// 열린 대화를 콜백 없이 닫고 이벤트 연결과 View를 정리한다. 중복 호출에 안전하다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.InteractPerformed -= OnInteractPerformed;
                m_view.AdvancePerformed -= OnAdvancePerformed;
                m_view.ClickPerformed -= OnClickPerformed;
                m_view.AnswerPointerDown -= OnAnswerPointerDown;
                m_view.AnswerPointerClick -= OnAnswerPointerClick;
                m_view.AnswerSubmitted -= OnAnswerSubmitted;
                Hide();
                m_view.Dispose();
            }

            m_answered = null;
            m_answerRequested = false;
            m_inputHeldOnOpen = false;
            m_inputLockedUntil = 0f;
            m_wasInputBlocked = false;
            m_isInitialized = false;
        }

        /// <summary>
        /// 대화를 표시한다. 이미 열려 있으면 화면과 콜백을 새 요청으로 교체하고 큐에 넣지 않는다.
        /// </summary>
        public bool Show(string _text, string _answer, Action _onAnswer, string _speaker, bool _confirmOnBackgroundClick = false, float _inputLockSeconds = 0f)
        {
            if (!m_isInitialized && !Initialize())
            {
                return false;
            }

            bool wasOpen = IsOpen;
            if (!m_view.Show(_text, _answer, _speaker))
            {
                return false;
            }

            bool isInputBlocked = RefreshViewState();

            m_answered = _onAnswer;
            m_confirmOnAnyClick = _confirmOnBackgroundClick;
            m_answerRequested = false;
            m_openedFrame = Time.frameCount;
            m_openedGestureId = m_currentGestureId;
            m_inputLockedUntil = Time.unscaledTime + Mathf.Max(0f, _inputLockSeconds);
            m_inputHeldOnOpen = m_view.IsInteractPressed ||
                                m_view.IsAdvancePressed ||
                                (m_confirmOnAnyClick && m_view.IsClickPressed);
            m_wasInputBlocked = isInputBlocked;

            if (!wasOpen && !isInputBlocked)
            {
                m_view.ApplyDefaultFocus();
            }

            SetDialogue(true);
            return true;
        }

        /// <summary>
        /// 대화 표시를 닫고 등록된 콜백을 실행하지 않은 채 비운다.
        /// </summary>
        public void Hide()
        {
            m_confirmOnAnyClick = false;
            m_answered = null;
            m_answerRequested = false;
            m_inputHeldOnOpen = false;
            m_inputLockedUntil = 0f;

            bool wasOpen = IsOpen;
            m_view?.Hide();
            if (wasOpen)
            {
                SetDialogue(false);
            }
        }

        /// <summary>
        /// 입력 유지 가드를 풀고 예약된 확인을 LateUpdate 시점에 실행한다.
        /// 콜백은 먼저 비우므로 콜백이 다음 대사를 표시해도 새 호출이 유지된다.
        /// </summary>
        public void Tick()
        {
            if (!m_isInitialized)
            {
                return;
            }

            if (m_inputHeldOnOpen)
            {
                bool isHeld = m_view.IsInteractPressed ||
                              m_view.IsAdvancePressed ||
                              (m_confirmOnAnyClick && m_view.IsClickPressed);
                if (!isHeld)
                {
                    m_inputHeldOnOpen = false;
                }
            }

            bool isInputBlocked = RefreshViewState();
            if (m_wasInputBlocked && !isInputBlocked && IsOpen)
            {
                m_view.RestoreFocusIfUnowned();
            }

            m_wasInputBlocked = isInputBlocked;

            if (!m_answerRequested)
            {
                return;
            }

            m_answerRequested = false;
            if (!IsOpen || isInputBlocked || Time.unscaledTime < m_inputLockedUntil)
            {
                return;
            }

            if (Time.frameCount <= m_openedFrame)
            {
                return;
            }

            if (!m_view.IsAnswerAvailable)
            {
                return;
            }

            Action callback = m_answered;
            m_answered = null;
            callback?.Invoke();
        }

        private void OnInteractPerformed()
        {
            RequestAnswerFromGesture(ObtainFrameGestureId());
        }

        private void OnAdvancePerformed()
        {
            RequestAnswerFromGesture(ObtainFrameGestureId());
        }

        private void OnClickPerformed()
        {
            if (m_confirmOnAnyClick)
            {
                RequestAnswerFromGesture(ObtainFrameGestureId());
            }
        }

        private void OnAnswerPointerDown()
        {
            if (!CanRequestAnswer())
            {
                return;
            }

            ObtainFrameGestureId();
        }

        private void OnAnswerPointerClick()
        {
            RequestAnswerFromGesture(m_currentGestureId);
        }

        private void OnAnswerSubmitted()
        {
            RequestAnswerFromGesture(ObtainFrameGestureId());
        }

        // 동일 프레임의 포인터·Submit·범용 입력을 하나의 제스처로 병합한다.
        private int ObtainFrameGestureId()
        {
            if (Time.frameCount != m_lastDownFrame)
            {
                m_lastDownFrame = Time.frameCount;
                m_currentGestureId++;
            }

            return m_currentGestureId;
        }

        private bool CanRequestAnswer()
        {
            if (!m_isInitialized || !IsOpen)
            {
                return false;
            }

            if (IsInputBlocked())
            {
                return false;
            }

            if (Time.unscaledTime < m_inputLockedUntil)
            {
                return false;
            }

            if (Time.frameCount <= m_openedFrame)
            {
                return false;
            }

            if (m_inputHeldOnOpen)
            {
                return false;
            }

            return m_view.IsAnswerAvailable;
        }

        private void RequestAnswerFromGesture(int _gestureId)
        {
            if (!CanRequestAnswer())
            {
                return;
            }

            if (_gestureId <= m_openedGestureId || _gestureId == m_consumedGestureId)
            {
                return;
            }

            m_consumedGestureId = _gestureId;
            m_answerRequested = true;
        }

        private GameManager ResolveGameManager()
        {
            return m_gameManagerProvider != null ? m_gameManagerProvider() : null;
        }

        // 상위 UI 상태는 Presenter가 계산하고 View에는 차단·겹침 상태만 전달한다.
        private bool RefreshViewState()
        {
            GameManager gameManager = ResolveGameManager();
            bool isInputBlocked = IsInputBlocked(gameManager);
            m_view.SetInputBlocked(isInputBlocked);
            m_view.SetSuspendedChoiceOverlay(IsOpen && IsSuspendedChoiceOverlay(gameManager));
            return isInputBlocked;
        }

        // 일시정지·날짜 전환·입력을 소유한 상위 선택창 뒤에서는 대화를 진행하지 않는다.
        // 선택 입력이 Suspend된 대출 안내 대화는 상위 대화가 입력을 소유하므로 예외로 둔다.
        private bool IsInputBlocked()
        {
            return IsInputBlocked(ResolveGameManager());
        }

        private static bool IsInputBlocked(GameManager _gameManager)
        {
            if (_gameManager == null)
            {
                return false;
            }

            if (_gameManager.IsPaused)
            {
                return true;
            }

            UIManager ui = _gameManager.UI;
            if (ui == null)
            {
                return false;
            }

            DayTransitionUI transition = ui.GetComponent<DayTransitionUI>();
            if (transition != null && transition.IsTransitioning)
            {
                return true;
            }

            return ui.IsChoiceOpen && (ui.Feedback == null || !ui.Feedback.IsChoiceInputSuspended);
        }

        // 대출 안내처럼 Feedback 선택 입력이 중단된 동안에는 대화가 선택창 위에 겹치는 기존 연출을 유지한다.
        private static bool IsSuspendedChoiceOverlay(GameManager _gameManager)
        {
            UIManager ui = _gameManager != null ? _gameManager.UI : null;
            return ui != null &&
                   ui.IsChoiceOpen &&
                   ui.Feedback != null &&
                   ui.Feedback.IsChoiceInputSuspended;
        }

        private void SetDialogue(bool _open)
        {
            GameManager gameManager = ResolveGameManager();
            if (gameManager != null)
            {
                gameManager.SetDialogue(_open);
            }
        }
    }
}
