using UnityEngine;

namespace WeaponWorkshop
{
    // 무엇을 잴지: 발사 경로 기준 스탯/모듈 수, 총 전체 스탯, 총 전체 스탯 모듈 수.
    public enum GunMeasure
    {
        MuzzleStat,   // 발사되는 총구 중 가장 높은 스탯 (코어 기본 + 그 경로의 모듈). 관통·연사 등
        WeaponStat,   // 총 전체 스탯 (작업대 표시와 같은 값). 무게 등
        PathModules,  // 발사 경로 하나에 달린 해당 모듈 최대 개수 (ModuleCounter.MaxPathCount)
        TotalModules, // 총에 달린 스탯 모듈 전체 개수 (stat 무시)
        SplitTwoBlocks,  // 총에 달린 2방향 분할 블록 개수 (stat 무시)
        SplitFourBlocks, // 총에 달린 4방향 분할 블록 개수 (stat 무시)
        Muzzles          // 실제로 발사되는 총구(경로) 수 (stat 무시)
    }

    [System.Serializable]
    public sealed class GunCondition
    {
        public GunMeasure measure;
        public StatType stat;
        public float min = 0f;
        public float max = 999f;

        public bool IsMet(EquipableWeapon weapon)
        {
            float value = Measure(weapon, measure, stat);
            return value >= min && value <= max;
        }

        public static float Measure(EquipableWeapon weapon, GunMeasure measure, StatType stat)
        {
            if (weapon == null) return 0f;
            switch (measure)
            {
                case GunMeasure.PathModules:
                    return ModuleCounter.MaxPathCount(weapon, stat);
                case GunMeasure.Muzzles:
                {
                    CubeAssembly assembly = weapon.GetComponentInChildren<CubeAssembly>(true);
                    return assembly != null ? assembly.GetProjectilePaths().Count : 0;
                }
                case GunMeasure.SplitTwoBlocks:
                    return CountBlocks(weapon, BlockKind.SplitTwo);
                case GunMeasure.SplitFourBlocks:
                    return CountBlocks(weapon, BlockKind.SplitFour);
                case GunMeasure.TotalModules:
                    int total = 0;
                    foreach (var entry in ModuleCounter.TotalCounts(weapon.GetComponentInChildren<CubeAssembly>(true)))
                        if (ModuleCounter.IsStatModule(entry.Key)) total += entry.Value;
                    return total;
                case GunMeasure.WeaponStat:
                    {
                        PowerCore core = weapon.GetComponentInChildren<PowerCore>(true);
                        return core != null ? core.GetCurrentStats().Get(stat) : 0f;
                    }
                default:
                    {
                        PowerCore core = weapon.GetComponentInChildren<PowerCore>(true);
                        CubeAssembly assembly = weapon.GetComponentInChildren<CubeAssembly>(true);
                        if (core == null) return 0f;
                        var paths = assembly != null ? assembly.GetProjectilePaths() : null;
                        if (paths == null) return core.GetMuzzleStats(new PlacedBlock[0]).Get(stat);
                        float best = 0f;
                        foreach (var muzzle in paths) best = Mathf.Max(best, core.GetMuzzleStats(muzzle.path).Get(stat));
                        return best;
                    }
            }
        }

        private static int CountBlocks(EquipableWeapon weapon, BlockKind kind)
        {
            CubeAssembly assembly = weapon != null ? weapon.GetComponentInChildren<CubeAssembly>(true) : null;
            if (assembly == null) return 0;
            int count = 0;
            foreach (PlacedBlock block in assembly.GetComponentsInChildren<PlacedBlock>())
                if (block.transform.parent == assembly.transform && block.Kind == kind) count++;
            return count;
        }

        public static bool AllMet(GunCondition[] conditions, EquipableWeapon weapon)
        {
            if (conditions == null) return true;
            foreach (GunCondition condition in conditions)
                if (!condition.IsMet(weapon)) return false;
            return true;
        }
    }

    [CreateAssetMenu(menuName = "Weapon Workshop/Customer Profile")]
    public sealed class CustomerProfile : ScriptableObject
    {
        [SerializeField, HideInInspector] private string profileId;
        public string ProfileId => profileId;
        [field: System.NonSerialized] public CustomerOrderState OrderState { get; internal set; }
        [Tooltip("이 손님이 첫 차례인 영업 시작 뒤 실행할 안내 데이터")]
        public TutorialSequence callTutorialSequence;
        [Tooltip("처음 주문을 다 들은 뒤 실행할 안내 데이터")]
        public TutorialSequence orderTutorialSequence;
        [Tooltip("원하는 총 종류 (권총, 산탄총...). 전부 만족해야 함. 최소조건만 보므로 다른 모듈을 더 달아도 통과")]
        [System.Obsolete("레거시 요청 참조. requestIds와 TableManager.CustomerRequest를 사용하세요."), HideInInspector]
        public GunArchetype[] archetypes;
        [Tooltip("종류 외 추가 조건")]
        [System.Obsolete("레거시 요청 조건. requestIds와 TableManager.CustomerRequest를 사용하세요."), HideInInspector]
        public GunCondition[] conditions;
        [Tooltip("비우면 종의 기본 손님 프리팹 사용")] public Customer prefab;
        [Tooltip("손님 그림. 비우면 프리팹의 기본 그림")] public Sprite portrait;

        // 주문 값(요청·대사·보상)은 주문 표(TableManager.CustomerOrder)의 손님 행(profileId)에서 읽는다.
        // 무한 모드 조합·저장 복원 손님은 행이 없으므로 런타임 값을 쓰며, 값을 바꾸면 이 손님만의 복사본이 생긴다.
        [System.NonSerialized] private CustomerOrderData.Guest runtimeOrder;
        private static readonly CustomerOrderData.Guest NoOrder = new CustomerOrderData.Guest();
        private CustomerOrderData.Guest Order => runtimeOrder ?? TableManager.Instance.CustomerOrder.FindGuest(profileId) ?? NoOrder;
        private CustomerOrderData.Guest Writable => runtimeOrder ??= CustomerOrderInfo.Copy(Order == NoOrder ? null : Order);
        public string[] RequestIds { get => Order.requestIds ?? System.Array.Empty<string>(); set => Writable.requestIds = value ?? System.Array.Empty<string>(); }
        public string[] Lines { get => Order.lines ?? System.Array.Empty<string>(); set => Writable.lines = value ?? System.Array.Empty<string>(); }
        public bool EndsClear { get => Order.endsClear; set => Writable.endsClear = value; }
        public string HappyLine { get => Order.happyLine; set => Writable.happyLine = value; }
        public string AngryLine { get => Order.angryLine; set => Writable.angryLine = value; }
        public int Price { get => Order.price; set => Writable.price = value; }
        public int UnlockDay => Order.unlockDay;
        public int SaleBonusCount() => Order.SaleBonusCount();
        // Instantiate는 직렬화 필드만 복사하므로 런타임 주문 값은 여기서 옮긴다.
        public void CopyRuntimeOrderFrom(CustomerProfile source)
            => runtimeOrder = source != null && source.runtimeOrder != null ? CustomerOrderInfo.Copy(source.runtimeOrder) : null;

        public string Reaction(bool satisfied)
        {
            string line = satisfied ? HappyLine : AngryLine;
            return !string.IsNullOrEmpty(line) ? line : satisfied ? "좋네요! 감사합니다." : "이거 제가 말한 거 아닌데요...";
        }

        public bool Check(EquipableWeapon weapon)
        {
            return TableManager.Instance.CustomerRequest.MeetsOrder(RequestIds, weapon);
        }
    }
}
