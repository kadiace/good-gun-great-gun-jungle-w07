using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 실제 조립을 읽어 드래그 예정 셀을 추가하는 가상 배치 상태. 실제 설치 판정은 CubeAssembly가 담당한다.
    // 프리뷰 때문에 GameObject를 생성하거나 실제 에너지 경로를 변경하지 않는다.
    internal sealed class CubePlacementState
    {
        internal static readonly Vector3Int[] Directions =
        { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };
        public static int Dot(Vector3Int a, Vector3Int b) => a.x * b.x + a.y * b.y + a.z * b.z;
        private readonly Dictionary<Vector3Int, Vector3Int[]> cells = new();
        private readonly Dictionary<Vector3Int, int> order = new();
        private int nextOrder;
        public bool HasTrigger { get; private set; }

        public CubePlacementState(CubeAssembly assembly)
        {
            foreach (var block in assembly.GetComponentsInChildren<PlacedBlock>())
            {
                if (block.transform.parent != assembly.transform) continue;
                cells[block.Cell] = block.GetOutputDirections();
                order[block.Cell] = block.transform.GetSiblingIndex();
                nextOrder = Mathf.Max(nextOrder, order[block.Cell] + 1);
                HasTrigger |= block.GetComponentInChildren<TriggerModule>(true) != null;
            }
        }

        public bool Occupied(Vector3Int cell) => cell == Vector3Int.zero || cells.ContainsKey(cell);
        public bool HasOutput(Vector3Int cell, Vector3Int direction) => cell == Vector3Int.zero ||
            (cells.TryGetValue(cell, out var outputs) && System.Array.IndexOf(outputs, direction) >= 0);
        public bool CanAttach(Vector3Int cell, bool trigger)
        {
            if (Occupied(cell) || (trigger && HasTrigger)) return false;
            foreach (var direction in Directions) if (Occupied(cell - direction)) return true;
            return false;
        }
        public bool TryGetNeighbor(Vector3Int cell, out Vector3Int face)
        {
            foreach (var direction in Directions)
                if (Occupied(cell - direction)) { face = direction; return true; }
            face = default;
            return false;
        }
        public bool TryGetFeed(Vector3Int cell, out Vector3Int feed)
        {
            feed = default;
            int latest = int.MinValue;
            foreach (var travel in Directions)
            {
                Vector3Int source = cell - travel;
                if (!HasOutput(source, travel)) continue;
                int candidate = source == Vector3Int.zero ? -1 : order[source];
                if (candidate > latest) { latest = candidate; feed = travel; }
            }
            return latest != int.MinValue;
        }
        public static Vector3Int[] Outputs(int slot, Vector3Int direction, Vector3Int normal, bool sight)
        {
            if (slot == 1 || sight) return System.Array.Empty<Vector3Int>();
            if (CubeAssembly.IsSingleOutput(slot)) return new[] { direction };
            if (slot == 3) return new[] { direction, -direction };
            if (slot != 4) return System.Array.Empty<Vector3Int>();
            Vector3 up = Mathf.Abs(normal.y) == 1 ? Vector3.forward : Vector3.up;
            Vector3Int right = Vector3Int.RoundToInt(Vector3.Cross(up, normal).normalized);
            Vector3Int vertical = Vector3Int.RoundToInt(Vector3.Cross(normal, right).normalized);
            return new[] { right, -right, vertical, -vertical };
        }
        public bool CanOrient(Vector3Int cell, int slot, Vector3Int direction, Vector3Int normal, bool sight)
        {
            if (slot == 1) return true;
            if (direction.sqrMagnitude != 1 || normal.sqrMagnitude != 1) return false;
            if (sight) return true;
            if (slot < 2) return false;
            foreach (var output in Outputs(slot, direction, normal, false))
                if (HasOutput(cell + output, -output)) return false;
            return true;
        }
        public void Add(Vector3Int cell, GameObject prefab, int slot, Vector3Int direction, Vector3Int normal, bool sight, bool trigger)
        {
            var block = prefab.GetComponent<PlacedBlock>();
            Vector3Int[] outputs = System.Array.Empty<Vector3Int>();
            if (block != null && block.Kind != BlockKind.Blocker && !sight)
                outputs = prefab.GetComponent<SplitModule>() != null ? Outputs(block.Kind == BlockKind.SplitTwo ? 3 : 4, direction, normal, false)
                    : prefab.GetComponent<ExtensionModule>() != null ? new[] { direction } : outputs;
            cells.Add(cell, outputs);
            order.Add(cell, nextOrder++);
            HasTrigger |= trigger;
        }
    }
}
