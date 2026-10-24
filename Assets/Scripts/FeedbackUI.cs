using System;

using UnityEngine;

namespace WeaponWorkshop
{
    // 기존 FeedbackUI 공개 API를 유지하며 새 MVP Presenter·View로 전달하는 레거시 브리지다.
    public sealed class FeedbackUI : MonoBehaviour
    {
        [Header("Views")]
        [SerializeField] private FeedbackChoiceView m_choiceView;
        [SerializeField] private FeedbackNotificationView m_notificationView;

        private FeedbackPresenter m_presenter;

        public event Action<object, FeedbackCloseReason> Closed;

        public bool IsChoiceOpen => m_presenter != null && m_presenter.IsOpen;
        public bool IsChoiceInputSuspended => m_presenter != null && m_presenter.IsChoiceInputSuspended;
        public RectTransform TutorialConfirmButton => m_presenter != null ? m_presenter.TutorialConfirmButton : null;

        /// <summary>
        /// 호출 컴포넌트의 부모 캔버스 또는 전역 GameManager에서 FeedbackUI 인스턴스를 찾는다.
        /// </summary>
        public static FeedbackUI For(Component _caller)
        {
            FeedbackUI local = _caller != null ? _caller.GetComponentInParent<FeedbackUI>(true) : null;
            if (local != null)
            {
                return local;
            }

            // 알림 화면이 형제 화면 묶음에 있으면 같은 UIManager 또는 루트 캔버스 아래에서 찾는다.
            UIManager ui = _caller != null ? _caller.GetComponentInParent<UIManager>(true) : null;
            local = ui != null ? ui.Screen<FeedbackUI>() : null;
            if (local != null)
            {
                return local;
            }

            Canvas canvas = _caller != null ? _caller.GetComponentInParent<Canvas>(true) : null;
            local = canvas != null ? canvas.rootCanvas.GetComponentInChildren<FeedbackUI>(true) : null;
            if (local != null)
            {
                return local;
            }

            return GameManager.Instance != null ? GameManager.Instance.UI?.Screen<FeedbackUI>() : null;
        }

        private void Awake()
        {
            m_presenter = new FeedbackPresenter(m_choiceView, m_notificationView, IsGamePaused, RefreshGameInputState);
            m_presenter.Closed += OnPresenterClosed;
            m_presenter.Initialize();
        }

        private void OnDisable()
        {
            m_presenter?.ForceClose();
            m_presenter?.HideNotification();
        }

        private void OnDestroy()
        {
            if (m_presenter != null)
            {
                m_presenter.Closed -= OnPresenterClosed;
                m_presenter.Dispose();
                m_presenter = null;
            }
        }

        private void LateUpdate()
        {
            m_presenter?.Tick();
        }

        /// <summary>
        /// 상단 알림 텍스트를 연출과 함께 표시하고, 빈 메시지면 숨긴다.
        /// </summary>
        public void ShowNotification(string _message)
        {
            m_presenter?.ShowNotification(_message);
        }

        /// <summary>
        /// 예/아니오 선택창을 표시하고 확인·취소 콜백을 등록한다. 이미 열려 있으면 false를 반환한다.
        /// </summary>
        public bool ShowChoice(object _requester, string _message, Action _onYes, Action _onNo = null,
            string _yesLabel = "예", string _noLabel = "아니오", bool _canConfirm = true)
        {
            if (!isActiveAndEnabled || m_presenter == null)
            {
                return false;
            }

            return m_presenter.ShowChoice(_requester, _message, _onYes, _onNo, _yesLabel, _noLabel, _canConfirm);
        }

        /// <summary>
        /// 단일 확인 안내를 표시한다. 이미 열려 있으면 false를 반환한다.
        /// </summary>
        public bool ShowAcknowledgement(object _requester, string _message, string _buttonLabel = "확인했습니다")
        {
            if (!isActiveAndEnabled || m_presenter == null)
            {
                return false;
            }

            return m_presenter.ShowAcknowledgement(_requester, _message, _buttonLabel);
        }

        /// <summary>
        /// 상위 화면을 위해 현재 선택창의 입력을 임시 중단한다.
        /// </summary>
        public void SuspendChoiceInput()
        {
            m_presenter?.SuspendChoiceInput();
        }

        /// <summary>
        /// 중단했던 선택창 입력을 다시 허용한다.
        /// </summary>
        public void ResumeChoiceInput()
        {
            m_presenter?.ResumeChoiceInput();
        }

        /// <summary>
        /// 요청자가 현재 소유자와 일치할 때만 선택창을 취소한다.
        /// 기존 호출부의 상태 정리 부수효과를 유지하기 위해 P2 전까지 Cancel과 같은 의미로 둔다.
        /// </summary>
        public void CancelChoice(object _requester)
        {
            m_presenter?.CancelChoice(_requester);
        }

        /// <summary>
        /// 현재 선택창을 취소하고 취소 콜백을 한 번 실행한다.
        /// </summary>
        public void Cancel()
        {
            m_presenter?.Cancel();
        }

        /// <summary>
        /// 현재 확인창을 확인·취소 콜백 없이 강제로 닫는다. 종료 결과는 Forced로 알린다.
        /// </summary>
        public void ForceClose()
        {
            m_presenter?.ForceClose();
        }

        private bool IsGamePaused()
        {
            return GameManager.Instance != null && GameManager.Instance.IsPaused;
        }

        private void RefreshGameInputState()
        {
            GameManager.Instance?.RefreshInputState();
        }

        private void OnPresenterClosed(object _requester, FeedbackCloseReason _reason)
        {
            Closed?.Invoke(_requester, _reason);
        }
    }
}
