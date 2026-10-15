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
        public bool TryRetry()
        {
            if (CurrentTest == null || !CurrentTest.CanRetryNearby || GameManager.GameplayBlocked) return false;
            if (TutorialManager.Instance != null && !TutorialManager.Instance.TryAuthorizeInteraction(CurrentTest.Bell)) return false;
            CurrentTest.Retry();
            return true; // A failed preparation consumes this press too; don't also open the selection panel.
        }
        private void OnDisable() { CurrentTest?.Cancel(); CurrentTest = null; }
    }
}
