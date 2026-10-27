using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class RaidModuleCrateScanKeycapUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        private UIManager uiManager;
        private PlayerScanMode scanMode;
        private Interaction interaction;
        private readonly List<Collider> crateColliders = new List<Collider>();

        private void Awake()
        {
            uiManager = GetComponent<UIManager>();
            Hide();
        }

        private void OnEnable() => Hide();
        private void OnDisable() => Hide();

        private void LateUpdate()
        {
            if (panel == null) return;

            FirstPersonPlayer player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null || !player.isActiveAndEnabled || !Application.isFocused ||
                NightRaidController.Instance == null || GameManager.GameplayBlocked)
            {
                Hide();
                return;
            }

            if (scanMode == null || scanMode.gameObject != player.gameObject)
                scanMode = player.GetComponent<PlayerScanMode>();
            if (interaction == null || !interaction.transform.IsChildOf(player.transform))
                interaction = player.GetComponentInChildren<Interaction>(true);
            if (interaction == null || !interaction.isActiveAndEnabled)
            {
                Hide();
                return;
            }
            if (scanMode == null || !scanMode.isActiveAndEnabled || scanMode.IsScanning)
            {
                Hide();
                return;
            }

            bool nearby = false;
            float squaredDistance = interaction.InteractionDistance * interaction.InteractionDistance;
            foreach (ModuleUnlockCrate crate in ModuleUnlockCrate.ActiveCrates)
            {
                if (crate == null || !crate.isActiveAndEnabled || crate.IsClaimed) continue;
                crate.GetComponentsInChildren(false, crateColliders);
                foreach (Collider collider in crateColliders)
                {
                    if (collider == null || !collider.enabled || collider.isTrigger) continue;
                    Vector3 nearest = collider.ClosestPoint(interaction.transform.position);
                    if ((nearest - interaction.transform.position).sqrMagnitude <= squaredDistance)
                    {
                        nearby = true;
                        break;
                    }
                }
                if (nearby) break;
            }

            if (panel.activeSelf != nearby) panel.SetActive(nearby);
        }

        private void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
