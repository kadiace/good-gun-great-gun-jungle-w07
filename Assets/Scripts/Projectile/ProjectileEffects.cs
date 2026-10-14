using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// Per-projectile snapshot of the muzzle stats supplied before activation.
    /// Every impact goes through the shared HitPayload so module effects stack the same way
    /// for bullets and flames.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileEffects : MonoBehaviour
    {
        private ProjectileStats projectileStats;
        private bool hasStats;
        private float baseDamage;
        private ProjectileStatsSource source;

        public ProjectileStats Stats => projectileStats;
        public bool HasStats => hasStats;
        public float BaseDamage => baseDamage;
        public bool IsWater => projectileStats.water > 0f;

        public void Initialize(WeaponStats stats, float projectileBaseDamage, ProjectileStatsSource statsSource)
        {
            projectileStats = ProjectileStats.FromWeapon(stats);
            baseDamage = Mathf.Max(0f, projectileBaseDamage);
            source = statsSource;
            hasStats = true;
        }

        public void ApplyPenetrationExplosion(Vector3 point, Vector3 direction, Transform owner, Transform weapon)
        {
            if (source == null || projectileStats.explosion <= 0f) return;
            WeaponStats stats = projectileStats;
            stats.gravity = 0f;
            HitPayload.Apply(new HitPayload.Context
            {
                stats = stats, source = source, point = point, direction = direction,
                applyArea = true, owner = owner, weapon = weapon
            });
        }

        public void ApplyStoppedArea(Vector3 point, Vector3 direction, TargetHealth hitTarget, Transform owner, Transform weapon)
        {
            if (source == null) return;
            HitPayload.Apply(new HitPayload.Context
            {
                stats = projectileStats, source = source, point = point, direction = direction,
                target = hitTarget, damage = 0f, applyStatus = false, applyArea = true,
                owner = owner, weapon = weapon
            });
        }

        /// <summary>One impact: damage and status on the hit target (if any), then area effects at the point.</summary>
        public void ApplyHit(Vector3 point, Vector3 direction, TargetHealth target, Transform owner, Transform weapon, bool applyArea = true)
        {
            if (source == null) return;
            HitPayload.Apply(new HitPayload.Context
            {
                stats = projectileStats,
                source = source,
                point = point,
                direction = direction,
                target = target,
                damage = baseDamage,
                applyStatus = true,
                applyArea = applyArea,
                owner = owner,
                weapon = weapon
            });
        }
    }
}
