using UnityEngine;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RaidTicketPickup : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float pickupDelay = 0.35f;
        [SerializeField] private PickupMagnet magnet = new PickupMagnet();
        [SerializeField] private Color color = new Color(0.65f, 0.2f, 1f, 1f);
        private FirstPersonPlayer recipient;
        private bool collected;
        private float availableAt;
        private System.Func<FirstPersonPlayer, bool> canCollect;

        private void Awake()
        {
            gameObject.AddComponent<PickupOutline>();
            availableAt = Time.time + pickupDelay;
            magnet.Initialize(GetComponent<Rigidbody>());
            canCollect = player => player == recipient && Time.time >= availableAt;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
                renderer.SetPropertyBlock(tint);
        }

        public void Release(FirstPersonPlayer player, Vector3 velocity)
        {
            recipient = player;
            magnet.Launch(velocity);
        }

        private void FixedUpdate()
        {
            if (collected || GameManager.GameplayBlocked) return;
            var player = magnet.Tick(canCollect);
            if (player == null || NightRaidController.Instance == null ||
                !NightRaidController.Instance.TryCollectTicket(player)) return;
            collected = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
