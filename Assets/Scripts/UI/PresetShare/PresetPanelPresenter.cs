using System;
using System.Linq;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 프리셋 저장·불러오기 요청, 튜토리얼·입력 가드, 지갑 차액 결제·환불과 안내 문구를 담당하는 Presenter입니다.
    /// 표시와 입력 이벤트는 PresetPanelView가 맡고, 저장 데이터 원본은 기존 WeaponPresets입니다.
    /// </summary>
    public sealed class PresetPanelPresenter : IDisposable
    {
        private const string PRESET_SAVED_SIGNAL = "preset-saved";

        private readonly PresetPanelView m_view;
        private readonly UIManager m_uiManager;

        private EquipableWeapon m_selectedWeapon;
        private int m_selectedSlot;
        private bool m_ownsDialogue;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

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
                return m_uiManager != null && m_uiManager.Player != null
                    ? m_uiManager.Player.GetComponent<WorkshopSession>() : null;
            }
        }

        private WeaponPresets Presets
        {
            get
            {
                return m_uiManager != null && m_uiManager.Player != null
                    ? m_uiManager.Player.GetComponent<WeaponPresets>() : null;
            }
        }

        private bool StillEditing
        {
            get
            {
                return m_selectedWeapon != null && Session != null && Session.IsEditing &&
                    Session.ActiveWorkbench.CurrentWeapon == m_selectedWeapon;
            }
        }

        /// <summary>
        /// View 초기화와 슬롯·저장·불러오기·닫기 이벤트 구독을 연결합니다.
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

            m_view.SlotSelected -= OnSlotSelected;
            m_view.SlotSelected += OnSlotSelected;
            m_view.SaveRequested -= OnSaveRequested;
            m_view.SaveRequested += OnSaveRequested;
            m_view.LoadRequested -= OnLoadRequested;
            m_view.LoadRequested += OnLoadRequested;
            m_view.CloseRequested -= OnCloseRequested;
            m_view.CloseRequested += OnCloseRequested;

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// View 이벤트 구독과 대화 소유를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.SlotSelected -= OnSlotSelected;
                m_view.SaveRequested -= OnSaveRequested;
                m_view.LoadRequested -= OnLoadRequested;
                m_view.CloseRequested -= OnCloseRequested;
                m_view.Dispose();
            }

            ReleaseFocus();
            m_selectedWeapon = null;
            m_isInitialized = false;
        }

        /// <summary>
        /// 매 프레임 패널 표시·슬롯 상호작용과 선택 대상 유효성을 기존 순서대로 갱신합니다.
        /// </summary>
        public void Tick()
        {
            if (!m_isInitialized)
            {
                return;
            }

            WorkshopSession session = Session;
            bool available = session != null && session.IsEditing && session.ActiveWorkbench.CurrentWeapon != null;
            m_view.SetPanelVisible(available);
            if (m_view.IsOpen && (!available || session.ActiveWorkbench.CurrentWeapon != m_selectedWeapon))
            {
                Close();
            }

            m_view.SetSlotsInteractable(available && !GameManager.ModalBlocked);
        }

        /// <summary>
        /// 비활성화 시 선택창을 닫고 패널을 숨깁니다. 대화 소유도 함께 해제합니다.
        /// </summary>
        public void HandleDisabled()
        {
            Close();
            m_view?.SetPanelVisible(false);
        }

        public void Close()
        {
            m_view?.HideChoice();
            m_selectedWeapon = null;
            ReleaseFocus();
        }

        private void OnSlotSelected(int _slot)
        {
            Select(_slot);
        }

        private void Select(int _slot)
        {
            if (TutorialManager.Instance != null && TutorialManager.Instance.RaidPresetSaveRequested && _slot != 0)
            {
                return;
            }

            if (!TutorialManager.ActionAllowed("preset-save") && !TutorialManager.ActionAllowed("preset-load"))
            {
                TutorialManager.TryAction("preset-save");
                return;
            }

            if (GameManager.GameplayBlocked || Session == null || !Session.IsEditing || Presets == null)
            {
                return;
            }

            m_selectedSlot = _slot;
            m_selectedWeapon = Session.ActiveWorkbench.CurrentWeapon;
            if (m_selectedWeapon == null)
            {
                return;
            }

            bool empty = string.IsNullOrEmpty(Presets.GetCode(_slot));
            if (empty && !(TutorialManager.Instance != null && TutorialManager.Instance.RaidPresetSaveRequested && _slot == 0))
            {
                Save();
                return;
            }

            try
            {
                var name = empty ? "빈 슬롯" : WeaponShareCode.Decode(Presets.GetCode(_slot)).name;
                m_view.ShowChoice(empty
                    ? $"프리셋 {_slot + 1} · {name}\n현재 무기를 저장하세요."
                    : $"프리셋 {_slot + 1} · {name}\n현재 무기를 저장하거나 저장된 무기를 불러오세요.\n" +
                      "불러오기는 현재 조립을 교체하며 제작비 차액을 결제/환불합니다.", !empty);
                m_ownsDialogue = GameManager.Instance != null;
                if (m_ownsDialogue)
                {
                    GameManager.Instance.SetDialogue(true);
                }
            }
            catch (Exception exception)
            {
                m_uiManager.ShowNotification(exception.Message);
                m_selectedWeapon = null;
            }
        }

        private void OnSaveRequested()
        {
            Save();
        }

        private void Save()
        {
            try
            {
                if (!StillEditing || Presets == null)
                {
                    return;
                }

                if (!TutorialManager.TryAction("preset-save"))
                {
                    return;
                }

                Presets.Store(m_selectedSlot, m_selectedWeapon);
                m_uiManager.ShowNotification($"프리셋{m_selectedSlot + 1}에 저장되었습니다.");
                Close();
                if (TutorialManager.Instance != null)
                {
                    TutorialManager.Instance.RecordTutorialPresetSaved(m_selectedSlot);
                    TutorialManager.Instance.NotifySignal(PRESET_SAVED_SIGNAL);
                }
            }
            catch (Exception exception)
            {
                m_uiManager.ShowNotification(exception.Message);
            }
        }

        private void OnLoadRequested()
        {
            Load();
        }

        private void Load()
        {
            if (!TutorialManager.TryAction("preset-load"))
            {
                return;
            }

            try
            {
                if (!StillEditing || Presets == null)
                {
                    return;
                }

                if (m_selectedWeapon.ConstructionLocked)
                {
                    throw new ArgumentException("커스텀을 제거한 후 프리셋을 불러와 주세요.");
                }

                var blueprint = WeaponShareCode.Decode(Presets.GetCode(m_selectedSlot));
                var assembly = m_selectedWeapon.GetComponentInChildren<CubeAssembly>();
                if (!assembly.TryCaptureLayout(out var old))
                {
                    throw new ArgumentException("현재 조립을 읽을 수 없습니다.");
                }

                int oldMoney = old.blocks.Sum(block => block.paidPurchaseCost);
                int oldMaterials = old.blocks.Sum(block => block.paidModuleCreditCost);
                FirstPersonPlayer player = m_uiManager.Player;
                var plan = WeaponShareCode.Resolve(blueprint, assembly, player.GetComponent<PlayerModuleUnlocks>(),
                    Mathf.Max(0, old.paidConstructionCost - oldMoney), out int money, out int materials);
                var wallet = player.GetComponent<PlayerWallet>();
                if (!wallet.TryPurchaseModule(Mathf.Max(0, money - oldMoney), Mathf.Max(0, materials - oldMaterials),
                    () => assembly.RestoreLayout(plan)))
                {
                    return;
                }

                wallet.RefundConstruction(Mathf.Max(0, oldMoney - money));
                wallet.CreditModuleCredits(Mathf.Max(0, oldMaterials - materials));
                m_selectedWeapon.SetWeaponName(blueprint.name);
                m_selectedWeapon.GetComponentInChildren<StatCollector>()?.Recalculate();
                m_uiManager.GetComponent<WeaponNameUI>()?.RefreshName();
                m_uiManager.ShowNotification($"프리셋{m_selectedSlot + 1}을 불러왔습니다.");
                int loadedSlot = m_selectedSlot;
                Close();
                var tutorial = TutorialManager.Instance;
                if (loadedSlot == 1 && tutorial != null && tutorial.CurrentStep != null &&
                    tutorial.CurrentStep.id == "raid-load-preset-two")
                {
                    tutorial.NotifySignal("preset-two-loaded");
                }
            }
            catch (Exception exception)
            {
                m_uiManager.ShowNotification(exception.Message);
            }
        }

        private void OnCloseRequested()
        {
            Close();
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
