using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using System.Diagnostics;

namespace WeaponWorkshop
{
    public sealed partial class DestructibleTarget
    {
        // Cache bounded meshes per target; a narrow hole only rebuilds nearby chunks.
        private const int RenderChunkWidth = 8;
        private sealed class RenderChunk
        {
            public Mesh mesh;
            public bool dirty = true;
            public bool damageDirty = true, hasDamage;
            public readonly List<int> cellIndices = new List<int>();
            public readonly List<Vector2> damageUV = new List<Vector2>();
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
            if (renderChunks.TryGetValue(RenderKey(coordinate), out var chunk)) chunk.dirty = true;
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
                        var origin = pair.Key * RenderChunkWidth;
                        int count = 0;
                        for (int x = origin.x; x < Mathf.Min(origin.x + RenderChunkWidth, divisions.x); x++)
                            for (int y = origin.y; y < Mathf.Min(origin.y + RenderChunkWidth, divisions.y); y++)
                                for (int z = origin.z; z < Mathf.Min(origin.z + RenderChunkWidth, divisions.z); z++)
                                    if (HasCell(new Vector3Int(x,y,z))) count++;
                        if (chunk.combines.Length != count) chunk.combines = new CombineInstance[count];
                        int used = 0;
                        chunk.cellIndices.Clear();
                        for (int x = origin.x; x < Mathf.Min(origin.x + RenderChunkWidth, divisions.x); x++)
                            for (int y = origin.y; y < Mathf.Min(origin.y + RenderChunkWidth, divisions.y); y++)
                                for (int z = origin.z; z < Mathf.Min(origin.z + RenderChunkWidth, divisions.z); z++)
                                    if (TryGetCell(new Vector3Int(x,y,z), out var cell))
                                    {
                                        chunk.cellIndices.Add(CellIndex(cell.coordinate));
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
                        chunk.damageDirty = true;
                    }
                    renderDirty = false;
                }
            }
            foreach (var chunk in renderChunks.Values)
                if (chunk.mesh != null && chunk.mesh.vertexCount > 0)
                {
                    if (chunk.damageDirty) UpdateChunkDamage(chunk);
                    Graphics.DrawMesh(chunk.mesh,CellRoot.localToWorldMatrix,instancedMaterial,targetMesh.gameObject.layer,
                        null,0,null,UnityEngine.Rendering.ShadowCastingMode.On,true);
                    if (chunk.hasDamage)
                    {
                        EnsureDamageMaterial();
                        if (damageOverlayMaterial != null)
                            Graphics.DrawMesh(chunk.mesh, CellRoot.localToWorldMatrix, damageOverlayMaterial,
                                targetMesh.gameObject.layer, null, 0, damageProperties,
                                UnityEngine.Rendering.ShadowCastingMode.Off, false);
                    }
                }
        }

        // Shared overlay preserves the original material/lighting; only damaged chunks get another draw.
        private static Material damageOverlayMaterial;
        private MaterialPropertyBlock damageProperties;
        private static readonly int DamageTintId = Shader.PropertyToID("_DamageTint");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashDurationId = Shader.PropertyToID("_FlashDuration");
        private static readonly int DamageStrengthId = Shader.PropertyToID("_DamageStrength");

        private void EnsureDamageMaterial()
        {
            if (damageOverlayMaterial == null)
                damageOverlayMaterial = Resources.Load<Material>("DestructibleDamage");
            if (damageProperties == null) ConfigureDamageAppearance(null);
        }

        private void ConfigureDamageAppearance(ProjectileTuning tuning)
        {
            damageProperties ??= new MaterialPropertyBlock();
            damageProperties.SetColor(DamageTintId, tuning != null ? tuning.explosionDamageTint : Color.black);
            damageProperties.SetColor(FlashColorId, tuning != null ? tuning.explosionHitFlashColor : Color.white);
            damageProperties.SetFloat(FlashDurationId, tuning != null ? Mathf.Max(0.01f, tuning.explosionHitFlashDuration) : 0.12f);
            damageProperties.SetFloat(DamageStrengthId, tuning != null ? Mathf.Clamp01(tuning.explosionDamageTintStrength) : 0.45f);
        }

        private void UpdateChunkDamage(RenderChunk chunk)
        {
            chunk.damageUV.Clear();
            chunk.hasDamage = false;
            int verticesPerCell = cellMesh.vertexCount;
            foreach (int index in chunk.cellIndices)
            {
                var cell = preparedCells[index];
                float damage = Mathf.Clamp01((float)(1d - cell.explosionHealth));
                chunk.hasDamage |= damage > 0f;
                var value = new Vector2(damage, cell.explosionHitTime);
                for (int vertex = 0; vertex < verticesPerCell; vertex++) chunk.damageUV.Add(value);
            }
            chunk.mesh.SetUVs(3, chunk.damageUV);
            chunk.damageDirty = false;
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
