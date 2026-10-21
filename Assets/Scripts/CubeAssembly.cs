using UnityEngine;
using System.Linq;

namespace WeaponWorkshop
{
    public sealed class CubeAssembly : MonoBehaviour
    {
        private bool buildingLayout;
        public void NotifyStructureChanged()
        {
            if (!buildingLayout) GetComponentInParent<EquipableWeapon>()?.NotifyAssemblyChanged();
        }
        public sealed class LayoutSnapshot
        {
            internal readonly BlockSnapshot[] blocks;
            internal readonly int paidConstructionCost;

            internal LayoutSnapshot(BlockSnapshot[] blocks, int paidConstructionCost)
            {
                this.blocks = blocks;
                this.paidConstructionCost = paidConstructionCost;
            }
        }

        internal struct BlockSnapshot
        {
            public GameObject prefab;
            public int slot;
            public Vector3Int cell;
            public Vector3Int outputDirection;
            public Vector3Int orientationNormal;
            public int paidPurchaseCost;
            public int paidModuleCreditCost;
            public bool hasColor;   // color module: its own picked color
            public Color color;
        }

        [SerializeField] private GameObject blockerPrefab;
        [SerializeField] [UnityEngine.Serialization.FormerlySerializedAs("refractionPrefab")] private GameObject extensionPrefab;
        [SerializeField] private GameObject splitTwoPrefab;
        [SerializeField] private GameObject splitFourPrefab;
        [SerializeField, Tooltip("5번 이후 스탯 모듈 프리팹 목록. 1~4번은 위 프리팹을 사용합니다.")]
        private ModuleCatalog moduleCatalog;
        private static readonly Vector3Int[] Neighbors =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up,
            Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };

        public void RecordPurchasedModule(Vector3Int cell, int amount)
        {
            foreach (PlacedBlock block in GetComponentsInChildren<PlacedBlock>())
            {
                if (block.transform.parent != transform || block.Cell != cell) continue;
                block.RecordPurchaseCost(amount);
                GetComponentInParent<EquipableWeapon>()?.RecordConstructionPurchase(amount);
                return;
            }
        }

        public void RecordPurchasedModuleCredits(Vector3Int cell, int amount)
        {
            foreach (PlacedBlock block in GetComponentsInChildren<PlacedBlock>())
                if (block.transform.parent == transform && block.Cell == cell)
                { block.RecordModuleCreditCost(amount); return; }
        }

        public bool RemoveBlock(PlacedBlock target, PlayerWallet refundWallet = null, PlayerInventory returnInventory = null)
        {
            if (IsConstructionLocked || target == null || target.transform.parent != transform || !target.gameObject.activeSelf)
                return false;

            if (SurvivalSession.IsActive && returnInventory == null)
            {
                returnInventory = PlayerInventory.Instance != null ? PlayerInventory.Instance : FindFirstObjectByType<PlayerInventory>();
            }

            int refund = refundWallet != null && !SurvivalSession.IsActive ? target.TakePurchaseCost() : 0;
            int creditRefund = refundWallet != null && !SurvivalSession.IsActive ? target.TakeModuleCreditCost() : 0;
            string partKind = refundWallet != null ? ModuleCounter.LogName(target) : "";

            if (SurvivalSession.IsActive && returnInventory != null)
            {
                returnInventory.Add(target.ModuleSlot, 1);
            }

            RemoveObject(target);
            var cells = new System.Collections.Generic.Dictionary<Vector3Int, PlacedBlock>();
            foreach (PlacedBlock block in GetComponentsInChildren<PlacedBlock>())
                cells[block.Cell] = block;
            var connected = new System.Collections.Generic.HashSet<Vector3Int> { Vector3Int.zero };
            var pending = new System.Collections.Generic.Queue<Vector3Int>();
            pending.Enqueue(Vector3Int.zero);
            while (pending.Count > 0)
            {
                Vector3Int cell = pending.Dequeue();
                foreach (Vector3Int offset in Neighbors)
                {
                    Vector3Int neighbor = cell + offset;
                    if (cells.ContainsKey(neighbor) && connected.Add(neighbor))
                        pending.Enqueue(neighbor);
                }
            }
            foreach (var entry in cells)
                if (!connected.Contains(entry.Key))
                {
                    if (SurvivalSession.IsActive && returnInventory != null)
                    {
                        returnInventory.Add(entry.Value.ModuleSlot, 1);
                    }
                    if (refundWallet != null && !SurvivalSession.IsActive) refund += entry.Value.TakePurchaseCost();
                    if (refundWallet != null && !SurvivalSession.IsActive) creditRefund += entry.Value.TakeModuleCreditCost();
                    RemoveObject(entry.Value);
                }
            if (!SurvivalSession.IsActive)
            {
                int moneyRefund = GetComponentInParent<EquipableWeapon>()?.RefundModulePurchases(refund, refundWallet) ?? 0;
                refundWallet?.CreditModuleCredits(creditRefund);
                // Disconnected blocks refund with the removed one, so the log names only the removed part.
                refundWallet?.NotifyPartRefunded(partKind, moneyRefund, creditRefund);
            }
            NotifyStructureChanged();
            return true;
        }

