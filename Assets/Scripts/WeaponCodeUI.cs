using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    public sealed class WeaponCodeUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button openButton, closeButton, copyButton, importButton;
        [SerializeField] private TMP_InputField codeInput;
        private UIManager ui;
        private EquipableWeapon weapon;
        private bool ownsDialogue;
        public bool IsOpen => panel != null && panel.activeSelf;
        private WorkshopSession Session => ui != null && ui.Player != null ? ui.Player.GetComponent<WorkshopSession>() : null;
        private void Awake()
        {
            ui = GetComponent<UIManager>(); panel.SetActive(false); openButton.gameObject.SetActive(false); ownsDialogue = false;
            openButton.onClick.AddListener(Open); closeButton.onClick.AddListener(Close);
            copyButton.onClick.AddListener(Copy); importButton.onClick.AddListener(Import);
        }
        private void OnDisable() => Close();
        private void LateUpdate()
        {
            var session = Session;
            bool available = session != null && session.IsEditing;
            openButton.gameObject.SetActive(available);
            if (IsOpen && (!available || weapon == null || session.ActiveWorkbench.CurrentWeapon != weapon)) Close();
        }
        private void Open()
        {
            if (!TutorialManager.ActionAllowed("share")) return;
            if (GameManager.GameplayBlocked || Session == null || !Session.IsEditing) return;
            weapon = Session.ActiveWorkbench.CurrentWeapon;
            if (weapon == null) return;
            codeInput.SetTextWithoutNotify("");
            try { codeInput.SetTextWithoutNotify(WeaponShareCode.Export(weapon)); }
            catch (Exception e) { ui.ShowNotification(e.Message); }
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            ownsDialogue = GameManager.Instance != null;
            if (ownsDialogue) GameManager.Instance.SetDialogue(true);
        }
        public void Close()
        {
            bool inspected = IsOpen;
            panel.SetActive(false); weapon = null;
            if (ownsDialogue && GameManager.Instance != null) GameManager.Instance.SetDialogue(false);
            ownsDialogue = false;
            if (inspected && TutorialManager.Instance != null) TutorialManager.Instance.NotifySignal("share-inspected");
        }
        private void Copy()
        {
            try
            {
                string code = WeaponShareCode.Export(weapon);
                codeInput.SetTextWithoutNotify(code); GUIUtility.systemCopyBuffer = code;
                ui.ShowNotification("무기 코드를 복사했습니다.");
            }
            catch (Exception e) { ui.ShowNotification(e.Message); }
        }
        private void Import()
        {
            if (!TutorialManager.ActionAllowed("share-import")) return;
            try
            {
                if (weapon == null || weapon.ConstructionLocked) throw new ArgumentException("커스텀을 제거한 후 코드를 가져와 주세요.");
                var blueprint = WeaponShareCode.Decode(codeInput.text);
                var assembly = weapon.GetComponentInChildren<CubeAssembly>();
                if (!assembly.TryCaptureLayout(out var old)) throw new ArgumentException("현재 조립을 읽을 수 없습니다.");
                int oldMoney = old.blocks.Sum(b => b.paidPurchaseCost), oldMaterials = old.blocks.Sum(b => b.paidModuleCreditCost);
                var plan = WeaponShareCode.Resolve(blueprint, assembly, ui.Player.GetComponent<PlayerModuleUnlocks>(),
                    Mathf.Max(0, old.paidConstructionCost - oldMoney), out int money, out int materials);
                var current = weapon;
                ui.Feedback.ShowChoice(this, $"현재 조립을 '{blueprint.name}'으로 교체할까요?\n차액: ${money - oldMoney:N0} / 재료 {materials - oldMaterials:N0}\n음수는 기존 모듈 구매비 환불입니다.", () =>
                {
                    if (current == null || current != weapon || current.ConstructionLocked) return;
                    var wallet = ui.Player.GetComponent<PlayerWallet>();
                    if (wallet.TryPurchaseModule(Mathf.Max(0, money - oldMoney), Mathf.Max(0, materials - oldMaterials),
                        () => assembly.RestoreLayout(plan)))
                    {
                        wallet.RefundConstruction(Mathf.Max(0, oldMoney - money)); wallet.CreditModuleCredits(Mathf.Max(0, oldMaterials - materials));
                        current.SetWeaponName(blueprint.name); GetComponent<WeaponNameUI>()?.RefreshName();
                        current.GetComponentInChildren<StatCollector>()?.Recalculate();
                        ui.ShowNotification("무기 코드를 적용했습니다.");
                    }
                }, () => { });
            }
            catch (Exception e) { ui.ShowNotification(e.Message); }
        }
    }
}
