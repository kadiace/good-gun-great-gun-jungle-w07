using UnityEngine;

namespace WeaponWorkshop
{
    [CreateAssetMenu(menuName = "Weapon Workshop/Shooting Test")]
    public sealed class ShootingTestData : ScriptableObject
    {
        [SerializeField] private string testId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 4)] private string instructions;
        [SerializeField, TextArea(2, 4)] private string failureHint;
        [SerializeField] private GameObject layoutPrefab;
        [SerializeField, Min(1f)] private float timeLimit = 30f;
        [SerializeField, Range(.01f, 1f)] private float requiredDestruction = .7f;
        public string Id => testId;
        public string DisplayName => displayName;
        public string Instructions => instructions;
        public string FailureHint => failureHint;
        public GameObject LayoutPrefab => layoutPrefab;
        public float TimeLimit => timeLimit;
        public float RequiredDestruction => requiredDestruction;
    }
}
