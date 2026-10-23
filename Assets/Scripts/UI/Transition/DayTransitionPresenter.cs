using System;

using UnityEngine;

namespace WeaponWorkshop
{
    // 날짜 진행과 대화 입력 소유권을 담당하고, 표시·연출은 DayTransitionView에 맡긴다.
    public sealed class DayTransitionPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "DayTransitionPresenter";

        private readonly DayTransitionView m_view;
        private readonly Func<GameManager> m_gameManagerProvider;

        private GameManager m_gameManager;
        private _DayManager m_day;
        private System.Action m_whenCovered;
        private bool m_isInitialized;
        private bool m_isRunning;
        private bool m_isFirstDay;
        private bool m_isTutorial;
        private bool m_skipsNightRaid;
        private bool m_ownsInput;
        private bool m_hasLoggedOwnerError;
        private int m_runId;

        public event System.Action Covered;
        public event System.Action DayTextShown;
        public event System.Action Completed;

        public bool IsRunning
        {
            get
            {
                return m_isRunning;
            }
        }

        public DayTransitionPresenter(DayTransitionView _view, Func<GameManager> _gameManagerProvider)
        {
            m_view = _view;
            m_gameManagerProvider = _gameManagerProvider;
        }

        /// <summary>
        /// View를 초기화하고 연출 진행 이벤트를 연결한다. 중복 호출에 안전하다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null)
            {
                Debug.LogError($"[{PRESENTER_NAME}] Initialize 실패: DayTransitionView 참조가 없습니다.");
                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.CoverReached += OnViewCoverReached;
            m_view.DayTextShown += OnViewDayTextShown;
            m_view.Finished += OnViewFinished;
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 진행 중인 전환을 완료 이벤트와 도메인 콜백 없이 취소하고 연결을 해제한다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.CoverReached -= OnViewCoverReached;
                m_view.DayTextShown -= OnViewDayTextShown;
                m_view.Finished -= OnViewFinished;
            }

            Cancel();
            m_isInitialized = false;
        }

        public bool Begin(_DayManager _day)
        {
            if (!CanBegin() || _day == null || !_day.IsNight || !_day.CanStartNextDay)
            {
                return false;
            }

            return BeginDay(_day, false, false, false);
        }

        public bool BeginFirstDay(_DayManager _day)
        {
            if (!CanBegin() || _day == null || _day.DayNumber != 0)
            {
                return false;
            }

            return BeginDay(_day, true, false, false);
        }

        public bool BeginReturn(_DayManager _day)
        {
            if (!CanBegin() || _day == null || !_day.IsNight || !_day.CanStartNextDay)
            {
                return false;
            }

            return BeginDay(_day, false, true, false);
        }

        public bool BeginSkippedNightRaid(_DayManager _day)
        {
            if (!CanBegin() || _day == null || !_day.IsNight || !_day.CanStartNextDay)
            {
                return false;
            }

            return BeginDay(_day, false, false, true);
        }

        public bool BeginTutorial(System.Action _whenCovered)
        {
            if (!CanBegin() || !TryAcquireOwner())
            {
                return false;
            }

            m_day = null;
            m_whenCovered = _whenCovered;
            m_isFirstDay = false;
            m_isTutorial = true;
            m_skipsNightRaid = false;
            m_isRunning = true;
            m_runId++;
            AcquireInput();

            if (m_view.TryPlayTutorialSequence())
            {
                return true;
            }

            ResetRun();
            ReleaseInput(false);
            return false;
        }

        /// <summary>
        /// 진행 중인 전환을 완료 이벤트 없이 중단하고 소유한 대화 입력을 해제한다.
        /// </summary>
        public void Cancel()
        {
            ResetRun();
            if (m_view != null)
            {
                m_view.Cancel();
            }

            ReleaseInput(false);
        }

        private bool BeginDay(_DayManager _day, bool _firstDay, bool _alreadyCovered, bool _skipsNightRaid)
        {
            if (!TryAcquireOwner())
            {
                return false;
            }

            m_day = _day;
            m_whenCovered = null;
            m_isFirstDay = _firstDay;
            m_isTutorial = false;
            m_skipsNightRaid = _skipsNightRaid;
            m_isRunning = true;
            m_runId++;
            AcquireInput();

            if (m_view.TryPlayDaySequence(_alreadyCovered))
            {
                return true;
            }

            ResetRun();
            ReleaseInput(false);
            return false;
        }

        private bool CanBegin()
        {
            return m_isInitialized && !m_isRunning && m_view != null && m_view.CanPlay;
        }

        private bool TryAcquireOwner()
        {
            m_gameManager = m_gameManagerProvider != null ? m_gameManagerProvider() : null;
            if (m_gameManager != null)
            {
                return true;
            }

            if (!m_hasLoggedOwnerError)
            {
                m_hasLoggedOwnerError = true;
                Debug.LogError($"[{PRESENTER_NAME}] 시작 실패: GameManager.Instance가 없습니다.");
            }

            return false;
        }

        private void AcquireInput()
        {
            m_ownsInput = true;
            if (m_gameManager != null)
            {
                m_gameManager.SetDialogue(true);
            }
        }

        private void ReleaseInput(bool _restoreDialogueOpen)
        {
            GameManager owner = m_gameManager;
            m_gameManager = null;
            if (!m_ownsInput)
            {
                return;
            }

            m_ownsInput = false;
            if (owner != null)
            {
                owner.SetDialogue(_restoreDialogueOpen && DialogueUI.Instance != null && DialogueUI.Instance.IsOpen);
            }
        }

        private void ResetRun()
        {
            m_runId++;
            m_isRunning = false;
            m_isFirstDay = false;
            m_isTutorial = false;
            m_skipsNightRaid = false;
            m_day = null;
            m_whenCovered = null;
        }

        private void OnViewCoverReached()
        {
            if (!m_isRunning)
            {
                return;
            }

            int runId = m_runId;
            if (m_isTutorial)
            {
                System.Action whenCovered = m_whenCovered;
                m_whenCovered = null;
                whenCovered?.Invoke();
                if (!IsRunActive(runId))
                {
                    return;
                }

                if (m_gameManager != null)
                {
                    m_gameManager.SetDialogue(true);
                }

                return;
            }

            Covered?.Invoke();
            if (!IsRunActive(runId))
            {
                return;
            }

            _DayManager day = m_day;
            if (day == null)
            {
                return;
            }

            if (m_isFirstDay)
            {
                day.BeginFirstBusinessDay();
            }
            else if (m_skipsNightRaid)
            {
                day.StartNextDay(skippedNightRaid: true);
            }
            else
            {
                day.StartNextDay();
            }

            if (!IsRunActive(runId))
            {
                return;
            }

            if (m_gameManager != null)
            {
                m_gameManager.SetDialogue(true);
            }

            if (m_view == null)
            {
                return;
            }

            m_view.ShowDayText("Day " + day.DayNumber);
        }

        private void OnViewDayTextShown()
        {
            if (!m_isRunning || m_isTutorial)
            {
                return;
            }

            DayTextShown?.Invoke();
        }

        private void OnViewFinished()
        {
            if (!m_isRunning)
            {
                return;
            }

            bool wasTutorial = m_isTutorial;
            ResetRun();
            ReleaseInput(wasTutorial);

            if (!wasTutorial)
            {
                Completed?.Invoke();
            }
        }

        private bool IsRunActive(int _runId)
        {
            return m_isRunning && m_runId == _runId;
        }
    }
}
