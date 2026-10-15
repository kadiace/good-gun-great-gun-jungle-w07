using UnityEngine;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ModuleCreditPickup : MonoBehaviour
    {
        public static event System.Action<FirstPersonPlayer, int> CreditCollected;
        [SerializeField, Min(0f)] private float pickupDelay = 0.35f;
        [SerializeField] private PickupMagnet magnet = new PickupMagnet();
        [SerializeField] private Color color = new Color(0.2f, 1f, 0.32f, 1f);
        private int amount;
        private bool materialCrateReward;
        private bool collected;
        private float availableAt;
        public event System.Action Collected;

        private System.Func<FirstPersonPlayer, bool> canCollect;

        private void Awake()
        {
            collected = false;
            availableAt = Time.time + pickupDelay;
            canCollect = player => Time.time >= availableAt && PlayerWallet.ForPlayer(player) != null;
            magnet.Initialize(GetComponent<Rigidbody>());
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
            if (collected || amount <= 0 || GameManager.GameplayBlocked) return;
            var player = magnet.Tick(canCollect);
            if (player == null) return;
            var wallet = PlayerWallet.ForPlayer(player);
            if (wallet == null) return;
            collected = true;
            wallet.CreditModuleCredits(amount);
            if (materialCrateReward) CreditCollected?.Invoke(player, amount);
            Collected?.Invoke();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
