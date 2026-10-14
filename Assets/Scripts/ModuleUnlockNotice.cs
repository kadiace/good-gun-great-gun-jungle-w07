using UnityEngine;
namespace WeaponWorkshop
{
    public sealed class ModuleUnlockNotice : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        private PlayerModuleUnlocks unlocks;
        private void Start()
        {
            unlocks = uiManager != null && uiManager.Player != null ? uiManager.Player.GetComponent<PlayerModuleUnlocks>() : null;
            if (unlocks != null) unlocks.ModuleUnlocked += Show;
        }
        private void OnEnable() { if (unlocks != null) unlocks.ModuleUnlocked += Show; }
        private void OnDisable() { if (unlocks != null) unlocks.ModuleUnlocked -= Show; }
        private void OnDestroy() { if (unlocks != null) unlocks.ModuleUnlocked -= Show; }
        private void Show(int id) => uiManager?.ShowNotification($"{unlocks.GetModuleName(id)} 모듈 해금!");
    }
}
