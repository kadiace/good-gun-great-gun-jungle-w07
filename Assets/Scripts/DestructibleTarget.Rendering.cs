using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using System.Diagnostics;

namespace WeaponWorkshop
{
    public sealed partial class DestructibleTarget
    {
        private static readonly int PenetrationBaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int PenetrationColorId = Shader.PropertyToID("_Color");
        private static readonly int PenetrationFlashUntilId = Shader.PropertyToID("_PenetrationFlashUntil");
        private MaterialPropertyBlock penetrationFlashProperties, penetrationOriginalProperties;
        private Color penetrationBaseColor, penetrationColor;
        private float penetrationFlashUntil;
        private bool penetrationFlashActive;

        private void ShowPenetrationHit()
        {
            if (originalRenderer == null || originalRenderer.sharedMaterial == null) return;
            penetrationFlashProperties ??= new MaterialPropertyBlock();
            penetrationOriginalProperties ??= new MaterialPropertyBlock();
            if (!penetrationFlashActive)
            {
                originalRenderer.GetPropertyBlock(penetrationOriginalProperties);
                originalRenderer.GetPropertyBlock(penetrationFlashProperties);
                var material = originalRenderer.sharedMaterial;
                penetrationBaseColor = penetrationOriginalProperties.HasColor(PenetrationBaseColorId)
                    ? penetrationOriginalProperties.GetColor(PenetrationBaseColorId)
                    : material.HasProperty(PenetrationBaseColorId) ? material.GetColor(PenetrationBaseColorId) : Color.white;
                penetrationColor = penetrationOriginalProperties.HasColor(PenetrationColorId)
                    ? penetrationOriginalProperties.GetColor(PenetrationColorId)
                    : material.HasProperty(PenetrationColorId) ? material.GetColor(PenetrationColorId) : penetrationBaseColor;
            }
            penetrationFlashActive = true;
            penetrationFlashUntil = Time.time + 0.12f;
            damageProperties?.SetFloat(PenetrationFlashUntilId, penetrationFlashUntil);
            UpdatePenetrationFeedback();
            RefreshUpdaterRegistration();
        }

        private void UpdatePenetrationFeedback()
        {
            if (!penetrationFlashActive) return;
            float flash = Mathf.Clamp01((penetrationFlashUntil - Time.time) / 0.12f);
            if (flash <= 0f)
            {
                ResetPenetrationFeedback();
                RefreshUpdaterRegistration();
                return;
            }
            Color tint = Color.Lerp(penetrationBaseColor, Color.white, flash);
            tint.a = penetrationBaseColor.a;
            penetrationFlashProperties.SetColor(PenetrationBaseColorId, tint);
            tint = Color.Lerp(penetrationColor, Color.white, flash);
            tint.a = penetrationColor.a;
            penetrationFlashProperties.SetColor(PenetrationColorId, tint);
            if (damageProperties != null)
            { damageProperties.SetFloat(PenetrationFlashUntilId, penetrationFlashUntil); originalRenderer.SetPropertyBlock(damageProperties); }
            else originalRenderer.SetPropertyBlock(penetrationFlashProperties);
        }

        private void ResetPenetrationFeedback()
        {
            damageProperties?.SetFloat(PenetrationFlashUntilId, 0f);
            if (penetrationFlashActive && originalRenderer != null)
                originalRenderer.SetPropertyBlock(damageProperties ?? penetrationOriginalProperties);
            penetrationFlashActive = false;
        }

