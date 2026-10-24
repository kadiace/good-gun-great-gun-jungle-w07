using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// WorkshopSession의 3D 작업대 상태(PowerCore, Camera, 2D/3D 보기)를 읽어
    /// 전면(+Z)·상단(+Y) 방향 마크의 3D 투영 좌표와 회전 원의 형상을 계산하고 View에 전달하는 Presenter입니다.
    /// </summary>
    public sealed class WorkshopDirectionMarksPresenter
    {
        private const float OPAQUE_OPACITY = 1f;
        private const float TRANSLUCENT_OPACITY = .2f;

        private readonly WorkshopDirectionMarksView m_view;

        public WorkshopDirectionMarksPresenter(WorkshopDirectionMarksView _view)
        {
            m_view = _view;
        }

        public void Tick(WorkshopSession _session)
        {
            if (m_view == null)
            {
                return;
            }

            bool editing = _session != null && _session.IsEditing;
            PowerCore core = editing && _session.ActiveWorkbench != null ? _session.ActiveWorkbench.CurrentCore : null;
            Camera view = editing ? _session.WorkshopCamera : null;
            bool planar = editing && _session.PlanarView != null && _session.PlanarView.Is2D;

            m_view.SetRotationCircleActive(editing && !planar);
            if (editing && !planar)
            {
                Rect viewport = _session.RotationViewport;
                m_view.SetRotationCircleGeometry(viewport, m_view.GetRotationRadius(viewport));
            }

            if (core == null || view == null || planar)
            {
                m_view.HideMarks();
                return;
            }

            // 2D와 같은 최외곽 면을 사용한다. 모듈의 출력 방향과는 무관하다.
            CubeAssembly assembly = core.GetComponent<CubeAssembly>();
            UpdateMark(m_view.FrontMark, Vector3Int.forward, core, view, assembly, m_view.FrontGraphics, m_view.FrontAlphas);
            UpdateMark(m_view.UpMark, Vector3Int.up, core, view, assembly, m_view.UpGraphics, m_view.UpAlphas);
        }

        private void UpdateMark(
            RectTransform _mark,
            Vector3Int _axis,
            PowerCore _core,
            Camera _view,
            CubeAssembly _assembly,
            UnityEngine.UI.Graphic[] _graphics,
            float[] _alphas)
        {
            if (_mark == null)
            {
                return;
            }

            Vector3 localFace = _assembly != null ? _assembly.GetOutermostFace(_axis) : (Vector3)_axis * .501f;
            Transform space = _assembly != null ? _assembly.transform : _core.transform;
            Vector3 facePoint = space.TransformPoint(localFace);
            float opacity = Vector3.Dot(_view.transform.forward, _core.transform.TransformDirection((Vector3)_axis)) > m_view.DirectionBackThreshold
                ? TRANSLUCENT_OPACITY
                : OPAQUE_OPACITY;

            m_view.UpdateDirectionMark(_mark, facePoint, _view, _graphics, _alphas, opacity);
        }
    }
}
