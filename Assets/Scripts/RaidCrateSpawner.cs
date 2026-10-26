using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 습격 보상 상자를 한 곳에서 같은 시점에 만든다. 무엇을·몇 개·얼마나는 스테이지 표의 reward 행이,
    // 어디에는 씬의 지점이 정한다. NightRaidController가 입장 상태를 복원한 직후 Spawn을 호출한다.
    public sealed class RaidCrateSpawner : MonoBehaviour
    {
        [SerializeField] private ModuleCreditCrate materialCratePrefab;
        [SerializeField] private ModuleUnlockCrate moduleCratePrefab;
        [SerializeField] private ModuleCatalog catalog;
        [SerializeField, Tooltip("재료 상자 후보 지점. 표의 materialCrates개를 겹치지 않게 무작위로 고른다.")]
        private Transform[] materialSpawnPoints;
        [SerializeField, Tooltip("랜덤 재료 상자 제외 영역의 바닥 중심점. 비어 있으면 제외하지 않습니다.")]
        private Transform[] _materialExclusionPoints;
        [SerializeField, Tooltip("제외 영역 전체 크기(X 너비, Y 높이, Z 깊이). 월드 축을 기준으로 바닥 중심에서 위로 적용합니다.")]
        private Vector3 _materialExclusionSize = new Vector3(3f, 2f, 3f);
        [SerializeField, Tooltip("모듈 상자 고정 지점. 지점마다 모듈을 지정한다.")]
        private RaidModuleCrateSpot[] moduleSpots;
        [SerializeField, Tooltip("보상 행을 찾을 씬 이름. 비우면 이 씬 이름을 쓴다(연습 씬은 원본 스테이지 이름).")]
        private string rewardScene;
        [SerializeField, Tooltip("연습 씬용. 이미 해금한 모듈도 모듈 상자를 생성합니다.")]
        private bool spawnUnlockedRewards;

        public sealed class Objective
        {
            public int ModuleId { get; }
            public Transform Target { get; set; }
            public Objective(int moduleId, Transform target) { ModuleId = moduleId; Target = target; }
        }
        private readonly List<Objective> objectives = new List<Objective>();
        private readonly List<ModuleCreditCrate> materialCrates = new List<ModuleCreditCrate>();
        public IReadOnlyList<Objective> Objectives => objectives;
        public bool IsReady { get; private set; }
        public int SpawnedModuleCrates => objectives.Count;
        public int SpawnedMaterialCrates => materialCrates.Count;
        public int ReplacedModuleSpots { get; private set; }
        public int MaterialPerCrate { get; private set; }
        public string RewardScene => string.IsNullOrEmpty(rewardScene) ? gameObject.scene.name : rewardScene;
        public int RemainingMaterialCrateCount
        {
            get
            {
                int count = 0;
                foreach (var crate in materialCrates)
                    if (crate != null && crate.isActiveAndEnabled && !crate.IsClaimed) count++;
                return count;
            }
        }

        private void Awake()
        {
            IsReady = false;
            ReplacedModuleSpots = MaterialPerCrate = 0;
            objectives.Clear();
            materialCrates.Clear();
        }

        // 한 번만 생성한다. unlocks는 입장 시점 해금 상태가 복원된 플레이어 컴포넌트다.
        public void Spawn(PlayerModuleUnlocks unlocks)
        {
            if (IsReady) return;
            IsReady = true;
            var region = TableManager.Instance.RaidMap.Find(RewardScene);
            var settings = region.Settings();
            if (settings == null || settings.reward == null)
            {
                Debug.LogError($"스테이지 테이블에 {RewardScene} 보상 행이 없어 보상 상자를 만들지 않습니다.", this);
                return;
            }
            MaterialPerCrate = settings.reward.materialPerCrate;
            SpawnModuleCrates(settings.reward, unlocks);
            SpawnMaterialCrates(settings.reward);
            DistributeMaterialRewards();
        }

        // 표의 모듈마다 지정된 지점에 모듈 상자를 놓는다. 이미 해금한 모듈이면 같은 자리에 재료 상자를 놓는다.
        private void SpawnModuleCrates(RaidMapData.Reward reward, PlayerModuleUnlocks unlocks)
        {
            foreach (string code in reward.moduleCodes ?? System.Array.Empty<string>())
            {
                var spot = FindSpot(code);
                if (spot == null) { Debug.LogWarning($"{RewardScene}에 모듈 {code}의 상자 지점이 없습니다.", this); continue; }
                int id = FindModuleId(code);
                if (id == 0) { Debug.LogWarning($"모듈 카탈로그에 {code} 모듈이 없습니다.", this); continue; }
                if (!spawnUnlockedRewards && unlocks != null && unlocks.IsUnlocked(id))
                {
                    SpawnMaterialCrate(spot.transform, reward.materialPerCrate);
                    ReplacedModuleSpots++;
                    continue;
                }
                if (moduleCratePrefab == null) continue;
                if (!catalog.TryGet(id, out var entry) || entry.prefab == null)
                {
                    Debug.LogError($"모듈 {code}의 외형 프리팹이 없습니다.", this);
                    continue;
                }
                var crate = Instantiate(moduleCratePrefab, spot.transform.position, spot.transform.rotation, transform);
                crate.Configure(id, entry.prefab, entry.displayName);
                var objective = new Objective(id, crate.transform);
                objectives.Add(objective);
                crate.TrackObjective(objective);
            }
        }

        // 후보 지점을 섞어 표의 개수만큼 겹치지 않게 고른다.
        private void SpawnMaterialCrates(RaidMapData.Reward reward)
        {
            var available = new List<Transform>();
            foreach (Transform point in materialSpawnPoints ?? System.Array.Empty<Transform>())
                if (point != null && !available.Contains(point) && !IsMaterialSpawnExcluded(point.position)) available.Add(point);
            int count = Mathf.Min(Mathf.Max(0, reward.materialCrates), available.Count);
            if (count < reward.materialCrates)
                Debug.LogWarning($"{RewardScene}의 재료 상자 지점({available.Count})이 표의 상자 수({reward.materialCrates})보다 적습니다.", this);
            for (int i = 0; i < count; i++)
            {
                int index = Random.Range(i, available.Count);
                Transform point = available[index];
                available[index] = available[i];
                available[i] = point;
                SpawnMaterialCrate(point, reward.materialPerCrate);
            }
        }

        /// <summary>
        /// 랜덤 재료 상자의 생성 중심이 지정한 바닥 기준 제외 영역에 들어오는지 확인합니다.
        /// 모듈 대체 상자와 고정 상자는 이 판정을 호출하지 않아 기존 위치를 유지합니다.
        /// </summary>
        private bool IsMaterialSpawnExcluded(Vector3 position)
        {
            if (_materialExclusionPoints == null) return false;
            float halfWidth = Mathf.Max(0f, _materialExclusionSize.x) * .5f;
            float height = Mathf.Max(0f, _materialExclusionSize.y);
            float halfDepth = Mathf.Max(0f, _materialExclusionSize.z) * .5f;
            foreach (Transform point in _materialExclusionPoints)
            {
                if (point == null) continue;
                Vector3 offset = position - point.position;
                if (Mathf.Abs(offset.x) <= halfWidth && Mathf.Abs(offset.z) <= halfDepth &&
                    offset.y >= 0f && offset.y <= height) return true;
            }
            return false;
        }

        private void SpawnMaterialCrate(Transform point, int amount)
        {
            if (materialCratePrefab == null) return;
            var crate = Instantiate(materialCratePrefab, point.position, point.rotation, transform);
            crate.Configure(amount);
            materialCrates.Add(crate);
        }

        private void DistributeMaterialRewards()
        {
            int count = materialCrates.Count;
            if (count < 2 || materialCratePrefab == null) return;
            int creditsPerPickup = materialCratePrefab.CreditsPerPickup;
            if (MaterialPerCrate < creditsPerPickup || MaterialPerCrate % creditsPerPickup != 0) return;

            int basePickups = MaterialPerCrate / creditsPerPickup;
            if (basePickups < 2) return;
            var pickups = new int[count];
            for (int i = 0; i < count; i++) pickups[i] = basePickups;
            for (int i = 0; i < count * 2; i++)
            {
                int from = Random.Range(0, count);
                int to = Random.Range(0, count - 1);
                if (to >= from) to++;
                if (pickups[from] <= 1 || pickups[to] >= basePickups * 2 - 1) continue;
                pickups[from]--;
                pickups[to]++;
            }
            bool varied = false;
            for (int i = 0; i < count; i++)
                if (pickups[i] != basePickups) { varied = true; break; }
            if (!varied)
            {
                int from = Random.Range(0, count);
                int to = (from + Random.Range(1, count)) % count;
                pickups[from]--;
                pickups[to]++;
            }
            for (int i = 0; i < count; i++) materialCrates[i].Configure(pickups[i] * creditsPerPickup);
        }

        private RaidModuleCrateSpot FindSpot(string code)
        {
            RaidModuleCrateSpot found = null;
            foreach (var spot in moduleSpots ?? System.Array.Empty<RaidModuleCrateSpot>())
            {
                if (spot == null || spot.ModuleCode != code) continue;
                if (found != null) { Debug.LogWarning($"{RewardScene}에 모듈 {code}의 상자 지점이 여러 개라 첫 지점을 씁니다.", this); break; }
                found = spot;
            }
            return found;
        }

        private int FindModuleId(string code)
        {
            for (int id = 1; catalog != null && id <= catalog.Count; id++)
                if (catalog.TryGet(id, out var entry) && entry.data != null && entry.data.CodeId == code) return id;
            return 0;
        }

        private void OnDrawGizmosSelected()
        {
            if (materialSpawnPoints == null) return;
            Gizmos.color = Color.cyan;
            foreach (Transform point in materialSpawnPoints)
                if (point != null) Gizmos.DrawWireCube(point.position, Vector3.one * 0.8f);
        }
    }
}
