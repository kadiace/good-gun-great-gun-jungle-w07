using UnityEngine;
namespace WeaponWorkshop
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ModuleUnlockPickup : MonoBehaviour
    {
        public static event System.Action<FirstPersonPlayer, int> RewardCollected;
        [SerializeField, Min(0f)] private float pickupDelay = 0.35f;
        [SerializeField] private PickupMagnet magnet = new PickupMagnet();
        private int moduleId;
        private bool collected;
        private float availableAt;
        private System.Func<FirstPersonPlayer, bool> canCollect;
        private void Awake()
        {
            collected = false; availableAt = Time.time + pickupDelay;
            canCollect = player => Time.time >= availableAt && player.GetComponent<PlayerModuleUnlocks>() != null;
            magnet.Initialize(GetComponent<Rigidbody>());
        }
        public void Release(int id, Vector3 velocity)
        { moduleId = id; magnet.Launch(velocity); }

        private void FixedUpdate()
        {
            if (collected || moduleId < 1 || GameManager.GameplayBlocked) return;
            var player = magnet.Tick(canCollect);
            if (player == null) return;
            var unlocks = player.GetComponent<PlayerModuleUnlocks>();
            if (unlocks == null) return;
            collected = true;
            if (NightRaidController.Instance == null || !NightRaidController.Instance.TryCollectTrainingModule(moduleId))
                unlocks.UnlockModule(moduleId);
            RewardCollected?.Invoke(player, moduleId);
            if (TutorialManager.Instance != null) TutorialManager.Instance.NotifyTrainingLoot(moduleId);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
