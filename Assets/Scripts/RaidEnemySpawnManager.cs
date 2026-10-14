using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class RaidEnemySpawnManager : MonoBehaviour
    {
        [SerializeField] private FirstPersonPlayer player;
        [SerializeField] private RaidHelicopter helicopterPrefab;
        [SerializeField] private bool helicopterEnabled = true;
        [SerializeField] private RaidTankTransport tankTransportPrefab;
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
        private void Awake() => alerted = false;
        private void OnEnable() => DestructibleTarget.PartDestroyed += OnPartDestroyed;
        private void OnDisable() => DestructibleTarget.PartDestroyed -= OnPartDestroyed;
        private void OnPartDestroyed(DestructibleTarget target)
        {
            if (alerted || target == null || target.gameObject.scene != gameObject.scene ||
                RaidEnemyCore.FindForPart(target.transform) != null || player == null ||
                ((helicopterPrefab == null || !helicopterEnabled) && tankTransportPrefab == null)) return;
            alerted = true;
            SpawnEnemies();
            spawnRemaining = Mathf.Max(1f, spawnInterval);
        }
        private void SpawnHelicopter()
        {
            if (!helicopterEnabled || helicopterPrefab == null || player == null) return;
            Vector3 position = spawnPoint != null ? spawnPoint.position : player.transform.position + new Vector3(35f,20f,35f);
            if (raidArea != null && raidArea.TryGetEnemySpawnPoint(player.transform.position, true,
                ref previousHelicopterPoint, out Vector3 selected, out _)) position = selected;
            var enemy = Instantiate(helicopterPrefab, position, Quaternion.identity);
            enemy.Initialize(player);
        }
        private void SpawnEnemies()
        {
            int helicopters = Mathf.Min(helicoptersPerWave, Mathf.Max(0, maximumHelicopters - CountLiving<RaidHelicopter>()));
            int tanks = Mathf.Min(tanksPerWave, Mathf.Max(0, maximumTanks - CountLiving<RaidTank>()));
            for (int i = 0; i < helicopters; i++) SpawnHelicopter();
            for (int i = 0; i < tanks; i++) SpawnTank();
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
        private void SpawnTank()
        {
            if (tankTransportPrefab == null || player == null || raidArea == null) return;
            if (!raidArea.TryGetEnemySpawnPoint(player.transform.position, false, ref previousTankPoint,
                out Vector3 point, out Vector3 outward))
                point = raidArea.GetTankDropPoint(nextDropPoint++, out outward);
            var transport = Instantiate(tankTransportPrefab);
            transport.Initialize(player, point, outward);
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
