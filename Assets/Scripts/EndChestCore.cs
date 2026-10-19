using UnityEngine;

namespace WeaponWorkshop
{
    // 엔드 상자 구형 범위 중앙의 코어. 중력·유도를 모두 갖춘 플레이어 탄만 맞고, 나머지는 그대로 통과한다.
    // 유도 락온은 TargetHealth + RaidEnemyCore 조합으로 걸리므로 같은 오브젝트에 둔다(체력은 사실상 무한).
    [DisallowMultipleComponent, RequireComponent(typeof(TargetHealth), typeof(RaidEnemyCore))]
    public sealed class EndChestCore : MonoBehaviour
    {
        [SerializeField] private EndChest chest;
        [SerializeField, Min(0f)] private float requiredGravity = 1f;
        [SerializeField, Min(0f)] private float requiredHoming = 1f;

        public float RequiredGravity => requiredGravity;
        public float RequiredHoming => requiredHoming;

        private bool Qualifies(in ProjectileStats stats) =>
            stats.gravity >= requiredGravity && stats.homing >= requiredHoming;

        public bool AcceptsHit(in ProjectileStats stats) => chest != null && chest.IsClosed && Qualifies(stats);

        // 닫혀 있는 동안 조건을 못 채운 탄은 구형 범위(방어막)에서 튕긴다.
        public bool Deflects(in ProjectileStats stats) => chest != null && chest.IsClosed && !Qualifies(stats);

        // ProjectileMotor가 AcceptsHit을 통과한 탄이 실제로 닿았을 때 호출한다.
        public void NotifyHit(in ProjectileStats stats)
        {
            if (chest == null || !chest.IsClosed) return;
            NightRaidController.Instance?.NotifyEndChestHit(stats);
            chest.BeginAbsorb();
        }
    }
}
