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
        [SerializeField] private ModuleCreditPickup pickupPrefab;
        [SerializeField] private DollarPickup dollarPrefab;
        [SerializeField, Min(1), Tooltip("픽업 1개에 담는 재료. 상자 재료 ÷ 이 값(올림)이 픽업 개수다.")]
        private int creditsPerPickup = 50;
        [SerializeField, Min(0f)] private float scatterSpeed = 0.75f;
        [SerializeField, Min(0f)] private float upwardSpeed = 6f;
        private DestructibleTarget structure;
        private int reward;
        private bool claimed;
        public bool IsClaimed => claimed;
        public Transform OutlineRoot => transform;

        private void Awake()
        {
            claimed = false;
            structure = GetComponent<DestructibleTarget>();
        }
        // 재료량은 스테이지 표(RaidMapData reward.materialPerCrate)에서 스포너가 넣는다.
        public void Configure(int amount) => reward = Mathf.Max(0, amount);
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
            if (reward <= 0) return;
            if (pickupPrefab == null)
            {
                Debug.LogError("상자에 ModuleCreditPickup 프리팹을 연결해주세요.", this);
                return;
            }
            int count = Mathf.Clamp(Mathf.CeilToInt(reward / (float)creditsPerPickup), 1, reward);
            var spawner = GetComponentInParent<RaidCrateSpawner>();
            string rewardScene = spawner != null ? spawner.RewardScene : gameObject.scene.name;
            var stageReward = TableManager.Instance.RaidMap.Find(rewardScene)?.Settings()?.reward;
            if (stageReward == null) Debug.LogError($"스테이지 테이블에 {rewardScene} 달러 보상 행이 없습니다.", this);
            int dollars = Mathf.Max(0, stageReward?.dollarsPerMaterialPickup ?? 0);
            if (dollars > 0 && dollarPrefab == null)
                Debug.LogError("상자에 DollarPickup 프리팹을 연결해주세요.", this);
            float rotation = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float angle = (rotation + 360f * i / count) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var pickup = Instantiate(pickupPrefab, transform.position + Vector3.up * 0.25f + direction * 0.2f,
                    Quaternion.identity, transform.parent);
                int amount = reward / count + (i < reward % count ? 1 : 0);
                pickup.ReleaseFromCrate(amount, direction * scatterSpeed + Vector3.up * upwardSpeed);
                if (dollars > 0 && dollarPrefab != null)
                {
                    Vector3 dollarDirection = Quaternion.Euler(0f, 180f / count, 0f) * direction;
                    var dollar = Instantiate(dollarPrefab, transform.position + Vector3.up * 0.4f + dollarDirection * 0.4f,
                        Quaternion.identity, transform.parent);
                    dollar.Release(dollars, dollarDirection * scatterSpeed + Vector3.up * upwardSpeed);
                }
            }
        }

        public bool CanInteract(Interaction interactor) => false;
        public bool Interact(Interaction interactor) => false;
    }
}