        private static void RemoveObject(PlacedBlock block)
        {
            // Release occupancy and collisions now, before end-of-frame destruction.
            block.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(block.gameObject);
            else DestroyImmediate(block.gameObject);
        }

        public bool TryAttach(Vector3Int cell, int slot)
            => TryAttach(cell, slot, new Vector3Int(0, 0, 1));

        public bool CanAttach(Vector3Int cell, int slot)
        {
            if (IsConstructionLocked || cell == Vector3Int.zero || slot < 1) return false;
            bool adjacent = cell.sqrMagnitude == 1;
            foreach (PlacedBlock block in GetComponentsInChildren<PlacedBlock>())
            {
                if (block.Cell == cell) return false;
                Vector3Int delta = block.Cell - cell;
                if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) + Mathf.Abs(delta.z) == 1)
                    adjacent = true;
            }
            if (!adjacent) return false;
            GameObject prefab = GetPrefab(slot);
            return prefab != null && (!HasTriggerModulePrefab(prefab) || !HasTriggerModule);
        }

        public bool HasTriggerModule
        {
            get
            {
                foreach (PlacedBlock block in GetComponentsInChildren<PlacedBlock>())
                    if (block.transform.parent == transform && block.GetComponentInChildren<TriggerModule>(true) != null)
                        return true;
                return false;
            }
        }

        public bool IsTriggerModuleSlot(int slot) => HasTriggerModulePrefab(GetPrefab(slot));

        private static bool HasTriggerModulePrefab(GameObject prefab)
            => prefab != null && prefab.GetComponentInChildren<TriggerModule>(true) != null;

        public bool TryCaptureLayout(out LayoutSnapshot snapshot)
        {
            var blocks = new System.Collections.Generic.List<BlockSnapshot>();
            foreach (PlacedBlock block in GetComponentsInChildren<PlacedBlock>(true))
            {
                if (block == null || block.transform.parent != transform || !block.gameObject.activeSelf) continue;
                if (!TryResolvePrefab(block, out int slot, out GameObject prefab))
                {
                    snapshot = null;
                    return false;
                }

                ExtensionModule extension = block.GetComponent<ExtensionModule>();
                ColorModule colorModule = block.GetComponent<ColorModule>();
                blocks.Add(new BlockSnapshot
                {
                    prefab = prefab,
                    slot = slot,
                    cell = block.Cell,
                    outputDirection = extension != null ? extension.OutputDirection : Vector3Int.forward,
                    orientationNormal = block.OrientationNormal,
                    paidPurchaseCost = block.PaidPurchaseCost,
                    paidModuleCreditCost = block.PaidModuleCreditCost,
                    hasColor = colorModule != null,
                    color = colorModule != null ? colorModule.Color : Color.white
                });
            }

            EquipableWeapon weapon = GetComponentInParent<EquipableWeapon>();
            snapshot = new LayoutSnapshot(blocks.ToArray(), weapon != null ? weapon.PaidConstructionCost : 0);
            return true;
        }

        public bool RestoreLayout(LayoutSnapshot snapshot)
        {
            if (IsConstructionLocked || snapshot == null) return false;
            foreach (BlockSnapshot state in snapshot.blocks)
                if (state.prefab == null || state.slot < 1) return false;

            bool sameStructure = TryCaptureLayout(out var before) &&
                before.blocks.OrderBy(b => b.cell.x).ThenBy(b => b.cell.y).ThenBy(b => b.cell.z)
                    .Select(b => (b.prefab, b.slot, b.cell, b.outputDirection, b.orientationNormal))
                    .SequenceEqual(snapshot.blocks.OrderBy(b => b.cell.x).ThenBy(b => b.cell.y).ThenBy(b => b.cell.z)
                        .Select(b => (b.prefab, b.slot, b.cell, b.outputDirection, b.orientationNormal)));

            foreach (PlacedBlock block in GetComponentsInChildren<PlacedBlock>(true))
                if (block != null && block.transform.parent == transform) RemoveObject(block);

            foreach (BlockSnapshot state in snapshot.blocks)
            {
                GameObject restored = CreateBlock(state.prefab, state.slot, state.cell,
                    state.outputDirection, state.orientationNormal);
                restored.GetComponent<PlacedBlock>()?.RecordPurchaseCost(state.paidPurchaseCost);
                restored.GetComponent<PlacedBlock>()?.RecordModuleCreditCost(state.paidModuleCreditCost);
                if (state.hasColor) restored.GetComponent<ColorModule>()?.SetColor(state.color);
            }

            GetComponentInParent<EquipableWeapon>()?.RestoreConstructionPurchaseCost(snapshot.paidConstructionCost);
            if (!sameStructure) NotifyStructureChanged();
            return true;
        }

        private bool TryResolvePrefab(PlacedBlock block, out int slot, out GameObject prefab)
        {
            slot = block.ModuleSlot;
            prefab = block.SourcePrefab;
            if (slot > 0 && prefab != null) return true;
            if (slot > 0)
            {
                prefab = GetPrefab(slot);
                if (prefab != null) return true;
            }

            ModuleStats placedStats = block.GetComponent<ModuleStats>();
            if (placedStats != null && placedStats.Data != null && moduleCatalog != null)
            {
                for (int candidateSlot = 5; candidateSlot <= moduleCatalog.Count; candidateSlot++)
                {
                    GameObject candidate = moduleCatalog.GetPrefab(candidateSlot);
                    ModuleStats candidateStats = candidate != null ? candidate.GetComponent<ModuleStats>() : null;
                    if (candidateStats == null || candidateStats.Data != placedStats.Data) continue;
                    slot = candidateSlot;
                    prefab = candidate;
                    return true;
                }
            }
            if (placedStats != null && placedStats.Data != null)
            {
                prefab = placedStats.Data.Prefab;
                slot = block.ModuleSlot >= 5 ? block.ModuleSlot : 5;
                return prefab != null;
            }

            slot = block.Kind switch
            {
                BlockKind.Blocker => 1,
                BlockKind.Extension => 2,
                BlockKind.SplitTwo => 3,
                BlockKind.SplitFour => 4,
                _ => 0
            };
            prefab = slot > 0 ? GetPrefab(slot) : null;
            return prefab != null;
        }

        public ModuleData GetModuleData(int slot)
            => moduleCatalog != null && moduleCatalog.TryGet(slot, out var entry) ? entry.data : null;
        public int ModuleCount => moduleCatalog != null ? moduleCatalog.Count : 0;

        public GameObject GetPrefab(int slot) => slot == 1 ? (blockerPrefab != null ? blockerPrefab : moduleCatalog != null ? moduleCatalog.GetPrefab(1) : null) : slot == 2 ? extensionPrefab : slot == 3 ? splitTwoPrefab : slot == 4 ? splitFourPrefab
            : moduleCatalog != null ? moduleCatalog.GetPrefab(slot) : null;

        // Stat modules (5+) route energy like an extension: one output direction.
        public static bool IsSingleOutput(int slot) => slot == 2 || slot >= 5;

        public bool TryAttach(Vector3Int cell, int slot, Vector3Int outputDirection)
            => TryAttach(cell, slot, outputDirection, outputDirection);

        public bool TryAttach(Vector3Int cell, int slot, Vector3Int outputDirection, Vector3Int orientationNormal)
        {
            if (!CanAttach(cell, slot)) return false;

            if (!CanOrient(cell, slot, outputDirection, orientationNormal)) return false;
            GameObject prefab = GetPrefab(slot);
            if (prefab == null) return false;
            CreateBlock(prefab, slot, cell, outputDirection, orientationNormal);
            NotifyStructureChanged();
            return true;
        }

        private GameObject CreateBlock(GameObject prefab, int slot, Vector3Int cell,
            Vector3Int outputDirection, Vector3Int orientationNormal)
        {
            bool previousBuilding = buildingLayout;
            buildingLayout = true;
            try
            {
                GameObject placed = Instantiate(prefab, transform);
                placed.GetComponent<ModuleDraw>()?.SetPreview(false);
                placed.transform.localPosition = cell;
                placed.transform.localRotation = Quaternion.identity;
                placed.transform.localScale = Vector3.one;
                foreach (Transform child in placed.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = gameObject.layer;
                placed.name = prefab.name;
                if (IsSingleOutput(slot) || slot == 3) placed.GetComponent<ExtensionModule>()?.SetDirection(outputDirection);

                PlacedBlock placedBlock = placed.GetComponent<PlacedBlock>();
                placedBlock?.SetModuleSource(slot, prefab);
                placedBlock?.SetOrientation(orientationNormal);
                return placed;
            }
            finally { buildingLayout = previousBuilding; }
        }

        public bool AttachAtHit(RaycastHit hit, int slot)
        {
            return TryGetAttachCell(hit, out Vector3Int cell, out Vector3Int face) && TryAttach(cell, slot, face);
        }

        public bool CanOrient(Vector3Int cell, int slot, Vector3Int outputDirection, Vector3Int orientationNormal)
        {
            if (slot == 1) return true;
            if (outputDirection.sqrMagnitude != 1 || orientationNormal.sqrMagnitude != 1) return false;
            // A scope carries no energy, so its lens may face anything (core, other outputs).
            GameObject prefab = GetPrefab(slot);
            PlacedBlock sight = prefab != null ? prefab.GetComponent<PlacedBlock>() : null;
            if (sight != null && sight.IsSight) return true;
            Vector3Int[] outputs;
            if (IsSingleOutput(slot)) outputs = new[] { outputDirection };
            else if (slot == 3) outputs = new[] { outputDirection, -outputDirection };
            else if (slot == 4)
            {
                Vector3 up = Mathf.Abs(orientationNormal.y) == 1 ? Vector3.forward : Vector3.up;
                Vector3Int right = Vector3Int.RoundToInt(Vector3.Cross(up, orientationNormal).normalized);
                Vector3Int vertical = Vector3Int.RoundToInt(Vector3.Cross(orientationNormal, right).normalized);
                outputs = new[] { right, -right, vertical, -vertical };
            }
            else return false;
            foreach (Vector3Int direction in outputs)
            {
                Vector3Int neighbor = cell + direction;
                // Every core face is an output.
                if (neighbor == Vector3Int.zero) return false;
                foreach (PlacedBlock block in GetComponentsInChildren<PlacedBlock>())
                    if (block.Cell == neighbor && block.HasOutput(-direction)) return false;
            }
            return true;
        }

        // Direction energy enters `cell` with, from the most recently placed neighbor that outputs into it.
        // The core outputs on every face and counts as the oldest source. False when nothing feeds the cell.
        public bool TryGetFeedDirection(Vector3Int cell, out Vector3Int direction)
        {
            direction = Vector3Int.zero;
            int latest = int.MinValue;
            foreach (Vector3Int travel in Neighbors)
            {
                Vector3Int source = cell - travel;
                if (source == Vector3Int.zero)
                {
                    if (latest == int.MinValue) { latest = -1; direction = travel; }
                    continue;
                }
                foreach (PlacedBlock block in GetComponentsInChildren<PlacedBlock>())
                {
                    if (block.transform.parent != transform || block.Cell != source || !block.HasOutput(travel)) continue;
                    // Blocks are appended as they are placed (undo/restore keeps that order), so a later sibling is newer.
                    int order = block.transform.GetSiblingIndex();
                    if (order > latest) { latest = order; direction = travel; }
                }
            }
            return latest != int.MinValue;
        }

        public bool HasStructuralFailure()
        {
            PlacedBlock[] blocks = GetComponentsInChildren<PlacedBlock>();
            foreach (PlacedBlock source in blocks)
                foreach (Vector3Int direction in source.GetOutputDirections())
                {
                    Vector3Int neighbor = source.Cell + direction;
                    if (neighbor == Vector3Int.zero) return true;
                    foreach (PlacedBlock target in blocks)
                        if (target.Cell == neighbor && target.HasOutput(-direction)) return true;
                }
            return false;
        }

        private bool IsConstructionLocked
            => GetComponentInParent<EquipableWeapon>() != null && GetComponentInParent<EquipableWeapon>().ConstructionLocked;

        public static System.Collections.Generic.List<(Vector3 origin, Vector3Int direction)> GetCoreOutputs()
        {
            var outputs = new System.Collections.Generic.List<(Vector3, Vector3Int)>();
            foreach (Vector3Int direction in Neighbors) outputs.Add((Vector3.zero, direction));
            return outputs;
        }

        public System.Collections.Generic.List<(Vector3 origin, Vector3Int direction)> GetProjectileOutputs()
        {
            var outputs = new System.Collections.Generic.List<(Vector3, Vector3Int)>();
            foreach (var path in GetProjectilePaths()) outputs.Add((path.origin, path.direction));
            return outputs;
        }

        // Same order as GetProjectileOutputs; path lists the blocks each projectile passed, core first.
        public System.Collections.Generic.List<(Vector3 origin, Vector3Int direction, PlacedBlock[] path)> GetProjectilePaths()
        {
            var cells = new System.Collections.Generic.Dictionary<Vector3Int, PlacedBlock>();
            foreach (PlacedBlock block in GetComponentsInChildren<PlacedBlock>())
                if (block.transform.parent == transform) cells[block.Cell] = block;
            var outputs = new System.Collections.Generic.List<(Vector3, Vector3Int, PlacedBlock[])>();
            foreach (Vector3Int direction in Neighbors)
                TraceOutput(direction, direction, Vector3.zero,
                    new System.Collections.Generic.HashSet<Vector3Int> { Vector3Int.zero }, cells,
                    new System.Collections.Generic.List<PlacedBlock>(), outputs);
            return outputs;
        }

        private static void TraceOutput(Vector3Int cell, Vector3Int travelDirection, Vector3 origin,
            System.Collections.Generic.HashSet<Vector3Int> visited,
            System.Collections.Generic.Dictionary<Vector3Int, PlacedBlock> cells,
            System.Collections.Generic.List<PlacedBlock> path,
            System.Collections.Generic.List<(Vector3 origin, Vector3Int direction, PlacedBlock[] path)> outputs)
        {
            if (visited.Contains(cell)) return;
            if (!cells.TryGetValue(cell, out PlacedBlock block))
            {
                outputs.Add((origin, travelDirection, path.ToArray()));
                return;
            }
            if (!block.AcceptsEnergy(travelDirection)) return;
            var ancestry = new System.Collections.Generic.HashSet<Vector3Int>(visited) { cell };
            path.Add(block);
            foreach (Vector3Int direction in block.GetOutputDirections())
            {

                // Each branch gets a complete projectile. Never divide or merge branches.
                TraceOutput(cell + direction, direction, (Vector3)cell + (Vector3)direction * 0.5f,
                    ancestry, cells, path, outputs);
            }
            path.RemoveAt(path.Count - 1);
        }
        public bool TryGetAttachCell(RaycastHit hit, out Vector3Int cell, out Vector3Int face)
        {
            cell = Vector3Int.zero;
            face = Vector3Int.zero;
            PlacedBlock block = hit.collider.GetComponentInParent<PlacedBlock>();
            if ((block == null && !hit.collider.transform.IsChildOf(transform)) ||
                (block != null && block.transform.parent != transform)) return false;
            Vector3 normal = transform.InverseTransformDirection(hit.normal);
            face = Mathf.Abs(normal.x) >= Mathf.Abs(normal.y) && Mathf.Abs(normal.x) >= Mathf.Abs(normal.z)
                ? new Vector3Int(normal.x > 0 ? 1 : -1, 0, 0)
                : Mathf.Abs(normal.y) >= Mathf.Abs(normal.z)
                    ? new Vector3Int(0, normal.y > 0 ? 1 : -1, 0)
                    : new Vector3Int(0, 0, normal.z > 0 ? 1 : -1);
            cell = (block == null ? Vector3Int.zero : block.Cell) + face;
            return true;
        }
    }
}

