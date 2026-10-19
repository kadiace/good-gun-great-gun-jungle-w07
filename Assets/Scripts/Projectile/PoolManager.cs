using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace WeaponWorkshop
{
    // Scene-owned pools. Retention limits affect idle storage, never the number of live shots.
    [DefaultExecutionOrder(-999)]
    public sealed class PoolManager : MonoBehaviour
    {
        private static readonly ProfilerMarker ReturnCachedChildrenMarker = new ProfilerMarker("PoolManager.ReturnCachedChildren");
        [Serializable] public sealed class PrefabPool
        {
            public GameObject prefab;
            [Min(0)] public int prewarm = 8;
            [Min(0)] public int maximumRetained = 64;
        }
        [SerializeField] private PrefabPool[] prefabPools = Array.Empty<PrefabPool>();
        [SerializeField, Min(0)] private int explosionPrewarm = 256;
        [SerializeField, Min(0)] private int gravityPrewarm = 32;
        [SerializeField, Min(0)] private int maximumRetainedWaves = 256;
        [SerializeField, Min(0)] private int defaultMaximumRetained = 256;
        [SerializeField, Min(0), Tooltip("폭발 파티클 풀의 최소 프리웜 수. 기존 풀 설정이 더 낮아도 이 수까지 확보합니다.")]
        private int explosionEffectPrewarm = 128;
        [SerializeField, Min(1), Tooltip("연쇄 기폭으로 한 프레임에 생성할 수 있는 추가 폭발 수.")]
        private int maximumChainDetonationsPerFrame = 64;
        [SerializeField, Min(0), Tooltip("한 프레임에 재생할 수 있는 폭발 파티클 효과 수.")]
        private int maximumExplosionEffectsPerFrame = 32;
        [SerializeField, Min(0)] private int debrisPrewarm = 16;
        [SerializeField, Min(0)] private int maximumRetainedDebris = 64;
        [SerializeField, Min(0), Tooltip("파편 컴포넌트 하나당 재사용할 낙하 덩어리와 메시의 최대 대기 개수.")]
        private int maximumCachedDebrisGroups = 8;
        [SerializeField, Min(0f)] private float debrisRollingFriction = 3f;
        [SerializeField, Range(0f,1f)] private float debrisBounceMultiplier = 0.4f;
        [SerializeField, Range(0f,1f)] private float debrisMotionMultiplier = 0.65f;
        [SerializeField, Min(0f), Tooltip("파편 생성 시 정적 벽을 한 번 수집할 주변 거리(m). 박스 콜라이더의 위치와 크기를 캐시합니다.")]
        private float debrisWallSearchRadius = 12f;
        [SerializeField] private int activeCount, cachedCount;
        private static PoolManager instance;
        private Transform storage;
        private bool prepared;
        internal sealed class Pool
        {
            public GameObject prefab;
            public Type componentType;
            public int maximumRetained;
            public int prewarmTarget;
            public readonly Stack<PooledCombatObject> idle = new Stack<PooledCombatObject>();
        }
        private readonly Dictionary<GameObject, Pool> prefabs = new Dictionary<GameObject, Pool>();
        private readonly Dictionary<Type, Pool> components = new Dictionary<Type, Pool>();
        private readonly HashSet<PooledCombatObject> active = new HashSet<PooledCombatObject>();
        private readonly HashSet<VisualDebrisCloud> activeDebris = new HashSet<VisualDebrisCloud>();
        private int explosionBudgetFrame = -1;
        private int chainDetonationsThisFrame, explosionEffectsThisFrame;
        private readonly List<DestructibleTarget> explosionDirty = new List<DestructibleTarget>();
        private readonly HashSet<DestructibleTarget> explosionDirtySet = new HashSet<DestructibleTarget>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void PrepareAtSceneStart() => Ensure();
        private int wallCheckFrame = -1, wallChecks;
        [SerializeField, Min(0), Tooltip("전체 파편의 프레임당 벽 충돌 검사 한도. 파편별로 순환 검사하며 물리 엔진 호출은 하지 않습니다.")]
        private int maximumDebrisWallChecksPerFrame = 512;
        public static int WallChecksPerCloud => instance == null ? 0 : Mathf.Min(64,
            instance.maximumDebrisWallChecksPerFrame / Mathf.Max(1,instance.activeDebris.Count));
        public static bool TryWallCheck()
        {
            if(instance==null)return false;
            if(instance.wallCheckFrame!=Time.frameCount){instance.wallCheckFrame=Time.frameCount;instance.wallChecks=0;}
            if(instance.wallChecks>=instance.maximumDebrisWallChecksPerFrame)return false;
            instance.wallChecks++;return true;
        }
        public int ActiveCount => activeCount;
        public int CachedCount => cachedCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => instance = null;
        public static PoolManager Ensure(PoolManager preferred = null)
        {
            if (instance != null) return instance;
            // 레거시 테이블: Resources 경로로 프리팹을 찾는다. 매니저 연결은 씬·프리팹 배치가 정본이다.
            if (preferred == null) preferred = Resources.Load<PoolManager>("PoolManager");
            instance = FindFirstObjectByType<PoolManager>();
            if (instance == null)
                instance = preferred != null ? Instantiate(preferred)
                    : new GameObject("PoolManager").AddComponent<PoolManager>();
            instance.Prepare();
            return instance;
        }
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            gameObject.name = "PoolManager";
            Prepare();
        }
        private void Prepare()
        {
            if (prepared) return;
            prepared = true;
            maximumChainDetonationsPerFrame = Mathf.Max(1, maximumChainDetonationsPerFrame);
            storage = new GameObject("Inactive").transform;
            storage.SetParent(transform, false);
            storage.gameObject.SetActive(false);
            foreach (var entry in prefabPools)
            {
                if (entry == null || entry.prefab == null || prefabs.ContainsKey(entry.prefab)) continue;
                var pool = new Pool { prefab = entry.prefab, maximumRetained = Mathf.Max(0, entry.maximumRetained),
                    prewarmTarget = Mathf.Max(0, entry.prewarm) };
                prefabs.Add(entry.prefab, pool);
                Prewarm(pool, entry.prewarm);
            }
            int effectiveExplosionPrewarm = Mathf.Max(explosionPrewarm, 256);
            maximumRetainedWaves = Mathf.Max(maximumRetainedWaves, effectiveExplosionPrewarm);
            Prewarm(ComponentPool(typeof(ExpandingExplosion)), effectiveExplosionPrewarm);
            Prewarm(ComponentPool(typeof(GravityField)), gravityPrewarm);
            Prewarm(ComponentPool(typeof(VisualDebrisCloud)), debrisPrewarm);
        }
        private Pool ComponentPool(Type type)
        {
            if (!components.TryGetValue(type, out var pool))
            {
                pool = new Pool { componentType = type, maximumRetained = Mathf.Max(0, type == typeof(VisualDebrisCloud) ? maximumRetainedDebris : maximumRetainedWaves) };
                components.Add(type, pool);
            }
            return pool;
        }
        private void Prewarm(Pool pool, int count)
        {
            for (int i = 0; i < Mathf.Min(Mathf.Max(0, count), pool.maximumRetained); i++)
            { pool.idle.Push(Create(pool)); cachedCount++; }
        }
        private PooledCombatObject Create(Pool pool)
        {
            GameObject go;
            if (pool.prefab != null) go = Instantiate(pool.prefab, storage);
            else
            {
                go = new GameObject(pool.componentType.Name);
                go.SetActive(false);
                go.transform.SetParent(storage, false);
                go.AddComponent(pool.componentType);
            }
            go.SetActive(false);
            var member = go.GetComponent<PooledCombatObject>();
            if (member == null) member = go.AddComponent<PooledCombatObject>();
            member.Initialize(this, pool);
            var debris = member.DebrisCloud;
            if (debris != null) debris.ReserveBuffers(64, maximumCachedDebrisGroups);
            return member;
        }
        private PooledCombatObject Rent(Pool pool, Vector3 position, Quaternion rotation, Transform parent)
        {
            PooledCombatObject member = null;
            while (pool.idle.Count > 0 && member == null)
            { member = pool.idle.Pop(); cachedCount = Mathf.Max(0, cachedCount - 1); }
            if (member == null) member = Create(pool);
            member.transform.SetParent(parent != null ? parent : transform, false);
            if (parent != null && parent.TryGetComponent<PooledCombatObject>(out var parentMember))
                parentMember.RegisterPooledChild(member);
            member.transform.SetPositionAndRotation(position, rotation);
            member.ResetForRent();
            var debris = member.DebrisCloud;
            if(debris != null) { debris.ConfigureMotion(debrisRollingFriction,debrisBounceMultiplier,debrisMotionMultiplier,debrisWallSearchRadius); activeDebris.Add(debris); }
            active.Add(member);
            activeCount = active.Count;
            return member;
        }
        // Inactive on return from Rent: configure all state before activation.
        public static GameObject RentPrefab(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null) return null;
            var manager = Ensure();
            if (!manager.prefabs.TryGetValue(prefab, out var pool))
            {
                pool = new Pool { prefab = prefab, maximumRetained = Mathf.Max(0, manager.defaultMaximumRetained) };
                manager.prefabs.Add(prefab, pool);
            }
            return manager.Rent(pool, position, rotation, parent).gameObject;
        }

        internal static void EnsureExplosionEffectPrewarm(GameObject prefab)
        {
            if (prefab == null) return;
            var manager = Ensure();
            if (!manager.prefabs.TryGetValue(prefab, out var pool))
            {
                pool = new Pool { prefab = prefab, maximumRetained = Mathf.Max(0, manager.defaultMaximumRetained) };
                manager.prefabs.Add(prefab, pool);
            }
            manager.EnsurePrewarm(pool, manager.explosionEffectPrewarm);
        }

        // 탄 임팩트처럼 첫 사용 프레임에 한꺼번에 대여되는 프리팹을 미리 만든다(무기 활성화 때 호출).
        internal static void EnsurePrefabPrewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;
            var manager = Ensure();
            if (!manager.prefabs.TryGetValue(prefab, out var pool))
            {
                pool = new Pool { prefab = prefab, maximumRetained = Mathf.Max(0, manager.defaultMaximumRetained) };
                manager.prefabs.Add(prefab, pool);
            }
            manager.EnsurePrewarm(pool, count);
        }

        private void EnsurePrewarm(Pool pool, int count)
        {
            count = Mathf.Max(0, count);
            if (pool.prewarmTarget >= count) return;
            int activeForPool = 0;
            foreach (var member in active)
                if (member != null && member.OwnerPool == pool) activeForPool++;
            int currentCount = pool.idle.Count + activeForPool;
            pool.maximumRetained = Mathf.Max(pool.maximumRetained, count);
            Prewarm(pool, Mathf.Max(0, count - currentCount));
            pool.prewarmTarget = count;
        }

        internal static bool TryConsumeChainDetonation()
        {
            if (instance == null) Ensure();
            instance.ResetExplosionBudgets();
            if (instance.chainDetonationsThisFrame >= instance.maximumChainDetonationsPerFrame) return false;
            instance.chainDetonationsThisFrame++;
            return true;
        }

        internal static bool TryUseExplosionEffectSlot()
        {
            if (instance == null) Ensure();
            instance.ResetExplosionBudgets();
            if (instance.explosionEffectsThisFrame >= instance.maximumExplosionEffectsPerFrame) return false;
            instance.explosionEffectsThisFrame++;
            return true;
        }

        private void ResetExplosionBudgets()
        {
            if (explosionBudgetFrame == Time.frameCount) return;
            explosionBudgetFrame = Time.frameCount;
            chainDetonationsThisFrame = explosionEffectsThisFrame = 0;
        }

        public static T RentComponent<T>(Vector3 position) where T : MonoBehaviour
        {
            var manager = Ensure();
            return manager.Rent(manager.ComponentPool(typeof(T)), position, Quaternion.identity, null).GetComponent<T>();
        }
        public static void Release(GameObject go)
        {
            if (go == null) return;
            var member = go.GetComponent<PooledCombatObject>();
            if (member != null && member.Manager != null) member.Manager.Return(member);
            else { go.SetActive(false); Destroy(go); }
        }
        internal static void QueueExplosionFinish(HashSet<DestructibleTarget> targets)
        {
            if (targets.Count == 0) return;
            var manager = Ensure();
            foreach (var target in targets)
                if (target != null && manager.explosionDirtySet.Add(target)) manager.explosionDirty.Add(target);
        }

        internal static void QueueCollisionFinish(DestructibleTarget target)
        {
            if (target == null) return;
            var manager = Ensure();
            // Projectile casts use current cell data; rebuild colliders at a batch boundary.
            if (manager.explosionDirtySet.Add(target)) manager.explosionDirty.Add(target);
        }

        internal static void FlushExplosionFinish()
        {
            if (instance == null || instance.explosionDirty.Count == 0) return;
            DestructibleTarget.FinishExplosionStep(instance.explosionDirty);
            instance.explosionDirty.Clear();
            instance.explosionDirtySet.Clear();
        }

        private void Update()
        {
            FlushExplosionFinish();
            ExpandingExplosion.ProcessPendingChainDetonations();
        }

        private void FixedUpdate() => FlushExplosionFinish();

        private void LateUpdate()
        {
            ExpandingExplosion.FlushPending();
            // Last-shot changes must reach physics even if no later shot queries this frame.
            FlushExplosionFinish();
        }

        private void Return(PooledCombatObject member)
        {
            if (!active.Remove(member)) return;
            Transform previousParent = member.transform.parent;
            if (previousParent != null && previousParent.TryGetComponent<PooledCombatObject>(out var parentMember))
                parentMember.UnregisterPooledChild(member);
            var debris = member.DebrisCloud;
            if(debris != null) activeDebris.Remove(debris);
            // Owned child effects return first, so trails/lenses cannot accumulate on a reused root.
            var children = member.PooledChildren;
            using (ReturnCachedChildrenMarker.Auto())
            {
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    var child = children[i];
                    if (child != null && child.transform.parent == member.transform && child.Manager == this) Return(child);
                }
            }
            member.ResetForReturn();
            member.gameObject.SetActive(false);
            member.transform.SetParent(storage, false);
            var pool = member.OwnerPool;
            if (pool.idle.Count < pool.maximumRetained)
            { pool.idle.Push(member); cachedCount++; }
            else Destroy(member.gameObject);
            activeCount = active.Count;
        }
        public static void ClearActive()
        {
            if (instance == null) return;
            ExpandingExplosion.ClearPendingRequests();
            var snapshot = new PooledCombatObject[instance.active.Count];
            instance.active.CopyTo(snapshot);
            foreach (var member in snapshot) if (member != null) instance.Return(member);
        }
        private void OnDestroy()
        {
            if (instance != this) return;
            ExpandingExplosion.ClearPendingRequests();
            if (explosionDirty.Count > 0) DestructibleTarget.FinishExplosionStep(explosionDirty);
            instance = null;
        }
    }
}
