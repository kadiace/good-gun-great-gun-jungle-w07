using UnityEngine;

namespace WeaponWorkshop
{
    // 모듈 상자 고정 위치. 어떤 모듈의 자리인지 지점에 직접 적어 배열 순서에 의미를 두지 않는다.
    // 해당 모듈을 이미 해금했으면 RaidCrateSpawner가 이 자리에 재료 상자를 대신 놓는다.
    public sealed class RaidModuleCrateSpot : MonoBehaviour
    {
        [SerializeField] private ModuleData module;
        public ModuleData Module => module;
        public string ModuleCode => module != null ? module.CodeId : null;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f);
            Gizmos.DrawWireCube(transform.position, new Vector3(1f, 0.8f, 0.75f));
        }
    }
}
