using UnityEngine;

namespace WeaponWorkshop
{
    // 주문 요구사항 HUD 브리지. 기존 직렬화 참조와 공개 API를 유지하고 View·Presenter를 연결한다.
    [RequireComponent(typeof(UIManager))]
    public sealed class CustomerRequirementsUI : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform rowTemplate;
        [SerializeField] private QuestInfoTextSizing textSizing;
        [SerializeField] private CustomerRequirementsView m_view;

        private Bell m_bell;
        private CustomerRequirementsPresenter m_presenter;
        private bool m_hasAttemptedInitialization;
        private bool m_hasLoggedReferenceError;

        public bool HasActiveOrder
        {
            get
            {
                return m_bell != null && m_bell.Current != null && m_bell.Current.HasActiveOrder;
            }
        }

        public void Bind(Bell source)
        {
            m_bell = source;
            EnsureInitialized();
            if (m_presenter != null)
            {
                m_presenter.Bind(source);
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void LateUpdate()
        {
            if (m_presenter == null)
            {
                EnsureInitialized();
            }

            if (m_presenter != null)
            {
                m_presenter.Tick();
            }
        }

        private void OnDisable()
        {
            if (m_presenter != null)
            {
                m_presenter.HandleDisabled();
            }
        }

        private void OnDestroy()
        {
            if (m_presenter != null)
            {
                m_presenter.Dispose();
                m_presenter = null;
            }
        }

        private void EnsureInitialized()
        {
            if (m_presenter != null || m_hasAttemptedInitialization)
            {
                return;
            }

            m_hasAttemptedInitialization = true;
            if (m_view == null)
            {
                if (!m_hasLoggedReferenceError)
                {
                    m_hasLoggedReferenceError = true;
                    Debug.LogError(
                        "[CustomerRequirementsUI] Initialize 실패: m_view 참조 누락. " +
                        "UIManager.prefab의 HoverOrder 저작을 실행하세요.", this);
                }

                return;
            }

            m_view.BindReferences(content, rowTemplate, textSizing);
            m_presenter = new CustomerRequirementsPresenter(m_view, GetComponent<UIManager>());
            if (m_presenter.Initialize())
            {
                m_presenter.Bind(m_bell);
            }
            else
            {
                m_presenter = null;
            }
        }
    }
}
