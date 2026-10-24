using System;
using System.Linq;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 공유 코드 내보내기·복사·가져오기 요청, 튜토리얼·입력 가드, 차액 결제·환불 확인창 흐름을 담당하는 Presenter입니다.
    /// 화면 표시와 버튼 이벤트는 WeaponCodeView가 맡고, 코드 형식 원본은 기존 WeaponShareCode입니다.
    /// </summary>
    public sealed class WeaponCodePresenter : IDisposable
    {
        private const string SHARE_ACTION = "share";
        private const string SHARE_IMPORT_ACTION = "share-import";
        private const string SHARE_INSPECTED_SIGNAL = "share-inspected";

        private readonly WeaponCodeView m_view;
        private readonly UIManager m_uiManager;
        private readonly MonoBehaviour m_owner;

        private EquipableWeapon m_weapon;
        private bool m_ownsDialogue;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public WeaponCodePresenter(WeaponCodeView _view, UIManager _uiManager, MonoBehaviour _owner)
        {
            m_view = _view;
            m_uiManager = _uiManager;
            m_owner = _owner;
        }

        private WorkshopSession Session
        {
            get
            {
                return m_uiManager != null && m_uiManager.Player != null
                    ? m_uiManager.Player.GetComponent<WorkshopSession>() : null;
            }
        }

        /// <summary>
        /// View 초기화와 열기·닫기·복사·가져오기 이벤트 구독을 연결합니다.
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
                        "[WeaponCodePresenter] Initialize 실패: 필수 의존성 누락 " +
                        $"(_view={m_view != null}, _uiManager={m_uiManager != null})");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.OpenRequested -= OnOpenRequested;
            m_view.OpenRequested += OnOpenRequested;
            m_view.CloseRequested -= OnCloseRequested;
            m_view.CloseRequested += OnCloseRequested;
            m_view.CopyRequested -= OnCopyRequested;
            m_view.CopyRequested += OnCopyRequested;
            m_view.ImportRequested -= OnImportRequested;
            m_view.ImportRequested += OnImportRequested;

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
                m_view.OpenRequested -= OnOpenRequested;
                m_view.CloseRequested -= OnCloseRequested;
                m_view.CopyRequested -= OnCopyRequested;
                m_view.ImportRequested -= OnImportRequested;
                m_view.Dispose();
            }

            ReleaseFocus();
            m_weapon = null;
            m_isInitialized = false;
        }

        /// <summary>
        /// 매 프레임 열기 버튼 노출과 대상 무기 유효성을 기존 순서대로 갱신합니다.
        /// </summary>
        public void Tick()
        {
            if (!m_isInitialized)
            {
                return;
            }

            WorkshopSession session = Session;
            bool available = session != null && session.IsEditing;
            m_view.SetOpenButtonVisible(available);
            if (m_view.IsOpen && (!available || m_weapon == null || session.ActiveWorkbench.CurrentWeapon != m_weapon))
            {
                Close();
            }
        }

        /// <summary>
        /// 비활성화 시 패널을 닫습니다. 열려 있던 화면은 기존처럼 검사 완료 신호를 보냅니다.
        /// </summary>
        public void HandleDisabled()
        {
            Close();
        }

        public void Open()
        {
            if (!TutorialManager.TryAction(SHARE_ACTION))
            {
                return;
            }

            if (GameManager.GameplayBlocked || Session == null || !Session.IsEditing)
            {
                return;
            }

            m_weapon = Session.ActiveWorkbench.CurrentWeapon;
            if (m_weapon == null)
            {
                return;
            }

            m_view.SetCode("");
            try
            {
                m_view.SetCode(WeaponShareCode.Export(m_weapon));
            }
            catch (Exception exception)
            {
                m_uiManager.ShowNotification(exception.Message);
            }

            m_view.ShowPanel();
            m_ownsDialogue = GameManager.Instance != null;
            if (m_ownsDialogue)
            {
                GameManager.Instance.SetDialogue(true);
            }
        }

        public void Close()
        {
            bool inspected = m_view != null && m_view.IsOpen;
            m_view?.HidePanel();
            m_weapon = null;
            ReleaseFocus();
            if (inspected && TutorialManager.Instance != null)
            {
                TutorialManager.Instance.NotifySignal(SHARE_INSPECTED_SIGNAL);
            }
        }

        public void Copy()
        {
            try
            {
                string code = WeaponShareCode.Export(m_weapon);
                m_view.SetCode(code);
                GUIUtility.systemCopyBuffer = code;
                m_uiManager.ShowNotification("무기 코드를 복사했습니다.");
            }
            catch (Exception exception)
            {
                m_uiManager.ShowNotification(exception.Message);
            }
        }

        public void Import()
        {
            if (!TutorialManager.TryAction(SHARE_IMPORT_ACTION))
            {
                return;
            }

            try
            {
                if (m_weapon == null || m_weapon.ConstructionLocked)
                {
                    throw new ArgumentException("커스텀을 제거한 후 코드를 가져와 주세요.");
                }

                var blueprint = WeaponShareCode.Decode(m_view.Code);
                var assembly = m_weapon.GetComponentInChildren<CubeAssembly>();
                if (!assembly.TryCaptureLayout(out var old))
                {
                    throw new ArgumentException("현재 조립을 읽을 수 없습니다.");
                }

                int oldMoney = old.blocks.Sum(block => block.paidPurchaseCost);
                int oldMaterials = old.blocks.Sum(block => block.paidModuleCreditCost);
                FirstPersonPlayer player = m_uiManager.Player;
                var plan = WeaponShareCode.Resolve(blueprint, assembly, player.GetComponent<PlayerModuleUnlocks>(),
                    Mathf.Max(0, old.paidConstructionCost - oldMoney), out int money, out int materials);
                EquipableWeapon current = m_weapon;
                m_uiManager.Screen<FeedbackUI>()?.ShowChoice(m_owner,
                    $"현재 조립을 '{blueprint.name}'으로 교체할까요?\n" +
                    $"차액: ${money - oldMoney:N0} / 재료 {materials - oldMaterials:N0}\n" +
                    "음수는 기존 모듈 구매비 환불입니다.", () =>
                    {
                        if (current == null || current != m_weapon || current.ConstructionLocked)
                        {
                            return;
                        }

                        var wallet = player.GetComponent<PlayerWallet>();
                        if (wallet.TryPurchaseModule(Mathf.Max(0, money - oldMoney), Mathf.Max(0, materials - oldMaterials),
                            () => assembly.RestoreLayout(plan)))
                        {
                            wallet.RefundConstruction(Mathf.Max(0, oldMoney - money));
                            wallet.CreditModuleCredits(Mathf.Max(0, oldMaterials - materials));
                            current.SetWeaponName(blueprint.name);
                            m_uiManager.GetComponent<WeaponNameUI>()?.RefreshName();
                            current.GetComponentInChildren<StatCollector>()?.Recalculate();
                            m_uiManager.ShowNotification("무기 코드를 적용했습니다.");
                        }
                    }, () => { });
            }
            catch (Exception exception)
            {
                m_uiManager.ShowNotification(exception.Message);
            }
        }

        private void OnOpenRequested()
        {
            Open();
        }

        private void OnCloseRequested()
        {
            Close();
        }

        private void OnCopyRequested()
        {
            Copy();
        }

        private void OnImportRequested()
        {
            Import();
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
