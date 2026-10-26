using UnityEngine;

namespace WeaponWorkshop
{
    // PopupLayer/FeedbackScreen의 구조 경고 브리지. 기존 직렬화 필드(assembly, message)와 씬 오버라이드를 유지하고
    // 상승 에지 알림 판정은 StructuralFailureWarningPresenter가 담당한다.
    public sealed class StructuralFailureWarning : MonoBehaviour
    {
        [SerializeField] private CubeAssembly assembly;
        [SerializeField] private string message = "연결되지 않은 모듈이 있습니다.";
        [SerializeField] private StructuralFailureWarningView m_view;

        private StructuralFailureWarningPresenter m_presenter;

        private void Awake()
        {
            if (m_view == null)
            {
                Debug.LogError(
                    "[StructuralFailureWarning] Initialize 실패: m_view에 StructuralFailureWarningView 참조가 필요합니다. " +
                    "UIManager.prefab의 같은 오브젝트에 View를 추가하고 연결하세요.", this);
                return;
            }

            m_presenter = new StructuralFailureWarningPresenter(m_view, assembly);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
                return;
            }

            m_presenter.ResetVisible();
        }

        private void OnDisable()
        {
            m_presenter?.ResetVisible();
        }

        private void OnDestroy()
        {
            if (m_presenter != null)
            {
                m_presenter.Dispose();
                m_presenter = null;
            }
        }

        private void Update()
        {
            m_presenter?.Tick(message);
        }
    }
}
