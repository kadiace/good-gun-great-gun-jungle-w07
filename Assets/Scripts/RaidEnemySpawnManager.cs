using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class RaidEnemySpawnManager : MonoBehaviour
    {
        [SerializeField] private FirstPersonPlayer player;
        [SerializeField] private RaidHelicopter helicopterPrefab;
        [SerializeField] private bool helicopterEnabled = true;
        [SerializeField] private RaidTankTransport tankTransportPrefab;
        [SerializeField] private RaidTank reinforcedTankPrefab;
        [SerializeField] private NightRaidArea raidArea;
        private int nextDropPoint;
        private int previousTankPoint = -1, previousHelicopterPoint = -1;
        [SerializeField] private Transform spawnPoint;
        [SerializeField, Min(1f), Tooltip("경보 발생 이후 탱크 수송선과 허용된 헬기가 추가 출격하는 간격(초)")]
        private float spawnInterval = 30f;
        private bool alerted;
        [SerializeField, Min(1)] private int helicoptersPerWave = 1;
        [SerializeField, Min(1)] private int tanksPerWave = 1;
        [SerializeField, Min(1)] private int maximumHelicopters = 4;
        [SerializeField, Min(1)] private int maximumTanks = 4;
        private float spawnRemaining;
        private int waveIndex;
        private void Awake() { alerted = false; waveIndex = 0; }
        private void OnEnable() => DestructibleTarget.PartDestroyed += OnPartDestroyed;
        private void OnDisable() => DestructibleTarget.PartDestroyed -= OnPartDestroyed;
        private void OnPartDestroyed(DestructibleTarget target)
        {
            if (alerted || target == null || target.gameObject.scene != gameObject.scene ||
                RaidEnemyCore.FindForPart(target.transform) != null || player == null ||
                ((helicopterPrefab == null || !helicopterEnabled) && tankTransportPrefab == null)) return;
            if (TutorialManager.TrainingRaidPending && gameObject.scene.name == TutorialManager.TrainingScene &&
                (TutorialManager.Instance == null || !TutorialManager.Instance.TrainingTankStageReached)) return;
            // 공격과 연쇄 붕괴 알림이 겹쳐도 최초 출격은 한 번만 시작한다.
            alerted = true;
            spawnRemaining = Mathf.Max(1f, spawnInterval);
            SpawnEnemies();
        }
        private bool SpawnHelicopter(string enemyId)
        {
            if (!helicopterEnabled || helicopterPrefab == null || player == null) return false;
            Vector3 position = spawnPoint != null ? spawnPoint.position : player.transform.position + new Vector3(35f,20f,35f);
            if (raidArea != null && raidArea.TryGetEnemySpawnPoint(player.transform.position, true,
                ref previousHelicopterPoint, out Vector3 selected, out _)) position = selected;
            var enemy = Instantiate(helicopterPrefab, position, Quaternion.identity);
            enemy.Initialize(player, enemyId);
            return true;
        }
        private void SpawnEnemies()
        {
            int helicopters = Mathf.Min(helicoptersPerWave, Mathf.Max(0, maximumHelicopters - CountLiving<RaidHelicopter>()));
            int tanks = Mathf.Min(tanksPerWave, Mathf.Max(0, maximumTanks - CountLivingTanks(false)));
            var region = TableManager.Instance.RaidMap.Find(gameObject.scene.name);
            var settings = region.Settings();
            int reinforcedTanks = reinforcedTankPrefab != null ?
                Mathf.Max(0, (settings?.maximumReinforcedTanks ?? 0) - CountLivingTanks(true)) : 0;
            int spawned = 0;
            int nextWave = waveIndex + 1;
            for (int i = 0; i < helicopters; i++)
                if (SpawnHelicopter($"{gameObject.scene.name}_W{nextWave}_{spawned + 1}")) spawned++;
            for (int i = 0; i < tanks; i++)
                if (SpawnTank($"{gameObject.scene.name}_W{nextWave}_{spawned + 1}")) spawned++;
            for (int i = 0; i < reinforcedTanks; i++)
                if (SpawnTank($"{gameObject.scene.name}_W{nextWave}_{spawned + 1}", reinforcedTankPrefab)) spawned++;
            if (spawned > 0) waveIndex = nextWave;
        }
        private int CountLiving<T>() where T : Component
        {
            int count = 0;
            for (int i = 0; i < RaidEnemyCore.ActiveCount; i++)
            {
                var core = RaidEnemyCore.ActiveAt(i);
                if (core != null && !core.IsDestroyed && core.gameObject.scene == gameObject.scene &&
                    core.GetComponentInParent<T>() != null) count++;
            }
            return count;
        }
        private int CountLivingTanks(bool reinforced)
        {
            int count = 0;
            for (int i = 0; i < RaidEnemyCore.ActiveCount; i++)
            {
                var core = RaidEnemyCore.ActiveAt(i);
                if (core == null || core.IsDestroyed || core.gameObject.scene != gameObject.scene) continue;
                var tank = core.GetComponentInParent<RaidTank>();
                if (tank != null && tank.IsReinforced == reinforced) count++;
            }
            return count;
        }
        private bool SpawnTank(string enemyId, RaidTank cargoPrefabOverride = null)
        {
            if (tankTransportPrefab == null || player == null || raidArea == null) return false;
            if (!raidArea.TryGetEnemySpawnPoint(player.transform.position, false, ref previousTankPoint,
                out Vector3 point, out Vector3 outward))
                point = raidArea.GetTankDropPoint(nextDropPoint++, out outward);
            var transport = Instantiate(tankTransportPrefab);
            return transport.Initialize(player, point, outward, enemyId, cargoPrefabOverride);
        }
        /// <summary>
        /// 연습 습격 진행 중 적어도 1대의 탱크가 현장에 존재(수송 중 포함)하도록 보장합니다.
        /// </summary>
        /// <returns>이미 살아있는 탱크가 있거나 새 탱크가 성공적으로 소환되면 true, 연습 습격 조건 불충족 또는 소환 실패 시 false.</returns>
        public bool EnsureTrainingTankSpawned()
        {
            if (!TutorialManager.TrainingRaidPending || gameObject.scene.name != TutorialManager.TrainingScene)
                return false;
            if (CountLiving<RaidTank>() > 0)
                return true;
            return SpawnTank($"{gameObject.scene.name}_Training_Tank");
        }
        private void Update()
        {
            if (GameManager.GameplayBlocked) return;
            if (!alerted) return;
            spawnRemaining -= Time.deltaTime;
            if (spawnRemaining > 0f) return;
            SpawnEnemies();
            spawnRemaining = Mathf.Max(1f, spawnInterval);
        }
    }
}
