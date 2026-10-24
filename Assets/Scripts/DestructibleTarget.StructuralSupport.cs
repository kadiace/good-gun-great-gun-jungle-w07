using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed partial class DestructibleTarget
    {
        // Links are between whole editable parts, using their surviving collision geometry.
        internal readonly HashSet<DestructibleTarget> supportLinks = new HashSet<DestructibleTarget>();
        internal bool structuralAnchor;
        private readonly List<Collider> externalSupportColliders = new List<Collider>();
        private readonly List<Collider> neighborSupportColliders = new List<Collider>();
        private readonly HashSet<int> externallySupportedCells = new HashSet<int>();
        // 충돌 콜라이더 구성(활성 여부·경계)이 바뀔 때마다 올린다. 이웃은 이 값으로 외부 지지 캐시를 검증한다.
        private int collisionShapeVersion;
        internal int CollisionShapeVersion => collisionShapeVersion;
        // 외부 지지 결과는 이웃 콜라이더 bounds와 격자 순회가 비싸므로 입력이 그대로면 재사용한다.
        // 키에는 bounds 대신 충돌 형상 버전·지지 상태·루트 자세·연결 집합만 넣는다.
        private bool externalSupportCacheValid;
        private long externalSupportCacheKey;
        private bool externalSupportCacheHeld;

        internal void InvalidateCollisionShape() => collisionShapeVersion++;

        // 입력이 바뀌었을 때만 다시 계산한다. 반환값은 다시 계산했는지 여부.
        private bool RefreshExternalSupport()
        {
            long key = ComputeExternalSupportKey();
            if (externalSupportCacheValid && key == externalSupportCacheKey) return false;
            CollectExternalSupport();
            externalSupportCacheKey = key;
            externalSupportCacheValid = true;
            externalSupportCacheHeld = false;
            foreach (int cellIndex in externallySupportedCells)
                if (cellIndex >= 0 && cellIndex < grid.Length && grid[cellIndex])
                { externalSupportCacheHeld = true; break; }
            return true;
        }

        private void InvalidateExternalSupportCache()
        {
            externalSupportCacheValid = false;
            externalSupportCacheKey = 0;
        }

        private static long MixSupportKey(long key, long value) => unchecked((key ^ value) * 1099511628211L);

        // 자신·이웃의 루트 자세, 이웃의 충돌 형상 버전·지지 상태, 연결 집합을 순서대로 접는다.
        private long ComputeExternalSupportKey()
        {
            long key = 1469598103934665603L;
            Transform root = CellRoot;
            Vector3 position = root.position, scale = root.lossyScale;
            Quaternion rotation = root.rotation;
            key = MixSupportKey(key, position.x.GetHashCode());
            key = MixSupportKey(key, position.y.GetHashCode());
            key = MixSupportKey(key, position.z.GetHashCode());
            key = MixSupportKey(key, rotation.x.GetHashCode());
            key = MixSupportKey(key, rotation.y.GetHashCode());
            key = MixSupportKey(key, rotation.z.GetHashCode());
            key = MixSupportKey(key, rotation.w.GetHashCode());
            key = MixSupportKey(key, scale.x.GetHashCode());
            key = MixSupportKey(key, scale.y.GetHashCode());
            key = MixSupportKey(key, scale.z.GetHashCode());
            key = MixSupportKey(key, BelowSupportOnly ? 1L : 0L);
            key = MixSupportKey(key, pivotGrounded.HasValue ? (pivotGrounded.Value ? 2L : 3L) : 4L);
            key = MixSupportKey(key, supportLinks.Count);
            foreach (var neighbor in supportLinks)
            {
                long link = neighbor == null ? -1L : neighbor.GetInstanceID();
                if (neighbor != null)
                {
                    link = MixSupportKey(link, neighbor.collisionShapeVersion);
                    link = MixSupportKey(link, StructuralSupportGraph.IsSupported(neighbor) ? 1L : 0L);
                    Transform neighborRoot = neighbor.targetMesh != null ? neighbor.targetMesh.transform : null;
                    if (neighborRoot != null)
                    {
                        Vector3 neighborPosition = neighborRoot.position, neighborScale = neighborRoot.lossyScale;
                        Quaternion neighborRotation = neighborRoot.rotation;
                        link = MixSupportKey(link, neighborPosition.x.GetHashCode());
                        link = MixSupportKey(link, neighborPosition.y.GetHashCode());
                        link = MixSupportKey(link, neighborPosition.z.GetHashCode());
                        link = MixSupportKey(link, neighborRotation.x.GetHashCode());
                        link = MixSupportKey(link, neighborRotation.y.GetHashCode());
                        link = MixSupportKey(link, neighborRotation.z.GetHashCode());
                        link = MixSupportKey(link, neighborRotation.w.GetHashCode());
                        link = MixSupportKey(link, neighborScale.x.GetHashCode());
                        link = MixSupportKey(link, neighborScale.y.GetHashCode());
                        link = MixSupportKey(link, neighborScale.z.GetHashCode());
                    }
                }
                key = MixSupportKey(key, link);
            }
            return key;
        }

        private void CollectExternalSupport()
        {
            externalSupportColliders.Clear();
            externallySupportedCells.Clear();
            foreach (var neighbor in supportLinks)
            {
                if (neighbor == null || !StructuralSupportGraph.IsSupported(neighbor)) continue;
                neighbor.CollectSupportColliders(neighborSupportColliders);
                externalSupportColliders.AddRange(neighborSupportColliders);
            }
            bool belowOnly = BelowSupportOnly;
            var ownBounds = belowOnly ? OriginalWorldBounds() : default;
            foreach (var collider in externalSupportColliders)
            {
                if (collider == null || !collider.enabled) continue;
                var contact = collider.bounds;
                // Walls and columns modeled through this part are side contact, not a floor under it.
                if (belowOnly && Interpenetrates(contact, ownBounds)) continue;
                var bounds = contact;
                bounds.Expand(.03f);
                Vector3 min = new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);
                Vector3 max = new Vector3(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = CellRoot.InverseTransformPoint(new Vector3(
                        (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z));
                    min = Vector3.Min(min,point); max = Vector3.Max(max,point);
                }
                var first = new Vector3Int(Mathf.Max(0,Mathf.FloorToInt((min.x-gridOrigin.x)/gridStep.x)),
                    Mathf.Max(0,Mathf.FloorToInt((min.y-gridOrigin.y)/gridStep.y)),Mathf.Max(0,Mathf.FloorToInt((min.z-gridOrigin.z)/gridStep.z)));
                var last = new Vector3Int(Mathf.Min(divisions.x-1,Mathf.FloorToInt((max.x-gridOrigin.x)/gridStep.x)),
                    Mathf.Min(divisions.y-1,Mathf.FloorToInt((max.y-gridOrigin.y)/gridStep.y)),Mathf.Min(divisions.z-1,Mathf.FloorToInt((max.z-gridOrigin.z)/gridStep.z)));
                for (int x=first.x;x<=last.x;x++)
                    for (int y=first.y;y<=last.y;y++)
                        for (int z=first.z;z<=last.z;z++)
                        {
                            var coordinate = new Vector3Int(x,y,z);
                            if (TryGetCell(coordinate,out var cell) && (!belowOnly || RestsOn(cell, contact)))
                                externallySupportedCells.Add(CellIndex(coordinate));
                        }
            }
        }

        // Below-only applies to parts standing on their own bottom row; side-hung parts keep any contact.
        private bool? pivotGrounded;
        private bool externallyHeld;
        private bool BelowSupportOnly
        {
            get
            {
                if (!StructuralSupportSettings.RequireSupportFromBelow(gameObject)) return false;
                if (pivotGrounded == null)
                {
                    bool grounded = false;
                    for (int index = 0; index < preparedCells.Length; index++)
                        if (CellBottom(preparedCells[index]) <= transform.position.y + .001f) { grounded = true; break; }
                    if (preparedCells.Length > 0) pivotGrounded = grounded;
                    return grounded;
                }
                return pivotGrounded.Value;
            }
        }
        private Bounds OriginalWorldBounds()
        {
            var local = targetMesh.sharedMesh.bounds;
            var matrix = CellRoot.localToWorldMatrix;
            var result = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
                result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(
                    (corner & 1) == 0 ? local.min.x : local.max.x,
                    (corner & 2) == 0 ? local.min.y : local.max.y,
                    (corner & 4) == 0 ? local.min.z : local.max.z)));
            return result;
        }
        private static bool Interpenetrates(Bounds a, Bounds b) =>
            Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x) > .05f &&
            Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y) > .05f &&
            Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z) > .05f;
        private float CellBottom(Cell cell) => cell.position.y
            - Mathf.Abs(cell.TransformVector(Vector3.right * .5f).y)
            - Mathf.Abs(cell.TransformVector(Vector3.up * .5f).y)
            - Mathf.Abs(cell.TransformVector(Vector3.forward * .5f).y);
        private bool RestsOn(Cell cell, Bounds support)
        {
            var center = cell.position;
            return Mathf.Abs(CellBottom(cell) - support.max.y) <= .05f &&
                center.x >= support.min.x && center.x <= support.max.x &&
                center.z >= support.min.z && center.z <= support.max.z;
        }
        // A neighbor change may remove the floor this part was resting on.
        internal void RecheckIfHeldByNeighbors()
        {
            if (!externallyHeld || !BelowSupportOnly) return;
            if (supportValidated)
                foreach (int cellIndex in cells) changedBoundary.Add(preparedCells[cellIndex].coordinate);
            MarkStructureDirty();
        }

        private bool HasExternalSupport(int cellIndex)
        {
            // Conservative bounds contact; only overlapping grid ranges are visited once above.
            return externallySupportedCells.Contains(cellIndex);
        }
        internal bool HasStructuralGeometry => !subdivided || cells.Count > 0;
        private bool hasFallingRangeTarget;
        internal bool UsesPhysicalGravity => hasFallingRangeTarget ||
            (TryGetComponent<Rigidbody>(out var body) && body != null && !body.isKinematic);

        private void CachePhysicalGravityComponents()
        {
            hasFallingRangeTarget = GetComponent<FallingRangeTarget>() != null;
        }

        private void Start()
        {
            bool wasPhysical = UsesPhysicalGravity;
            CachePhysicalGravityComponents();
            CacheTransformMovement();
            RefreshUpdaterRegistration();
            if (wasPhysical != UsesPhysicalGravity) StructuralSupportGraph.Changed(this);
        }

        internal void CollectSupportColliders(List<Collider> result)
        {
            result.Clear();
            if (!subdivided)
            {
                foreach (var collider in originalColliders.Keys)
                    if (collider != null && collider.enabled && !collider.isTrigger) result.Add(collider);
            }
            else if (collisionColumns != null)
                foreach (var column in collisionColumns)
                    if (column != null)
                        foreach (var collider in column)
                            if (collider != null && collider.enabled) result.Add(collider);
        }

        internal void DropDisconnectedStructure()
        {
            if (!enabled || UsesPhysicalGravity || (subdivided && cells.Count == 0)) return;
            if (!subdivided) Subdivide(false);
            groupPieces.Clear();
            foreach (int cellIndex in cells) groupPieces.Add(preparedCells[cellIndex].Snapshot);
            if (groupPieces.Count == 0) return;
            EnsureDebrisCloud();
            debrisCloud.EmitGroup(groupPieces, debrisLifetime, debrisFadeDuration);
            DebrisEmissionBudget.NoteGroup(groupPieces.Count, debrisLifetime);
            while (cells.Count > 0) RemoveCell(cells[cells.Count - 1]);
            structureDirty = false;
            UpdateCollisionRuns();
        }
    }

}
