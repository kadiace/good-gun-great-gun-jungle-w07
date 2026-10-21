#pragma warning disable 618
using System;
using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 월드에 물리로 떨어져 있는 실물 블록 픽업 아이템 컴포넌트입니다. 플레이어가 F키로 주워 가방에 넣을 수 있습니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(Collider)), RequireComponent(typeof(InteractionLabel))]
    public sealed class SurvivalBlockPickup : MonoBehaviour, IInteractable
    {
        [Header("Module Settings")]
        [SerializeField] private int _moduleId = 1;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private InteractionLabel _label;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private MeshRenderer _placeholderRenderer;
        [SerializeField] private ModuleCatalog _catalog;

        private float _weightKg;
        private int _currentVisualModuleId = -1;

        public int ModuleId => _moduleId;
        public float WeightKg => _weightKg;
        public Transform OutlineRoot => transform;

        void Awake()
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_label == null) _label = GetComponent<InteractionLabel>();
            if (_visualRoot == null) _visualRoot = transform.Find("VisualRoot");
            if (_placeholderRenderer == null && _visualRoot != null) _placeholderRenderer = _visualRoot.GetComponent<MeshRenderer>();
        }

        void Start()
        {
            // 씬에 정적으로 배치되어 Configure가 호출되지 않은 경우 초기 외형을 생성합니다.
            if (_currentVisualModuleId < 0)
            {
                UpdateLabelAndVisual();
            }
        }

        /// <summary>
        /// 모듈 슬롯 ID를 설정하고 시각 외형과 라벨 텍스트를 갱신합니다.
        /// </summary>
        /// <param name="moduleId">설정할 모듈 슬롯 ID(1~19)입니다.</param>
        public void Configure(int moduleId)
        {
            if (_moduleId == moduleId && _currentVisualModuleId == moduleId && _visualRoot != null && _visualRoot.childCount > 0)
            {
                return;
            }

            _moduleId = moduleId;
            UpdateLabelAndVisual(forceRefresh: true);
        }

        /// <summary>
        /// 상자에서 방출되거나 플레이어가 버릴 때 초기 속도와 회전력을 가합니다.
        /// </summary>
        /// <param name="velocity">방출 초기 선속도입니다.</param>
        public void Launch(Vector3 velocity)
        {
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = velocity;
                _rigidbody.angularVelocity = UnityEngine.Random.insideUnitSphere * 5f;
            }
        }

        /// <summary>
        /// F 상호작용 가능 여부를 판단합니다. 가방 UI가 열려 있지 않고 플레이어 인벤토리가 존재하면 무게 초과 시에도 항상 주울 수 있습니다.
        /// </summary>
        /// <param name="interactor">상호작용을 시도하는 시선 컴포넌트입니다.</param>
        /// <returns>상호작용 가능하면 true를 반환합니다.</returns>
        public bool CanInteract(Interaction interactor)
        {
            if (SurvivalInventoryUI.IsOpen) return false;
            if (interactor == null || interactor.Player == null) return false;

            var inventory = interactor.Player.GetComponent<PlayerInventory>();
            return inventory != null;
        }

        /// <summary>
        /// F키 상호작용 시 플레이어 인벤토리에 해당 블록을 1개 추가하고 픽업 오브젝트를 파괴합니다.
        /// </summary>
        /// <param name="interactor">상호작용을 수행한 컴포넌트입니다.</param>
        /// <returns>습득에 성공하면 true를 반환합니다.</returns>
        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor)) return false;

            var inventory = interactor.Player.GetComponent<PlayerInventory>();
            if (inventory == null) return false;

            inventory.Add(_moduleId, 1);
            Destroy(gameObject);
            return true;
        }

        /// <summary>
        /// 모듈 카탈로그 정보를 기반으로 블록의 시각 모델을 복제하고 상호작용 라벨(이름 및 무게)을 갱신합니다.
        /// </summary>
        /// <param name="forceRefresh">동일 ID 검사를 건너뛰고 강제로 모델을 교체할지 여부입니다.</param>
        private void UpdateLabelAndVisual(bool forceRefresh = false)
        {
            _weightKg = SurvivalLootWeightInfo.GetWeightKg(_moduleId);

            if (!forceRefresh && _currentVisualModuleId == _moduleId && _visualRoot != null && _visualRoot.childCount > 0)
            {
                return;
            }

            var catalog = ResolveCatalog();
            if (catalog != null && catalog.TryGet(_moduleId, out var entry) && entry.prefab != null)
            {
                ClearVisualChildren();

                if (_visualRoot != null)
                {
                    var visual = Instantiate(entry.prefab, _visualRoot);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale = entry.prefab.transform.localScale;

                    // 복제된 시각 모델의 콜라이더와 물리/기능 컴포넌트가 작동하지 않도록 비활성화합니다.
                    var colliders = visual.GetComponentsInChildren<Collider>(true);
                    for (int i = 0; i < colliders.Length; i++)
                    {
                        colliders[i].enabled = false;
                    }

                    var rigidbodies = visual.GetComponentsInChildren<Rigidbody>(true);
                    for (int i = 0; i < rigidbodies.Length; i++)
                    {
                        if (Application.isPlaying) Destroy(rigidbodies[i]);
                        else DestroyImmediate(rigidbodies[i]);
                    }

                    var placedBlocks = visual.GetComponentsInChildren<PlacedBlock>(true);
                    for (int i = 0; i < placedBlocks.Length; i++)
                    {
                        if (Application.isPlaying) Destroy(placedBlocks[i]);
                        else DestroyImmediate(placedBlocks[i]);
                    }

                    var statsList = visual.GetComponentsInChildren<ModuleStats>(true);
                    for (int i = 0; i < statsList.Length; i++)
                    {
                        if (Application.isPlaying) Destroy(statsList[i]);
                        else DestroyImmediate(statsList[i]);
                    }

                    var behaviours = visual.GetComponentsInChildren<MonoBehaviour>(true);
                    for (int i = 0; i < behaviours.Length; i++)
                    {
                        behaviours[i].enabled = false;
                    }

                    // 정상 모듈 외형이 표시되므로 기본 큐브 렌더러를 숨깁니다.
                    if (_placeholderRenderer != null)
                    {
                        _placeholderRenderer.enabled = false;
                    }
                }

                _currentVisualModuleId = _moduleId;

                if (_label != null)
                {
                    _label.SetTitle($"{entry.displayName} ({_weightKg:0.0}kg)");
                }
            }
            else
            {
                // 카탈로그 조회 실패 시 임의 대체 모델로 숨기지 않고 오류 상태를 유지합니다.
                ClearVisualChildren();
                _currentVisualModuleId = -1;

                if (_placeholderRenderer != null)
                {
                    _placeholderRenderer.enabled = true;
                }

                if (_label != null)
                {
                    _label.SetTitle($"알 수 없는 모듈 {_moduleId} ({_weightKg:0.0}kg)");
                }
            }
        }

        /// <summary>
        /// 시각 루트 하위에 생성된 기존 모듈 자식 오브젝트들을 정리합니다.
        /// </summary>
        private void ClearVisualChildren()
        {
            if (_visualRoot == null) return;

            for (int i = _visualRoot.childCount - 1; i >= 0; i--)
            {
                var child = _visualRoot.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        /// <summary>
        /// 모듈 카탈로그 에셋을 탐색하여 반환합니다.
        /// </summary>
        private ModuleCatalog ResolveCatalog()
        {
            if (_catalog != null) return _catalog;

            _catalog = Resources.Load<ModuleCatalog>("ModuleCatalog");
            if (_catalog != null) return _catalog;

            var builder = FindFirstObjectByType<CubeBuilder>();
            if (builder != null && builder.Catalog != null)
            {
                _catalog = builder.Catalog;
                return _catalog;
            }

#if UNITY_EDITOR
            _catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalog>("Assets/Data/ModuleCatalog.asset");
            if (_catalog != null) return _catalog;
#endif

            return null;
        }
    }
}
