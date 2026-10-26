using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeaponWorkshop;

public class _DayManager : MonoBehaviour
{
    [SerializeField] private DaySettings settings;
    [SerializeField] private SetTimer timerController;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Bell customerBell;
    [SerializeField, Min(1f), Tooltip("영업 시작 후 낮 운영 시간(초)")] private float businessDuration = 180f;
    private float businessRemaining;
    private bool businessRunning;
    public float BusinessRemaining => businessRemaining;
    public float BusinessDuration => businessDuration;
    public bool BusinessRunning => businessRunning;
    public bool CanAcceptCustomers => !IsNight && (DayNumber == 0 || businessRunning && businessRemaining > 0f);
    [SerializeField, Tooltip("일차 번호로 조회할 CustomerDay 에셋 목록")] private CustomerDay[] customerDays;
    [SerializeField] private CustomerDay tutorialDay;
    [SerializeField, Min(0), Tooltip("0은 튜토리얼 일차. 내용 작성 전에는 기존 1일차 시작을 유지합니다.")]
    private int startingDay = 1;
    [SerializeField] private bool askToSkipTutorial;
    public CustomerDay CurrentCustomerDay { get; private set; }
    [SerializeField, Min(1), Tooltip("정해진 일차가 끝난 뒤 무한 모드의 하루 손님 수")] private int endlessGuestsPerDay = 8;
    // 무한 모드 대기열: 한 바퀴 다 나올 때까지 같은 손님이 다시 나오지 않음.
    private readonly List<CustomerProfile> endlessBag = new List<CustomerProfile>();
    private readonly List<CustomerProfile> deferredCustomers = new List<CustomerProfile>();
    private PlayerModuleUnlocks ModuleUnlocks => wallet != null ? wallet.GetComponent<PlayerModuleUnlocks>() : null;
    [SerializeField, Tooltip("일반 손님을 만드는 요구사항 조합표 (1~7일차 일반 손님 자리 + 무한 모드). 비우면 기존 손님 그대로")] private CustomerRequestPool endlessRequests;
    [SerializeField, Range(0f, 1f), Tooltip("무한 모드에서 조합 손님이 나올 확률. 나머지는 '예?'/진상 손님")] private float generatedRatio = 0.5f;
    public bool HasNextDay => FindCustomerDay(DayNumber + 1) != null || HasAnyCustomer();
    private int dailyGuestCount;
    public int CompletedGuests { get; private set; }
    public int DissatisfiedGuests { get; private set; }
    public int KilledGuests { get; private set; }
    private PlayerWallet wallet;
    public int Money => wallet != null ? wallet.Balance : 0;
    public int ModuleCredits => wallet != null ? wallet.ModuleCredits : 0;
    public int TodayIncome { get; private set; }
    public int TodaySoldConstructionCost { get; private set; }
    public int TodayRent { get; private set; }
    public int TodayLoanRepayment { get; private set; }
    public int TodayLoanInterest { get; private set; }
    public int TodayLoanIncome { get; private set; }
    public bool SettlementCostsWaived => DayNumber == 0 ||
        (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive);
    public int NextSettlementRent
    {
        get
        {
            if (SettlementCostsWaived) return 0;
            if (!IsNight) return CurrentCustomerDay != null ? CurrentCustomerDay.Rent : 0;
            int nextDay = DayNumber + 1;
            var fixedDay = FindCustomerDay(nextDay);
            if (fixedDay != null) return fixedDay.Rent;
            int firstEndlessDay = 1;
            if (customerDays != null)
                foreach (var day in customerDays)
                    if (day != null) firstEndlessDay = Mathf.Max(firstEndlessDay, day.day + 1);
            return endlessRent + endlessRentGrowth * Mathf.Max(0, nextDay - firstEndlessDay);
        }
    }
    [SerializeField, Min(0), Tooltip("무한 모드 첫날 월세")] private int endlessRent = 2000;
    [SerializeField, Min(0), Tooltip("무한 모드 하루마다 오르는 월세")] private int endlessRentGrowth = 50;
    public int DayNumber { get; private set; } = 1;
    public bool IsNight { get; private set; }
    private float dayNightStartedAt;
    public float SecondsSinceDayNightTransition => Mathf.Max(0f, Time.time - dayNightStartedAt);
    public bool IsAwaitingOpening => !IsNight && customerBell != null && !customerBell.DayStarted;
    public bool IsSettlementOpen { get; private set; }
    public bool CanStartNextDay { get; private set; }
    public bool IsBankrupt { get; private set; }
    public setTimer CurrentPeriod { get; private set; }
    // 고정 일차(CustomerDay 에셋)가 끝난 뒤의 무한 모드 일차인지.
    public bool IsEndlessDay => FindCustomerDay(DayNumber) == null;
    public event Action Changed;
    public event Action<int> DayAdvanced;
    public event Action<int, int> BusinessOpened;
    public event Action<int, int> ManualCloseRequested;
    public event Action<CustomerProfile, int> OrderRegistered;
    // Shop telemetry: (runtime order, day it was pushed from, reason).
    public event Action<CustomerProfile, int, string> OrderDeferred;
    // Shop telemetry before night starts: manual or automatic close.
    public event Action<bool> ShopClosing;
    public int DailyGuestCount => dailyGuestCount;
    public bool CanCollectShopLogs => shopLogsReady && shopLogSuppression == 0;
    private bool shopLogsReady;
    private int shopLogSuppression;
    private readonly List<CustomerProfile> ownedOrders = new List<CustomerProfile>();
    private bool hasCurrentPeriod;
    private bool isSettled;
    private bool returningToStart;
    private bool bankruptcyResultOpen;
    private bool startedDayInDebt;

