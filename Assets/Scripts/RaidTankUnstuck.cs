using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 이동하려는데 실제 속도가 낮은 시간이 이어지면 단계적으로 푼다: 1) 몸체 영역 부수기 2) 임시로 더 높은 턱 오르기 3) 막는 콜라이더 잠시 통과.
    // 정상 이동 중에는 타이머만 돌며 물리 조회를 하지 않는다.
    [RequireComponent(typeof(RaidTank))]
    public sealed class RaidTankUnstuck : MonoBehaviour
    {
        [SerializeField, Range(0.05f, 0.9f), Tooltip("목표 이동 속도 대비 실제 수평 속도가 이 비율 미만이면 걸린 것으로 봄")] private float stuckSpeedRatio = 0.3f;
        [SerializeField, Min(0.1f), Tooltip("1단계: 걸린 지 이 시간(초) 뒤 몸체 영역(위쪽 포함)을 부숨")] private float crushAfter = 0.5f;
        [SerializeField, Min(0.1f), Tooltip("1단계 반복 간격(초)")] private float crushInterval = 0.25f;
        [SerializeField, Min(0f), Tooltip("1단계에서 몸체 위쪽으로 더 부수는 높이(m, 얹힌 파츠 대응)")] private float crushExtraTop = 1f;
        [SerializeField, Min(0.1f), Tooltip("2단계: 걸린 지 이 시간(초) 뒤 턱 오르기 한도를 임시로 올림")] private float raiseAfter = 1.5f;
        [SerializeField, Min(0f), Tooltip("2단계 임시 턱 오르기 한도(m)")] private float stuckStepHeight = 1.6f;
        [SerializeField, Min(0.1f), Tooltip("3단계: 걸린 지 이 시간(초) 뒤 막는 콜라이더를 잠시 통과")] private float ghostAfter = 3f;
        [SerializeField, Min(0.1f), Tooltip("3단계 충돌 무시 유지 시간(초)")] private float ghostDuration = 2f;
        private RaidTankCrusher crusher;
        private RaidTankStepper stepper;
        private Collider[] bodyColliders;
        private readonly List<Collider> ghosted = new List<Collider>();
        private readonly List<float> ghostExpiry = new List<float>();
        private float stuckTime, nextCrush;

        private void Awake()
        {
            crusher = GetComponent<RaidTankCrusher>();
            stepper = GetComponent<RaidTankStepper>();
        }

        private void OnDisable() => ReleaseGhosts(true);

        // RaidTank.FixedUpdate에서 호출. 추가로 필요한 상승 속도(0이면 없음)를 반환한다.
        public float Tick(bool wantsToMove, Vector3 forward, float horizontalSpeed, float moveSpeed)
        {
            ReleaseGhosts(false);
            if (!wantsToMove || horizontalSpeed >= moveSpeed * stuckSpeedRatio) { stuckTime = 0f; return 0f; }
            stuckTime += Time.fixedDeltaTime;
            if (stuckTime >= crushAfter && crusher != null && Time.time >= nextCrush)
            {
                crusher.CrushBodyVolume(crushExtraTop);
                nextCrush = Time.time + crushInterval;
            }
            float climb = 0f;
            if (stuckTime >= raiseAfter && stepper != null) climb = stepper.ClimbVelocity(forward, stuckStepHeight);
            if (stuckTime >= ghostAfter && stepper != null)
            {
                Collider blocker = stepper.FindBlocker(forward);
                if (blocker != null && !ghosted.Contains(blocker)) Ghost(blocker);
                stuckTime = raiseAfter;
            }
            return climb;
        }

        private void Ghost(Collider blocker)
        {
            // 부분 파괴로 런타임에 생긴 충돌체도 포함하도록 매번 다시 모은다(드문 호출).
            bodyColliders = GetComponentsInChildren<Collider>();
            foreach (var collider in bodyColliders)
                if (collider != null) Physics.IgnoreCollision(collider, blocker, true);
            ghosted.Add(blocker);
            ghostExpiry.Add(Time.time + ghostDuration);
        }

        private void ReleaseGhosts(bool all)
        {
            for (int i = ghosted.Count - 1; i >= 0; i--)
            {
                if (!all && Time.time < ghostExpiry[i]) continue;
                if (ghosted[i] != null && bodyColliders != null)
                    foreach (var collider in bodyColliders)
                        if (collider != null) Physics.IgnoreCollision(collider, ghosted[i], false);
                ghosted.RemoveAt(i);
                ghostExpiry.RemoveAt(i);
            }
        }
    }
}
