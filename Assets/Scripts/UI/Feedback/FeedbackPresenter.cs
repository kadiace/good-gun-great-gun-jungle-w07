using System;

using UnityEngine;

namespace WeaponWorkshop
{
    // 확인창의 열림 상태, 요청자 소유 콜백, 제스처 중복 방지, 입력 중단, 포커스 보관과 종료 결과를 관리한다.
    public sealed class FeedbackPresenter : IDisposable
    {
        private readonly FeedbackChoiceView m_choiceView;
        private readonly FeedbackNotificationView m_notificationView;
        private readonly Func<bool> m_isGamePaused;
        private readonly Action m_refreshInputState;

        private bool m_isInitialized;
        private bool m_isOpen;
        private object m_owner;
        private Action m_yes;
        private Action m_no;
        private bool m_isAcknowledgement;
        private bool m_focusYes;
        private bool m_inputSuspended;
        private bool m_canConfirm = true;
        private bool m_confirmRequested;
        private bool m_inputHeldOnOpen;
        private int m_openedFrame;
        private int m_currentGestureId;
        private int m_consumedGestureId = -1;
        private int m_openedGestureId;
        private int m_lastDownFrame = -1;
        private GameObject m_previousFocus;
        private GameObject m_suspendFocus;

        public event Action<object, FeedbackCloseReason> Closed;

        public bool IsOpen => m_isOpen;
        public bool IsChoiceInputSuspended => m_inputSuspended;
        public RectTransform TutorialConfirmButton => m_choiceView != null ? m_choiceView.TutorialConfirmButton : null;

        public FeedbackPresenter(FeedbackChoiceView _choiceView, FeedbackNotificationView _notificationView,
            Func<bool> _isGamePaused, Action _refreshInputState)
        {
            m_choiceView = _choiceView;
            m_notificationView = _notificationView;
            m_isGamePaused = _isGamePaused;
            m_refreshInputState = _refreshInputState;
        }

        /// <summary>
        /// 두 View를 초기화하고 입력·버튼 이벤트를 연결한다. 중복 호출에 안전하다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_choiceView == null || m_notificationView == null)
            {
                Debug.LogError(
                    "[FeedbackPresenter] Initialize 실패: View 참조 누락 " +
                    $"(m_choiceView={m_choiceView != null}, m_notificationView={m_notificationView != null})");
                return false;
            }

            if (!m_notificationView.Initialize())
            {
                return false;
            }

            if (!m_choiceView.Initialize())
            {
                return false;
            }

            m_choiceView.InteractPerformed += OnInteractPerformed;
            m_choiceView.AdvancePerformed += OnAdvancePerformed;
            m_choiceView.YesPointerDown += OnYesPointerDown;
            m_choiceView.YesPointerClick += OnYesPointerClick;
            m_choiceView.YesSubmitted += OnYesSubmitted;
            m_choiceView.NoClicked += OnNoClicked;

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 열린 확인창을 콜백 없이 강제로 닫고 이벤트 연결과 View를 정리한다. 중복 호출에 안전하다.
        /// </summary>
        public void Dispose()
        {
            if (m_isInitialized)
            {
                ForceClose();

                m_choiceView.InteractPerformed -= OnInteractPerformed;
                m_choiceView.AdvancePerformed -= OnAdvancePerformed;
                m_choiceView.YesPointerDown -= OnYesPointerDown;
                m_choiceView.YesPointerClick -= OnYesPointerClick;
                m_choiceView.YesSubmitted -= OnYesSubmitted;
                m_choiceView.NoClicked -= OnNoClicked;
            }

            m_choiceView?.Dispose();
            m_notificationView?.Dispose();
            m_isInitialized = false;
        }

        /// <summary>
        /// 알림을 표시한다. 빈 메시지는 알림을 숨긴다.
        /// </summary>
        public void ShowNotification(string _message)
        {
            m_notificationView?.ShowNotification(_message);
        }

        /// <summary>
        /// 알림 표시를 숨긴다.
        /// </summary>
        public void HideNotification()
        {
            m_notificationView?.HideNotification();
        }

