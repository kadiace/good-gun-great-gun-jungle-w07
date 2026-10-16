using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 紐⑤뱺 怨듦꺽(??李⑺깂, ?붿뿼 ?? ?붿뿼 ??컻)??怨듭쑀?섎뒗 ?쇨꺽 泥섎━.
    // 紐⑤뱢 議고빀???곕줈 ?뺤쓽?섏? ?딄퀬, 留욎쓣 ?뚮쭏??媛숈? ?쒖꽌濡??④낵媛 ?볦씤??
    //   ?쇳빐(臾쇱씠硫?0) ???띿꽦(臾??뚰솕) ??踰붿쐞 ?④낵(??컻 ??以묐젰??. 臾?諛쒖뿴? ?섏쬆湲?珥앷뎄???꾩씠 ?놁쓬.
    // ??컻???섎쭚由??쒖쟻???쇳빐쨌?띿꽦??諛쏆?留???컻쨌以묐젰?μ쓣 ?ㅼ떆 留뚮뱾吏???딅뒗??
    public static class HitPayload
    {
        public struct Context
        {
            public WeaponStats stats;
            public ProjectileStatsSource source;
            public ProjectileMotor projectile;
            public Vector3 point;
            public Vector3 direction;
            public TargetHealth target;
            public float damage;
            public bool applyStatus;
            public bool applyArea;
            public Transform owner;
            public Transform weapon;
        }

        public static void Apply(in Context context)
        {
            if (context.source == null) return;
            if (context.target != null && !context.target.IsDead)
                ApplyToTarget(context.target, context.stats, context.source.Tuning, context.damage,
                    context.direction, context.applyStatus);
            if (!context.applyArea) return;
            if (context.stats.explosion > 0f) Explode(context);
            if (context.stats.gravity > 0f) CreateGravityField(context);
        }

        public static void ApplyToTarget(TargetHealth target, WeaponStats stats, ProjectileTuning tuning,
            float damage, Vector3 direction, bool applyStatus)
        {
            bool water = stats.water > 0f;
            if (applyStatus || damage > 0f) target.ApplyImpact(water ? 0f : damage);
            if (!applyStatus || target.IsDead) return;
            if (water) target.Extinguish();
            // Heat no longer burns (heat = pentagon bullets + fire rate); TargetHealth.AddBurn is kept unused.
            // Water + heat = steam muzzle: PowerCore fires only a steam puff, so no projectile reaches here.
        }

        private static void Explode(in Context context)
        {
            ProjectileTuning tuning = context.source.Tuning;
            float radius = tuning.explosionRadiusPerPoint * Mathf.Pow(context.stats.explosion, 1f / 3f);
            if (radius <= 0f) return;
            WorkshopSoundEffects.RaiseExplosionTriggered(context.point);
            ExpandingExplosion.Create(context.point, radius, context.stats, context.source, context.owner, context.weapon, context.projectile);
        }

        // Gravity pulls nearby targets/physics objects toward the hit; penetration makes it stronger.
        private static void CreateGravityField(in Context context)
        {
            ProjectileTuning tuning = context.source.Tuning;
            float pull = tuning.gravityPullPerPoint * context.stats.gravity *
                         (1f + tuning.gravityPenetrationBonus * Mathf.Max(0f, context.stats.penetration));
            // Sphere volume scales linearly with gravity strength.
            float radius = tuning.gravityRadiusPerPoint * Mathf.Pow(context.stats.gravity, 1f / 3f);
            GravityField.Create(context.point, radius, pull,
                tuning.gravityDuration, context.source.CollisionLayers, context.owner, context.weapon,
                tuning.gravityEffect, tuning.gravityDistortionPrefab);
        }
    }
}
