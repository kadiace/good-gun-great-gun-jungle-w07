using System;
using System.Collections.Generic;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 플레이어의 생존 모듈 가방(인벤토리)과 장착 총기를 포함한 휴대 무게를 관리하는 컴포넌트입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        public static PlayerInventory Instance { get; private set; }

        [Header("Prefabs & References")]
        [SerializeField] private SurvivalBlockPickup _pickupPrefab;
        [SerializeField] private WeaponHolder _weaponHolder;

        private readonly Dictionary<int, int> _counts = new Dictionary<int, int>();
        private EquipableWeapon _subscribedWeapon;
        private float _cachedTotalWeightKg;

        public event Action InventoryChanged;

        public float CurrentCarryWeightKg => _cachedTotalWeightKg;

        /// <summary>
        /// ?고??꾩뿉 ?ㅻЪ 釉붾줉 ?쒕∼???꾨━?뱀쓣 紐낆떆?곸쑝濡??ㅼ젙?⑸땲??
        /// </summary>
        /// <param name="pickupPrefab">?쒕∼??釉붾줉 ?쎌뾽 ?꾨━??/param>
        public void SetPickupPrefab(SurvivalBlockPickup pickupPrefab)
        {
            if (pickupPrefab != null)
            {
                _pickupPrefab = pickupPrefab;
            }
        }

        void Awake()
        {
            Instance = this;
            if (_weaponHolder == null) _weaponHolder = GetComponent<WeaponHolder>();
            if (_pickupPrefab == null)
            {
                _pickupPrefab = Resources.Load<SurvivalBlockPickup>("SurvivalBlockPickup");
#if UNITY_EDITOR
                if (_pickupPrefab == null)
                {
                    _pickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<SurvivalBlockPickup>("Assets/Prefabs/Loot/SurvivalBlockPickup.prefab");
                }
#endif
            }
            RecalculateWeight();
        }

        void OnEnable()
        {
            if (_weaponHolder != null)
            {
                _weaponHolder.EquippedWeaponChanged += OnEquippedWeaponChanged;
                SubscribeWeaponEvents(_weaponHolder.EquippedWeapon);
            }
            RecalculateWeight();
        }

        void OnDisable()
        {
            if (_weaponHolder != null)
            {
                _weaponHolder.EquippedWeaponChanged -= OnEquippedWeaponChanged;
            }
            UnsubscribeWeaponEvents();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 특정 모듈 슬롯 ID의 현재 가방 보유 수량을 조회합니다.
        /// </summary>
        /// <param name="moduleId">조회할 모듈 슬롯 ID(1~18)입니다.</param>
        /// <returns>보유 수량을 반환합니다.</returns>
        public int GetCount(int moduleId)
        {
            return _counts.TryGetValue(moduleId, out int count) ? count : 0;
        }

        /// <summary>
        /// 현재 가방에 1개 이상 존재하는 모든 모듈 슬롯 ID와 수량 목록을 가져옵니다.
        /// </summary>
        /// <param name="results">결과를 담을 리스트입니다.</param>
        public void GetAllItems(List<KeyValuePair<int, int>> results)
        {
            if (results == null) return;
            results.Clear();
            foreach (var kvp in _counts)
            {
                if (kvp.Value > 0)
                {
                    results.Add(kvp);
                }
            }
        }

        /// <summary>
        /// 가방에 모듈 수량을 추가합니다.
        /// </summary>
        /// <param name="moduleId">추가할 모듈 슬롯 ID(1~18)입니다.</param>
        /// <param name="count">추가할 개수입니다.</param>
        public void Add(int moduleId, int count = 1)
        {
            if (count <= 0 || SurvivalLootWeightInfo.IsDisabledInSurvival(moduleId)) return;

            int current = GetCount(moduleId);
            _counts[moduleId] = current + count;
            RecalculateWeight();
            InventoryChanged?.Invoke();
        }

        /// <summary>
        /// 가방에서 지정한 수량만큼 모듈을 차감합니다. 수량이 부족하면 차감하지 않고 false를 반환합니다.
        /// </summary>
        /// <param name="moduleId">차감할 모듈 슬롯 ID입니다.</param>
        /// <param name="count">차감할 수량입니다.</param>
        /// <returns>차감 성공 시 true를 반환합니다.</returns>
        public bool TryConsume(int moduleId, int count = 1)
        {
            if (count <= 0) return true;

            int current = GetCount(moduleId);
            if (current < count) return false;

            int remaining = current - count;
            if (remaining > 0) _counts[moduleId] = remaining;
            else _counts.Remove(moduleId);

            RecalculateWeight();
            InventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 보유 수량에서 count만큼 차감하고 플레이어 발밑 전방 월드에 실물 픽업 오브젝트를 생성하여 버립니다.
        /// </summary>
        /// <param name="moduleId">버릴 모듈 슬롯 ID입니다.</param>
        /// <param name="count">버릴 수량입니다.</param>
        /// <returns>버리기에 성공하면 true를 반환합니다.</returns>
        public bool TryDropToWorld(int moduleId, int count)
        {
            if (count <= 0 || GetCount(moduleId) < count) return false;

            if (_pickupPrefab == null)
            {
                _pickupPrefab = Resources.Load<SurvivalBlockPickup>("SurvivalBlockPickup");
                if (_pickupPrefab == null)
                {
                    Debug.LogError("PlayerInventory에 SurvivalBlockPickup 프리팹이 연결되지 않았습니다.", this);
                    return false;
                }
            }

            if (!TryConsume(moduleId, count)) return false;

            Vector3 spawnOrigin = transform.position + transform.forward * 0.8f + Vector3.up * 0.3f;
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = UnityEngine.Random.insideUnitSphere * 0.2f;
                offset.y = Mathf.Abs(offset.y);
                var pickup = Instantiate(_pickupPrefab, spawnOrigin + offset, Quaternion.identity);
                pickup.Configure(moduleId);
                Vector3 dropVelocity = transform.forward * 1.5f + Vector3.up * 1.0f + UnityEngine.Random.insideUnitSphere * 0.5f;
                pickup.Launch(dropVelocity);
            }

            return true;
        }

        /// <summary>
        /// 가방 안의 모든 블록 무게(100%)와 손에 장착한 총의 블록 무게(50%)를 합산하여 갱신합니다.
        /// </summary>
        public void RecalculateWeight()
        {
            float bagWeight = 0f;
            foreach (var kvp in _counts)
            {
                if (kvp.Value > 0)
                {
                    bagWeight += SurvivalLootWeightInfo.GetWeightKg(kvp.Key) * kvp.Value;
                }
            }

            float equippedWeight = 0f;
            if (_weaponHolder != null && _weaponHolder.EquippedWeapon != null && _weaponHolder.EquippedWeapon.IsEquipped)
            {
                var assembly = _weaponHolder.EquippedWeapon.GetComponentInChildren<CubeAssembly>(true);
                if (assembly != null)
                {
                    var blocks = assembly.GetComponentsInChildren<PlacedBlock>(true);
                    for (int i = 0; i < blocks.Length; i++)
                    {
                        var block = blocks[i];
                        if (block != null && block.transform.parent == assembly.transform)
                        {
                            equippedWeight += SurvivalLootWeightInfo.GetWeightKg(block.ModuleSlot);
                        }
                    }
                }
            }

            var table = TableManager.Instance.SurvivalLootWeight;
            float ratio = table != null ? table.equippedWeightRatio : 0.5f;
            _cachedTotalWeightKg = bagWeight + (equippedWeight * ratio);
        }

        /// <summary>
        /// 장착 무기가 교체되었을 때 기존 구독을 해제하고 새 무기의 조립 변경 이벤트를 구독합니다.
        /// </summary>
        /// <param name="previous">이전 장착 무기입니다.</param>
        /// <param name="current">새로 장착된 무기입니다.</param>
        private void OnEquippedWeaponChanged(EquipableWeapon previous, EquipableWeapon current)
        {
            UnsubscribeWeaponEvents();
            SubscribeWeaponEvents(current);
            RecalculateWeight();
            InventoryChanged?.Invoke();
        }

        /// <summary>
        /// 장착 무기의 조립 변경 이벤트를 구독하여 블록 증감 시 무게를 갱신하도록 연결합니다.
        /// </summary>
        /// <param name="weapon">구독할 장착 무기입니다.</param>
        private void SubscribeWeaponEvents(EquipableWeapon weapon)
        {
            if (weapon == null) return;
            _subscribedWeapon = weapon;
            _subscribedWeapon.AssemblyChanged += OnWeaponAssemblyChanged;
        }

        /// <summary>
        /// 현재 구독 중인 장착 무기의 조립 변경 이벤트 구독을 해제합니다.
        /// </summary>
        private void UnsubscribeWeaponEvents()
        {
            if (_subscribedWeapon != null)
            {
                _subscribedWeapon.AssemblyChanged -= OnWeaponAssemblyChanged;
                _subscribedWeapon = null;
            }
        }

        /// <summary>
        /// 장착 총기의 블록 조립이 변경되었을 때 무게를 재계산하고 인벤토리 변경 이벤트를 호출합니다.
        /// </summary>
        private void OnWeaponAssemblyChanged()
        {
            RecalculateWeight();
            InventoryChanged?.Invoke();
        }
    }
}
