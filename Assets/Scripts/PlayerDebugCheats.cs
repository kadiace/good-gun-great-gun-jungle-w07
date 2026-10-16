using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class PlayerDebugCheats : MonoBehaviour
    {
#if UNITY_EDITOR
        private const int DebugCurrencyAmount = 9999999;

        [SerializeField] private InputActionAsset inputActions;
        private InputActionMap controls;

        private void Awake()
        {
            if (inputActions == null)
            {
                enabled = false;
                return;
            }

            controls = inputActions.FindActionMap("Player", true).Clone();
            controls.FindAction("DebugUnlockAll", true).performed += _ => ApplyDebugUnlocks();
            controls.FindAction("DebugSkipTutorialStep", true).performed += _ => SkipTutorialStep();
        }

        private static void SkipTutorialStep()
        {
            if (TutorialManager.Instance != null) TutorialManager.Instance.DebugSkipStep();
        }

        private void OnEnable() => controls?.Enable();
        private void OnDisable() => controls?.Disable();
        private void OnDestroy() => controls?.Dispose();

        private void ApplyDebugUnlocks()
        {
            PlayerModuleUnlocks unlocks = GetComponent<PlayerModuleUnlocks>();
            PlayerWallet wallet = GetComponent<PlayerWallet>();
            if (unlocks == null || wallet == null)
            {
                Debug.LogWarning("Player debug unlocks require PlayerModuleUnlocks and PlayerWallet.", this);
                return;
            }

            unlocks.UnlockAllLockedModules();
            wallet.RestoreBalance(DebugCurrencyAmount);
            wallet.RestoreModuleCredits(DebugCurrencyAmount);
        }
#else
        private void Awake() => enabled = false;
#endif
    }
}
