using UnityEngine;

namespace WeaponWorkshop
{
    // 모듈 프리팹에 붙여 어떤 ModuleData인지 알린다. 스탯 합산은 StatCollector가 담당.
    [DisallowMultipleComponent]
    public sealed class ModuleStats : MonoBehaviour
    {
        [SerializeField] private ModuleData data;
        public ModuleData Data => data;
    }
}
