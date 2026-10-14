using System.Collections.Generic;
using UnityEngine;
namespace WeaponWorkshop
{
    // One rigidbody per target. Stationary targets sleep, but are never permanently frozen.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FallingRangeTarget : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] private float supportCheckInterval = 0.15f;
        [SerializeField, Min(0.01f), Tooltip("기본 허용 높이와 폭발저항 1당 추가 허용 높이(m). 허용 높이 = (폭발저항 + 1) × 이 값. 정확히 허용 높이까지는 버티며 초과할 때 파괴.")] private float fallMetersPerPower = 5f;
        [SerializeField, Min(0.01f), Tooltip("낙하 파괴의 고정 월드 거리 구간(m).")] private float impactLayerDistance = 0.2f;
        private DestructibleTarget target;
        private float fallPeak;
        public void SetFallMetersPerPower(float value) => fallMetersPerPower = Mathf.Max(0.01f,value);

        private void OnCollisionEnter(Collision collision) => HandleLanding(collision);
        private void OnCollisionStay(Collision collision) => HandleLanding(collision);
        private void HandleLanding(Collision collision)
        {
            for (int i=0;i<collision.contactCount;i++)
            {
                var contact=collision.GetContact(i);
                if (contact.normal.y <= 0.5f) continue;
                float height=Mathf.Max(0f,fallPeak-transform.position.y);
                fallPeak=transform.position.y;
                if (height < 0.05f) return;
                if (target == null) target=GetComponent<DestructibleTarget>();
                target?.ApplyFallImpact(Mathf.Max(0f,height/Mathf.Max(0.01f,fallMetersPerPower)-1f),contact.point,impactLayerDistance);
                return;
            }
        }
        private Rigidbody body;
        private float supportTimer;
        private readonly List<Collider> colliders = new List<Collider>();
        private readonly RaycastHit[] supportHits = new RaycastHit[64];

        private void Awake() => ResolveBody();
        private void OnEnable()
        {
            ResolveBody();
            supportTimer = 0f;
            fallPeak = transform.position.y;
        }
        private void ResolveBody()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            body.useGravity = true;
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        private void FixedUpdate()
        {
            if (body == null) ResolveBody();
            fallPeak = Mathf.Max(fallPeak,transform.position.y);
            // Also recover targets that were frozen by the previous version.
            if (body.isKinematic) { body.isKinematic = false; body.WakeUp(); }
            if (!body.IsSleeping()) return;
            supportTimer -= Time.fixedDeltaTime;
            if (supportTimer > 0f) return;
            supportTimer = Mathf.Max(0.02f,supportCheckInterval);
            colliders.Clear();
            GetComponentsInChildren(false,colliders);
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var collider in colliders)
            {
                if (!collider.enabled || collider.isTrigger || collider.attachedRigidbody != body) continue;
                if (!hasBounds) { bounds = collider.bounds; hasBounds = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            if (!hasBounds) return;
            Vector3 origin = new Vector3(bounds.center.x,bounds.min.y+0.03f,bounds.center.z);
            Vector3 halfExtents = new Vector3(Mathf.Max(0.01f,bounds.extents.x*0.95f),0.01f,
                Mathf.Max(0.01f,bounds.extents.z*0.95f));
            int count = Physics.BoxCastNonAlloc(origin,halfExtents,Vector3.down,supportHits,
                Quaternion.identity,0.08f,~LayerMask.GetMask("Energy"),QueryTriggerInteraction.Ignore);
            for (int i=0;i<count;i++)
                if (supportHits[i].collider != null && supportHits[i].collider.attachedRigidbody != body &&
                    !supportHits[i].collider.transform.IsChildOf(transform) && supportHits[i].normal.y > 0.5f)
                    return;
            body.WakeUp();
        }
    }
}
