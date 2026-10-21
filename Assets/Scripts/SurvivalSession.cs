using System;
using System.Collections.Generic;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 생존 모드 씬의 활성 상태를 나타내고 플레이어 인벤토리 초기화, 기본 총기 장착 및 생존 씬 내 불필요한 공방 요소들을 비활성화하는 세션 컨트롤러입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurvivalSession : MonoBehaviour
    {
        public static bool IsActive { get; private set; }

        [Header("Survival Disabled Objects")]
        [SerializeField] private List<GameObject> _objectsToDisable = new List<GameObject>();

        [Header("Survival Setup")]
        [SerializeField] private GameObject _defaultCoreGunPrefab;
        [SerializeField] private SurvivalBlockPickup _pickupPrefab;

        [Header("시작 위치")]
        [SerializeField, Min(0f)] private float _spawnRadius = 3f;
        private bool _startingCoreIssued;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsActive = false;
        }

        void Awake()
        {
            IsActive = true;
            ApplySurvivalEnvironment();
        }

        void Start()
        {
            RandomizePlayerStart();
            EnsurePlayerSetup();
        }

        void OnDestroy()
        {
            IsActive = false;
        }

        /// <summary>
        /// 씬의 플레이어 위치를 중심으로 지정 반경의 바닥과 캡슐 여유 공간을 검사해 시작 위치를 정한다.
        /// 후보가 모두 막혀 있으면 기존 위치와 방향을 유지한다.
        /// </summary>
        private void RandomizePlayerStart()
        {
            var player = FindFirstObjectByType<FirstPersonPlayer>();
            if (player == null || _spawnRadius <= 0f) return;
            var controller = player.GetComponent<CharacterController>();
            if (controller == null) return;

            Vector3 origin = player.transform.position;
            Vector3 scale = player.transform.lossyScale;
            float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfHeight = Mathf.Max(radius, controller.height * Mathf.Abs(scale.y) * 0.5f);
            Vector3 centerOffset = player.transform.TransformVector(controller.center);
            float minimumGroundNormal = Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad);
            Physics.SyncTransforms();

            for (int attempt = 0; attempt < 32; attempt++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * _spawnRadius;
                Vector3 probe = origin + new Vector3(offset.x, 2f, offset.y);
                RaycastHit[] hits = Physics.RaycastAll(probe, Vector3.down, 6f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                int nearest = -1;
                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i].collider.transform.IsChildOf(player.transform)) continue;
                    if (nearest < 0 || hits[i].distance < hits[nearest].distance) nearest = i;
                }
                if (nearest < 0 || hits[nearest].normal.y < minimumGroundNormal) continue;

                Vector3 position = new Vector3(probe.x,
                    hits[nearest].point.y + halfHeight - centerOffset.y + 0.05f, probe.z);
                Vector3 center = position + centerOffset;
                Vector3 capOffset = Vector3.up * (halfHeight - radius);
                Collider[] overlaps = Physics.OverlapCapsule(center - capOffset, center + capOffset,
                    radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int i = 0; i < overlaps.Length; i++)
                {
                    if (overlaps[i].transform.IsChildOf(player.transform)) continue;
                    blocked = true;
                    break;
                }
                if (blocked) continue;

                player.RestorePose(position, player.transform.eulerAngles,
                    player.ViewPoint != null ? player.ViewPoint.localEulerAngles : Vector3.zero);
                return;
            }

            Debug.LogWarning("시작 반경 안에 빈 바닥을 찾지 못해 기존 플레이어 위치를 유지합니다.", this);
        }

        /// <summary>
        /// 플레이어에게 인벤토리와 가방 UI를 부착하고 시작 기본 코어 총기를 손에 장착시킵니다.
        /// </summary>
        public void EnsurePlayerSetup()
        {
            var player = FindFirstObjectByType<FirstPersonPlayer>();
            if (player == null) return;

            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                inventory = player.gameObject.AddComponent<PlayerInventory>();
            }

            if (_pickupPrefab != null)
            {
                inventory.SetPickupPrefab(_pickupPrefab);
            }
            else
            {
                var loadedPickup = Resources.Load<SurvivalBlockPickup>("SurvivalBlockPickup");
#if UNITY_EDITOR
                if (loadedPickup == null)
                {
                    loadedPickup = UnityEditor.AssetDatabase.LoadAssetAtPath<SurvivalBlockPickup>("Assets/Prefabs/Loot/SurvivalBlockPickup.prefab");
                }
#endif
                if (loadedPickup != null)
                {
                    inventory.SetPickupPrefab(loadedPickup);
                }
            }

            var inventoryUI = FindFirstObjectByType<SurvivalInventoryUI>();
            if (inventoryUI == null)
            {
                player.gameObject.AddComponent<SurvivalInventoryUI>();
            }

            var holder = player.GetComponent<WeaponHolder>();
            if (holder == null || holder.EquippedWeapon != null || _startingCoreIssued) return;
            if (_defaultCoreGunPrefab == null)
            {
                Debug.LogError("생존 시작용 빈 코어 프리팹이 연결되지 않았습니다.", this);
                return;
            }
            if (_defaultCoreGunPrefab.GetComponent<EquipableWeapon>() == null ||
                _defaultCoreGunPrefab.GetComponentInChildren<PowerCore>(true) == null ||
                _defaultCoreGunPrefab.GetComponentInChildren<PlacedBlock>(true) != null)
            {
                Debug.LogError("생존 시작 총은 모듈이 없는 코어 프리팹이어야 합니다.", this);
                return;
            }

            var spawnedGun = Instantiate(_defaultCoreGunPrefab,
                player.transform.position + Vector3.up, Quaternion.identity);
            var coreWeapon = spawnedGun.GetComponent<EquipableWeapon>();
            if (!holder.Equip(coreWeapon))
            {
                Destroy(spawnedGun);
                Debug.LogError("생존 시작 코어를 장착하지 못했습니다.", this);
                return;
            }
            _startingCoreIssued = true;
        }

        /// <summary>
        /// 생존 모드에서 불필요한 상점 전시대, 손님 카운터, 슬롯머신, 일차 전환 상호작용 등을 비활성화합니다.
        /// </summary>
        public void ApplySurvivalEnvironment()
        {
            // 수동으로 등록된 오브젝트들을 비활성화합니다.
            if (_objectsToDisable != null)
            {
                for (int i = 0; i < _objectsToDisable.Count; i++)
                {
                    if (_objectsToDisable[i] != null)
                    {
                        _objectsToDisable[i].SetActive(false);
                    }
                }
            }

            // 씬 내부의 기존 상점/일차 전환 컴포넌트들을 찾아 비활성화합니다.
            var dayManager = FindFirstObjectByType<_DayManager>();
            if (dayManager != null) dayManager.gameObject.SetActive(false);

            var nextDayInteractable = FindFirstObjectByType<NextDayInteractable>();
            if (nextDayInteractable != null) nextDayInteractable.gameObject.SetActive(false);

            var slotMachine = FindFirstObjectByType<SlotMachineReels>();
            if (slotMachine != null) slotMachine.gameObject.SetActive(false);

            var retireButton = FindFirstObjectByType<RetireButton>();
            if (retireButton != null) retireButton.gameObject.SetActive(false);

            // 야간습격 전용 사망 UI 비활성화 (생존 모드는 SurvivalSessionResetter가 처리)
            var raidDeathUI = FindFirstObjectByType<RaidDeathUI>(FindObjectsInactive.Include);
            if (raidDeathUI != null) raidDeathUI.gameObject.SetActive(false);

            // 손님 카운터 및 남쪽 통로 방해물 비활성화
            var allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allObjects.Length; i++)
            {
                string objName = allObjects[i].name;
                if (objName.Contains("CustomerCounter2") ||
                    objName.Contains("Display_Module") ||
                    objName.Contains("Wall_10") ||
                    objName.Contains("SlotMachineTable") ||
                    objName.Contains("CloseShop"))
                {
                    allObjects[i].SetActive(false);
                }
            }
        }
    }
}
