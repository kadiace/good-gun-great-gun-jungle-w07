using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    public sealed class WeaponNameUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_InputField input;
        [SerializeField] private Button confirm;
        private UIManager ui;
        private EquipableWeapon weapon;
        private bool ownsDialogue;
        private void Awake()
        {
            ui = GetComponent<UIManager>(); panel.SetActive(false); ownsDialogue = false;
            confirm.interactable = false;
            var label = confirm.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "저장";
            confirm.onClick.AddListener(Confirm);
            input.onValueChanged.AddListener(_ => RefreshSaveButton());
            input.onSelect.AddListener(_ => Focus());
            input.onDeselect.AddListener(_ => ReleaseFocus());
        }
        private void LateUpdate()
        {
            var session = ui.Player != null ? ui.Player.GetComponent<WorkshopSession>() : null;
            var current = session != null && session.IsEditing ? session.ActiveWorkbench.CurrentWeapon : null;
            panel.SetActive(current != null);
            if (weapon == current) { RefreshSaveButton(); return; }
            ReleaseFocus(); weapon = current; RefreshName();
        }
        public void RefreshName()
        {
            input.SetTextWithoutNotify(weapon != null ? weapon.WeaponName : "");
            RefreshSaveButton();
        }
        private string PendingName()
        {
            string value = (input.text ?? "").Trim().Replace("\n", " ").Replace("\r", " ");
            return string.IsNullOrWhiteSpace(value) ? "나만의 총" : value.Substring(0, Mathf.Min(40, value.Length));
        }
        private void RefreshSaveButton() => confirm.interactable = weapon != null && PendingName() != weapon.WeaponName;
        private void Confirm()
        {
            if (!TutorialManager.TryAuthorizeAction("rename")) return;
            if (weapon == null || PendingName() == weapon.WeaponName) return;
            weapon.SetWeaponName(input.text); RefreshName(); ReleaseFocus();
            EventSystem.current?.SetSelectedGameObject(null);
            ui.ShowNotification("무기 이름을 저장했습니다.");
            if (TutorialManager.Instance != null) TutorialManager.Instance.NotifySignal("weapon-renamed");
        }
        private void Focus()
        {
            if (!TutorialManager.TryAuthorizeAction("rename")) { input.DeactivateInputField(); return; }
            // 입력칸 자신이 선택된 상태는 막힘으로 보지 않는다.
            if (GameManager.ModalBlocked || GameManager.Instance == null) return;
            ownsDialogue = true; GameManager.Instance.SetDialogue(true);
        }
        private void ReleaseFocus()
        {
            if (ownsDialogue && GameManager.Instance != null) GameManager.Instance.SetDialogue(false);
            ownsDialogue = false;
        }
        public bool CancelEditing()
        {
            if (!input.isFocused) return false;
            RefreshName(); input.DeactivateInputField();
            EventSystem.current?.SetSelectedGameObject(null); ReleaseFocus(); return true;
        }
        private void OnDisable() { panel.SetActive(false); ReleaseFocus(); weapon = null; confirm.interactable = false; }
    }
}
