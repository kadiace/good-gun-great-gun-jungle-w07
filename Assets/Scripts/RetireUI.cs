using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 은퇴 화면의 기존 연결점(브리지). 직렬화 필드·Cost·IsOpen·씬 오버라이드 호환을 유지한다.
    // 표시·기본 선택은 RetireView, 로그·씬 이동은 RetirePresenter가 담당하며 실행 시 실효 참조를 View로 전달한다.
    public sealed class RetireUI : MonoBehaviour, IUIScreen
    {
        public const int Cost = 250000;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text continueText;
        [SerializeField] private Button lobbyButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private _DayManager dayManager;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private RetireView m_view;

        private RetirePresenter m_presenter;
        private _DayManager m_configuredDay;

        public bool IsOpen => panel != null && panel.activeSelf;
        public _DayManager DayManager => dayManager;
        public UIManager UiManager => uiManager;

        public void InitializeScreen(UIContext _context)
        {
            ResolveDependencies(_context);
            EnsureInitialized();
        }

        private void ResolveDependencies(UIContext _context = null)
        {
            if (uiManager == null)
            {
                if (_context != null && _context.Ui != null)
                {
                    uiManager = _context.Ui;
                }

                if (uiManager == null)
                {
                    uiManager = GetComponentInParent<UIManager>() ?? FindFirstObjectByType<UIManager>();
                }
            }

            if (dayManager == null)
            {
                if (_context != null && _context.Game != null)
                {
                    dayManager = _context.Game.GetComponent<_DayManager>() ??
                                 _context.Game.GetComponentInChildren<_DayManager>();
                }

                if (dayManager == null)
                {
                    dayManager = FindFirstObjectByType<_DayManager>();
                }
            }
        }

        private void Awake()
        {
            // 씬에 저장된 활성 상태와 무관하게 닫힌 상태로 시작한다.
            if (panel != null)
            {
                panel.SetActive(false);
            }

            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            ResolveDependencies();

            RetireView view = ResolveView();
            if (view == null)
            {
                Debug.LogError(
                    "[RetireUI] Awake 실패: RetireView 참조가 없습니다. " +
                    "Tools/UI/Authoring/Ending/Author 실행 결과를 확인하세요.", this);
                return;
            }

            if (panel == null && view != null)
            {
                panel = view.gameObject;
            }

            if (m_presenter != null && m_configuredDay == dayManager)
            {
                return;
            }

            if (m_presenter != null)
            {
                m_presenter.Dispose();
                m_presenter = null;
            }

            m_configuredDay = dayManager;
            view.Configure(panel, continueText, lobbyButton, continueButton);
            m_presenter = new RetirePresenter(view, dayManager);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
            }
        }

        private void OnDestroy()
        {
            m_presenter?.Dispose();
            m_presenter = null;
        }

        public void Open(bool firstEnding)
        {
            if (m_presenter == null)
            {
                EnsureInitialized();
            }

            if (m_presenter == null)
            {
                return;
            }

            m_presenter.Open(firstEnding);
        }

        private RetireView ResolveView()
        {
            if (m_view != null)
            {
                return m_view;
            }

            return GetComponentInChildren<RetireView>(true) ?? GetComponent<RetireView>();
        }
    }
}
