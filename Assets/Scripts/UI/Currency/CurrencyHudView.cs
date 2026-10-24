using System;

using TMPro;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// HudLayer/CurrencyGroup 안의 재화 HUD(돈·크레딧·티켓·자리세) 표시와 숫자 롤링 연출을 담당하는 View입니다.
    /// 정산 화면(DaySettlementUI/SettlementView) 및 기타 시스템이 UIManager.Screen<CurrencyHudView>()를 통해 호출합니다.
    /// </summary>
    public sealed class CurrencyHudView : MonoBehaviour
    {
        private const string SCREEN_NAME = "CurrencyHudView";
        private static readonly Color NEGATIVE_VALUE_COLOR = new Color(213f / 255f, 68f / 255f, 63f / 255f);

        [SerializeField] private RectTransform m_moneyDisplay;
        [SerializeField] private TextMeshProUGUI m_moneyText;
        [SerializeField] private MoneyRollDisplay m_moneyRoll;

        [SerializeField] private RectTransform m_moduleCreditDisplay;
        [SerializeField] private TextMeshProUGUI m_moduleCreditText;
        [SerializeField] private MoneyRollDisplay m_moduleCreditRoll;

        [SerializeField] private GameObject m_slotTicketDisplay;
        [SerializeField] private TextMeshProUGUI m_slotTicketText;
        [SerializeField] private MoneyRollDisplay m_slotTicketRoll;

        [SerializeField] private TextMeshProUGUI m_dailyRentText;

        [SerializeField, Min(0f), Tooltip("돈·모듈 재화 숫자 연출 시간(초).")]
        private float m_moneyCountDuration = 0.25f;
        [SerializeField, Min(0.01f), Tooltip("사망 귀환 시 획득 모듈 재화를 잃는 슬롯 숫자 회전 시간(초).")]
        private float m_raidLossRollDuration = 0.25f;
        [SerializeField, Min(0.01f)] private float m_insufficientFundsDuration = 1.2f;
        [SerializeField, Min(1)] private int m_insufficientFundsPulses = 3;
        [SerializeField, Min(1f)] private float m_insufficientFundsScale = 1.2f;

        private readonly BalanceDisplay m_moneyCounter = new BalanceDisplay();
        private readonly BalanceDisplay m_moduleCreditCounter = new BalanceDisplay();
        private readonly BalanceDisplay m_slotTicketCounter = new BalanceDisplay();

        private bool m_isInitialized;

        public RectTransform MoneyDisplay => m_moneyDisplay;
        public RectTransform ModuleCreditDisplay => m_moduleCreditDisplay;
        public GameObject SlotTicketDisplay => m_slotTicketDisplay;
        public TextMeshProUGUI DailyRentText => m_dailyRentText;

        public float MoneyCountDuration
        {
            get => m_moneyCountDuration;
            set => m_moneyCountDuration = Mathf.Max(0f, value);
        }

        public float RaidLossRollDuration
        {
            get => m_raidLossRollDuration;
            set => m_raidLossRollDuration = Mathf.Max(0.01f, value);
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            m_moneyCounter.Tick(m_moneyCountDuration);
            m_moduleCreditCounter.Tick(m_moneyCountDuration);
            m_slotTicketCounter.Tick(m_moneyCountDuration);
        }

        public bool Initialize()
        {
            EnsureInitialized();
            return m_isInitialized;
        }

        public void BindReferences(
            RectTransform _moneyDisplay, TextMeshProUGUI _moneyText, MoneyRollDisplay _moneyRoll,
            RectTransform _moduleCreditDisplay, TextMeshProUGUI _moduleCreditText, MoneyRollDisplay _moduleCreditRoll,
            GameObject _slotTicketDisplay, TextMeshProUGUI _slotTicketText, MoneyRollDisplay _slotTicketRoll,
            TextMeshProUGUI _dailyRentText)
        {
            m_moneyDisplay = _moneyDisplay;
            m_moneyText = _moneyText;
            m_moneyRoll = _moneyRoll;
            m_moduleCreditDisplay = _moduleCreditDisplay;
            m_moduleCreditText = _moduleCreditText;
            m_moduleCreditRoll = _moduleCreditRoll;
            m_slotTicketDisplay = _slotTicketDisplay;
            m_slotTicketText = _slotTicketText;
            m_slotTicketRoll = _slotTicketRoll;
            m_dailyRentText = _dailyRentText;

            m_isInitialized = false;
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (m_isInitialized)
            {
                return;
            }

            AutoResolveReferences();

            if (m_slotTicketText != null && m_moduleCreditText != null)
            {
                m_slotTicketText.enableAutoSizing = m_moduleCreditText.enableAutoSizing;
                m_slotTicketText.fontSizeMin = m_moduleCreditText.fontSizeMin;
                m_slotTicketText.fontSizeMax = m_moduleCreditText.fontSizeMax;
                m_slotTicketText.fontSize = m_moduleCreditText.fontSize;
            }

            m_moneyCounter.Initialize(m_moneyText, m_moneyRoll);
            m_moduleCreditCounter.Initialize(m_moduleCreditText, m_moduleCreditRoll);
            m_slotTicketCounter.Initialize(m_slotTicketText, m_slotTicketRoll);

            m_isInitialized = true;
        }

        private void AutoResolveReferences()
        {
            if (m_moneyDisplay == null)
            {
                Transform t = transform.Find("MoneyDisplay");
                if (t != null)
                {
                    m_moneyDisplay = t as RectTransform;
                }
            }

            if (m_moneyText == null && m_moneyDisplay != null)
            {
                Transform t = m_moneyDisplay.Find("MoneyText");
                if (t != null)
                {
                    m_moneyText = t.GetComponent<TextMeshProUGUI>();
                }
            }

            if (m_moneyRoll == null && m_moneyText != null)
            {
                m_moneyRoll = m_moneyText.GetComponent<MoneyRollDisplay>();
            }

            if (m_dailyRentText == null && m_moneyDisplay != null)
            {
                Transform t = m_moneyDisplay.Find("DailyRentDisplay");
                if (t != null)
                {
                    m_dailyRentText = t.GetComponent<TextMeshProUGUI>();
                }
            }

            if (m_moduleCreditDisplay == null)
            {
                Transform t = transform.Find("ModuleCreditDisplay");
                if (t != null)
                {
                    m_moduleCreditDisplay = t as RectTransform;
                }
            }

            if (m_moduleCreditText == null && m_moduleCreditDisplay != null)
            {
                Transform t = m_moduleCreditDisplay.Find("ModuleCreditText");
                if (t != null)
                {
                    m_moduleCreditText = t.GetComponent<TextMeshProUGUI>();
                }
            }

            if (m_moduleCreditRoll == null && m_moduleCreditText != null)
            {
                m_moduleCreditRoll = m_moduleCreditText.GetComponent<MoneyRollDisplay>();
            }

            if (m_slotTicketDisplay == null)
            {
                Transform t = transform.Find("SlotTicketDisplay");
                if (t != null)
                {
                    m_slotTicketDisplay = t.gameObject;
                }
            }

            if (m_slotTicketText == null && m_slotTicketDisplay != null)
            {
                Transform t = m_slotTicketDisplay.transform.Find("ModuleCreditText");
                if (t != null)
                {
                    m_slotTicketText = t.GetComponent<TextMeshProUGUI>();
                }
            }

            if (m_slotTicketRoll == null && m_slotTicketText != null)
            {
                m_slotTicketRoll = m_slotTicketText.GetComponent<MoneyRollDisplay>();
            }
        }

        public void SetBalanceDisplaysVisible(bool _visible)
        {
            EnsureInitialized();
            if (m_moneyDisplay != null)
            {
                m_moneyDisplay.gameObject.SetActive(_visible);
            }

            // 보유 부품은 정산 화면에 표시하지 않는다. 원본 행은 티켓 행 저작의 기준으로만 남긴다.
            if (m_moduleCreditDisplay != null)
            {
                m_moduleCreditDisplay.gameObject.SetActive(false);
            }

            if (m_slotTicketDisplay != null)
            {
                m_slotTicketDisplay.SetActive(_visible);
            }
        }

        public void SetSlotTickets(int _amount)
        {
            EnsureInitialized();
            m_slotTicketCounter.SetTarget(_amount, m_moneyCountDuration);
        }

        public void SetModuleCredits(int _amount)
        {
            EnsureInitialized();
            m_moduleCreditCounter.SetTarget(_amount, m_moneyCountDuration);
        }

        public void SetMoneyBalance(int _amount, bool _negative)
        {
            EnsureInitialized();
            m_moneyCounter.SetTarget(_amount, m_moneyCountDuration);
            m_moneyCounter.SetNegative(_negative);
        }

        public void ShowDailyRent(int _rent, long _principal, long _interest)
        {
            EnsureInitialized();
            if (m_dailyRentText == null)
            {
                return;
            }

            m_dailyRentText.gameObject.SetActive(true);
            m_dailyRentText.text = $"자리세: −$ {_rent:N0}";
            if (_principal > 0)
            {
                m_dailyRentText.text += $"\n대출 원금: −$ {_principal:N0}\n대출 이자: −$ {_interest:N0}";
            }

            m_dailyRentText.color = _rent > 0 || _principal > 0 ? NEGATIVE_VALUE_COLOR : Color.white;
        }

        public void HideDailyRent()
        {
            if (m_dailyRentText != null)
            {
                m_dailyRentText.gameObject.SetActive(false);
            }
        }

        public void PulseInsufficientFunds(bool _useModuleCredits)
        {
            EnsureInitialized();
            BalanceDisplay counter = _useModuleCredits ? m_moduleCreditCounter : m_moneyCounter;
            counter.PulseInsufficientFunds(m_insufficientFundsDuration, m_insufficientFundsPulses, m_insufficientFundsScale);
        }

        public void RollModuleCreditLoss(int _previousAmount, int _amount)
        {
            EnsureInitialized();
            m_moduleCreditCounter.RollLoss(_previousAmount, _amount, m_raidLossRollDuration);
        }

        public void ApplyDisabledState()
        {
            EnsureInitialized();
            m_moneyCounter.Reset();
            m_moduleCreditCounter.Reset();
            m_slotTicketCounter.Reset();
            SetBalanceDisplaysVisible(false);
            HideDailyRent();
        }

        /// <summary>
        /// 잔액·부품·티켓 숫자의 증감 카운트와 자릿수 굴림·부족 펄스 연출. 원본 DaySettlementUI.BalanceDisplay를 옮겼다.
        /// </summary>
        private sealed class BalanceDisplay
        {
            private TextMeshProUGUI text;
            private MoneyRollDisplay roll;
            private int displayed, target, start;
            private float elapsed;
            private bool initialized;
            private Color originalColor;
            private Vector3 originalScale;
            private float pulseElapsed, pulseDuration, pulseScale;
            private int pulseCount;
            private Color displayColor;

            public void Initialize(TextMeshProUGUI _source, MoneyRollDisplay _roll)
            {
                text = _source;
                roll = _roll;
                if (text == null || roll == null)
                {
                    return;
                }

                originalColor = text.color;
                displayColor = originalColor;
                originalScale = text.rectTransform.localScale;
                roll.Initialize(text);
                roll.Show(0);
            }

            public void Reset()
            {
                RestoreAppearance();
                initialized = false;
                roll?.Show(target);
            }

            public void PulseInsufficientFunds(float _duration, int _count, float _scale)
            {
                if (text == null)
                {
                    return;
                }

                pulseElapsed = 0f;
                pulseDuration = Mathf.Max(0.01f, _duration);
                pulseCount = Mathf.Max(1, _count);
                pulseScale = Mathf.Max(1f, _scale);
            }

            public void SetNegative(bool _negative)
            {
                if (text == null)
                {
                    return;
                }

                displayColor = _negative
                    ? new Color(NEGATIVE_VALUE_COLOR.r, NEGATIVE_VALUE_COLOR.g, NEGATIVE_VALUE_COLOR.b, originalColor.a)
                    : originalColor;
                roll?.SetColor(displayColor);
            }

            private void RestoreAppearance()
            {
                pulseDuration = 0f;
                if (text == null)
                {
                    return;
                }

                roll?.SetColor(displayColor);
                text.rectTransform.localScale = originalScale;
            }

            private void TickInsufficientFunds()
            {
                if (pulseDuration <= 0f || text == null)
                {
                    return;
                }

                pulseElapsed += Time.unscaledDeltaTime;
                if (pulseElapsed >= pulseDuration)
                {
                    RestoreAppearance();
                    return;
                }

                float wave = Mathf.Sin(pulseElapsed / pulseDuration * pulseCount * Mathf.PI);
                float pulse = wave * wave;
                Color red = new Color(1f, 0f, 0f, displayColor.a);
                roll?.SetColor(Color.Lerp(displayColor, red, pulse));
                text.rectTransform.localScale = originalScale * Mathf.Lerp(1f, pulseScale, pulse);
            }

            public void SetTarget(int _amount, float _duration)
            {
                if (text == null)
                {
                    return;
                }

                if (!initialized || _duration <= 0f)
                {
                    initialized = true;
                    displayed = target = _amount;
                    Show(_amount);
                    return;
                }

                if (target == _amount)
                {
                    return;
                }

                if (_amount < target)
                {
                    target = start = displayed = _amount;
                    elapsed = 0f;
                    if (roll != null)
                    {
                        roll.RollTo(_amount, _duration);
                    }
                    else
                    {
                        text.text = _amount.ToString("N0");
                    }

                    return;
                }

                Show(displayed);
                start = displayed;
                target = _amount;
                elapsed = 0f;
            }

            public void RollLoss(int _previousAmount, int _amount, float _duration)
            {
                if (text == null || _previousAmount <= _amount)
                {
                    return;
                }

                RestoreAppearance();
                initialized = true;
                displayed = target = start = _amount;
                elapsed = 0f;
                Show(_previousAmount);
                if (roll != null)
                {
                    roll.RollTo(_amount, _duration);
                }
            }

            public void Tick(float _duration)
            {
                TickInsufficientFunds();
                if (!initialized || text == null || displayed == target)
                {
                    return;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = _duration > 0f ? Mathf.Clamp01(elapsed / _duration) : 1f;
                double eased = 1d - (1d - t) * (1d - t) * (1d - t);
                int amount = t >= 1f ? target
                    : (int)System.Math.Round(start + ((double)target - start) * eased);
                if (amount == displayed)
                {
                    return;
                }

                displayed = amount;
                Show(amount);
            }

            private void Show(int _amount)
            {
                if (roll != null)
                {
                    roll.Show(_amount);
                    return;
                }

                if (text != null)
                {
                    text.text = _amount.ToString("N0");
                }
            }
        }
    }
}
