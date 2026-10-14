using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class CrateGravity : MonoBehaviour
    {
        private void Awake()
        {
            var body = GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // Keep checking gravity when supporting floor voxels are removed.
            body.sleepThreshold = 0f;
        }
    }
}
