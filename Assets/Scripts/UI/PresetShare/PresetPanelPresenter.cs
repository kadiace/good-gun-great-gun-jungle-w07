using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 프리셋 브라우저(6슬롯 관리창)의 저장·불러오기·삭제·이름변경 요청, 튜토리얼·입력 가드,
    /// 지갑 차액 결제·환불과 안내 문구를 담당하는 Presenter입니다.
    /// 표시와 입력 이벤트는 PresetPanelView가 맡고, 저장 데이터 원본은 기존 WeaponPresets입니다.
    /// 상단 열기 버튼으로 6슬롯 관리창을 모달로 열고, 슬롯은 선택만 담당하며
    /// 저장·불러오기(적용)·삭제·이름변경은 명시 버튼으로 실행합니다.
    /// </summary>
    public sealed class PresetPanelPresenter : IDisposable
    {
        private const string PRESET_SAVED_SIGNAL = "preset-saved";

        // 브라우저를 열어 둔 동안 상세·상태 재계산 주기. 프레임마다 Decode/Resolve를 하지 않게 상한으로 쓴다.
        private const float DetailRefreshInterval = 0.3f;
        private const int BlueprintCacheCapacity = 24;

        private sealed class BlueprintEntry
        {
            public WeaponShareCode.Blueprint blueprint;
            public string key;
            public string error;
        }

        private readonly PresetPanelView m_view;
        private readonly UIManager m_uiManager;
        private readonly Dictionary<string, BlueprintEntry> m_blueprintCache =
            new Dictionary<string, BlueprintEntry>();

        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        private int m_selectedSlot = -1;
        private bool m_ownsDialogue;
        private bool m_browserOpen;
        private float m_nextDetailRefresh;
        private WeaponPresets m_boundPresets;
        private readonly WeaponShareCode.Blueprint[] m_appliedSlotPreviews =
            new WeaponShareCode.Blueprint[WeaponPresets.SlotCount];
        private WeaponShareCode.Blueprint m_appliedDetailPreview;

        // 현재 무기 스냅샷(0.3초 주기로 재계산).
        private EquipableWeapon m_currentWeapon;
        private string m_currentName = string.Empty;
        private string m_currentKey;
        private string m_currentError;
        private int m_currentMoney;
        private int m_currentMaterials;
        private int m_currentCoreCost;

        // 같은 표시 갱신 안에서만 Resolve 결과를 재사용한다(가격·해금 변경도 다음 갱신에 반영).
        private bool m_resolvedValid;
        private string m_resolvedCode;
        private string m_resolvedKey;
        private int m_resolvedMoney;
        private int m_resolvedMaterials;
        private string m_resolvedError;

        public PresetPanelPresenter(PresetPanelView _view, UIManager _uiManager)
        {
            m_view = _view;
            m_uiManager = _uiManager;
        }

        /// <summary>
        /// 현재 선택된 프리셋 슬롯 번호(0 기반)를 가져옵니다.
        /// </summary>
        public int SelectedSlot
        {
            get
            {
                return m_selectedSlot;
            }
        }

        private WorkshopSession Session
        {
            get
            {
                return GameManager.Instance != null && GameManager.Instance.Player != null
                    ? GameManager.Instance.Player.GetComponent<WorkshopSession>() : null;
            }
        }

        private WeaponPresets Presets
        {
            get
            {
                return GameManager.Instance != null && GameManager.Instance.Player != null
                    ? GameManager.Instance.Player.GetComponent<WeaponPresets>() : null;
            }
        }

        private bool BrowserEditing
        {
            get
            {
                return m_currentWeapon != null && Session != null && Session.IsEditing &&
                    Session.ActiveWorkbench.CurrentWeapon == m_currentWeapon;
            }
        }

        /// <summary>
        /// View 초기화와 열기·슬롯·저장·불러오기·삭제·이름변경·닫기 이벤트 구독을 연결합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null || m_uiManager == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError(
                        "[PresetPanelPresenter] Initialize 실패: 필수 의존성 누락 " +
                        $"(_view={m_view != null}, _uiManager={m_uiManager != null})");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.OpenRequested -= OnBrowserOpenRequested;
            m_view.OpenRequested += OnBrowserOpenRequested;
            m_view.BrowserSlotSelected -= OnBrowserSlotSelected;
            m_view.BrowserSlotSelected += OnBrowserSlotSelected;
            m_view.BrowserSaveRequested -= OnBrowserSaveRequested;
            m_view.BrowserSaveRequested += OnBrowserSaveRequested;
            m_view.BrowserLoadRequested -= OnBrowserLoadRequested;
            m_view.BrowserLoadRequested += OnBrowserLoadRequested;
            m_view.BrowserDeleteRequested -= OnBrowserDeleteRequested;
            m_view.BrowserDeleteRequested += OnBrowserDeleteRequested;
            m_view.RenameRequested -= OnRenameRequested;
            m_view.RenameRequested += OnRenameRequested;
            m_view.BrowserCloseRequested -= OnBrowserCloseRequested;
            m_view.BrowserCloseRequested += OnBrowserCloseRequested;

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// View 이벤트 구독과 대화 소유, 저장소 이벤트 구독을 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.OpenRequested -= OnBrowserOpenRequested;
                m_view.BrowserSlotSelected -= OnBrowserSlotSelected;
                m_view.BrowserSaveRequested -= OnBrowserSaveRequested;
                m_view.BrowserLoadRequested -= OnBrowserLoadRequested;
                m_view.BrowserDeleteRequested -= OnBrowserDeleteRequested;
                m_view.RenameRequested -= OnRenameRequested;
                m_view.BrowserCloseRequested -= OnBrowserCloseRequested;
                m_view.Dispose();
            }

            CloseBrowser();
            m_isInitialized = false;
        }

        /// <summary>
        /// 매 프레임 패널 표시와 브라우저 상태를 갱신합니다.
        /// 상세·상태 재계산은 0.3초 주기로만 실행해 프레임마다 코드를 해석하지 않습니다.
        /// </summary>
        public void Tick()
        {
            if (!m_isInitialized || m_view == null)
            {
                return;
            }

            WorkshopSession session = Session;
            bool available = session != null && session.IsEditing && session.ActiveWorkbench.CurrentWeapon != null;

            // 편집이 끝나면 패널을 숨기기 전에 브라우저를 먼저 닫아 포커스 복원을 지킨다.
            if (m_browserOpen && !available)
            {
                CloseBrowser();
            }

            m_view.SetPanelVisible(available);
            m_view.SetOpenButtonInteractable(available && !GameManager.ModalBlocked);

            if (!m_browserOpen || !available)
            {
                return;
            }

            EquipableWeapon weapon = session.ActiveWorkbench.CurrentWeapon;
            if (weapon != m_currentWeapon)
            {
                // 무기가 바뀌면 스냅샷과 표시를 즉시 다시 계산한다.
                RebindCurrentWeapon(weapon);
                RefreshCurrentSnapshot();
                RefreshBrowserDisplay();
                m_nextDetailRefresh = Time.unscaledTime + DetailRefreshInterval;
                return;
            }

            if (Time.unscaledTime >= m_nextDetailRefresh)
            {
                RefreshCurrentSnapshot();
                RefreshBrowserDisplay();
                m_nextDetailRefresh = Time.unscaledTime + DetailRefreshInterval;
            }
        }

        /// <summary>
        /// 비활성화 시 브라우저·구 선택창을 닫고 패널을 숨깁니다. 대화 소유도 함께 해제합니다.
        /// </summary>
        public void HandleDisabled()
        {
            CloseBrowser();
            m_view?.HideChoice();
            m_view?.SetPanelVisible(false);
        }

        /// <summary>UICancelRouter 등 기존 호출부의 닫기 요청.</summary>
        public void Close()
        {
            CloseBrowser();
            m_view?.HideChoice();
        }

        private void CloseBrowser()
        {
            UnbindPresetsEvents();
            UnbindCurrentWeapon();
            bool wasOpen = m_browserOpen || (m_view != null && m_view.IsBrowserOpen);
            m_browserOpen = false;
            if (m_uiManager != null) m_uiManager.Screen<FeedbackUI>()?.CancelChoice(this);
            m_selectedSlot = -1;
            if (wasOpen)
            {
                ReleaseFocus();
                // Tick disabled the opener while our modal was active. Make it selectable before restoring focus.
                WorkshopSession session = Session;
                m_view?.SetOpenButtonInteractable(session != null && session.IsEditing &&
                    session.ActiveWorkbench != null && session.ActiveWorkbench.CurrentWeapon != null && !GameManager.ModalBlocked);
            }

            m_view?.SetBrowserVisible(false);
        }

        private void OnBrowserOpenRequested()
        {
            if (m_browserOpen)
            {
                return;
            }

            WorkshopSession session = Session;
            if (session == null || !session.IsEditing || session.ActiveWorkbench.CurrentWeapon == null)
            {
                return;
            }

            // 우리 모달이 아닌 다른 모달(일시정지·야간·대화 등)이 위에 있으면 열지 않는다.
            if (GameManager.ModalBlocked)
            {
                return;
            }

            if (!TutorialManager.ActionAllowed("preset-save") && !TutorialManager.ActionAllowed("preset-load"))
            {
                TutorialManager.TryAction("preset-save");
                return;
            }

            OpenBrowser();
        }

        private void OpenBrowser()
        {
            WorkshopSession session = Session;
            if (session == null || session.ActiveWorkbench.CurrentWeapon == null || Presets == null)
            {
                return;
            }

            m_browserOpen = true;
            m_selectedSlot = -1;
            Array.Clear(m_appliedSlotPreviews, 0, m_appliedSlotPreviews.Length);
            m_appliedDetailPreview = null;
            m_view.SetBrowserVisible(true);
            m_ownsDialogue = GameManager.Instance != null;
            if (m_ownsDialogue)
            {
                GameManager.Instance.SetDialogue(true);
            }

            BindPresetsEvents();
            RebindCurrentWeapon(session.ActiveWorkbench.CurrentWeapon);
            RefreshCurrentSnapshot();
            RefreshBrowserDisplay();
            m_view.SetRenameInputText(string.Empty);
            m_nextDetailRefresh = Time.unscaledTime + DetailRefreshInterval;

            // 튜토리얼 저장·불러오기 단계에서는 열자마자 해당 슬롯을 선택한다(열기만으로 저장하지 않는다).
            TutorialManager tutorial = TutorialManager.Instance;
            TutorialStep step = tutorial != null ? tutorial.CurrentStep : null;
            if (tutorial != null && tutorial.IsTutorialActive && step != null)
            {
                if (tutorial.RaidPresetSaveRequested || step.buttonGuide == TutorialButtonGuide.PresetFirst)
                {
                    SelectBrowserSlot(TutorialManager.RaidPresetSaveSlot);
                }
                else if (step.buttonGuide == TutorialButtonGuide.PresetSecond)
                {
                    SelectBrowserSlot(TutorialManager.RaidPresetLoadSlot);
                }
            }
        }

        private void OnBrowserSlotSelected(int _slot)
        {
            SelectBrowserSlot(_slot);
        }

        private void SelectBrowserSlot(int _slot)
        {
            if (!m_browserOpen || _slot < 0 || _slot >= WeaponPresets.SlotCount)
            {
                return;
            }

            if (BrowserInputBlocked)
            {
                return;
            }

            // 습격 퇴장 저장 안내 중에는 저장 안내 슬롯(5번)만 선택할 수 있다.
            if (TutorialManager.Instance != null && TutorialManager.Instance.RaidPresetSaveRequested && _slot != TutorialManager.RaidPresetSaveSlot)
            {
                return;
            }

            if (!TutorialManager.ActionAllowed("preset-save") && !TutorialManager.ActionAllowed("preset-load"))
            {
                TutorialManager.TryAction("preset-save");
                return;
            }

            if (_slot == m_selectedSlot)
            {
                return;
            }

            m_selectedSlot = _slot;
            BlueprintEntry entry = GetBlueprint(Presets != null ? Presets.GetCode(_slot) : null);
            m_view.SetRenameInputText(entry?.blueprint?.name ?? string.Empty);
            RefreshBrowserDisplay();
        }

        private void OnBrowserSaveRequested()
        {
            if (!m_browserOpen || m_selectedSlot < 0 || Presets == null || !BrowserEditing)
            {
                return;
            }

            if (BrowserInputBlocked)
            {
                return;
            }

            if (TutorialManager.Instance != null && TutorialManager.Instance.RaidPresetSaveRequested && m_selectedSlot != TutorialManager.RaidPresetSaveSlot)
            {
                return;
            }

            if (!TutorialManager.TryAction("preset-save"))
            {
                return;
            }

            int slot = m_selectedSlot;
            try
            {
                Presets.Store(slot, m_currentWeapon);
            }
            catch (Exception exception)
            {
                m_uiManager.Screen<FeedbackUI>()?.ShowNotification(exception.Message);
                return;
            }

            m_uiManager.Screen<FeedbackUI>()?.ShowNotification($"프리셋{slot + 1}에 저장되었습니다.");
            TutorialManager tutorial = TutorialManager.Instance;
            if (tutorial != null)
            {
                tutorial.RecordTutorialPresetSaved(slot);
            }

            CloseBrowser();
            if (tutorial != null)
            {
                tutorial.NotifySignal(PRESET_SAVED_SIGNAL);
            }
        }

        private void OnBrowserLoadRequested()
        {
            if (!m_browserOpen || BrowserInputBlocked)
            {
                return;
            }
            if (m_selectedSlot < 0 || Presets == null || !BrowserEditing)
            {
                ShowApplyFailure("작업대에서 무기와 저장된 프리셋을 선택해 주세요.");
                return;
            }

            BlueprintEntry entry = GetBlueprint(Presets.GetCode(m_selectedSlot));
            if (entry?.blueprint == null)
            {
                ShowApplyFailure(entry?.error ?? "비어 있는 프리셋은 적용할 수 없습니다.");
                return;
            }

            // 실패도 클릭으로 설명한다. 실제 적용 직전의 재화·가격·잠금을 다시 확인한다.
            RefreshCurrentSnapshot();
            if (!TryEvaluateLoad(entry, out int moneyDelta, out int materialsDelta, out string reason))
            {
                ShowApplyFailure(reason ?? "프리셋의 적용 비용을 계산할 수 없습니다.");
                return;
            }

            if (!TutorialManager.TryAction("preset-load"))
            {
                return;
            }

            if (reason != null)
            {
                ShowApplyFailure(reason);
                TryOfferPresetLoan(Mathf.Max(0, moneyDelta), Mathf.Max(0, materialsDelta));
                return;
            }

            try
            {
                var assembly = m_currentWeapon.GetComponentInChildren<CubeAssembly>();
                if (!assembly.TryCaptureLayout(out var old))
                {
                    throw new ArgumentException("현재 조립을 읽을 수 없습니다.");
                }

                int oldMoney = old.blocks.Sum(block => block.paidPurchaseCost);
                int oldMaterials = old.blocks.Sum(block => block.paidModuleCreditCost);
                FirstPersonPlayer player = GameManager.Instance != null ? GameManager.Instance.Player : null;
                // 적용 시점에 실제 구성·비용을 다시 계산한다(표시용 캐시와 무관하게 Resolve).
                var plan = WeaponShareCode.Resolve(entry.blueprint, assembly, player.GetComponent<PlayerModuleUnlocks>(),
                    Mathf.Max(0, old.paidConstructionCost - oldMoney), out int money, out int materials);
                var wallet = player.GetComponent<PlayerWallet>();
                if (!wallet.TryPurchaseModule(Mathf.Max(0, money - oldMoney), Mathf.Max(0, materials - oldMaterials),
                    () => assembly.RestoreLayout(plan)))
                {
                    ShowApplyFailure(FundsReason(money - oldMoney, materials - oldMaterials) ??
                        "무기 구성을 교체할 수 없습니다. 작업대 편집 상태와 커스텀 잠금을 확인하세요.");
                    return;
                }

                wallet.RefundConstruction(Mathf.Max(0, oldMoney - money));
                wallet.CreditModuleCredits(Mathf.Max(0, oldMaterials - materials));
                m_currentWeapon.SetWeaponName(entry.blueprint.name);
                m_currentWeapon.GetComponentInChildren<StatCollector>()?.Recalculate();
                m_uiManager.Screen<WeaponNameUI>()?.RefreshName();
                m_uiManager.Screen<FeedbackUI>()?.ShowNotification($"프리셋{m_selectedSlot + 1}을 불러왔습니다.");
                int loadedSlot = m_selectedSlot;
                TutorialManager tutorial = TutorialManager.Instance;
                CloseBrowser();
                if (loadedSlot == TutorialManager.RaidPresetLoadSlot && tutorial != null && tutorial.CurrentStep != null &&
                    tutorial.CurrentStep.id == "raid-load-preset-two")
                {
                    tutorial.NotifySignal("preset-two-loaded");
                }
            }
            catch (Exception exception)
            {
                ShowApplyFailure(exception.Message);
            }
        }

        private void ShowApplyFailure(string message)
        {
            RefreshBrowserDisplay();
            m_view.SetApplyFailure(message);
            m_uiManager.Screen<FeedbackUI>()?.ShowNotification(message);
        }

        private void TryOfferPresetLoan(int dollars, int materials)
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            var wallet = player != null ? player.GetComponent<PlayerWallet>() : null;
            // Match module placement: loans cover dollars only, and never purchase/apply automatically.
            if (wallet == null || !wallet.isActiveAndEnabled || wallet.Balance >= dollars ||
                wallet.ModuleCredits < materials) return;

            var day = UnityEngine.Object.FindFirstObjectByType<_DayManager>();
            if (day == null || !wallet.CanTakeLoan(day.DayNumber))
            {
                ShowApplyFailure(day != null && wallet.LastLoanDay == day.DayNumber
                    ? "대출은 하루에 한 번만 받을 수 있습니다."
                    : "지금은 대출을 받을 수 없습니다.");
                return;
            }

            var feedback = m_uiManager.Screen<FeedbackUI>();
            int requestedDay = day.DayNumber;
            int interest = (int)((long)day.NextSettlementRent * (wallet.OutstandingLoans + 1) / 5);
            bool opened = feedback != null && feedback.ShowChoice(this,
                $"프리셋 적용에 필요한 돈이 부족합니다.\n${PlayerWallet.LoanAmount:N0} 대출을 받으시겠습니까? 예상 이자 ${interest:N0}\n대출 후 프리셋 적용 버튼을 다시 눌러 주세요.",
                () =>
                {
                    if (!m_browserOpen || !BrowserEditing || day == null || day.DayNumber != requestedDay) return;
                    if (wallet != null && wallet.TryTakeLoan(requestedDay))
                    {
                        var tutorial = TutorialManager.Instance;
                        tutorial?.NotifySignal("loan-taken", tutorial.TutorialWorkbench);
                        RefreshCurrentSnapshot();
                        RestoreBrowserFocus();
                        feedback.ShowNotification($"${PlayerWallet.LoanAmount:N0} 대출을 받았습니다. 프리셋 적용 버튼을 다시 눌러 주세요.");
                    }
                    else
                    {
                        RestoreBrowserFocus();
                        ShowApplyFailure("대출을 받을 수 없습니다. 오늘 대출 여부를 확인해 주세요.");
                    }
                }, RestoreBrowserFocus, "대출받기", "취소");
            if (!opened)
            {
                ShowApplyFailure("대출 안내를 표시할 수 없습니다. 열려 있는 확인창을 먼저 닫아 주세요.");
                return;
            }

            var guide = TutorialManager.Instance;
            if (guide != null && guide.CurrentStep?.requiredSignal == "loan-taken") guide.ShowLoanGuide(feedback);
        }

        private void OnBrowserDeleteRequested()
        {
            if (!m_browserOpen || m_selectedSlot < 0 || Presets == null)
            {
                return;
            }

            if (BrowserInputBlocked)
            {
                return;
            }

            WeaponPresets store = Presets;
            int slot = m_selectedSlot;
            string code = store.GetCode(slot);
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            if (!TutorialManager.TryAction("preset-save"))
            {
                return;
            }

            FeedbackUI feedback = m_uiManager.Screen<FeedbackUI>();
            if (feedback == null) return;
            // 슬롯과 코드를 고정하고 수락 시 재확인한다. 거절·ESC·화면 종료는 저장소를 변경하지 않는다.
            bool opened = feedback.ShowChoice(this,
                $"프리셋 {slot + 1}을 삭제하시겠습니까?\n저장된 설계만 삭제되며 현재 무기는 유지됩니다.",
                () =>
                {
                    RestoreBrowserFocus();
                    if (!m_browserOpen || !BrowserEditing || BrowserInputBlocked) return;
                    if (Presets != store || m_selectedSlot != slot || store.GetCode(slot) != code)
                    {
                        feedback.ShowNotification("선택한 프리셋이 변경됐습니다. 다시 삭제해 주세요.");
                        return;
                    }
                    if (!TutorialManager.TryAction("preset-save")) return;
                    store.Clear(slot);
                    feedback.ShowNotification($"프리셋{slot + 1}을 삭제했습니다.");
                    m_view.SetRenameInputText(string.Empty);
                    RefreshBrowserDisplay();
                }, RestoreBrowserFocus, "수락", "거절");
            if (!opened) feedback.ShowNotification("열려 있는 확인창을 먼저 닫아 주세요.");
        }

        private void RestoreBrowserFocus()
        {
            if (!m_browserOpen) return;
            RefreshBrowserDisplay();
            m_view.FocusBrowserSlot(m_selectedSlot);
        }

        private void OnRenameRequested()
        {
            if (!m_browserOpen || m_selectedSlot < 0 || Presets == null || !BrowserEditing)
            {
                return;
            }

            if (BrowserInputBlocked)
            {
                return;
            }

            BlueprintEntry entry = GetBlueprint(Presets.GetCode(m_selectedSlot));
            if (entry?.blueprint == null)
            {
                return;
            }

            if (!TutorialManager.TryAction("rename"))
            {
                return;
            }

            string newName = (m_view.GetRenameInputText() ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(newName))
            {
                m_uiManager.Screen<FeedbackUI>()?.ShowNotification("프리셋 이름을 입력해 주세요.");
                return;
            }

            if (newName.Length > 40)
            {
                m_uiManager.Screen<FeedbackUI>()?.ShowNotification("프리셋 이름이 너무 깁니다(최대 40자).");
                return;
            }

            int slot = m_selectedSlot;
            try
            {
                // 캐시된 블루프린트는 공유 중이므로 블록을 재활용한 새 블루프린트로 이름만 바꿔 저장한다.
                var renamed = new WeaponShareCode.Blueprint
                {
                    version = entry.blueprint.version, name = newName, blocks = entry.blueprint.blocks
                };
                Presets.StoreCode(slot, WeaponShareCode.Encode(renamed));
            }
            catch (Exception exception)
            {
                m_uiManager.Screen<FeedbackUI>()?.ShowNotification(exception.Message);
                return;
            }

            m_uiManager.Screen<FeedbackUI>()?.ShowNotification($"프리셋{slot + 1}의 이름을 바꿨습니다.");
            m_view.SetRenameInputText(newName);
            RefreshBrowserDisplay();
            m_nextDetailRefresh = Time.unscaledTime + DetailRefreshInterval;
        }

        private void OnBrowserCloseRequested()
        {
            CloseBrowser();
        }

        private void OnPresetStored(int _slot, string _code)
        {
            if (!m_browserOpen)
            {
                return;
            }

            // 저장·삭제·이름변경은 저장소 이벤트로 즉시 반영한다(주기 재계산을 기다리지 않는다).
            RefreshBrowserDisplay();
        }

        /// <summary>
        /// 브라우저 내부 입력 가능 여부. 우리가 소유한 모달 안에서는 슬롯 선택을 계속 허용하고,
        /// 일시정지처럼 우리 밖의 차단 상태나 남의 모달이 위에 있으면 막는다.
        /// </summary>
        private bool BrowserInputBlocked
        {
            get
            {
                if (UIInputPolicy.IsPaused || UIInputPolicy.IsChoiceOpen || UIInputPolicy.IsOverlayBlocked)
                {
                    return true;
                }

                return !m_ownsDialogue && GameManager.GameplayBlocked;
            }
        }

        // ── 브라우저 표시 갱신 ──────────────────────────────────────────

        private void RebindCurrentWeapon(EquipableWeapon _weapon)
        {
            if (ReferenceEquals(m_currentWeapon, _weapon))
            {
                return;
            }

            m_currentWeapon = _weapon;
            InvalidateCurrent();
        }

        private void InvalidateCurrent()
        {
            m_currentKey = null;
            m_currentError = null;
            m_currentName = m_currentWeapon != null ? m_currentWeapon.WeaponName : string.Empty;
            m_currentMoney = 0;
            m_currentMaterials = 0;
            m_currentCoreCost = 0;
            InvalidateResolve();
        }

        /// <summary>현재 무기 구성 스냅샷(조립 키·지불 비용)을 다시 계산합니다.</summary>
        private void RefreshCurrentSnapshot()
        {
            InvalidateResolve();
            if (m_currentWeapon == null)
            {
                InvalidateCurrent();
                return;
            }

            m_currentName = m_currentWeapon.WeaponName;
            var assembly = m_currentWeapon.GetComponentInChildren<CubeAssembly>();
            if (assembly == null || !assembly.TryCaptureLayout(out var layout))
            {
                m_currentKey = null;
                m_currentError = "현재 조립을 읽을 수 없습니다.";
                InvalidateResolve();
                return;
            }

            int money = 0, materials = 0;
            foreach (var block in layout.blocks)
            {
                money += block.paidPurchaseCost;
                materials += block.paidModuleCreditCost;
            }

            m_currentMoney = money;
            m_currentMaterials = materials;
            m_currentCoreCost = Mathf.Max(0, layout.paidConstructionCost - money);
            string key = ComputeSnapshotKey(assembly, layout.blocks);
            if (key == null)
            {
                m_currentKey = null;
                m_currentError = "공유할 수 없는 모듈이 포함된 구성입니다.";
            }
            else
            {
                m_currentError = null;
                if (key != m_currentKey)
                {
                    m_currentKey = key;
                    InvalidateResolve();
                }
            }
        }

        private void RefreshBrowserDisplay()
        {
            RefreshSlotRows();
            RefreshDetail();
            m_view.SetBrowserActions(SaveInteractable(), LoadInteractable(), DeleteInteractable(), RenameInteractable());
        }

        private void RefreshSlotRows()
        {
            for (int i = 0; i < WeaponPresets.SlotCount; i++)
            {
                string code = Presets != null ? Presets.GetCode(i) : null;
                BlueprintEntry entry = GetBlueprint(code);
                WeaponShareCode.Blueprint blueprint = entry?.blueprint;

                string name;
                if (blueprint != null)
                {
                    name = blueprint.name;
                }
                else if (entry?.error != null)
                {
                    name = "손상된 코드";
                }
                else
                {
                    name = "(비어 있음)";
                }

                string state;
                if (blueprint == null)
                {
                    state = entry?.error != null ? "손상됨" : "비어 있음";
                }
                else if (m_currentKey == null)
                {
                    state = "저장됨";
                }
                else
                {
                    state = entry.key == m_currentKey ? "현재와 동일" : "현재와 다름";
                }

                m_view.SetSlotRow(i, name, state, i == m_selectedSlot, SlotInteractable(i));

                if (!ReferenceEquals(m_appliedSlotPreviews[i], blueprint))
                {
                    m_appliedSlotPreviews[i] = blueprint;
                    m_view.SetSlotPreview(i, blueprint);
                }
            }
        }

        private void RefreshDetail()
        {
            if (m_selectedSlot < 0 || Presets == null)
            {
                ApplyDetailPreview(null);
                m_view.SetDetailTexts("프리셋 슬롯을 선택하세요",
                    "6칸 저장소", "—", "슬롯을 선택하면 저장·불러오기 정보가 표시됩니다.");
                return;
            }

            int slot = m_selectedSlot;
            BlueprintEntry entry = GetBlueprint(Presets.GetCode(slot));
            if (entry?.blueprint == null)
            {
                ApplyDetailPreview(null);
                if (entry?.error != null)
                {
                    m_view.SetDetailTexts($"프리셋 {slot + 1} · 손상된 코드",
                        $"현재 '{m_currentName}'", "—", entry.error);
                }
                else
                {
                    m_view.SetDetailTexts($"프리셋 {slot + 1} · 빈 슬롯",
                        $"현재 '{m_currentName}'", "저장: 비용 없음",
                        "저장을 누르면 현재 무기가 이 슬롯에 저장됩니다.");
                }

                return;
            }

            // 같은 구성이라도 현재 가격 기준으로 다시 계산해 차액과 사유를 함께 보여 준다.
            bool hasDelta = TryEvaluateLoad(entry, out int moneyDelta, out int materialsDelta, out string reason);
            bool same = entry.key != null && entry.key == m_currentKey;
            string title = $"프리셋 {slot + 1} · {entry.blueprint.name}";
            string status = m_currentKey == null
                ? "저장됨"
                : same ? $"현재 '{m_currentName}'와 동일" : $"현재 '{m_currentName}'와 다름(수정됨)";
            string cost = hasDelta ? FormatCostDelta(moneyDelta, materialsDelta) : "—";
            string note;
            if (reason != null)
            {
                note = reason;
            }
            else
            {
                note = "불러오기는 현재 조립을 교체하고 제작비 차액을 결제·환불합니다.";
            }

            ApplyDetailPreview(entry.blueprint);
            m_view.SetDetailTexts(title, status, cost, note);
            m_view.SetApplyBlocked(reason != null);
        }

        private void ApplyDetailPreview(WeaponShareCode.Blueprint _blueprint)
        {
            if (ReferenceEquals(m_appliedDetailPreview, _blueprint))
            {
                return;
            }

            m_appliedDetailPreview = _blueprint;
            m_view.SetDetailPreview(_blueprint);
        }

        private bool SlotInteractable(int _slot)
        {
            if (!m_browserOpen || BrowserInputBlocked)
            {
                return false;
            }

            if (TutorialManager.Instance != null && TutorialManager.Instance.RaidPresetSaveRequested && _slot != TutorialManager.RaidPresetSaveSlot)
            {
                return false;
            }

            return true;
        }

        private bool SaveInteractable()
        {
            return m_browserOpen && m_selectedSlot >= 0 && BrowserEditing && !BrowserInputBlocked &&
                TutorialManager.ActionAllowed("preset-save") &&
                !(TutorialManager.Instance != null && TutorialManager.Instance.RaidPresetSaveRequested &&
                    m_selectedSlot != TutorialManager.RaidPresetSaveSlot);
        }

        private bool LoadInteractable()
        {
            if (!m_browserOpen || m_selectedSlot < 0 || Presets == null || !BrowserEditing || BrowserInputBlocked)
            {
                return false;
            }

            BlueprintEntry entry = GetBlueprint(Presets.GetCode(m_selectedSlot));
            return entry?.blueprint != null;
        }

        private bool DeleteInteractable()
        {
            return m_browserOpen && m_selectedSlot >= 0 && Presets != null && !BrowserInputBlocked &&
                TutorialManager.ActionAllowed("preset-save") &&
                !string.IsNullOrEmpty(Presets.GetCode(m_selectedSlot));
        }

        private bool RenameInteractable()
        {
            if (!m_browserOpen || m_selectedSlot < 0 || Presets == null || !BrowserEditing || BrowserInputBlocked ||
                !TutorialManager.ActionAllowed("rename"))
            {
                return false;
            }

            BlueprintEntry entry = GetBlueprint(Presets.GetCode(m_selectedSlot));
            return entry?.blueprint != null;
        }

        /// <summary>
        /// 불러오기(적용) 차액과 사유를 구합니다. 같은 구성이라도 현재 가격 기준 Resolve로 계산하며,
        /// 차액 계산에 성공하면 true를 돌려주고 사유는 자금 부족 여부(없으면 null)를 담습니다.
        /// 실제 적용 시점에는 캐시 없이 다시 계산합니다.
        /// </summary>
        private bool TryEvaluateLoad(BlueprintEntry _entry, out int _moneyDelta, out int _materialsDelta, out string _reason)
        {
            _moneyDelta = 0;
            _materialsDelta = 0;
            if (m_currentWeapon == null)
            {
                _reason = "현재 무기를 확인할 수 없습니다.";
                return false;
            }

            if (m_currentWeapon.ConstructionLocked)
            {
                _reason = "커스텀을 제거한 후 프리셋을 불러와 주세요.";
                return false;
            }

            if (_entry.error != null)
            {
                _reason = _entry.error;
                return false;
            }

            if (m_currentKey == null)
            {
                _reason = m_currentError ?? "현재 조립을 읽을 수 없습니다.";
                return false;
            }

            if (!m_resolvedValid || m_resolvedCode != GetSelectedCode() || m_resolvedKey != m_currentKey)
            {
                m_resolvedCode = GetSelectedCode();
                m_resolvedKey = m_currentKey;
                m_resolvedError = null;
                try
                {
                    var assembly = m_currentWeapon.GetComponentInChildren<CubeAssembly>();
                    FirstPersonPlayer player = GameManager.Instance != null ? GameManager.Instance.Player : null;
                    if (assembly == null || player == null)
                    {
                        throw new ArgumentException("현재 조립을 읽을 수 없습니다.");
                    }

                    // 표시용 차액 계산. 실제 적용(OnBrowserLoadRequested) 시점에는 캐시 없이 다시 계산한다.
                    WeaponShareCode.Resolve(_entry.blueprint, assembly,
                        player.GetComponent<PlayerModuleUnlocks>(), m_currentCoreCost,
                        out int money, out int materials);
                    m_resolvedMoney = money;
                    m_resolvedMaterials = materials;
                }
                catch (Exception exception)
                {
                    m_resolvedError = exception.Message;
                }

                m_resolvedValid = true;
            }

            if (m_resolvedError != null)
            {
                _reason = m_resolvedError;
                return false;
            }

            _moneyDelta = m_resolvedMoney - m_currentMoney;
            _materialsDelta = m_resolvedMaterials - m_currentMaterials;
            _reason = FundsReason(_moneyDelta, _materialsDelta);
            return true;
        }

        private string GetSelectedCode()
        {
            return m_selectedSlot >= 0 && Presets != null ? Presets.GetCode(m_selectedSlot) : null;
        }

        private static string FundsReason(int _moneyDelta, int _materialsDelta)
        {
            PlayerWallet wallet = GameManager.Instance != null && GameManager.Instance.Player != null
                ? GameManager.Instance.Player.GetComponent<PlayerWallet>() : null;
            if (wallet == null)
            {
                return "지갑 정보를 확인할 수 없습니다.";
            }

            bool moneyNeeded = Mathf.Max(0, _moneyDelta) > wallet.Balance;
            bool materialsNeeded = Mathf.Max(0, _materialsDelta) > wallet.ModuleCredits;
            if (!wallet.isActiveAndEnabled) return "지갑을 사용할 수 없습니다.";
            if (!moneyNeeded && !materialsNeeded) return null;
            string dollars = moneyNeeded ? $"달러 ${Mathf.Max(0, _moneyDelta) - wallet.Balance:N0} 부족" : null;
            string materials = materialsNeeded ? $"재료 {Mathf.Max(0, _materialsDelta) - wallet.ModuleCredits:N0}개 부족" : null;
            return "비용 부족: " + (moneyNeeded && materialsNeeded ? dollars + "\n" + materials : dollars ?? materials);
        }

        private static string FormatCostDelta(int _moneyDelta, int _materialsDelta)
        {
            string money = _moneyDelta == 0 ? "제작비 동일"
                : _moneyDelta > 0 ? $"추가 결제 ${_moneyDelta:N0}" : $"환불 ${-_moneyDelta:N0}";
            string materials = _materialsDelta == 0 ? "재료 동일"
                : _materialsDelta > 0 ? $"재료 {_materialsDelta:N0} 소요" : $"재료 {-_materialsDelta:N0} 환불";
            return $"{money}\n{materials}";
        }

        // ── 코드 해석 캐시 ───────────────────────────────────────────────

        private BlueprintEntry GetBlueprint(string _code)
        {
            if (string.IsNullOrEmpty(_code))
            {
                return null;
            }

            if (m_blueprintCache.TryGetValue(_code, out BlueprintEntry entry))
            {
                return entry;
            }

            entry = new BlueprintEntry();
            try
            {
                entry.blueprint = WeaponShareCode.Decode(_code);
                entry.key = ComputeBlueprintKey(entry.blueprint);
            }
            catch (Exception exception)
            {
                entry.error = exception.Message;
            }

            if (m_blueprintCache.Count >= BlueprintCacheCapacity)
            {
                m_blueprintCache.Clear();
            }

            m_blueprintCache[_code] = entry;
            return entry;
        }

        private void InvalidateResolve()
        {
            m_resolvedValid = false;
            m_resolvedCode = null;
            m_resolvedKey = null;
            m_resolvedError = null;
            m_resolvedMoney = 0;
            m_resolvedMaterials = 0;
        }

        // 저장되는 모듈 ID·칸·방향·색상으로 저장본과 현재 구성을 비교한다.
        private static string ComputeBlueprintKey(WeaponShareCode.Blueprint _blueprint)
        {
            if (_blueprint?.blocks == null)
            {
                return null;
            }

            return ComputeKey(_blueprint.blocks.Select(block =>
                (block.module, block.cell, block.output, block.normal, block.hasColor, block.color)));
        }

        private static string ComputeSnapshotKey(CubeAssembly _assembly, CubeAssembly.BlockSnapshot[] _blocks)
        {
            if (_assembly == null || _blocks == null)
            {
                return null;
            }

            var keyed = new (string module, Vector3Int cell, Vector3Int output, Vector3Int normal, bool hasColor, Color color)[_blocks.Length];
            for (int i = 0; i < _blocks.Length; i++)
            {
                var data = _assembly.GetModuleData(_blocks[i].slot);
                if (data == null || string.IsNullOrEmpty(data.CodeId))
                {
                    return null;
                }

                keyed[i] = (data.CodeId, _blocks[i].cell, _blocks[i].outputDirection, _blocks[i].orientationNormal,
                    _blocks[i].hasColor, _blocks[i].color);
            }

            return ComputeKey(keyed);
        }

        private static string ComputeKey(
            IEnumerable<(string module, Vector3Int cell, Vector3Int output, Vector3Int normal, bool hasColor, Color color)> _blocks)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(1);
                    foreach (var block in _blocks.OrderBy(b => b.cell.x).ThenBy(b => b.cell.y).ThenBy(b => b.cell.z))
                    {
                        writer.Write(block.module);
                        foreach (var vector in new[] { block.cell, block.output, block.normal })
                        {
                            writer.Write(vector.x);
                            writer.Write(vector.y);
                            writer.Write(vector.z);
                        }
                        writer.Write(block.hasColor);
                        if (block.hasColor)
                        {
                            writer.Write(block.color.r); writer.Write(block.color.g);
                            writer.Write(block.color.b); writer.Write(block.color.a);
                        }
                    }

                    writer.Flush();
                }

                using (var hash = SHA256.Create())
                {
                    return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
                }
            }
        }

        // ── 이벤트 구독 관리 ─────────────────────────────────────────────

        private void BindPresetsEvents()
        {
            WeaponPresets presets = Presets;
            if (ReferenceEquals(m_boundPresets, presets))
            {
                return;
            }

            if (m_boundPresets != null)
            {
                m_boundPresets.Stored -= OnPresetStored;
            }

            m_boundPresets = presets;
            if (m_boundPresets != null)
            {
                m_boundPresets.Stored += OnPresetStored;
            }
        }

        private void UnbindPresetsEvents()
        {
            if (m_boundPresets != null)
            {
                m_boundPresets.Stored -= OnPresetStored;
                m_boundPresets = null;
            }
        }

        private void UnbindCurrentWeapon()
        {
            m_currentWeapon = null;
            InvalidateCurrent();
        }

        private void ReleaseFocus()
        {
            if (m_ownsDialogue && GameManager.Instance != null)
            {
                GameManager.Instance.SetDialogue(false);
            }

            m_ownsDialogue = false;
        }
    }
}
