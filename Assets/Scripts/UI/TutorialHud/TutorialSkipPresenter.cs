using System;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 튜토리얼 건너뛰기 패널의 표시·버튼 선택 콜백·날짜 전환 경로·대화 입력 소유를 기존 순서대로 처리하는 Presenter입니다.
    /// 화면 표시는 TutorialSkipView가 맡습니다.
    /// </summary>
    public sealed class TutorialSkipPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "TutorialSkipPresenter";

        private readonly TutorialSkipView m_view;
        private readonly DayTransitionUI m_dayTransition;

        private Action m_skip;
        private Action m_begin;
        private bool m_ownsInput;
        private bool m_transitioning;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public TutorialSkipPresenter(TutorialSkipView _view, DayTransitionUI _dayTransition)
        {
            m_view = _view;
            m_dayTransition = _dayTransition;
        }

        public bool IsInitialized
        {
            get
            {
                return m_isInitialized;
            }
        }

        public bool IsOpen
        {
            get
            {
                return m_view != null && m_view.IsOpen;
            }
        }

        /// <summary>
        /// View를 초기화하고 건너뛰기·시작 버튼 이벤트를 구독합니다. 중복 호출에 안전합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError("[" + PRESENTER_NAME + "] Initialize 실패: TutorialSkipView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.SkipRequested -= OnSkipRequested;
            m_view.SkipRequested += OnSkipRequested;
            m_view.TutorialRequested -= OnTutorialRequested;
            m_view.TutorialRequested += OnTutorialRequested;
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// View 이벤트·날짜 전환 구독과 대화 입력 소유를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.SkipRequested -= OnSkipRequested;
                m_view.TutorialRequested -= OnTutorialRequested;
            }

            if (m_dayTransition != null)
            {
                m_dayTransition.Covered -= HideCoveredSelection;
            }

            Close();
            m_transitioning = false;
            if (m_view != null)
            {
                m_view.Dispose();
            }

            m_isInitialized = false;
        }

        /// <summary>
        /// 패널을 열고 선택 콜백을 보관합니다. 이미 열려 있거나 컴포넌트가 비활성이면 false를 반환합니다.
        /// </summary>
        public bool Show(Action _onSkip, Action _onTutorial)
        {
            if (!m_isInitialized && !Initialize())
            {
                return false;
            }

            if (!m_view.isActiveAndEnabled || m_view.IsOpen)
            {
                return false;
            }

            m_skip = _onSkip;
            m_begin = _onTutorial;
            m_transitioning = false;
            m_view.SetInteractable(true);
            m_view.ShowPanel();
            m_ownsInput = GameManager.Instance != null;
            if (m_ownsInput)
            {
                GameManager.Instance.SetDialogue(true);
            }

            return true;
        }

        /// <summary>
        /// 브리지 비활성화 시 기존 OnDisable처럼 패널을 닫고 입력 소유를 해제합니다.
        /// </summary>
        public void HandleDisabled()
        {
            Close();
        }

        private void OnSkipRequested()
        {
            Complete(true);
        }

        private void OnTutorialRequested()
        {
            Complete(false);
        }

        private void Complete(bool _skipped)
        {
            if (!m_view.IsOpen || m_transitioning)
            {
                return;
            }

            m_transitioning = true;
            m_view.SetInteractable(false);
            Action callback = _skipped ? m_skip : m_begin;
            if (m_dayTransition == null)
            {
                Close();
                callback?.Invoke();
                return;
            }

            if (_skipped)
            {
                m_dayTransition.Covered += HideCoveredSelection;
                callback?.Invoke();
            }
            else if (!m_dayTransition.BeginTutorial(() =>
                {
                    Close();
                    callback?.Invoke();
                }))
            {
                m_transitioning = false;
                m_view.SetInteractable(true);
            }
        }

        private void HideCoveredSelection()
        {
            if (m_dayTransition != null)
            {
                m_dayTransition.Covered -= HideCoveredSelection;
            }

            // 날짜 전환 가림막이 입력을 이어받으므로 여기서는 대화 입력을 해제하지 않는다.
            m_ownsInput = false;
            Close();
        }

        private void Close()
        {
            if (m_view != null)
            {
                m_view.HidePanel();
            }

            m_skip = null;
            m_begin = null;
            if (m_ownsInput && GameManager.Instance != null)
            {
                GameManager.Instance.SetDialogue(false);
            }

            m_ownsInput = false;
        }
    }
}
