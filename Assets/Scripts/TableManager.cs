using UnityEngine;

namespace WeaponWorkshop
{
    // 실행 초기에 한 번 읽어 유지하는 참조 테이블 보관소.
    // 씬·프리팹 연결이나 스크립터블오브젝트 없이 인스턴스가 테이블을 들고 있고, 게임 코드는 조회 로직 클래스로 읽는다.
    //
    // 규칙: 파일(Resources/X.json) → 순수 직렬화 자료(XData) → TableManager가 1회 로드·보유.
    //       테이블을 읽는 로직은 XInfo 같은 별도 정적 클래스에 두고, 데이터 클래스에는 필드만 남긴다.
    //       프로퍼티명은 클래스명에서 Data를 뺀 이름(RaidMapData → RaidMap)을 쓴다.
    public sealed class TableManager
    {
        // Resources 안의 테이블 경로. 확장자는 붙이지 않는다.
        public const string RaidMapResource = "RaidMapData";

        private static TableManager instance;

        /// <summary>초기화된 보관소. 초기화 전에 읽어도 한 번 채우고 되돌린다.</summary>
        public static TableManager Instance => instance ?? Initialize();

        /// <summary>습격 스테이지 테이블. 로드 실패 시 빈 표이며 원인은 이미 로그로 남았다.</summary>
        public RaidMapData RaidMap { get; private set; }
        public GunShapeData GunShape { get; private set; }
        public CustomerRequestData CustomerRequest { get; private set; }
        public ShootingTestTableData ShootingTestTable { get; private set; }
        public CustomerOrderData CustomerOrder { get; private set; }
        public CustomerSoundData CustomerSound { get; private set; }
        public SoundData Sound { get; private set; }

        private TableManager()
        {
            RaidMap = Load<RaidMapData>(RaidMapResource);
            RaidStageInfo.Validate(RaidMap, RaidMapResource);
            ShootingTestTable = Load<ShootingTestTableData>(nameof(ShootingTestTableData));
            ShootingTestInfo.Validate(ShootingTestTable);
            GunShape = Load<GunShapeData>(nameof(GunShapeData));
            GunShapeInfo.Validate(GunShape);
            CustomerRequest = Load<CustomerRequestData>(nameof(CustomerRequestData));
            CustomerRequestInfo.Validate(CustomerRequest, ShootingTestTable, GunShape);
            CustomerOrder = Load<CustomerOrderData>(nameof(CustomerOrderData));
            CustomerOrderInfo.Validate(CustomerOrder, CustomerRequest);
            CustomerSound = Load<CustomerSoundData>(nameof(CustomerSoundData));
            CustomerSoundInfo.Validate(CustomerSound);
            // 사운드 규칙과 곡 경로도 기존 테이블 저장 방식을 따른다.
            Sound = Load<SoundData>(nameof(SoundData));
        }

        /// <summary>반복 호출해도 테이블을 다시 읽지 않는다.</summary>
        public static TableManager Initialize()
        {
            if (instance == null) instance = new TableManager();
            return instance;
        }
#if UNITY_EDITOR
        public static void ReloadAfterTableEdit() { instance = null; Initialize(); }
#endif

        // 테이블 파일을 한 번 읽는다. 실패하면 빈 객체를 주고 이유를 한 번만 남긴다.
        private static T Load<T>(string resourcePath) where T : class, new()
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogError($"테이블 {resourcePath}를 Resources에서 찾지 못했습니다. 빈 테이블로 동작합니다.");
                return new T();
            }
            T table;
            // 사운드 표를 비롯해 JSON이 손상된 경우에도 빈 표와 기본곡 폴백으로 진행한다.
            try { table = JsonUtility.FromJson<T>(asset.text); }
            catch (System.ArgumentException exception)
            {
                Debug.LogError($"테이블 {resourcePath}의 JSON을 읽지 못했습니다: {exception.Message}");
                return new T();
            }
            if (table == null)
            {
                Debug.LogError($"테이블 {resourcePath}의 형식을 읽지 못했습니다. 빈 테이블로 동작합니다.");
                return new T();
            }
            return table;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => Initialize();

        // 에디터 재생마다 정적 잔재를 정리한다(프로젝트 관례: SoundManager, NightRaidTransfer).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => instance = null;
    }
}
