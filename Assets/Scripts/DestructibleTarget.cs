using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    public sealed partial class DestructibleTarget : MonoBehaviour
    {
        // 블록 ID와 파편 생성 여부에 무관한 실제 셀 제거 알림. 저장 복원은 제외한다.
        public static event System.Action<DestructibleTarget> PartDestroyed;
        public static event System.Action<DestructibleTarget, string> CellDestroyed;
        [SerializeField, HideInInspector] private string targetId;
        public string TargetId => targetId;
        public string StageId => gameObject.scene.name;

        public bool TryGetCellId(Vector3Int coordinate, out string id)
        {
            id = null;
            if (string.IsNullOrEmpty(targetId) || targetMesh == null || targetMesh.sharedMesh == null) return false;
            Vector3Int size = subdivided ? divisions : GetGridDivisions();
            if (coordinate.x < 0 || coordinate.y < 0 || coordinate.z < 0 ||
                coordinate.x >= size.x || coordinate.y >= size.y || coordinate.z >= size.z ||
                (subdivided && !HasCell(coordinate))) return false;
            id = StageId + "/" + targetId + "/" + coordinate.x + "_" + coordinate.y + "_" + coordinate.z;
            return true;
        }

        private Vector3Int GetGridDivisions()
        {
            Bounds bounds = targetMesh.sharedMesh.bounds;
            Transform root = targetMesh.transform;
            float size = Mathf.Max(0.01f, partSize);
            return new Vector3Int(
                Mathf.Max(1, Mathf.CeilToInt(root.TransformVector(Vector3.right).magnitude * bounds.size.x / size - 0.00001f)),
                Mathf.Max(1, Mathf.CeilToInt(root.TransformVector(Vector3.up).magnitude * bounds.size.y / size - 0.00001f)),
                Mathf.Max(1, Mathf.CeilToInt(root.TransformVector(Vector3.forward).magnitude * bounds.size.z / size - 0.00001f)));
        }
        public bool DamageProtected { get; set; }
        [SerializeField, Tooltip("차량/몹에 붙은 부품은 코어 파괴 전까지 건물 지지 계산에서 제외합니다.")]
        private bool attachedToMovingBody;
        internal bool AttachedToMovingBody => attachedToMovingBody;
        [SerializeField, Tooltip("독립 소품은 건물 전체의 지지 연결과 자동 붕괴 판정에서 제외합니다. 피격 파괴는 유지합니다.")]
        private bool standaloneProp;
        internal bool ParticipatesInStructuralSupport => !attachedToMovingBody && !standaloneProp;
        [SerializeField, Tooltip("파괴할 메시와 콜라이더가 있는 자식 오브젝트. 비어 있으면 자식 메시를 찾습니다.")]
        private MeshFilter targetMesh;
        [SerializeField, Tooltip("빌드에도 포함할 투명 GPU 인스턴싱 파편 머테리얼.")]
        private Material debrisMaterial;
        private Transform CellRoot => targetMesh.transform;
        internal Matrix4x4 PenetrationGeometryMatrix => CellRoot.localToWorldMatrix;
        [SerializeField, Min(0.01f), Tooltip("월드 좌표 기준 조각 한 변의 길이(m). 가장자리 잔여 조각은 더 작을 수 있습니다.")]
        private float partSize = 0.2f;
        [SerializeField, Min(0f), Tooltip("기본 관통 저항. 관통력>1인 탄환은 저항이 더 낮으면 경로 셀을 파괴하고 통과. 같으면 저항 감소 후 통과, 더 높으면 저항 감소 후 탄환 소멸. 탄환은 블록당 관통력1 소모.")]
        private float penetrationResistance = 1f;
        private float penetrationResistanceLoss;
        [SerializeField, Min(0f), Tooltip("폭발 파괴 기준과 한 칸(Part Size) 이동할 때 차감하는 폭발력.")]
        private float explosionResistance = 1f;
        [SerializeField, Min(0.01f), Tooltip("구형 폭발 반경의 초당 증가량(m/s). 착탄 표적 설정을 폭발 전체에 적용.")]
        private float explosionExpansionSpeed = 10f;
        [SerializeField, Min(0.01f), Tooltip("폭발 검사 및 저항 차감 구간의 월드 거리(m). Part Size와 무관.")]
        private float explosionCheckDistance = 0.2f;
        public float ExplosionExpansionSpeed => Mathf.Max(0.01f, explosionExpansionSpeed);
        public float ExplosionCheckDistance => Mathf.Max(0.01f, explosionCheckDistance);
        [SerializeField, Tooltip("태그 SO의 수치 하한을 저항으로 사용. 무게·스코프·중력·상한은 제외.")]
        private WeaponTypeData resistanceSource;
        [SerializeField, Min(0f)] private float heatResistance;
        [SerializeField, Min(0f)] private float waterResistance;
        public float PenetrationResistance => Mathf.Max(0f, penetrationResistance - penetrationResistanceLoss);
        public float HeatResistance => Mathf.Max(0f, heatResistance);
        public float WaterResistance => Mathf.Max(0f, waterResistance);

        public void ApplyResistanceSource()
        {
            if (resistanceSource == null) return;
            penetrationResistance = explosionResistance = heatResistance = waterResistance = 0f;
            foreach (var condition in resistanceSource.Conditions)
            {
                if (condition.comparison != WeaponTypeData.Comparison.AtLeast) continue;
                float value = Mathf.Max(0f, condition.value);
                switch (condition.stat)
                {
                    case StatType.Penetration: penetrationResistance = Mathf.Max(penetrationResistance, value); break;
                    case StatType.Explosion: explosionResistance = Mathf.Max(explosionResistance, value); break;
                    case StatType.Heat: heatResistance = Mathf.Max(heatResistance, value); break;
                    case StatType.Water: waterResistance = Mathf.Max(waterResistance, value); break;
                }
            }
        }


        public void ConfigureCustom(Vector3 size, float penetration, float explosion, float heat, float water)
        {
            resistanceSource = null;
            penetrationResistance = Mathf.Max(0f, penetration);
            penetrationResistanceLoss = 0f;
            explosionResistance = Mathf.Max(0f, explosion);
            heatResistance = Mathf.Max(0f, heat);
            waterResistance = Mathf.Max(0f, water);
            if (targetMesh == null) targetMesh = GetComponentInChildren<MeshFilter>(true);
            intactGridReady = false;
            targetMesh.transform.localScale = size;
            targetMesh.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            MarkDebrisIndexDirty();
        }

        internal bool TryDebrisBounds(out Bounds bounds)
        {
            bounds = default;
            if (!isActiveAndEnabled || targetMesh == null || targetMesh.sharedMesh == null || (subdivided && cells.Count == 0)) return false;
            var local = targetMesh.sharedMesh.bounds;
            var matrix = CellRoot.localToWorldMatrix;
            var x = matrix.MultiplyVector(Vector3.right * local.extents.x);
            var y = matrix.MultiplyVector(Vector3.up * local.extents.y);
            var z = matrix.MultiplyVector(Vector3.forward * local.extents.z);
            bounds = new Bounds(matrix.MultiplyPoint3x4(local.center), new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2f);
            return true;
        }

        // Read current occupied cells without creating physics objects or forcing subdivision.
        private readonly HashSet<Vector3Int> debrisVisited = new HashSet<Vector3Int>();
        private void CheckDebrisCell(Vector3Int coordinate, Vector3 a, Vector3 b, Vector3 padding,
            ref float time, ref Vector3 normal, ref Vector3 point)
        {
            if (!TryGetCell(coordinate, out var cell)) return;
            var bounds = new Bounds(cell.localPosition, cell.localScale + padding * 2f);
            if (!DebrisWallCache.SweepBounds(bounds, a, b, out float t, out var n, out var hit) || t >= time) return;
            time = t; normal = n; point = hit;
        }
        internal bool SweepDebris(Vector3 from, Vector3 to, float radius, out float time, out Vector3 normal, out Vector3 point)
        {
            time = 2f; normal = point = default;
            if (targetMesh == null || targetMesh.sharedMesh == null || (subdivided && cells.Count == 0)) return false;
            var root = CellRoot;
            var a = root.InverseTransformPoint(from);
            var b = root.InverseTransformPoint(to);
            var scale = root.lossyScale;
            var padding = new Vector3(radius / Mathf.Max(Mathf.Abs(scale.x), .00001f),
                radius / Mathf.Max(Mathf.Abs(scale.y), .00001f), radius / Mathf.Max(Mathf.Abs(scale.z), .00001f));
            var outer = targetMesh.sharedMesh.bounds;
            outer.Expand(padding * 2f);
            if (!DebrisWallCache.SweepBounds(outer, a, b, out _, out _, out _)) return false;
            if (!subdivided)
                DebrisWallCache.SweepBounds(outer, a, b, out time, out normal, out point);
            else
            {
                var low = Vector3.Min(a, b) - padding;
                var high = Vector3.Max(a, b) + padding;
                var min = new Vector3Int(); var max = new Vector3Int();
                for (int axis = 0; axis < 3; axis++)
                {
                    min[axis] = Mathf.Max(0, Mathf.FloorToInt((low[axis] - gridOrigin[axis]) / gridStep[axis]));
                    max[axis] = Mathf.Min(divisions[axis] - 1, Mathf.FloorToInt((high[axis] - gridOrigin[axis]) / gridStep[axis]));
                }
                // Include boundary neighbours even for a zero-radius ray crossing an exact edge/corner.
                var neighborhood = new Vector3Int(Mathf.Max(1, Mathf.CeilToInt(padding.x / gridStep.x)),
                    Mathf.Max(1, Mathf.CeilToInt(padding.y / gridStep.y)), Mathf.Max(1, Mathf.CeilToInt(padding.z / gridStep.z)));
                long boxCost = (long)(max.x - min.x + 1) * (max.y - min.y + 1) * (max.z - min.z + 1);
                long pathCost = (long)(Mathf.Abs(max.x - min.x) + Mathf.Abs(max.y - min.y) + Mathf.Abs(max.z - min.z) + 1)
                    * (neighborhood.x * 2 + 1) * (neighborhood.y * 2 + 1) * (neighborhood.z * 2 + 1);
                // Small/axis-aligned sweeps are cheaper as a compact box. DDA is for sparse diagonal corridors.
                if (boxCost <= pathCost)
                {
                    for (int x = min.x; x <= max.x; x++) for (int y = min.y; y <= max.y; y++) for (int z = min.z; z <= max.z; z++)
                        CheckDebrisCell(new Vector3Int(x, y, z), a, b, padding, ref time, ref normal, ref point);
                }
                else if (DebrisWallCache.ClipBounds(outer, a, b, out float enter, out float exit))
                {
                    debrisVisited.Clear();
                    var start = Vector3.LerpUnclamped(a, b, enter);
                    var end = Vector3.LerpUnclamped(a, b, exit);
                    var delta = end - start;
                    var coordinate = new Vector3Int(); var finish = new Vector3Int(); var step = new Vector3Int();
                    var next = Vector3.zero; var increment = Vector3.zero;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        coordinate[axis] = Mathf.FloorToInt((start[axis] - gridOrigin[axis]) / gridStep[axis]);
                        finish[axis] = Mathf.FloorToInt((end[axis] - gridOrigin[axis]) / gridStep[axis]);
                        step[axis] = delta[axis] > 0f ? 1 : delta[axis] < 0f ? -1 : 0;
                        increment[axis] = step[axis] == 0 ? float.PositiveInfinity : gridStep[axis] / Mathf.Abs(delta[axis]);
                        next[axis] = step[axis] == 0 ? float.PositiveInfinity :
                            (gridOrigin[axis] + (coordinate[axis] + (step[axis] > 0 ? 1 : 0)) * gridStep[axis] - start[axis]) / delta[axis];
                    }
                    int remaining = Mathf.Abs(finish.x - coordinate.x) + Mathf.Abs(finish.y - coordinate.y) + Mathf.Abs(finish.z - coordinate.z) + 1;
                    while (remaining-- > 0)
                    {
                        for (int x = Mathf.Max(min.x, coordinate.x - neighborhood.x); x <= Mathf.Min(max.x, coordinate.x + neighborhood.x); x++)
                            for (int y = Mathf.Max(min.y, coordinate.y - neighborhood.y); y <= Mathf.Min(max.y, coordinate.y + neighborhood.y); y++)
                                for (int z = Mathf.Max(min.z, coordinate.z - neighborhood.z); z <= Mathf.Min(max.z, coordinate.z + neighborhood.z); z++)
                                {
                                    var key = new Vector3Int(x, y, z);
                                    if (debrisVisited.Add(key)) CheckDebrisCell(key, a, b, padding, ref time, ref normal, ref point);
                                }
                        if (coordinate == finish) break;
                        int axis = next.x <= next.y && next.x <= next.z ? 0 : next.y <= next.z ? 1 : 2;
                        coordinate[axis] += step[axis]; next[axis] += increment[axis];
                    }
                }
            }
            if (time > 1f) return false;
            normal = root.localToWorldMatrix.inverse.transpose.MultiplyVector(normal).normalized;
            point = root.TransformPoint(point);
            return true;
        }

        private void OnValidate() => ApplyResistanceSource();

        public bool AllowsImpact(ProjectileStats stats)
        {
            return !DamageProtected && stats.heat >= heatResistance && stats.water >= waterResistance;
        }

        public void BreakAttributeImpact(ProjectileStats stats, Vector3 center, Quaternion rotation, Vector3 halfExtents)
        {
            bool water = stats.water > 0f && waterResistance > 0f && stats.water >= waterResistance;
            bool heat = stats.water <= 0f && stats.heat > 0f && heatResistance > 0f && stats.heat >= heatResistance;
            if (!enabled || !AllowsImpact(stats) || !(water || heat)) return;
            if (!subdivided) Subdivide(false);
            var removed = CollectParticleCells(center, rotation, halfExtents);
            foreach (int cellIndex in removed) Detach(cellIndex);
            if (removed.Count > 0) MarkStructureDirty();
            UpdateCollisionRuns();
        }

        private Vector3 gridOrigin, gridStep;
        private int totalCellCount;
        private VisualDebrisCloud debrisCloud;
        [SerializeField, Min(0.1f), Tooltip("중력탄 파편이 피격 지점으로 줄어들며 사라지는 시간.")]
        private float gravityAbsorptionDuration = 0.8f;
        [SerializeField, Min(0f)] private float gravitySpreadDistance = 0.35f;
        [SerializeField, Range(0.05f, 0.8f)] private float gravitySpreadFraction = 0.25f;
        private bool gravityImpact;
        private Vector3 gravityImpactCenter;

        public void BeginGravityImpact(Vector3 center)
        {
            if (DamageProtected) return;
            gravityImpact = true;
            gravityImpactCenter = center;
            debrisCloud?.SetAbsorptionEmission(true, center, gravityAbsorptionDuration, gravitySpreadDistance, gravitySpreadFraction);
        }

        public void EndGravityImpact()
        {
            if (gravityImpact && structureDirty)
            {
                structureDirty = false;
                ResolveSupport();
                UpdateCollisionRuns();
            }
            gravityImpact = false;
            debrisCloud?.SetAbsorptionEmission(false, Vector3.zero, gravityAbsorptionDuration);
        }
        private bool structureDirty;
        private struct Cell
        {
            public double explosionHealth;
            public float explosionHitTime;
            public Transform root;
            public int slot;
            public Vector3Int coordinate;
            public Vector3 localPosition, localScale;
            public Vector3 position => root.TransformPoint(localPosition);
            public Vector3 lossyScale => Vector3.Scale(root.lossyScale, localScale);
            public Vector3 TransformVector(Vector3 vector) => root.TransformVector(Vector3.Scale(localScale, vector));
            public VisualDebrisCloud.Piece Snapshot => new VisualDebrisCloud.Piece { position = position, rotation = root.rotation, scale = lossyScale };
        }
        private List<BoxCollider>[] collisionColumns;
        private readonly List<BoxCollider> collisionColliderPool = new List<BoxCollider>();
        private int collisionColliderCursor;
        // 풀 오브젝트에 실리지 않은 표적만 콜라이더를 공용 인덱스에 고정 등록한다. 대여 때마다 부모가
        // 바뀌는 풀 소속 표적은 등록하면 낡은 소속을 캐시하게 되므로 기존 부모 탐색 경로를 유지한다.
        private bool registersColliders;
        // 원본 콜라이더의 풀 편입은 한 번이면 충분하다. 매 성장마다 풀 전체를 다시 훑던 비용을 없앤다.
        private bool collisionPoolMergedOriginals;
        private static readonly Stack<List<BoxCollider>> collisionColumnListPool = new Stack<List<BoxCollider>>(512);
        private bool[] dirtyColumnFlags;
        private readonly List<int> dirtyColumns = new List<int>();
        private bool allColumnsDirty = true;
        private readonly List<int> impactCells = new List<int>();
        // Reused snapshots for the per-hit removal loops so repeated hits do not allocate a list each call.
        private readonly List<int> absorbedCells = new List<int>();
        private readonly List<int> breakAllCells = new List<int>();
        private System.Comparison<int> cellSlotComparison;
        private readonly HashSet<Vector3Int> supportedCells = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> fallingCoordinates = new HashSet<Vector3Int>();
        private readonly Queue<Vector3Int> supportQueue = new Queue<Vector3Int>();
        private readonly List<int> fallingCells = new List<int>();
        private Vector3Int divisions;
        private bool collisionDirty;
        private readonly List<VisualDebrisCloud.Piece> groupPieces = new List<VisualDebrisCloud.Piece>();
        private Cell[] preparedCells = System.Array.Empty<Cell>();

        private bool renderDirty;
        private Material instancedMaterial;
        private Mesh cellMesh;
        [SerializeField, Min(0.1f)] private float debrisLifetime = 2f;
        private readonly List<int> cells = new List<int>();
        [SerializeField, Min(0.01f), Tooltip("파편 크기를 유지한 채 투명해지며 사라지는 시간.")]
        private float debrisFadeDuration = 0.2f;
        [SerializeField, Min(0f)] private float debrisFloorBounce = 0.25f;
        [SerializeField, Min(0f)] private float debrisFloorFriction = 0.8f;
        private bool[] grid = System.Array.Empty<bool>();
        private static readonly Vector3Int[] neighbors = { Vector3Int.right, Vector3Int.left,
            Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };

        private int CellIndex(Vector3Int coordinate) => coordinate.x * divisions.y * divisions.z
            + coordinate.y * divisions.z + coordinate.z;
        private bool TryGetCell(Vector3Int coordinate, out Cell cell)
        {
            cell = default;
            if (grid.Length == 0 || coordinate.x < 0 || coordinate.y < 0 || coordinate.z < 0 ||
                coordinate.x >= divisions.x || coordinate.y >= divisions.y || coordinate.z >= divisions.z) return false;
            int index = CellIndex(coordinate);
            if (!grid[index]) return false;
            cell = preparedCells[index];
            return true;
        }
        private bool HasCell(Vector3Int coordinate) => TryGetCell(coordinate, out _);
        private bool subdivided;
        private bool restoringSave;
        private bool transformMayMove;
        private bool debrisIndexDirty = true;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 initialScale;
        private Renderer originalRenderer;
        private readonly Dictionary<Collider, bool> originalColliders = new Dictionary<Collider, bool>();

        private void Awake()
        {
            CachePhysicalGravityComponents();
            ApplyResistanceSource();
            initialPosition = transform.localPosition;
            initialRotation = transform.localRotation;
            initialScale = transform.localScale;
            if (targetMesh == null) targetMesh = GetComponentInChildren<MeshFilter>(true);
            if (targetMesh == null || targetMesh.sharedMesh == null)
            {
                Debug.LogError("DestructibleTarget에 파괴할 Target Mesh를 연결하세요.", this);
                enabled = false;
                return;
            }
            originalRenderer = targetMesh.GetComponent<Renderer>();
            Bounds bounds = targetMesh.sharedMesh.bounds;
            float size = Mathf.Max(0.01f, partSize);
            Vector3 worldSize = new Vector3(CellRoot.TransformVector(Vector3.right).magnitude * bounds.size.x,
                CellRoot.TransformVector(Vector3.up).magnitude * bounds.size.y,
                CellRoot.TransformVector(Vector3.forward).magnitude * bounds.size.z);
            totalCellCount = Mathf.Max(1, Mathf.CeilToInt(worldSize.x / size - 0.00001f))
                * Mathf.Max(1, Mathf.CeilToInt(worldSize.y / size - 0.00001f))
                * Mathf.Max(1, Mathf.CeilToInt(worldSize.z / size - 0.00001f));
            registersColliders = GetComponentInParent<PooledCombatObject>(true) == null;
            foreach (Collider collider in targetMesh.GetComponents<Collider>())
            {
                originalColliders.Add(collider, collider.enabled);
                // 미분할 상태에서 맞는 원본 콜라이더도 조회 때 부모 탐색 없이 바로 풀리도록 등록한다.
                if (registersColliders) ColliderTargetIndex.RegisterStructure(collider, this);
            }
            // Collision colliders grow on demand (AcquireCollisionCollider) and stay pooled; a worst-case prewarm
            // here would add hundreds of BoxColliders per target at scene load.
            CacheTransformMovement();
            StructuralSupportGraph.Register(this);
        }

        private void OnEnable()
        {
            debrisIndexed = false;
            debrisIndexDirty = true;
            RefreshDebrisIndex();
            if (originalRenderer != null) StructuralSupportGraph.Register(this);
            CacheTransformMovement();
            RefreshUpdaterRegistration();
        }

        internal bool NeedsLateUpdate => isActiveAndEnabled &&
            (damageDataDirty || penetrationFlashActive || collisionDirty || (renderDirty && subdivided && cells.Count > 0) || structureDirty || debrisIndexDirty ||
                (transformMayMove && (!subdivided || cells.Count > 0)));
        internal bool NeedsRenderUpdate => isActiveAndEnabled && subdivided && cells.Count > 0;
        internal bool TracksDebrisTransform => isActiveAndEnabled && transformMayMove && (!subdivided || cells.Count > 0);

        private void CacheTransformMovement()
        {
            transformMayMove = attachedToMovingBody || GetComponentInParent<Rigidbody>() != null ||
                (targetMesh != null && targetMesh.GetComponentInParent<Rigidbody>() != null);
        }

        private void OnTransformParentChanged()
        {
            CacheTransformMovement();
            RefreshUpdaterRegistration();
        }

        internal void RefreshUpdaterRegistration() => DestructibleTargetUpdater.RefreshRegistration(this);

        private void MarkStructureDirty()
        {
            structureDirty = true;
            RefreshUpdaterRegistration();
        }

        private void MarkDebrisIndexDirty()
        {
            debrisIndexDirty = true;
            RefreshUpdaterRegistration();
        }

        // Static intact targets are indexed on enable and need no per-frame transform checks.
        private bool debrisIndexed;
        internal void RefreshDebrisIndex()
        {
            Transform root = targetMesh != null ? targetMesh.transform : null;
            if (debrisIndexed && !debrisIndexDirty && root != null && !root.hasChanged) return;
            if (root != null) root.hasChanged = false;
            DebrisTargetIndex.Update(this);
            debrisIndexed = true;
            debrisIndexDirty = false;
        }

        internal void ProcessLateUpdate()
        {
            UploadDamageAppearance();
            UpdatePenetrationFeedback();
            RefreshDebrisIndex();
            if (structureDirty)
            {
                structureDirty = false;
                ResolveSupport();
            }
            if (!subdivided) return;
            BreakSmallRemnant();
            if (collisionDirty) PoolManager.QueueCollisionFinish(this);
            RefreshUpdaterRegistration();
        }
        internal void DrawLateUpdate() { if (subdivided) DrawRemainingChunks(); }

        public float PenetrationStepDistance => Mathf.Max(0.01f, partSize);

        // Compatibility for older callers; active bullets pass their actual forward segment.
        public float Penetrate(float penetration, Vector3 center, Quaternion rotation, Vector3 halfExtents,
            out bool penetrated, out float resistance)
            => PenetrateAlongLine(penetration, center, rotation, halfExtents, 0f, out penetrated, out resistance);

        public float PenetrateAlongLine(float penetration, Vector3 start, Quaternion rotation, Vector3 halfExtents,
            float distance, out bool penetrated, out float resistance)
            => PenetrateAlongLine(penetration, start, rotation, halfExtents, distance,
                out penetrated, out resistance, out _);

        public float PenetrateAlongLine(float penetration, Vector3 start, Quaternion rotation, Vector3 halfExtents,
            float distance, out bool penetrated, out float resistance, out bool penetratedMaterial,
            bool allowResistanceReduction = true)
        {
            penetrated = false;
            penetratedMaterial = false;
            resistance = PenetrationResistance;
            if (DamageProtected || !isActiveAndEnabled || penetration < 0f) return penetration;
            List<int> touched = null;
            // An intact block already has an exact collider hit. Only split geometry needs a hole check.
            if (subdivided)
            {
                distance = Mathf.Clamp(distance, 0f, PenetrationStepDistance);
                touched = CollectPenetrationCells(start, rotation, halfExtents, distance);
                if (touched.Count == 0)
                {
                    penetrated = true;
                    return penetration;
                }
            }
            // Low-power bullets stop at the wall without weakening or destroying it.
            if (penetration <= 1f)
            {
                return penetration;
            }
            if (allowResistanceReduction)
            {
                if (resistance < penetration)
                {
                    if (touched == null)
                    {
                        Subdivide(false);
                        distance = Mathf.Clamp(distance, 0f, PenetrationStepDistance);
                        touched = CollectPenetrationCells(start, rotation, halfExtents, distance);
                    }
                    penetratedMaterial = RemovePenetrationCells(touched);
                }
                else
                {
                    // Compare BEFORE the hit: equality weakens to zero and destroys on a later hit.
                    penetrationResistanceLoss = Mathf.Min(Mathf.Max(0f, penetrationResistance),
                        penetrationResistanceLoss + penetration);
                    ShowPenetrationHit();
                }
            }
            // Use resistance BEFORE this hit: weakening a stronger wall does not
            // grant passage to the bullet that just hit it.
            penetrated = resistance <= penetration;
            // The caller owns projectile power consumption, separately from wall resistance.
            return penetration;
        }

        // A successful block penetration grants this bullet a passage, not destruction of the
        // entire mesh. The motor continues carving only its actual swept volume across frames.
        internal bool DestroyPenetrationCells(Vector3 start, Quaternion rotation, Vector3 halfExtents, float distance)
        {
            if (DamageProtected || !isActiveAndEnabled || targetMesh == null || (subdivided && cells.Count == 0)) return false;
            if (!subdivided) Subdivide(false);
            distance = Mathf.Max(0f, distance);
            return RemovePenetrationCells(CollectPenetrationCells(start, rotation, halfExtents, distance));
        }

        private bool RemovePenetrationCells(List<int> touched)
        {
            bool changed = touched.Count > 0;
            foreach (int index in touched)
            {
                if (gravityImpact) Detach(index);
                else RemoveCell(index);
            }
            if (changed) MarkStructureDirty();
            // Swept segments update cells immediately; colliders wait for the shared batch boundary.
            if (collisionDirty) PoolManager.QueueCollisionFinish(this);
            return changed;
        }

        private static Vector3 Absolute(Vector3 value)
            => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        private struct CellIntersectionQuery
        {
            public Matrix4x4 matrix;
            public Vector3 center, a, b, c, cellX, cellY, cellZ;
        }
        private int[] penetrationVisitMarks = System.Array.Empty<int>();
        private int penetrationVisitVersion;

        private bool PrepareCellQuery(Vector3 center, Quaternion rotation, Vector3 halfExtents,
            out CellIntersectionQuery query, out Matrix4x4 inverse, out Vector3Int first, out Vector3Int last)
        {
            var matrix = CellRoot.localToWorldMatrix;
            inverse = CellRoot.worldToLocalMatrix;
            query = new CellIntersectionQuery
            {
                matrix = matrix, center = center,
                a = rotation * Vector3.right * halfExtents.x,
                b = rotation * Vector3.up * halfExtents.y,
                c = rotation * Vector3.forward * halfExtents.z,
                cellX = matrix.MultiplyVector(Vector3.right * 0.5f),
                cellY = matrix.MultiplyVector(Vector3.up * 0.5f),
                cellZ = matrix.MultiplyVector(Vector3.forward * 0.5f)
            };
            Vector3 localCenter = inverse.MultiplyPoint3x4(center);
            Vector3 extent = Absolute(inverse.MultiplyVector(query.a))
                + Absolute(inverse.MultiplyVector(query.b)) + Absolute(inverse.MultiplyVector(query.c));
            Vector3 tolerance = CellQueryTolerance(inverse);
            Vector3 min = localCenter - extent - tolerance - gridOrigin;
            Vector3 max = localCenter + extent + tolerance - gridOrigin;
            first = new Vector3Int(Mathf.Max(0, Mathf.FloorToInt(min.x / gridStep.x)),
                Mathf.Max(0, Mathf.FloorToInt(min.y / gridStep.y)), Mathf.Max(0, Mathf.FloorToInt(min.z / gridStep.z)));
            last = new Vector3Int(Mathf.Min(divisions.x - 1, Mathf.FloorToInt(max.x / gridStep.x)),
                Mathf.Min(divisions.y - 1, Mathf.FloorToInt(max.y / gridStep.y)),
                Mathf.Min(divisions.z - 1, Mathf.FloorToInt(max.z / gridStep.z)));
            return first.x <= last.x && first.y <= last.y && first.z <= last.z;
        }

        private static Vector3 CellQueryTolerance(Matrix4x4 inverse) =>
            Absolute(inverse.MultiplyVector(Vector3.right * 0.00001f))
            + Absolute(inverse.MultiplyVector(Vector3.up * 0.00001f))
            + Absolute(inverse.MultiplyVector(Vector3.forward * 0.00001f));

        private List<int> CollectParticleCells(Vector3 center, Quaternion rotation, Vector3 halfExtents)
        {
            impactCells.Clear();
            if (PrepareCellQuery(center, rotation, halfExtents, out var query, out _, out var first, out var last))
                CollectCellBox(first, last, query, 0);
            return impactCells;
        }

        private void CollectCellBox(Vector3Int first, Vector3Int last, in CellIntersectionQuery query, int stamp)
        {
            for (int x = first.x; x <= last.x; x++)
                for (int y = first.y; y <= last.y; y++)
                    for (int z = first.z; z <= last.z; z++)
                    {
                        int index = x * divisions.y * divisions.z + y * divisions.z + z;
                        if (stamp != 0)
                        {
                            if (penetrationVisitMarks[index] == stamp) continue;
                            penetrationVisitMarks[index] = stamp;
                        }
                        if (grid[index] && IntersectsParticle(preparedCells[index], query)) impactCells.Add(index);
                    }
        }

        private List<int> CollectPenetrationCells(Vector3 start, Quaternion rotation, Vector3 halfExtents, float distance)
        {
            impactCells.Clear();
            Vector3 direction = rotation * Vector3.forward;
            Vector3 sweptExtents = halfExtents;
            sweptExtents.z += distance * 0.5f;
            if (!PrepareCellQuery(start + direction * (distance * 0.5f), rotation, sweptExtents,
                out var query, out var inverse, out var first, out var last)) return impactCells;
            // Traverse the center line with a conservative footprint, then retain the exact OBB test.
            Vector3 padding = Absolute(inverse.MultiplyVector(query.a)) + Absolute(inverse.MultiplyVector(query.b))
                + Absolute(inverse.MultiplyVector(direction * halfExtents.z)) + CellQueryTolerance(inverse);
            var radius = new Vector3Int(Mathf.CeilToInt(padding.x / gridStep.x),
                Mathf.CeilToInt(padding.y / gridStep.y), Mathf.CeilToInt(padding.z / gridStep.z));
            Vector3 from = ToGrid(inverse.MultiplyPoint3x4(start));
            Vector3 to = ToGrid(inverse.MultiplyPoint3x4(start + direction * distance));
            if (!ClipGridSegment(ref from, ref to, radius)) return impactCells;
            var cell = Vector3Int.FloorToInt(from);
            var end = Vector3Int.FloorToInt(to);
            int steps = Mathf.Abs(end.x - cell.x) + Mathf.Abs(end.y - cell.y) + Mathf.Abs(end.z - cell.z) + 1;
            long boxCount = (long)(last.x - first.x + 1) * (last.y - first.y + 1) * (last.z - first.z + 1);
            double walkCount = (double)steps * (2d * radius.x + 1d) * (2d * radius.y + 1d) * (2d * radius.z + 1d);
            if (boxCount <= walkCount)
            {
                // Short, thick, and axis-aligned sweeps are cheaper as a small box scan.
                CollectCellBox(first, last, query, 0);
                return impactCells;
            }
            if (penetrationVisitMarks.Length != preparedCells.Length)
                penetrationVisitMarks = new int[preparedCells.Length];
            if (penetrationVisitVersion == int.MaxValue)
            {
                System.Array.Clear(penetrationVisitMarks, 0, penetrationVisitMarks.Length);
                penetrationVisitVersion = 0;
            }
            int stamp = ++penetrationVisitVersion;
            Vector3 delta = to - from;
            var step = new Vector3Int(delta.x > 0f ? 1 : delta.x < 0f ? -1 : 0,
                delta.y > 0f ? 1 : delta.y < 0f ? -1 : 0, delta.z > 0f ? 1 : delta.z < 0f ? -1 : 0);
            Vector3 next = new Vector3(NextGridBoundary(from.x, delta.x, cell.x, step.x),
                NextGridBoundary(from.y, delta.y, cell.y, step.y), NextGridBoundary(from.z, delta.z, cell.z, step.z));
            Vector3 interval = new Vector3(step.x == 0 ? float.PositiveInfinity : Mathf.Abs(1f / delta.x),
                step.y == 0 ? float.PositiveInfinity : Mathf.Abs(1f / delta.y),
                step.z == 0 ? float.PositiveInfinity : Mathf.Abs(1f / delta.z));
            for (int iteration = 0; iteration < steps; iteration++)
            {
                var low = Vector3Int.Max(first, cell - radius);
                var high = Vector3Int.Min(last, cell + radius);
                CollectCellBox(low, high, query, stamp);
                if (cell == end) break;
                float arrival = Mathf.Min(next.x, Mathf.Min(next.y, next.z));
                // Advance one axis on ties; this includes the edge-adjacent grid cells too.
                if (next.x <= arrival) { cell.x += step.x; next.x += interval.x; }
                else if (next.y <= arrival) { cell.y += step.y; next.y += interval.y; }
                else { cell.z += step.z; next.z += interval.z; }
            }
            return impactCells;
        }

        private Vector3 ToGrid(Vector3 local) => new Vector3((local.x - gridOrigin.x) / gridStep.x,
            (local.y - gridOrigin.y) / gridStep.y, (local.z - gridOrigin.z) / gridStep.z);

        private static float NextGridBoundary(float origin, float delta, int cell, int step) =>
            step == 0 ? float.PositiveInfinity : (cell + (step > 0 ? 1f : 0f) - origin) / delta;

        private bool ClipGridSegment(ref Vector3 from, ref Vector3 to, Vector3Int radius)
        {
            Vector3 delta = to - from;
            float enter = 0f, exit = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float low = -radius[axis], high = divisions[axis] + radius[axis];
                if (Mathf.Abs(delta[axis]) < 1e-12f)
                {
                    if (from[axis] < low || from[axis] > high) return false;
                    continue;
                }
                float a = (low - from[axis]) / delta[axis], b = (high - from[axis]) / delta[axis];
                if (a > b) { float swap = a; a = b; b = swap; }
                enter = Mathf.Max(enter, a); exit = Mathf.Min(exit, b);
                if (enter > exit) return false;
            }
            to = from + delta * exit;
            from += delta * enter;
            return true;
        }

        private static bool Separated(Vector3 axis, Vector3 offset, Vector3 a, Vector3 b, Vector3 c,
            Vector3 x, Vector3 y, Vector3 z)
        {
            float extent = Mathf.Abs(Vector3.Dot(axis, a)) + Mathf.Abs(Vector3.Dot(axis, b))
                + Mathf.Abs(Vector3.Dot(axis, c)) + Mathf.Abs(Vector3.Dot(axis, x))
                + Mathf.Abs(Vector3.Dot(axis, y)) + Mathf.Abs(Vector3.Dot(axis, z));
            return Mathf.Abs(Vector3.Dot(axis, offset)) > extent + axis.magnitude * 0.00001f;
        }

        private static bool IntersectsParticle(Cell cell, in CellIntersectionQuery query)
        {
            Vector3 a = query.a, b = query.b, c = query.c;
            Vector3 x = query.cellX * cell.localScale.x;
            Vector3 y = query.cellY * cell.localScale.y;
            Vector3 z = query.cellZ * cell.localScale.z;
            Vector3 offset = query.matrix.MultiplyPoint3x4(cell.localPosition) - query.center;
            return !Separated(Vector3.Cross(a, b), offset, a, b, c, x, y, z)
                && !Separated(Vector3.Cross(b, c), offset, a, b, c, x, y, z)
                && !Separated(Vector3.Cross(c, a), offset, a, b, c, x, y, z)
                && !Separated(Vector3.Cross(x, y), offset, a, b, c, x, y, z)
                && !Separated(Vector3.Cross(y, z), offset, a, b, c, x, y, z)
                && !Separated(Vector3.Cross(z, x), offset, a, b, c, x, y, z)
                && !Separated(Vector3.Cross(a, x), offset, a, b, c, x, y, z)
                && !Separated(Vector3.Cross(a, y), offset, a, b, c, x, y, z)
                && !Separated(Vector3.Cross(a, z), offset, a, b, c, x, y, z)
                && !Separated(Vector3.Cross(b, x), offset, a, b, c, x, y, z)
                && !Separated(Vector3.Cross(b, y), offset, a, b, c, x, y, z)
                && !Separated(Vector3.Cross(b, z), offset, a, b, c, x, y, z)
                && !Separated(Vector3.Cross(c, x), offset, a, b, c, x, y, z)
                && !Separated(Vector3.Cross(c, y), offset, a, b, c, x, y, z)
                && !Separated(Vector3.Cross(c, z), offset, a, b, c, x, y, z);
        }

        // Legacy movement compatibility; active projectiles use the shared sphere payload below.
        public void Explode(float power, Vector3 point, Vector3 normal,
            Quaternion shotRotation, Vector3 particleHalfExtents)
            => ExplodeSphere(power, point, 0.5f * Mathf.Pow(Mathf.Max(0f, power), 1f / 3f));

        public void ExplodeSphere(float power, Vector3 center, float radius)
        {
            if (DamageProtected) return;
            float resistance = Mathf.Max(0f, explosionResistance);
            if (!isActiveAndEnabled || power <= 0f || radius <= 0f) return;
            var candidates = new List<ExpandingExplosion.Piece>();
            CollectExplosionPiecesCore(center, radius, Mathf.Max(.01f, partSize), candidates);
            // Retain legacy shell ordering, but collect virtual cells without splitting an intact target.
            var shells = new SortedDictionary<int, List<Vector3Int>>();
            float step = Mathf.Max(.01f, partSize);
            foreach (var piece in candidates)
            {
                int shell = Mathf.Max(0, Mathf.CeilToInt(piece.distance / step - .00001f) - 1);
                if (!shells.TryGetValue(shell, out var layer)) shells[shell] = layer = new List<Vector3Int>();
                layer.Add(piece.coordinate);
            }
            bool changed = false;
            float remainingPower = power;
            foreach (var shell in shells)
            {
                if (remainingPower <= 0f) break;
                foreach (var coordinate in shell.Value)
                    changed |= ApplyExplosionDamage(coordinate, remainingPower, center, remainingPower) == ExplosionDamageResult.Destroyed;
                if (remainingPower < resistance) break;
                remainingPower = Mathf.Max(0f, remainingPower - resistance);
            }
            if (changed) { MarkStructureDirty(); PoolManager.QueueCollisionFinish(this); }
        }

        // 회전된 박스 안에 중심이 든 칸을 저항과 무관하게 모두 파괴(탱크 압착용). 파괴한 칸 수를 반환.
        private readonly List<int> boxBreakCells = new List<int>();
        public int BreakInBox(Vector3 center, Quaternion rotation, Vector3 halfExtents, float debrisSpeed)
        {
            if (DamageProtected) return 0;
            if (!isActiveAndEnabled) return 0;
            if (!subdivided) Subdivide();
            Quaternion inverse = Quaternion.Inverse(rotation);
            boxBreakCells.Clear();
            foreach (int cellIndex in cells)
            {
                Cell cell = preparedCells[cellIndex];
                Vector3 local = inverse * (cell.position - center);
                if (Mathf.Abs(local.x) <= halfExtents.x && Mathf.Abs(local.y) <= halfExtents.y &&
                    Mathf.Abs(local.z) <= halfExtents.z) boxBreakCells.Add(cellIndex);
            }
            foreach (int cellIndex in boxBreakCells)
            {
                Cell cell = preparedCells[cellIndex];
                Detach(cellIndex, (cell.position - center).normalized * debrisSpeed + Vector3.up);
            }
            int count = boxBreakCells.Count;
            if (count > 0) { MarkStructureDirty(); UpdateCollisionRuns(); }
            return count;
        }

        public float ExplosionResistance => Mathf.Max(0f, explosionResistance);

        public void ApplyFallImpact(float power, Vector3 contactPoint, float distanceStep)
        {
            if (DamageProtected) return;
            float resistance = ExplosionResistance;
            if (!isActiveAndEnabled || power <= 0f || power <= resistance + 0.00001f) return;
            if (!subdivided) Subdivide();
            float bottom = float.PositiveInfinity;
            foreach (int cellIndex in cells)
            {
                var cell = preparedCells[cellIndex];
                float halfHeight = Mathf.Abs(cell.TransformVector(Vector3.right * 0.5f).y)
                    + Mathf.Abs(cell.TransformVector(Vector3.up * 0.5f).y)
                    + Mathf.Abs(cell.TransformVector(Vector3.forward * 0.5f).y);
                bottom = Mathf.Min(bottom, cell.position.y - halfHeight);
            }
            var layers = new SortedDictionary<int, List<int>>();
            float step = Mathf.Max(0.01f, distanceStep);
            foreach (int cellIndex in cells)
            {
                var cell = preparedCells[cellIndex];
                float halfHeight = Mathf.Abs(cell.TransformVector(Vector3.right * 0.5f).y)
                    + Mathf.Abs(cell.TransformVector(Vector3.up * 0.5f).y)
                    + Mathf.Abs(cell.TransformVector(Vector3.forward * 0.5f).y);
                int layer = Mathf.Max(0, Mathf.FloorToInt((cell.position.y - halfHeight - bottom) / step + 0.00001f));
                if (!layers.TryGetValue(layer, out var list)) layers[layer] = list = new List<int>();
                list.Add(cellIndex);
            }
            bool changed = false;
            foreach (var layer in layers)
            {
                if (power <= 0f || power + 0.00001f < resistance) break;
                foreach (int cellIndex in layer.Value)
                {
                    var cell = preparedCells[cellIndex];
                    Vector3 offset = cell.position - contactPoint;
                    Vector3 outward = new Vector3(offset.x, 0f, offset.z).normalized;
                    Detach(cellIndex, outward * Mathf.Min(power, 3f) + Vector3.up * Mathf.Min(power * 0.15f, 1f));
                }
                power = Mathf.Max(0f, power - resistance);
                changed = true;
            }
            if (changed) MarkStructureDirty();
            UpdateCollisionRuns();
        }

        public void CollectExplosionPieces(Vector3 center, float radius, float step, List<ExpandingExplosion.Piece> output)
        {
            CollectExplosionPiecesCore(center, radius, step, output);
        }

        internal bool TryCollectExplosionPieces(Vector3 center, float radius, float step, List<ExpandingExplosion.Piece> output)
        {
            if (DamageProtected) return true;
            if (!isActiveAndEnabled || targetMesh == null) return true;
            if (!subdivided && originalRenderer != null &&
                originalRenderer.bounds.SqrDistance(center) > radius * radius) return true;

            CollectExplosionPiecesCore(center, radius, step, output);
            return true;
        }

        // Collection reads geometry metadata only; topology is created at the first actual removal.
        internal bool CanCollectExplosionPiecesNow(Vector3 center, float radius) => true;

        private void CollectExplosionPiecesCore(Vector3 center, float radius, float step, List<ExpandingExplosion.Piece> output)
        {
            if (DamageProtected) return;
            if (!isActiveAndEnabled || targetMesh == null) return;
            if (!subdivided && originalRenderer != null &&
                originalRenderer.bounds.SqrDistance(center) > radius * radius) return;
            PrepareIntactGrid();
            Transform root = CellRoot;
            Vector3 localCenter = root.InverseTransformPoint(center);
            Vector3 localRadius = Absolute(root.InverseTransformVector(Vector3.right * radius))
                + Absolute(root.InverseTransformVector(Vector3.up * radius))
                + Absolute(root.InverseTransformVector(Vector3.forward * radius));
            Vector3 minBound = localCenter - localRadius - gridOrigin;
            Vector3 maxBound = localCenter + localRadius - gridOrigin;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minBound.x / gridStep.x) - 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minBound.y / gridStep.y) - 1);
            int z0 = Mathf.Max(0, Mathf.FloorToInt(minBound.z / gridStep.z) - 1);
            int x1 = Mathf.Min(divisions.x - 1, Mathf.FloorToInt(maxBound.x / gridStep.x) + 1);
            int y1 = Mathf.Min(divisions.y - 1, Mathf.FloorToInt(maxBound.y / gridStep.y) + 1);
            int z1 = Mathf.Min(divisions.z - 1, Mathf.FloorToInt(maxBound.z / gridStep.z) + 1);
            impactCells.Clear();
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                    {
                        var coordinate = new Vector3Int(x, y, z);
                        if (TryGetExplosionCell(coordinate, out _)) impactCells.Add(CellIndex(coordinate));
                    }
            // Preserve the pre-existing cell order for equally timed debris emission.
            if (subdivided)
            {
                if (cellSlotComparison == null) cellSlotComparison = CompareCellSlots;
                impactCells.Sort(cellSlotComparison);
            }
            foreach (int cellIndex in impactCells)
            {
                var coordinate = new Vector3Int(cellIndex / (divisions.y * divisions.z), (cellIndex / divisions.z) % divisions.y, cellIndex % divisions.z);
                TryGetExplosionCell(coordinate, out var cell);
                Vector3 min = cell.localPosition - cell.localScale * 0.5f;
                Vector3 max = cell.localPosition + cell.localScale * 0.5f;
                Vector3 nearest = new Vector3(Mathf.Clamp(localCenter.x, min.x, max.x),
                    Mathf.Clamp(localCenter.y, min.y, max.y), Mathf.Clamp(localCenter.z, min.z, max.z));
                float distance = Vector3.Distance(root.TransformPoint(nearest), center);
                if (distance > radius) continue;
                output.Add(new ExpandingExplosion.Piece
                {
                    target = this, coordinate = coordinate, position = cell.position, distance = distance,
                    direction = ExpandingExplosion.DirectionBin(cell.position - center),
                    shell = Mathf.Max(0, Mathf.FloorToInt(distance / step))
                });
            }
        }
        private int CompareCellSlots(int a, int b) => preparedCells[a].slot.CompareTo(preparedCells[b].slot);

        public bool DestroyExplosionPiece(Vector3Int coordinate, Vector3 center, float force)
        {
            if (DamageProtected) return false;
            if (!isActiveAndEnabled) return false;
            if (!subdivided) Subdivide(false);
            if (!TryGetCell(coordinate, out var cell)) return false;
            Vector3 direction = (cell.position - center).normalized;
            Detach(CellIndex(coordinate), direction * force + Vector3.up * (force * 0.05f));
            MarkStructureDirty();
            return true;
        }
        public static void FinishExplosionStep(IEnumerable<DestructibleTarget> targets)
        {
            using (explosionFinishMarker.Auto()) FinishExplosionStepCore(targets);
        }
        private static void FinishExplosionStepCore(IEnumerable<DestructibleTarget> targets)
        {
            bool changed = false;
            Scene scene = default;
            bool hasScene = false;
            foreach (var target in targets)
                if (target != null)
                {
                    changed |= target.UpdateCollisionRunsCore();
                    if (!hasScene) { scene = target.gameObject.scene; hasScene = true; }
                }
            if (!changed) return;
            // Coalesce with the per-event syncs of the same frame in opt-in scenes; otherwise sync now.
            bool synced = true;
            if (hasScene) synced = DestructionCollisionBatch.RequestSync(scene);
            else Physics.SyncTransforms();
            foreach (var target in targets)
                if (target != null) StructuralSupportGraph.Changed(target);
            // A deferred sync has not happened yet, so the support graph must still sync before its queries.
            if (synced) StructuralSupportGraph.NotifyPhysicsSynchronized();
        }

        // 월드 좌표를 메시 범위로 당겨 해당 칸이 아직 붙어 있는지 확인. 분할 전에는 온전한 상태로 본다.
        public bool HasCellAt(Vector3 worldPoint)
        {
            if (!subdivided) return true;
            var bounds = targetMesh.sharedMesh.bounds;
            var local = CellRoot.InverseTransformPoint(worldPoint);
            local = Vector3.Max(bounds.min, Vector3.Min(bounds.max, local));
            var coordinate = new Vector3Int();
            for (int axis = 0; axis < 3; axis++)
                coordinate[axis] = Mathf.Clamp(Mathf.FloorToInt((local[axis] - gridOrigin[axis]) / gridStep[axis]), 0, divisions[axis] - 1);
            return HasCell(coordinate);
        }

        public bool IsDestroyed => subdivided && cells.Count == 0 && (debrisCloud == null || debrisCloud.IntactGroupCount == 0);
        public int InitialCellCount => totalCellCount;
        public int RemainingCellCount => !subdivided ? totalCellCount
            : cells.Count + (debrisCloud != null ? debrisCloud.IntactCellCount : 0);
        // Cells still attached to this target; detached debris chunks are not counted.
        public int AttachedCellCount => !subdivided ? totalCellCount : cells.Count;

        public void AbsorbSphere(Vector3 center, float radius)
        {
            if (DamageProtected) return;
            if (!isActiveAndEnabled || targetMesh == null || radius <= 0f) return;
            if (!subdivided) Subdivide(false);
            BeginGravityImpact(center);
            var removed = absorbedCells;
            removed.Clear();
            Vector3 localCenter = CellRoot.InverseTransformPoint(center);
            float radiusSquared = radius * radius;
            foreach (int cellIndex in cells)
            {
                Cell cell = preparedCells[cellIndex];
                Vector3 min = cell.localPosition - cell.localScale * 0.5f;
                Vector3 max = cell.localPosition + cell.localScale * 0.5f;
                Vector3 nearest = new Vector3(Mathf.Clamp(localCenter.x, min.x, max.x),
                    Mathf.Clamp(localCenter.y, min.y, max.y), Mathf.Clamp(localCenter.z, min.z, max.z));
                if ((CellRoot.TransformPoint(nearest) - center).sqrMagnitude <= radiusSquared) removed.Add(cellIndex);
            }
            foreach (int cellIndex in removed) Detach(cellIndex);
            // Sphere deletion is immediate; detached unsupported chunks use the same suction animation.
            if (removed.Count > 0) MarkStructureDirty();
            EndGravityImpact();
            UpdateCollisionRuns();
        }

        // Called after an external hit-condition component approves destruction.
        public void BreakAll(Vector3 point, Vector3 direction)
        {
            if (DamageProtected) return;
            if (!isActiveAndEnabled || targetMesh == null) return;
            if (!subdivided) Subdivide(false);
            if (cells.Count == 0) return;
            WorkshopSoundEffects.RaiseBlockFragmented(transform.position);
            breakAllCells.Clear();
            breakAllCells.AddRange(cells);
            foreach (int cellIndex in breakAllCells)
            {
                Vector3 outward = (preparedCells[cellIndex].position - point).normalized;
                Detach(cellIndex, outward * 1.5f + direction.normalized * 0.5f + Vector3.up * 0.5f);
            }
            structureDirty = false;
            UpdateCollisionRuns();
        }

        public void ClearRemaining()
        {
            // Keep the reusable cells for Restart, but remove their visuals and collisions now.
            if (!subdivided) Subdivide();
            cells.Clear();
            System.Array.Clear(grid, 0, grid.Length);
            MarkDebrisIndexDirty();
            InvalidateAllCollisionColumns();
            UpdateCollisionRuns();
            debrisCloud?.ClearDebris();
            structureDirty = false;
            renderDirty = true;
            InvalidateRenderChunks();
        }

        public TargetWorldSave CaptureSave()
        {
            return new TargetWorldSave
            {
                exists = true,
                subdivided = subdivided,
                meshSize = targetMesh.transform.localScale,
                penetration = penetrationResistance,
                penetrationResistanceLoss = penetrationResistanceLoss,
                explosion = explosionResistance,
                heat = heatResistance,
                water = waterResistance,
                position = transform.localPosition,
                rotation = transform.localRotation,
                scale = transform.localScale,
                remaining = GetRemainingCoordinates(),
                explosionDamage = CaptureExplosionDamage()
            };
        }
        public void RestoreSave(TargetWorldSave state)
        {
            if (state == null || !state.exists || !enabled) return;
            if (state.subdivided)
            {
                if (!subdivided) Subdivide();
                ResetTarget();
                var keep = new HashSet<Vector3Int>(state.remaining ?? System.Array.Empty<Vector3Int>());
                restoringSave = true;
                impactCells.Clear(); impactCells.AddRange(cells);
                foreach (int cellIndex in impactCells)
                    if (!keep.Contains(preparedCells[cellIndex].coordinate)) RemoveCell(cellIndex);
                restoringSave = false;
                structureDirty = false; renderDirty = true;
            }
            penetrationResistanceLoss = float.IsNaN(state.penetrationResistanceLoss) || float.IsInfinity(state.penetrationResistanceLoss)
                ? 0f : Mathf.Clamp(state.penetrationResistanceLoss, 0f, Mathf.Max(0f, penetrationResistance));
            transform.localPosition = state.position; transform.localRotation = state.rotation; transform.localScale = state.scale;
            if (!state.subdivided) { intactDamage.Clear(); ResetDamageAppearance(); }
            RestoreExplosionDamage(state.explosionDamage);
            MarkDebrisIndexDirty();
            StructuralSupportGraph.Changed(this);
        }

        public void ResetTarget()
        {
            ResetPenetrationFeedback();
            intactDamage.Clear();
            ResetDamageAppearance();
            penetrationResistanceLoss = 0f;
            if (!subdivided) return;
            debrisCloud?.ClearDebris();
            cells.Clear();
            System.Array.Clear(grid, 0, grid.Length);
            transform.localPosition = initialPosition;
            transform.localRotation = initialRotation;
            transform.localScale = initialScale;
            Bounds bounds = targetMesh.sharedMesh.bounds;
            for (int index = 0; index < totalCellCount; index++)
            {
                Cell cell = preparedCells[index];
                Vector3Int coordinate = cell.coordinate;
                Vector3 offset = new Vector3(coordinate.x * gridStep.x, coordinate.y * gridStep.y, coordinate.z * gridStep.z);
                cell.localScale = Vector3.Min(gridStep, bounds.size - offset);
                cell.localPosition = gridOrigin + offset + cell.localScale * 0.5f;
                cell.slot = cells.Count;
                cell.explosionHealth = 1d;
                cell.explosionHitTime = -1000f;
                preparedCells[index] = cell;
                cells.Add(index);
                grid[index] = true;
            }
            MarkDebrisIndexDirty();
            InvalidateRenderChunks();
            changedBoundary.Clear();
            if (supportValidated)
                foreach (int cellIndex in cells) changedBoundary.Add(preparedCells[cellIndex].coordinate);
            structureDirty = false; externallyHeld = false;
            InvalidateAllCollisionColumns();
            UpdateCollisionRuns();

        }

        private Vector3Int[] GetRemainingCoordinates()
        {
            var remaining = new Vector3Int[cells.Count];
            for (int i = 0; i < cells.Count; i++) remaining[i] = preparedCells[cells[i]].coordinate;
            return remaining;
        }

        private void Detach(int cellIndex, Vector3 velocity = default)
        {
            Cell cell = preparedCells[cellIndex];
            // 칸 제거(실제 파괴)는 예산과 무관하게 항상 하고, 시각 파편만 전역 예산으로 걸러 낸다.
            bool emitDebris = DebrisEmissionBudget.TryEmitPiece(cell.position,
                gravityImpact ? gravityAbsorptionDuration : debrisLifetime);
            if (emitDebris) EnsureDebrisCloud();
            RemoveCell(cellIndex);
            if (emitDebris) debrisCloud.Emit(cell.Snapshot, velocity, debrisLifetime, debrisFadeDuration);
        }

        private void RemoveCell(int cellIndex)
        {
            Cell cell = preparedCells[cellIndex];
            string cellId = null;
            Vector3Int coordinate = cell.coordinate;
            if (!restoringSave && CellDestroyed != null) TryGetCellId(coordinate, out cellId);
            RecordSupportRemoval(coordinate);
            MarkRenderChunk(coordinate);
            grid[cellIndex] = false;
            MarkCollisionColumn(coordinate.x * divisions.y + coordinate.y);
            int slot = cell.slot;
            int lastIndex = cells[cells.Count - 1];
            cells[slot] = lastIndex;
            Cell last = preparedCells[lastIndex];
            last.slot = slot;
            preparedCells[lastIndex] = last;
            cells.RemoveAt(cells.Count - 1);
            cell.slot = -1;
            preparedCells[cellIndex] = cell;
            renderDirty = true;
            if (cells.Count == 0) MarkDebrisIndexDirty();
            if (cellId != null) CellDestroyed?.Invoke(this, cellId);
            if (!restoringSave) PartDestroyed?.Invoke(this);
        }

        private void DropUnsupported()
        {
            supportValidated = false;
            CollectExternalSupport();
            // Newly created/moved cell bounds must be current before checking floor support.
            var supported = supportedCells; supported.Clear();
            var pending = supportQueue; pending.Clear();
            // Ground reach first; anything added by neighbor support afterwards is held externally.
            for (int i = 0; i < cells.Count; i++)
            {
                int cellIndex = cells[i];
                Cell cell = preparedCells[cellIndex];
                Vector3Int coordinate = cell.coordinate;
                if (CellBottom(cell) <= transform.position.y + 0.001f && supported.Add(coordinate)) pending.Enqueue(coordinate);
            }
            FloodSupport();
            int groundReach = supported.Count;
            for (int i = 0; i < cells.Count; i++)
            {
                int cellIndex = cells[i];
                Cell cell = preparedCells[cellIndex];
                Vector3Int coordinate = cell.coordinate;
                if (HasExternalSupport(cellIndex) && supported.Add(coordinate)) pending.Enqueue(coordinate);
            }
            FloodSupport();
            externallyHeld = supported.Count > groundReach;
            var falling = fallingCoordinates; falling.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                Vector3Int coordinate = preparedCells[cells[i]].coordinate;
                if (!supported.Contains(coordinate)) falling.Add(coordinate);
            }
            while (falling.Count > 0)
            {
                Vector3Int seed = default;
                foreach (Vector3Int coordinate in falling) { seed = coordinate; break; }
                var group = fallingCells; group.Clear();
                pending.Enqueue(seed);
                falling.Remove(seed);
                while (pending.Count > 0)
                {
                    Vector3Int coordinate = pending.Dequeue();
                    if (TryGetCell(coordinate, out _)) group.Add(CellIndex(coordinate));
                    foreach (Vector3Int direction in neighbors)
                        if (falling.Remove(coordinate + direction)) pending.Enqueue(coordinate + direction);
                }
                groupPieces.Clear();
                // 덩어리는 예산으로 줄이지 않는다. 낙하 중인 덩어리 칸은 RemainingCellCount·IsDestroyed(상자·코어 판정)에 들어간다.
                foreach (int cellIndex in group) groupPieces.Add(preparedCells[cellIndex].Snapshot);
                EnsureDebrisCloud();
                debrisCloud.EmitGroup(groupPieces, debrisLifetime, debrisFadeDuration);
                DebrisEmissionBudget.NoteGroup(groupPieces.Count, debrisLifetime);
                foreach (int cellIndex in group) RemoveCell(cellIndex);
            }
        }

        private void DropUnsupportedNearRemovedBoundary()
        {
            CollectExternalSupport();
            changedBoundary.RemoveWhere(coordinate => !HasCell(coordinate));
            localSupportVisited.Clear();
            localSupportQueue.Clear();
            externallyHeld = false;
            foreach (int supportedIndex in externallySupportedCells)
                if (supportedIndex >= 0 && supportedIndex < grid.Length && grid[supportedIndex])
                { externallyHeld = true; break; }

            var group = fallingCells; group.Clear();
            foreach (Vector3Int seed in changedBoundary)
            {
                if (!TryGetCell(seed, out _) || !localSupportVisited.Add(seed)) continue;
                localSupportQueue.Enqueue(seed);
                group.Clear();
                bool supported = false;
                while (localSupportQueue.Count > 0)
                {
                    Vector3Int coordinate = localSupportQueue.Dequeue();
                    if (!TryGetCell(coordinate, out Cell cell)) continue;
                    int cellIndex = CellIndex(coordinate);
                    group.Add(cellIndex);
                    if (CellBottom(cell) <= transform.position.y + 0.001f || HasExternalSupport(cellIndex)) supported = true;
                    foreach (Vector3Int direction in neighbors)
                    {
                        Vector3Int next = coordinate + direction;
                        if (TryGetCell(next, out _) && localSupportVisited.Add(next)) localSupportQueue.Enqueue(next);
                    }
                }
                if (supported || group.Count == 0) continue;
                groupPieces.Clear();
                // 덩어리는 예산으로 줄이지 않는다. 낙하 중인 덩어리 칸은 RemainingCellCount·IsDestroyed(상자·코어 판정)에 들어간다.
                foreach (int cellIndex in group) groupPieces.Add(preparedCells[cellIndex].Snapshot);
                EnsureDebrisCloud();
                debrisCloud.EmitGroup(groupPieces, debrisLifetime, debrisFadeDuration);
                DebrisEmissionBudget.NoteGroup(groupPieces.Count, debrisLifetime);
                resolvingSupportDrop = true;
                try
                {
                    foreach (int cellIndex in group) RemoveCell(cellIndex);
                }
                finally { resolvingSupportDrop = false; }
            }
            if (cells.Count == 0) externallyHeld = false;
        }

        private void FloodSupport()
        {
            while (supportQueue.Count > 0)
            {
                Vector3Int coordinate = supportQueue.Dequeue();
                foreach (Vector3Int direction in neighbors)
                {
                    Vector3Int next = coordinate + direction;
                    if (HasCell(next) && supportedCells.Add(next)) supportQueue.Enqueue(next);
                }
            }
        }

        private void Subdivide(bool createCollisions = true)
        {
            if (subdivided) return;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            try { using (subdivideMarker.Auto()) SubdivideCore(createCollisions); }
            finally { RecordSubdivisionTime(System.Diagnostics.Stopwatch.GetTimestamp() - start); }
        }
        private void SubdivideCore(bool createCollisions)
        {
            subdivided = true;
            Material material = originalRenderer.sharedMaterial;
            instancedMaterial = new Material(material) { enableInstancing = true };
            // Retain the original broad-phase shape until the deferred collision rebuild.
            originalRenderer.enabled = false;
            Bounds bounds = targetMesh.sharedMesh.bounds;
            Vector3 axisScale = new Vector3(CellRoot.TransformVector(Vector3.right).magnitude,
                CellRoot.TransformVector(Vector3.up).magnitude, CellRoot.TransformVector(Vector3.forward).magnitude);
            float size = Mathf.Max(0.01f, partSize);
            Vector3 step = new Vector3(size / Mathf.Max(axisScale.x, 0.00001f),
                size / Mathf.Max(axisScale.y, 0.00001f), size / Mathf.Max(axisScale.z, 0.00001f));
            gridOrigin = bounds.min;
            gridStep = step;
            divisions = new Vector3Int(Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / step.x - 0.00001f)),
                Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / step.y - 0.00001f)),
                Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / step.z - 0.00001f)));
            totalCellCount = divisions.x * divisions.y * divisions.z;
            cells.Clear();
            cells.Capacity = Mathf.Max(cells.Capacity, totalCellCount);
            impactCells.Clear();
            impactCells.Capacity = Mathf.Max(impactCells.Capacity, Mathf.Min(totalCellCount, 256));
            fallingCells.Clear();
            fallingCells.Capacity = Mathf.Max(fallingCells.Capacity, Mathf.Min(totalCellCount, 256));
            boxBreakCells.Capacity = Mathf.Max(boxBreakCells.Capacity, Mathf.Min(totalCellCount, 256));
            dirtyColumns.Capacity = Mathf.Max(dirtyColumns.Capacity, divisions.x * divisions.y);
            preparedCells = new Cell[totalCellCount];
            grid = new bool[totalCellCount];
            if (cellMesh == null)
            {
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                primitive.SetActive(false);
                cellMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
                Destroy(primitive);
            }
            for (int x = 0; x < divisions.x; x++)
                for (int y = 0; y < divisions.y; y++)
                    for (int z = 0; z < divisions.z; z++)
                    {
                        Vector3 offset = new Vector3(x * step.x, y * step.y, z * step.z);
                        Vector3 cellSize = Vector3.Min(step, bounds.size - offset);
                        var coordinate = new Vector3Int(x, y, z);
                        int index = CellIndex(coordinate);
                        Vector3 localPosition = bounds.min + offset + cellSize * 0.5f;
                        Cell cell = new Cell
                        {
                            root = CellRoot, slot = cells.Count, coordinate = coordinate,
                            explosionHealth = 1d, explosionHitTime = -1000f,
                            localPosition = localPosition, localScale = cellSize
                        };
                        if (intactDamage.TryGetValue(index, out var damage))
                        { cell.explosionHealth = damage.explosionHealth; cell.explosionHitTime = damage.explosionHitTime; }
                        preparedCells[index] = cell;
                        cells.Add(index);
                        grid[index] = true;
                    }
            intactDamage.Clear();
            renderDirty = true;
            InvalidateAllCollisionColumns();
            if (createCollisions) UpdateCollisionRuns();
            MarkDebrisIndexDirty();
        }


        private void EnsureCollisionColliderPoolSize(int required)
        {
            if (!collisionPoolMergedOriginals)
            {
                collisionPoolMergedOriginals = true;
                foreach (Collider collider in originalColliders.Keys)
                    if (collider is BoxCollider box && !collisionColliderPool.Contains(box)) collisionColliderPool.Add(box);
            }
            while (collisionColliderPool.Count < required)
            {
                BoxCollider box = CellRoot.gameObject.AddComponent<BoxCollider>();
                box.enabled = false;
                // 파괴 구간에 늘어나는 새 풀 콜라이더도 곧장 등록해 탄 조회의 탐색·항목 생성을 막는다.
                if (registersColliders) ColliderTargetIndex.RegisterStructure(box, this);
                collisionColliderPool.Add(box);
            }
        }

        private BoxCollider AcquireCollisionCollider()
        {
            if (collisionColliderCursor >= collisionColliderPool.Count)
                EnsureCollisionColliderPoolSize(collisionColliderCursor + 1);
            return collisionColliderPool[collisionColliderCursor++];
        }

        // Collider identities stay within their X/Y column; a hole only rebuilds that column.
        private void InvalidateAllCollisionColumns()
        {
            collisionDirty = true; allColumnsDirty = true; dirtyColumns.Clear();
            if (dirtyColumnFlags != null) System.Array.Clear(dirtyColumnFlags, 0, dirtyColumnFlags.Length);
        }
        private void MarkCollisionColumn(int column)
        {
            collisionDirty = true;
            if (allColumnsDirty) return;
            if (!dirtyColumnFlags[column])
            { dirtyColumnFlags[column] = true; dirtyColumns.Add(column); }
        }
        private void UpdateCollisionRuns()
        {
            if (!UpdateCollisionRunsCore()) return;
            // Opt-in scenes coalesce the per-event sync into one flush per frame.
            bool synced = DestructionCollisionBatch.RequestSync(gameObject.scene);
            StructuralSupportGraph.Changed(this);
            if (synced) StructuralSupportGraph.NotifyPhysicsSynchronized();
        }
        private bool UpdateCollisionRunsCore()
        {
            using (collisionMarker.Auto()) return RebuildCollisionRuns();
        }
        private bool RebuildCollisionRuns()
        {
            if (!subdivided || !collisionDirty) return false;
            if (collisionColumns == null)
            {
                foreach (Collider collider in originalColliders.Keys) collider.enabled = false;
                collisionColumns = new List<BoxCollider>[divisions.x * divisions.y];
                dirtyColumnFlags = new bool[collisionColumns.Length];
                allColumnsDirty = true;
            }
            if (allColumnsDirty)
                for (int column = 0; column < collisionColumns.Length; column++) UpdateCollisionColumn(column);
            else
                foreach (int column in dirtyColumns) UpdateCollisionColumn(column);
            foreach (int column in dirtyColumns) dirtyColumnFlags[column] = false;
            dirtyColumns.Clear(); allColumnsDirty = false; collisionDirty = false;
            return true;
        }
        private void UpdateCollisionColumn(int column)
        {
            var runs = collisionColumns[column];
            if (runs == null)
            {
                runs = collisionColumnListPool.Count > 0
                    ? collisionColumnListPool.Pop()
                    : new List<BoxCollider>(Mathf.Min(divisions.z, 8));
                runs.Clear();
                int initialCapacity = Mathf.Min(divisions.z, 8);
                if (runs.Capacity < initialCapacity) runs.Capacity = initialCapacity;
                collisionColumns[column] = runs;
            }
            int x = column / divisions.y, y = column % divisions.y, used = 0;
            for (int z = 0; z < divisions.z; z++)
            {
                if (!TryGetCell(new Vector3Int(x, y, z), out Cell first)) continue;
                Cell last = first;
                while (z + 1 < divisions.z && TryGetCell(new Vector3Int(x, y, z + 1), out Cell next))
                { last = next; z++; }
                if (used == runs.Count) runs.Add(AcquireCollisionCollider());
                BoxCollider box = runs[used++];
                Vector3 min = first.localPosition - first.localScale * 0.5f;
                Vector3 max = last.localPosition + last.localScale * 0.5f;
                Vector3 center = (min + max) * 0.5f, size = max - min;
                if (!box.center.Equals(center)) box.center = center;
                if (!box.size.Equals(size)) box.size = size;
                if (!box.enabled) box.enabled = true;
            }
            for (int i = used; i < runs.Count; i++) if (runs[i].enabled) runs[i].enabled = false;
        }

        private void BreakSmallRemnant()
        {
            if (totalCellCount <= 0) return;
            int remaining = RemainingCellCount;
            // Already emitted individual debris is excluded; intact falling chunks still count.
            if (remaining <= 0 || (long)remaining * 10 > totalCellCount) return;
            WorkshopSoundEffects.RaiseBlockFragmented(transform.position);
            while (cells.Count > 0) Detach(cells[cells.Count - 1], Vector3.up * 0.3f);
            debrisCloud?.BreakIntactGroups();
            structureDirty = false;
        }

        private void EnsureDebrisCloud()
        {
            if (debrisCloud != null) return;
            debrisCloud = PoolManager.RentComponent<VisualDebrisCloud>(transform.position);
            debrisCloud.gameObject.layer = gameObject.layer;
            debrisCloud.BindOwner(this);
            debrisCloud.Initialize(cellMesh, originalRenderer.sharedMaterial, debrisFloorBounce, debrisFloorFriction, transform, debrisMaterial);
            debrisCloud.SetAbsorptionEmission(gravityImpact, gravityImpactCenter, gravityAbsorptionDuration, gravitySpreadDistance, gravitySpreadFraction);
            debrisCloud.gameObject.SetActive(true);
        }
        internal void ForgetDebrisCloud(VisualDebrisCloud cloud)
        {
            if (debrisCloud == cloud) debrisCloud = null;
        }
        private void OnDisable()
        {
            ResetPenetrationFeedback();
            debrisIndexed = false;
            debrisIndexDirty = true;
            DestructibleTargetUpdater.Unregister(this);
            DebrisTargetIndex.Remove(this);
            supportValidated = false; changedBoundary.Clear(); externallyHeld = false;
            StructuralSupportGraph.Unregister(this);
            if (debrisCloud != null) PoolManager.Release(debrisCloud.gameObject);
        }

        private void OnDestroy()
        {
            DestructibleTargetUpdater.Unregister(this);
            DebrisTargetIndex.Remove(this);
            DisposeDamageAppearance();
            if (instancedMaterial != null) Destroy(instancedMaterial);
            foreach (var chunk in renderChunks.Values) if (chunk.mesh != null) Destroy(chunk.mesh);
            if (collisionColumns != null)
                foreach (var runs in collisionColumns)
                    if (runs != null)
                    {
                        runs.Clear();
                        if (collisionColumnListPool.Count < 512) collisionColumnListPool.Push(runs);
                    }
        }
    }
}



