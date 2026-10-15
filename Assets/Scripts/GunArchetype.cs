using UnityEngine;

namespace WeaponWorkshop
{
    // 총 종류(저격소총, 산탄총...)의 공통 조건. 수치 X는 여기 한 곳에서 조정.
    [CreateAssetMenu(menuName = "Weapon Workshop/Gun Archetype")]
    [System.Obsolete("레거시 요청 저장소. TableManager.CustomerRequest와 요청 ID를 사용하세요.")]
    public sealed class GunArchetype : ScriptableObject
    {
        [SerializeField, HideInInspector] private string tableId;
        public string TableId => tableId;
        public string displayName;
        [Tooltip("무한 모드 손님 주문 대사에 들어갈 요구사항 문구. 예) 관통 모듈(저격총)")]
        public string request;
        public GunCondition[] conditions;
        [Tooltip("총에 이 태그(총 종류)가 붙어야 함. 비우면 무시")]
        public WeaponTypeData weaponType;
        [Tooltip("같이 요구하면 만족 불가능한 요구사항. 무한 모드 조합에서 함께 뽑히지 않음 (양방향)")]
        public GunArchetype[] excludes;
        [Min(1), Tooltip("이 일차부터 조합 손님이 요구할 수 있음 (필요 모듈 해금일)")]
        public int unlockDay = 1;
        [Tooltip("이 랜덤 주문에 필요한 모듈. 하나라도 잠겨 있으면 조합 후보에서 제외")]
        public ModuleData[] requiredModules;

        public bool Check(EquipableWeapon weapon)
        {
            return TableManager.Instance.CustomerRequest.MeetsRequest(tableId, new CustomerRequestInfo.WeaponSnapshot(weapon));
        }
    }
}