        /// <summary>
        /// 예/아니오 선택창을 연다. 이미 열려 있으면 기존 요청과 화면을 유지하고 false를 반환한다.
        /// </summary>
        public bool ShowChoice(object _requester, string _message, Action _onYes, Action _onNo,
            string _yesLabel, string _noLabel, bool _canConfirm, bool _focusYes = false)
        {
            if (!m_isInitialized && !Initialize())
            {
                return false;
            }

            return OpenChoice(_requester, _message, _onYes, _onNo, _yesLabel, _noLabel, _canConfirm, false, _focusYes);
        }

        /// <summary>
        /// 단일 확인 안내를 연다. 이미 열려 있으면 기존 요청과 화면을 유지하고 false를 반환한다.
        /// </summary>
        public bool ShowAcknowledgement(object _requester, string _message, string _buttonLabel)
        {
            if (!m_isInitialized && !Initialize())
            {
                return false;
            }

            return OpenChoice(_requester, _message, null, null, _buttonLabel, "아니오", true, true);
        }

        /// <summary>
        /// 현재 선택창의 입력을 중단하고 확인 요청을 비운다.
        /// </summary>
        public void SuspendChoiceInput()
        {
            if (!m_isOpen)
            {
                return;
            }

            m_suspendFocus = m_choiceView.GetValidSelectedObject();
            m_inputSuspended = true;
            m_confirmRequested = false;
            m_choiceView.SetButtonInteractivity(false, false);
        }

        /// <summary>
        /// 중단했던 입력을 다시 허용한다. Yes는 원래 canConfirm 값으로 복원하고 중단 전 포커스를 되살린다.
        /// </summary>
        public void ResumeChoiceInput()
        {
            if (!m_isOpen || !m_inputSuspended)
            {
                return;
            }

            m_inputSuspended = false;
            m_openedFrame = Time.frameCount;
            m_openedGestureId = m_currentGestureId;
            m_inputHeldOnOpen = m_choiceView.IsInteractPressed ||
                                m_isAcknowledgement && m_choiceView.IsAdvancePressed;
            m_choiceView.SetButtonInteractivity(m_canConfirm, true);
            m_choiceView.RestoreFocusOrApplyDefault(m_suspendFocus, m_isAcknowledgement || m_focusYes);
            m_suspendFocus = null;
        }

        /// <summary>
        /// 요청자가 현재 소유자와 일치할 때만 취소한다.
        /// </summary>
        public void CancelChoice(object _requester)
        {
            if (ReferenceEquals(m_owner, _requester))
            {
                Cancel();
            }
        }

        /// <summary>
        /// 현재 선택창을 취소하고 취소 콜백을 한 번 실행한다.
        /// </summary>
        public void Cancel()
        {
            Complete(false, FeedbackCloseReason.Cancelled);
        }

        /// <summary>
        /// 현재 확인창을 확인·취소 콜백 없이 강제로 닫고 Forced 결과를 알린다.
        /// </summary>
        public void ForceClose()
        {
            Complete(false, FeedbackCloseReason.Forced);
        }

        /// <summary>
        /// 매 프레임 열림 시 입력 유지 상태를 해제하고 예약된 확인을 처리한다.
        /// </summary>
        public void Tick()
        {
            if (m_inputHeldOnOpen)
            {
                bool isHeld = m_choiceView != null &&
                              (m_choiceView.IsInteractPressed || m_isAcknowledgement && m_choiceView.IsAdvancePressed);
                if (!isHeld)
                {
                    m_inputHeldOnOpen = false;
                }
            }

            if (m_confirmRequested)
            {
                m_confirmRequested = false;
                Confirm();
            }
        }

