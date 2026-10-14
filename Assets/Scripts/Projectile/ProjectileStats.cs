namespace WeaponWorkshop
{
    // Only values used by a flying projectile. Fire rate remains on the weapon/muzzle.
    public struct ProjectileStats
    {
        public float penetration, explosion, ricochet, gravity, heat, water, homing, shotgun;

        public static ProjectileStats FromWeapon(WeaponStats stats) => new ProjectileStats
        {
            penetration = stats.penetration, explosion = stats.explosion, ricochet = stats.ricochet,
            gravity = stats.gravity, heat = stats.heat,
            water = stats.water, homing = stats.homing, shotgun = stats.shotgun
        };

        // Compatibility for shared hit processing; weapon-only values remain zero.
        public static implicit operator WeaponStats(ProjectileStats stats) => new WeaponStats
        {
            penetration = stats.penetration, explosion = stats.explosion, ricochet = stats.ricochet,
            gravity = stats.gravity, heat = stats.heat,
            water = stats.water, homing = stats.homing, shotgun = stats.shotgun
        };
    }
}
