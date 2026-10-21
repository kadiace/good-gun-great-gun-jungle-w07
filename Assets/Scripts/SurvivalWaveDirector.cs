using System;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 생존 모드에서 씬 시작 즉시 타이머를 시작하여 60초 후 첫 적을 출격시키고,
    /// 45초에서 15초까지 출격 주기를 점진 가속하며 단계별로 헬기와 강화 탱크를 해금하는 디렉터 컴포넌트입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurvivalWaveDirector : MonoBehaviour
    {
        private const float DEFAULT_FIRST_SPAWN_TIME = 60f;
        private const float DEFAULT_INITIAL_INTERVAL = 45f;
        private const float DEFAULT_MIN_INTERVAL = 15f;

        [Header("플레이어 및 적 프리팹 참조")]
        [SerializeField] private FirstPersonPlayer _player;
        [SerializeField] private RaidTank _tankPrefab;
        [SerializeField] private RaidTank _reinforcedTankPrefab;
        [SerializeField] private RaidHelicopter _helicopterPrefab;
        [SerializeField] private RaidTankTransport _tankTransportPrefab;
        [SerializeField] private NightRaidArea _raidArea;

        [Header("출격 타이머 및 가속 설정")]
        [SerializeField, Min(1f)] private float _firstSpawnTime = DEFAULT_FIRST_SPAWN_TIME;
        [SerializeField, Min(1f)] private float _initialSpawnInterval = DEFAULT_INITIAL_INTERVAL;
        [SerializeField, Min(1f)] private float _minSpawnInterval = DEFAULT_MIN_INTERVAL;
        [SerializeField, Min(0.1f)] private float _intervalReductionPerMinute = 3f;

        [Header("적 해금 시간 설정")]
        [SerializeField, Min(1f)] private float _helicopterUnlockTime = 180f;
        [SerializeField, Min(1f)] private float _reinforcedTankUnlockTime = 300f;

        [Header("동시 생존 상한 설정")]
        [SerializeField, Min(1)] private int _maxStandardTanks = 4;
        [SerializeField, Min(1)] private int _maxHelicopters = 2;
        [SerializeField, Min(1)] private int _maxReinforcedTanks = 1;

        private float _elapsedTime;
        private float _nextSpawnTimer;
        private bool _firstWaveSpawned;
        private int _waveIndex;
        private int _previousTankPoint = -1;
        private int _previousHelicopterPoint = -1;

        public float ElapsedTime => _elapsedTime;
        public float CurrentSpawnInterval => CalculateCurrentInterval();
        public int WaveIndex => _waveIndex;

        void Awake()
        {
            _elapsedTime = 0f;
            _nextSpawnTimer = _firstSpawnTime;
            _firstWaveSpawned = false;
            _waveIndex = 0;
            CachePlayer();
        }

        void Start()
        {
            CachePlayer();
        }

        void Update()
        {
            if (GameManager.GameplayBlocked) return;
            if (_player == null)
            {
                CachePlayer();
                if (_player == null) return;
            }

            _elapsedTime += Time.deltaTime;

            if (!_firstWaveSpawned)
            {
                if (_elapsedTime >= _firstSpawnTime)
                {
                    _firstWaveSpawned = true;
                    SpawnWave();
                    _nextSpawnTimer = CalculateCurrentInterval();
                }
                return;
            }

            _nextSpawnTimer -= Time.deltaTime;
            if (_nextSpawnTimer <= 0f)
            {
                SpawnWave();
                _nextSpawnTimer = CalculateCurrentInterval();
            }
        }

        /// <summary>
        /// 씬에 존재하는 FirstPersonPlayer 컴포넌트를 탐색하여 캐시합니다.
        /// </summary>
        private void CachePlayer()
        {
            if (_player != null) return;
            _player = FindAnyObjectByType<FirstPersonPlayer>();
        }

        /// <summary>
        /// 경과 시간에 따라 1분당 지정된 초만큼 단축된 현재 출격 주기를 계산합니다.
        /// </summary>
        /// <returns>하한선 이상으로 제한된 출격 주기(초)</returns>
        public float CalculateCurrentInterval()
        {
            if (!_firstWaveSpawned) return _firstSpawnTime;

            float minutesAfterFirstSpawn = Mathf.Max(0f, (_elapsedTime - _firstSpawnTime) / 60f);
            float currentInterval = _initialSpawnInterval - (minutesAfterFirstSpawn * _intervalReductionPerMinute);
            return Mathf.Max(_minSpawnInterval, currentInterval);
        }

        /// <summary>
        /// 현재 해금 시간과 동시 생존 상한을 검사하여 주기당 최대 1기의 적(강화 탱크, 헬기, 일반 탱크 중 택1)을 출격시킵니다.
        /// </summary>
        public void SpawnWave()
        {
            CachePlayer();
            if (_player == null) return;

            int livingStandardTanks = CountLivingTanks(false);
            int livingReinforcedTanks = CountLivingTanks(true);
            int livingHelicopters = CountLivingHelicopters();

            bool allowHelicopter = _elapsedTime >= _helicopterUnlockTime;
            bool allowReinforcedTank = _elapsedTime >= _reinforcedTankUnlockTime;

            _waveIndex++;

            // 주기당 최대 1기만 출격합니다.
            // 1. 강화 탱크 해금 조건 및 생존 상한 충족 시 확률 출격
            if (allowReinforcedTank && livingReinforcedTanks < _maxReinforcedTanks && UnityEngine.Random.value < 0.4f)
            {
                if (SpawnTank(true)) return;
            }

            // 2. 헬기 해금 조건 및 생존 상한 충족 시 확률 출격
            if (allowHelicopter && livingHelicopters < _maxHelicopters && UnityEngine.Random.value < 0.5f)
            {
                if (SpawnHelicopter()) return;
            }

            // 3. 일반 탱크 생존 상한 충족 시 출격
            if (livingStandardTanks < _maxStandardTanks)
            {
                if (SpawnTank(false)) return;
            }

            // 폴백: 위 확률 추첨에서 모두 미출격 시 상한 여유가 있는 종류를 순차 시도해 1기 출격
            if (allowHelicopter && livingHelicopters < _maxHelicopters)
            {
                if (SpawnHelicopter()) return;
            }

            if (allowReinforcedTank && livingReinforcedTanks < _maxReinforcedTanks)
            {
                SpawnTank(true);
            }
        }

        /// <summary>
        /// 씬에 생존해 있는 일반 탱크 또는 강화 탱크의 수를 집계합니다.
        /// </summary>
        /// <param name="reinforced">강화 탱크 여부</param>
        /// <returns>생존 탱크 수</returns>
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

        /// <summary>
        /// 씬에 생존해 있는 공격 헬기의 수를 집계합니다.
        /// </summary>
        /// <returns>생존 헬기 수</returns>
        private int CountLivingHelicopters()
        {
            int count = 0;
            for (int i = 0; i < RaidEnemyCore.ActiveCount; i++)
            {
                var core = RaidEnemyCore.ActiveAt(i);
                if (core == null || core.IsDestroyed || core.gameObject.scene != gameObject.scene) continue;
                var heli = core.GetComponentInParent<RaidHelicopter>();
                if (heli != null) count++;
            }
            return count;
        }

        /// <summary>
        /// 일반 또는 강화 탱크를 수송선 또는 직접 스폰 방식으로 생성합니다.
        /// </summary>
        /// <param name="reinforced">강화 탱크 여부</param>
        /// <returns>출격 성공 여부</returns>
        private bool SpawnTank(bool reinforced)
        {
            if (_player == null) return false;

            RaidTank cargoPrefab = reinforced ? _reinforcedTankPrefab : _tankPrefab;
            string enemyId = $"Survival_W{_waveIndex}_Tank_{(reinforced ? "Reinf" : "Std")}_{Time.time:F0}";

            if (_tankTransportPrefab != null && _raidArea != null)
            {
                if (_raidArea.TryGetEnemySpawnPoint(_player.transform.position, false, ref _previousTankPoint,
                    out Vector3 point, out Vector3 outward))
                {
                    var transport = Instantiate(_tankTransportPrefab);
                    return transport.Initialize(_player, point, outward, enemyId, cargoPrefab);
                }
            }

            Vector3 spawnPos = CalculateGroundSpawnPoint();
            if (cargoPrefab != null)
            {
                var tank = Instantiate(cargoPrefab, spawnPos, Quaternion.identity);
                tank.Initialize(_player, enemyId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 공격 헬기를 공중 지점에 생성합니다.
        /// </summary>
        /// <returns>출격 성공 여부</returns>
        private bool SpawnHelicopter()
        {
            if (_helicopterPrefab == null || _player == null) return false;

            string enemyId = $"Survival_W{_waveIndex}_Heli_{Time.time:F0}";
            Vector3 spawnPos;

            if (_raidArea != null && _raidArea.TryGetEnemySpawnPoint(_player.transform.position, true,
                ref _previousHelicopterPoint, out Vector3 selectedPoint, out _))
            {
                spawnPos = selectedPoint;
            }
            else
            {
                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(45f, 70f);
                spawnPos = _player.transform.position + new Vector3(randomCircle.x, 25f, randomCircle.y);
            }

            var heli = Instantiate(_helicopterPrefab, spawnPos, Quaternion.identity);
            heli.Initialize(_player, enemyId);
            return true;
        }

        /// <summary>
        /// 플레이어 주변 안전 거리의 지면 스폰 좌표를 계산합니다.
        /// </summary>
        /// <returns>지면 스폰 월드 좌표</returns>
        private Vector3 CalculateGroundSpawnPoint()
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(40f, 65f);
            Vector3 targetPos = _player.transform.position + new Vector3(offset.x, 20f, offset.y);

            if (Physics.Raycast(targetPos, Vector3.down, out RaycastHit hit, 50f))
            {
                return hit.point + Vector3.up * 0.5f;
            }

            return _player.transform.position + new Vector3(offset.x, 0.5f, offset.y);
        }
    }
}
