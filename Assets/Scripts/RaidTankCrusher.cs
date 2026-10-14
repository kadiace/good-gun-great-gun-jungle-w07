using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 탱크가 추락하며 닿는 파츠와, 착지 후 이동 중 막힌 파츠를 부순다. RaidEnemyImmune 파츠와 적 부위는 부수지 않는다.
    [RequireComponent(typeof(RaidTank), typeof(Rigidbody))]
    public sealed class RaidTankCrusher : MonoBehaviour
    {
        [SerializeField, Tooltip("차체 충돌 범위 중심(탱크 로컬)")] private Vector3 bodyCenter = new Vector3(0f, 1.6f, 0.72f);
        [SerializeField, Tooltip("차체 충돌 범위 크기(탱크 로컬, 포신 포함)")] private Vector3 bodySize = new Vector3(3.82f, 3.11f, 5.65f);
        [SerializeField, Min(0f), Tooltip("몸체 둘레 여유(m). 추락 중과 접촉 시 부수는 영역에 더함")] private float margin = 0.5f;
        [SerializeField, Min(0f), Tooltip("착지 후에는 차체 바닥에서 이 높이 아래는 부수지 않음(밟고 있는 바닥 보호)")]
        private float groundClearance = 0.3f;
        [SerializeField, Min(0f), Tooltip("착지 후 차체 앞·뒤 끝에서 바깥으로 이 거리(m)까지의 부술 수 있는 파츠를 접촉과 무관하게 부숨(Track 앞뒤 걸림 방지)")]
        private float endReach = 1f;
        [SerializeField, Min(1), Tooltip("물리 스텝당 부술 수 있는 DestructibleTarget 개수 상한(초과분은 다음 스텝)")]
        private int maxPartsPerStep = 24;
        [SerializeField, Min(0f), Tooltip("부서진 칸 파편이 튀는 속도(m/s)")] private float debrisSpeed = 4f;
        private RaidTank tank;
        private Rigidbody body;
        private readonly Collider[] hits = new Collider[128];
        private readonly HashSet<DestructibleTarget> processed = new HashSet<DestructibleTarget>();
        private int budget;

        private void Awake()
        {
            tank = GetComponent<RaidTank>();
            body = GetComponent<Rigidbody>();
        }

        // 추락 중 닿아도 착지로 보지 않는 대상인지(부술 수 있는 파츠)
        public bool CanCrush(Collider collider)
        {
            if (collider == null || collider.transform.IsChildOf(transform)) return false;
            return CanCrush(collider.GetComponentInParent<DestructibleTarget>());
        }

        private static bool CanCrush(DestructibleTarget part) =>
            part != null && part.isActiveAndEnabled && !part.TryGetComponent<RaidEnemyImmune>(out _) &&
            RaidEnemyCore.FindForPart(part.transform) == null;

        private void FixedUpdate()
        {
            processed.Clear();
            budget = maxPartsPerStep;
            if (tank.IsFalling) CrushFalling();
            else if (tank.IsLanded && endReach > 0f) CrushEnds();
        }

        // 차체 앞·뒤 끝 바깥(endReach)의 파츠를 접촉 콜백 없이 부순다. 바닥 보호 높이와 윗면 범위는 몸체 영역과 같다.
        private void CrushEnds()
        {
            float baseY = bodyCenter.y - bodySize.y * 0.5f;
            float lowerY = baseY + groundClearance, upperY = baseY + bodySize.y + margin;
            Vector3 half = new Vector3(bodySize.x * 0.5f + margin, (upperY - lowerY) * 0.5f, endReach * 0.5f);
            float offsetZ = bodySize.z * 0.5f + endReach * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 center = transform.TransformPoint(new Vector3(bodyCenter.x, (lowerY + upperY) * 0.5f, bodyCenter.z + side * offsetZ));
                int count = Physics.OverlapBoxNonAlloc(center, half, hits, transform.rotation,
                    ~LayerMask.GetMask("Energy"), QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count && budget > 0; i++) Crush(hits[i], center, transform.rotation, half);
            }
        }

        // 한 스텝 앞까지 미리 비워 부딪혀 속도를 잃지 않게 한다.
        private void CrushFalling()
        {
            float travel = Mathf.Max(0f, -body.linearVelocity.y) * Time.fixedDeltaTime + margin;
            Vector3 half = bodySize * 0.5f + Vector3.one * margin;
            half.y += travel * 0.5f;
            Vector3 center = transform.TransformPoint(bodyCenter) + Vector3.down * (travel * 0.5f);
            int count = Physics.OverlapBoxNonAlloc(center, half, hits, transform.rotation,
                ~LayerMask.GetMask("Energy"), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count && budget > 0; i++) Crush(hits[i], center, transform.rotation, half);
        }

        // 착지 후 부술 수 있는 파츠에 앞·옆·위에서 닿으면(바닥을 밟은 접촉 제외) 몸체 영역 안의 칸을 부순다.
        // 정면뿐 아니라 옆 스커트·안테나 같은 돌출부가 걸리는 경우도 포함한다.
        private void OnCollisionStay(Collision collision)
        {
            if (!tank.IsLanded || budget <= 0) return;
            bool touching = false;
            foreach (var contact in collision.contacts)
                if (contact.normal.y < 0.7f) { touching = true; break; }
            if (!touching) return;
            GetBodyBox(0f, out Vector3 center, out Vector3 half);
            Crush(collision.collider, center, transform.rotation, half);
        }

        // 걸림 해소용: 접촉 콜백과 무관하게 몸체 영역(+위쪽 extraTop)에 겹친 부술 수 있는 파츠를 부순다. 몸체 위에 얹힌 파츠 포함.
        public void CrushBodyVolume(float extraTop)
        {
            processed.Clear();
            budget = maxPartsPerStep;
            GetBodyBox(extraTop, out Vector3 center, out Vector3 half);
            int count = Physics.OverlapBoxNonAlloc(center, half, hits, transform.rotation,
                ~LayerMask.GetMask("Energy"), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count && budget > 0; i++) Crush(hits[i], center, transform.rotation, half);
        }

        // 차체 바닥에서 groundClearance 위부터 차체 윗면+margin+extraTop까지(밟고 있는 바닥은 보호).
        private void GetBodyBox(float extraTop, out Vector3 center, out Vector3 half)
        {
            float baseY = bodyCenter.y - bodySize.y * 0.5f;
            float lowerY = baseY + groundClearance, upperY = baseY + bodySize.y + margin + extraTop;
            half = new Vector3(bodySize.x * 0.5f + margin, (upperY - lowerY) * 0.5f, bodySize.z * 0.5f + margin);
            center = transform.TransformPoint(new Vector3(bodyCenter.x, (lowerY + upperY) * 0.5f, bodyCenter.z));
        }

        private void Crush(Collider collider, Vector3 center, Quaternion rotation, Vector3 half)
        {
            if (collider == null || collider.transform.IsChildOf(transform)) return;
            var part = collider.GetComponentInParent<DestructibleTarget>();
            if (!CanCrush(part) || !processed.Add(part)) return;
            budget--;
            part.BreakInBox(center, rotation, half, debrisSpeed);
        }
    }
}
