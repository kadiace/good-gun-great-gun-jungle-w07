using UnityEngine;
using UnityEngine.Serialization;

namespace WeaponWorkshop
{
    public enum BlockKind { Blocker = 0, Extension = 3, SplitTwo = 4, SplitFour = 5 }

    public sealed class PlacedBlock : MonoBehaviour
    {
        [SerializeField] private BlockKind kind;
        [SerializeField, Tooltip("모듈의 가산 스탯. 추후 SO 연결은 GetStatContribution에서 처리합니다. 연사는 초당 발사 회차의 가산값입니다.")]
        private WeaponStats statContribution;
        [SerializeField, HideInInspector] private int moduleSlot;
        [SerializeField, HideInInspector] private GameObject sourcePrefab;
        public WeaponStats GetStatContribution() => statContribution.Clamped();
        [FormerlySerializedAs("incomingDirection")]
        [SerializeField, Tooltip("4방향 분할의 출력 평면 법선. 입력면 제한과 무관한 배치 방향입니다.")]
        private Vector3Int orientationNormal = Vector3Int.forward;
        private int paidPurchaseCost;
        public int PaidPurchaseCost => paidPurchaseCost;
        private int paidModuleCreditCost;
        public int PaidModuleCreditCost => paidModuleCreditCost;
        public void RecordModuleCreditCost(int amount) => paidModuleCreditCost = Mathf.Max(0, amount);
        public int TakeModuleCreditCost()
        {
            int amount = paidModuleCreditCost;
            paidModuleCreditCost = 0;
            return amount;
        }
        public void RecordPurchaseCost(int amount) => paidPurchaseCost = Mathf.Max(0, amount);
        public int TakePurchaseCost()
        {
            int amount = paidPurchaseCost;
            paidPurchaseCost = 0;
            return amount;
        }
        public BlockKind Kind => kind;
        public int ModuleSlot => moduleSlot;
        public GameObject SourcePrefab => sourcePrefab;
        public Vector3Int OrientationNormal => orientationNormal;
        public Vector3Int Cell => Vector3Int.RoundToInt(transform.localPosition);

        // Scope = sight only: no energy passes through, so no shot ever leaves from it. Its arrow still sets the view.
        public bool IsSight
        {
            get
            {
                ModuleStats stats = GetComponent<ModuleStats>();
                return stats != null && stats.Data != null && stats.Data.ModuleType == StatType.Scope;
            }
        }

        public Vector3Int[] GetOutputDirections()
        {
            if (kind == BlockKind.Blocker || IsSight) return System.Array.Empty<Vector3Int>();
            SplitModule split = GetComponent<SplitModule>();
            if (split != null) return split.GetOutputDirections();
            ExtensionModule extension = GetComponent<ExtensionModule>();
            return extension != null ? new[] { extension.OutputDirection } : new Vector3Int[0];
        }

        public bool HasOutput(Vector3Int direction)
            => System.Array.IndexOf(GetOutputDirections(), direction) >= 0;

        // Energy modules take energy only through the face they were attached by (travel == orientationNormal).
        // Blockers, scopes and triggers keep the old rule: any face that is not an output.
        public bool AcceptsEnergy(Vector3Int travelDirection)
        {
            if (HasOutput(-travelDirection)) return false;
            if (IgnoresInputFace) return true;
            return travelDirection == orientationNormal;
        }

        public bool IgnoresInputFace => GetOutputDirections().Length == 0 || IsSight || GetComponent<TriggerModule>() != null;

        public void Configure(BlockKind value) => kind = value;

        public void SetModuleSource(int slot, GameObject prefab)
        {
            moduleSlot = slot;
            sourcePrefab = prefab;
        }

        public void SetOrientation(Vector3Int normal)
        {
            if (normal.sqrMagnitude != 1) return;
            bool changed = orientationNormal != normal;
            orientationNormal = normal;
            RefreshDraw();
            if (changed) GetComponentInParent<CubeAssembly>()?.NotifyStructureChanged();
        }

        private void OnEnable()
        {
            RefreshDraw();
            GetComponentInParent<EquipableWeapon>()?.IgnorePlayerCollisions();
        }

        public void RefreshDraw()
        {
            if (kind == BlockKind.SplitFour) GetComponent<ModuleDraw>()?.SetDirection(orientationNormal);
        }
    }
}
