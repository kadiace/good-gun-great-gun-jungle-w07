using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 공방 3D 방향 마크(frontMark·upMark)와 회전 원(rotationCircle)을 관리하는 UI 브리지입니다.
    /// IUIScreen을 구현하여 UIContext 주입을 통해 WorkshopSession을 찾아 양방향 바인딩을 형성합니다.
    /// 세션이 없는 습격 씬 등에서는 LateUpdate를 즉시 탈출해 프레임 낭비를 방지합니다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class WorkshopDirectionMarksUI : MonoBehaviour, IUIScreen
    {
        [SerializeField] private WorkshopDirectionMarksView m_view;

        [Header("Authoring Fallback (프리팹 직렬화 값 보존)")]
        [SerializeField] private RotationCircleGraphic rotationCircle;
        [SerializeField] private RectTransform frontMark;
        [SerializeField] private RectTransform upMark;
        [SerializeField, Range(0f, 1f), Tooltip("이 값보다 확실히 뒤를 향할 때만 표시를 흐리게 합니다.")]
        private float directionBackThreshold = .05f;
        [SerializeField, Range(0f, 1f)] private float rotationCircleRadius = .5f;

        private WorkshopSession m_session;
        private WorkshopDirectionMarksPresenter m_presenter;

        public WorkshopDirectionMarksView View => m_view;

        private void Awake()
        {
            EnsureViewAndPresenter();
            SetWorkshopUI(false);
        }

        public void InitializeScreen(UIContext _context)
        {
            EnsureViewAndPresenter();
            FirstPersonPlayer player = _context != null ? _context.Player : null;
            m_session = player != null ? player.GetComponent<WorkshopSession>() : null;
            if (m_session == null)
            {
                m_session = FindFirstObjectByType<WorkshopSession>();
            }

            if (m_session != null)
            {
                m_session.BindWorkshopUI(this);
            }
        }

        public float GetWorkshopRotationRadius(Rect _viewport)
        {
            if (m_view != null)
            {
                return m_view.GetRotationRadius(_viewport);
            }

            return Mathf.Min(_viewport.width, _viewport.height) * rotationCircleRadius;
        }

        public void SetWorkshopUI(bool _editing)
        {
            if (this == null) return;
            EnsureViewAndPresenter();
            bool is2D = m_session != null && m_session.PlanarView != null && m_session.PlanarView.Is2D;
            if (m_view != null)
            {
                m_view.SetWorkshopUI(_editing, is2D);
            }
        }

        private void LateUpdate()
        {
            if (m_session == null)
            {
                return;
            }

            m_presenter?.Tick(m_session);
        }

        private void OnDisable()
        {
            // 종료 중에는 기존 View만 숨기고 새 컴포넌트를 생성하지 않는다.
            if (m_view != null) m_view.SetWorkshopUI(false, false);
        }

        private void OnDestroy()
        {
            if (m_session != null) m_session.UnbindWorkshopUI(this);
            m_session = null;
        }

        private void EnsureViewAndPresenter()
        {
            if (m_view == null)
            {
                m_view = GetComponent<WorkshopDirectionMarksView>();
                if (m_view == null)
                {
                    m_view = GetComponentInChildren<WorkshopDirectionMarksView>(true);
                }

                if (m_view == null)
                {
                    m_view = gameObject.AddComponent<WorkshopDirectionMarksView>();
                }
            }

            if (m_view != null)
            {
                m_view.Configure(rotationCircle, frontMark, upMark, directionBackThreshold, rotationCircleRadius);
                m_view.Initialize();
            }

            if (m_presenter == null && m_view != null)
            {
                m_presenter = new WorkshopDirectionMarksPresenter(m_view);
            }
        }
    }
}
