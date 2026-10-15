using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class DaySettlementUI : MonoBehaviour
{
    [SerializeField] private UnityEngine.InputSystem.InputActionAsset inputActions;
    private UnityEngine.InputSystem.InputActionMap confirmControls;
    private bool confirmRequested;
    private int openedFrame;
    private bool CanConfirmWithInput => settlementPanel != null && settlementPanel.activeInHierarchy &&
        declineButton != null && declineButton.isActiveAndEnabled && declineButton.interactable &&
        Time.frameCount > openedFrame &&
        (WeaponWorkshop.DialogueUI.Instance == null || !WeaponWorkshop.DialogueUI.Instance.IsOpen) &&
        (WeaponWorkshop.GameManager.Instance == null || (!WeaponWorkshop.GameManager.Instance.IsPaused &&
            !WeaponWorkshop.GameManager.Instance.InDialogue && !WeaponWorkshop.GameManager.Instance.UI.IsChoiceOpen));
    private static readonly Color NegativeValueColor = new Color(213f / 255f, 68f / 255f, 63f / 255f);
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
    [SerializeField, Min(0f)] private float dailyRentGap = 16f;
    [SerializeField, Min(1f)] private float dailyRentWidth = 340f;
    private TMPro.TextMeshProUGUI dailyRentText;
    private Coroutine morningNotice;
    public RectTransform TutorialConfirmButton => declineButton != null ? declineButton.transform as RectTransform : null;
    [SerializeField, Min(0f), Tooltip("돈·모듈 재화 숫자 연출 시간(초). 정산 중에도 실제 시간으로 재생합니다.")]
    private float moneyCountDuration = 0.25f;
    [SerializeField, Min(0.01f), Tooltip("사망 귀환 시 획득 모듈 재화를 잃는 슬롯 숫자 회전 시간(초).")]
    private float raidLossRollDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float insufficientFundsDuration = 1.2f;
    [SerializeField, Min(1)] private int insufficientFundsPulses = 3;
    [SerializeField, Min(1f)] private float insufficientFundsScale = 1.2f;
    private readonly BalanceDisplay moneyCounter = new BalanceDisplay();
    private readonly BalanceDisplay moduleCreditCounter = new BalanceDisplay();

    private void Awake()
    {
        confirmRequested = false;
        if (inputActions != null)
        {
            confirmControls = inputActions.FindActionMap("Workshop", true).Clone();
            confirmControls.FindAction("Interact", true).performed += _ =>
            { if (CanConfirmWithInput) confirmRequested = true; };
        }
        if (wallet == null) wallet = WeaponWorkshop.PlayerWallet.ForScene(this);
        moneyCounter.Initialize(moneyText);
        moduleCreditCounter.Initialize(moduleCreditText);
        dailyRentText = CreateDailyRentText();
        dailyRentText?.gameObject.SetActive(false);
        moneyDisplay?.gameObject.SetActive(false);
        moduleCreditDisplay?.gameObject.SetActive(false);
        todayIncomeText?.gameObject.SetActive(false);
        settlementPanel?.SetActive(false);
        morningNoticeText?.gameObject.SetActive(false);
        reopenSettlementButton?.gameObject.SetActive(false);
        if (questionText != null) questionText.gameObject.SetActive(false);
        SetButtonLabel(nextDayButton,"다음 날 아침");
        SetButtonLabel(declineButton,"확인");
        if (nextDayButton != null) nextDayButton.interactable = false;
        if (declineButton != null) declineButton.interactable = false;
        nextDayButton?.onClick.AddListener(StartNextDay);
        declineButton?.onClick.AddListener(DeclineNextDay);
        reopenSettlementButton?.onClick.AddListener(ReopenSettlement);
        closeSettlementButton?.onClick.AddListener(CloseSettlement);
    }

    private void OnEnable()
    {
        confirmControls?.Enable();
        if (dayManager != null) dayManager.Changed += Refresh;
        if (wallet != null) wallet.Changed += Refresh;
        if (wallet != null) wallet.PurchaseFundsInsufficient += ShowInsufficientFunds;
        if (wallet != null) wallet.ModuleCreditsLost += ShowModuleCreditLoss;
        Refresh();
    }

    private void OnDisable()
    {
        confirmControls?.Disable();
        confirmRequested = false;
        if (dayManager != null) dayManager.Changed -= Refresh;
        if (wallet != null) wallet.Changed -= Refresh;
        if (wallet != null) wallet.PurchaseFundsInsufficient -= ShowInsufficientFunds;
        if (wallet != null) wallet.ModuleCreditsLost -= ShowModuleCreditLoss;
        HideMorningNotice();
        moneyCounter.Reset();
        moduleCreditCounter.Reset();
        // Hide displays without changing the authored hierarchy or layout.
        moneyDisplay?.gameObject.SetActive(false);
        moduleCreditDisplay?.gameObject.SetActive(false);
        dailyRentText?.gameObject.SetActive(false);
        todayIncomeText?.gameObject.SetActive(false);
        settlementPanel?.SetActive(false);
        reopenSettlementButton?.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        confirmControls?.Dispose();
        nextDayButton?.onClick.RemoveListener(StartNextDay);
        declineButton?.onClick.RemoveListener(DeclineNextDay);
        reopenSettlementButton?.onClick.RemoveListener(ReopenSettlement);
        closeSettlementButton?.onClick.RemoveListener(CloseSettlement);
    }

    private void StartNextDay()
    {
        if (dayManager == null || !dayManager.IsSettlementOpen || !dayManager.CanStartNextDay) return;
        var feedback = WeaponWorkshop.FeedbackUI.For(this);
        feedback?.ShowChoice(this, "야간 습격을 하지 않고 다음 날로 넘어가시겠습니까?",
            ConfirmSkipNightRaid, null, "예", "아니오");
    }

    private void ConfirmSkipNightRaid()
    {
        if (dayManager == null || !dayManager.IsSettlementOpen || !dayManager.CanStartNextDay) return;
        HideMorningNotice();
        dayManager.StartNextDay(skippedNightRaid: true);
    }

    private void DeclineNextDay()
    {
        HideMorningNotice();
        if (dayManager != null) dayManager.DeclineNextDay();
    }
    private static void SetButtonLabel(Button button,string label)
    {
        if(button==null)return;
        var text=button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if(text!=null)text.text=label;
    }

    private void ReopenSettlement() => dayManager?.ShowSettlement();
    private void CloseSettlement() => dayManager?.DeclineNextDay();

    private void ShowInsufficientFunds(bool useModuleCredits)
    {
        WeaponWorkshop.FeedbackUI.For(this)?.ShowNotification(useModuleCredits ? "모듈 재료가 부족합니다." : "돈이 부족합니다.");
        (useModuleCredits ? moduleCreditCounter : moneyCounter).PulseInsufficientFunds(
            insufficientFundsDuration, insufficientFundsPulses, insufficientFundsScale);
    }

    private void ShowModuleCreditLoss(int previousAmount)
    {
        if (wallet == null) return;
        Refresh();
        moduleCreditCounter.RollLoss(previousAmount, wallet.ModuleCredits, raidLossRollDuration);
    }

    private IEnumerator HideMorningNoticeAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        morningNoticeText.gameObject.SetActive(false);
        morningNotice = null;
    }

    private void HideMorningNotice()
    {
        if (morningNotice != null) StopCoroutine(morningNotice);
        morningNotice = null;
        morningNoticeText?.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        bool show = dayManager != null && dayManager.IsNight && dayManager.IsSettlementOpen;
        if (show && settlementPanel != null && !settlementPanel.activeSelf) openedFrame = Time.frameCount;
        if (!show) confirmRequested = false;
        if (!show) WeaponWorkshop.FeedbackUI.For(this)?.CancelChoice(this);
        moneyDisplay?.gameObject.SetActive(wallet != null);
        moduleCreditDisplay?.gameObject.SetActive(wallet != null);
        bool hasDailyRent = wallet != null && dayManager != null && dayManager.CurrentCustomerDay != null;
        dailyRentText?.gameObject.SetActive(hasDailyRent);
        if (hasDailyRent && dailyRentText != null)
        {
            int rent = dayManager.CurrentCustomerDay.rent;
            dailyRentText.text = rent > 0 ? $"자리세: −$ {rent:N0}" : $"자리세: $ {rent:N0}";
            dailyRentText.color = rent > 0 ? NegativeValueColor : Color.white;
        }
        if (moduleCreditText != null && wallet != null)
            moduleCreditCounter.SetTarget(wallet.ModuleCredits, moneyCountDuration);
        if (moneyText != null && wallet != null)
        {
            moneyCounter.SetTarget(wallet.Balance, moneyCountDuration);
            int rent = dayManager != null && dayManager.CurrentCustomerDay != null
                ? dayManager.CurrentCustomerDay.rent : 0;
            long balanceAfterRent = (long)wallet.Balance - (dayManager != null && dayManager.IsNight ? 0 : rent);
            moneyCounter.SetNegative(balanceAfterRent <= 0);
        }
        if (todayIncomeText != null)
        {
            todayIncomeText.gameObject.SetActive(show);
            if (show)
            {
                long netIncome = (long)dayManager.TodayIncome - dayManager.TodayRent - dayManager.TodaySoldConstructionCost;
                string amount = netIncome < 0 ? $"−$ {-netIncome:N0}" : $"$ {netIncome:N0}";
                if (netIncome < 0) amount = $"<color=#D5443F>{amount}</color>";
                int balance = wallet != null ? wallet.Balance : 0;
                long previousBalance = (long)balance - netIncome;
                string previousAmount = previousBalance < 0 ? $"−$ {-previousBalance:N0}" : $"$ {previousBalance:N0}";
                string settlementAmount = netIncome < 0 ? $"(−$ {-netIncome:N0})" : $"$ {netIncome:N0}";
                string currentAmount = balance < 0 ? $"−$ {-(long)balance:N0}" : $"$ {balance:N0}";
                if (balance < 0) currentAmount = $"<color=#D5443F>{currentAmount}</color>";
                todayIncomeText.text = $"<size=18>최종 정산액</size>\n{amount}\n" +
                    $"<size=14>기존 보유 금액 + 최종 정산액 = 현재 보유 금액</size>\n" +
                    $"<size=18>{previousAmount} + {settlementAmount} = {currentAmount}</size>";
            }
        }
        if (show && receiptTitle != null) receiptTitle.text = dayManager.DayNumber == 0 ? "튜토리얼 - 영업 정산" : $"DAY {dayManager.DayNumber:00} - 영업 정산";
        if (show && summaryText != null)
        {
            string rentAmount = dayManager.TodayRent > 0
                ? $"<color=#D5443F>−$ {dayManager.TodayRent:N0}</color>"
                : $"$ {dayManager.TodayRent:N0}";
            int constructionCost = dayManager.TodaySoldConstructionCost;
            string constructionAmount = constructionCost > 0
                ? $"<color=#D5443F>−$ {constructionCost:N0}</color>" : "$ 0";
            summaryText.text = $"완료한 손님<pos=78%>{dayManager.CompletedGuests}명\n" +
                $"불만족 손님<pos=78%>{dayManager.DissatisfiedGuests}명\n" +
                $"살해한 손님<pos=78%>{dayManager.KilledGuests}명\n" +
                $"수입<pos=65%>+$ {dayManager.TodayIncome:N0}\n" +
                $"총기 제작비<pos=65%>{constructionAmount}\n" +
                $"자리세<pos=65%>{rentAmount}";
            if (dayManager.IsBankrupt)
                summaryText.text += "\n\n<color=#D5443F>자릿세를 낼 돈이 부족합니다.</color>";
        }
        if (nextDayButton != null)
        {
            nextDayButton.gameObject.SetActive(dayManager == null || !dayManager.IsBankrupt);
            nextDayButton.interactable = show && dayManager.CanStartNextDay;
        }
        if (declineButton != null) declineButton.interactable = show;
        reopenSettlementButton?.gameObject.SetActive(dayManager != null && dayManager.IsNight && !show &&
            (nightRaid == null || !nightRaid.IsBusy));
        if (!show) HideMorningNotice();
        settlementPanel?.SetActive(show);
    }

    private TMPro.TextMeshProUGUI CreateDailyRentText()
    {
        if (moneyDisplay == null || moneyText == null) return null;

        var textObject = new GameObject("DailyRentDisplay", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TMPro.TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(moneyDisplay, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(moneyDisplay.sizeDelta.x + dailyRentGap, 0f);
        rect.sizeDelta = new Vector2(dailyRentWidth, moneyDisplay.sizeDelta.y);

        var text = textObject.GetComponent<TMPro.TextMeshProUGUI>();
        text.font = moneyText.font;
        text.fontSize = moneyText.fontSize;
        text.fontWeight = moneyText.fontWeight;
        text.fontStyle = moneyText.fontStyle;
        text.color = moneyText.color;
        text.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        text.overflowMode = TMPro.TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.text = "자리세: $ 0";
        return text;
    }

    // Both balances use the same increase counting and decrease digit-roll effects.
    private sealed class BalanceDisplay
    {
        private TMPro.TextMeshProUGUI text;
        private WeaponWorkshop.MoneyRollDisplay roll;
        private int displayed, target, start;
        private float elapsed;
        private bool initialized;
        private Color originalColor;
        private Vector3 originalScale;
        private float pulseElapsed, pulseDuration, pulseScale;
        private int pulseCount;
        private Color displayColor;

        public void Initialize(TMPro.TextMeshProUGUI source)
        {
            text = source;
            if (text == null) return;
            originalColor = text.color;
            displayColor = originalColor;
            originalScale = text.rectTransform.localScale;
            roll = text.GetComponent<WeaponWorkshop.MoneyRollDisplay>();
            if (roll == null) roll = text.gameObject.AddComponent<WeaponWorkshop.MoneyRollDisplay>();
            roll.Initialize(text);
            roll.Show(0);
        }

        public void Reset()
        {
            RestoreAppearance();
            initialized = false;
            roll?.Show(target);
        }

        public void PulseInsufficientFunds(float duration, int count, float scale)
        {
            if (text == null) return;
            pulseElapsed = 0f;
            pulseDuration = Mathf.Max(0.01f, duration);
            pulseCount = Mathf.Max(1, count);
            pulseScale = Mathf.Max(1f, scale);
        }

        public void SetNegative(bool negative)
        {
            if (text == null) return;
            displayColor = negative
                ? new Color(NegativeValueColor.r, NegativeValueColor.g, NegativeValueColor.b, originalColor.a)
                : originalColor;
            roll?.SetColor(displayColor);
        }

        private void RestoreAppearance()
        {
            pulseDuration = 0f;
            if (text == null) return;
            roll?.SetColor(displayColor);
            text.rectTransform.localScale = originalScale;
        }

        private void TickInsufficientFunds()
        {
            if (pulseDuration <= 0f || text == null) return;
            pulseElapsed += Time.unscaledDeltaTime;
            if (pulseElapsed >= pulseDuration) { RestoreAppearance(); return; }
            float wave = Mathf.Sin(pulseElapsed / pulseDuration * pulseCount * Mathf.PI);
            float pulse = wave * wave;
            Color red = new Color(1f, 0f, 0f, displayColor.a);
            roll?.SetColor(Color.Lerp(displayColor, red, pulse));
            text.rectTransform.localScale = originalScale * Mathf.Lerp(1f, pulseScale, pulse);
        }

        public void SetTarget(int amount, float duration)
        {
            if (text == null) return;
            if (!initialized || duration <= 0f)
            {
                initialized = true;
                displayed = target = amount;
                Show(amount);
                return;
            }
            if (target == amount) return;
            if (amount < target)
            {
                target = start = displayed = amount;
                elapsed = 0f;
                if (roll != null) roll.RollTo(amount, duration);
                else text.text = amount.ToString("N0");
                return;
            }
            Show(displayed);
            start = displayed;
            target = amount;
            elapsed = 0f;
        }

        public void RollLoss(int previousAmount, int amount, float duration)
        {
            if (text == null || previousAmount <= amount) return;
            RestoreAppearance();
            initialized = true;
            displayed = target = start = amount;
            elapsed = 0f;
            Show(previousAmount);
            if (roll != null) roll.RollTo(amount, duration);
            else Show(amount);
        }

        public void Tick(float duration)
        {
            TickInsufficientFunds();
            if (!initialized || text == null || displayed == target) return;
            elapsed += Time.unscaledDeltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            double eased = 1d - (1d - t) * (1d - t) * (1d - t);
            int amount = t >= 1f ? target
                : (int)System.Math.Round(start + ((double)target - start) * eased);
            if (amount == displayed) return;
            displayed = amount;
            Show(amount);
        }

        private void Show(int amount)
        {
            if (roll != null) roll.Show(amount);
            else if (text != null) text.text = amount.ToString("N0");
        }
    }

    private void LateUpdate()
    {
        if (confirmRequested)
        {
            confirmRequested = false;
            if (CanConfirmWithInput) declineButton.onClick.Invoke();
        }
        moneyCounter.Tick(moneyCountDuration);
        moduleCreditCounter.Tick(moneyCountDuration);
    }

}
