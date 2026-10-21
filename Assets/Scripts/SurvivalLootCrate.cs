using System;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 생존 모드에서 첫 셀 손실 시 실물 블록 3개를 드롭하는 상자 컴포넌트입니다. 본체 잔해는 남기며 재생성되지 않습니다.
    /// </summary>
    [RequireComponent(typeof(DestructibleTarget))]
    public sealed class SurvivalLootCrate : MonoBehaviour, IInteractable
    {
        [Header("Drop Settings")]
        [SerializeField] private SurvivalBlockPickup _pickupPrefab;
        [SerializeField, Min(0f)] private float _scatterSpeed = 2.5f;
        [SerializeField, Min(0f)] private float _upwardSpeed = 3.5f;

        private DestructibleTarget _structure;
        private bool _claimed;

        public bool IsClaimed => _claimed;
        public Transform OutlineRoot => transform;

        void Awake()
        {
            _claimed = false;
            _structure = GetComponent<DestructibleTarget>();
        }

        void LateUpdate()
        {
            // 탄환 관통, 폭발 등으로 인해 상자의 첫 번째 셀이 손실되면 딱 한 번 블록 3개를 방출합니다.
            if (!_claimed && _structure != null && _structure.InitialCellCount > 0 &&
                _structure.RemainingCellCount < _structure.InitialCellCount)
            {
                ReleaseContents();
            }
        }

        /// <summary>
        /// 기본 코어 총(관통력 1 이하)의 탄환이 명중했을 때 강제로 첫 셀 손실을 발생시켜 상자가 열리도록 합니다.
        /// </summary>
        /// <param name="position">탄환 명중 시작 좌표입니다.</param>
        /// <param name="rotation">탄환 비행 방향 회전입니다.</param>
        /// <param name="halfExtents">탄환 경계 상자 반경입니다.</param>
        /// <param name="distance">관통 진행 거리입니다.</param>
        public void TakeBasicBulletHit(Vector3 position, Quaternion rotation, Vector3 halfExtents, float distance)
        {
            if (_claimed || _structure == null || !isActiveAndEnabled) return;

            // 관통 저항보다 높은 유효 힘을 전달하여 셀을 파괴합니다.
            float force = Mathf.Max(1.1f, _structure.PenetrationResistance + 0.5f);
            _structure.PenetrateAlongLine(force, position, rotation, halfExtents, distance,
                out _, out _, out _, allowResistanceReduction: true);
        }

        /// <summary>
        /// 구조 블록 2종과 기능 블록 1종(총 3개)을 추첨하여 상자 주변으로 방출합니다.
        /// </summary>
        public void ReleaseContents()
        {
            if (_claimed) return;
            _claimed = true;

            SurvivalLootWeightInfo.RollCrateDrops(null, out int[] droppedModuleIds);

            if (_pickupPrefab == null)
            {
                Debug.LogError("SurvivalLootCrate에 SurvivalBlockPickup 프리팹이 연결되지 않았습니다.", this);
                return;
            }

            float baseAngle = UnityEngine.Random.Range(0f, 360f);
            for (int i = 0; i < droppedModuleIds.Length; i++)
            {
                float angle = (baseAngle + 360f * i / droppedModuleIds.Length) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 spawnPos = transform.position + Vector3.up * 0.4f + direction * 0.3f;

                var pickup = Instantiate(_pickupPrefab, spawnPos, Quaternion.identity);
                pickup.Configure(droppedModuleIds[i]);
                pickup.Launch(direction * _scatterSpeed + Vector3.up * _upwardSpeed);
            }
        }

        /// <summary>
        /// 상자는 직접 F키로 상호작용할 수 없습니다.
        /// </summary>
        public bool CanInteract(Interaction interactor) => false;

        /// <summary>
        /// 상자는 직접 F키로 상호작용할 수 없습니다.
        /// </summary>
        public bool Interact(Interaction interactor) => false;
    }
}
