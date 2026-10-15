using UnityEngine;

namespace WeaponWorkshop
{
    // The player's temporary retry interaction reuses Workshop/Interact (F).
    public sealed class PlayerShootingTest : MonoBehaviour
    {
        public ShootingTestController CurrentTest { get; private set; }
        private void Awake() => CurrentTest = null;
        public void Bind(ShootingTestController test) => CurrentTest = test;
        public void Unbind(ShootingTestController test) { if (CurrentTest == test) CurrentTest = null; }
        public bool TryRetry(IInteractable aimedTarget = null)
        {
            if (CurrentTest == null || !CurrentTest.CanRetryWithWeapon || GameManager.GameplayBlocked) return false;
            // Keep F available for picking up/placing/modding a gun after failure.
            if (aimedTarget != null && !object.ReferenceEquals(aimedTarget, CurrentTest.Bell)) return false;
            if (TutorialManager.Instance != null && !TutorialManager.Instance.TryAuthorizeInteraction(CurrentTest.Bell)) return false;
            CurrentTest.Retry();
            return true; // A failed preparation consumes this press too; don't also open the selection panel.
        }
        private void OnDisable() { CurrentTest?.Cancel(); CurrentTest = null; }
    }
}