    private void Awake()
    {
        IsNight = false;
        dayNightStartedAt = Time.time;
        IsSettlementOpen = false;
        CanStartNextDay = false;
        IsBankrupt = false;
        bankruptcyResultOpen = false;
        isSettled = false;
        startedDayInDebt = false;
        hasCurrentPeriod = false;
        CompletedGuests = 0;
        DissatisfiedGuests = 0;
        KilledGuests = 0;
        TodayIncome = 0;
        TodaySoldConstructionCost = 0;
        TodayRent = 0;
        TodayLoanRepayment = TodayLoanInterest = 0;
        TodayLoanIncome = 0;
        wallet = PlayerWallet.ForScene(this);
        DayNumber = startingDay;
        CurrentCustomerDay = null;
        dailyGuestCount = 0;
        CurrentPeriod = setTimer.morning;
        businessRemaining = businessDuration;
        businessRunning = false;
    }
    private void Update()
    {
        if (!businessRunning || IsNight || OnboardingManager.IsShootingGuideActive ||
            OnboardingManager.IsCustomGuideActive) return;
        businessRemaining = Mathf.Max(0f, businessRemaining - Time.deltaTime);
        if (businessRemaining <= 0f && (customerBell == null || customerBell.Current == null)) CloseShop();
    }
    private void OnEnable()
    {
        if (wallet == null) wallet = PlayerWallet.ForScene(this);
        if (wallet != null) wallet.Changed += OnWalletChanged;
    }
    private void OnWalletChanged() => Changed?.Invoke();

    private void OnDisable()
    {
        if (wallet != null) wallet.Changed -= OnWalletChanged;
    }
    private void Start()
    {
        if (!HasRequiredReferences()) return;
        shopLogsReady = GameSaveFiles.PendingLoad == null && !NightRaidTransfer.HasPendingReturn;
        if (askToSkipTutorial && GameSaveFiles.PendingLoad == null && !NightRaidTransfer.HasPendingReturn)
        {
            DayNumber = 0;
            if (!ApplyCustomerDay(PrepareDay(0))) return;
            gameManager.SetNight(false); ApplyMorning(); Changed?.Invoke();
            var tutorial = TutorialManager.Instance;
            if (tutorial != null) tutorial.BeginDayZero(this);
            else BeginFirstBusinessDay();
            return;
        }
        if (!ApplyCustomerDay(PrepareDay(DayNumber))) return;
        gameManager.SetNight(false);
        ApplyMorning();
        Changed?.Invoke();
        if (TutorialManager.TrainingReturnPending) return;
        TutorialManager.Run(CurrentCustomerDay.startTutorial, this);
    }
    public void BeginFirstBusinessDay()
    {
        if (DayNumber != 0 || !HasRequiredReferences()) return;
        customerBell?.EndDay();
        if (!ApplyCustomerDay(PrepareDay(1))) return;
        wallet?.RestoreTodayConstructionCost(0);
        DayNumber = 1;
        CompletedGuests = DissatisfiedGuests = KilledGuests = TodayIncome = TodaySoldConstructionCost = TodayRent = 0;
        TodayLoanIncome = 0;
        isSettled = IsNight = IsSettlementOpen = CanStartNextDay = false;
        businessRunning = false;
        businessRemaining = businessDuration;
        startedDayInDebt = wallet != null && Money < 0;
        ApplyMorning(); gameManager.SetNight(false); Changed?.Invoke();
        DayAdvanced?.Invoke(DayNumber);
    }

    // Submission pays now; departure only advances the guest/day count.
    public void ReceivePayment(int payment, int constructionCost = 0)
    {
        if (!isActiveAndEnabled || IsNight || isSettled || !HasRequiredReferences()) return;
        int income = Mathf.Max(0, payment);
        TodayIncome += income;
        TodaySoldConstructionCost = (int)Math.Min(int.MaxValue, (long)TodaySoldConstructionCost + Mathf.Max(0, constructionCost));
        wallet?.Credit(income);
        Changed?.Invoke();
    }

    // 실제 판매금액이 없는 호출은 설정된 고정 단가를 사용합니다.
    public void CompletedGuest() => CompletedGuest(settings != null ? settings.payPerGuest : 0);
    public void CompletedGuest(int payment, bool dissatisfied = false, bool killed = false)
    {
        if (!isActiveAndEnabled || IsNight || isSettled || !HasRequiredReferences()) return;
        CompletedGuests++;
        // 퇴장 결과만 집계. 판매 후 사망한 손님도 살해로 한 번만 분류합니다.
        if (killed) KilledGuests++;
        else if (dissatisfied) DissatisfiedGuests++;
        int income = Mathf.Max(0, payment);
        TodayIncome += income;
        wallet?.Credit(income);
        UpdatePeriod();
        if (!IsNight) Changed?.Invoke();
    }
    private void ApplyMorning()
    {
        dayNightStartedAt = Time.time;
        CurrentPeriod = setTimer.morning;
        hasCurrentPeriod = true;
        SetTimerData morning = settings.timing?.Find(t => t != null && t.period == setTimer.morning);
        if (morning != null) timerController.SetImmediately(morning);
        else Debug.LogWarning("DaySettings에 morning 시간대를 추가하세요.", this);
    }
    private void UpdatePeriod()
    {
        SetTimerData timing = FindTiming(CompletedGuests);
        if (timing == null || (hasCurrentPeriod && CurrentPeriod == timing.period)) return;
        CurrentPeriod = timing.period;
        hasCurrentPeriod = true;
        timerController.Apply(timing);
        if (CurrentPeriod == setTimer.night) SettleDay();
    }
    private SetTimerData FindTiming(int guestCount)
    {
        SetTimerData selected = null;
        if (settings.timing == null) return null;
        // 일차별 마지막 손님을 처리했을 때 밤 정산을 시작합니다.
        if (CurrentCustomerDay != null && CurrentCustomerDay.day != 0 && guestCount >= dailyGuestCount && !businessRunning)
            return settings.timing.Find(t => t != null && t.period == setTimer.night);
        foreach (SetTimerData timing in settings.timing)
            if (timing != null && (CurrentCustomerDay == null || timing.period != setTimer.night) &&
                guestCount >= timing.startsAtGuestCount &&
                (selected == null || timing.startsAtGuestCount > selected.startsAtGuestCount))
                selected = timing;
        return selected;
    }
    public void CloseShop() => CloseShop(false);
    public void CloseShopAfterCustomer()
    {
        if (businessRunning && businessRemaining <= 0f) CloseShop();
    }
    public void CloseShop(bool manual)
    {
        if (IsNight || IsAwaitingOpening || isSettled || !HasRequiredReferences()) return;
        if (manual) ManualCloseRequested?.Invoke(DayNumber, Money);
        businessRunning = false;
        ShopClosing?.Invoke(manual);
        CurrentPeriod = setTimer.night;
        hasCurrentPeriod = true;
        var night = settings.timing?.Find(t => t != null && t.period == setTimer.night);
        if (night != null) timerController.SetImmediately(night);
        SettleDay();
    }

