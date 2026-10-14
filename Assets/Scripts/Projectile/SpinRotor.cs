using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 회전 모듈 = 총열 회전. 장착 중에만 회전 모듈의 출력 방향을 축(회전 모듈 중심 통과)으로,
    // 회전 모듈 앞쪽에 물리적으로 붙어 있는 블록 전부가 한 덩어리로 돈다(옆으로 여러 칸 붙인 블록 포함).
    //   덩어리 = 회전 모듈 출력 칸에서 시작해 면으로 맞닿은 블록을 따라 퍼진 범위.
    //   제외 = 회전 모듈 자신, 코어, 회전 모듈을 거치지 않고 코어에서 에너지가 닿는 블록.
    // 덩어리 안의 다른 회전 모듈은 바깥 회전을 따라가며 자기 덩어리를 추가로 돌린다. 같은 축으로 연속 연결하면 회전 속도가 누적된다.
    // 블록의 칸(transform.localPosition)은 그대로 두고 자식(외형)만 돌리며, 총구 위치·방향은 TransformMuzzle로 같은 회전을 적용.
    [DisallowMultipleComponent]
    public sealed class SpinRotor : MonoBehaviour
    {
        private struct Rigid
        {
            public Quaternion rotation;
            public Vector3 translation;
            public static Rigid Identity => new Rigid { rotation = Quaternion.identity };
            public Vector3 Point(Vector3 point) => rotation * point + translation;
            // this ∘ inner
            public Rigid After(Rigid inner) => new Rigid
            {
                rotation = rotation * inner.rotation,
                translation = rotation * inner.translation + translation
            };
        }

        private sealed class Spin
        {
            public PlacedBlock block;
            public Vector3Int axis;
            public float degreesPerSecond;
            public readonly HashSet<PlacedBlock> group = new HashSet<PlacedBlock>();
            public int depth;
        }

        private static readonly Vector3Int[] Neighbors =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up,
            Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };

        private readonly Dictionary<Transform, (Vector3 position, Quaternion rotation)> originals =
            new Dictionary<Transform, (Vector3, Quaternion)>();
        private readonly Dictionary<Transform, Quaternion> arrowRelative = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<PlacedBlock, Rigid> poses = new Dictionary<PlacedBlock, Rigid>();
        private readonly Dictionary<Vector3Int, PlacedBlock> cells = new Dictionary<Vector3Int, PlacedBlock>();
        private readonly List<Spin> spins = new List<Spin>();
        private CubeAssembly assembly;
        private ProjectileStatsSource statsSource;
        private EquipableWeapon weapon;
        private float elapsed;
        private bool spinning;

        public bool IsSpinning => spinning;

        private void Awake()
        {
            assembly = GetComponent<CubeAssembly>();
            statsSource = GetComponent<ProjectileStatsSource>();
        }

        private void OnDisable() => StopAndRestore();

        private void LateUpdate()
        {
            if (weapon == null) weapon = GetComponentInParent<EquipableWeapon>();
            bool equipped = assembly != null && weapon != null && weapon.IsEquipped;
            if (!equipped)
            {
                if (spinning || originals.Count > 0) StopAndRestore();
                return;
            }
            spinning = true;
            if (!GameManager.GameplayBlocked) elapsed += Time.deltaTime;
            ComputePoses();
            ApplyVisuals();
        }

        /// <summary>Moves a muzzle (assembly-local) with the barrel that its last block belongs to.</summary>
        public void TransformMuzzle(PlacedBlock[] path, ref Vector3 origin, ref Vector3 direction)
        {
            if (!spinning || path == null || path.Length == 0) return;
            if (!poses.TryGetValue(path[path.Length - 1], out Rigid pose)) return;
            origin = pose.Point(origin);
            direction = pose.rotation * direction;
        }

        private void ComputePoses()
        {
            poses.Clear();
            cells.Clear();
            spins.Clear();
            foreach (PlacedBlock block in assembly.GetComponentsInChildren<PlacedBlock>())
                if (block.transform.parent == assembly.transform) cells[block.Cell] = block;

            HashSet<Vector3Int> powered = Powered(null);
            foreach (PlacedBlock block in cells.Values)
            {
                if (!powered.Contains(block.Cell) || !ModuleCounter.TryGetModuleType(block, out StatType type) ||
                    type != StatType.Spin) continue;
                Vector3Int[] outputs = block.GetOutputDirections();
                if (outputs.Length == 0) continue;
                var spin = new Spin { block = block, axis = outputs[0] };
                BuildGroup(spin, Powered(block));
                spins.Add(spin);
            }
            if (spins.Count == 0) return;

            float speed = statsSource != null ? statsSource.Tuning.moduleSpinDegreesPerSecond : 180f;
            foreach (Spin spin in spins)
            {
                spin.depth = 0;
                foreach (Spin other in spins)
                    if (other != spin && other.group.Contains(spin.block)) spin.depth++;
                spin.degreesPerSecond = speed * spin.block.GetComponent<ModuleStats>().Data.GetStats().spin;
            }
            // Innermost first, so outer barrels carry the inner ones: pose = outer ∘ ... ∘ inner.
            spins.Sort((a, b) =>
            {
                int order = b.depth.CompareTo(a.depth);
                if (order != 0) return order;
                Vector3Int ac = a.block.Cell, bc = b.block.Cell;
                order = ac.x.CompareTo(bc.x);
                if (order != 0) return order;
                order = ac.y.CompareTo(bc.y);
                return order != 0 ? order : ac.z.CompareTo(bc.z);
            });
            foreach (Spin spin in spins)
            {
                Vector3 pivot = spin.block.Cell;
                Quaternion turn = Quaternion.AngleAxis(elapsed * spin.degreesPerSecond, spin.axis);
                var rotation = new Rigid { rotation = turn, translation = pivot - turn * pivot };
                foreach (PlacedBlock member in spin.group)
                    poses[member] = rotation.After(poses.TryGetValue(member, out Rigid inner) ? inner : Rigid.Identity);
            }
        }

        // Blocks that core energy reaches; with `blocked` set, energy may not pass through that block.
        private HashSet<Vector3Int> Powered(PlacedBlock blocked)
        {
            var reached = new HashSet<Vector3Int>();
            foreach (Vector3Int direction in Neighbors)
                Flow(direction, direction, new HashSet<Vector3Int> { Vector3Int.zero }, blocked, reached);
            return reached;
        }

        private void Flow(Vector3Int cell, Vector3Int travelDirection, HashSet<Vector3Int> visited,
            PlacedBlock blocked, HashSet<Vector3Int> reached)
        {
            if (visited.Contains(cell) || !cells.TryGetValue(cell, out PlacedBlock block)) return;
            if (block == blocked || !block.AcceptsEnergy(travelDirection)) return;
            reached.Add(cell);
            var ancestry = new HashSet<Vector3Int>(visited) { cell };
            foreach (Vector3Int direction in block.GetOutputDirections())
                Flow(cell + direction, direction, ancestry, blocked, reached);
        }

        // Everything touching the spin module's front, except blocks fed from the core without passing it.
        private void BuildGroup(Spin spin, HashSet<Vector3Int> poweredWithoutSpin)
        {
            Vector3Int spinCell = spin.block.Cell;
            Vector3Int start = spinCell + spin.axis;
            if (!cells.TryGetValue(start, out PlacedBlock first) || poweredWithoutSpin.Contains(start)) return;
            var pending = new Queue<Vector3Int>();
            var seen = new HashSet<Vector3Int> { spinCell, Vector3Int.zero, start };
            pending.Enqueue(start);
            spin.group.Add(first);
            while (pending.Count > 0)
            {
                Vector3Int cell = pending.Dequeue();
                foreach (Vector3Int offset in Neighbors)
                {
                    Vector3Int next = cell + offset;
                    if (!seen.Add(next) || poweredWithoutSpin.Contains(next) ||
                        !cells.TryGetValue(next, out PlacedBlock block)) continue;
                    spin.group.Add(block);
                    pending.Enqueue(next);
                }
            }
        }

        // Blocks keep identity local rotation/scale (CubeAssembly.TryAttach), so children map directly.
        private void ApplyVisuals()
        {
            foreach (var entry in cells)
            {
                PlacedBlock block = entry.Value;
                Rigid pose = poses.TryGetValue(block, out Rigid found) ? found : Rigid.Identity;
                Vector3 blockPosition = block.transform.localPosition;
                // The hidden Arrow defines an extension's real output direction (ExtensionModule/ModuleDraw).
                // Record it before anything turns, then pin it so spinning never changes energy routes.
                ModuleDraw draw = block.GetComponent<ModuleDraw>();
                Transform arrow = draw != null ? draw.Arrow : null;
                if (arrow != null && !arrowRelative.ContainsKey(arrow))
                {
                    arrowRelative[arrow] = Quaternion.Inverse(block.transform.rotation) * arrow.rotation;
                    if (!originals.ContainsKey(arrow)) originals[arrow] = (arrow.localPosition, arrow.localRotation);
                }
                foreach (Transform child in block.transform)
                {
                    if (!originals.TryGetValue(child, out var original))
                    {
                        original = (child.localPosition, child.localRotation);
                        originals[child] = original;
                    }
                    child.localPosition = pose.Point(blockPosition + original.position) - blockPosition;
                    child.localRotation = pose.rotation * original.rotation;
                }
                if (arrow != null) arrow.rotation = block.transform.rotation * arrowRelative[arrow];
            }
        }

        private void StopAndRestore()
        {
            foreach (var entry in originals)
            {
                if (entry.Key == null) continue;
                entry.Key.localPosition = entry.Value.position;
                entry.Key.localRotation = entry.Value.rotation;
            }
            originals.Clear();
            arrowRelative.Clear();
            elapsed = 0f;
            spinning = false;
        }
    }
}
