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
        private float chargeTime;
        private bool releasingCharge;
        private ParticleSystem chargeEffect;
        private Light chargeLight;
        private float chargeLightMaxIntensity;
        private float chargeEffectMaxRate;
        // Mixed color-module tint of the projectile being configured (valid only inside ApplyStatsToProjectile).
        private bool hasPathTint;
        private Color pathTint;
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
        // ricochet, shotgun, gravity, homing). More charge modules = more stages and higher multipliers.
        public bool UsesCharge => powerCore != null && powerCore.HasChargeMuzzle;
        public float ChargeTime => chargeTime;
        public bool IsCharging => chargeTime > 0f;
        // Gauge values use the strongest charge muzzle on the weapon.
        public int MaxChargeLevel => powerCore != null ? MaxStages(powerCore.MaxMuzzleCharge) : 0;
        public int CurrentChargeLevel => Mathf.Min(MaxChargeLevel, Mathf.FloorToInt(chargeTime / Tuning.chargeSecondsPerLevel));
        public float ChargeProgress => MaxChargeLevel > 0
            ? Mathf.Clamp01(chargeTime / (Tuning.chargeSecondsPerLevel * MaxChargeLevel)) : 0f;

        private int ChargeModules(float chargeStat) => Mathf.Max(0, Mathf.FloorToInt(chargeStat));

        public int MaxStages(float chargeStat)
        {
            int modules = ChargeModules(chargeStat);
            return modules <= 0 ? 0 : Tuning.chargeBaseStages + Tuning.chargeStagesPerExtraModule * (modules - 1);
        }

        public int GetChargeLevel(float chargeStat)
            => Mathf.Min(MaxStages(chargeStat), Mathf.FloorToInt(chargeTime / Tuning.chargeSecondsPerLevel));

        /// <summary>Stage k of n charge modules: first + (k-1)·step + (n-1)·perModule, plus a bonus at the max stage.</summary>
        public float GetChargeMultiplier(float chargeStat)
        {
            int level = GetChargeLevel(chargeStat);
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

        // Random direction inside a cone; uniform over the cone's cross-section.
        private static Vector3 Scatter(Vector3 direction, float degrees)
        {
            if (degrees <= 0f) return direction;
            Vector3 side = Vector3.Cross(direction, Mathf.Abs(direction.y) > 0.99f ? Vector3.forward : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(side, direction);
            Vector2 offset = Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            return (direction + side * offset.x + up * offset.y).normalized;
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
            // Per-muzzle caps apply at release; the timer stops at the weapon's highest cap.
            float maxTime = Tuning.chargeSecondsPerLevel * MaxChargeLevel;
            if (maxTime <= 0f) return;
            bool starting = chargeTime <= 0f;
            bool wasFull = chargeTime >= maxTime;
            chargeTime = Mathf.Min(maxTime, chargeTime + Mathf.Max(0f, deltaTime));
            if (starting && chargeTime > 0f)
            {
                WorkshopSoundEffects.RaiseChargeStarted(transform.position);
                WorkshopSoundEffects.RaiseChargeLoopStarted(transform);
            }
            if (!wasFull && chargeTime >= maxTime)
                WorkshopSoundEffects.RaiseChargeFullyLoaded(transform.position);
            UpdateChargeEffect();
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

        /// <summary>Fires every charge muzzle once with the current charge, then clears it.</summary>
        public bool ReleaseCharge()
        {
            releasingCharge = true;
            bool fired = powerCore != null && powerCore.FireChargeMuzzles();
            releasingCharge = false;
            ResetCharge();
            return fired;
        }

        public void ResetCharge()
        {
            bool wasCharging = chargeTime > 0f;
            chargeTime = 0f;
            releasingCharge = false;
            StopChargeEffect();
            if (wasCharging) WorkshopSoundEffects.RaiseChargeLoopStopped(transform);
        }

        private void ApplyStatsToProjectile(GameObject projectile)
        {
            if (projectile == null) return;

            Vector3 direction = projectile.transform.forward.normalized;
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

            WeaponStats stats = powerCore != null ? powerCore.SpawningStats : weaponStats;
            // Speed modules on this muzzle's route: +100% of the base speed each (multiplicative), before charge.
            float speed = projectileSpeed * (1f + Tuning.speedBonusPerModule * Mathf.Max(0f, stats.speed));
            float damage = baseDamage;
            if (releasingCharge)
            {
                float multiplier = GetChargeMultiplier(stats.charge);
                speed *= multiplier;
                damage *= multiplier;
                stats.penetration *= multiplier;
                stats.explosion *= multiplier;
                stats.ricochet *= multiplier;
                stats.shotgun *= multiplier;
                stats.gravity *= multiplier;
                stats.homing *= multiplier;
            }

            // Spread: the whole shot (shotgun / heat pattern included) leaves in a random direction inside the cone.
            direction = Scatter(direction, powerCore != null ? powerCore.SpawningSpread : BaseSpread);

            var pelletDirections = new System.Collections.Generic.List<Vector3>();
            // Charge scales the number of radial parts before rounding; diameter 2n+1 gives (2n+1)^2 shots.
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

            // Color modules on this muzzle's route, mixed like paint; overrides the water/heat color.
            hasPathTint = TryGetProjectileTint(powerCore != null ? powerCore.SpawningPath : null, stats, out pathTint);

            // Clone before configuring the source.
            for (int i = 1; i < pelletDirections.Count; i++)
                SpawnAdditionalPellet(projectile, pelletDirections[i], stats, speed, damage);
            ConfigureProjectile(projectile, pelletDirections[0], stats, speed, damage);
            hasPathTint = false;
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

        private void ConfigureProjectile(GameObject projectile, Vector3 direction, WeaponStats stats, float speed, float damage)
        {
            FirstPersonPlayer owner = GetComponentInParent<FirstPersonPlayer>(true);
            EnergyCube legacyMovement = projectile.GetComponent<EnergyCube>();
            if (legacyMovement != null)
            {
                legacyMovement.Initialize(direction, speed,
                    owner != null ? owner.transform : null, transform);
                legacyMovement.enabled = false;
            }
            if (hasPathTint) ApplyColor(projectile, pathTint);

            ProjectileEffects effects = projectile.GetComponent<ProjectileEffects>();
            if (effects == null) effects = projectile.AddComponent<ProjectileEffects>();
            effects.Initialize(stats, damage, this);

            ProjectileMotor motor = projectile.GetComponent<ProjectileMotor>();
            if (motor == null) motor = projectile.AddComponent<ProjectileMotor>();
            motor.enabled = true;
            motor.Initialize(this, direction, speed, owner != null ? owner.transform : null, transform);
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

        private void SpawnAdditionalPellet(GameObject sourceProjectile, Vector3 direction, WeaponStats stats, float speed, float damage)
        {
            var lease = sourceProjectile.GetComponent<PooledCombatObject>();
            GameObject template = lease != null && lease.SourcePrefab != null ? lease.SourcePrefab : sourceProjectile;
            GameObject pellet = PoolManager.RentPrefab(template, sourceProjectile.transform.position,
                Quaternion.LookRotation(direction, Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f
                    ? Vector3.forward : Vector3.up));
            pellet.name = "Projectile_Shotgun";
            pellet.SetActive(false);
            ConfigureProjectile(pellet, direction, stats, speed, damage);
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
