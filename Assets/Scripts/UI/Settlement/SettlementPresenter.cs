using System;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 정산 View와 기존 정산 도메인(_DayManager·PlayerWallet)을 연결하는 Presenter입니다.
    /// 정산 상태 조회, 다음 날·스킵·닫기·다시 열기 요청, D05 스킵 경로(DayTransitionUI.BeginSkippedNightRaid)를 담당하고,
    /// 지갑·날짜·정산 원본은 도메인이 소유하며 UI에 복제하지 않습니다.
    /// </summary>
    public sealed class SettlementPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "SettlementPresenter";
        private const string SKIP_NIGHT_RAID_MESSAGE = "야간 습격을 하지 않고 다음 날로 넘어가시겠습니까?";

        private readonly SettlementView m_view;
        private readonly _DayManager m_day;
        private readonly PlayerWallet m_wallet;
        private readonly NightRaidController m_nightRaid;
        private readonly Component m_owner;
        private readonly Func<FeedbackUI> m_feedbackProvider;

        private GameObject m_savedFocus;
        private bool m_wasPanelShown;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public bool IsInitialized
        {
            get
            {
                return m_isInitialized;
            }
        }

        public SettlementPresenter(SettlementView _view, _DayManager _day, PlayerWallet _wallet,
            NightRaidController _nightRaid, Component _owner, Func<FeedbackUI> _feedbackProvider)
        {
            m_view = _view;
            m_day = _day;
            m_wallet = _wallet;
            m_nightRaid = _nightRaid;
            m_owner = _owner;
            m_feedbackProvider = _feedbackProvider;
        }

        /// <summary>
        /// View를 초기화하고 화면 입력 이벤트를 구독한다. 중복 호출에 안전하다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError($"[{PRESENTER_NAME}] Initialize 실패: SettlementView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.ConfirmInputPerformed += OnConfirmInputPerformed;
            m_view.NextDayRequested += RequestNextDay;
            m_view.DeclineRequested += DeclineNextDay;
            m_view.ReopenRequested += ReopenSettlement;
            m_view.CloseRequested += DeclineNextDay;
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 화면 이벤트 구독을 해제한다. 도메인 구독은 Enable 수명이므로 여기서 풀지 않는다. 중복 호출에 안전하다.
        /// </summary>
        public void Dispose()
        {
            if (!m_isInitialized)
            {
                return;
            }

            m_view.ConfirmInputPerformed -= OnConfirmInputPerformed;
            m_view.NextDayRequested -= RequestNextDay;
            m_view.DeclineRequested -= DeclineNextDay;
            m_view.ReopenRequested -= ReopenSettlement;
            m_view.CloseRequested -= DeclineNextDay;
            m_isInitialized = false;
        }

        /// <summary>
        /// 도메인 상태 구독을 시작하고 현재 상태를 즉시 반영한다. 브리지 OnEnable 수명이다.
        /// </summary>
        public void OnEnabled()
        {
            if (!m_isInitialized)
            {
                return;
            }

            m_view.EnableConfirmInput();
            if (m_day != null)
            {
                m_day.Changed += Refresh;
            }

            if (m_wallet != null)
            {
                m_wallet.Changed += Refresh;
                m_wallet.PurchaseFundsInsufficient += ShowInsufficientFunds;
                m_wallet.ModuleCreditsLost += ShowModuleCreditLoss;
            }

            Refresh();
        }

        /// <summary>
        /// 도메인 상태 구독을 끝내고 화면을 숨김 상태로 되돌린다. 브리지 OnDisable 수명이다.
        /// </summary>
        public void OnDisabled()
        {
            if (!m_isInitialized)
            {
                return;
            }

            if (m_day != null)
            {
                m_day.Changed -= Refresh;
            }

            if (m_wallet != null)
            {
                m_wallet.Changed -= Refresh;
                m_wallet.PurchaseFundsInsufficient -= ShowInsufficientFunds;
                m_wallet.ModuleCreditsLost -= ShowModuleCreditLoss;
            }

            m_view.ApplyDisabledState();
            m_savedFocus = null;
            m_wasPanelShown = false;
        }

        private void Refresh()
        {
            bool show = m_day != null && m_day.IsNight && m_day.IsSettlementOpen;
            if (!show)
            {
                CancelFeedbackChoice();
            }

            m_view.SetBalanceDisplaysVisible(m_wallet != null);
            if (m_wallet != null)
            {
                m_view.SetSlotTickets(m_wallet.SlotTickets);
            }

            bool hasDailyRent = m_wallet != null && m_day != null && m_day.CurrentCustomerDay != null;
            if (hasDailyRent)
            {
                bool settledToday = m_day.IsNight && m_day.IsSettlementOpen;
                int rent = settledToday ? m_day.TodayRent : m_day.NextSettlementRent;
                int loans = m_day.SettlementCostsWaived ? 0 : m_wallet.OutstandingLoans;
                long principal = settledToday ? m_day.TodayLoanRepayment : (long)loans * PlayerWallet.LoanAmount;
                long interest = settledToday ? m_day.TodayLoanInterest : (long)rent * loans / 5;
                m_view.ShowDailyRent(rent, principal, interest);
            }
            else
            {
                m_view.HideDailyRent();
            }

            if (m_wallet != null)
            {
                m_view.SetModuleCredits(m_wallet.ModuleCredits);
                long due = m_day != null && !m_day.SettlementCostsWaived && !m_day.IsNight
                    ? (long)m_day.NextSettlementRent * (5L + m_wallet.OutstandingLoans) / 5
                        + (long)m_wallet.OutstandingLoans * PlayerWallet.LoanAmount : 0;
                bool warnInsufficientFunds = (m_day == null || !m_day.SettlementCostsWaived) &&
                    (long)m_wallet.Balance - due <= 0;
                m_view.SetMoneyBalance(m_wallet.Balance, warnInsufficientFunds);
            }

            m_view.SetTodayIncomeVisible(show);
            if (show)
            {
                m_view.SetTodayIncome(ComputeNetIncome(), m_wallet != null ? m_wallet.Balance : 0);
                m_view.SetReceiptTitle(m_day.DayNumber == 0 ? "튜토리얼 - 영업 정산" : $"DAY {m_day.DayNumber:00} - 영업 정산");
                m_view.SetSummary(CreateSummaryData());
            }

            m_view.SetNextDayState(m_day == null || !m_day.IsBankrupt, show && m_day != null && m_day.CanStartNextDay);
            m_view.SetConfirmInteractable(show);
            m_view.SetReopenVisible(m_day != null && m_day.IsNight && !show &&
                (m_nightRaid == null || !m_nightRaid.IsBusy));
            if (!show)
            {
                m_view.HideMorningNotice();
            }

            bool wasShown = m_wasPanelShown;
            if (wasShown && !show)
            {
                m_savedFocus = m_view.CapturePanelFocus();
            }

            m_view.SetPanelVisible(show);
            m_wasPanelShown = show;
            if (show && !wasShown)
            {
                m_view.ApplyPanelFocus(ResolveDefaultSelection(), m_savedFocus);
            }
        }

        private long ComputeNetIncome()
        {
            return (long)m_day.TodayIncome + m_day.TodayLoanIncome - m_day.TodayRent - m_day.TodaySoldConstructionCost
                - m_day.TodayLoanRepayment - m_day.TodayLoanInterest;
        }

        private SettlementView.SettlementSummaryData CreateSummaryData()
        {
            return new SettlementView.SettlementSummaryData
            {
                CompletedGuests = m_day.CompletedGuests,
                DissatisfiedGuests = m_day.DissatisfiedGuests,
                KilledGuests = m_day.KilledGuests,
                TodayIncome = m_day.TodayIncome,
                TodayLoanIncome = m_day.TodayLoanIncome,
                SoldConstructionCost = m_day.TodaySoldConstructionCost,
                TodayRent = m_day.TodayRent,
                TodayLoanRepayment = m_day.TodayLoanRepayment,
                TodayLoanInterest = m_day.TodayLoanInterest,
                IsBankrupt = m_day.IsBankrupt
            };
        }

        /// <summary>
        /// 패널이 열릴 때의 기본 선택. 다음 날 버튼이 실제로 있고 누를 수 있으면 그쪽, 아니면 확인 버튼이다.
        /// </summary>
        private Selectable ResolveDefaultSelection()
        {
            Selectable nextDay = m_view.NextDaySelectable;
            if (nextDay != null && nextDay.gameObject.activeSelf && nextDay.interactable)
            {
                return nextDay;
            }

            return m_view.ConfirmSelectable;
        }

        // Workshop/Interact 확인 입력. 버튼 클릭과 같은 닫기 처리이되 대화·일시정지·선택창 상태를 다시 검사한다.
        private void OnConfirmInputPerformed()
        {
            if (!CanConfirmWithInput())
            {
                return;
            }

            DeclineNextDay();
        }

        private bool CanConfirmWithInput()
        {
            return m_view.IsPanelOpen && m_view.IsConfirmInputReady && Time.frameCount > m_view.OpenedFrame &&
                (DialogueUI.Instance == null || !DialogueUI.Instance.IsOpen) &&
                (GameManager.Instance == null || (!GameManager.Instance.IsPaused &&
                    !GameManager.Instance.InDialogue && !(GameManager.Instance.UI.Screen<FeedbackUI>()?.IsChoiceOpen ?? false)));
        }

        private void RequestNextDay()
        {
            if (m_day == null || !m_day.IsSettlementOpen || !m_day.CanStartNextDay)
            {
                return;
            }

            FeedbackUI feedback = m_feedbackProvider != null ? m_feedbackProvider() : null;
            feedback?.ShowChoice(m_owner, SKIP_NIGHT_RAID_MESSAGE, ConfirmSkipNightRaid, null, "예", "아니오");
        }

        // D05: 정산창에서의 스킵도 셔터 경로와 같은 BeginSkippedNightRaid로 진행한다. 부품 100 지급은 도메인이 한 번 처리한다.
        private void ConfirmSkipNightRaid()
        {
            if (m_day == null || !m_view.IsActiveAndEnabled || !m_day.IsSettlementOpen || !m_day.CanStartNextDay)
            {
                return;
            }

            GameManager gameManager = GameManager.Instance;
            UIManager ui = gameManager != null ? gameManager.UI : null;
            DayTransitionUI transition = ui != null ? ui.GetComponent<DayTransitionUI>() : null;
            if (transition == null)
            {
                return;
            }

            if (!transition.BeginSkippedNightRaid(m_day))
            {
                return;
            }

            m_view.HideMorningNotice();
        }

        // 확인(닫기)은 파산이면 기존 게임오버 결과 경로(_DayManager.DeclineNextDay → ShowBankruptcyResult)를 그대로 탄다.
        private void DeclineNextDay()
        {
            m_view.HideMorningNotice();
            if (m_day != null)
            {
                m_day.DeclineNextDay();
            }
        }

        private void ReopenSettlement()
        {
            if (m_day != null)
            {
                m_day.ShowSettlement();
            }
        }

        private void ShowInsufficientFunds(bool _useModuleCredits)
        {
            FeedbackUI feedback = m_feedbackProvider != null ? m_feedbackProvider() : null;
            feedback?.ShowNotification(_useModuleCredits ? "부품이 부족합니다." : "돈이 부족합니다.");
            m_view.PulseInsufficientFunds(_useModuleCredits);
        }

        private void ShowModuleCreditLoss(int _previousAmount)
        {
            if (m_wallet == null)
            {
                return;
            }

            Refresh();
            m_view.RollModuleCreditLoss(_previousAmount, m_wallet.ModuleCredits);
        }

        private void CancelFeedbackChoice()
        {
            FeedbackUI feedback = m_feedbackProvider != null ? m_feedbackProvider() : null;
            feedback?.CancelChoice(m_owner);
        }
    }
}
