using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// Supplies per-muzzle stats to every projectile emitted by a PowerCore.
    /// weaponStats is the core base shared by every muzzle (test-scene input; StatCollector
    /// replaces it with CoreBaseStats). Modules only affect the muzzle whose route they are on.
    /// Detail values live in the ProjectileTuning asset.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerCore))]
    public sealed class ProjectileStatsSource : MonoBehaviour
    {
        [SerializeField] private PowerCore powerCore;
        [SerializeField] private WeaponStats weaponStats;
        [SerializeField] private ProjectileTuning tuning;

        [Header("Projectile baseline")]
        [SerializeField, Min(0f), Tooltip("PowerCore의 Projectile Speed와 같은 값으로 유지합니다. 현재 PowerCore는 이 값을 공개하지 않습니다.")]
        private float projectileSpeed = 10f;
        [SerializeField, Min(0f)] private float baseDamage = 10f;

        private ProjectileTuning fallbackTuning;
        private ParticleSystem chargeEffect;
        private Light chargeLight;
        private float chargeLightMaxIntensity;
        private float chargeEffectMaxRate;
        private readonly System.Collections.Generic.List<Color> pathColors = new System.Collections.Generic.List<Color>();
        private StatCollector statCollector;
        private CubeAssembly cubeAssembly;

        public WeaponStats CurrentStats => weaponStats;
        public ProjectileTuning Tuning
        {
            get
            {
                if (tuning != null) return tuning;
                if (fallbackTuning == null) fallbackTuning = ScriptableObject.CreateInstance<ProjectileTuning>();
                return fallbackTuning;
            }
        }
        public float ProjectileSpeed => projectileSpeed;
        public float BaseDamage => baseDamage;
        public float MaximumTravelDistance => Tuning.maximumTravelDistance;
        public LayerMask CollisionLayers => Tuning.collisionLayers;

        // Charge: muzzles with a charge module charge while held and fire on release.
        // The stage multiplies nearly every value of that muzzle (damage, speed, penetration, explosion,
        // ricochet, gravity, homing), but never the shotgun pellet count.
        public bool UsesCharge => powerCore != null && powerCore.HasChargeMuzzle;
        public float ChargeTime => powerCore != null ? powerCore.ChargeTime : 0f;
        public bool IsCharging => ChargeTime > 0f;
        // Locked burst muzzles do not hide the charge progress of other available muzzles.
        public int MaxChargeLevel => powerCore != null ? MaxStages(powerCore.MaxAvailableMuzzleCharge) : 0;
        public int CurrentChargeLevel => ChargeLevel(MaxChargeLevel, ChargeTime);
        public float ChargeProgress => MaxChargeLevel > 0
            ? Mathf.Clamp01(ChargeTime / ChargeDuration(MaxChargeLevel)) : 0f;

        private int ChargeModules(float chargeStat) => Mathf.Max(0, Mathf.FloorToInt(chargeStat));

        public int MaxStages(float chargeStat)
        {
            int modules = ChargeModules(chargeStat);
            return modules <= 0 ? 0 : Mathf.Max(1, Tuning.chargeBaseStages)
                + Mathf.Max(0, Tuning.chargeStagesPerExtraModule) * (modules - 1);
        }

        // Full-charge duration grows linearly with capacity; distribute it evenly over this muzzle's stages.
        // Defaults: 3 stages = 3s, each added stage adds 1/3s, so 12 stages = 6s.
        private float ChargeDuration(int stages)
        {
            if (stages <= 0) return 0f;
            int baseStages = Mathf.Max(1, Tuning.chargeBaseStages);
            return Mathf.Min(stages, baseStages) * Mathf.Max(0.01f, Tuning.chargeSecondsPerLevel)
                + Mathf.Max(0, stages - baseStages) * Mathf.Max(0.01f, Tuning.chargeSecondsPerExtraStage);
        }

        private int ChargeLevel(int stages, float seconds)
        {
            if (stages <= 0) return 0;
            float duration = ChargeDuration(stages);
            // The timer is clamped to this exact duration; ensure the last stage and its bonus are reached.
            if (seconds >= duration) return stages;
            return Mathf.Min(stages, Mathf.FloorToInt(Mathf.Max(0f, seconds) / duration * stages));
        }

        public float MaxChargeTime(float chargeStat) => ChargeDuration(MaxStages(chargeStat));

        public int GetChargeLevel(float chargeStat)
            => GetChargeLevel(chargeStat, ChargeTime);

        public int GetChargeLevel(float chargeStat, float seconds)
            => ChargeLevel(MaxStages(chargeStat), seconds);

        /// <summary>Stage k of n charge modules: first + (k-1)·step + (n-1)·perModule, plus a bonus at the max stage.</summary>
        public float GetChargeMultiplier(float chargeStat) => GetChargeMultiplier(chargeStat, ChargeTime);

        private float GetChargeMultiplier(float chargeStat, float seconds)
        {
            int level = GetChargeLevel(chargeStat, seconds);
            if (level <= 0) return 1f;
            float multiplier = Tuning.chargeFirstStageMultiplier + Tuning.chargeStageStep * (level - 1)
                               + Tuning.chargeMultiplierPerExtraModule * (ChargeModules(chargeStat) - 1);
            if (level >= MaxStages(chargeStat)) multiplier += Tuning.chargeMaxStageBonus;
            return multiplier;
        }

        // Spread (degrees). Idle = core base only; each firing round adds (spreadPerShot + route modules' Spread),
        // which PowerCore accumulates and lets recover. Heavier weapons kick less and recover faster.
        private float WeightStability
        {
            get
            {
                float weight = 0f;
                StatCollector collector = statCollector;
                if (collector != null) weight = collector.CurrentStats.weight;
                else
                    foreach (ModuleStats module in GetComponentsInChildren<ModuleStats>())
                        if (module.Data != null) weight += module.Data.GetStats().weight;
                return 1f + Tuning.spreadReductionPerWeight * Mathf.Max(0f, weight);
            }
        }

        public float BaseSpread => Mathf.Max(0f, weaponStats.spread) / WeightStability;

        // Route modules = this muzzle's Spread minus the core base already in weaponStats.
        public float SpreadPerShot(WeaponStats muzzleStats)
            => Mathf.Max(0f, Tuning.spreadPerShot + muzzleStats.spread - weaponStats.spread) / WeightStability;

        public float SpreadRecoveryPerSecond => Tuning.spreadRecoveryPerSecond * WeightStability;

        // Firing bloom cap shrinks with weight too, so heavy weapons stay tighter even under sustained fire.
        public float MaxBloom => Tuning.spreadMaxBloom / WeightStability;

        private float VerticalRecoilWeightStability
        {
            get
            {
                float weight = 0f;
                StatCollector collector = statCollector;
                if (collector != null) weight = collector.CurrentStats.weight;
                else
                    foreach (ModuleStats module in GetComponentsInChildren<ModuleStats>())
                        if (module.Data != null) weight += module.Data.GetStats().weight;
                return 1f + Tuning.verticalRecoilReductionPerWeight * Mathf.Max(0f, weight);
            }
        }

        /// <summary>One recoil impulse per emitted muzzle round, independent of its generated projectile count.</summary>
        public float VerticalRecoilForShot(WeaponStats muzzleStats, PlacedBlock[] path, bool chargeRelease)
        {
            int routeModules = 0;
            if (path != null)
                foreach (PlacedBlock block in path)
                    if (block != null && block.TryGetComponent<ModuleStats>(out var module) && module.Data != null) routeModules++;

            float recoil = Tuning.verticalRecoilDegreesPerShot
                + routeModules * Tuning.verticalRecoilDegreesPerRouteModule
                + Mathf.Max(0f, muzzleStats.penetration) * Tuning.verticalRecoilDegreesPerPenetration
                + Mathf.Max(0f, muzzleStats.explosion) * Tuning.verticalRecoilDegreesPerExplosion
                + Mathf.Max(0f, muzzleStats.shotgun) * Tuning.verticalRecoilDegreesPerShotgunPoint;
            if (muzzleStats.shotgun > 0f) recoil *= Tuning.verticalRecoilShotgunMultiplier;
            if (chargeRelease)
                recoil *= GetChargeMultiplier(muzzleStats.charge) * Tuning.verticalRecoilChargeMultiplier;
            return Mathf.Max(0f, recoil / VerticalRecoilWeightStability);
        }

        // The same muzzle-relative spread offset follows current aim throughout a charge burst.
        private static Vector3 Scatter(Vector3 direction, Vector2 offset)
        {
            Vector3 side = Vector3.Cross(direction, Mathf.Abs(direction.y) > 0.99f ? Vector3.forward : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(side, direction);
            return (direction + side * offset.x + up * offset.y).normalized;
        }

        // Value copies survive release/reset and are shared by every pellet and deferred round.
        public readonly struct ShotSnapshot
        {
            public readonly WeaponStats Stats;
            public readonly float Speed, Damage, Scale, Recoil;
            public readonly Vector2 SpreadOffset;
            public readonly bool HasTint;
            public readonly Color Tint;

            public ShotSnapshot(WeaponStats stats, float speed, float damage, float scale, float recoil,
                Vector2 spreadOffset, bool hasTint, Color tint)
            {
                Stats = stats; Speed = speed; Damage = damage; Scale = scale; Recoil = recoil;
                SpreadOffset = spreadOffset; HasTint = hasTint; Tint = tint;
            }
        }

        internal ShotSnapshot CaptureShot(WeaponStats stats, PlacedBlock[] path, float seconds,
            float spread, bool chargeRelease)
        {
            float speed = projectileSpeed * (1f + Tuning.speedBonusPerModule * Mathf.Max(0f, stats.speed));
            float damage = baseDamage;
            float scale = 1f;
            float recoil = VerticalRecoilForShot(stats, path, false);
            if (chargeRelease)
            {
                seconds = Mathf.Clamp(seconds, 0f, MaxChargeTime(stats.charge));
                float multiplier = GetChargeMultiplier(stats.charge, seconds);
                recoil *= multiplier * Tuning.verticalRecoilChargeMultiplier;
                speed *= multiplier;
                damage *= multiplier;
                stats.penetration *= multiplier;
                stats.explosion *= multiplier;
                stats.ricochet *= multiplier;
                // Shotgun count is defined solely by the route, never by charge.
                stats.gravity *= multiplier;
                stats.homing *= multiplier;
                if (GetChargeLevel(stats.charge, seconds) > 0)
                    scale += seconds * Mathf.Max(0f, Tuning.chargeScaleGrowthPerSecond);
            }
            bool hasTint = TryGetProjectileTint(path, stats, out Color tint);
            Vector2 offset = spread > 0f ? Random.insideUnitCircle * Mathf.Tan(spread * Mathf.Deg2Rad) : Vector2.zero;
            return new ShotSnapshot(stats, speed, damage, scale, recoil, offset, hasTint, tint);
        }

        public WeaponStats GetMuzzleStats(PlacedBlock[] path)
        {
            WeaponStats stats = weaponStats;
            if (stats.fireRate <= 0f && powerCore != null) stats.fireRate = powerCore.EmissionsPerSecond;
            if (path == null) return stats;
            float baseFireRate = stats.fireRate;
            float fireRateModules = 0f;
            foreach (PlacedBlock block in path)
            {
                if (block == null) continue;
                if (!block.TryGetComponent<ModuleStats>(out ModuleStats module) || module.Data == null) continue;
                WeaponStats bonus = module.Data.GetStats();
                // 연사 모듈의 연사는 곱연산용으로 따로 모음. 다른 모듈의 연사(발열 +4 등)는 그대로 더함.
                if (module.Data.ModuleType == StatType.FireRate)
                {
                    fireRateModules += bonus.fireRate;
                    bonus.fireRate = 0f;
                }
                stats.Add(bonus);
            }
            // 연사 = 코어 기본 × 배율^(연사 모듈 연사 합) + 다른 모듈의 연사. 배율 1.5면 1·1.5·2.25·3.375배.
            stats.fireRate += baseFireRate * (Mathf.Pow(Tuning.fireRateMultiplierPerModule, fireRateModules) - 1f);
            CubeAssembly assembly = cubeAssembly;
            if (stats.shotgun > 0f || ModuleCounter.TotalCount(assembly, ModuleKind.Shotgun) > 0)
            {
                stats.explosion *= 0.5f;
                stats.penetration *= 0.5f;
                stats.water *= 0.5f;
                stats.heat *= 0.5f;
            }
            return stats;
        }

        private void Awake()
        {
            TryGetComponent(out statCollector);
            cubeAssembly = GetComponent<CubeAssembly>();
            ResolvePowerCore();
        }

        private void OnEnable()
        {
            ResolvePowerCore();
            ResetCharge();
            if (Tuning.explosionEffect != null)
                PoolManager.EnsureExplosionEffectPrewarm(Tuning.explosionEffect.gameObject);
            if (powerCore != null) powerCore.ProjectileSpawned += ApplyStatsToProjectile;
        }

        private void OnDisable()
        {
            if (powerCore != null) powerCore.ProjectileSpawned -= ApplyStatsToProjectile;
            ResetCharge();
        }

        /// <summary>
        /// Entry point for StatCollector. Each projectile receives a value copy,
        /// so later recalculation cannot mutate stats on projectiles already fired.
        /// </summary>
        public void SetWeaponStats(WeaponStats stats)
        {
            weaponStats = stats;
        }

        /// <summary>Entry point for CoreBaseStats or another core-owned base damage source.</summary>
        public void SetBaseDamage(float damage)
        {
            baseDamage = Mathf.Max(0f, damage);
        }

        public void HoldCharge(float deltaTime)
        {
            bool starting = !IsCharging;
            bool wasFull = ChargeProgress >= 1f;
            if (powerCore == null || !powerCore.HoldCharge(deltaTime)) return;
            if (starting && IsCharging)
            {
                WorkshopSoundEffects.RaiseChargeStarted(transform.position);
                WorkshopSoundEffects.RaiseChargeLoopStarted(transform);
            }
            if (!wasFull && ChargeProgress >= 1f)
                WorkshopSoundEffects.RaiseChargeFullyLoaded(transform.position);
            if (IsCharging) UpdateChargeEffect();
        }

        private void UpdateChargeEffect()
        {
            if (chargeEffect == null)
            {
                if (Tuning.chargeEffect == null) return;
                chargeEffect = Instantiate(Tuning.chargeEffect, transform);
                chargeEffect.transform.localPosition = Vector3.zero;
                chargeEffect.transform.localRotation = Quaternion.identity;
                chargeEffectMaxRate = chargeEffect.emission.rateOverTime.constantMax;
                chargeLight = chargeEffect.GetComponentInChildren<Light>(true);
                chargeLightMaxIntensity = chargeLight != null ? chargeLight.intensity : 0f;
            }
            float progress = ChargeProgress;
            var emission = chargeEffect.emission;
            emission.rateOverTime = Mathf.Lerp(chargeEffectMaxRate * 0.2f, chargeEffectMaxRate, progress);
            if (chargeLight != null)
            {
                chargeLight.enabled = true;
                chargeLight.intensity = chargeLightMaxIntensity * progress;
            }
            if (!chargeEffect.gameObject.activeSelf) chargeEffect.gameObject.SetActive(true);
            if (!chargeEffect.isEmitting) chargeEffect.Play(true);
        }

        private void StopChargeEffect()
        {
            if (chargeEffect == null) return;
            chargeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (chargeLight != null) chargeLight.enabled = false;
        }

        /// <summary>Consumes available muzzle charges; deferred rounds own immutable shot snapshots.</summary>
        public bool ReleaseCharge()
        {
            bool fired = powerCore != null && powerCore.FireChargeMuzzles();
            StopChargeEffect();
            WorkshopSoundEffects.RaiseChargeLoopStopped(transform);
            return fired;
        }

        public void ResetCharge()
        {
            bool wasCharging = IsCharging;
            if (powerCore != null) powerCore.CancelCharge();
            StopChargeEffect();
            if (wasCharging) WorkshopSoundEffects.RaiseChargeLoopStopped(transform);
        }

        private void ApplyStatsToProjectile(GameObject projectile)
        {
            if (projectile == null) return;

            Vector3 direction = projectile.transform.forward.normalized;
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

            ShotSnapshot shot = powerCore != null && powerCore.SpawningShot.HasValue
                ? powerCore.SpawningShot.Value
                : CaptureShot(powerCore != null ? powerCore.SpawningStats : weaponStats,
                    powerCore != null ? powerCore.SpawningPath : null, 0f,
                    powerCore != null ? powerCore.SpawningSpread : BaseSpread, false);
            WeaponStats stats = shot.Stats;

            // Spread: the whole shot (shotgun / heat pattern included) leaves in a random direction inside the cone.
            direction = Scatter(direction, shot.SpreadOffset);

            var pelletDirections = new System.Collections.Generic.List<Vector3>();
            // Route-only shotgun count: charge grows each pellet instead of adding pellets.
            int shotgunPoints = Mathf.Max(0, Mathf.FloorToInt(stats.shotgun + 0.5f));
            int diameter = 2 * shotgunPoints + 1;
            int pellets = diameter * diameter;
            // Up-axis muzzles use the core's right axis.
            Vector3 fanAxis = Mathf.Abs(Vector3.Dot(direction, transform.up)) > 0.9f ? transform.right : transform.up;
            var shotgunDirections = new System.Collections.Generic.List<Vector3>();
            if (pellets <= 1) shotgunDirections.Add(direction);
            else
            {
                Vector3 side = Vector3.Cross(fanAxis, direction).normalized;
                Vector3 up = Vector3.Cross(direction, side).normalized;
                float spread = diameter / 3f * Mathf.Tan(Tuning.shotgunSpreadDegrees * 0.5f * Mathf.Deg2Rad);
                float phase = Random.Range(0f, 2f * Mathf.PI);
                for (int i = 0; i < pellets; i++)
                {
                    float angle = phase + i * 137.50776f * Mathf.Deg2Rad;
                    float radialOffset = Mathf.Sqrt((i + Random.Range(0.3f, 0.7f)) / pellets) * spread;
                    shotgunDirections.Add((direction + radialOffset * (Mathf.Cos(angle) * side + Mathf.Sin(angle) * up)).normalized);
                }
            }
            foreach (Vector3 pellet in shotgunDirections)
            {
                // Heat: every pellet becomes a pentagon of 5 shots (first corner up) that drift apart with distance.
                if (stats.heat > 0f) AddPolygon(pelletDirections, pellet, fanAxis, 5, Tuning.heatPentagonDegrees, 0f);
                else pelletDirections.Add(pellet);
            }

            // Clone before configuring the source.
            for (int i = 1; i < pelletDirections.Count; i++)
                SpawnAdditionalPellet(projectile, pelletDirections[i], shot);
            ConfigureProjectile(projectile, pelletDirections[0], shot);
        }

        private bool TryMixPathColor(PlacedBlock[] path, out Color color)
        {
            color = Color.white;
            pathColors.Clear();
            if (path != null)
                foreach (PlacedBlock block in path)
                {
                    if (block != null && block.TryGetComponent<ColorModule>(out ColorModule module)) pathColors.Add(module.Color);
                }
            if (pathColors.Count == 0) return false;
            color = LightMixer.Mix(pathColors);
            return true;
        }

        public bool TryGetColorModuleTint(PlacedBlock[] path, out Color color)
        {
            return TryMixPathColor(path, out color);
        }

        public bool TryGetProjectileTint(PlacedBlock[] path, WeaponStats stats, out Color color)
        {
            if (TryMixPathColor(path, out color)) return true;
            if (stats.water > 0f)
            {
                color = Tuning.waterColor;
                return true;
            }
            if (stats.heat > 0f)
            {
                color = Tuning.heatColor;
                return true;
            }
            // The untinted projectile keeps its red prefab material; mirror that color on its muzzle flash.
            color = Color.red;
            return false;
        }

        // Corners of a regular polygon on a cone around `center`. startDegrees 0 = first corner up, 90 = to the side.
        private static void AddPolygon(System.Collections.Generic.List<Vector3> directions, Vector3 center,
            Vector3 upHint, int corners, float coneDegrees, float startDegrees)
        {
            Vector3 side = Vector3.Cross(upHint, center);
            if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(Vector3.up, center);
            side.Normalize();
            Vector3 up = Vector3.Cross(center, side).normalized;
            for (int i = 0; i < corners; i++)
            {
                float corner = (startDegrees + 360f * i / corners) * Mathf.Deg2Rad;
                Vector3 tilt = Mathf.Cos(corner) * up + Mathf.Sin(corner) * side;
                directions.Add(Quaternion.AngleAxis(coneDegrees, Vector3.Cross(center, tilt)) * center);
            }
        }

        private void ConfigureProjectile(GameObject projectile, Vector3 direction, ShotSnapshot shot)
        {
            WeaponStats stats = shot.Stats;
            // Scale before the motor measures the renderer. Each pellet uses its own prefab baseline.
            if (projectile.TryGetComponent<PooledCombatObject>(out var pooled)) pooled.SetScaleMultiplier(shot.Scale);
            FirstPersonPlayer owner = GetComponentInParent<FirstPersonPlayer>(true);
            EnergyCube legacyMovement = projectile.GetComponent<EnergyCube>();
            if (legacyMovement != null)
            {
                legacyMovement.Initialize(direction, shot.Speed,
                    owner != null ? owner.transform : null, transform);
                legacyMovement.enabled = false;
            }
            if (shot.HasTint) ApplyColor(projectile, shot.Tint);

            ProjectileEffects effects = projectile.GetComponent<ProjectileEffects>();
            if (effects == null) effects = projectile.AddComponent<ProjectileEffects>();
            effects.Initialize(stats, shot.Damage, this);

            ProjectileMotor motor = projectile.GetComponent<ProjectileMotor>();
            if (motor == null) motor = projectile.AddComponent<ProjectileMotor>();
            motor.enabled = true;
            motor.Initialize(this, direction, shot.Speed, owner != null ? owner.transform : null, transform);
            // After the motor sized its hit box, so the trail never counts as the projectile body.
            if (stats.heat > 0f && stats.water <= 0f) AttachHeatTrail(projectile);
        }

        // Heat shots: orange body wrapped in a damage-free flame trail.
        private void AttachHeatTrail(GameObject projectile)
        {
            if (Tuning.heatTrailEffect == null) return;
            GameObject trail = PoolManager.RentPrefab(Tuning.heatTrailEffect.gameObject, projectile.transform.position, projectile.transform.rotation, projectile.transform);
            trail.GetComponent<PooledCombatObject>().PlayParticles();
        }

        private static void ApplyColor(GameObject projectile, Color color)
        {
            foreach (Renderer renderer in projectile.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
            }
        }

        private void SpawnAdditionalPellet(GameObject sourceProjectile, Vector3 direction, ShotSnapshot shot)
        {
            var lease = sourceProjectile.GetComponent<PooledCombatObject>();
            GameObject template = lease != null && lease.SourcePrefab != null ? lease.SourcePrefab : sourceProjectile;
            GameObject pellet = PoolManager.RentPrefab(template, sourceProjectile.transform.position,
                Quaternion.LookRotation(direction, Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f
                    ? Vector3.forward : Vector3.up));
            pellet.name = "Projectile_Shotgun";
            pellet.SetActive(false);
            ConfigureProjectile(pellet, direction, shot);
            pellet.SetActive(true);
        }

        private void ResolvePowerCore()
        {
            if (powerCore == null) powerCore = GetComponent<PowerCore>();
        }

        private void OnValidate()
        {
            ResolvePowerCore();
            projectileSpeed = Mathf.Max(0f, projectileSpeed);
            baseDamage = Mathf.Max(0f, baseDamage);
        }
    }
}