        // Cache bounded meshes per target; a narrow hole only rebuilds nearby chunks.
        private const int RenderChunkWidth = 8;
        private sealed class RenderChunk
        {
            public Mesh mesh;
            public bool dirty = true;
            public CombineInstance[] combines = System.Array.Empty<CombineInstance>();
        }
        private readonly Dictionary<Vector3Int, RenderChunk> renderChunks = new Dictionary<Vector3Int, RenderChunk>();
        private static readonly ProfilerMarker meshMarker = new ProfilerMarker("DestructibleTarget.RebuildChunkMeshes");
        private static readonly ProfilerMarker supportMarker = new ProfilerMarker("DestructibleTarget.ResolveSupport");
        private static readonly ProfilerMarker collisionMarker = new ProfilerMarker("DestructibleTarget.UpdateCollisionRuns");
        private static readonly ProfilerMarker subdivideMarker = new ProfilerMarker("DestructibleTarget.Subdivide");
        private static readonly ProfilerMarker explosionFinishMarker = new ProfilerMarker("DestructibleTarget.FinishExplosionStep");
        private static readonly ProfilerMarker subdivisionBudgetMarker = new ProfilerMarker("DestructibleTarget.SubdivisionBudget");
        public static float SubdivisionBudgetMilliseconds { get; set; } = 4f;
        private static int subdivisionBudgetFrame = -1;
        private static int subdivisionsThisFrame;
        private static long subdivisionElapsedTicks;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubdivisionBudget()
        {
            subdivisionBudgetFrame = -1;
            subdivisionsThisFrame = 0; subdivisionElapsedTicks = 0;
            SubdivisionBudgetMilliseconds = 4f;
        }

        private static bool IsSubdivisionBudgetAvailable()
        {
            using (subdivisionBudgetMarker.Auto())
            {
                ResetSubdivisionFrameIfNeeded();
                return subdivisionsThisFrame == 0 || subdivisionElapsedTicks < SubdivisionBudgetTicks;
            }
        }

        private static long SubdivisionBudgetTicks => (long)(Mathf.Max(0f, SubdivisionBudgetMilliseconds) * Stopwatch.Frequency / 1000f);
        private static void ResetSubdivisionFrameIfNeeded()
        {
            if (subdivisionBudgetFrame == Time.frameCount) return;
            subdivisionBudgetFrame = Time.frameCount; subdivisionsThisFrame = 0; subdivisionElapsedTicks = 0;
        }
        private static void RecordSubdivisionTime(long elapsedTicks)
        {
            using (subdivisionBudgetMarker.Auto())
            {
                ResetSubdivisionFrameIfNeeded(); subdivisionsThisFrame++; subdivisionElapsedTicks += elapsedTicks;
            }
        }
        private static Vector3Int RenderKey(Vector3Int cell) => new Vector3Int(
            cell.x / RenderChunkWidth, cell.y / RenderChunkWidth, cell.z / RenderChunkWidth);

