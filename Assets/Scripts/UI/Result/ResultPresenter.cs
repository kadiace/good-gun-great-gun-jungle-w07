using System;

using UnityEngine;

namespace WeaponWorkshop
{
    // 결과 화면의 요청·확인 콜백·게임 입력 소유를 담당하고 표시와 연출은 ResultView에 맡긴다.
    public sealed class ResultPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "ResultPresenter";

        private readonly ResultView m_view;
        private readonly Func<GameManager> m_gameManagerProvider;
        private readonly Action m_hideExtraction;

        private bool m_isInitialized;
        private bool m_isOpen;
        private bool m_isRevealed;
        private bool m_isLeaving;
        private bool m_isFailure;
        private Action m_confirmed;
        private GameManager m_gameManager;
        private bool m_ownsInput;
        private bool m_previousDialogueOpen;
        private GameObject m_previousFocus;
        private int m_runId;
        private bool m_hasLoggedViewError;
        private bool m_hasLoggedInputOwnerError;

        public ResultPresenter(ResultView _view, Func<GameManager> _gameManagerProvider, Action _hideExtraction)
        {
            m_view = _view;
            m_gameManagerProvider = _gameManagerProvider;
            m_hideExtraction = _hideExtraction;
        }

        /// <summary>
        /// View를 초기화하고 확인·연출 이벤트를 연결한다. 중복 호출에 안전하다.
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
                    Debug.LogError($"[{PRESENTER_NAME}] Initialize 실패: ResultView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.ConfirmRequested += OnConfirmRequested;
            m_view.CoverReached += OnViewCoverReached;
            m_view.Revealed += OnViewRevealed;
            m_view.Hidden += OnViewHidden;

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 열린 결과를 확인 콜백 없이 강제로 닫고 입력·포커스·이벤트 연결을 정리한다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                ForceClose();
                m_view.ConfirmRequested -= OnConfirmRequested;
                m_view.CoverReached -= OnViewCoverReached;
                m_view.Revealed -= OnViewRevealed;
                m_view.Hidden -= OnViewHidden;
                m_view.Dispose();
            }

            m_isInitialized = false;
        }

        public bool ShowRaidResult(bool _died, string _summary, Action _confirmed)
        {
            return ShowResult(
                _died,
                _died ? "당신은 죽었습니다" : "귀환 성공",
                _died ? new Color(1f, .22f, .18f) : new Color(.2f, 1f, .45f),
                _died ? "이번 습격에서 획득한 모든 재화는 가져갈 수 없습니다." : _summary,
                _confirmed);
        }

        public bool ShowBankruptcyResult(Action _confirmed)
        {
            return ShowResult(
                true,
                "가게를 내놓았습니다",
                new Color(1f, .22f, .18f),
                "자릿세를 내지 못해 가게를 내놓게 되었습니다.",
                _confirmed);
        }

        private bool ShowResult(bool _failure, string _title, Color _titleColor, string _body, Action _confirmed)
        {
            if (!m_isInitialized && !Initialize())
            {
                return false;
            }

            if (m_isOpen && !_failure)
            {
                return false;
            }

            if (_confirmed == null)
            {
                return false;
            }

            int runId = ++m_runId;
            if (!AcquireInput())
            {
                return false;
            }

            if (!m_isOpen)
            {
                m_previousFocus = m_view.GetValidSelectedObject();
            }

            if (_failure)
            {
                m_hideExtraction?.Invoke();
                if (!IsRunActive(runId))
                {
                    return false;
                }
            }

            m_isFailure = _failure;
            m_confirmed = _confirmed;
            m_isOpen = true;
            m_isRevealed = false;
            m_isLeaving = false;

            if (m_view.TryShow(_title, _titleColor, _body))
            {
                return true;
            }

            RejectShow();
            return false;
        }

        private bool AcquireInput()
        {
            if (m_ownsInput)
            {
                return true;
            }

            GameManager gameManager = m_gameManagerProvider != null ? m_gameManagerProvider() : null;
            if (gameManager == null)
            {
                if (!m_hasLoggedInputOwnerError)
                {
                    m_hasLoggedInputOwnerError = true;
                    Debug.LogError($"[{PRESENTER_NAME}] 입력 소유 실패: GameManager.Instance가 없습니다.");
                }

                return false;
            }

            m_gameManager = gameManager;
            m_previousDialogueOpen = gameManager.InDialogue;
            m_ownsInput = true;
            gameManager.SetDialogue(true);
            return true;
        }

        private void ReleaseInput()
        {
            GameManager gameManager = m_gameManager;
            bool ownsInput = m_ownsInput;
            bool previousDialogueOpen = m_previousDialogueOpen;
            m_gameManager = null;
            m_ownsInput = false;
            m_previousDialogueOpen = false;

            if (!ownsInput || gameManager == null)
            {
                return;
            }

            gameManager.SetDialogue(previousDialogueOpen);
        }

        private void OnConfirmRequested()
        {
            if (!m_isInitialized || !m_isOpen || !m_isRevealed || m_isLeaving)
            {
                return;
            }

            m_isLeaving = true;
            if (!m_view.BeginHide())
            {
                m_isLeaving = false;
            }
        }

        private void OnViewCoverReached()
        {
            if (!m_isInitialized || !m_isOpen || m_isFailure)
            {
                return;
            }

            int runId = m_runId;
            m_hideExtraction?.Invoke();
            if (!IsRunActive(runId))
            {
                return;
            }
        }

        private void OnViewRevealed()
        {
            if (!m_isInitialized || !m_isOpen || m_isLeaving)
            {
                return;
            }

            m_isRevealed = true;
            m_view.ApplyConfirmFocus();
        }

        private void OnViewHidden()
        {
            if (!m_isInitialized || !m_isOpen || !m_isLeaving)
            {
                return;
            }

            CompleteConfirmation();
        }

        // 확인 콜백은 한 번만 실행한다. 검은 배경은 씬 복귀가 덮을 때까지 그대로 둔다.
        private void CompleteConfirmation()
        {
            Action confirmed = m_confirmed;
            m_confirmed = null;
            m_isOpen = false;
            m_isRevealed = false;
            m_isLeaving = false;
            m_isFailure = false;
            m_previousFocus = null;
            m_runId++;
            confirmed?.Invoke();
        }

        private void RejectShow()
        {
            GameObject previousFocus = m_previousFocus;
            m_previousFocus = null;
            m_confirmed = null;
            m_isOpen = false;
            m_isRevealed = false;
            m_isLeaving = false;
            m_isFailure = false;
            m_runId++;
            m_view.Cancel();
            m_view.RestoreFocus(previousFocus);

            ReleaseInput();
        }

        private void ForceClose()
        {
            bool wasOpen = m_isOpen;
            GameObject previousFocus = m_previousFocus;
            m_confirmed = null;
            m_isOpen = false;
            m_isRevealed = false;
            m_isLeaving = false;
            m_isFailure = false;
            m_previousFocus = null;
            m_runId++;

            m_view.Cancel();
            if (wasOpen)
            {
                m_view.RestoreFocus(previousFocus);
            }

            ReleaseInput();
        }

        private bool IsRunActive(int _runId)
        {
            return m_isInitialized && m_runId == _runId;
        }
    }
}
