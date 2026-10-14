using UnityEngine;
namespace WeaponWorkshop
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ModuleUnlockPickup : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float pickupDelay = 0.35f;
        [SerializeField] private PickupMagnet magnet = new PickupMagnet();
        private int moduleId;
        private int moduleCredits;
        private bool rewardsCredits;
        private bool collected;
        private float availableAt;
        private System.Func<FirstPersonPlayer, bool> canCollect;
        private void Awake()
        {
            collected = false; availableAt = Time.time + pickupDelay;
            canCollect = player => Time.time >= availableAt &&
                (rewardsCredits ? player.GetComponent<PlayerWallet>() != null : player.GetComponent<PlayerModuleUnlocks>() != null);
            magnet.Initialize(GetComponent<Rigidbody>());
        }
        public void Release(int id, Vector3 velocity)
        { moduleId = id; rewardsCredits = false; magnet.Launch(velocity); }
        public void ReleaseCredits(int amount, Vector3 velocity)
        { moduleCredits = Mathf.Max(0, amount); rewardsCredits = true; magnet.Launch(velocity); }

        private void FixedUpdate()
        {
            if (collected || (!rewardsCredits && moduleId < 1) || GameManager.GameplayBlocked) return;
            var player = magnet.Tick(canCollect);
            if (player == null) return;
            var unlocks = player.GetComponent<PlayerModuleUnlocks>();
            var wallet = player.GetComponent<PlayerWallet>();
            if (rewardsCredits ? wallet == null : unlocks == null) return;
            collected = true;
            if (rewardsCredits) wallet.CreditModuleCredits(moduleCredits);
            else if (NightRaidController.Instance == null || !NightRaidController.Instance.TryCollectTrainingModule(moduleId))
                unlocks.UnlockModule(moduleId);
            if (!rewardsCredits && TutorialManager.Instance != null) TutorialManager.Instance.NotifyTrainingLoot(moduleId);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
