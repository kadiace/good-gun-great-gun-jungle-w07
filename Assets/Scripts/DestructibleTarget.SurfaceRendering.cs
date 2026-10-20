using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 개선 복셀 렌더링(DestructionOptimizationSettings가 켠 씬에서만 사용). CombineMeshes 대신
    // 청크별로 빈 공간에 노출된 면만 골라 정점·인덱스·법선·UV를 재사용 버퍼에 채워 메시를 만든다.
    // 칸 크기·UV 배치·머테리얼은 기존 칸 큐브 결합과 동일하게 유지한다. 해제된 씬에서는 이 파일이
    // 만드는 메시를 전혀 만지지 않고 기존 렌더링(DestructibleTarget.Rendering.cs)을 그대로 쓴다.
    public sealed partial class DestructibleTarget
    {
        // 면별 표는 공용 neighbors 배열 순서(+X, -X, +Y, -Y, +Z, -Z)와 같은 인덱스를 쓴다.
        private static readonly Vector3[] surfaceFaceNormals =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
        };
        // 면 원점 모서리(칸 중심에서 반 크기 단위 부호). u×v가 면 법선이 되도록 u·v 축을 정해
        // 바깥에서 봤을 때 시계 반대 방향으로 삼각형을 잇는다.
        private static readonly Vector3[] surfaceFaceOriginSigns =
        {
            new Vector3(1f, -1f, -1f), new Vector3(-1f, -1f, -1f),
            new Vector3(-1f, 1f, -1f), new Vector3(-1f, -1f, -1f),
            new Vector3(-1f, -1f, 1f), new Vector3(-1f, -1f, -1f)
        };
        private static readonly Vector3[] surfaceFaceU =
        {
            Vector3.up, Vector3.forward,
            Vector3.forward, Vector3.right,
            Vector3.right, Vector3.up
        };
        private static readonly Vector3[] surfaceFaceV =
        {
            Vector3.forward, Vector3.up,
            Vector3.right, Vector3.forward,
            Vector3.up, Vector3.right
        };

        // 청크·표적 전체에서 재사용하는 버퍼. 청크 재구축은 메인 스레드에서 순차 실행되므로 공유해도 안전하다.
        private static readonly List<Vector3> surfaceVertices = new List<Vector3>(4096);
        private static readonly List<Vector3> surfaceNormals = new List<Vector3>(4096);
        private static readonly List<Vector4> surfaceTangents = new List<Vector4>(4096);
        private static readonly List<Vector2> surfaceUVs = new List<Vector2>(4096);
        private static readonly List<int> surfaceIndices = new List<int>(8192);

        // 표적이 속한 씬 기준으로 한 번만 판정한다. 칸 분할 후에만 조회되므로 씬 판정 시점이 어긋나지 않는다.
        private bool surfaceRendering;
        private bool surfaceRenderingResolved;
        private bool SurfaceRendering
        {
            get
            {
                if (!surfaceRenderingResolved)
                {
                    surfaceRendering = DestructionOptimizationSettings.EnabledFor(gameObject.scene);
                    surfaceRenderingResolved = true;
                }
                return surfaceRendering;
            }
        }

        // 최악의 한 청크(8×8×8칸)는 512칸 × 6면 × 4정점 = 12,288개로 16비트 인덱스 한도 안이다.
        private void RebuildSurfaceChunk(Vector3Int key, RenderChunk chunk)
        {
            Vector3Int origin = key * RenderChunkWidth;
            int maxX = Mathf.Min(origin.x + RenderChunkWidth, divisions.x);
            int maxY = Mathf.Min(origin.y + RenderChunkWidth, divisions.y);
            int maxZ = Mathf.Min(origin.z + RenderChunkWidth, divisions.z);
            surfaceVertices.Clear(); surfaceNormals.Clear(); surfaceTangents.Clear();
            surfaceUVs.Clear(); surfaceIndices.Clear();
            var coordinate = new Vector3Int();
            for (coordinate.x = origin.x; coordinate.x < maxX; coordinate.x++)
                for (coordinate.y = origin.y; coordinate.y < maxY; coordinate.y++)
                    for (coordinate.z = origin.z; coordinate.z < maxZ; coordinate.z++)
                    {
                        if (!TryGetCell(coordinate, out Cell cell)) continue;
                        Vector3 half = cell.localScale * 0.5f;
                        for (int face = 0; face < neighbors.Length; face++)
                        {
                            // 이웃 칸이 막고 있으면 숨은 면이므로 내보내지 않는다. 경계 밖은 노출 면.
                            if (HasCell(coordinate + neighbors[face])) continue;
                            Vector3 corner = cell.localPosition + Vector3.Scale(surfaceFaceOriginSigns[face], half);
                            Vector3 scaledU = Vector3.Scale(surfaceFaceU[face], cell.localScale);
                            Vector3 scaledV = Vector3.Scale(surfaceFaceV[face], cell.localScale);
                            int baseIndex = surfaceVertices.Count;
                            surfaceVertices.Add(corner);
                            surfaceVertices.Add(corner + scaledU);
                            surfaceVertices.Add(corner + scaledV);
                            surfaceVertices.Add(corner + scaledU + scaledV);
                            Vector3 normal = surfaceFaceNormals[face];
                            Vector4 tangent = Vector3.Normalize(scaledU); tangent.w = 1f;
                            for (int cornerIndex = 0; cornerIndex < 4; cornerIndex++)
                            {
                                surfaceNormals.Add(normal);
                                surfaceTangents.Add(tangent);
                            }
                            surfaceUVs.Add(Vector2.zero);
                            surfaceUVs.Add(Vector2.right);
                            surfaceUVs.Add(Vector2.up);
                            surfaceUVs.Add(Vector2.one);
                            surfaceIndices.Add(baseIndex); surfaceIndices.Add(baseIndex + 1); surfaceIndices.Add(baseIndex + 3);
                            surfaceIndices.Add(baseIndex); surfaceIndices.Add(baseIndex + 3); surfaceIndices.Add(baseIndex + 2);
                        }
                    }
            if (chunk.mesh == null)
            {
                chunk.mesh = new Mesh { name = "RemainingTargetSurface" };
                chunk.mesh.MarkDynamic();
            }
            chunk.mesh.Clear();
            if (surfaceVertices.Count > 0)
            {
                chunk.mesh.SetVertices(surfaceVertices);
                chunk.mesh.SetNormals(surfaceNormals);
                chunk.mesh.SetTangents(surfaceTangents);
                chunk.mesh.SetUVs(0, surfaceUVs);
                chunk.mesh.SetTriangles(surfaceIndices, 0);
                chunk.mesh.RecalculateBounds();
            }
        }
    }
}
