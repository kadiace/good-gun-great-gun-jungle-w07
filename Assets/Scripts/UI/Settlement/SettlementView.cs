using System;
using System.Collections;

using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 저작된 영업 정산 UI의 참조 보관, 정산·잔액·자리세 표시와 재화 카운트 연출,
    /// 버튼·확인 입력의 이벤트 전달, 패널 포커스 적용·복원만 담당하는 View입니다.
    /// 지갑·정산 상태 같은 도메인은 Presenter를 통해서만 읽습니다.
    /// </summary>
    public sealed class SettlementView : MonoBehaviour
    {
        private const string VIEW_NAME = "SettlementView";
        private const string NEXT_DAY_LABEL = "다음 날 아침";
        private const string CONFIRM_LABEL = "확인";
        private static readonly Color NEGATIVE_VALUE_COLOR = new Color(213f / 255f, 68f / 255f, 63f / 255f);

        /// <summary>
        /// 정산 영수증 한 화면분의 도메인 집계 값. Presenter가 매 갱신 때 채우는 표시 전달 값이다.
        /// </summary>
        [Serializable]
        public sealed class SettlementSummaryData
        {
            public int CompletedGuests;
            public int DissatisfiedGuests;
            public int KilledGuests;
            public int TodayIncome;
            public int TodayLoanIncome;
            public int SoldConstructionCost;
            public int TodayRent;
            public int TodayLoanRepayment;
            public int TodayLoanInterest;
            public bool IsBankrupt;
        }

        // Tools/UI/Authoring/Settlement/Author가 새로 저작해 연결하는 참조다. 기존 브리지에는 직렬화 필드가 없었다.
        [SerializeField] private GameObject m_slotTicketDisplay;
        [SerializeField] private TextMeshProUGUI m_slotTicketText;
        [SerializeField] private TextMeshProUGUI m_dailyRentText;
        [SerializeField] private MoneyRollDisplay m_moneyRoll;
        [SerializeField] private MoneyRollDisplay m_moduleCreditRoll;
        [SerializeField] private MoneyRollDisplay m_slotTicketRoll;

        // 실행 시 원본은 DaySettlementUI 브리지의 직렬화 참조이며 Configure로 전달받는다.
        private GameObject m_panel;
        private TextMeshProUGUI m_summaryText;
        private TextMeshProUGUI m_receiptTitle;
        private TextMeshProUGUI m_questionText;
        private Button m_nextDayButton;
        private Button m_confirmButton;
        private Button m_reopenButton;
        private Button m_closeButton;
        private TextMeshProUGUI m_morningNoticeText;
        private RectTransform m_moneyDisplay;
        private TextMeshProUGUI m_moneyText;
        private RectTransform m_moduleCreditDisplay;
        private TextMeshProUGUI m_moduleCreditText;
        private TextMeshProUGUI m_todayIncomeText;
        private UnityEngine.InputSystem.InputActionAsset m_inputActions;

        private float m_moneyCountDuration = 0.25f;
        private float m_raidLossRollDuration = 0.25f;
        private float m_insufficientFundsDuration = 1.2f;
        private int m_insufficientFundsPulses = 3;
        private float m_insufficientFundsScale = 1.2f;

        private readonly BalanceDisplay m_moneyCounter = new BalanceDisplay();
        private readonly BalanceDisplay m_moduleCreditCounter = new BalanceDisplay();
        private readonly BalanceDisplay m_slotTicketCounter = new BalanceDisplay();

        private UnityEngine.InputSystem.InputActionMap m_confirmControls;
        private bool m_confirmQueued;
        private int m_openedFrame;
        private Coroutine m_morningNotice;
        private GameObject m_savedFocus;
        private bool m_isListenerBound;
        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        public event Action ConfirmInputPerformed;
        public event Action NextDayRequested;
        public event Action DeclineRequested;
        public event Action ReopenRequested;
        public event Action CloseRequested;

        public bool IsInitialized
        {
            get
            {
                return m_isInitialized;
            }
        }

        public bool IsActiveAndEnabled
        {
            get
            {
                return isActiveAndEnabled;
            }
        }

        public bool IsPanelOpen
        {
            get
            {
                return m_panel != null && m_panel.activeInHierarchy;
            }
        }

        /// <summary>정산 패널이 열린 프레임. 확인 입력은 열린 프레임 이후에만 받는다.</summary>
        public int OpenedFrame
        {
            get
            {
                return m_openedFrame;
            }
        }

        public bool IsConfirmInputReady
        {
            get
            {
                return m_confirmButton != null && m_confirmButton.isActiveAndEnabled && m_confirmButton.interactable;
            }
        }

        public Selectable ConfirmSelectable
        {
            get
            {
                return m_confirmButton;
            }
        }

        public Selectable NextDaySelectable
        {
            get
            {
                return m_nextDayButton;
            }
        }

        private void Awake()
        {
            // Configure·Initialize 전에 깨어나더라도 숨김 상태만 보장한다. 이미 초기화됐으면 다시 숨기지 않는다.
            if (!m_isInitialized)
            {
                ApplyInitialHiddenState();
            }
        }

        private void LateUpdate()
        {
            if (m_confirmQueued)
            {
                m_confirmQueued = false;
                ConfirmInputPerformed?.Invoke();
            }

            m_moneyCounter.Tick(m_moneyCountDuration);
            m_moduleCreditCounter.Tick(m_moneyCountDuration);
            m_slotTicketCounter.Tick(m_moneyCountDuration);
        }

        private void OnDestroy()
        {
            DisposeConfirmControls();
            UnbindListeners();
        }

        /// <summary>
        /// 기존 DaySettlementUI 브리지에 직렬화된 참조와 연출 값을 전달받는다. 표시 상태는 바꾸지 않는다.
        /// </summary>
        public void Configure(GameObject _panel, TextMeshProUGUI _summaryText, TextMeshProUGUI _receiptTitle, TextMeshProUGUI _questionText,
            Button _nextDayButton, Button _confirmButton, Button _reopenButton, Button _closeButton,
            TextMeshProUGUI _morningNoticeText, RectTransform _moneyDisplay, TextMeshProUGUI _moneyText,
            RectTransform _moduleCreditDisplay, TextMeshProUGUI _moduleCreditText, TextMeshProUGUI _todayIncomeText,
            UnityEngine.InputSystem.InputActionAsset _inputActions,
            float _moneyCountDuration, float _raidLossRollDuration, float _insufficientFundsDuration,
            int _insufficientFundsPulses, float _insufficientFundsScale)
        {
            m_panel = _panel;
            m_summaryText = _summaryText;
            m_receiptTitle = _receiptTitle;
            m_questionText = _questionText;
            m_nextDayButton = _nextDayButton;
            m_confirmButton = _confirmButton;
            m_reopenButton = _reopenButton;
            m_closeButton = _closeButton;
            m_morningNoticeText = _morningNoticeText;
            m_moneyDisplay = _moneyDisplay;
            m_moneyText = _moneyText;
            m_moduleCreditDisplay = _moduleCreditDisplay;
            m_moduleCreditText = _moduleCreditText;
            m_todayIncomeText = _todayIncomeText;
            m_inputActions = _inputActions;
            m_moneyCountDuration = Mathf.Max(0f, _moneyCountDuration);
            m_raidLossRollDuration = Mathf.Max(0.01f, _raidLossRollDuration);
            m_insufficientFundsDuration = Mathf.Max(0.01f, _insufficientFundsDuration);
            m_insufficientFundsPulses = Mathf.Max(1, _insufficientFundsPulses);
            m_insufficientFundsScale = Mathf.Max(1f, _insufficientFundsScale);
        }

        /// <summary>
        /// 필수 참조를 확인하고 시작 숨김 상태·버튼 이벤트·확인 입력을 연결한다. 중복 호출에 안전하다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            m_confirmQueued = false;
            m_openedFrame = 0;
            ApplyInitialHiddenState();
            SetButtonLabel(m_nextDayButton, NEXT_DAY_LABEL);
            SetButtonLabel(m_confirmButton, CONFIRM_LABEL);
            if (m_nextDayButton != null)
            {
                m_nextDayButton.interactable = false;
            }

            if (m_confirmButton != null)
            {
                m_confirmButton.interactable = false;
            }

            // 기존 런타임 복제는 씬 오버라이드가 적용된 부품 숫자 글자 크기를 그대로 가져왔다. 저작된 티켓 숫자도 같은 크기를 따른다.
            m_slotTicketText.enableAutoSizing = m_moduleCreditText.enableAutoSizing;
            m_slotTicketText.fontSizeMin = m_moduleCreditText.fontSizeMin;
            m_slotTicketText.fontSizeMax = m_moduleCreditText.fontSizeMax;
            m_slotTicketText.fontSize = m_moduleCreditText.fontSize;

            m_moneyCounter.Initialize(m_moneyText, m_moneyRoll);
            m_moduleCreditCounter.Initialize(m_moduleCreditText, m_moduleCreditRoll);
            m_slotTicketCounter.Initialize(m_slotTicketText, m_slotTicketRoll);
            BindListeners();
            m_isInitialized = true;
            return true;
        }

        public void EnableConfirmInput()
        {
            if (m_confirmControls == null && m_inputActions != null)
            {
                m_confirmControls = m_inputActions.FindActionMap("Workshop", true).Clone();
                m_confirmControls.FindAction("Interact", true).performed += OnConfirmActionPerformed;
            }

            m_confirmControls?.Enable();
        }

        public void DisableConfirmInput()
        {
            m_confirmControls?.Disable();
            m_confirmQueued = false;
        }

        public void SetBalanceDisplaysVisible(bool _visible)
        {
            if (m_moneyDisplay != null)
            {
                m_moneyDisplay.gameObject.SetActive(_visible);
            }

            if (m_moduleCreditDisplay != null)
            {
                m_moduleCreditDisplay.gameObject.SetActive(_visible);
            }

            if (m_slotTicketDisplay != null)
            {
                m_slotTicketDisplay.gameObject.SetActive(_visible);
            }
        }

        public void SetSlotTickets(int _amount)
        {
            m_slotTicketCounter.SetTarget(_amount, m_moneyCountDuration);
        }

        public void SetModuleCredits(int _amount)
        {
            m_moduleCreditCounter.SetTarget(_amount, m_moneyCountDuration);
        }

        public void SetMoneyBalance(int _amount, bool _negative)
        {
            m_moneyCounter.SetTarget(_amount, m_moneyCountDuration);
            m_moneyCounter.SetNegative(_negative);
        }

        public void ShowDailyRent(int _rent, long _principal, long _interest)
        {
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

        public void SetTodayIncomeVisible(bool _visible)
        {
            if (m_todayIncomeText != null)
            {
                m_todayIncomeText.gameObject.SetActive(_visible);
            }
        }

        public void SetTodayIncome(long _netIncome, int _balance)
        {
            if (m_todayIncomeText == null)
            {
                return;
            }

            string amount = _netIncome < 0 ? $"−$ {-_netIncome:N0}" : $"$ {_netIncome:N0}";
            if (_netIncome < 0)
            {
                amount = $"<color=#D5443F>{amount}</color>";
            }

            long previousBalance = (long)_balance - _netIncome;
            string previousAmount = previousBalance < 0 ? $"−$ {-previousBalance:N0}" : $"$ {previousBalance:N0}";
            string settlementAmount = _netIncome < 0 ? $"(−$ {-_netIncome:N0})" : $"$ {_netIncome:N0}";
            string currentAmount = _balance < 0 ? $"−$ {-(long)_balance:N0}" : $"$ {_balance:N0}";
            if (_balance < 0)
            {
                currentAmount = $"<color=#D5443F>{currentAmount}</color>";
            }

            m_todayIncomeText.text = $"<size=61.11111%>최종 정산액</size>\n{amount}\n" +
                $"<size=50%>기존 보유 금액 + 최종 정산액 = 현재 보유 금액</size>\n" +
                $"<size=61.11111%>{previousAmount} + {settlementAmount} = {currentAmount}</size>";
        }

        public void SetReceiptTitle(string _title)
        {
            if (m_receiptTitle != null)
            {
                m_receiptTitle.text = _title ?? string.Empty;
            }
        }

        public void SetSummary(SettlementSummaryData _data)
        {
            if (_data == null || m_summaryText == null)
            {
                return;
            }

            string rentAmount = _data.TodayRent > 0
                ? $"<color=#D5443F>−$ {_data.TodayRent:N0}</color>"
                : $"$ {_data.TodayRent:N0}";
            string constructionAmount = _data.SoldConstructionCost > 0
                ? $"<color=#D5443F>−$ {_data.SoldConstructionCost:N0}</color>" : "$ 0";
            m_summaryText.text = $"완료한 손님<pos=78%>{_data.CompletedGuests}명\n" +
                $"불만족 손님<pos=78%>{_data.DissatisfiedGuests}명\n" +
                $"살해한 손님<pos=78%>{_data.KilledGuests}명\n" +
                $"수입<pos=65%>+$ {_data.TodayIncome:N0}\n" +
                (_data.TodayLoanIncome > 0 ? $"대출 원금<pos=65%>+$ {_data.TodayLoanIncome:N0}\n" : "") +
                $"총기 제작비<pos=65%>{constructionAmount}\n" +
                $"자리세<pos=65%>{rentAmount}";
            if (_data.TodayLoanRepayment > 0)
            {
                m_summaryText.text += $"\n대출 원금<pos=65%><color=#D5443F>−$ {_data.TodayLoanRepayment:N0}</color>" +
                    $"\n대출 이자<pos=65%><color=#D5443F>−$ {_data.TodayLoanInterest:N0}</color>";
            }

            if (_data.IsBankrupt)
            {
                m_summaryText.text += "\n\n<color=#D5443F>자릿세를 낼 돈이 부족합니다.</color>";
            }
        }

        public void SetNextDayState(bool _active, bool _interactable)
        {
            if (m_nextDayButton == null)
            {
                return;
            }

            m_nextDayButton.gameObject.SetActive(_active);
            m_nextDayButton.interactable = _interactable;
            if (_active && _interactable && IsPanelOpen && (m_confirmButton == null || !m_confirmButton.interactable))
            {
                UIRoot.SetFocus(m_nextDayButton);
            }
        }

        public void SetConfirmInteractable(bool _interactable)
        {
            if (m_confirmButton != null)
            {
                m_confirmButton.interactable = _interactable;
                if (_interactable && IsPanelOpen)
                {
                    UIRoot.SetFocus(m_confirmButton, m_nextDayButton != null ? new Selectable[] { m_nextDayButton } : null);
                }
            }
        }

        public void SetReopenVisible(bool _visible)
        {
            if (m_reopenButton != null)
            {
                m_reopenButton.gameObject.SetActive(_visible);
            }
        }

        public void SetPanelVisible(bool _visible)
        {
            if (m_panel == null)
            {
                return;
            }

            if (_visible && !m_panel.activeSelf)
            {
                m_openedFrame = Time.frameCount;
                UIRoot.SaveFocus(ref m_savedFocus, transform);
            }

            if (!_visible)
            {
                m_confirmQueued = false;
            }

            m_panel.SetActive(_visible);

            if (_visible)
            {
                Selectable defaultBtn = (m_confirmButton != null && m_confirmButton.interactable)
                    ? m_confirmButton
                    : (m_nextDayButton != null && m_nextDayButton.interactable ? (Selectable)m_nextDayButton : null);
                if (defaultBtn != null)
                {
                    UIRoot.SetFocus(defaultBtn);
                }
            }
            else
            {
                UIRoot.RestoreFocus(ref m_savedFocus);
            }
        }

        public void HideMorningNotice()
        {
            if (m_morningNotice != null)
            {
                StopCoroutine(m_morningNotice);
            }

            m_morningNotice = null;
            if (m_morningNoticeText != null)
            {
                m_morningNoticeText.gameObject.SetActive(false);
            }
        }

        public void PulseInsufficientFunds(bool _useModuleCredits)
        {
            BalanceDisplay counter = _useModuleCredits ? m_moduleCreditCounter : m_moneyCounter;
            counter.PulseInsufficientFunds(m_insufficientFundsDuration, m_insufficientFundsPulses, m_insufficientFundsScale);
        }

        public void RollModuleCreditLoss(int _previousAmount, int _amount)
        {
            m_moduleCreditCounter.RollLoss(_previousAmount, _amount, m_raidLossRollDuration);
        }

        /// <summary>
        /// 비활성 시 원래 상태로 되돌린다. 표시 중이던 연출과 패널을 숨기고 저작 계층·레이아웃은 그대로 둔다.
        /// </summary>
        public void ApplyDisabledState()
        {
            DisableConfirmInput();
            HideMorningNotice();
            m_moneyCounter.Reset();
            m_moduleCreditCounter.Reset();
            m_slotTicketCounter.Reset();
            SetBalanceDisplaysVisible(false);
            HideDailyRent();
            SetTodayIncomeVisible(false);
            SetPanelVisible(false);
            SetReopenVisible(false);
        }

        /// <summary>
        /// 패널 안에서 현재 선택된 오브젝트를 돌려준다. 패널 밖 선택이면 null이다.
        /// </summary>
        public GameObject CapturePanelFocus()
        {
            GameObject selected = GetValidSelectedObject();
            if (selected == null || !IsInsidePanel(selected.transform))
            {
                return null;
            }

            return selected;
        }

        /// <summary>
        /// 복원할 선택이 유효하면 그대로 쓰고, 아니면 화면 기본 선택(다음 날 버튼 → 확인 버튼 순)을 적용한다.
        /// </summary>
        public void ApplyPanelFocus(Selectable _preferred, GameObject _restored)
        {
            if (EventSystem.current == null)
            {
                return;
            }

            GameObject target = null;
            if (_restored != null && _restored.activeInHierarchy && IsInsidePanel(_restored.transform))
            {
                Selectable restoredSelectable = _restored.GetComponent<Selectable>();
                if (restoredSelectable != null && restoredSelectable.interactable)
                {
                    target = _restored;
                }
            }

            if (target == null && _preferred != null && _preferred.gameObject.activeInHierarchy && _preferred.interactable)
            {
                target = _preferred.gameObject;
            }

            EventSystem.current.SetSelectedGameObject(target);
        }

        private GameObject GetValidSelectedObject()
        {
            if (EventSystem.current == null)
            {
                return null;
            }

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || selected.GetComponent<Selectable>() == null)
            {
                return null;
            }

            return selected;
        }

        private bool IsInsidePanel(Transform _node)
        {
            return m_panel != null && _node != null && _node.IsChildOf(m_panel.transform);
        }

        private void OnConfirmActionPerformed(UnityEngine.InputSystem.InputAction.CallbackContext _context)
        {
            // 확인 버튼 입력은 패널이 열려 있고 버튼이 누를 수 있을 때만 큐에 넣는다.
            // 대화·일시정지·선택창 판정은 Presenter가 소비 시점에 검사한다.
            if (IsPanelOpen && IsConfirmInputReady && Time.frameCount > m_openedFrame)
            {
                m_confirmQueued = true;
            }
        }

        private void BindListeners()
        {
            if (m_isListenerBound)
            {
                return;
            }

            m_nextDayButton?.onClick.AddListener(OnNextDayButtonClicked);
            m_confirmButton?.onClick.AddListener(OnConfirmButtonClicked);
            m_reopenButton?.onClick.AddListener(OnReopenButtonClicked);
            m_closeButton?.onClick.AddListener(OnCloseButtonClicked);
            m_isListenerBound = true;
        }

        private void UnbindListeners()
        {
            if (!m_isListenerBound)
            {
                return;
            }

            m_nextDayButton?.onClick.RemoveListener(OnNextDayButtonClicked);
            m_confirmButton?.onClick.RemoveListener(OnConfirmButtonClicked);
            m_reopenButton?.onClick.RemoveListener(OnReopenButtonClicked);
            m_closeButton?.onClick.RemoveListener(OnCloseButtonClicked);
            m_isListenerBound = false;
        }

        private void OnNextDayButtonClicked()
        {
            NextDayRequested?.Invoke();
        }

        private void OnConfirmButtonClicked()
        {
            DeclineRequested?.Invoke();
        }

        private void OnReopenButtonClicked()
        {
            ReopenRequested?.Invoke();
        }

        private void OnCloseButtonClicked()
        {
            CloseRequested?.Invoke();
        }

        private void DisposeConfirmControls()
        {
            m_confirmControls?.Disable();
            m_confirmControls?.Dispose();
            m_confirmControls = null;
            m_confirmQueued = false;
        }

        private void ApplyInitialHiddenState()
        {
            HideDailyRent();
            if (m_slotTicketDisplay != null)
            {
                m_slotTicketDisplay.gameObject.SetActive(false);
            }

            if (m_moneyDisplay != null)
            {
                m_moneyDisplay.gameObject.SetActive(false);
            }

            if (m_moduleCreditDisplay != null)
            {
                m_moduleCreditDisplay.gameObject.SetActive(false);
            }

            SetTodayIncomeVisible(false);
            SetPanelVisible(false);
            if (m_morningNoticeText != null)
            {
                m_morningNoticeText.gameObject.SetActive(false);
            }

            SetReopenVisible(false);
            if (m_questionText != null)
            {
                m_questionText.gameObject.SetActive(false);
            }
        }

        private bool ValidateReferences()
        {
            // 다음 날·다시 열기·닫기·안내 문구는 프리팹에 아직 연결되지 않은 미사용 경로라 필수가 아니다.
            bool isValid = m_panel != null && m_summaryText != null && m_receiptTitle != null &&
                m_confirmButton != null && m_moneyDisplay != null && m_moneyText != null &&
                m_moduleCreditDisplay != null && m_moduleCreditText != null && m_todayIncomeText != null &&
                m_dailyRentText != null && m_slotTicketDisplay != null && m_slotTicketText != null &&
                m_moneyRoll != null && m_moduleCreditRoll != null && m_slotTicketRoll != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    $"[{VIEW_NAME}] 필수 참조 누락: panel={m_panel != null}, summaryText={m_summaryText != null}, " +
                    $"receiptTitle={m_receiptTitle != null}, confirmButton={m_confirmButton != null}, " +
                    $"moneyDisplay={m_moneyDisplay != null}, moneyText={m_moneyText != null}, " +
                    $"moduleCreditDisplay={m_moduleCreditDisplay != null}, moduleCreditText={m_moduleCreditText != null}, " +
                    $"todayIncomeText={m_todayIncomeText != null}, dailyRentText={m_dailyRentText != null}, " +
                    $"slotTicketDisplay={m_slotTicketDisplay != null}, slotTicketText={m_slotTicketText != null}, " +
                    $"moneyRoll={m_moneyRoll != null}, moduleCreditRoll={m_moduleCreditRoll != null}, " +
                    $"slotTicketRoll={m_slotTicketRoll != null}. " +
                    "Tools/UI/Authoring/Settlement/Author 실행 결과와 DaySettlementUI 직렬화 참조를 확인하세요.");
            }

            return isValid;
        }

        private static void SetButtonLabel(Button _button, string _label)
        {
            if (_button == null)
            {
                return;
            }

            TextMeshProUGUI text = _button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.text = _label;
            }
        }

        private IEnumerator HideMorningNoticeAfterDelay()
        {
            // 현재 호출부가 없는 보존 경로다. 아침 안내 문구가 표시 경로를 얻으면 함께 쓴다.
            yield return new WaitForSecondsRealtime(1.5f);
            if (m_morningNoticeText != null)
            {
                m_morningNoticeText.gameObject.SetActive(false);
            }

            m_morningNotice = null;
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
