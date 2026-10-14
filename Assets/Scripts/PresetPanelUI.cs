using System;
using System.Linq;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class PresetPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel, choicePanel;
        [SerializeField] private UnityEngine.UI.Button[] slots;
        [SerializeField] private UnityEngine.UI.Button saveButton, loadButton, closeButton;
        [SerializeField] private TMPro.TMP_Text choiceMessage;
        private UIManager ui;
        private EquipableWeapon selectedWeapon;
        private int selectedSlot;
        private bool ownsDialogue;
        public bool IsOpen => choicePanel != null && choicePanel.activeSelf;
        public RectTransform TutorialFirstSlot => slots != null && slots.Length > 0 && slots[0] != null ? slots[0].transform as RectTransform : null;
        public RectTransform TutorialSaveButton => saveButton != null ? saveButton.transform as RectTransform : null;
        private WorkshopSession Session => ui.Player != null ? ui.Player.GetComponent<WorkshopSession>() : null;
        private WeaponPresets Presets => ui.Player != null ? ui.Player.GetComponent<WeaponPresets>() : null;
        private void Awake()
        {
            ui = GetComponent<UIManager>();
            panel.SetActive(false); choicePanel.SetActive(false); ownsDialogue = false;
            for (int i = 0; i < slots.Length; i++) { int slot = i; slots[i].onClick.AddListener(() => Select(slot)); }
            saveButton.onClick.AddListener(Save); loadButton.onClick.AddListener(Load); closeButton.onClick.AddListener(Close);
        }
        private void LateUpdate()
        {
            var session = Session;
            bool available = session != null && session.IsEditing && session.ActiveWorkbench.CurrentWeapon != null;
            panel.SetActive(available);
            if (IsOpen && (!available || session.ActiveWorkbench.CurrentWeapon != selectedWeapon)) Close();
            foreach (var slot in slots) slot.interactable = available && !GameManager.GameplayBlocked;
        }
        private void Select(int slot)
        {
            if (TutorialManager.Instance != null && TutorialManager.Instance.CurrentStep != null &&
                TutorialManager.Instance.CurrentStep.requiredSignal == "preset-saved" && slot != 0) return;
            if (!TutorialManager.ActionAllowed("preset-save")) return;
            if (GameManager.GameplayBlocked || Session == null || !Session.IsEditing || Presets == null) return;
            selectedSlot = slot; selectedWeapon = Session.ActiveWorkbench.CurrentWeapon;
            if (selectedWeapon == null) return;
            if (string.IsNullOrEmpty(Presets.GetCode(slot))) { Save(); return; }
            try
            {
                var name = WeaponShareCode.Decode(Presets.GetCode(slot)).name;
                choiceMessage.text = $"프리셋 {slot + 1} · {name}\n현재 무기를 저장하거나 저장된 무기를 불러오세요.\n불러오기는 현재 조립을 교체하며 제작비 차액을 결제/환불합니다.";
                choicePanel.SetActive(true); choicePanel.transform.SetAsLastSibling();
                ownsDialogue = GameManager.Instance != null;
                if (ownsDialogue) GameManager.Instance.SetDialogue(true);
            }
            catch (Exception e) { ui.ShowNotification(e.Message); selectedWeapon = null; }
        }
        private bool StillEditing => selectedWeapon != null && Session != null && Session.IsEditing && Session.ActiveWorkbench.CurrentWeapon == selectedWeapon;
        private void Save()
        {
            try
            {
                if (!StillEditing || Presets == null) return;
                Presets.Store(selectedSlot, selectedWeapon);
                ui.ShowNotification($"프리셋{selectedSlot + 1}에 저장되었습니다.");
                Close();
                if (TutorialManager.Instance != null) TutorialManager.Instance.NotifySignal("preset-saved");
            }
            catch (Exception e) { ui.ShowNotification(e.Message); }
        }
        private void Load()
        {
            if (!TutorialManager.ActionAllowed("preset-load")) return;
            try
            {
                if (!StillEditing || Presets == null) return;
                if (selectedWeapon.ConstructionLocked) throw new ArgumentException("커스텀을 제거한 후 프리셋을 불러와 주세요.");
                var blueprint = WeaponShareCode.Decode(Presets.GetCode(selectedSlot));
                var assembly = selectedWeapon.GetComponentInChildren<CubeAssembly>();
                if (!assembly.TryCaptureLayout(out var old)) throw new ArgumentException("현재 조립을 읽을 수 없습니다.");
                int oldMoney = old.blocks.Sum(b => b.paidPurchaseCost), oldMaterials = old.blocks.Sum(b => b.paidModuleCreditCost);
                var plan = WeaponShareCode.Resolve(blueprint, assembly, ui.Player.GetComponent<PlayerModuleUnlocks>(),
                    Mathf.Max(0, old.paidConstructionCost - oldMoney), out int money, out int materials);
                var wallet = ui.Player.GetComponent<PlayerWallet>();
                if (!wallet.TryPurchaseModule(Mathf.Max(0, money - oldMoney), Mathf.Max(0, materials - oldMaterials), () => assembly.RestoreLayout(plan))) return;
                wallet.Credit(Mathf.Max(0, oldMoney - money)); wallet.CreditModuleCredits(Mathf.Max(0, oldMaterials - materials));
                selectedWeapon.SetWeaponName(blueprint.name);
                selectedWeapon.GetComponentInChildren<StatCollector>()?.Recalculate();
                GetComponent<WeaponNameUI>()?.RefreshName();
                ui.ShowNotification($"프리셋{selectedSlot + 1}을 불러왔습니다.");
                Close();
            }
            catch (Exception e) { ui.ShowNotification(e.Message); }
        }
        public void Close()
        {
            choicePanel.SetActive(false); selectedWeapon = null;
            if (ownsDialogue && GameManager.Instance != null) GameManager.Instance.SetDialogue(false);
            ownsDialogue = false;
        }
        private void OnDisable() { Close(); panel.SetActive(false); }
    }
}