    public void SettleDay()
    {
        if (isSettled || CurrentPeriod != setTimer.night) return;
        isSettled = true;
        businessRunning = false;
        IsNight = true;
        dayNightStartedAt = Time.time;
        WorkshopSoundEffects.RaiseBusinessEnded();
        customerBell?.EndDay();
        bool tutorial = SettlementCostsWaived;
        TodayRent = !tutorial && CurrentCustomerDay != null ? CurrentCustomerDay.Rent : 0;
        int balanceBefore = Money;
        int loans = !tutorial && wallet != null ? wallet.OutstandingLoans : 0;
        long principal = (long)loans * PlayerWallet.LoanAmount;
        long interest = (long)TodayRent * loans / 5;
        long due = (long)TodayRent + principal + interest;
        TodayLoanRepayment = (int)Math.Min(int.MaxValue, principal);
        TodayLoanIncome = TodayLoanRepayment;
        TodayLoanInterest = (int)Math.Min(int.MaxValue, interest);
        if (!tutorial && wallet != null && (due > int.MaxValue || wallet.Balance < due))
        {
            IsBankrupt = true;
            LogDaySettled(balanceBefore);
            LogKit.GameLogger.Data("Info", "Game", "bankrupt",
                new GameBankruptLog { day = DayNumber, balance = wallet.Balance, rent = TodayRent });
            CanStartNextDay = false;
            IsSettlementOpen = true;
            gameManager.SetNight(true);
            Changed?.Invoke();
            return;
        }
        wallet?.Debit((int)Math.Min(int.MaxValue, due));
        wallet?.RestoreLoans(0);
        if (tutorial) wallet?.RestoreBalance(5000);
        LogDaySettled(balanceBefore);
        IsSettlementOpen = true;
        gameManager.SetNight(true);
        CanStartNextDay = HasNextDay && !(TutorialManager.Instance != null && TutorialManager.Instance.IsGuidedDayZero);
        Changed?.Invoke();
        // Stay at night until the player chooses a raid or the next morning.
    }
    private void LogDaySettled(int balanceBefore)
    {
        var save = FindFirstObjectByType<GameSaveController>();
        LogKit.GameLogger.Data("Info", "Game", "day_settled", new GameDaySettledLog
        {
            retired = save != null && save.IsRetired, endChestOpened = save != null && save.EndChestOpened,
            day = DayNumber, balanceBefore = balanceBefore, income = TodayIncome,
            constructionCost = wallet != null ? wallet.TodayConstructionCost : 0, soldConstructionCost = TodaySoldConstructionCost,
            rent = TodayRent, balanceAfter = Money, completedGuests = CompletedGuests, dissatisfiedGuests = DissatisfiedGuests,
            killedGuests = KilledGuests, moduleCredits = ModuleCredits, bankrupt = IsBankrupt
        });
    }
    public void DeclineNextDay()
    {
        if (!IsNight || !IsSettlementOpen || !HasRequiredReferences()) return;
        if (IsBankrupt)
        {
            if (bankruptcyResultOpen) return;
            var resultUI = gameManager.UI != null ? gameManager.UI.RaidUI : null;
            if (resultUI == null) { Debug.LogError("자릿세 게임오버 결과 UI가 연결되지 않았습니다.", this); return; }
            bankruptcyResultOpen = true;
            gameManager.SetDialogue(true);
            IsSettlementOpen = false;
            Changed?.Invoke();
            resultUI.ShowBankruptcyResult(ReturnAfterBankruptcy);
            return;
        }
        IsSettlementOpen = false;
        gameManager.SetNight(false);
        Changed?.Invoke();
    }
    private void ReturnAfterBankruptcy()
    {
        if (returningToStart) return;
        returningToStart = true;
        NightRaidTransfer.Abandon();
        TutorialManager.AbandonTraining();
        GameSaveFiles.PendingLoad = null;
        GameSaveFiles.ClearRaidRecovery();
        PoolManager.ClearActive();
        WeaponWorkshop.UIInputPolicy.ApplyLobbyPolicy();
        SceneManager.LoadSceneAsync("Intro", LoadSceneMode.Single);
    }
    // Explicit hook for the authored tutorial's eventual completion; an empty day never auto-advances.
    public void CompleteTutorialDay()
    {
        if (DayNumber != 0 || IsNight || !HasRequiredReferences()) return;
        CurrentPeriod = setTimer.night;
        SettleDay();
        StartNextDay();
    }

    public bool OpenShop()
    {
        if (!IsAwaitingOpening || GameManager.GameplayBlocked || !HasRequiredReferences()) return false;
        // 잔액·대출 가능 여부는 영업 시작 시점 기준. 대기열을 다시 넣으므로 종 시작 전에 판정한다.
        if (DayNumber != 0 && CurrentCustomerDay != null && CurrentCustomerDay.customers != null)
        {
            ApplyDeferrals(CurrentCustomerDay, DayNumber, CanServeToday);
            if (!ApplyCustomerDay(CurrentCustomerDay)) return false;
        }
        businessRemaining = businessDuration;
        businessRunning = DayNumber != 0;
        if (!customerBell.StartBusiness())
        {
            businessRunning = false;
            return false;
        }
        BusinessOpened?.Invoke(DayNumber, Money);
        Changed?.Invoke();
        return true;
    }

    public void HideSettlementForRaid()
    {
        if (!IsNight) return;
        IsSettlementOpen = false;
        Changed?.Invoke();
    }

    public void ShowSettlement()
    {
        if (!IsNight || IsSettlementOpen || !HasRequiredReferences()) return;
        IsSettlementOpen = true;
        gameManager.SetNight(true);
        Changed?.Invoke();
    }

