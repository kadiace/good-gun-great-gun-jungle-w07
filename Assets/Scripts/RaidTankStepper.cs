using UnityEngine;

namespace WeaponWorkshop
{
    // 이동 중 앞이 낮은 정적 턱(건물 기초, 계단 등)에 막히면 올라설 수 있게 상승 속도를 돌려준다. 한도보다 높으면 올라가지 않는다.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RaidTankStepper : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("올라설 수 있는 최대 턱 높이(m). 0이면 사용 안 함")] private float maxStepHeight = 1f;
        [SerializeField, Min(0.1f), Tooltip("턱을 올라서는 상승 속도(m/s)")] private float climbSpeed = 3f;
        [SerializeField, Tooltip("차체 앞 끝까지의 거리(탱크 로컬 +Z, 포신 제외 차체 기준)")] private float frontOffset = 3.4f;
        [SerializeField, Min(0.1f), Tooltip("앞쪽 탐지 폭의 절반(m)")] private float halfWidth = 1.6f;
        [SerializeField, Tooltip("피벗에서 충돌체 바닥까지의 높이(m)")] private float bottomOffset = 0.045f;
        [SerializeField, Min(0.1f), Tooltip("앞쪽 탐지 거리(m)")] private float probeDistance = 0.6f;
        private const float ProbeLift = 0.05f, ProbeHeight = 0.15f;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly RaycastHit[] topHits = new RaycastHit[16];

        // 앞쪽 낮은 높이에서 처음 닿는 정적 콜라이더(자신·움직이는 물체 제외)
        private bool Probe(Vector3 forward, out RaycastHit hit)
        {
            Vector3 center = transform.position + forward * (frontOffset - probeDistance * 0.5f)
                + Vector3.up * (bottomOffset + ProbeLift + ProbeHeight);
            Vector3 half = new Vector3(halfWidth, ProbeHeight, probeDistance * 0.5f);
            int count = Physics.BoxCastNonAlloc(center, half, forward, hits, Quaternion.LookRotation(forward),
                probeDistance, ~LayerMask.GetMask("Energy", "EndChest", "EndChestShield"), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform) || hits[i].collider.attachedRigidbody != null) continue;
                hit = hits[i];
                return true;
            }
            hit = default;
            return false;
        }

        public Collider FindBlocker(Vector3 forward) => Probe(forward, out var hit) ? hit.collider : null;

        // forward 방향으로 이동하려는데 한도 이하의 낮은 턱에 막혀 있으면 상승 속도를, 아니면 0을 반환한다. 한도가 음수이면 maxStepHeight.
        public float ClimbVelocity(Vector3 forward, float heightLimit = -1f)
        {
            float limit = heightLimit >= 0f ? heightLimit : maxStepHeight;
            if (limit <= 0f || !Probe(forward, out var hit)) return 0f;
            // 턱 윗면 높이를 앞쪽 위에서 아래로 쏴서 잰다.
            Vector3 origin = hit.point + forward * 0.15f + Vector3.up * (limit + 0.5f);
            int down = Physics.RaycastNonAlloc(origin, Vector3.down, topHits, limit + 0.7f,
                ~LayerMask.GetMask("Energy"), QueryTriggerInteraction.Ignore);
            float top = float.NegativeInfinity;
            for (int j = 0; j < down; j++)
                if (!topHits[j].collider.transform.IsChildOf(transform) && topHits[j].point.y > top) top = topHits[j].point.y;
            float rise = top - (transform.position.y + bottomOffset);
            return rise > 0.02f && rise <= limit ? climbSpeed : 0f;
        }
    }
}
