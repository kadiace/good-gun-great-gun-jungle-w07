using UnityEngine;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ModuleCreditPickup : MonoBehaviour
    {
        public static event System.Action<FirstPersonPlayer, int> CreditCollected;
        [SerializeField, Min(0f)] private float pickupDelay = 0.35f;
        [SerializeField] private PickupMagnet magnet = new PickupMagnet();
        [SerializeField] private Color color = new Color(1f, 0.75f, 0.12f, 1f);
        private int amount;
        private bool materialCrateReward;
        private bool collected;
        private float availableAt;
        public event System.Action Collected;

        private System.Func<FirstPersonPlayer, bool> canCollect;

        private void Awake()
        {
            gameObject.AddComponent<PickupOutline>();
            collected = false;
            availableAt = Time.time + pickupDelay;
            magnet.Initialize(GetComponent<Rigidbody>());
            canCollect = player => amount > 0 && Time.time >= availableAt &&
                player.GetComponent<PlayerWallet>() != null;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
                renderer.SetPropertyBlock(tint);
        }

        public void Release(int value, Vector3 velocity)
        {
            amount = Mathf.Max(0, value);
            magnet.Launch(velocity);
        }
        public void ReleaseFromCrate(int value, Vector3 velocity)
        {
            materialCrateReward = true;
            Release(value, velocity);
        }

        private void FixedUpdate()
        {
            if (collected || GameManager.GameplayBlocked) return;
            var player = magnet.Tick(canCollect);
            if (player == null) return;
            var raid = NightRaidController.Instance;
            if (raid != null && raid.IsInRaid)
            {
                if (!raid.TryCollectMaterial(player, amount)) return;
            }
            else PlayerWallet.ForPlayer(player)?.CreditModuleCredits(amount);
            collected = true;
            if (materialCrateReward) CreditCollected?.Invoke(player, amount);
            Collected?.Invoke();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
