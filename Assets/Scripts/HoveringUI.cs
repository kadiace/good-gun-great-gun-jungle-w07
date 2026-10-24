using UnityEngine;

namespace WeaponWorkshop
{
    // 호버·스캔 HUD 브리지. 기존 직렬화 필드와 공개 API를 유지하고 View·Presenter를 연결한다.
    [DefaultExecutionOrder(60)]
    public sealed class HoveringUI : MonoBehaviour, IUIScreen
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMPro.TMP_Text title;
        [SerializeField] private TMPro.TMP_Text description;
        [SerializeField] private GameObject interact;
        [SerializeField] private ModulePaletteUI palette;
        [SerializeField] private RectTransform textArea;
        [SerializeField] private RectTransform point1;
        [SerializeField] private RectTransform point2;
        [SerializeField] private RectTransform connector;
        [SerializeField, Min(0f)] private float connectorWidth = 3f;
        [SerializeField] private Vector2 textOffset = new Vector2(30f, 30f);
        [SerializeField, Min(0f)] private float screenPadding = 9f;
        [SerializeField] private WorkshopTagsUI tagSource;
        [SerializeField] private TMPro.TMP_Text scanModeText;
        [SerializeField, Min(1f)] private float scanLoadingDiameter = 67.5f;
        [SerializeField, Min(0.1f)] private float scanLoadingThickness = 2.25f;
        [SerializeField, Min(0f)] private float scanTextRevealDuration = 0.25f;
        [SerializeField] private RectTransform tagGrid;
        [SerializeField] private RectTransform tagTemplate;
        [SerializeField] private HoverTooltipView m_view;

        private HoverTooltipPresenter m_presenter;
        private bool m_hasAttemptedInitialization;
        private bool m_hasLoggedReferenceError;

        public void InitializeScreen(UIContext _context) => Initialize(_context?.Builder, _context?.Interaction);

        public void Initialize(CubeBuilder source, Interaction interactionSource)
        {
            EnsureInitialized();
            if (m_presenter != null)
            {
                m_presenter.SetSources(source, interactionSource);
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        // 습격 결과 화면의 모듈 아이콘에서 호출한다. 결과 화면 위에 모듈 상세를 띄운다.
        public void ShowResultModule(ModuleData _module, Vector2 _position)
        {
            EnsureInitialized();
            if (m_presenter != null)
            {
                m_presenter.ShowResultModule(_module, _position);
            }
        }

        public void ClearResultModule()
        {
            if (m_presenter != null)
            {
                m_presenter.ClearResultModule();
            }
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
                        "[HoveringUI] Initialize 실패: m_view 참조 누락. " +
                        "UIManager.prefab의 HoverOrder 저작을 실행하세요.", this);
                }

                return;
            }

            m_view.BindReferences(panel, title, description, interact, textArea, point1, point2, connector,
                connectorWidth, textOffset, screenPadding, scanModeText, scanLoadingDiameter,
                scanLoadingThickness, scanTextRevealDuration, tagGrid, tagTemplate);
            m_presenter = new HoverTooltipPresenter(m_view, palette, tagSource);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
            }
        }
    }
}
