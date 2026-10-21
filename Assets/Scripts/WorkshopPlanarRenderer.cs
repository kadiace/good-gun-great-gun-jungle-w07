using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WeaponWorkshop
{
    // No cloned behaviours, colliders or weapon state: submit only the source's meshes to one camera.
    internal sealed class WorkshopPlanarRenderer : IDisposable
    {
        private sealed class Part
        {
            internal Renderer source;
            internal Mesh mesh;
            internal readonly List<Material> materials = new List<Material>();
            internal bool previousForceOff, ownsMesh;
        }

        private const int DisplayLayer = 31; // Draw submissions only; no project layer or source layer is changed.
        private static readonly Vector3 DisplayOrigin = new Vector3(0f, -10000f, 0f);
        private readonly List<Part> parts = new List<Part>();
        private readonly List<Renderer> scan = new List<Renderer>();
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock subProperties = new MaterialPropertyBlock();
        private readonly Camera camera;
        private readonly Material gridMaterial, markerMaterial;
        private readonly Mesh grid, marker;
        private RenderTexture texture;
        private CubeAssembly assembly;
        private Bounds bounds;
        private float zoom = 1f;
        private WorkshopPlanarAxis axis = WorkshopPlanarAxis.Z;
        private int depth;
        private bool dirty = true;
        private float nextReconcile;

        internal Camera ViewCamera => camera;
        internal RenderTexture Texture => texture;

        internal WorkshopPlanarRenderer(Transform owner, Shader shader)
        {
            var root = new GameObject("Workshop Planar Camera") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(owner, false);
            camera = root.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = false;
            camera.fieldOfView = 8f;
            camera.cullingMask = 1 << DisplayLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .045f, .065f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            camera.depth = -100f;
            var data = root.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            gridMaterial = new Material(shader) { name = "Workshop Grid", hideFlags = HideFlags.DontSave };
            markerMaterial = new Material(shader) { name = "Workshop Surface Marker", hideFlags = HideFlags.DontSave };
            gridMaterial.SetColor("_BaseColor", new Color(.4f, .62f, .8f, .28f));
            grid = new Mesh { name = "Workshop Grid", hideFlags = HideFlags.DontSave };
            marker = CreateMarker();
        }

        internal void Bind(CubeAssembly source)
        {
            ReleaseSource();
            assembly = source;
            if (assembly == null) return;
            RefreshParts();
            nextReconcile = Time.unscaledTime + .25f;
        }

        internal void SetView(WorkshopPlanarAxis nextAxis, int nextDepth)
        {
            if (axis == nextAxis && depth == nextDepth) return;
            axis = nextAxis; depth = nextDepth; dirty = true;
        }

        internal void Zoom(float scroll)
        {
            zoom = Mathf.Clamp(zoom * Mathf.Exp(-scroll * .12f), .3f, 4f);
            dirty = true;
        }

        internal void Resize(int width, int height)
        {
            width = Mathf.Clamp(width, 64, 2048); height = Mathf.Clamp(height, 64, 2048);
            if (texture != null && texture.width == width && texture.height == height) return;
            ReleaseTexture();
            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            { name = "Workshop Planar View", hideFlags = HideFlags.DontSave, antiAliasing = 1 };
            texture.Create();
            camera.targetTexture = texture;
            camera.aspect = (float)width / height;
            dirty = true;
        }

        internal void SetVisible(bool visible) => camera.enabled = visible && assembly != null && texture != null;
        internal void PrepareView() { if (dirty && assembly != null && texture != null) FrameView(); }

        internal void Draw(bool hasSelection, WorkshopGridSelection selection)
        {
            if (assembly == null || !camera.enabled) return;
            if (Time.unscaledTime >= nextReconcile)
            {
                nextReconcile = Time.unscaledTime + .25f;
                scan.Clear(); assembly.GetComponentsInChildren(true, scan);
                bool changed = false;
                int supported = 0;
                foreach (var source in scan)
                {
                    if (!HasMesh(source)) continue;
                    if (supported >= parts.Count || parts[supported].source != source) changed = true;
                    supported++;
                }
                changed |= supported != parts.Count;
                if (changed) RefreshParts();
            }
            if (dirty) FrameView();
            Matrix4x4 fromAssembly = Matrix4x4.Translate(DisplayOrigin);
            foreach (var part in parts)
            {
                if (part.source == null || part.previousForceOff || !part.source.enabled || !part.source.gameObject.activeInHierarchy) continue;
                if (part.ownsMesh) ((SkinnedMeshRenderer)part.source).BakeMesh(part.mesh);
                Matrix4x4 matrix = fromAssembly * assembly.transform.worldToLocalMatrix * part.source.localToWorldMatrix;
                part.source.GetSharedMaterials(part.materials);
                part.source.GetPropertyBlock(properties);
                for (int i = 0; i < part.mesh.subMeshCount && i < part.materials.Count; i++)
                {
                    if (part.materials[i] == null) continue;
                    part.source.GetPropertyBlock(subProperties, i);
                    DrawMesh(part.mesh, part.materials[i], matrix, subProperties.isEmpty ? properties : subProperties, i);
                }
            }
            DrawMesh(grid, gridMaterial, fromAssembly, null, 0);
            if (hasSelection)
            {
                markerMaterial.SetColor("_BaseColor", selection.IsValid
                    ? new Color(.25f, 1f, .65f, 1f) : new Color(1f, .3f, .25f, 1f));
                DrawMesh(marker, markerMaterial, fromAssembly * Matrix4x4.Translate(selection.Cell), null, 0);
            }
        }

        private void DrawMesh(Mesh mesh, Material material, Matrix4x4 matrix, MaterialPropertyBlock block, int submesh)
        {
            var parameters = new RenderParams(material)
            {
                camera = camera, layer = DisplayLayer, matProps = block,
                shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                worldBounds = TransformBounds(mesh.bounds, matrix)
            };
            Graphics.RenderMesh(parameters, mesh, submesh, matrix);
        }

        internal bool TryGetPlaneCell(Vector2 viewportPoint, WorkshopPlanarAxis planeAxis, int planeDepth, out Vector3Int cell)
        {
            cell = default;
            if (assembly == null) return false;
            Ray ray = camera.ViewportPointToRay(viewportPoint);
            var normal = WorkshopPlanarCoordinates.Normal(planeAxis);
            var plane = new Plane(normal, DisplayOrigin + normal * planeDepth);
            if (!plane.Raycast(ray, out float distance)) return false;
            cell = Vector3Int.RoundToInt(ray.GetPoint(distance) - DisplayOrigin);
            return true;
        }

        private void RefreshParts()
        {
            RestoreRenderers();
            scan.Clear(); assembly.GetComponentsInChildren(true, scan);
            bool first = true;
            foreach (var source in scan)
            {
                if (!HasMesh(source)) continue;
                Mesh mesh = null;
                bool owns = source is SkinnedMeshRenderer;
                if (owns)
                {
                    mesh = new Mesh { name = "Workshop Baked Display", hideFlags = HideFlags.DontSave };
                    ((SkinnedMeshRenderer)source).BakeMesh(mesh);
                }
                else if (source.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                if (mesh == null) continue;
                parts.Add(new Part { source = source, mesh = mesh, ownsMesh = owns,
                    previousForceOff = source.forceRenderingOff });
                source.forceRenderingOff = true;
                Bounds local = TransformBounds(mesh.bounds, assembly.transform.worldToLocalMatrix * source.localToWorldMatrix);
                if (first) { bounds = local; first = false; } else bounds.Encapsulate(local);
            }
            if (first) bounds = new Bounds(Vector3.zero, Vector3.one);
            dirty = true;
        }

        internal void RefreshSource() { if (assembly != null) RefreshParts(); }

        private void FrameView()
        {
            dirty = false;
            var normal = WorkshopPlanarCoordinates.Normal(axis);
            var up = WorkshopPlanarCoordinates.Up(axis);
            Vector3 right = Vector3.Cross(up, -normal);
            float vertical = Vector3.Dot(Abs(up), bounds.size);
            float horizontal = Vector3.Dot(Abs(right), bounds.size);
            float height = Mathf.Max(6f, vertical + 3f, (horizontal + 3f) / camera.aspect) * zoom;
            float distance = height * .5f / Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            float depthExtent = Vector3.Dot(Abs(normal), bounds.extents);
            float planeOffset = depth - Vector3.Dot(normal, bounds.center);
            distance += Mathf.Max(depthExtent, planeOffset);
            Vector3 center = DisplayOrigin + bounds.center;
            camera.transform.SetPositionAndRotation(center + normal * distance, Quaternion.LookRotation(-normal, up));
            camera.nearClipPlane = .01f;
            camera.farClipPlane = distance + Mathf.Max(bounds.size.magnitude, Mathf.Abs(planeOffset)) + 20f;
            BuildGrid(Mathf.Clamp(Mathf.CeilToInt(height * Mathf.Max(1f, camera.aspect)), 4, 96));
        }

        private void BuildGrid(int radius)
        {
            var vertices = new List<Vector3>((radius * 2 + 2) * 4);
            int centerU = Mathf.RoundToInt(axis == WorkshopPlanarAxis.X ? bounds.center.z : bounds.center.x);
            int centerV = Mathf.RoundToInt(axis == WorkshopPlanarAxis.Y ? bounds.center.z : bounds.center.y);
            for (int i = -radius; i <= radius + 1; i++)
            {
                float line = i - .5f;
                vertices.Add(WorkshopPlanarCoordinates.Point(centerU + line, centerV - radius - .5f, depth, axis));
                vertices.Add(WorkshopPlanarCoordinates.Point(centerU + line, centerV + radius + .5f, depth, axis));
                vertices.Add(WorkshopPlanarCoordinates.Point(centerU - radius - .5f, centerV + line, depth, axis));
                vertices.Add(WorkshopPlanarCoordinates.Point(centerU + radius + .5f, centerV + line, depth, axis));
            }
            var indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            grid.Clear(); grid.SetVertices(vertices); grid.SetIndices(indices, MeshTopology.Lines, 0);
            grid.RecalculateBounds();
        }

        private static Mesh CreateMarker()
        {
            var vertices = new Vector3[8];
            for (int i = 0; i < 8; i++) vertices[i] = new Vector3((i & 1) == 0 ? -.502f : .502f,
                (i & 2) == 0 ? -.502f : .502f, (i & 4) == 0 ? -.502f : .502f);
            var mesh = new Mesh { name = "Workshop Cell Outline", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.SetIndices(new[] { 0,1, 2,3, 4,5, 6,7, 0,2, 1,3, 4,6, 5,7, 0,4, 1,5, 2,6, 3,7 }, MeshTopology.Lines, 0);
            mesh.RecalculateBounds(); return mesh;
        }

        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        private static bool HasMesh(Renderer source) => source is SkinnedMeshRenderer skin
            ? skin.sharedMesh != null : source.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null;
        private static Bounds TransformBounds(Bounds value, Matrix4x4 matrix)
        {
            Vector3 x = matrix.MultiplyVector(new Vector3(value.extents.x, 0f, 0f));
            Vector3 y = matrix.MultiplyVector(new Vector3(0f, value.extents.y, 0f));
            Vector3 z = matrix.MultiplyVector(new Vector3(0f, 0f, value.extents.z));
            return new Bounds(matrix.MultiplyPoint3x4(value.center), (Abs(x) + Abs(y) + Abs(z)) * 2f);
        }

        internal void Suspend() => RestoreRenderers();
        internal void Resume() { if (assembly != null) RefreshParts(); }
        private void RestoreRenderers()
        {
            foreach (var part in parts)
            {
                if (part.source != null) part.source.forceRenderingOff = part.previousForceOff;
                if (part.ownsMesh) UnityEngine.Object.Destroy(part.mesh);
            }
            parts.Clear();
        }
        private void ReleaseSource()
        {
            camera.enabled = false;
            RestoreRenderers(); assembly = null; zoom = 1f;
        }
        private void ReleaseTexture()
        {
            camera.targetTexture = null;
            if (texture == null) return;
            texture.Release(); UnityEngine.Object.Destroy(texture); texture = null;
        }
        public void Dispose()
        {
            ReleaseSource(); ReleaseTexture();
            UnityEngine.Object.Destroy(camera.gameObject);
            UnityEngine.Object.Destroy(grid); UnityEngine.Object.Destroy(marker);
            UnityEngine.Object.Destroy(gridMaterial); UnityEngine.Object.Destroy(markerMaterial);
        }
    }
}
