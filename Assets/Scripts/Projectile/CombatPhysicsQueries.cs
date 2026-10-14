using UnityEngine;
namespace WeaponWorkshop
{
    // Reusable buffers on Unity's main thread. Grow and retry instead of dropping saturated hits.
    internal static class CombatPhysicsQueries
    {
        private static Collider[] overlaps = new Collider[128];
        private static RaycastHit[] casts = new RaycastHit[64];
        public static int OverlapSphere(Vector3 center, float radius, int mask, out Collider[] results)
        {
            int count;
            while ((count = Physics.OverlapSphereNonAlloc(center, radius, overlaps, mask, QueryTriggerInteraction.Ignore)) == overlaps.Length)
                System.Array.Resize(ref overlaps, overlaps.Length * 2);
            results = overlaps;
            return count;
        }
        private static Collider[] boxOverlaps = new Collider[128];
        public static int OverlapBox(Vector3 center, Vector3 halfExtents, int mask, out Collider[] results)
        {
            int count;
            while ((count = Physics.OverlapBoxNonAlloc(center, halfExtents, boxOverlaps, Quaternion.identity,
                mask, QueryTriggerInteraction.Ignore)) == boxOverlaps.Length)
                System.Array.Resize(ref boxOverlaps, boxOverlaps.Length * 2);
            results = boxOverlaps;
            return count;
        }
        public static int BoxCast(Vector3 center, Vector3 halfExtents, Vector3 direction, Quaternion rotation,
            float distance, int mask, out RaycastHit[] results)
        {
            int count;
            while ((count = Physics.BoxCastNonAlloc(center, halfExtents, direction, casts, rotation, distance,
                mask, QueryTriggerInteraction.Ignore)) == casts.Length)
                System.Array.Resize(ref casts, casts.Length * 2);
            results = casts;
            return count;
        }
    }
}
