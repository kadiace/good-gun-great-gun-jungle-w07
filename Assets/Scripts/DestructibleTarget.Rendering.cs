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
                                    if (grid.ContainsKey(new Vector3Int(x,y,z))) count++;
                        if (chunk.combines.Length != count) chunk.combines = new CombineInstance[count];
                        int used = 0;
                        for (int x = origin.x; x < Mathf.Min(origin.x + RenderChunkWidth, divisions.x); x++)
                            for (int y = origin.y; y < Mathf.Min(origin.y + RenderChunkWidth, divisions.y); y++)
                                for (int z = origin.z; z < Mathf.Min(origin.z + RenderChunkWidth, divisions.z); z++)
                                    if (grid.TryGetValue(new Vector3Int(x,y,z), out var cell))
                                        chunk.combines[used++] = new CombineInstance { mesh = cellMesh,
                                            transform = Matrix4x4.TRS(cell.localPosition,cell.localRotation,cell.localScale) };
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
        private bool supportValidated, removedGroundSupport;
        private void RecordSupportRemoval(Cell cell, Vector3Int coordinate)
        {
            // Full checks are already required in these cases; do not build a boundary cache.
            if (!supportValidated || removedGroundSupport) return;
            float halfHeight = Mathf.Abs(cell.TransformVector(Vector3.right * .5f).y)
                + Mathf.Abs(cell.TransformVector(Vector3.up * .5f).y)
                + Mathf.Abs(cell.TransformVector(Vector3.forward * .5f).y);
            if (cell.position.y - halfHeight <= transform.position.y + .001f)
            {
                removedGroundSupport = true; changedBoundary.Clear(); return;
            }
            foreach (var direction in neighbors) changedBoundary.Add(coordinate + direction);
        }
        private bool PreservesSupportLocally()
        {
            if (!supportValidated || removedGroundSupport) return false;
            changedBoundary.RemoveWhere(c => !grid.ContainsKey(c));
            if (changedBoundary.Count == 0) return true;
            localSupportVisited.Clear(); localSupportQueue.Clear();
            foreach (var coordinate in changedBoundary)
            { localSupportQueue.Enqueue(coordinate); localSupportVisited.Add(coordinate); break; }
            int remaining = changedBoundary.Count;
            // Reaching every surviving neighbor proves connectivity was preserved.
            // An inconclusive bounded search falls back to the original full calculation.
            while (localSupportQueue.Count > 0 && localSupportVisited.Count <= 512)
            {
                var coordinate = localSupportQueue.Dequeue();
                if (changedBoundary.Contains(coordinate) && --remaining == 0) return true;
                foreach (var direction in neighbors)
                {
                    var next = coordinate + direction;
                    if (grid.ContainsKey(next) && localSupportVisited.Add(next)) localSupportQueue.Enqueue(next);
                }
            }
            return false;
        }
        private void ResolveSupport()
        {
            if (attachedToMovingBody) return;
            using (supportMarker.Auto())
            {
                if (!PreservesSupportLocally()) DropUnsupported();
                supportValidated = true;
                changedBoundary.Clear(); removedGroundSupport = false;
            }
        }
    }
}
