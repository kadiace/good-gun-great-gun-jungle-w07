using System;
using UnityEngine;

namespace WeaponWorkshop
{
    // 시험 값은 표에서 읽고, Unity 자산 참조만 연결한다. 배열 위치는 시험 ID가 아니다.
    [Serializable]
    public sealed class ShootingTestBinding
    {
        [SerializeField] private string testId;
        [SerializeField] private GameObject layoutPrefab;

        public string Id => testId;
        public GameObject LayoutPrefab => layoutPrefab;
        public ShootingTestTableData.Entry Definition => TableManager.Instance.ShootingTestTable.Find(testId);
        public bool IsAvailable => layoutPrefab != null && TableManager.Instance.ShootingTestTable.IsAvailable(testId);
        public string DisplayName => Definition?.name ?? "미등록 사격 테스트";
        public string Instructions => Definition?.instructions ?? "";
        public string FailureHint => Definition?.failureHint ?? "";
        public float TimeLimit => Definition?.timeLimit ?? 0f;
        public float RequiredDestruction => Definition?.requiredDestruction ?? 0f;
    }
}
