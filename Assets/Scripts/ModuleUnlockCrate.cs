using System.Collections.Generic;
using UnityEngine;
namespace WeaponWorkshop
{
    [RequireComponent(typeof(DestructibleTarget))]
    public sealed class ModuleUnlockCrate : MonoBehaviour, IInteractable
    {
        private static readonly List<ModuleUnlockCrate> activeCrates = new List<ModuleUnlockCrate>();
        public static IReadOnlyList<ModuleUnlockCrate> ActiveCrates => activeCrates;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => activeCrates.Clear();
        private void OnEnable() => activeCrates.Add(this);
        private void OnDisable() => activeCrates.Remove(this);
        [SerializeField, Min(1)] private int moduleId = 10;
        [SerializeField] private ModuleUnlockPickup pickupPrefab;
        [SerializeField, Min(0f)] private float upwardSpeed = 3f;
        [SerializeField] private GameObject classificationLabel;
        private DestructibleTarget body;
        private bool claimed;
        public bool IsClaimed => claimed;
        private RaidCrateSpawner.Objective objective;
        public void TrackObjective(RaidCrateSpawner.Objective target) => objective = target;
        public Transform OutlineRoot => transform;
        private void Awake() { body = GetComponent<DestructibleTarget>(); claimed = false; }
        public void Configure(int id)
        {
            moduleId = id;
            body.DamageProtected = TutorialManager.TrainingRaidPending;
        }
        private void LateUpdate()
        {
            if (claimed || body == null || body.InitialCellCount <= 0 || body.RemainingCellCount >= body.InitialCellCount) return;
            if (pickupPrefab == null) return;
            claimed = true;
            if (classificationLabel != null) classificationLabel.SetActive(false);
            var pickup = Instantiate(pickupPrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity, transform.parent);
            if (objective != null) objective.Target = pickup.transform;
            pickup.Release(moduleId, Vector3.up * upwardSpeed);
        }
        public bool CanInteract(Interaction interactor) => false;
        public bool Interact(Interaction interactor) => false;
    }
}
