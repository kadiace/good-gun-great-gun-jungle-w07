using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    // 사격 테스트 입력은 Workshop의 Interact(F)와 EndShootingTest(E)를 사용한다.
    public sealed class PlayerShootingTest : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        private InputActionMap controls;
        private InputAction endTest;
        public ShootingTestController CurrentTest { get; private set; }
        private void Awake()
        {
            CurrentTest = null;
            if (inputActions == null)
            {
                Debug.LogError("사격 테스트 종료 입력 에셋 연결을 확인하세요.", this);
                return;
            }
            controls = inputActions.FindActionMap("Workshop", true).Clone();
            endTest = controls.FindAction("EndShootingTest", true);
            endTest.performed += OnEndTestPerformed;
        }
        private void OnEnable() => endTest?.Enable();
        private void OnEndTestPerformed(InputAction.CallbackContext context) => TryEndTest();
        public bool TryEndTest()
        {
            if (CurrentTest == null || GameManager.GameplayBlocked || UIInputPolicy.IsInWorkshop) return false;
            CurrentTest.Cancel();
            return true;
        }
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
        private void OnDisable()
        {
            endTest?.Disable();
            CurrentTest?.Cancel();
            CurrentTest = null;
        }
        private void OnDestroy()
        {
            if (endTest != null) endTest.performed -= OnEndTestPerformed;
            controls?.Dispose();
        }
    }
}
