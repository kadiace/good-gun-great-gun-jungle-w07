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
            // Instantiate가 모든 활성 부품의 Awake를 마친 뒤 적 초기화 경로에서 기준을 고정한다.
            CaptureInitialParts();
            Spawned?.Invoke(this);
        }
        [SerializeField] private GameObject enemyRoot;
        [SerializeField, Min(0)] private int slotTicketReward = 1;
        [SerializeField] private RaidTicketPickup ticketPickupPrefab;
        public int SlotTicketReward => IsRaidEnemy ? slotTicketReward : 0;
        public void ReleaseTicketVisuals(FirstPersonPlayer player)
        {
            if (ticketPickupPrefab == null || player == null) return;
            for (int index = 0; index < SlotTicketReward; index++)
            {
                var pickup = Instantiate(ticketPickupPrefab, MarkerPosition + Vector3.up * 0.5f, Quaternion.identity);
                float angle = index * Mathf.PI * 2f / SlotTicketReward;
                pickup.Release(player, new Vector3(Mathf.Cos(angle) * 2f, 3f, Mathf.Sin(angle) * 2f));
            }
        }
        [SerializeField, Range(0f, 1f), Tooltip("생성 시 고정한 전체 파괴 가능 셀 중 본체에서 제거된 비율이 이 값 이상이면 사망. 0이면 사용 안 함.")]
        private float destroyAtLostRatio = 0.5f;
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
        private static void ResetRegistry()
        {
            activeCores.Clear();
            findForPartCache.Clear();
        }
        // part → core lookup cache (Transform instanceID keys). enemyRoot never changes at runtime and nothing is
        // re-parented into enemy subtrees after creation, so misses (null) stay valid until a core registers or
        // leaves; positives are re-checked with one IsChildOf so a part that left its enemy returns a fresh answer.
        private static readonly System.Collections.Generic.Dictionary<int, RaidEnemyCore> findForPartCache = new System.Collections.Generic.Dictionary<int, RaidEnemyCore>();
        public static RaidEnemyCore FindForPart(Transform part)
        {
            int partId = part.GetInstanceID();
            if (findForPartCache.TryGetValue(partId, out var cached))
            {
                if (cached == null) return null;
                if (!cached.IsDestroyed && cached.enemyRoot != null && part.IsChildOf(cached.enemyRoot.transform)) return cached;
            }
            RaidEnemyCore match = null;
            foreach (var core in activeCores)
                if (core != null && !core.IsDestroyed && core.enemyRoot != null && part.IsChildOf(core.enemyRoot.transform) &&
                    (match == null || core.enemyRoot.transform.IsChildOf(match.enemyRoot.transform))) match = core;
            findForPartCache[partId] = match;
            return match;
        }
        public bool IsDestroyed { get; private set; }
        public event System.Action Destroyed;
        private void Awake() => health = GetComponent<TargetHealth>();
        private void OnEnable()
        {
            activeCores.Add(this);
            findForPartCache.Clear();
            if (health == null) return;
            health.Destroyed += BreakEnemy;
        }
        private void OnDisable()
        {
            activeCores.Remove(this);
            findForPartCache.Clear();
            if (health == null) return;
            health.Destroyed -= BreakEnemy;
        }

        private const float CheckInterval = 0.2f;
        private DestructibleTarget[] parts;
        private int[] initialCells;
        private long initialCellCount;
        private float checkRemaining;

        // 씬에 직접 배치되어 AssignIdentity를 거치지 않는 대상도 모든 Awake가 끝난 뒤 기준을 고정한다.
        private void Start() => CaptureInitialParts();

        private void CaptureInitialParts()
        {
            if (parts != null || enemyRoot == null) return;
            parts = enemyRoot.GetComponentsInChildren<DestructibleTarget>();
            initialCells = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!parts[i].isActiveAndEnabled) continue;
                initialCells[i] = Mathf.Max(0, parts[i].InitialCellCount);
                initialCellCount += initialCells[i];
            }
            checkRemaining = CheckInterval;
        }

        private void Update()
        {
            if (IsDestroyed || destroyAtLostRatio <= 0f || enemyRoot == null || initialCellCount <= 0) return;
            checkRemaining -= Time.deltaTime;
            if (checkRemaining > 0f) return;
            checkRemaining = CheckInterval;
            if (HasReachedLossThreshold()) BreakByLoss();
        }

        private bool HasReachedLossThreshold()
        {
            long attached = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part != null && part.transform.IsChildOf(enemyRoot.transform))
                    attached += Mathf.Clamp(part.AttachedCellCount, 0, initialCells[i]);
            }
            // 정수 셀 수로 비교해 정확히 50%인 경우와 홀수 총량의 경계를 보존한다.
            long requiredLoss = (long)System.Math.Ceiling(initialCellCount * (double)destroyAtLostRatio);
            return initialCellCount - attached >= requiredLoss;
        }

        private void BreakByLoss()
        {
            if (IsDestroyed || health == null || health.IsDead) return;
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
            if (IsRaidEnemy) Killed?.Invoke(this);
            Destroyed?.Invoke();
            if (enemyRoot == null) return;
            foreach (var part in enemyRoot.GetComponentsInChildren<DestructibleTarget>())
                part.BreakAll(transform.position, Vector3.down);
            // 셀/파편 처리는 기존 예산을 따르되, 사라진 적이 충돌체 갱신을 기다리며 길을 막지 않게 한다.
            foreach (var collider in enemyRoot.GetComponentsInChildren<Collider>(true))
                if (collider.enabled) collider.enabled = false;
            // Keep the debris owners alive until their existing fade finishes.
            // 코어는 TargetHealth.Die에서 곧바로 꺼지므로 삭제 대기는 루트에 붙인 EnemyWreckRemoval이 맡는다.
            if (enemyRoot != gameObject && enemyRoot.activeInHierarchy)
                enemyRoot.AddComponent<EnemyWreckRemoval>().Begin(DebrisRemovalDelay);
            else Destroy(enemyRoot, DebrisRemovalDelay);
        }

        private const float DebrisRemovalDelay = 12f;

        internal static void PrepareRootForDestroy(GameObject root)
        {
            var bodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].automaticCenterOfMass = false;
                bodies[i].automaticInertiaTensor = false;
            }
        }

        // 공방 복귀 언로드 전에 호출해 씬에 남은 적 잔해 전체를 삭제 준비 상태로 바꾼다.
        public static void PrepareAllForUnload()
        {
            for (int i = 0; i < activeCores.Count; i++)
            {
                var core = activeCores[i];
                if (core == null || core.enemyRoot == null) continue;
                PrepareRootForDestroy(core.enemyRoot);
            }
        }
    }
}
