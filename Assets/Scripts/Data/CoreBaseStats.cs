using UnityEngine;

namespace WeaponWorkshop
{
    // 에너지 코어 기본 스탯. 모듈 스탯은 이 값 위에 더해진다.
    [CreateAssetMenu(fileName = "CoreBaseStats", menuName = "Weapon Workshop/Core Base Stats")]
    public sealed class CoreBaseStats : ScriptableObject
    {
        [SerializeField, TextArea(2, 8)] private string description = "무기의 중심이 되는 코어입니다. 여섯 면으로 탄환을 내보내며 연결된 모듈이 발사 경로의 스펙을 바꿉니다.";
        public string Description => description;
        [SerializeField, Tooltip("연사력은 초당 발사 횟수로 사용한다. 기본 1 = 1초에 1발.")]
        private WeaponStats stats = new WeaponStats { fireRate = 1f };
        [SerializeField, Min(0f)] private float baseDamage = 10f;

        public WeaponStats Stats => stats;
        public float BaseDamage => baseDamage;
    }
}
