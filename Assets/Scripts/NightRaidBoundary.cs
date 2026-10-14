using UnityEngine;
namespace WeaponWorkshop
{
    [RequireComponent(typeof(BoxCollider),typeof(Rigidbody))]
    public sealed class NightRaidBoundary : MonoBehaviour
    {
        [SerializeField] private Vector3 inwardDirection;
        [SerializeField] private string exitName = "탈출구";
        public string ExitName => exitName;
        private BoxCollider zone;
        private void Awake() => zone = GetComponent<BoxCollider>();
        public bool Contains(FirstPersonPlayer player)
        {
            if (zone == null || player == null) return false;
            Vector3 local = transform.InverseTransformPoint(player.transform.position) - zone.center;
            Vector3 half = zone.size * .5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z &&
                local.y >= -half.y - .1f && local.y <= half.y;
        }
        private void OnTriggerEnter(Collider other)
        {
            var player=other.GetComponentInParent<FirstPersonPlayer>();
            if(player!=null)NightRaidController.Instance?.BeginExtraction(player,this);
        }
        private void OnTriggerStay(Collider other)
        {
            var player = other.GetComponentInParent<FirstPersonPlayer>();
            if (player != null && Contains(player)) NightRaidController.Instance?.BeginExtraction(player, this);
        }
        private void OnDisable() => NightRaidController.Instance?.CancelExtraction(this);
    }
}
