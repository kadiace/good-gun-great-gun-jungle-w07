using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(DestructibleTarget))]
    public sealed class ModuleCreditCrate : MonoBehaviour, IInteractable
    {
        private static readonly List<ModuleCreditCrate> activeCrates = new List<ModuleCreditCrate>();
        public static IReadOnlyList<ModuleCreditCrate> ActiveCrates => activeCrates;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => activeCrates.Clear();
        private void OnEnable() => activeCrates.Add(this);
        private void OnDisable() => activeCrates.Remove(this);
        [SerializeField, Min(1)] private int reward = 50;
        [SerializeField] private ModuleCreditPickup pickupPrefab;
        [SerializeField, Min(1)] private int dropCount = 5;
        [SerializeField, Min(0f)] private float scatterSpeed = 2f;
        [SerializeField, Min(0f)] private float upwardSpeed = 3f;
        private DestructibleTarget structure;
        private bool claimed;
        public bool IsClaimed => claimed;
        public Transform OutlineRoot => transform;

        private void Awake()
        {
            claimed = false;
            structure = GetComponent<DestructibleTarget>();
        }
        private void LateUpdate()
        {
            // Covers penetration, explosion, attributes and gravity without deleting the remaining body.
            if (!claimed && structure != null && structure.InitialCellCount > 0 &&
                structure.RemainingCellCount < structure.InitialCellCount)
                ReleaseContents();
        }
        private void ReleaseContents()
        {
            if (claimed) return;
            claimed = true;
            if (pickupPrefab == null)
            {
                Debug.LogError("상자에 ModuleCreditPickup 프리팹을 연결해주세요.", this);
                return;
            }
            int count = Mathf.Clamp(dropCount, 1, Mathf.Max(1, reward));
            float rotation = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float angle = (rotation + 360f * i / count) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var pickup = Instantiate(pickupPrefab, transform.position + Vector3.up * 0.25f + direction * 0.2f,
                    Quaternion.identity, transform.parent);
                int amount = reward / count + (i < reward % count ? 1 : 0);
                pickup.ReleaseFromCrate(amount, direction * scatterSpeed + Vector3.up * upwardSpeed);
            }
        }

        public bool CanInteract(Interaction interactor) => false;
        public bool Interact(Interaction interactor) => false;
    }
}
