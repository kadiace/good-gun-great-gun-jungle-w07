using UnityEngine;
using UnityEngine.Serialization;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent, RequireComponent(typeof(TargetHealth), typeof(RaidEnemyCore))]
    public sealed class EndChestCore : MonoBehaviour
    {
        [FormerlySerializedAs("chest"), SerializeField] private EndChest _chest;
        [FormerlySerializedAs("requiredGravity"), SerializeField, Min(0f)] private float _requiredGravity = 1f;
        [FormerlySerializedAs("requiredHoming"), SerializeField, Min(0f)] private float _requiredHoming = 1f;

        public float RequiredGravity => _requiredGravity;
        public float RequiredHoming => _requiredHoming;

        /// <summary>Checks whether the projectile has enough gravity to pass the shield and reach the core.</summary>
        /// <param name="stats">Projectile stat values captured when it was fired.</param>
        /// <returns>True when gravity meets the configured threshold.</returns>
        public bool CanReachCore(in ProjectileStats stats) => stats.gravity >= _requiredGravity;

        /// <summary>Checks whether the projectile meets both stat thresholds for the discovery animation.</summary>
        /// <param name="stats">Projectile stat values captured when it was fired.</param>
        /// <returns>True when gravity and homing both meet their configured thresholds.</returns>
        public bool QualifiesForDiscovery(in ProjectileStats stats) =>
            CanReachCore(stats) && stats.homing >= _requiredHoming;

        /// <summary>Allows gravity-qualified projectiles to collide with a closed chest core.</summary>
        /// <param name="stats">Projectile stat values captured when it was fired.</param>
        /// <returns>True when the chest is closed and gravity meets its threshold.</returns>
        public bool AcceptsHit(in ProjectileStats stats) => _chest != null && _chest.IsClosed && CanReachCore(stats);

        /// <summary>Checks whether a closed chest shield reflects a projectile below the gravity threshold.</summary>
        /// <param name="stats">Projectile stat values captured when it was fired.</param>
        /// <returns>True when the chest is closed and gravity is below its threshold.</returns>
        public bool Deflects(in ProjectileStats stats) => _chest != null && _chest.IsClosed && !CanReachCore(stats);

        /// <summary>Shows the Jungle-only notice after a projectile strikes the shield.</summary>
        public void NotifyShieldHit() => _chest?.NotifyBlockedHit();

        /// <summary>Shows the Jungle-only notice after a projectile is redirected at the core.</summary>
        public void NotifyRedirectedHit() => _chest?.NotifyBlockedHit();

        /// <summary>Starts discovery after a qualifying projectile hits the core.</summary>
        public void NotifyHit()
        {
            if (_chest != null) _chest.NotifyDiscoveryHit();
        }
    }
}
