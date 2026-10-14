using System.Collections.Generic;
using UnityEngine;
namespace WeaponWorkshop
{
    public sealed class ModuleUnlockCrateSpawner : MonoBehaviour
    {
        [SerializeField] private ModuleUnlockCrate cratePrefab;
        [SerializeField] private ModuleCatalog catalog;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private ModuleData[] rewardModules;
        [SerializeField, Tooltip("연습 씬용. 이미 해금한 모듈도 상자를 생성합니다.")] private bool spawnUnlockedRewards;
        public sealed class Objective
        {
            public int ModuleId { get; }
            public Transform Target { get; set; }
            public Objective(int moduleId, Transform target) { ModuleId = moduleId; Target = target; }
        }
        private readonly List<Objective> objectives = new List<Objective>();
        public IReadOnlyList<Objective> Objectives => objectives;
        public bool IsReady { get; private set; }

        private System.Collections.IEnumerator Start()
        {
            // Restore the raid-entry player's progression before drawing the loot pool.
            yield return null;
            Spawn();
            IsReady = true;
        }
        private void Spawn()
        {
            if (cratePrefab == null || catalog == null || spawnPoints == null) return;
            var wallet = PlayerWallet.ForScene(this);
            var unlocks = wallet != null ? wallet.GetComponent<PlayerModuleUnlocks>() : null;
            if (unlocks == null) return;
            if (rewardModules == null) return;
            var spawned = new HashSet<int>();
            for (int i = 0; i < rewardModules.Length && i < spawnPoints.Length; i++)
            {
                if (rewardModules[i] == null || spawnPoints[i] == null) continue;
                int id = 0;
                for (int candidate = 1; candidate <= catalog.Count; candidate++)
                    if (catalog.TryGet(candidate, out var entry) && entry.data == rewardModules[i])
                    { id = candidate; break; }
                if (id == 0 || (!spawnUnlockedRewards && unlocks.IsUnlocked(id)) || !spawned.Add(id)) continue;
                var point = spawnPoints[i];
                var crate = Instantiate(cratePrefab, point.position, point.rotation, transform);
                crate.Configure(id);
                var objective = new Objective(id, crate.transform);
                objectives.Add(objective);
                crate.TrackObjective(objective);
            }
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f);
            if (spawnPoints == null) return;
            foreach (var point in spawnPoints)
                if (point != null) Gizmos.DrawWireCube(point.position, new Vector3(1f, 0.8f, 0.75f));
        }
    }
}
