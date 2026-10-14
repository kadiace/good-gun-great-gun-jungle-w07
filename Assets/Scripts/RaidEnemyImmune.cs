using UnityEngine;

namespace WeaponWorkshop
{
    // 적(탱크 압착, 헬기·탱크 폭발)에게 부서지지 않는 파츠(금고 벽·천장, 상자 등)에 붙입니다. DestructibleTarget과 같은 오브젝트에 둡니다.
    // 플레이어 무기에는 영향이 없습니다.
    [DisallowMultipleComponent]
    public sealed class RaidEnemyImmune : MonoBehaviour
    {
        // attacker가 적(RaidEnemyCore 아래)이고 target이 보호 대상이면 true
        public static bool Protects(DestructibleTarget target, Transform attacker) =>
            target != null && attacker != null && target.TryGetComponent<RaidEnemyImmune>(out _) &&
            RaidEnemyCore.FindForPart(attacker) != null;
    }
}
