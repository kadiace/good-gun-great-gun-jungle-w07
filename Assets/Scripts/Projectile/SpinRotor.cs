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
            public float spinStat;
            public readonly HashSet<PlacedBlock> group = new HashSet<PlacedBlock>();
            public int depth;
        }

        // 블록 하나의 구조 값. 칸·출력·에너지면·화살표·회전 모듈 여부는 조립이 바뀔 때만 읽는다.
        // 매 프레임 PlacedBlock을 조회하면 내부의 컴포넌트 탐색( 없는 컴포넌트는 에디터 전용 오류 경로 )과
        // 출력 방향 배열 할당이 그대로 반복되므로, 값은 캐시하고 회전 각도만 매 프레임 계산한다.
        private struct Node
        {
            public PlacedBlock block;
            public Vector3Int[] outputs;
            public uint accepts;
            public Transform arrow;
            public bool spin;
            public float spinStat;
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
        private readonly Dictionary<Vector3Int, Node> cells = new Dictionary<Vector3Int, Node>();
        private readonly List<Spin> spins = new List<Spin>();
        // 매 프레임 LateUpdate 경로의 탐색용 컬렉션과 스핀 개체는 재사용해 GC 할당을 없앤다.
        private readonly List<Spin> spinPool = new List<Spin>();
        private readonly List<PlacedBlock> blockScan = new List<PlacedBlock>();
        private readonly HashSet<Vector3Int> ancestry = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> poweredAll = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> poweredNoSpin = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> buildSeen = new HashSet<Vector3Int>();
        private readonly Queue<Vector3Int> buildQueue = new Queue<Vector3Int>();
        private CubeAssembly assembly;
        private ProjectileStatsSource statsSource;
        private EquipableWeapon weapon;
        private EquipableWeapon subscribedWeapon;
        // 캐시한 블록 값이 낡았는지 보는 구조 서명(블록 수·식별자·칸·종류)과 조립 변경 표시.
        private int cachedBlockCount = -1;
        private int cachedSignature;
        private bool structureDirty;
        private float elapsed;
        private bool spinning;

        public bool IsSpinning => spinning;

        private void Awake()
        {
            assembly = GetComponent<CubeAssembly>();
            statsSource = GetComponent<ProjectileStatsSource>();
        }

        private void OnDisable()
        {
            StopAndRestore();
            SubscribeWeapon(null);
        }

        private void LateUpdate()
        {
            if (weapon == null) weapon = GetComponentInParent<EquipableWeapon>();
            SubscribeWeapon(weapon);
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

        // 조립이 바뀌면 NotifyAssemblyChanged가 오른다(블록 배치·모듈 방향 변경). 이벤트가 없어도 구조 서명으로 걸러낸다.
        private void SubscribeWeapon(EquipableWeapon next)
        {
            if (ReferenceEquals(subscribedWeapon, next)) return;
            if (!ReferenceEquals(subscribedWeapon, null)) subscribedWeapon.AssemblyChanged -= OnAssemblyChanged;
            subscribedWeapon = next;
            if (next == null) return;
            next.AssemblyChanged += OnAssemblyChanged;
            structureDirty = true;
        }

        private void OnAssemblyChanged() => structureDirty = true;

        private void ComputePoses()
        {
            poses.Clear();
            // 값 재계산이 필요한 프레임은 여기뿐이다. 나머지 프레임은 회전 각도와 외형 적용만 돈다.
            if (RefreshCells()) RebuildSpins();
            if (spins.Count == 0) return;

            float speed = statsSource != null ? statsSource.Tuning.moduleSpinDegreesPerSecond : 180f;
            foreach (Spin spin in spins) spin.degreesPerSecond = speed * spin.spinStat;
            foreach (Spin spin in spins)
            {
                Vector3 pivot = spin.block.Cell;
                Quaternion turn = Quaternion.AngleAxis(elapsed * spin.degreesPerSecond, spin.axis);
                var rotation = new Rigid { rotation = turn, translation = pivot - turn * pivot };
                foreach (PlacedBlock member in spin.group)
                    poses[member] = rotation.After(poses.TryGetValue(member, out Rigid inner) ? inner : Rigid.Identity);
            }
        }

        // 블록 집합의 서명이 그대로면 캐시를 쓰고, 바뀌면 블록 값과 칸 배치를 다시 채운다.
        private bool RefreshCells()
        {
            Transform assemblyTransform = assembly.transform;
            blockScan.Clear();
            assembly.GetComponentsInChildren(false, blockScan);
            int count = 0;
            int signature = 0;
            for (int i = 0; i < blockScan.Count; i++)
            {
                PlacedBlock block = blockScan[i];
                if (block.transform.parent != assemblyTransform) continue;
                Vector3Int cell = block.Cell;
                count++;
                // 순서와 무관한 서명: 블록 식별자·칸·종류.
                signature ^= unchecked(block.GetInstanceID() * 397 + cell.x * 73856093 +
                    cell.y * 19349663 + cell.z * 83492791 + (int)block.Kind * 8191);
            }
            if (!structureDirty && count == cachedBlockCount && signature == cachedSignature) return false;

            cells.Clear();
            foreach (PlacedBlock block in blockScan)
                if (block.transform.parent == assemblyTransform) cells[block.Cell] = BuildNode(block);
            cachedBlockCount = count;
            cachedSignature = signature;
            structureDirty = false;
            return true;
        }

        private Node BuildNode(PlacedBlock block)
        {
            var node = new Node
            {
                block = block,
                outputs = block.GetOutputDirections(),
                // 화살표는 ModuleDraw가 있는 블록에만 있다. 없는 블록은 TryGetComponent로 조회 실패 경로를 타지 않는다.
                arrow = block.TryGetComponent(out ModuleDraw draw) ? draw.Arrow : null,
            };
            uint accepts = 0;
            for (int i = 0; i < Neighbors.Length; i++)
                if (block.AcceptsEnergy(Neighbors[i])) accepts |= 1u << i;
            node.accepts = accepts;
            node.spin = ModuleCounter.TryGetModuleType(block, out StatType type) && type == StatType.Spin;
            if (node.spin) node.spinStat = block.GetComponent<ModuleStats>().Data.GetStats().spin;
            return node;
        }

        // 코어 에너지 범위·회전 덩어리·회전 순서. 조립이 바뀐 프레임에만 돈다.
        private void RebuildSpins()
        {
            spins.Clear();
            Powered(null, poweredAll);
            foreach (KeyValuePair<Vector3Int, Node> entry in cells)
            {
                Node node = entry.Value;
                if (!node.spin || !poweredAll.Contains(entry.Key) || node.outputs.Length == 0) continue;
                if (spins.Count == spinPool.Count) spinPool.Add(new Spin());
                Spin spin = spinPool[spins.Count];
                spin.block = node.block;
                spin.axis = node.outputs[0];
                spin.spinStat = node.spinStat;
                spin.group.Clear();
                Powered(node.block, poweredNoSpin);
                BuildGroup(spin, poweredNoSpin);
                spins.Add(spin);
            }
            if (spins.Count == 0) return;

            foreach (Spin spin in spins)
            {
                spin.depth = 0;
                foreach (Spin other in spins)
                    if (other != spin && other.group.Contains(spin.block)) spin.depth++;
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
        }

        // Blocks that core energy reaches; with `blocked` set, energy may not pass through that block.
        // reached는 호출자가 제공한 집합을 재사용하고, 방문 집합 복사 대신 공유 ancestry에 재귀 표식을 넣고 되돌리며 지운다.
        private void Powered(PlacedBlock blocked, HashSet<Vector3Int> reached)
        {
            reached.Clear();
            foreach (Vector3Int direction in Neighbors)
            {
                ancestry.Clear();
                ancestry.Add(Vector3Int.zero);
                Flow(direction, direction, blocked, reached);
            }
        }

        private void Flow(Vector3Int cell, Vector3Int travelDirection,
            PlacedBlock blocked, HashSet<Vector3Int> reached)
        {
            if (!ancestry.Add(cell)) return;
            if (!cells.TryGetValue(cell, out Node node) || node.block == blocked || !Accepts(node, travelDirection))
            {
                ancestry.Remove(cell);
                return;
            }
            reached.Add(cell);
            foreach (Vector3Int direction in node.outputs)
                Flow(cell + direction, direction, blocked, reached);
            ancestry.Remove(cell);
        }

        // PlacedBlock.AcceptsEnergy와 같은 판별. 면은 캐시한 비트로 보고, 여섯 면 밖의 방향은 본체에 묻는다.
        private static bool Accepts(in Node node, Vector3Int travelDirection)
        {
            for (int i = 0; i < Neighbors.Length; i++)
                if (Neighbors[i] == travelDirection) return (node.accepts & (1u << i)) != 0;
            return node.block != null && node.block.AcceptsEnergy(travelDirection);
        }

        // Everything touching the spin module's front, except blocks fed from the core without passing it.
        private void BuildGroup(Spin spin, HashSet<Vector3Int> poweredWithoutSpin)
        {
            Vector3Int spinCell = spin.block.Cell;
            Vector3Int start = spinCell + spin.axis;
            if (!cells.TryGetValue(start, out Node first) || poweredWithoutSpin.Contains(start)) return;
            buildQueue.Clear();
            buildSeen.Clear();
            buildSeen.Add(spinCell);
            buildSeen.Add(Vector3Int.zero);
            buildSeen.Add(start);
            buildQueue.Enqueue(start);
            spin.group.Add(first.block);
            while (buildQueue.Count > 0)
            {
                Vector3Int cell = buildQueue.Dequeue();
                foreach (Vector3Int offset in Neighbors)
                {
                    Vector3Int next = cell + offset;
                    if (!buildSeen.Add(next) || poweredWithoutSpin.Contains(next) ||
                        !cells.TryGetValue(next, out Node node)) continue;
                    spin.group.Add(node.block);
                    buildQueue.Enqueue(next);
                }
            }
        }

        // Blocks keep identity local rotation/scale (CubeAssembly.TryAttach), so children map directly.
        private void ApplyVisuals()
        {
            foreach (KeyValuePair<Vector3Int, Node> entry in cells)
            {
                PlacedBlock block = entry.Value.block;
                Rigid pose = poses.TryGetValue(block, out Rigid found) ? found : Rigid.Identity;
                Vector3 blockPosition = block.transform.localPosition;
                // The hidden Arrow defines an extension's real output direction (ExtensionModule/ModuleDraw).
                // Record it before anything turns, then pin it so spinning never changes energy routes.
                Transform arrow = entry.Value.arrow;
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
            cells.Clear();
            spins.Clear();
            cachedBlockCount = -1;
            cachedSignature = 0;
            structureDirty = true;
            elapsed = 0f;
            spinning = false;
        }
    }
}