        private bool OpenChoice(object _requester, string _message, Action _onYes, Action _onNo,
            string _yesLabel, string _noLabel, bool _canConfirm, bool _isAcknowledgement, bool _focusYes = false)
        {
            if (m_isOpen || _requester == null || m_choiceView == null || !m_choiceView.IsInitialized)
            {
                return false;
            }

            m_previousFocus = m_choiceView.GetValidSelectedObject();
            m_owner = _requester;
            m_yes = _onYes;
            m_no = _onNo;
            m_isAcknowledgement = _isAcknowledgement;
            m_focusYes = _focusYes;
            m_inputSuspended = false;
            m_canConfirm = _canConfirm;
            m_confirmRequested = false;
            m_openedFrame = Time.frameCount;
            m_openedGestureId = m_currentGestureId;
            m_inputHeldOnOpen = m_choiceView.IsInteractPressed ||
                                _isAcknowledgement && m_choiceView.IsAdvancePressed;
            m_isOpen = true;

            bool isPaused = m_isGamePaused != null && m_isGamePaused();
            if (!m_choiceView.ShowChoice(_message, _yesLabel, _noLabel, _canConfirm, _isAcknowledgement, isPaused))
            {
                ResetOpenState();
                return false;
            }

            m_choiceView.ApplyDefaultFocus(_isAcknowledgement || _focusYes);
            m_refreshInputState?.Invoke();
            return true;
        }

        private void OnInteractPerformed()
        {
            RequestConfirmFromGesture(ObtainFrameGestureId());
        }

        private void OnAdvancePerformed()
        {
            if (!m_isAcknowledgement)
            {
                return;
            }

            RequestConfirmFromGesture(ObtainFrameGestureId());
        }

        private void OnYesPointerDown()
        {
            if (!CanRequestConfirm())
            {
                return;
            }

            ObtainFrameGestureId();
        }

        private void OnYesPointerClick()
        {
            RequestConfirmFromGesture(m_currentGestureId);
        }

        private void OnYesSubmitted()
        {
            RequestConfirmFromGesture(ObtainFrameGestureId());
        }

        private void OnNoClicked()
        {
            Cancel();
        }

        // 동일 프레임의 입력을 하나의 제스처로 병합한다.
        private int ObtainFrameGestureId()
        {
            if (Time.frameCount != m_lastDownFrame)
            {
                m_lastDownFrame = Time.frameCount;
                m_currentGestureId++;
            }

            return m_currentGestureId;
        }

        private bool CanRequestConfirm()
        {
            if (!m_isOpen || m_inputSuspended)
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

            if (m_choiceView == null || !m_choiceView.IsYesButtonAvailable)
            {
                return false;
            }

            // 일시정지 중에는 뒤에 깔린 단일 안내의 입력을 차단한다.
            if (m_isAcknowledgement && m_isGamePaused != null && m_isGamePaused())
            {
                return false;
            }

            return true;
        }

        private void RequestConfirmFromGesture(int _gestureId)
        {
            if (!CanRequestConfirm())
            {
                return;
            }

            if (_gestureId <= m_openedGestureId || _gestureId == m_consumedGestureId)
            {
                return;
            }

            m_consumedGestureId = _gestureId;
            m_confirmRequested = true;
        }

        private void Confirm()
        {
            if (!m_isOpen)
            {
                return;
            }

            if (m_isAcknowledgement && m_isGamePaused != null && m_isGamePaused())
            {
                return;
            }

            if (m_inputSuspended || !m_choiceView.IsYesButtonAvailable)
            {
                return;
            }

            Complete(true, FeedbackCloseReason.Confirmed);
        }

        // 상태·화면·포커스·게임 입력을 모두 정리한 뒤 결과와 콜백을 알린다.
        // 콜백이 즉시 새 확인창을 열어도 이전 종료 처리가 새 화면을 지우지 않는다.
        private void Complete(bool _accepted, FeedbackCloseReason _reason)
        {
            if (!m_isOpen)
            {
                return;
            }

            object owner = m_owner;
            Action callback = _reason == FeedbackCloseReason.Forced ? null : _accepted ? m_yes : m_no;
            GameObject previousFocus = m_previousFocus;

            ResetOpenState();
            m_choiceView.Hide();
            m_choiceView.RestoreFocus(previousFocus);
            m_refreshInputState?.Invoke();

            Closed?.Invoke(owner, _reason);
            callback?.Invoke();
        }

        private void ResetOpenState()
        {
            m_isOpen = false;
            m_owner = null;
            m_yes = null;
            m_no = null;
            m_isAcknowledgement = false;
            m_focusYes = false;
            m_inputSuspended = false;
            m_canConfirm = true;
            m_confirmRequested = false;
            m_inputHeldOnOpen = false;
            m_previousFocus = null;
            m_suspendFocus = null;
        }
    }
}
