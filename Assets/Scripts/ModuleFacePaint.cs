using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class ModuleFacePaint : MonoBehaviour
    {
        [SerializeField, Tooltip("도색 가능한 외장 메시만 연결합니다. 내부 코어·화살표는 제외합니다.")]
        private MeshRenderer[] paintSurfaces = new MeshRenderer[0];
        [SerializeField, Tooltip("각 외장 메시가 속한 모듈 면. 0이면 삼각형 방향으로 구분합니다.")]
        private Vector3Int[] paintSurfaceFaces = new Vector3Int[0];

        private sealed class PaintedFace
        {
            public GameObject gameObject;
            public Mesh mesh;
            public MeshRenderer renderer;
        }

        private readonly Dictionary<Vector3Int, PaintedFace> faces = new Dictionary<Vector3Int, PaintedFace>();

        public void ClearAll()
        {
            foreach (PaintedFace painted in faces.Values) DestroyPaintedFace(painted);
            faces.Clear();
        }

        public Material GetMaterial(Vector3Int face)
            => faces.TryGetValue(face, out PaintedFace painted) && painted.renderer != null
                ? painted.renderer.sharedMaterial : null;

        public bool SetMaterial(Vector3Int face, Material material)
        {
            if (material != null && GetComponent<PowerCore>() != null) return false;
            if (Mathf.Abs(face.x) + Mathf.Abs(face.y) + Mathf.Abs(face.z) != 1) return false;
            if (faces.TryGetValue(face, out PaintedFace painted))
            {
                if (painted.renderer == null) return false;
                if (material != null)
                {
                    if (painted.renderer.sharedMaterial == material) return false;
                    painted.renderer.sharedMaterial = material;
                    return true;
                }
                faces.Remove(face);
                DestroyPaintedFace(painted);
                return true;
            }
            if (material == null || !TryCreateFaceMesh(face, out Mesh mesh)) return false;
            var faceObject = new GameObject("PaintedFace");
            faceObject.layer = gameObject.layer;
            faceObject.transform.SetParent(transform, false);
            faceObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = faceObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            faces.Add(face, new PaintedFace { gameObject = faceObject, mesh = mesh, renderer = renderer });
            return true;
        }

        private bool TryCreateFaceMesh(Vector3Int face, out Mesh mesh)
        {
            mesh = null;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            Vector3 direction = face;
            for (int surfaceIndex = 0; surfaceIndex < paintSurfaces.Length; surfaceIndex++)
            {
                var surface = paintSurfaces[surfaceIndex];
                if (surface == null) continue;
                Vector3Int assignedFace = surfaceIndex < paintSurfaceFaces.Length
                    ? paintSurfaceFaces[surfaceIndex] : Vector3Int.zero;
                if (assignedFace != Vector3Int.zero && assignedFace != face) continue;
                var filter = surface.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var source = filter.sharedMesh;
                var sourceVertices = source.vertices;
                var sourceTriangles = source.triangles;
                var matrix = transform.worldToLocalMatrix * surface.transform.localToWorldMatrix;
                for (int i = 0; i < sourceTriangles.Length; i += 3)
                {
                    Vector3 a = matrix.MultiplyPoint3x4(sourceVertices[sourceTriangles[i]]);
                    Vector3 b = matrix.MultiplyPoint3x4(sourceVertices[sourceTriangles[i + 1]]);
                    Vector3 c = matrix.MultiplyPoint3x4(sourceVertices[sourceTriangles[i + 2]]);
                    Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                    // An authored panel includes its recessed opening walls; unassigned bodies use triangle direction.
                    float dominant = Mathf.Max(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                    if (dominant < .001f) continue;
                    if (assignedFace == Vector3Int.zero && Vector3.Dot(normal, direction) < dominant - .0001f) continue;
                    int start = vertices.Count;
                    foreach (var point in new[] { a, b, c })
                    {
                        vertices.Add(point + normal * .0008f);
                        normals.Add(normal);
                        uv.Add(face.x != 0 ? new Vector2(point.z, point.y) :
                            face.y != 0 ? new Vector2(point.x, point.z) : new Vector2(point.x, point.y));
                    }
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                }
            }
            if (vertices.Count == 0) return false;
            mesh = new Mesh { name = "PaintedModuleFace" };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return true;
        }

        private static void DestroyPaintedFace(PaintedFace painted)
        {
            if (painted.gameObject != null)
            {
                if (Application.isPlaying) Destroy(painted.gameObject);
                else DestroyImmediate(painted.gameObject);
            }
            if (painted.mesh != null)
            {
                if (Application.isPlaying) Destroy(painted.mesh);
                else DestroyImmediate(painted.mesh);
            }
        }

        private void OnDestroy() => ClearAll();
    }
}
