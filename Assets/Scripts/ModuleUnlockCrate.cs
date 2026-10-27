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
        [SerializeField, Min(0f)] private float upwardSpeed = 6f;
        [SerializeField] private GameObject classificationLabel;
        private DestructibleTarget body;
        private bool claimed;
        private GameObject modulePrefab;
        private string moduleName;
        public bool IsClaimed => claimed;
        private RaidCrateSpawner.Objective objective;
        public void TrackObjective(RaidCrateSpawner.Objective target) => objective = target;
        public Transform OutlineRoot => transform;
        private void Awake() { body = GetComponent<DestructibleTarget>(); claimed = false; }
        /// <summary>
        /// 상자에 저장할 모듈 정보(식별자, 외형 프리팹, 표시 이름)를 설정합니다.
        /// </summary>
        public void Configure(int id, GameObject prefab, string displayName)
        {
            moduleId = id;
            modulePrefab = prefab;
            moduleName = displayName;
        }
        private void LateUpdate()
        {
            if (claimed || body == null || body.InitialCellCount <= 0 || body.RemainingCellCount >= body.InitialCellCount) return;
            if (pickupPrefab == null) return;
            claimed = true;
            // 잔해는 유지하고, 공통 시선 판정에서 상자 상호작용만 제외한다.
            enabled = false;
            if (classificationLabel != null) classificationLabel.SetActive(false);
            var pickup = Instantiate(pickupPrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity, transform.parent);
            if (objective != null) objective.Target = pickup.transform;
            pickup.Release(moduleId, Vector3.up * upwardSpeed, modulePrefab, moduleName);
        }
        public bool CanInteract(Interaction interactor) => false;
        public bool Interact(Interaction interactor) => false;
    }
}
