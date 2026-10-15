using System;

namespace WeaponWorkshop
{
    // 테스트용 구조화 로그 데이터. JsonUtility 직렬화를 위해 public 필드만 둔다.
    [Serializable] public sealed class SceneBootLog { public string scene; }
    [Serializable] public sealed class WeaponSaleLog { public int price; public bool satisfied; public int tagCount; }
    [Serializable] public sealed class RaidResultLog { public string scene; public bool survived; }
}
