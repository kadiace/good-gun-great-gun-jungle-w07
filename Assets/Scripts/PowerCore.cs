using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class PowerCore : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("기본 초당 발사 회차(코어 기본 스탯이 없을 때). 총구마다 따로 적용됩니다.")]
        private float emissionsPerSecond = 1f;
        [SerializeField, Min(0f), Tooltip("모든 출구에서 동일하게 사용하는 투사체 이동 속도(m/s).")]
        private float projectileSpeed = 10f;
        [SerializeField] private GameObject energyTemplate;
        private ProjectileStatsSource statsSource;
        private SpinRotor spinRotor;
        private readonly Dictionary<string, double> lastEmissionTimes = new Dictionary<string, double>();
        private List<(Vector3 origin, Vector3Int direction, PlacedBlock[] path)> muzzles;
        private readonly Dictionary<(Vector3, Vector3Int, PlacedBlock[]), string> muzzleKeys
            = new Dictionary<(Vector3, Vector3Int, PlacedBlock[]), string>();
        private readonly Dictionary<string, float> muzzleChargeTimes = new Dictionary<string, float>();
        private readonly HashSet<string> lockedChargeMuzzles = new HashSet<string>();
        private readonly List<ChargeBurstGroup> chargeBursts = new List<ChargeBurstGroup>();
        private Coroutine chargeBurstRoutine;
        private int chargeGeneration;

        private sealed class ChargeShot
        {
            public string Key;
            public Vector3 Origin;
            public Vector3Int Direction;
            public PlacedBlock[] Path;
            public WeaponStats BaseStats;
            public ProjectileStatsSource.ShotSnapshot Snapshot;
            public int Remaining;
            public double NextEmissionTime;
        }

        private sealed class ChargeBurstGroup
        {
            public readonly List<ChargeShot> Shots = new List<ChargeShot>();
            public float Interval;
        }

        public bool IsBursting => chargeBursts.Count > 0;
        public float ChargeTime
        {
            get
            {
                float max = 0f;
                foreach (float seconds in muzzleChargeTimes.Values) max = Mathf.Max(max, seconds);
                return max;
            }
        }

        // Raised after Initialize and before activation so listeners can attach stats/effects.
        public event System.Action<GameObject> ProjectileSpawned;
        // Muzzle of the projectile being spawned; valid only during ProjectileSpawned.
        public PlacedBlock[] SpawningPath { get; private set; } = new PlacedBlock[0];
        public WeaponStats SpawningStats { get; private set; }
        // Spread cone (degrees) for the projectile being spawned; valid only during ProjectileSpawned.
        public float SpawningSpread { get; private set; }
        public ProjectileStatsSource.ShotSnapshot? SpawningShot { get; private set; }
        private readonly Dictionary<string, (float bloom, double time)> blooms = new Dictionary<string, (float, double)>();
        public float EmissionsPerSecond
        {
            get => emissionsPerSecond;
            set => emissionsPerSecond = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            statsSource = GetComponent<ProjectileStatsSource>();
            spinRotor = GetComponent<SpinRotor>();
            if (spinRotor == null) spinRotor = gameObject.AddComponent<SpinRotor>();
        }

        // 습격 첫 탄막에서 임팩트 이펙트 수백 개를 한 프레임에 새로 만들지 않도록 무기 활성화 때 풀을 미리 채운다.
        private const int ImpactEffectPrewarm = 280;

        private void OnEnable()
        {
            if (NightRaidController.Instance == null || energyTemplate == null) return;
            if (energyTemplate.TryGetComponent(out ProjectileMotor motor) && motor.ImpactEffectPrefab != null)
                PoolManager.EnsurePrefabPrewarm(motor.ImpactEffectPrefab.gameObject, ImpactEffectPrewarm);
        }

        private void OnDisable() => CancelCharge();

        // Blocks attach as children of this object (CubeAssembly), so rebuild muzzles on change.
        private void OnTransformChildrenChanged()
            => InvalidateMuzzles();

        internal void InvalidateMuzzles()
        {
            muzzles = null;
            muzzleKeys.Clear();
        }

        private List<(Vector3 origin, Vector3Int direction, PlacedBlock[] path)> Muzzles
        {
            get
            {
                CubeAssembly assembly = GetComponent<CubeAssembly>();
                if (assembly != null) return assembly.Analysis.Paths;
                if (muzzles != null) return muzzles;
                muzzles = new List<(Vector3, Vector3Int, PlacedBlock[])>();
                foreach (var core in CubeAssembly.GetCoreOutputs()) muzzles.Add((core.origin, core.direction, new PlacedBlock[0]));
                return muzzles;
            }
        }

        // Stats of one muzzle: core base stats + only the modules on that projectile's route.
        public WeaponStats GetMuzzleStats(PlacedBlock[] path)
        {
            if (statsSource == null) TryGetComponent(out statsSource);
            if (statsSource != null) return statsSource.GetMuzzleStats(path);
            return new WeaponStats { fireRate = emissionsPerSecond };
        }

        // Workshop display uses the whole weapon; projectile stats remain route-specific.
        public WeaponStats GetCurrentStats()
        {
            StatCollector collector = GetComponent<StatCollector>();
            if (collector != null) return collector.CurrentStats;
            WeaponStats stats = statsSource != null ? statsSource.CurrentStats
                : new WeaponStats { fireRate = emissionsPerSecond };
            return TryGetComponent<CubeAssembly>(out var assembly) ? assembly.Analysis.WholeStats : stats;
        }

        // Heat muzzles fire pentagon bullets like any other muzzle (ProjectileStatsSource), so charge applies normally.
        // Water + heat on one muzzle = steam: only a steam puff at the fire rate, every other module ignored.
        private static bool IsSteam(WeaponStats stats) => stats.water > 0f && stats.heat > 0f;
        private static bool IsCharge(WeaponStats stats) => stats.charge > 0f && !IsSteam(stats);

        public bool HasChargeMuzzle => MaxMuzzleCharge > 0f;

        public float MaxMuzzleCharge => MaximumCharge(false);
        public float MaxAvailableMuzzleCharge => MaximumCharge(true);

        private float MaximumCharge(bool availableOnly)
        {
            float max = 0f;
            foreach (var muzzle in Muzzles)
            {
                if (availableOnly && lockedChargeMuzzles.Contains(MuzzleKey(muzzle.origin, muzzle.direction, muzzle.path))) continue;
                WeaponStats stats = GetMuzzleStats(muzzle.path);
                if (IsCharge(stats)) max = Mathf.Max(max, stats.charge);
            }
            return max;
        }

        // Widest current spread among muzzles that shoot projectiles (crosshair size). Steam muzzles have no shots.
        public float MaxSpreadDegrees
        {
            get
            {
                if (statsSource == null) return 0f;
                double now = Time.timeAsDouble;
                float max = 0f;
                foreach (var muzzle in Muzzles)
                {
                    WeaponStats stats = GetMuzzleStats(muzzle.path);
                    if (!IsSteam(stats))
                        max = Mathf.Max(max, MuzzleSpread(MuzzleKey(muzzle.origin, muzzle.direction, muzzle.path), now));
                }
                return max;
            }
        }

        public float MaximumVerticalRecoilPerShotDegrees
        {
            get
            {
                if (statsSource == null) return 0f;
                float max = 0f;
                foreach (var muzzle in Muzzles)
                {
                    WeaponStats stats = GetMuzzleStats(muzzle.path);
                    if (!IsSteam(stats))
                        max = Mathf.Max(max, statsSource.VerticalRecoilForShot(stats, muzzle.path, false));
                }
                return max;
            }
        }

        // Bloom: each firing round of a muzzle widens its spread; after a short pause it shrinks back quickly.
        private float CurrentBloom(string key, double now)
        {
            if (statsSource == null || !blooms.TryGetValue(key, out var state)) return 0f;
            float idle = (float)(now - state.time) - statsSource.Tuning.spreadRecoveryDelay;
            return idle <= 0f ? state.bloom : Mathf.Max(0f, state.bloom - statsSource.SpreadRecoveryPerSecond * idle);
        }

        private float MuzzleSpread(string key, double now) => statsSource == null ? 0f
            : Mathf.Min(statsSource.BaseSpread + CurrentBloom(key, now), statsSource.Tuning.spreadMaxDegrees);

        /// <summary>Fires every non-charge muzzle whose own cooldown has elapsed.</summary>
        public bool TryFire() => Fire();

        public bool HoldCharge(float deltaTime)
        {
            if (!CanEmit || statsSource == null) return false;
            bool available = false;
            foreach (var muzzle in Muzzles)
            {
                string key = MuzzleKey(muzzle.origin, muzzle.direction, muzzle.path);
                if (lockedChargeMuzzles.Contains(key)) continue;
                WeaponStats stats = GetMuzzleStats(muzzle.path);
                if (!IsCharge(stats)) continue;
                muzzleChargeTimes.TryGetValue(key, out float seconds);
                // Keep zero-second presses too: a very quick tap still emits the basic pattern once.
                muzzleChargeTimes[key] = Mathf.Min(statsSource.MaxChargeTime(stats.charge), seconds + Mathf.Max(0f, deltaTime));
                available = true;
            }
            return available;
        }

        private bool CanEmit => !GameManager.GameplayBlocked && isActiveAndEnabled
            && energyTemplate != null && projectileSpeed > 0f;

        private static int FireRateModuleCount(PlacedBlock[] path)
        {
            int count = 0;
            foreach (PlacedBlock block in path)
                if (block != null && block.TryGetComponent<ModuleStats>(out var module)
                    && module.Data != null && module.Data.ModuleType == StatType.FireRate) count++;
            return count;
        }

        /// <summary>Emits ready charge paths now and schedules their remaining rounds from value snapshots.</summary>
        public bool FireChargeMuzzles()
        {
            if (!CanEmit || statsSource == null) return false;
            int generation = chargeGeneration;
            double now = Time.timeAsDouble;
            var shots = new List<ChargeShot>();
            var group = new ChargeBurstGroup { Interval = Mathf.Max(0.001f, statsSource.Tuning.chargeBurstInterval) };
            foreach (var muzzle in Muzzles)
            {
                string key = MuzzleKey(muzzle.origin, muzzle.direction, muzzle.path);
                if (lockedChargeMuzzles.Contains(key) || !muzzleChargeTimes.TryGetValue(key, out float seconds)) continue;
                muzzleChargeTimes.Remove(key);
                WeaponStats stats = GetMuzzleStats(muzzle.path);
                if (!IsCharge(stats)) continue;
                var shot = new ChargeShot
                {
                    Key = key, Origin = muzzle.origin, Direction = muzzle.direction,
                    Path = (PlacedBlock[])muzzle.path.Clone(), BaseStats = stats,
                    Snapshot = statsSource.CaptureShot(stats, muzzle.path, seconds, MuzzleSpread(key, now), true),
                    Remaining = statsSource.GetChargeLevel(stats.charge, seconds) > 0 ? 1 + FireRateModuleCount(muzzle.path) : 1
                };
                shots.Add(shot);
                if (shot.Remaining > 1)
                {
                    group.Shots.Add(shot);
                }
            }
            // Install all locks before spawning; spawn listeners may call back into weapon input.
            if (group.Shots.Count > 0)
            {
                foreach (ChargeShot shot in group.Shots) lockedChargeMuzzles.Add(shot.Key);
                chargeBursts.Add(group);
            }
            bool fired = false, soundRaised = false;
            float recoilSquared = 0f;
            FirstPersonPlayer owner = GetComponentInParent<FirstPersonPlayer>();
            try
            {
                foreach (ChargeShot shot in shots)
                {
                    if (generation != chargeGeneration) break;
                    if (!EmitChargeShot(shot, owner)) { shot.Remaining = 0; continue; }
                    fired = true;
                    shot.Remaining--;
                    shot.NextEmissionTime = Time.timeAsDouble + group.Interval;
                    lastEmissionTimes[shot.Key] = now;
                    recoilSquared += shot.Snapshot.Recoil * shot.Snapshot.Recoil;
                    blooms[shot.Key] = (Mathf.Min(CurrentBloom(shot.Key, now) + statsSource.SpreadPerShot(shot.BaseStats),
                        statsSource.MaxBloom), now);
                    if (!soundRaised)
                    {
                        RaiseShotSound(shot.BaseStats, transform.TransformPoint(shot.Origin), true);
                        soundRaised = true;
                    }
                }
                if (generation == chargeGeneration)
                {
                    ReleaseCompletedBurstGroups();
                    if (IsBursting && chargeBurstRoutine == null) chargeBurstRoutine = StartCoroutine(ChargeBurstRoutine());
                }
                if (fired && owner != null)
                    owner.AddVerticalRecoil(Mathf.Sqrt(recoilSquared), statsSource.Tuning.verticalRecoilMaximumDegrees,
                        statsSource.Tuning.verticalRecoilSmoothTime);
                return fired;
            }
            catch
            {
                CancelCharge();
                throw;
            }
        }

        private bool EmitChargeShot(ChargeShot shot, FirstPersonPlayer owner)
        {
            if (!CanEmit || statsSource == null || !statsSource.isActiveAndEnabled) return false;
            bool exists = false;
            foreach (var muzzle in Muzzles)
                if (MuzzleKey(muzzle.origin, muzzle.direction, muzzle.path) == shot.Key) { exists = true; break; }
            if (!exists) return false;
            Vector3 origin = shot.Origin, direction = shot.Direction;
            if (spinRotor != null) spinRotor.TransformMuzzle(shot.Path, ref origin, ref direction);
            return Spawn(origin, direction, shot.Path, shot.BaseStats, owner, shot.Snapshot);
        }

        private IEnumerator ChargeBurstRoutine()
        {
            int generation = chargeGeneration;
            while (IsBursting)
            {
                // At most one round per path per frame; hitch recovery must not dump a burst at once.
                yield return null;
                if (generation != chargeGeneration) yield break;
                if (GameManager.GameplayBlocked || Time.deltaTime <= 0f) continue;
                FirstPersonPlayer owner = GetComponentInParent<FirstPersonPlayer>();
                for (int i = 0; i < chargeBursts.Count; i++)
                {
                    ChargeBurstGroup group = chargeBursts[i];
                    foreach (ChargeShot shot in group.Shots)
                    {
                        if (shot.Remaining <= 0 || Time.timeAsDouble < shot.NextEmissionTime) continue;
                        bool emitted = false;
                        try { emitted = EmitChargeShot(shot, owner); }
                        catch (System.Exception exception) { Debug.LogException(exception, this); }
                        if (generation != chargeGeneration) yield break;
                        if (emitted)
                        {
                            shot.Remaining--;
                            shot.NextEmissionTime = Time.timeAsDouble + group.Interval;
                        }
                        else shot.Remaining = 0;
                    }
                }
                ReleaseCompletedBurstGroups();
            }
            chargeBurstRoutine = null;
        }

        private void ReleaseCompletedBurstGroups()
        {
            for (int i = chargeBursts.Count - 1; i >= 0; i--)
            {
                ChargeBurstGroup group = chargeBursts[i];
                bool complete = true;
                foreach (ChargeShot shot in group.Shots)
                    if (shot.Remaining > 0) { complete = false; break; }
                if (!complete) continue;
                foreach (ChargeShot shot in group.Shots) lockedChargeMuzzles.Remove(shot.Key);
                chargeBursts.RemoveAt(i);
            }
        }

        public void CancelCharge()
        {
            chargeGeneration++;
            if (chargeBurstRoutine != null) StopCoroutine(chargeBurstRoutine);
            chargeBurstRoutine = null;
            chargeBursts.Clear();
            lockedChargeMuzzles.Clear();
            muzzleChargeTimes.Clear();
        }

        private static void RaiseShotSound(WeaponStats stats, Vector3 position, bool charged)
        {
            if (charged) WorkshopSoundEffects.RaiseChargedShotFired(position);
            if (stats.water > 0f) WorkshopSoundEffects.RaiseWaterFired(position);
            else if (stats.shotgun > 0f) WorkshopSoundEffects.RaiseShotgunFired(position);
            else if (!charged) WorkshopSoundEffects.RaiseBasicShotFired(position);
        }

        private bool Fire()
        {
            if (GameManager.GameplayBlocked || !isActiveAndEnabled || energyTemplate == null || projectileSpeed <= 0f)
                return false;
            double now = Time.timeAsDouble;
            FirstPersonPlayer owner = GetComponentInParent<FirstPersonPlayer>();
            bool fired = false;
            bool soundRaised = false;
            float recoilMagnitudeSquared = 0f;
            int recoilingMuzzles = 0;
            foreach (var muzzle in Muzzles)
            {
                WeaponStats stats = GetMuzzleStats(muzzle.path);
                string key = MuzzleKey(muzzle.origin, muzzle.direction, muzzle.path);
                // Spin modules turn the barrel: muzzles after them leave from a rotated position/direction.
                Vector3 origin = muzzle.origin;
                Vector3 direction = muzzle.direction;
                if (spinRotor != null) spinRotor.TransformMuzzle(muzzle.path, ref origin, ref direction);
                if (IsCharge(stats) || lockedChargeMuzzles.Contains(key)) continue;
                if (stats.fireRate <= 0f) continue;
                if (lastEmissionTimes.TryGetValue(key, out double last) && now - last < 1d / stats.fireRate) continue;
                lastEmissionTimes[key] = now;
                if (IsSteam(stats)) SpawnSteam(origin, direction);
                else
                {
                    // This round uses the spread built up so far, then adds its own kick.
                    SpawningSpread = MuzzleSpread(key, now);
                    if (!Spawn(origin, direction, muzzle.path, stats, owner)) continue;
                    if (statsSource != null)
                    {
                        float recoil = statsSource.VerticalRecoilForShot(stats, muzzle.path, false);
                        recoilMagnitudeSquared += recoil * recoil;
                        recoilingMuzzles++;
                        blooms[key] = (Mathf.Min(CurrentBloom(key, now) + statsSource.SpreadPerShot(stats),
                            statsSource.MaxBloom), now);
                    }
                }
                if (!soundRaised)
                {
                    Vector3 soundPosition = transform.TransformPoint(origin);
                    if (IsSteam(stats))
                    {
                        WorkshopSoundEffects.RaiseWaterFired(soundPosition);
                    }
                    else
                    {
                        RaiseShotSound(stats, soundPosition, false);
                    }
                    soundRaised = true;
                }
                fired = true;
            }
            if (owner != null && statsSource != null && recoilingMuzzles > 0)
                owner.AddVerticalRecoil(Mathf.Sqrt(recoilMagnitudeSquared), statsSource.Tuning.verticalRecoilMaximumDegrees,
                    statsSource.Tuning.verticalRecoilSmoothTime);
            SpawningPath = new PlacedBlock[0];
            SpawningStats = default;
            SpawningSpread = 0f;
            return fired;
        }

        private bool Spawn(Vector3 origin, Vector3 localDirection, PlacedBlock[] path, WeaponStats stats,
            FirstPersonPlayer owner, ProjectileStatsSource.ShotSnapshot? shot = null)
        {
            SpawningPath = path;
            SpawningStats = stats;
            SpawningShot = shot;
            Vector3 direction = transform.TransformDirection(localDirection).normalized;
            GameObject projectile = null;
            try
            {
                projectile = PoolManager.RentPrefab(energyTemplate, transform.TransformPoint(origin), transform.rotation);
                if (projectile == null) return false;
                projectile.name = "Projectile";
                EnergyCube movement = projectile.GetComponent<EnergyCube>();
                if (movement == null) movement = projectile.AddComponent<EnergyCube>();
                movement.enabled = true;
                var pooledMotor = projectile.GetComponent<ProjectileMotor>();
                if (pooledMotor != null) pooledMotor.enabled = false;
                movement.Initialize(direction, projectileSpeed, owner != null ? owner.transform : null, transform);
                ProjectileSpawned?.Invoke(projectile);
                SpawnMuzzleFlash(origin, direction, path);
                projectile.SetActive(true);
                return true;
            }
            catch
            {
                if (projectile != null) PoolManager.Release(projectile);
                throw;
            }
            finally
            {
                SpawningPath = System.Array.Empty<PlacedBlock>();
                SpawningStats = default;
                SpawningSpread = 0f;
                SpawningShot = null;
            }
        }

        private void SpawnMuzzleFlash(Vector3 origin, Vector3 direction, PlacedBlock[] path)
        {
            ParticleSystem prefab = statsSource != null ? statsSource.Tuning.muzzleFlashEffect : null;
            if (prefab == null) return;

            Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            Vector3 position = transform.TransformPoint(origin) + direction * 0.02f;
            GameObject flash = PoolManager.RentPrefab(prefab.gameObject, position, Quaternion.LookRotation(direction, up));
            if (flash == null) return;

            Color flashColor = Color.white;
            if (SpawningShot.HasValue) flashColor = SpawningShot.Value.Tint;
            else if (statsSource != null) statsSource.TryGetProjectileTint(path, SpawningStats, out flashColor);
            foreach (ParticleSystem particles in flash.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                main.startColor = flashColor;
            }
            flash.GetComponent<PooledCombatObject>()?.PlayParticles(true, 0.5f);
        }

        // Visual only: no projectile, no damage, no collision.
        private void SpawnSteam(Vector3 origin, Vector3 localDirection)
        {
            ParticleSystem prefab = statsSource != null ? statsSource.Tuning.steamEffect : null;
            if (prefab == null) return;
            Vector3 direction = transform.TransformDirection(localDirection).normalized;
            GameObject puff = PoolManager.RentPrefab(prefab.gameObject, transform.TransformPoint(origin), Quaternion.LookRotation(direction,
                Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up));
            puff.GetComponent<PooledCombatObject>().PlayParticles(true);
        }

        private string MuzzleKey(Vector3 origin, Vector3Int direction, PlacedBlock[] path)
        {
            var identity = (origin, direction, path);
            if (muzzleKeys.TryGetValue(identity, out string cached)) return cached;
            var key = new System.Text.StringBuilder();
            key.Append(origin).Append(direction);
            foreach (PlacedBlock block in path) key.Append('/').Append(block != null ? block.GetInstanceID() : 0);
            string value = key.ToString();
            muzzleKeys.Add(identity, value);
            return value;
        }

        public void ResetFireCooldown()
        {
            if (statsSource != null) statsSource.ResetCharge();
            else CancelCharge();
            lastEmissionTimes.Clear();
            blooms.Clear();
        }

        private void OnValidate()
        {
            emissionsPerSecond = Mathf.Max(0f, emissionsPerSecond);
            projectileSpeed = Mathf.Max(0f, projectileSpeed);
        }
    }
}
