using UnityEngine;

namespace WeaponWorkshop
{
    public enum RaidEnemyType { None, Helicopter, Tank }

    [RequireComponent(typeof(TargetHealth))]
    public sealed class RaidEnemyCore : MonoBehaviour
    {
        public static event System.Action<RaidEnemyCore> Spawned;
        public static event System.Action<RaidEnemyCore> Killed;
        public string EnemyId { get; private set; }
        public RaidEnemyType EnemyType { get; private set; }
        public void AssignIdentity(string id, RaidEnemyType type)
        {
            EnemyId = id;
            EnemyType = type;
            Spawned?.Invoke(this);
        }
        [SerializeField] private GameObject enemyRoot;
        [SerializeField, Range(0f, 1f), Tooltip("적 전체(enemyRoot 아래 모든 파괴 가능 부위)의 큐브가 이 비율 이상 떨어져 나가면 파괴. 0이면 사용 안 함.")]
        private float destroyAtLostRatio;
        [SerializeField, Tooltip("큐브 손실 임계값으로 파괴되는 순간 적 중앙에서 한 번 터지는 폭발 연출(피해 없음).")]
        private GameObject breakBurstPrefab;
        [SerializeField, Min(0.01f)] private float breakBurstScale = 1.5f;
        private TargetHealth health;
        private static readonly System.Collections.Generic.List<RaidEnemyCore> activeCores = new System.Collections.Generic.List<RaidEnemyCore>();
        public static int ActiveCount => activeCores.Count;
        public static RaidEnemyCore ActiveAt(int index) => index >= 0 && index < activeCores.Count ? activeCores[index] : null;
        public bool IsRaidEnemy => enemyRoot != null &&
            (enemyRoot.GetComponent<RaidHelicopter>() != null || enemyRoot.GetComponent<RaidTank>() != null);
        public string MarkerName => enemyRoot == null ? "적" :
            enemyRoot.GetComponent<RaidHelicopter>() != null ? "헬기" :
            enemyRoot.GetComponent<RaidTank>() != null ? "탱크" : "적";
        public Vector3 MarkerPosition => enemyRoot != null ? enemyRoot.transform.position : transform.position;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => activeCores.Clear();
        public static RaidEnemyCore FindForPart(Transform part)
        {
            RaidEnemyCore match = null;
            foreach (var core in activeCores)
                if (core != null && !core.IsDestroyed && core.enemyRoot != null && part.IsChildOf(core.enemyRoot.transform) &&
                    (match == null || core.enemyRoot.transform.IsChildOf(match.enemyRoot.transform))) match = core;
            return match;
        }
        public bool IsDestroyed { get; private set; }
        public event System.Action Destroyed;
        private void Awake() => health = GetComponent<TargetHealth>();
        private void OnEnable()
        {
            activeCores.Add(this);
            if (health == null) return;
            health.Destroyed += BreakEnemy;
        }
        private void OnDisable()
        {
            activeCores.Remove(this);
            if (health == null) return;
            health.Destroyed -= BreakEnemy;
        }

        private const float CheckInterval = 0.2f;
        private DestructibleTarget[] parts;
        private int[] initialCells;
        private float checkRemaining;

        private void Update()
        {
            if (IsDestroyed || destroyAtLostRatio <= 0f || enemyRoot == null) return;
            checkRemaining -= Time.deltaTime;
            if (checkRemaining > 0f) return;
            checkRemaining = CheckInterval;
            if (LostRatio() >= destroyAtLostRatio) BreakByLoss();
        }

        // Parts subdivide in their own Awake, so the starting cube counts are read on first use.
        private float LostRatio()
        {
            if (parts == null)
            {
                parts = enemyRoot.GetComponentsInChildren<DestructibleTarget>();
                initialCells = new int[parts.Length];
                for (int i = 0; i < parts.Length; i++) initialCells[i] = parts[i].InitialCellCount;
            }
            int initial = 0, attached = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                initial += initialCells[i];
                if (parts[i] != null) attached += Mathf.Min(initialCells[i], parts[i].AttachedCellCount);
            }
            return initial > 0 ? 1f - (float)attached / initial : 0f;
        }

        private void BreakByLoss()
        {
            if (breakBurstPrefab != null)
            {
                var burst = Instantiate(breakBurstPrefab, health.AimPoint, Quaternion.identity, enemyRoot != null ? enemyRoot.transform : null);
                burst.transform.localScale = Vector3.one * breakBurstScale;
            }
            health.Kill();
        }
        private void BreakEnemy()
        {
            if (IsDestroyed) return;
            IsDestroyed = true;
            if (!string.IsNullOrEmpty(EnemyId)) Killed?.Invoke(this);
            Destroyed?.Invoke();
            if (enemyRoot == null) return;
            foreach (var part in enemyRoot.GetComponentsInChildren<DestructibleTarget>())
                part.BreakAll(transform.position, Vector3.down);
            // Keep the debris owners alive until their existing fade finishes.
            Destroy(enemyRoot, 12f);
        }
    }
}