    public void StartNextDay(bool skippedNightRaid = false)
    {
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsGuidedDayZero) return;
        if (returningToStart || !IsNight || !CanStartNextDay || !HasRequiredReferences()) return;
        if (wallet != null && startedDayInDebt && Money < 0)
        {
            if (!Application.CanStreamedLevelBeLoaded("Intro"))
            {
                Debug.LogError("게임오버 복귀 씬 Intro를 빌드 목록에 등록하세요.", this);
                return;
            }
            returningToStart = true;
            CanStartNextDay = false;
            IsSettlementOpen = false;
            gameManager.SetNight(true);
            NightRaidTransfer.Abandon();
            GameSaveFiles.PendingLoad = null;
            GameSaveFiles.ClearRaidRecovery();
            PoolManager.ClearActive();
            WeaponWorkshop.UIInputPolicy.ApplyLobbyPolicy();
            Changed?.Invoke();
            SceneManager.LoadSceneAsync("Intro", LoadSceneMode.Single);
            return;
        }
        int skippedDay = DayNumber;
        CustomerDay nextDay = PrepareDay(DayNumber + 1);
        if (!ApplyCustomerDay(nextDay)) return;
        wallet?.RestoreTodayConstructionCost(0);
        startedDayInDebt = wallet != null && Money < 0;
        DayNumber = nextDay.day;
        // 고정 일차(CustomerDay 에셋)가 끝나고 처음 맞는 무한 모드 일차.
        if (FindCustomerDay(DayNumber) == null && FindCustomerDay(DayNumber - 1) != null)
            LogKit.GameLogger.Data("Info", "Game", "endless_started", new GameDayLog { day = DayNumber });
        CompletedGuests = 0;
        DissatisfiedGuests = 0;
        KilledGuests = 0;
        TodayIncome = 0;
        TodaySoldConstructionCost = 0;
        TodayRent = 0;
        TodayLoanRepayment = TodayLoanInterest = 0;
        TodayLoanIncome = 0;
        isSettled = false;
        IsNight = false;
        businessRunning = false;
        businessRemaining = businessDuration;
        IsSettlementOpen = false;
        CanStartNextDay = false;
        // 임시 지원: 습격 대신 다음 날 아침을 선택한 경우에만 지급합니다.
        if (skippedNightRaid)
        {
            wallet?.CreditModuleCredits(100);
            LogKit.GameLogger.Data("Info", "Game", "raid_skipped", new RaidSkippedLog
            { day = skippedDay, nextDay = DayNumber, moduleCreditsGranted = wallet != null ? 100 : 0, moduleCreditsAfter = ModuleCredits });
        }
        ApplyMorning();
        gameManager.SetNight(false);
        Changed?.Invoke();
        DayAdvanced?.Invoke(DayNumber);
        TutorialManager.Run(CurrentCustomerDay.startTutorial, this);
    }
#if UNITY_EDITOR
    public bool DebugJumpToDay(int day, bool night)
    {
        if (day < 1 || day > 7 || !isActiveAndEnabled || !HasRequiredReferences() || customerBell == null) return false;
        var transition = gameManager.UI != null ? gameManager.UI.GetComponent<DayTransitionUI>() : null;
        if (transition != null && transition.IsTransitioning) return false;
        TutorialManager.Instance?.DebugEndTutorial();
        deferredCustomers.Clear();
        endlessBag.Clear();
        CustomerDay target = PrepareDay(day);
        if (target == null || target.customers == null || target.customers.Length == 0 ||
            Array.Exists(target.customers, profile => profile == null)) return false;

        customerBell.EndDay();
        if (!ApplyCustomerDay(target)) return false;
        DayNumber = day;
        wallet?.RestoreTodayConstructionCost(0);
        CompletedGuests = DissatisfiedGuests = KilledGuests = TodayIncome = TodaySoldConstructionCost = TodayRent = 0;
        TodayLoanIncome = 0;
        isSettled = IsNight = night;
        businessRunning = false;
        businessRemaining = businessDuration;
        dayNightStartedAt = Time.time;
        IsSettlementOpen = IsBankrupt = bankruptcyResultOpen = returningToStart = startedDayInDebt = false;
        CanStartNextDay = night && HasNextDay;
        if (night)
        {
            CurrentPeriod = setTimer.night;
            hasCurrentPeriod = true;
            var nightTiming = settings.timing?.Find(t => t != null && t.period == setTimer.night);
            if (nightTiming != null) timerController.SetImmediately(nightTiming);
            TodayRent = target.Rent;
            customerBell.EndDay();
        }
        else ApplyMorning();
        gameManager.SetDialogue(false);
        gameManager.SetNight(false);
        Changed?.Invoke();
        return true;
    }
