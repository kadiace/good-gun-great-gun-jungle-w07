using System;
using UnityEngine;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DollarPickup : MonoBehaviour
    {
        public static event Action<FirstPersonPlayer, int> DollarCollected;
        [SerializeField, Min(0f)] private float pickupDelay = 0.35f;
        [SerializeField] private PickupMagnet magnet = new PickupMagnet();
        [SerializeField] private Color color = new Color(0.2f, 1f, 0.32f, 1f);
        private Func<FirstPersonPlayer, bool> canCollect;
        private int amount;
        private bool collected;
        private float availableAt;

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

        public void Release(int amount, Vector3 velocity)
        {
            this.amount = Mathf.Max(0, amount);
            magnet.Launch(velocity);
        }

        private void FixedUpdate()
        {
            if (collected || GameManager.GameplayBlocked) return;
            var player = magnet.Tick(canCollect);
            if (player == null) return;
            var raid = NightRaidController.Instance;
            if (raid != null && raid.IsInRaid)
            {
                if (!raid.TryCollectDollar(player, amount)) return;
            }
            else PlayerWallet.ForPlayer(player)?.Credit(amount);
            collected = true;
            DollarCollected?.Invoke(player, amount);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
