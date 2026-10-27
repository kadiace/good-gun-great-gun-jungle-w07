using System;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// CubeAssembly의 구조 판정을 읽어 상승 에지에서만 기존 FeedbackUI 알림을 요청하는 Presenter입니다.
    /// 현재 표시 상태는 복제하지 않고 상승 에지 판정용 이전 값만 유지합니다.
    /// </summary>
    public sealed class StructuralFailureWarningPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "StructuralFailureWarningPresenter";

        private readonly StructuralFailureWarningView m_view;
        private readonly CubeAssembly m_assembly;

        private bool m_wasVisible;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public StructuralFailureWarningPresenter(StructuralFailureWarningView _view, CubeAssembly _assembly)
        {
            m_view = _view;
            m_assembly = _assembly;
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
                        $"[{PRESENTER_NAME}] Initialize 실패: StructuralFailureWarningView 참조가 없습니다.");
                }

                return false;
            }

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 매 프레임 구조 상태를 조회해 경고가 새로 나타난 순간에만 알림을 요청합니다.
        /// </summary>
        public void Tick(string _message)
        {
            if (!m_isInitialized)
            {
                return;
            }

            bool visible = m_assembly != null && m_assembly.isActiveAndEnabled && m_assembly.HasStructuralFailure();
            if (visible && !m_wasVisible)
            {
                m_view.Show(_message);
            }

            m_wasVisible = visible;
        }

        /// <summary>
        /// 표시 상태를 숨김으로 되돌립니다. 다시 경고가 나타나면 알림을 한 번 더 요청합니다.
        /// </summary>
        public void ResetVisible()
        {
            m_wasVisible = false;
        }

        public void Dispose()
        {
            m_wasVisible = false;
            m_isInitialized = false;
        }
    }
}
