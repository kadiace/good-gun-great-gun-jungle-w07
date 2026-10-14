using UnityEngine;

namespace WeaponWorkshop
{
    // 엔드 상자 구형 범위의 방어막. 중력·유도 조건을 못 채운 플레이어 탄은 표면에서 튕겨 나가고,
    // 조건을 채운 탄은 통과해 코어로 향한다. 조준선은 방어막을 통과해 코어에 락온된다.
    [DisallowMultipleComponent]
    public sealed class EndChestShield : MonoBehaviour
    {
        [SerializeField] private EndChestCore core;

        public bool Deflects(in ProjectileStats stats) => core == null || core.Deflects(stats);
    }
}
