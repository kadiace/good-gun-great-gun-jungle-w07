using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

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
            public CombineInstance[] combines = System.Array.Empty<CombineInstance>();
        }
        private readonly Dictionary<Vector3Int, RenderChunk> renderChunks = new Dictionary<Vector3Int, RenderChunk>();
        private static readonly ProfilerMarker meshMarker = new ProfilerMarker("DestructibleTarget.RebuildChunkMeshes");
        private static readonly ProfilerMarker supportMarker = new ProfilerMarker("DestructibleTarget.ResolveSupport");
        private static readonly ProfilerMarker collisionMarker = new ProfilerMarker("DestructibleTarget.UpdateCollisionRuns");
        private static readonly ProfilerMarker subdivideMarker = new ProfilerMarker("DestructibleTarget.Subdivide");
        private static readonly ProfilerMarker explosionFinishMarker = new ProfilerMarker("DestructibleTarget.FinishExplosionStep");
        private static readonly ProfilerMarker subdivisionBudgetMarker = new ProfilerMarker("DestructibleTarget.SubdivisionBudget");
        // 0 keeps existing callers unlimited until they adopt the resumable collection contract.
        public static int MaxSubdivisionsPerFrame { get; set; }
        private static int subdivisionBudgetFrame = -1;
        private static int subdivisionsThisFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubdivisionBudget()
        {
            subdivisionBudgetFrame = -1;
            subdivisionsThisFrame = 0;
            MaxSubdivisionsPerFrame = 0;
        }

        private static bool IsSubdivisionBudgetAvailable()
        {
            using (subdivisionBudgetMarker.Auto())
            {
                int limit = MaxSubdivisionsPerFrame;
                return limit <= 0 || subdivisionBudgetFrame != Time.frameCount || subdivisionsThisFrame < limit;
            }
        }

        private static bool TryReserveSubdivisionBudget()
        {
            using (subdivisionBudgetMarker.Auto())
            {
                int limit = MaxSubdivisionsPerFrame;
                if (limit <= 0) return true;
                if (subdivisionBudgetFrame != Time.frameCount)
                {
                    subdivisionBudgetFrame = Time.frameCount;
                    subdivisionsThisFrame = 0;
                }
                if (subdivisionsThisFrame >= limit) return false;
                subdivisionsThisFrame++;
                return true;
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
                        for (int x = origin.x; x < Mathf.Min(origin.x + RenderChunkWidth, divisions.x); x++)
                            for (int y = origin.y; y < Mathf.Min(origin.y + RenderChunkWidth, divisions.y); y++)
                                for (int z = origin.z; z < Mathf.Min(origin.z + RenderChunkWidth, divisions.z); z++)
                                    if (TryGetCell(new Vector3Int(x,y,z), out var cell))
                                        chunk.combines[used++] = new CombineInstance { mesh = cellMesh,
                                            transform = Matrix4x4.TRS(cell.localPosition,Quaternion.identity,cell.localScale) };
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
                    Graphics.DrawMesh(chunk.mesh,CellRoot.localToWorldMatrix,instancedMaterial,targetMesh.gameObject.layer,
                        null,0,null,UnityEngine.Rendering.ShadowCastingMode.On,true);
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
