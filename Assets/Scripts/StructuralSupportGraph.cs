using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    // No per-part Rigidbody and no continuously repeated full-scene physics search.
    // Changed contacts are processed in bounded batches; connectivity runs after contacts settle.
    public sealed class StructuralSupportGraph : MonoBehaviour
    {
        private static StructuralSupportGraph instance;
        private readonly HashSet<DestructibleTarget> targets = new HashSet<DestructibleTarget>();
        private readonly HashSet<DestructibleTarget> queued = new HashSet<DestructibleTarget>();
        private readonly Queue<DestructibleTarget> dirty = new Queue<DestructibleTarget>();
        private readonly HashSet<DestructibleTarget> supported = new HashSet<DestructibleTarget>();
        private readonly HashSet<DestructibleTarget> establishedSupport = new HashSet<DestructibleTarget>();
        private readonly Queue<DestructibleTarget> traversal = new Queue<DestructibleTarget>();
        private readonly List<DestructibleTarget> detached = new List<DestructibleTarget>();
        private readonly List<Collider> colliders = new List<Collider>();
        // 스캔을 시작하기 전의 이웃 목록. 스캔이 끝나고 되살아나지 않은 연결만 그 이웃을 다시 훑게 한다.
        private readonly HashSet<DestructibleTarget> previousLinks = new HashSet<DestructibleTarget>();
        private Collider[] hits = new Collider[128];
        private DestructibleTarget scanning;
        private Transform scanningRoot;
        // 스캔 중에 변하지 않는 값들을 히트마다 다시 읽지 않는다.
        private Scene scanningScene;
        private bool scanAnchorStart;
        private int cursor;
        // 맞닿음·고정이 실제로 바뀐 뒤인지 나타낸다. 표적 수에 비례하는 연결 판정을 변화 없는 프레임에서는 건너뛴다.
        private bool graphChanged;
        private bool needsSync;
        private const float ContactTolerance = 0.015f;
        private const int QueryBudget = 64;
        // Milliseconds one frame spends rebuilding contacts; the dirty queue keeps its place and
        // continues next frame. Tunable in one place only: this static field.
        internal static float QueryBudgetMilliseconds = 3f;
        // Resolved once instead of LayerMask.GetMask doing a string lookup for every query.
        // Lazy because GetMask may not run inside a MonoBehaviour's static initializer during load.
        private static int nonEnergyLayers;
        private static int NonEnergyLayers => nonEnergyLayers != 0 ? nonEnergyLayers : nonEnergyLayers = ~LayerMask.GetMask("Energy");

        private void OnEnable()
        {
            // Rebuild registrations after a script reload, including work interrupted by an exception.
            instance = this;
            foreach (var target in FindObjectsByType<DestructibleTarget>(FindObjectsSortMode.None))
                if (target.isActiveAndEnabled) Register(target);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            nonEnergyLayers = 0;
            QueryBudgetMilliseconds = 3f;
        }

        private static float ElapsedMilliseconds(long startTicks) =>
            (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) * 1000f / System.Diagnostics.Stopwatch.Frequency;

        internal static void Register(DestructibleTarget target)
        {
            if (!Application.isPlaying || !target.ParticipatesInStructuralSupport) return;
            if (instance == null)
                instance = new GameObject("StructuralSupportGraph").AddComponent<StructuralSupportGraph>();
            instance.targets.Add(target);
            // 새 표적은 아직 연결 판정에 포함되지 않으므로 첫 스캔을 기다리지 않고 판정을 다시 한다.
            instance.graphChanged = true;
            Changed(target);
        }

        internal static void Changed(DestructibleTarget target)
        {
            if (instance == null || !instance.targets.Contains(target)) return;
            instance.Enqueue(target);
            // 맞닿음은 두 표적 어느 쪽 스캔이든 같은 링크를 되살리므로, 이웃을 함께 다시 스캔하지 않는다.
            // 스캔을 끝낸 쪽에서 실제로 사라진 연결만 그 이웃을 되돌려 다시 훑는다(FinishScan). 표적 수가 큰 씬에서
            // 폭발 한 번에 이웃 전체를 다시 조회하는 것이 support 재계산의 주된 비용이었다.
            foreach (var neighbor in target.supportLinks)
                if (neighbor != null) neighbor.RecheckIfHeldByNeighbors();
            // 연결 판정을 다시 할지는 스캔이 링크·고정을 실제로 바꿨는지로 정한다(FinishScan).
            instance.needsSync = true;
        }

        // A collision batch has already synchronized every transform before notifying us.
        // Keep the contact rebuild queued, without repeating that global sync in LateUpdate.
        internal static void NotifyPhysicsSynchronized()
        {
            if (instance != null) instance.needsSync = false;
        }

        internal static void Unregister(DestructibleTarget target)
        {
            if (instance == null) return;
            instance.targets.Remove(target);
            instance.establishedSupport.Remove(target);
            foreach (var neighbor in target.supportLinks)
                if (neighbor != null)
                {
                    neighbor.supportLinks.Remove(target); instance.Enqueue(neighbor);
                    neighbor.RecheckIfHeldByNeighbors();
                }
            target.supportLinks.Clear();
            instance.graphChanged = true;
        }

        private void Enqueue(DestructibleTarget target)
        {
            if (target != null && targets.Contains(target) && queued.Add(target)) dirty.Enqueue(target);
        }

        // 스캔 전에 양쪽 링크를 벗겨 둔다. 이전 이웃 목록은 스캔이 끝나면 사라진 연결을 찾는 데만 쓴다.
        private void BeginScan()
        {
            previousLinks.Clear();
            foreach (var neighbor in scanning.supportLinks)
                if (neighbor != null)
                {
                    neighbor.supportLinks.Remove(scanning);
                    previousLinks.Add(neighbor);
                }
            scanning.supportLinks.Clear();
            scanAnchorStart = scanning.structuralAnchor;
            scanning.structuralAnchor = scanning.UsesPhysicalGravity;
            scanning.CollectSupportColliders(colliders);
            scanningRoot = scanning.transform;
            scanningScene = scanning.gameObject.scene;
            cursor = 0;
        }

        // 스캔에서 되살아나지 않은 연결만 진짜 사라진 맞닿음이다. 그런 이웃만 다시 훑게 한다.
        private void FinishScan()
        {
            if (scanning.structuralAnchor != scanAnchorStart) graphChanged = true;
            foreach (var neighbor in previousLinks)
            {
                if (neighbor == null || scanning.supportLinks.Contains(neighbor)) continue;
                graphChanged = true;
                Enqueue(neighbor);
            }
            previousLinks.Clear();
            scanning = null;
        }

        // 그래프를 떠난 표적의 스캔은 그대로 버린다. 링크 정리는 Unregister가 이미 끝냈다.
        private void AbandonScan()
        {
            previousLinks.Clear();
            colliders.Clear();
            scanning = null;
        }

        private void LateUpdate()
        {
            PoolManager.FlushExplosionFinish();
            // The deferred opt-in sync covers the same transforms as needsSync.
            if (DestructionCollisionBatch.Flush()) needsSync = false;
            if (needsSync) { Physics.SyncTransforms(); needsSync = false; }
            int budget = QueryBudget;
            // Share the frame window with explosion, mesh, and log work; a tiny guaranteed slice
            // keeps support scans moving when destruction consumes the rest of the budget.
            float sharedBudgetMilliseconds = Mathf.Max(0.05f,
                Mathf.Min(QueryBudgetMilliseconds, DestructionFrameBudget.RemainingMilliseconds));
            long budgetStart = System.Diagnostics.Stopwatch.GetTimestamp();
            while (budget > 0)
            {
                // Stop at this frame's time budget; the scan resumes at the same collider next frame.
                if (ElapsedMilliseconds(budgetStart) >= sharedBudgetMilliseconds) break;
                // A scan can span frames, so drop one whose target left the graph in the meantime.
                if (scanning != null && !targets.Contains(scanning)) { AbandonScan(); continue; }
                if (scanning == null)
                {
                    if (dirty.Count == 0) break;
                    scanning = dirty.Dequeue();
                    queued.Remove(scanning);
                    if (scanning == null || !targets.Contains(scanning)) { scanning = null; continue; }
                    BeginScan();
                }
                if (cursor >= colliders.Count) { FinishScan(); continue; }
                var box = colliders[cursor++] as BoxCollider;
                if (box == null || !box.enabled) continue;
                budget--;
                var scale = box.transform.lossyScale;
                var extents = Vector3.Scale(box.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)))
                    + Vector3.one * ContactTolerance;
                int count;
                while (true)
                {
                    count = Physics.OverlapBoxNonAlloc(box.transform.TransformPoint(box.center), extents, hits,
                        box.transform.rotation, NonEnergyLayers, QueryTriggerInteraction.Ignore);
                    if (count < hits.Length) break;
                    System.Array.Resize(ref hits, hits.Length * 2);
                }
                for (int i = 0; i < count; i++)
                {
                    var hit = hits[i];
                    if (hit == null) continue;
                    // 콜라이더→표적은 전투 조회와 같은 전역 인덱스로 찾는다. 히트마다 부모를 기어오르지 않는다.
                    var other = ColliderTargetIndex.Structure(hit);
                    if (other == scanning) continue;
                    if (other != null)
                    {
                        // 스캔 중인 표적 아래에 중첩된 표적은 기존처럼 자기 일부로 보고 링크하지 않는다.
                        if (other.transform.IsChildOf(scanningRoot)) continue;
                        if (targets.Contains(other) && other.gameObject.scene == scanningScene)
                        {
                            // 첫 접촉만 이전 목록과 견줘 새 맞닿음인지 본다. 상대 쪽 링크가 남아 있으면 같은 맞닿음이다.
                            if (scanning.supportLinks.Add(other) && !previousLinks.Contains(other)) graphChanged = true;
                            other.supportLinks.Add(scanning);
                        }
                        continue;
                    }
                    if (hit.transform.IsChildOf(scanningRoot)) continue;
                    // 고정이 정해지면 남는 히트의 환경 판정은 결과에 영향을 주지 않는다.
                    if (scanning.structuralAnchor) continue;
                    // Static environment contact anchors a structure; players, weapons and debris do not.
                    if (hit.attachedRigidbody == null && !(hit is CharacterController) &&
                        hit.GetComponentInParent<FirstPersonPlayer>() == null &&
                        hit.GetComponentInParent<EquipableWeapon>() == null)
                        scanning.structuralAnchor = true;
                }
            }
            if (!graphChanged || scanning != null || dirty.Count > 0) return;
            graphChanged = false;
            supported.Clear(); traversal.Clear(); detached.Clear();
            foreach (var target in targets)
                if (target != null && target.structuralAnchor && supported.Add(target))
                {
                    establishedSupport.Add(target);
                    traversal.Enqueue(target);
                }
            while (traversal.Count > 0)
                foreach (var neighbor in traversal.Dequeue().supportLinks)
                    if (neighbor != null && targets.Contains(neighbor) && supported.Add(neighbor))
                    {
                        establishedSupport.Add(neighbor);
                        traversal.Enqueue(neighbor);
                    }
            foreach (var target in targets)
                // Authored gaps are not a destruction event. Collapse only after a real support loss.
                if (target != null && target.HasStructuralGeometry && establishedSupport.Contains(target) &&
                    !supported.Contains(target)) detached.Add(target);
            // Building collapse must not subdivide every large part in one frame.
            for (int i = 0; i < Mathf.Min(2, detached.Count); i++) detached[i].DropDisconnectedStructure();
        }

        internal static bool IsSupported(DestructibleTarget target) =>
            instance != null && instance.supported.Contains(target);
    }
}