        private void InvalidateRenderChunks()
        {
            foreach (var chunk in renderChunks.Values) chunk.dirty = true;
            renderDirty = true;
        }
        private void MarkRenderChunk(Vector3Int coordinate)
        {
            Vector3Int key = RenderKey(coordinate);
            if (renderChunks.TryGetValue(key, out var chunk)) chunk.dirty = true;
            if (!SurfaceRendering) return;
            // 노출 면 방식은 칸 하나가 사라지면 맞닿던 이웃 칸의 면도 새로 드러난다.
            // 이웃이 같은 청크에 있으면 자기 청크 재구축에 포함되므로 청크 경계만 추가로 검사한다.
            for (int i = 0; i < neighbors.Length; i++)
            {
                Vector3Int neighborKey = RenderKey(coordinate + neighbors[i]);
                if (neighborKey == key) continue;
                if (renderChunks.TryGetValue(neighborKey, out var neighborChunk)) neighborChunk.dirty = true;
            }
        }
        private void DrawRemainingChunks()
        {
            if (renderDirty)
            {
                using (meshMarker.Auto())
                {
                    if (renderChunks.Count == 0)
                        for (int x = 0; x < divisions.x; x += RenderChunkWidth)
                            for (int y = 0; y < divisions.y; y += RenderChunkWidth)
                                for (int z = 0; z < divisions.z; z += RenderChunkWidth)
                                    renderChunks.Add(RenderKey(new Vector3Int(x,y,z)), new RenderChunk());
                    foreach (var pair in renderChunks)
                    {
                        var chunk = pair.Value;
                        if (!chunk.dirty) continue;
                        if (SurfaceRendering) { RebuildSurfaceChunk(pair.Key, chunk); chunk.dirty = false; continue; }
                        var origin = pair.Key * RenderChunkWidth;
                        int count = 0;
                        for (int x = origin.x; x < Mathf.Min(origin.x + RenderChunkWidth, divisions.x); x++)
                            for (int y = origin.y; y < Mathf.Min(origin.y + RenderChunkWidth, divisions.y); y++)
                                for (int z = origin.z; z < Mathf.Min(origin.z + RenderChunkWidth, divisions.z); z++)
                                    if (HasCell(new Vector3Int(x,y,z))) count++;
                        if (chunk.combines.Length != count) chunk.combines = new CombineInstance[count];
                        int used = 0;
                        for (int x = origin.x; x < Mathf.Min(origin.x + RenderChunkWidth, divisions.x); x++)
                            for (int y = origin.y; y < Mathf.Min(origin.y + RenderChunkWidth, divisions.y); y++)
                                for (int z = origin.z; z < Mathf.Min(origin.z + RenderChunkWidth, divisions.z); z++)
                                    if (TryGetCell(new Vector3Int(x,y,z), out var cell))
                                    {
                                        chunk.combines[used++] = new CombineInstance { mesh = cellMesh,
                                            transform = Matrix4x4.TRS(cell.localPosition,Quaternion.identity,cell.localScale) };
                                    }
                        if (chunk.mesh == null && used > 0)
                        {
                            chunk.mesh = new Mesh { name = "RemainingTargetChunk" };
                            chunk.mesh.MarkDynamic();
                        }
                        if (chunk.mesh != null)
                        {
                            chunk.mesh.Clear();
                            if (used > 0) chunk.mesh.CombineMeshes(chunk.combines, true, true, false);
                        }
                        chunk.dirty = false;
                    }
                    renderDirty = false;
                }
            }
            foreach (var chunk in renderChunks.Values)
                if (chunk.mesh != null && chunk.mesh.vertexCount > 0)
                {
                    Graphics.DrawMesh(chunk.mesh, CellRoot.localToWorldMatrix, instancedMaterial, targetMesh.gameObject.layer,
                        null, 0, CurrentDamageProperties, UnityEngine.Rendering.ShadowCastingMode.On, true);
                }
        }

        private Material damageMaterial, undamagedMaterial;
        private static Material damageTemplate;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrepareDamageShader()
        {
            damageTemplate = Resources.Load<Material>("DestructibleDamageLit");
            Resources.Load<ShaderVariantCollection>("DestructibleDamageVariants")?.WarmUp();
        }
        private MaterialPropertyBlock damageProperties;
        private sealed class DamagePage
        {
            public Vector3Int coordinate;
            public int slot;
            public Vector2[] cells;
            public bool dirty;
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct DamagePageEntry
        {
            public int x, y, z, page;
        }
        // Only pages with damage exist. The GPU index is a sparse hash table, not a
        // volume-sized lookup, so a large untouched building allocates no damage storage.
        private readonly Dictionary<Vector3Int, DamagePage> damagePages = new();
        private readonly List<DamagePage> damagePageOrder = new();
        private readonly List<DamagePage> dirtyDamagePages = new();
        private GraphicsBuffer damageCellBuffer, damageIndexBuffer;
        private DamagePageEntry[] damagePageIndex = System.Array.Empty<DamagePageEntry>();
        private Vector3Int damageChunkSize;
        private bool damageIndexDirty, damageDataDirty;
        private int DamagePageCellCount => damageChunkSize.x * damageChunkSize.y * damageChunkSize.z;
        private Color damageTint = Color.black, hitFlashColor = Color.white;
        private float hitFlashDuration = .12f, damageTintStrength = .45f;
        private static readonly int DamageCellsId = Shader.PropertyToID("_DamageCells");
        private static readonly int DamageIndexId = Shader.PropertyToID("_DamagePageIndex");
        private static readonly int DamageIndexMaskId = Shader.PropertyToID("_DamageIndexMask");
        private static readonly int DamageChunkSizeId = Shader.PropertyToID("_DamageChunkSize");
        private static readonly int DamageOriginId = Shader.PropertyToID("_DamageOrigin");
        private static readonly int DamageStepId = Shader.PropertyToID("_DamageStep");
        private static readonly int DamageDimensionsId = Shader.PropertyToID("_DamageDimensions");
        private static readonly int DamageTintId = Shader.PropertyToID("_DamageTint");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashDurationId = Shader.PropertyToID("_FlashDuration");
        private static readonly int DamageStrengthId = Shader.PropertyToID("_DamageStrength");
        private MaterialPropertyBlock CurrentDamageProperties => damageProperties ?? (penetrationFlashActive ? penetrationFlashProperties : null);

