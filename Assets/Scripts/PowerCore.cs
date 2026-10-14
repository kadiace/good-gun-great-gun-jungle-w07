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

        // Raised after Initialize and before activation so listeners can attach stats/effects.
        public event System.Action<GameObject> ProjectileSpawned;
        // Muzzle of the projectile being spawned; valid only during ProjectileSpawned.
        public PlacedBlock[] SpawningPath { get; private set; } = new PlacedBlock[0];
        public WeaponStats SpawningStats { get; private set; }
        // Spread cone (degrees) for the projectile being spawned; valid only during ProjectileSpawned.
        public float SpawningSpread { get; private set; }
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

        // Blocks attach as children of this object (CubeAssembly), so rebuild muzzles on change.
        private void OnTransformChildrenChanged() => muzzles = null;

        private List<(Vector3 origin, Vector3Int direction, PlacedBlock[] path)> Muzzles
        {
            get
            {
                if (muzzles != null) return muzzles;
                CubeAssembly assembly = GetComponent<CubeAssembly>();
                if (assembly != null) muzzles = assembly.GetProjectilePaths();
                else
                {
                    muzzles = new List<(Vector3, Vector3Int, PlacedBlock[])>();
                    foreach (var core in CubeAssembly.GetCoreOutputs()) muzzles.Add((core.origin, core.direction, new PlacedBlock[0]));
                }
                return muzzles;
            }
        }

        // Stats of one muzzle: core base stats + only the modules on that projectile's route.
        public WeaponStats GetMuzzleStats(PlacedBlock[] path)
        {
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
            return WeaponAssemblyStats.Calculate(GetComponent<CubeAssembly>(), stats);
        }

        // Heat muzzles fire pentagon bullets like any other muzzle (ProjectileStatsSource), so charge applies normally.
        // Water + heat on one muzzle = steam: only a steam puff at the fire rate, every other module ignored.
        private static bool IsSteam(WeaponStats stats) => stats.water > 0f && stats.heat > 0f;
        private static bool IsCharge(WeaponStats stats) => stats.charge > 0f && !IsSteam(stats);

        public bool HasChargeMuzzle => MaxMuzzleCharge > 0f;

        public float MaxMuzzleCharge
        {
            get
            {
                float max = 0f;
                foreach (var muzzle in Muzzles)
                {
                    WeaponStats stats = GetMuzzleStats(muzzle.path);
                    if (IsCharge(stats)) max = Mathf.Max(max, stats.charge);
                }
                return max;
            }
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
        public bool TryFire() => Fire(false);

        /// <summary>Fires every charge muzzle once (charge release ignores the fire-rate cooldown).</summary>
        public bool FireChargeMuzzles() => Fire(true);

        private bool Fire(bool chargeRelease)
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
                if (IsCharge(stats) != chargeRelease) continue;
                if (!chargeRelease)
                {
                    if (stats.fireRate <= 0f) continue;
                    if (lastEmissionTimes.TryGetValue(key, out double last) && now - last < 1d / stats.fireRate) continue;
                }
                lastEmissionTimes[key] = now;
                if (IsSteam(stats)) SpawnSteam(origin, direction);
                else
                {
                    // This round uses the spread built up so far, then adds its own kick.
                    SpawningSpread = MuzzleSpread(key, now);
                    Spawn(origin, direction, muzzle.path, stats, owner);
                    if (statsSource != null)
                    {
                        float recoil = statsSource.VerticalRecoilForShot(stats, muzzle.path, chargeRelease);
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
                        if (chargeRelease) WorkshopSoundEffects.RaiseChargedShotFired(soundPosition);
                        if (stats.water > 0f) WorkshopSoundEffects.RaiseWaterFired(soundPosition);
                        else if (stats.shotgun > 0f) WorkshopSoundEffects.RaiseShotgunFired(soundPosition);
                        else if (!chargeRelease) WorkshopSoundEffects.RaiseBasicShotFired(soundPosition);
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

        private void Spawn(Vector3 origin, Vector3 localDirection, PlacedBlock[] path, WeaponStats stats,
            FirstPersonPlayer owner)
        {
            SpawningPath = path;
            SpawningStats = stats;
            Vector3 direction = transform.TransformDirection(localDirection).normalized;
            GameObject projectile = PoolManager.RentPrefab(energyTemplate, transform.TransformPoint(origin), transform.rotation);
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
            if (statsSource != null) statsSource.TryGetProjectileTint(path, SpawningStats, out flashColor);
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

        private static string MuzzleKey(Vector3 origin, Vector3Int direction, PlacedBlock[] path)
        {
            var key = new System.Text.StringBuilder();
            key.Append(origin).Append(direction);
            foreach (PlacedBlock block in path) key.Append('/').Append(block != null ? block.GetInstanceID() : 0);
            return key.ToString();
        }

        public void ResetFireCooldown()
        {
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
