using System;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 현재 튜토리얼 단계와 상호작용 대상을 조회해 TutorialTargetView에 표시 대상을 전달하는 Presenter입니다.
    /// 대상 좌표 계산과 문구 배치는 View가 맡고, 여기서는 표시 여부와 라벨만 결정합니다.
    /// </summary>
    public sealed class TutorialTargetPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "TutorialTargetPresenter";
        private const string CUSTOMER_LABEL = "손님";

        private readonly TutorialTargetView m_view;

        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public TutorialTargetPresenter(TutorialTargetView _view)
        {
            m_view = _view;
        }

        public bool IsInitialized
        {
            get
            {
                return m_isInitialized;
            }
        }

        /// <summary>
        /// View를 초기화합니다. 필수 의존성이 없으면 화면만 초기화 실패로 남깁니다.
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
                    Debug.LogError("[" + PRESENTER_NAME + "] Initialize 실패: TutorialTargetView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// View 표시 참조를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.Dispose();
            }

            m_isInitialized = false;
        }

        public void OnDisabled()
        {
            if (m_view != null)
            {
                m_view.Hide();
            }
        }

        /// <summary>
        /// 기존 LateUpdate와 같은 조건으로 대상 표식을 표시하거나 숨깁니다.
        /// </summary>
        public void Tick()
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            TutorialManager tutorial = TutorialManager.Instance;
            if (OnboardingManager.IsShootingGuideVisible)
            {
                GameManager game = GameManager.Instance;
                var player = game != null ? game.Player : null;
                var workshop = player != null ? player.GetComponent<WorkshopSession>() : null;
                Component guidedTarget = OnboardingManager.ShootingTarget;
                if (guidedTarget == null || GameManager.GameplayBlocked || workshop != null && workshop.IsInWorkshop)
                    m_view.Hide();
                else
                    m_view.ShowTarget(guidedTarget, OnboardingManager.ShootingTargetLabel);
                return;
            }
            TutorialStep step = tutorial != null ? tutorial.CurrentStep : null;
            Component target = tutorial != null ? tutorial.CurrentInteractionTarget : null;
            if (tutorial != null && tutorial.IsEditingTutorialWorkbench)
            {
                m_view.Hide();
                return;
            }

            if (step == null || target == null || GameManager.GameplayBlocked)
            {
                m_view.Hide();
                return;
            }

            string title = target is Customer
                ? CUSTOMER_LABEL
                : (string.IsNullOrEmpty(step.targetLabel) ? step.title : step.targetLabel);
            m_view.ShowTarget(target, title);
        }
    }
}