        private void ConfigureDamageAppearance(ProjectileTuning tuning)
        {
            if (tuning == null) return;
            damageTint = tuning.explosionDamageTint; hitFlashColor = tuning.explosionHitFlashColor;
            hitFlashDuration = Mathf.Max(.01f, tuning.explosionHitFlashDuration);
            damageTintStrength = Mathf.Clamp01(tuning.explosionDamageTintStrength);
        }

        private void SetCellDamageAppearance(Cell cell)
        {
            if (damageProperties == null)
            {
                // The same forward pass reads only allocated damage pages, including
                // while the target still uses its intact, unsplit mesh.
                var template = damageTemplate != null ? damageTemplate : Resources.Load<Material>("DestructibleDamageLit");
                if (template == null) throw new System.InvalidOperationException("DestructibleDamageLit material is missing.");
                undamagedMaterial = originalRenderer.sharedMaterial;
                damageMaterial = new Material(template);
                damageMaterial.CopyPropertiesFromMaterial(undamagedMaterial);
                damageChunkSize = Vector3Int.Min(divisions, Vector3Int.one * RenderChunkWidth);
                damageProperties = new MaterialPropertyBlock();
                if (penetrationFlashActive) originalRenderer.SetPropertyBlock(penetrationOriginalProperties);
                originalRenderer.GetPropertyBlock(damageProperties);
                damageProperties.SetVector(DamageChunkSizeId, (Vector3)damageChunkSize);
                damageProperties.SetVector(DamageOriginId, gridOrigin);
                damageProperties.SetVector(DamageStepId, gridStep);
                damageProperties.SetVector(DamageDimensionsId, (Vector3)divisions);
                originalRenderer.sharedMaterial = damageMaterial;
                if (instancedMaterial != null) { Destroy(instancedMaterial); instancedMaterial = new Material(damageMaterial); }
            }
            var coordinate = cell.coordinate;
            var key = new Vector3Int(coordinate.x / damageChunkSize.x,
                coordinate.y / damageChunkSize.y, coordinate.z / damageChunkSize.z);
            if (!damagePages.TryGetValue(key, out var page))
            {
                page = new DamagePage { coordinate = key, slot = damagePageOrder.Count,
                    cells = new Vector2[DamagePageCellCount] };
                damagePages.Add(key, page);
                damagePageOrder.Add(page);
                damageIndexDirty = true;
            }
            var local = coordinate - Vector3Int.Scale(key, damageChunkSize);
            page.cells[local.x + damageChunkSize.x * (local.y + damageChunkSize.y * local.z)] =
                new Vector2(Mathf.Clamp01((float)(1d - cell.explosionHealth)), cell.explosionHitTime);
            MarkDamagePageDirty(page);
        }

        private void MarkDamagePageDirty(DamagePage page)
        {
            if (!page.dirty) { page.dirty = true; dirtyDamagePages.Add(page); }
            if (!damageDataDirty) { damageDataDirty = true; RefreshUpdaterRegistration(); }
        }

        private static uint DamagePageHash(Vector3Int key)
        {
            unchecked { return (uint)key.x * 73856093u ^ (uint)key.y * 19349663u ^ (uint)key.z * 83492791u; }
        }

        private void UploadDamageAppearance()
        {
            if (!damageDataDirty || damageProperties == null) return;
            int pageCells = DamagePageCellCount;
            int requiredCells = Mathf.NextPowerOfTwo(damagePageOrder.Count) * pageCells;
            bool grew = damageCellBuffer == null || damageCellBuffer.count < requiredCells;
            if (grew)
            {
                damageCellBuffer?.Release();
                damageCellBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, requiredCells, sizeof(float) * 2);
                damageProperties.SetBuffer(DamageCellsId, damageCellBuffer);
                // Capacity doubles only as new hit regions appear. Re-upload existing
                // allocated pages on growth; never allocate/upload untouched target cells.
                foreach (var page in damagePageOrder)
                    damageCellBuffer.SetData(page.cells, 0, page.slot * pageCells, pageCells);
            }
            else
                foreach (var page in dirtyDamagePages)
                    damageCellBuffer.SetData(page.cells, 0, page.slot * pageCells, pageCells);

