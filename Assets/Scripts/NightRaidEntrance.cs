using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class NightRaidEntrance : MonoBehaviour, IInteractable
    {
        [SerializeField] private _DayManager dayManager;
        [SerializeField] private NightRaidController nightRaid;
        [SerializeField] private Light readyLight;
        [SerializeField] private GameObject readyIndicator;
        private bool Ready => isActiveAndEnabled && dayManager != null && (dayManager.IsNight || (dayManager.NightWorkshop && dayManager.HasNightOrder)) &&
            !dayManager.IsSettlementOpen && nightRaid != null && !nightRaid.IsBusy;
        public Transform OutlineRoot => transform;
        private void Awake()
        {
            if (dayManager == null) dayManager = FindFirstObjectByType<_DayManager>();
            if (nightRaid == null) nightRaid = FindFirstObjectByType<NightRaidController>();
            SetReadyLight(false);
        }
        private void LateUpdate() => SetReadyLight(Ready);
        private void OnDisable() { SetReadyLight(false); }
        private void SetReadyLight(bool on)
        {
            if (readyLight != null) readyLight.enabled = on;
            if (readyIndicator != null && readyIndicator.activeSelf != on) readyIndicator.SetActive(on);
        }
        public bool CanInteract(Interaction interactor) => isActiveAndEnabled &&
            !GameManager.GameplayBlocked && dayManager != null && (dayManager.IsNight || (dayManager.NightWorkshop && dayManager.HasNightOrder)) &&
            nightRaid != null && !nightRaid.IsBusy && interactor != null && interactor.Player != null;
        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor)) return false;
            nightRaid.OpenMap();
            return true;
        }
    }
}