#endif
    public void CaptureSave(GameSaveData data)
    {
        data.completed = CompletedGuests; data.dissatisfied = DissatisfiedGuests; data.killed = KilledGuests;
        data.income = TodayIncome; data.constructionCost = wallet != null ? wallet.TodayConstructionCost : 0;
        data.soldConstructionCost = TodaySoldConstructionCost;
        data.period = (int)CurrentPeriod; data.night = IsNight;
        data.settlement = IsSettlementOpen; data.timer = timerController.CaptureSave();
        data.businessRemaining = businessRemaining;
        data.startedDayInDebt = startedDayInDebt;
        data.todayLoanRepayment = TodayLoanRepayment;
        data.todayLoanInterest = TodayLoanInterest;
        data.todayLoanIncome = TodayLoanIncome;
        customerBell.CaptureSave(data);
        if (CurrentCustomerDay != null)
        {
            data.dailyOrders = Array.ConvertAll(CurrentCustomerDay.customers, CaptureOrder);
            data.dailyRent = CurrentCustomerDay.Rent;
        }
        data.deferredCustomers = deferredCustomers.ConvertAll(CaptureOrder).ToArray();
    }

    public bool CanRestoreSave(GameSaveData data)
    {
        if (data == null) return false;
        if (data.dailyOrders != null)
        {
            if (!HasRequiredReferences() || customerBell == null || data.dailyOrders.Length == 0 ||
                Array.Exists(data.dailyOrders, order => !CanRestoreOrder(order)) ||
                (data.deferredCustomers != null && Array.Exists(data.deferredCustomers,
                    order => !CanRestoreOrder(order) || FindSavedProfile(order) == null))) return false;
            if (data.completed < 0 || data.nextCustomer < 0 || data.nextCustomer > data.dailyOrders.Length ||
                data.currentCustomer < -1 || data.currentCustomer >= data.dailyOrders.Length) return false;
            if (data.currentCustomer < 0) return true;
            var order = data.dailyOrders[data.currentCustomer];
            var profile = FindSavedProfile(order);
            var lines = profile != null ? profile.Lines : order.lines;
            return data.customer != null && (data.customer.stage != 2 ||
                (lines != null && data.customer.line >= 0 && data.customer.line < lines.Length));
        }
        var day = data.travelCustomerDay != null ? data.travelCustomerDay : FindCustomerDay(data.day);
        return HasRequiredReferences() && customerBell != null && day != null && day.customers != null &&
            data.completed >= 0 && data.nextCustomer >= 0 && data.nextCustomer <= day.customers.Length &&
            data.currentCustomer >= -1 && data.currentCustomer < day.customers.Length &&
            (data.currentCustomer < 0 || (data.customer != null && day.customers[data.currentCustomer] != null &&
                (data.customer.stage != 2 || (day.customers[data.currentCustomer].Lines != null &&
                data.customer.line < day.customers[data.currentCustomer].Lines.Length))));
    }

    public void RestoreSavedState(GameSaveData data)
    {
        if (!CanRestoreSave(data)) return;
        using (SuppressShopLogs()) RestoreSavedStateCore(data);
    }
    private void RestoreSavedStateCore(GameSaveData data)
    {
        deferredCustomers.Clear();
        endlessBag.Clear();
        if (data.deferredCustomers != null)
            foreach (var order in data.deferredCustomers)
            {
                if (FindSavedProfile(order) == null) continue;
                var profile = RestoreOrder(order, data.day);
                if (!IsDeferred(profile)) deferredCustomers.Add(profile);
            }
        CustomerDay restoredDay;
        if (data.dailyOrders != null)
        {
            restoredDay = ScriptableObject.CreateInstance<CustomerDay>();
            restoredDay.day = data.day;
            restoredDay.Rent = data.dailyRent;
            restoredDay.customers = Array.ConvertAll(data.dailyOrders, order => RestoreOrder(order, data.day));
        }
        else
        {
            var sourceDay = data.travelCustomerDay != null ? data.travelCustomerDay : FindCustomerDay(data.day);
            restoredDay = Instantiate(sourceDay);
            restoredDay.CopyRuntimeFrom(sourceDay);
            restoredDay.customers = (CustomerProfile[])restoredDay.customers.Clone();
            for (int i = 0; i < restoredDay.customers.Length; i++)
                restoredDay.customers[i] = CreateRuntimeOrder(restoredDay.customers[i], data.day, restored: true);
        }
        for (int i = 0; i < restoredDay.customers.Length; i++)
        {
            bool currentOrder = i == data.currentCustomer && data.customer != null && data.customer.stage < 3;
            if (i < data.nextCustomer && !currentOrder) continue;
            var profile = restoredDay.customers[i];
            if (CanServe(profile, data.day)) continue;
            if (TryFindProfile(profile, out _, out _) && !IsDeferred(profile)) deferredCustomers.Add(profile);
            restoredDay.customers[i] = CreateCombination(data.day, restoredDay.MinRequests, restoredDay.MaxRequests);
            if (currentOrder)
            {
                data.customer = new CustomerSave { stage = data.customer.stage == 0 ? 0 : 1, position = data.customer.position };
                data.confirmation = false;
            }
        }
        if (!ApplyCustomerDay(restoredDay)) return;
        DayNumber = data.day; CompletedGuests = data.completed; DissatisfiedGuests = data.dissatisfied;
        KilledGuests = data.killed; TodayIncome = data.income; TodaySoldConstructionCost = data.soldConstructionCost;
        CurrentPeriod = (setTimer)data.period;
        wallet?.RestoreTodayConstructionCost(data.constructionCost);
        hasCurrentPeriod = true; isSettled = IsNight = data.night; IsSettlementOpen = data.settlement;
        dayNightStartedAt = Time.time;
        startedDayInDebt = data.startedDayInDebt;
        // The saved wallet already includes rent; restore its display without charging again.
        bool chargeSettlement = IsNight && !SettlementCostsWaived;
        TodayRent = chargeSettlement ? CurrentCustomerDay.Rent : 0;
        TodayLoanRepayment = chargeSettlement ? data.todayLoanRepayment : 0;
        TodayLoanInterest = chargeSettlement ? data.todayLoanInterest : 0;
        TodayLoanIncome = SettlementCostsWaived ? 0 : data.todayLoanIncome;
        CanStartNextDay = IsNight && HasNextDay;
        timerController.RestoreSave(data.timer);
        gameManager.SetNight(data.night && data.settlement);
        customerBell.RestoreSave(data);
        businessRemaining = data.businessRemaining > 0f ? data.businessRemaining : businessDuration;
        businessRunning = !IsNight && data.day != 0 && customerBell.DayStarted;
        if (businessRunning && data.businessRemaining <= 0f && data.version >= 10)
            businessRemaining = 0f;
        if (data.confirmation && customerBell.Current != null && SaleConfirmationUI.Instance != null)
            SaleConfirmationUI.Instance.UseCustomer(customerBell.Current, wallet.GetComponent<WeaponHolder>());
        Changed?.Invoke();
    }

    private CustomerDay FindCustomerDay(int day)
    {
        if (day == 0 && tutorialDay != null) return tutorialDay;
        if (customerDays == null) return null;
        foreach (CustomerDay candidate in customerDays)
            if (candidate != null && candidate.day == day) return candidate;
        return null;
    }

    private bool TryFindProfile(CustomerProfile profile, out int sourceDay, out int sourceSlot)
    {
        if (tutorialDay != null && tutorialDay.customers != null)
        {
            int tutorialSlot = Array.FindIndex(tutorialDay.customers, p => p == profile ||
                (!string.IsNullOrEmpty(profile.ProfileId) && p != null && p.ProfileId == profile.ProfileId));
            if (tutorialSlot >= 0) { sourceDay = 0; sourceSlot = tutorialSlot; return true; }
        }
        if (customerDays != null)
            foreach (var day in customerDays)
            {
                if (day == null || day.customers == null) continue;
                int slot = Array.FindIndex(day.customers, p => p == profile ||
                    (!string.IsNullOrEmpty(profile.ProfileId) && p != null && p.ProfileId == profile.ProfileId));
                if (slot < 0) continue;
                sourceDay = day.day; sourceSlot = slot; return true;
            }
        sourceDay = 0; sourceSlot = -1; return false;
    }

    private CustomerProfile FindSavedProfile(CustomerOrderSave order)
    {
        if (order != null && !string.IsNullOrEmpty(order.profileId))
        {
            if (tutorialDay != null)
                foreach (var profile in tutorialDay.customers ?? Array.Empty<CustomerProfile>())
                    if (profile != null && profile.ProfileId == order.profileId) return profile;
            foreach (var candidateDay in customerDays ?? Array.Empty<CustomerDay>())
                foreach (var profile in candidateDay != null ? candidateDay.customers ?? Array.Empty<CustomerProfile>() : Array.Empty<CustomerProfile>())
                    if (profile != null && profile.ProfileId == order.profileId) return profile;
            return null;
        }
        if (order == null || order.sourceSlot < 0) return null;
        var day = FindCustomerDay(order.sourceDay);
        return day != null && day.customers != null && order.sourceSlot < day.customers.Length
            ? day.customers[order.sourceSlot] : null;
    }

    private CustomerOrderSave CaptureOrder(CustomerProfile profile)
    {
        if (TryFindProfile(profile, out int sourceDay, out int slot))
            return new CustomerOrderSave { profileId = profile.ProfileId, orderState = profile.OrderState?.Copy(), requestIds = TableManager.Instance.CustomerRequest.CanonicalRequestIds(profile.RequestIds) };
        return new CustomerOrderSave { orderState = profile.OrderState?.Copy(), requestIds = TableManager.Instance.CustomerRequest.CanonicalRequestIds(profile.RequestIds), lines = profile.Lines,
            happyLine = profile.HappyLine, angryLine = profile.AngryLine,
            portrait = endlessRequests != null && endlessRequests.portraits != null
                ? Array.IndexOf(endlessRequests.portraits, profile.portrait) : -1 };
    }

    private bool CanRestoreOrder(CustomerOrderSave order)
    {
        if (order == null) return false;
        if (order.orderState != null && !string.IsNullOrEmpty(order.orderState.orderId) &&
            !Guid.TryParseExact(order.orderState.orderId, "N", out _)) return false;
        string[] rewardIds = order.requestIds ?? FindSavedProfile(order)?.RequestIds ?? SavedRequestIds(order);
        if (!CustomerRewardInfo.ValidSavedRewards(order.orderState, rewardIds)) return false;
        if (!string.IsNullOrEmpty(order.profileId) || order.sourceSlot >= 0)
            return FindSavedProfile(order) != null && (order.requestIds == null || TableManager.Instance.CustomerRequest.ContainsOrder(order.requestIds));
        return order.lines != null && order.lines.Length > 0 && order.price >= 0 &&
            TableManager.Instance.CustomerRequest.ContainsOrder(SavedRequestIds(order));
    }

    private string[] SavedRequestIds(CustomerOrderSave order) => order.requestIds != null
        ? TableManager.Instance.CustomerRequest.CanonicalRequestIds(order.requestIds)
        : TableManager.Instance.CustomerRequest.RestoreLegacyOrder(endlessRequests != null ? endlessRequests.PoolId : null, order.requests);

    private CustomerProfile RestoreOrder(CustomerOrderSave order, int savedDay)
    {
        var authored = FindSavedProfile(order);
        if (authored != null)
        {
            var restored = CreateRuntimeOrder(authored, savedDay, restored: true);
            if (order.requestIds != null) restored.RequestIds = SavedRequestIds(order);
            restored.OrderState = !string.IsNullOrEmpty(order.orderState?.orderId)
                ? order.orderState.Copy(fromSave: true) : CustomerOrderState.Create(restored: true);
            CustomerRewardInfo.InitializeOrder(restored, savedDay);
            return restored;
        }
        var profile = ScriptableObject.CreateInstance<CustomerProfile>();
        profile.name = "RestoredCombination";
        profile.OrderState = !string.IsNullOrEmpty(order.orderState?.orderId)
            ? order.orderState.Copy(fromSave: true) : CustomerOrderState.Create(restored: true);
        ownedOrders.Add(profile);
        profile.Lines = order.lines;
        profile.RequestIds = (string[])SavedRequestIds(order).Clone();
        profile.HappyLine = order.happyLine; profile.AngryLine = order.angryLine;
        CustomerRewardInfo.InitializeOrder(profile, savedDay);
        if (endlessRequests != null && endlessRequests.portraits != null && order.portrait >= 0 &&
            order.portrait < endlessRequests.portraits.Length) profile.portrait = endlessRequests.portraits[order.portrait];
        return profile;
    }

    private bool CanServe(CustomerProfile profile, int day)
        => CustomerModuleRequirements.Allows(profile, ModuleUnlocks,
            endlessRequests != null ? endlessRequests.CoreStats : default, day);

    // 손님 한 명 처리 후 남은 손님을 잔액·대출 가능 여부로 재판정하고, 못 받는 손님은 미룬다.
    public void RefreshRemainingCustomers()
    {
        if (DayNumber == 0 || IsNight || CurrentCustomerDay == null || CurrentCustomerDay.customers == null || customerBell == null) return;
        ApplyDeferrals(CurrentCustomerDay, DayNumber, CanServeToday, customerBell.NextCustomerIndex);
        PrepareRuntimeOrders(CurrentCustomerDay);
        customerBell.ReplaceRemaining(CurrentCustomerDay.customers);
    }

    public void RefillCustomers()
    {
        if (!businessRunning || IsNight || CurrentCustomerDay == null || customerBell == null ||
            customerBell.RemainingCustomers > 0) return;
        var orders = new CustomerProfile[CurrentCustomerDay.customers.Length + 1];
        Array.Copy(CurrentCustomerDay.customers, orders, orders.Length - 1);
        orders[orders.Length - 1] = CreateRuntimeOrder(CreateCombination(DayNumber,
            CurrentCustomerDay.MinRequests, CurrentCustomerDay.MaxRequests), DayNumber);
        CurrentCustomerDay.customers = orders;
        dailyGuestCount = orders.Length;
        customerBell.ReplaceRemaining(orders);
        OrderRegistered?.Invoke(orders[orders.Length - 1], DayNumber);
    }

    // 모듈 해금은 필수. 잔액이 부족해도 오늘 대출을 사용할 수 있으면 주문을 허용한다.
    private bool CanServeToday(CustomerProfile profile, int day)
        => CanServe(profile, day) && wallet != null &&
            (wallet.CanTakeLoan(day) || wallet.CanAfford(CustomerModuleRequirements.DollarCost(profile, ModuleUnlocks,
                endlessRequests != null ? endlessRequests.CoreStats : default)));

    private CustomerProfile CreateCombination(int day, int min = -1, int max = -1)
        => endlessRequests == null ? ScriptableObject.CreateInstance<CustomerProfile>()
            : endlessRequests.Create(day,
                min < 0 ? endlessRequests.MinRequests : min,
                max < 0 ? endlessRequests.MaxRequests : max,
                ModuleUnlocks, Money, wallet != null && wallet.CanTakeLoan(day));

    private CustomerDay PrepareDay(int dayNumber)
    {
        var day = PrepareFixedDay(FindCustomerDay(dayNumber)) ?? BuildEndlessDay(dayNumber);
        // 연기되기 전의 생성 일차와 요청 정액을 먼저 계약한다.
        if (day?.customers != null)
            for (int i = 0; i < day.customers.Length; i++)
                day.customers[i] = CreateRuntimeOrder(day.customers[i], dayNumber);
        if (dayNumber != 0 && day != null && day.customers != null) ApplyDeferrals(day, dayNumber, CanServe);
        return day;
    }

    // 못 받는 손님은 미루고 조합 손님으로 대체, 미뤄둔 손님 중 받을 수 있게 된 손님은 조합 손님 자리로 복귀.
    private void ApplyDeferrals(CustomerDay day, int dayNumber, Func<CustomerProfile, int, bool> canServe, int from = 0)
    {
        var pending = deferredCustomers.ToArray();
        for (int i = from; i < day.customers.Length; i++)
        {
            var profile = day.customers[i];
            if (profile == null || canServe(profile, dayNumber)) continue;
            if (TryFindProfile(profile, out _, out _) && !IsDeferred(profile))
            {
                var deferred = CreateRuntimeOrder(profile, dayNumber);
                deferredCustomers.Add(deferred);
                OrderDeferred?.Invoke(deferred, dayNumber, CanServe(profile, dayNumber) ? "money" : "module_locked");
            }
            day.customers[i] = CreateCombination(dayNumber, day.MinRequests, day.MaxRequests);
        }
        foreach (var profile in pending)
        {
            if (!canServe(profile, dayNumber)) continue;
            int existing = Array.FindIndex(day.customers, from, candidate => SameProfile(candidate, profile));
            if (existing >= 0)
            {
                day.customers[existing] = profile;
                deferredCustomers.Remove(profile);
                continue;
            }
            for (int i = from; i < day.customers.Length; i++)
            {
                if (day.customers[i] == null || TryFindProfile(day.customers[i], out _, out _)) continue;
                day.customers[i] = profile;
                deferredCustomers.Remove(profile);
                break;
            }
        }
    }

    // 정해진 일차: '예?'/진상은 그대로, 일반 손님 자리만 그 일차 해금 범위의 요구사항 조합 손님으로 교체한 복사본.
    private CustomerDay PrepareFixedDay(CustomerDay source)
    {
        if (source == null) return null;
        var day = Instantiate(source);
        day.CopyRuntimeFrom(source);
        if (source.customers == null) return day;
        day.customers = (CustomerProfile[])source.customers.Clone();
        if (source.day == 0 || endlessRequests == null) return day;
        if (!source.UseRequestPool)
        {
            for (int i = 0; i < day.customers.Length; i++)
                if (day.customers[i] != null && !day.customers[i].IsDifficult &&
                    !HasMandatoryTag(day.customers[i])) day.customers[i] = CreateCombination(source.day, day.MinRequests, day.MaxRequests);
            return day;
        }
        var specials = new List<CustomerProfile>();
        int generated = source.ExtraGenerated;
        foreach (CustomerProfile profile in source.customers)
            if (profile != null && (!IsSpecial(profile) || (!profile.IsDifficult && !HasMandatoryTag(profile)))) generated++;
            else specials.Add(profile); // null도 그대로 넘겨 ApplyCustomerDay가 에러를 띄우게 함
        int total = specials.Count + generated;
        var customers = new CustomerProfile[total];
        int next = 0;
        for (int t = 0; t < total; t++)
        {
            // '예?' 손님을 목록 순서대로 하루 전체에 고르게 펼침. 첫 손님은 조합 손님이 오도록.
            bool special = next < specials.Count && (t + 1) * specials.Count / total > t * specials.Count / total;
            customers[t] = special ? specials[next++] : endlessRequests.Create(source.day, source.MinRequests, source.MaxRequests,
                ModuleUnlocks, Money, wallet != null && wallet.CanTakeLoan(source.day));
        }
        day.customers = customers;
        return day;
    }

    // 정해진 일차 이후: 모든 일차 손님을 섞어 대기열에서 꺼냄. 비면 다시 채움(오늘 이미 나온 손님 제외).
    private CustomerDay BuildEndlessDay(int dayNumber)
    {
        var today = new List<CustomerProfile>();
        endlessBag.RemoveAll(IsDeferred);
        foreach (var profile in deferredCustomers)
            if (today.Count < endlessGuestsPerDay && CanServe(profile, dayNumber)) today.Add(profile);
        while (today.Count < endlessGuestsPerDay)
        {
            // 일반 손님은 요구사항 조합으로 새로 만들고, '예?'/진상 손님은 대기열에서 꺼냄.
            if (endlessRequests != null && UnityEngine.Random.value < generatedRatio)
            { today.Add(CreateCombination(dayNumber)); continue; }
            if (endlessBag.Count == 0 && !RefillEndlessBag(today))
            {
                if (endlessRequests == null) break;
                today.Add(CreateCombination(dayNumber));
                continue;
            }
            int last = endlessBag.Count - 1;
            today.Add(endlessBag[last]);
            endlessBag.RemoveAt(last);
        }
        if (today.Count == 0) return null;
        var day = ScriptableObject.CreateInstance<CustomerDay>();
        day.day = dayNumber;
        day.MinRequests = endlessRequests != null ? endlessRequests.MinRequests : 1;
        day.MaxRequests = endlessRequests != null ? endlessRequests.MaxRequests : 1;
        int firstEndlessDay = 1;
        if (customerDays != null) foreach (CustomerDay fixedDay in customerDays) if (fixedDay != null) firstEndlessDay = Mathf.Max(firstEndlessDay, fixedDay.day + 1);
        day.Rent = endlessRent + endlessRentGrowth * Mathf.Max(0, dayNumber - firstEndlessDay);
        day.customers = today.ToArray();
        return day;
    }

    private bool RefillEndlessBag(List<CustomerProfile> exclude)
    {
        var seen = new HashSet<CustomerProfile>(exclude);
        seen.UnionWith(deferredCustomers);
        if (customerDays != null)
            foreach (CustomerDay day in customerDays)
                if (day != null && day.customers != null)
                    foreach (CustomerProfile profile in day.customers)
                        if (profile != null && (endlessRequests == null || (IsSpecial(profile) &&
                            (profile.IsDifficult || HasMandatoryTag(profile)))) &&
                            !ContainsProfile(exclude, profile) && !IsDeferred(profile) && seen.Add(profile)) endlessBag.Add(profile);
        for (int i = endlessBag.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            CustomerProfile swap = endlessBag[i];
            endlessBag[i] = endlessBag[j];
            endlessBag[j] = swap;
        }
        return endlessBag.Count > 0;
    }

    // '예?'를 눌러야 말하는 손님 또는 진상. 일반 손님은 무한 모드에서 조합으로 대체됨.
    private static bool IsSpecial(CustomerProfile profile)
        => !profile.EndsClear || profile.Lines.Length > 1;

    private static bool HasMandatoryTag(CustomerProfile profile)
    {
        var table = TableManager.Instance.CustomerRequest;
        foreach (string id in profile.RequestIds)
            if (!string.IsNullOrEmpty(table.FindRequest(id)?.tagId)) return true;
        return false;
    }

    private bool HasAnyCustomer()
    {
        if (customerDays != null)
            foreach (CustomerDay day in customerDays)
                if (day != null && day.customers != null && day.customers.Length > 0) return true;
        return false;
    }

    private bool ApplyCustomerDay(CustomerDay day)
    {
        if (day == null)
        {
            Debug.LogWarning($"{DayNumber + (IsNight ? 1 : 0)}일차 CustomerDay 에셋을 DayManager에 연결하세요.", this);
            return false;
        }
        if (customerBell == null || day.customers == null || (day.customers.Length == 0 && day.day != 0))
        {
            Debug.LogError($"{day.day}일차의 손님 목록 또는 Bell 연결이 없습니다.", this);
            return false;
        }
        foreach (CustomerProfile profile in day.customers)
            if (profile == null)
            {
                Debug.LogError($"{day.day}일차 손님 목록에 비어 있는 항목이 있습니다.", this);
                return false;
            }
        CurrentCustomerDay = day;
        PrepareRuntimeOrders(day);
        dailyGuestCount = day.customers.Length;
        customerBell.SetCustomers(day.customers);
        return true;
    }

    private static bool SameProfile(CustomerProfile a, CustomerProfile b) => a == b ||
        (a != null && b != null && !string.IsNullOrEmpty(a.ProfileId) && a.ProfileId == b.ProfileId);

    private static bool ContainsProfile(IList<CustomerProfile> profiles, CustomerProfile profile, int from = 0)
    {
        for (int i = from; i < profiles.Count; i++) if (SameProfile(profiles[i], profile)) return true;
        return false;
    }
    private bool IsDeferred(CustomerProfile profile) => ContainsProfile(deferredCustomers, profile);

    private CustomerProfile CreateRuntimeOrder(CustomerProfile profile, int day, bool restored = false)
    {
        if (profile == null) return null;
        if (profile.OrderState != null)
        {
            CustomerRewardInfo.InitializeOrder(profile, day);
            return profile;
        }
        var copy = Instantiate(profile);
        copy.CopyRuntimeOrderFrom(profile);
        copy.hideFlags = HideFlags.DontSave;
        copy.OrderState = CustomerOrderState.Create(restored);
        CustomerRewardInfo.InitializeOrder(copy, day);
        ownedOrders.Add(copy);
        return copy;
    }

    private void PrepareRuntimeOrders(CustomerDay day)
    {
        for (int i = 0; i < day.customers.Length; i++) day.customers[i] = CreateRuntimeOrder(day.customers[i], day.day);
        PublishOrderLinks(day);
    }
    private void PublishOrderLinks(CustomerDay day)
    {
        if (!CanCollectShopLogs || day == null) return;
        foreach (var profile in day.customers ?? Array.Empty<CustomerProfile>())
            if (profile?.OrderState != null) OrderRegistered?.Invoke(profile, day.day);
    }

    // Restore calls Enter/UseCustomer as part of reconstructing state, not player input.
    public IDisposable SuppressShopLogs()
    {
        shopLogSuppression++;
        return new ShopLogRestoreScope(this);
    }
    private sealed class ShopLogRestoreScope : IDisposable
    {
        private _DayManager owner;
        public ShopLogRestoreScope(_DayManager owner) => this.owner = owner;
        public void Dispose()
        {
            if (owner == null) return;
            var manager = owner;
            owner = null;
            manager.shopLogSuppression--;
            if (manager.shopLogSuppression != 0) return;
            manager.shopLogsReady = true;
            manager.PublishOrderLinks(manager.CurrentCustomerDay);
        }
    }
    private void OnDestroy()
    {
        foreach (var profile in ownedOrders) if (profile != null) Destroy(profile);
        ownedOrders.Clear();
    }

    private bool HasRequiredReferences()
    {
        if (settings != null && timerController != null && gameManager != null) return true;
        Debug.LogError("_DayManager에 DaySettings, SetTimer, GameManager를 연결하세요.", this);
        return false;
    }
}