            if (damageIndexDirty)
            {
                int indexSize = Mathf.NextPowerOfTwo(Mathf.Max(4, damagePageOrder.Count * 2));
                if (damagePageIndex.Length != indexSize)
                {
                    damagePageIndex = new DamagePageEntry[indexSize];
                    damageIndexBuffer?.Release();
                    damageIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, indexSize, sizeof(int) * 4);
                    damageProperties.SetBuffer(DamageIndexId, damageIndexBuffer);
                    damageProperties.SetFloat(DamageIndexMaskId, indexSize - 1);
                }
                for (int i = 0; i < indexSize; i++) damagePageIndex[i].page = -1;
                int mask = indexSize - 1;
                foreach (var page in damagePageOrder)
                {
                    var key = page.coordinate;
                    int bucket = (int)(DamagePageHash(key) & (uint)mask);
                    while (damagePageIndex[bucket].page >= 0) bucket = (bucket + 1) & mask;
                    damagePageIndex[bucket] = new DamagePageEntry { x = key.x, y = key.y, z = key.z, page = page.slot };
                }
                damageIndexBuffer.SetData(damagePageIndex);
                damageIndexDirty = false;
            }
            damageProperties.SetColor(DamageTintId, damageTint);
            damageProperties.SetColor(FlashColorId, hitFlashColor);
            damageProperties.SetFloat(FlashDurationId, hitFlashDuration);
            damageProperties.SetFloat(DamageStrengthId, damageTintStrength);
            damageProperties.SetFloat(PenetrationFlashUntilId, penetrationFlashActive ? penetrationFlashUntil : 0f);
            originalRenderer.SetPropertyBlock(damageProperties);
            foreach (var page in dirtyDamagePages) page.dirty = false;
            dirtyDamagePages.Clear();
            damageDataDirty = false;
        }

        private void ResetDamageAppearance()
        {
            foreach (var page in damagePageOrder)
            {
                System.Array.Clear(page.cells, 0, page.cells.Length);
                MarkDamagePageDirty(page);
            }
        }

        private void DisposeDamageAppearance()
        {
            if (originalRenderer != null && undamagedMaterial != null) originalRenderer.sharedMaterial = undamagedMaterial;
            if (damageMaterial != null) Destroy(damageMaterial);
            damageCellBuffer?.Release(); damageCellBuffer = null;
            damageIndexBuffer?.Release(); damageIndexBuffer = null;
            damagePages.Clear(); damagePageOrder.Clear(); dirtyDamagePages.Clear();
        }

        private readonly HashSet<Vector3Int> changedBoundary = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> localSupportVisited = new HashSet<Vector3Int>();
        private readonly Queue<Vector3Int> localSupportQueue = new Queue<Vector3Int>();
        private bool supportValidated, resolvingSupportDrop;
        private static readonly ProfilerMarker localSupportMarker = new ProfilerMarker("DestructibleTarget.ResolveSupport.Local");
        private static readonly ProfilerMarker supportFallbackMarker = new ProfilerMarker("DestructibleTarget.ResolveSupport.FullFallback");
        private void RecordSupportRemoval(Vector3Int coordinate)
        {
            if (!supportValidated || resolvingSupportDrop) return;
            foreach (var direction in neighbors) changedBoundary.Add(coordinate + direction);
        }
        private void ResolveSupport()
        {
            if (attachedToMovingBody) return;
            using (supportMarker.Auto())
            {
                if (!supportValidated)
                {
                    using (supportFallbackMarker.Auto()) DropUnsupported();
                }
                else if (changedBoundary.Count > 0)
                    using (localSupportMarker.Auto()) DropUnsupportedNearRemovedBoundary();
                supportValidated = true;
                changedBoundary.Clear();
            }
        }
    }
}
