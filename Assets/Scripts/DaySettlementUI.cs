using UnityEngine;
using UnityEngine.UI;
using WeaponWorkshop;

// 영업 정산 UI의 기존 연결점(브리지). 직렬화 필드·TutorialConfirmButton 등 외부 공개 API와 씬 오버라이드 호환을 유지한다.
// 표시는 SettlementView, 정산 진행은 SettlementPresenter가 담당하며 실행 시 원본 참조는 이 컴포넌트가 View로 전달한다.
public sealed class DaySettlementUI : MonoBehaviour, IUIScreen
{
    [SerializeField] private UnityEngine.InputSystem.InputActionAsset inputActions;
    [SerializeField] private _DayManager dayManager;
    [SerializeField] private WeaponWorkshop.NightRaidController nightRaid;
    [SerializeField] private GameObject settlementPanel;
    [SerializeField] private TMPro.TextMeshProUGUI summaryText;
    [SerializeField] private TMPro.TextMeshProUGUI receiptTitle;
    [SerializeField] private TMPro.TextMeshProUGUI questionText;
    [SerializeField] private Button nextDayButton;
    [SerializeField] private Button declineButton;
    [SerializeField] private Button reopenSettlementButton;
    [SerializeField] private Button closeSettlementButton;
    [SerializeField] private TMPro.TextMeshProUGUI morningNoticeText;
    [SerializeField] private RectTransform moneyDisplay;
    [SerializeField] private TMPro.TextMeshProUGUI moneyText;
    [SerializeField] private RectTransform moduleCreditDisplay;
    [SerializeField] private TMPro.TextMeshProUGUI moduleCreditText;
    [SerializeField] private WeaponWorkshop.PlayerWallet wallet;
    [SerializeField] private TMPro.TextMeshProUGUI todayIncomeText;
    [SerializeField, Min(0f), Tooltip("자리세 행 위치 저작 전용 값. 런타임에는 프리팹에 저작된 DailyRentDisplay를 쓴다.")]
    private float dailyRentGap = 12f;
    [SerializeField, Min(1f), Tooltip("자리세 행 너비 저작 전용 값. 런타임에는 프리팹에 저작된 DailyRentDisplay를 쓴다.")]
    private float dailyRentWidth = 255f;
    [SerializeField, Min(0f), Tooltip("돈·모듈 재화 숫자 연출 시간(초). 정산 중에도 실제 시간으로 재생합니다.")]
    private float moneyCountDuration = 0.25f;
    [SerializeField, Min(0.01f), Tooltip("사망 귀환 시 획득 모듈 재화를 잃는 슬롯 숫자 회전 시간(초).")]
    private float raidLossRollDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float insufficientFundsDuration = 1.2f;
    [SerializeField, Min(1)] private int insufficientFundsPulses = 3;
    [SerializeField, Min(1f)] private float insufficientFundsScale = 1.2f;
    [SerializeField] private SettlementView m_view;

    private SettlementPresenter m_presenter;
    private _DayManager m_configuredDay;
    private WeaponWorkshop.NightRaidController m_configuredNightRaid;
    private WeaponWorkshop.PlayerWallet m_configuredWallet;

    public RectTransform TutorialConfirmButton => declineButton != null ? declineButton.transform as RectTransform : null;

    public void InitializeScreen(UIContext _context)
    {
        ResolveSceneReferences(_context);
        if (m_view != null && _context != null && _context.Ui != null)
        {
            m_view.SetUIManager(_context.Ui);
        }

        EnsurePresenter();
    }

    private void ResolveSceneReferences(UIContext _context)
    {
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

        if (nightRaid == null)
        {
            if (_context != null && _context.Game != null)
            {
                nightRaid = _context.Game.GetComponent<WeaponWorkshop.NightRaidController>() ??
                            _context.Game.GetComponentInChildren<WeaponWorkshop.NightRaidController>();
            }

            if (nightRaid == null)
            {
                nightRaid = WeaponWorkshop.NightRaidController.Instance != null
                    ? WeaponWorkshop.NightRaidController.Instance
                    : FindFirstObjectByType<WeaponWorkshop.NightRaidController>();
            }
        }

        if (wallet == null)
        {
            wallet = WeaponWorkshop.PlayerWallet.ForScene(this);
        }
    }

    private void Awake()
    {
        ResolveSceneReferences(null);
        if (m_view != null)
        {
            var ui = GetComponentInParent<UIManager>() ?? FindFirstObjectByType<UIManager>();
            if (ui != null)
            {
                m_view.SetUIManager(ui);
            }
        }

        EnsurePresenter();
    }

    private void EnsurePresenter()
    {
        if (m_view == null)
        {
            Debug.LogError(
                "[DaySettlementUI] Awake 실패: m_view에 SettlementView 참조가 필요합니다. " +
                "Tools/UI/Authoring/Settlement/Author 실행 결과를 확인하세요.");
            return;
        }

        if (m_presenter != null && m_configuredDay == dayManager &&
            m_configuredNightRaid == nightRaid && m_configuredWallet == wallet)
        {
            return;
        }

        if (m_presenter != null)
        {
            m_presenter.Dispose();
            m_presenter = null;
        }

        m_view.Configure(settlementPanel, summaryText, receiptTitle, questionText,
            nextDayButton, declineButton, reopenSettlementButton, closeSettlementButton,
            morningNoticeText, moneyDisplay, moneyText, moduleCreditDisplay, moduleCreditText,
            todayIncomeText, inputActions,
            moneyCountDuration, raidLossRollDuration, insufficientFundsDuration,
            insufficientFundsPulses, insufficientFundsScale);

        m_configuredDay = dayManager;
        m_configuredNightRaid = nightRaid;
        m_configuredWallet = wallet;

        m_presenter = new SettlementPresenter(m_view, dayManager, wallet, nightRaid, this,
            () => WeaponWorkshop.FeedbackUI.For(this));
        if (!m_presenter.Initialize())
        {
            // 참조 오류는 View가 화면명·필드명으로 기록한다. 이 화면만 초기화를 포기한다.
            m_presenter = null;
            return;
        }

        if (isActiveAndEnabled)
        {
            m_presenter.OnEnabled();
        }
    }

    private void OnEnable()
    {
        m_presenter?.OnEnabled();
    }

    private void OnDisable()
    {
        m_presenter?.OnDisabled();
    }

    private void OnDestroy()
    {
        if (m_presenter == null)
        {
            return;
        }

        m_presenter.Dispose();
        m_presenter = null;
    }
}
