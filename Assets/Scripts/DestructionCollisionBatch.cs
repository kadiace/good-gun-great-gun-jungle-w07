using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    // Voxel removal and collider rebuild stay immediate. Only the per-event Physics.SyncTransforms that
    // DestructibleTarget.UpdateCollisionRuns performs is coalesced, and only in scenes that opt in through
    // DestructionOptimizationSettings. Callers ask for the sync with the static RequestSync; this host
    // component flushes it before the fixed physics step and at the end of the frame. Scenes without the
    // settings object keep the original immediate sync, so their behaviour is unchanged.
    public sealed class DestructionCollisionBatch : MonoBehaviour
    {
        private static DestructionCollisionBatch host;
        private static bool pending;

        // Optional pre-query hook: callers that are about to issue a physics query can flush the
        // deferred collider changes first so the query sees the rebuilt boxes.
        public static void Flush()
        {
            if (!pending) return;
            pending = false;
            Physics.SyncTransforms();
        }

        // A DestructibleTarget rebuilt its collision boxes. Opt-in scenes defer the sync; all others
        // sync immediately exactly as before.
        public static void RequestSync(Scene scene)
        {
            if (!DestructionOptimizationSettings.EnabledFor(scene))
            {
                Physics.SyncTransforms();
                return;
            }
            if (pending) return;
            pending = true;
            EnsureHost();
        }

        private static void EnsureHost()
        {
            if (host != null) return;
            var hostObject = new GameObject("DestructionCollisionBatch");
            DontDestroyOnLoad(hostObject);
            host = hostObject.AddComponent<DestructionCollisionBatch>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            host = null;
            pending = false;
        }

        // Guarantees a pending sync reaches the physics simulation of the next fixed step.
        private void FixedUpdate() => Flush();
        // Flushes again after Update so same-frame queries in LateUpdate see current colliders.
        private void LateUpdate() => Flush();
    }
}
