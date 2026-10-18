using UnityEngine;

namespace WeaponWorkshop
{
    // Enemy sight and projectile casts. Hits come back nearest first, so callers inspect only
    // the few closest colliders instead of every block the ray passes through.
    internal static class RaidPhysicsQueries
    {
        // 엔드 상자는 적 투사체·시선 판정에서 제외한다.
        private static int Mask => Physics.DefaultRaycastLayers & ~LayerMask.GetMask("EndChest", "EndChestShield");
        private static RaycastHit[] hits = new RaycastHit[256];
        private static float[] distances = new float[256];

        public static int Raycast(Vector3 origin, Vector3 direction, float distance, out RaycastHit[] sorted)
        {
            PoolManager.FlushExplosionFinish();
            DestructionCollisionBatch.Flush();
            int count;
            while ((count = Physics.RaycastNonAlloc(origin, direction, hits, distance,
                Mask, QueryTriggerInteraction.Ignore)) == hits.Length) Grow();
            return Sort(count, out sorted);
        }

        public static int SphereCast(Vector3 origin, float radius, Vector3 direction, float distance, out RaycastHit[] sorted)
        {
            PoolManager.FlushExplosionFinish();
            DestructionCollisionBatch.Flush();
            int count;
            while ((count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, distance,
                Mask, QueryTriggerInteraction.Ignore)) == hits.Length) Grow();
            return Sort(count, out sorted);
        }

        private static int Sort(int count, out RaycastHit[] sorted)
        {
            for (int i = 0; i < count; i++) distances[i] = hits[i].distance;
            System.Array.Sort(distances, hits, 0, count);
            sorted = hits;
            return count;
        }

        private static void Grow()
        {
            hits = new RaycastHit[hits.Length * 2];
            distances = new float[hits.Length];
        }
    }
}
