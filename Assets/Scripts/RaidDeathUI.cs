using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    public sealed class RaidDeathUI : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button returnButton;
        private RaidPlayerHealth health;
        private bool hasStarted;
        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (returnButton != null) returnButton.onClick.AddListener(Return);
        }
        private void OnEnable() => BindHealth();

        private void Start()
        {
            hasStarted = true;
            BindHealth();
        }

        private void BindHealth()
        {
            if (SurvivalSession.IsActive)
            {
                if (health != null)
                {
                    health.Died -= Show;
                    health = null;
                }
                return;
            }

            var player = uiManager != null ? uiManager.Player : null;
            var nextHealth = player != null ? player.GetComponent<RaidPlayerHealth>() : null;
            if (health != nextHealth)
            {
                if (health != null) health.Died -= Show;
                health = nextHealth;
                if (health != null) health.Died += Show;
            }
            if (hasStarted && health != null && health.IsDead) Show();
        }
        private void Show()
        {
            if (SurvivalSession.IsActive) return;

            if (NightRaidController.Instance != null)
            {
                if (panel != null) panel.SetActive(false);
                NightRaidController.Instance.ShowDeathResult();
                return;
            }
            GameManager.Instance?.SetDialogue(true);
            GameManager.Instance?.SetNight(true);
            var player = uiManager != null ? uiManager.Player : null;
            player?.GetComponent<WeaponHolder>()?.EquippedWeapon?.CancelCharge();
            if (panel == null) return;
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            if (returnButton != null) returnButton.interactable = true;
        }
        private void Return() => NightRaidController.Instance?.ReturnAfterDeath();
        private void OnDisable()
        {
            if (health != null) health.Died -= Show;
            health = null;
            if (panel != null) panel.SetActive(false);
        }
        private void OnDestroy()
        {
            if (health != null) health.Died -= Show;
            if (returnButton != null) returnButton.onClick.RemoveListener(Return);
        }
    }
}
