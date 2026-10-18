using UnityEngine;
using UnityEngine.Serialization;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class EndChestShield : MonoBehaviour
    {
        [FormerlySerializedAs("core"), SerializeField] private EndChestCore _core;

        /// <summary>Checks whether the shield reflects the supplied projectile.</summary>
        /// <param name="stats">Projectile stat values captured when it was fired.</param>
        /// <returns>True when the core is missing or the projectile is below its gravity threshold.</returns>
        public bool Deflects(in ProjectileStats stats) => _core == null || _core.Deflects(stats);

        /// <summary>Reports a projectile collision that the shield reflected.</summary>
        public void NotifyHit() => _core?.NotifyShieldHit();
    }
}
