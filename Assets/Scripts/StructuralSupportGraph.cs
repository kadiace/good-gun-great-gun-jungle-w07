using System.Collections.Generic;
using UnityEngine;

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
        private Collider[] hits = new Collider[128];
        private DestructibleTarget scanning;
        private int cursor;
        private bool changed;
        private bool needsSync;
        private const float ContactTolerance = 0.015f;
        private const int QueryBudget = 64;

        private void OnEnable()
        {
            // Rebuild registrations after a script reload, including work interrupted by an exception.
            instance = this;
            foreach (var target in FindObjectsByType<DestructibleTarget>(FindObjectsSortMode.None))
                if (target.isActiveAndEnabled) Register(target);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        internal static void Register(DestructibleTarget target)
        {
            if (!Application.isPlaying || !target.ParticipatesInStructuralSupport) return;
            if (instance == null)
                instance = new GameObject("StructuralSupportGraph").AddComponent<StructuralSupportGraph>();
            instance.targets.Add(target);
            Changed(target);
        }

        internal static void Changed(DestructibleTarget target)
        {
            if (instance == null || !instance.targets.Contains(target)) return;
            instance.Enqueue(target);
            foreach (var neighbor in target.supportLinks)
            {
                instance.Enqueue(neighbor);
                if (neighbor != null) neighbor.RecheckIfHeldByNeighbors();
            }
            instance.changed = true;
            instance.needsSync = true;
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
            instance.changed = true;
        }

        private void Enqueue(DestructibleTarget target)
        {
            if (target != null && targets.Contains(target) && queued.Add(target)) dirty.Enqueue(target);
        }

        private void LateUpdate()
        {
            if (needsSync) { Physics.SyncTransforms(); needsSync = false; }
            int budget = QueryBudget;
            while (budget > 0)
            {
                if (scanning == null)
                {
                    if (dirty.Count == 0) break;
                    scanning = dirty.Dequeue();
                    queued.Remove(scanning);
                    if (scanning == null || !targets.Contains(scanning)) { scanning = null; continue; }
                    foreach (var neighbor in scanning.supportLinks)
                        if (neighbor != null) neighbor.supportLinks.Remove(scanning);
                    scanning.supportLinks.Clear();
                    scanning.structuralAnchor = scanning.UsesPhysicalGravity;
                    scanning.CollectSupportColliders(colliders);
                    cursor = 0;
                }
                if (cursor >= colliders.Count) { scanning = null; continue; }
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
                        box.transform.rotation, ~LayerMask.GetMask("Energy"), QueryTriggerInteraction.Ignore);
                    if (count < hits.Length) break;
                    System.Array.Resize(ref hits, hits.Length * 2);
                }
                for (int i = 0; i < count; i++)
                {
                    var hit = hits[i];
                    if (hit == null || hit.transform.IsChildOf(scanning.transform)) continue;
                    var other = hit.GetComponentInParent<DestructibleTarget>();
                    if (other != null)
                    {
                        if (other != scanning && targets.Contains(other) && other.gameObject.scene == scanning.gameObject.scene)
                        { scanning.supportLinks.Add(other); other.supportLinks.Add(scanning); }
                    }
                    // Static environment contact anchors a structure; players, weapons and debris do not.
                    else if (hit.attachedRigidbody == null && !(hit is CharacterController) &&
                        hit.GetComponentInParent<FirstPersonPlayer>() == null &&
                        hit.GetComponentInParent<EquipableWeapon>() == null)
                        scanning.structuralAnchor = true;
                }
            }
            if (!changed || scanning != null || dirty.Count > 0) return;
            changed = false;
            supported.Clear(); traversal.Clear(); detached.Clear();
            foreach (var target in targets)
                if (target != null && target.structuralAnchor && supported.Add(target)) traversal.Enqueue(target);
            while (traversal.Count > 0)
                foreach (var neighbor in traversal.Dequeue().supportLinks)
                    if (neighbor != null && targets.Contains(neighbor) && supported.Add(neighbor)) traversal.Enqueue(neighbor);
            foreach (var target in supported) establishedSupport.Add(target);
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
