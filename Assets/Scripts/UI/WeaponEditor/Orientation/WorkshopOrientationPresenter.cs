using System;
using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 브리지가 매 프레임 읽은 공방 도메인 보기 상태입니다. 카메라·모델 상태를 복사하지 않고
    /// 참조와 그 프레임의 최종 자세만 전달합니다.
    /// </summary>
    public struct WorkshopOrientationState
    {
        public WorkshopPlanarView PlanarView;
        public bool ControlsVisible;
        public bool Planar;
        public bool HasWorkshopPose;
        public Quaternion RelativeRotation;
    }

    /// <summary>
    /// 현재 공방 도메인의 보기 상태(2D/3D·선택 축·입력 점유 게이트)를 View 표시로 사상하고,
    /// View의 모드 전환·축 선택·축 메뉴 요청을 기존 WorkshopPlanarView로 전달하는 Presenter입니다.
    /// </summary>
    public sealed class WorkshopOrientationPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "WorkshopOrientationPresenter";
        private const int AXIS_COUNT = 6;
        private const float SELECTED_AXIS_ANGLE = .5f;

        private readonly WorkshopOrientationView m_view;
        private WorkshopPlanarView m_planarView;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public bool IsInitialized => m_isInitialized;

        public WorkshopOrientationPresenter(WorkshopOrientationView _view)
        {
            m_view = _view;
        }

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
                    Debug.LogError(
                        $"[{PRESENTER_NAME}] Initialize 실패: 필수 의존성 누락 (m_view=False)");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.ModeToggleRequested -= OnModeToggleRequested;
            m_view.ModeToggleRequested += OnModeToggleRequested;
            m_view.AxisSelectRequested -= OnAxisSelectRequested;
            m_view.AxisSelectRequested += OnAxisSelectRequested;
            m_view.AxisMenuToggleRequested -= OnAxisMenuToggleRequested;
            m_view.AxisMenuToggleRequested += OnAxisMenuToggleRequested;
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// View 이벤트 구독과 버튼 리스너를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.ModeToggleRequested -= OnModeToggleRequested;
                m_view.AxisSelectRequested -= OnAxisSelectRequested;
                m_view.AxisMenuToggleRequested -= OnAxisMenuToggleRequested;
                m_view.Dispose();
            }

            m_planarView = null;
            m_isInitialized = false;
        }

        /// <summary>
        /// 도메인 상태를 View 표시 함수로 옮긴다. 표시 값은 매 프레임 전달된 그 값만 사용한다.
        /// </summary>
        public void Refresh(in WorkshopOrientationState _state)
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            // 버튼 이벤트는 다음 LateUpdate 전에도 올 수 있으므로 마지막으로 확인된 WorkshopPlanarView를 사용한다.
            m_planarView = _state.PlanarView;
            m_view.SetControlsVisible(_state.ControlsVisible);
            if (!_state.ControlsVisible)
            {
                m_view.HideOrientation();
                return;
            }

            bool interactable = _state.PlanarView != null && _state.PlanarView.CanChangeView;
            m_view.SetModeCaption(_state.Planar);
            m_view.SetModeInteractable(interactable);
            if (!_state.HasWorkshopPose)
            {
                m_view.HideOrientation();
                return;
            }

            m_view.ShowAxisPose(_state.RelativeRotation, ResolveSelectedAxis(in _state), interactable);
        }

        private void OnModeToggleRequested()
        {
            // 원래 모드 버튼 순서 유지: 축 메뉴을 먼저 닫고 모드 전환을 요청한다.
            m_view.CloseAxisMenu();
            m_planarView?.ToggleMode();
        }

        private void OnAxisSelectRequested(int _index)
        {
            if (_index < 0 || _index >= AXIS_COUNT)
            {
                return;
            }

            m_view.CloseAxisMenu();
            m_planarView?.SelectAxis((WorkshopViewAxis)_index);
        }

        private void OnAxisMenuToggleRequested()
        {
            if (m_planarView != null && m_planarView.CanChangeView)
            {
                m_view.ToggleAxisMenu();
            }
        }

        // 2D에서는 뷰가 소유한 축을, 3D에서는 최종 자세와 일치하는 축을 찾는다. 자세를 UI에 누적하지 않는다.
        private static int ResolveSelectedAxis(in WorkshopOrientationState _state)
        {
            if (_state.Planar)
            {
                return _state.PlanarView != null ? (int)_state.PlanarView.Axis : -1;
            }

            for (int i = 0; i < AXIS_COUNT; i++)
            {
                if (Quaternion.Angle(_state.RelativeRotation,
                    WorkshopPlanarView.CoreRotationInView((WorkshopViewAxis)i)) < SELECTED_AXIS_ANGLE)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
