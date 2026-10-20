using UnityEngine;
namespace WeaponWorkshop
{
    // Reusable buffers on Unity's main thread. Grow and retry instead of dropping saturated hits.
    internal static class CombatPhysicsQueries
    {
        internal struct Hit
        {
            public Collider collider;
            public float distance;
            public Vector3 point, normal;
        }
        private static Hit[] resolvedCasts = new Hit[64];
        private static readonly System.Collections.Generic.HashSet<DestructibleTarget> refinedTargets = new();
        private static Collider[] overlaps = new Collider[128];
        // 캐스트 결과 버퍼: 가득 차면 두 배로 키우고 같은 캐스트를 다시 돌린다. 파괴 구간의 촘촘한
        // 후보 목록에서 64개 경계 재캐스트가 반복되지 않게 처음부터 넉넉히 둔다.
        private static RaycastHit[] casts = new RaycastHit[128];
        public static int OverlapSphere(Vector3 center, float radius, int mask, out Collider[] results)
        {
            PoolManager.FlushExplosionFinish();
            DestructionCollisionBatch.Flush();
            int count;
            while ((count = Physics.OverlapSphereNonAlloc(center, radius, overlaps, mask, QueryTriggerInteraction.Ignore)) == overlaps.Length)
                System.Array.Resize(ref overlaps, overlaps.Length * 2);
            results = overlaps;
            return count;
        }
        private static Collider[] boxOverlaps = new Collider[128];
        public static int OverlapBox(Vector3 center, Vector3 halfExtents, int mask, out Collider[] results)
        {
            PoolManager.FlushExplosionFinish();
            DestructionCollisionBatch.Flush();
            int count;
            while ((count = Physics.OverlapBoxNonAlloc(center, halfExtents, boxOverlaps, Quaternion.identity,
                mask, QueryTriggerInteraction.Ignore)) == boxOverlaps.Length)
                System.Array.Resize(ref boxOverlaps, boxOverlaps.Length * 2);
            results = boxOverlaps;
            return count;
        }
        public static int BoxCast(Vector3 center, Vector3 halfExtents, Vector3 direction, Quaternion rotation,
            float distance, int mask, out Hit[] results, System.Predicate<Collider> skip = null,
            System.Func<Collider, DestructibleTarget> findTarget = null)
        {
            // Broad-phase colliders remain a conservative superset until the end-of-frame batch.
            // Opt-in scenes may still hold a deferred transform sync from an earlier batch.
            DestructionCollisionBatch.Flush();
            int count;
            while ((count = Physics.BoxCastNonAlloc(center, halfExtents, direction, casts, rotation, distance,
                mask, QueryTriggerInteraction.Ignore)) == casts.Length)
                System.Array.Resize(ref casts, casts.Length * 2);
            if (resolvedCasts.Length < count) System.Array.Resize(ref resolvedCasts, casts.Length);
            refinedTargets.Clear();
            int used = 0;
            for (int i = 0; i < count; i++)
            {
                var hit = casts[i];
                if (skip != null && skip(hit.collider)) continue;
                var resolved = new Hit { collider = hit.collider, distance = hit.distance, point = hit.point, normal = hit.normal };
                var target = findTarget != null ? findTarget(hit.collider) : hit.collider.GetComponentInParent<DestructibleTarget>();
                if (target != null && target.HasPendingCellCollisions)
                {
                    if (!refinedTargets.Add(target) || !target.SweepOccupiedCells(center, rotation, halfExtents, distance,
                        out resolved.distance, out resolved.normal, out resolved.point)) continue;
                }
                resolvedCasts[used++] = resolved;
            }
            results = resolvedCasts;
            return used;
        }
    }
}
