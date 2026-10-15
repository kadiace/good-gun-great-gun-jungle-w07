using UnityEngine;

namespace WeaponWorkshop
{
    [CreateAssetMenu(menuName = "Weapon Workshop/Shooting Test")]
    [System.Obsolete("레거시 테스트 프리팹 바인딩. 시험 정의는 TableManager.ShootingTestTable에서 조회하세요.")]
    public sealed class ShootingTestData : ScriptableObject
    {
        [SerializeField] private string testId;
        [SerializeField, HideInInspector] private string displayName;
        [SerializeField, HideInInspector] private string instructions;
        [SerializeField, HideInInspector] private string failureHint;
        [SerializeField] private GameObject layoutPrefab;
        [SerializeField, HideInInspector] private float timeLimit;
        [SerializeField, HideInInspector] private float requiredDestruction;
        public string Id => testId;
        public ShootingTestTableData.Entry Definition => TableManager.Instance.ShootingTestTable.Find(testId);
        public bool IsAvailable => layoutPrefab != null && TableManager.Instance.ShootingTestTable.IsAvailable(testId);
        public string DisplayName => Definition?.name ?? "미등록 사격 테스트";
        public string Instructions => Definition?.instructions ?? "";
        public string FailureHint => Definition?.failureHint ?? "";
        public GameObject LayoutPrefab => layoutPrefab;
        public float TimeLimit => Definition?.timeLimit ?? 0f;
        public float RequiredDestruction => Definition?.requiredDestruction ?? 0f;
    }
}
