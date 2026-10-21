using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WeaponWorkshop
{
    // Original meshes, existing camera, one reversible display matrix. No cloned gun or viewport.
    internal sealed class WorkshopPlanarRenderer : IDisposable
    {
        private sealed class Part
        {
            internal MeshRenderer source;
            internal bool previousForceOff;
            internal readonly List<Material> materials = new List<Material>();
        }
        private readonly Camera camera;
        private readonly List<Part> parts = new List<Part>();
        private readonly List<MeshRenderer> scan = new List<MeshRenderer>();
        private readonly List<Collider> colliders = new List<Collider>();
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock subProperties = new MaterialPropertyBlock();
        private readonly Material gridMaterial, markerMaterial;
        private readonly Mesh grid, marker, directionArrow;
        private CubeAssembly assembly;
        private Bounds bounds;
        private Vector3 pivot;
        private WorkshopPlanarAxis axis;
        private int depth;
        private Quaternion rotation = Quaternion.identity, fromRotation, toRotation;
        private float compression = 1f, fromCompression, toCompression;
        private float scale = 1f, fromScale, toScale, zoom = 1f;
        private float elapsed, duration, overlay, fromOverlay, toOverlay;
        internal bool IsTransitioning { get; private set; }
        internal Matrix4x4 AssemblyToDisplay { get; private set; } = Matrix4x4.identity;

        internal WorkshopPlanarRenderer(Camera viewCamera, Shader shader)
        {
            camera = viewCamera;
            gridMaterial = new Material(shader) { name = "Workshop Grid", hideFlags = HideFlags.DontSave };
            markerMaterial = new Material(shader) { name = "Workshop Cell Marker", hideFlags = HideFlags.DontSave };
            grid = new Mesh { name = "Workshop Grid", hideFlags = HideFlags.DontSave };
            marker = CreateMarker();
            directionArrow = new Mesh { name = "Workshop Output Preview", hideFlags = HideFlags.DontSave };
            directionArrow.vertices = new[] { new Vector3(0f, .55f, 0f), new Vector3(0f, 1.12f, 0f),
                new Vector3(-.14f, .88f, 0f), new Vector3(.14f, .88f, 0f) };
            directionArrow.SetIndices(new[] { 0, 1, 1, 2, 1, 3 }, MeshTopology.Lines, 0);
            directionArrow.RecalculateBounds();
        }

        internal static bool Supports(CubeAssembly source)
        {
            // Do not bake/duplicate an unsupported animated mesh or omit part of a weapon.
            foreach (var renderer in source.GetComponentsInChildren<Renderer>())
                if (renderer.enabled && !renderer.forceRenderingOff &&
                    (!(renderer is MeshRenderer) || !renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null))
                    return false;
            return true;
        }

        internal void Bind(CubeAssembly source)
        {
            RestoreRenderers();
            assembly = source; IsTransitioning = false;
            rotation = Quaternion.identity; compression = scale = zoom = 1f; overlay = 0f;
            if (assembly == null) return;
            RefreshSource();
            // Keep the pivot fixed during construction so adding a cell cannot move the gun.
            pivot = bounds.center;
            PrepareView();
        }

        internal void Transition(bool planar, WorkshopPlanarAxis nextAxis, float seconds, float remainingPerspective)
        {
            axis = nextAxis;
            fromRotation = rotation; fromCompression = compression; fromScale = scale; fromOverlay = overlay;
            toRotation = planar ? AxisRotation(axis) : Quaternion.identity;
            toCompression = planar ? Mathf.Clamp(remainingPerspective, .05f, 1f) : 1f;
            toScale = planar ? FitScale(toRotation, toCompression) * zoom : 1f;
            toOverlay = planar ? 1f : 0f;
            duration = Mathf.Max(.01f, seconds); elapsed = 0f; IsTransitioning = true;
            BuildGrid();
        }

        internal void Advance(float deltaTime)
        {
            if (IsTransitioning)
            {
                elapsed += deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                rotation = Quaternion.Slerp(fromRotation, toRotation, t);
                compression = Mathf.Lerp(fromCompression, toCompression, t);
                scale = Mathf.Lerp(fromScale, toScale, t); overlay = Mathf.Lerp(fromOverlay, toOverlay, t);
                IsTransitioning = elapsed < duration;
            }
            PrepareView();
        }

        internal void SetDepth(int value) { depth = value; BuildGrid(); }
        internal void Zoom(float scroll)
        {
            if (IsTransitioning) return;
            float next = Mathf.Clamp(zoom * Mathf.Exp(scroll * .12f), .35f, 3.5f);
            scale *= next / zoom; zoom = next;
            PrepareView();
        }
        private Quaternion AxisRotation(WorkshopPlanarAxis value)
        {
            var localView = Quaternion.LookRotation(-WorkshopPlanarCoordinates.Normal(value), WorkshopPlanarCoordinates.Up(value));
            return camera.transform.rotation * Quaternion.Inverse(localView) * Quaternion.Inverse(assembly.transform.rotation);
        }
        private Matrix4x4 WorldDisplay(Quaternion turn, float depthScale, float size)
        {
            Vector3 worldPivot = assembly.transform.TransformPoint(pivot);
            Vector3 viewPivot = camera.worldToCameraMatrix.MultiplyPoint3x4(worldPivot);
            Matrix4x4 compress = camera.cameraToWorldMatrix * Matrix4x4.Translate(viewPivot) *
                Matrix4x4.Scale(new Vector3(1f, 1f, depthScale)) * Matrix4x4.Translate(-viewPivot) * camera.worldToCameraMatrix;
            return compress * Matrix4x4.TRS(worldPivot, turn, Vector3.one * size) * Matrix4x4.Translate(-worldPivot);
        }
        private float FitScale(Quaternion turn, float depthScale)
        {
            float reference = ProjectedSize(WorldDisplay(rotation, compression, scale / zoom));
            float target = ProjectedSize(WorldDisplay(turn, depthScale, 1f));
            return target > .001f ? Mathf.Clamp(reference / target, .1f, 10f) : 1f;
        }
        private float ProjectedSize(Matrix4x4 worldDisplay)
        {
            var matrix = worldDisplay * assembly.transform.localToWorldMatrix;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 screen = camera.WorldToScreenPoint(matrix.MultiplyPoint3x4(corner));
                if (screen.z <= camera.nearClipPlane) return 0f;
                min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
            }
            return (max - min).magnitude;
        }
        internal void PrepareView()
        {
            if (assembly != null) AssemblyToDisplay = WorldDisplay(rotation, compression, scale) * assembly.transform.localToWorldMatrix;
        }

        internal void Draw(bool hasSelection, WorkshopGridSelection selection, bool showGrid = true, PlacedBlock deleteTarget = null, bool directionPending = false)
        {
            if (assembly == null || camera == null) return;
            PrepareView();
            Matrix4x4 worldDisplay = AssemblyToDisplay * assembly.transform.worldToLocalMatrix;
            foreach (var part in parts)
            {
                var source = part.source;
                if (source == null || part.previousForceOff || !source.enabled || !source.gameObject.activeInHierarchy) continue;
                var filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var mesh = filter.sharedMesh;
                source.GetSharedMaterials(part.materials); source.GetPropertyBlock(properties);
                for (int i = 0; i < mesh.subMeshCount && i < part.materials.Count; i++)
                {
                    if (part.materials[i] == null) continue;
                    source.GetPropertyBlock(subProperties, i);
                    DrawMesh(mesh, part.materials[i], worldDisplay * source.localToWorldMatrix,
                        subProperties.isEmpty ? properties : subProperties, i, source.gameObject.layer, source);
                }
            }
            if (!showGrid) return;
            gridMaterial.SetColor("_BaseColor", new Color(.4f, .62f, .8f, .28f * overlay));
            DrawMesh(grid, gridMaterial, AssemblyToDisplay, null, 0, assembly.gameObject.layer);
            if (deleteTarget != null && !IsTransitioning)
            {
                markerMaterial.SetColor("_BaseColor", new Color(1f, .3f, .25f));
                DrawMesh(marker, markerMaterial, AssemblyToDisplay * Matrix4x4.Translate(deleteTarget.Cell), null, 0, assembly.gameObject.layer);
                return;
            }
            if (!hasSelection || IsTransitioning) return;
            markerMaterial.SetColor("_BaseColor", directionPending && selection.OutputDirection == Vector3Int.zero
                ? new Color(1f, .8f, .25f) : selection.IsValid ? new Color(.25f, 1f, .65f) : new Color(1f, .3f, .25f));
            DrawMesh(marker, markerMaterial, AssemblyToDisplay * Matrix4x4.Translate(selection.Cell), null, 0, assembly.gameObject.layer);
            if (directionPending && selection.OutputDirection.sqrMagnitude == 1)
            {
                var rotation = Quaternion.LookRotation(WorkshopPlanarCoordinates.Normal(selection.Axis), selection.OutputDirection);
                DrawMesh(directionArrow, markerMaterial, AssemblyToDisplay * Matrix4x4.TRS(selection.Cell, rotation, Vector3.one), null, 0, assembly.gameObject.layer);
                if (selection.ModuleId == 3)
                {
                    var opposite = Quaternion.LookRotation(WorkshopPlanarCoordinates.Normal(selection.Axis), -selection.OutputDirection);
                    DrawMesh(directionArrow, markerMaterial, AssemblyToDisplay * Matrix4x4.TRS(selection.Cell, opposite, Vector3.one), null, 0, assembly.gameObject.layer);
                }
            }
        }
        private void DrawMesh(Mesh mesh, Material material, Matrix4x4 matrix, MaterialPropertyBlock block,
            int submesh, int layer, Renderer source = null)
        {
            var parameters = new RenderParams(material)
            {
                camera = camera, layer = layer, matProps = block,
                shadowCastingMode = source != null ? source.shadowCastingMode : ShadowCastingMode.Off,
                receiveShadows = source != null && source.receiveShadows,
                lightProbeUsage = source != null ? source.lightProbeUsage : LightProbeUsage.Off,
                reflectionProbeUsage = source != null ? source.reflectionProbeUsage : ReflectionProbeUsage.Off,
                renderingLayerMask = source != null ? source.renderingLayerMask : uint.MaxValue,
                worldBounds = TransformBounds(mesh.bounds, matrix)
            };
            Graphics.RenderMesh(parameters, mesh, submesh, matrix);
        }

        // Screen -> main-camera ray -> inverse display matrix -> assembly plane.
        // Mesh submission and picking use exactly the same matrix, including zoom.
        internal bool TryGetPlaneCell(Vector2 screen, WorkshopPlanarAxis planeAxis, int planeDepth,
            out Vector3Int cell, out bool occluded)
        {
            cell = default; occluded = false;
            if (assembly == null || IsTransitioning || !camera.pixelRect.Contains(screen)) return false;
            PrepareView();
            Ray screenRay = camera.ScreenPointToRay(screen);
            Matrix4x4 inverse = AssemblyToDisplay.inverse;
            var ray = new Ray(inverse.MultiplyPoint3x4(screenRay.origin), inverse.MultiplyVector(screenRay.direction).normalized);
            Vector3 normal = WorkshopPlanarCoordinates.Normal(planeAxis);
            if (!new Plane(normal, normal * planeDepth).Raycast(ray, out float distance)) return false;
            Vector3 point = ray.GetPoint(distance);
            if (point.sqrMagnitude > 512f * 512f) return false;
            cell = Vector3Int.RoundToInt(point);
            // An empty deeper cell cannot be selected through an existing front module.
            Vector3 sourceOrigin = assembly.transform.TransformPoint(ray.origin);
            Vector3 sourcePoint = assembly.transform.TransformPoint(point);
            Vector3 delta = sourcePoint - sourceOrigin;
            var sourceRay = new Ray(sourceOrigin, delta.normalized);
            foreach (var collider in colliders)
                if (collider != null && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy &&
                    TryGetColliderDistance(collider, sourceRay, out float hit) && hit < delta.magnitude - .001f)
                { occluded = true; break; }
            return true;
        }

        internal bool TryGetBlock(Vector2 screen, out PlacedBlock block)
        {
            block = null;
            if (assembly == null || IsTransitioning || !camera.pixelRect.Contains(screen)) return false;
            PrepareView();
            var ray = camera.ScreenPointToRay(screen);
            Matrix4x4 toSource = assembly.transform.localToWorldMatrix * AssemblyToDisplay.inverse;
            var sourceRay = new Ray(toSource.MultiplyPoint3x4(ray.origin), toSource.MultiplyVector(ray.direction).normalized);
            Collider closest = null;
            float nearest = float.PositiveInfinity;
            foreach (var collider in colliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy ||
                    collider.GetComponentInParent<StickerOverlay>() != null || collider.GetComponentInParent<WeaponKeyring>() != null ||
                    !TryGetColliderDistance(collider, sourceRay, out float hit) || hit >= nearest) continue;
                closest = collider; nearest = hit;
            }
            // Include the core in the nearest-hit comparison: it shields blocks behind it,
            // even though it cannot itself be deleted. Depth UI never selects through a front block.
            if (closest == null) return false;
            block = closest.GetComponentInParent<PlacedBlock>();
            return block != null && block.transform.parent == assembly.transform;
        }

        private static bool TryGetColliderDistance(Collider collider, Ray ray, out float distance)
        {
            distance = 0f;
            if (!(collider is BoxCollider box))
            {
                if (!collider.Raycast(ray, out var result, float.PositiveInfinity)) return false;
                distance = result.distance; return true;
            }
            // Workshop cells/core use boxes. Read their current transform directly so a
            // newly placed/undone block is correct before the next physics synchronization.
            Matrix4x4 inverse = box.transform.worldToLocalMatrix;
            Vector3 direction = inverse.MultiplyVector(ray.direction);
            float length = direction.magnitude;
            if (length < .00001f) return false;
            var localRay = new Ray(inverse.MultiplyPoint3x4(ray.origin), direction / length);
            if (!new Bounds(box.center, box.size).IntersectRay(localRay, out float hit)) return false;
            distance = hit / length; return true;
        }

        internal void RefreshSource()
        {
            if (assembly == null) return;
            RestoreRenderers(); scan.Clear(); colliders.Clear();
            assembly.GetComponentsInChildren(true, scan); assembly.GetComponentsInChildren(true, colliders);
            bool first = true;
            foreach (var source in scan)
            {
                if (!source.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
                parts.Add(new Part { source = source, previousForceOff = source.forceRenderingOff });
                source.forceRenderingOff = true;
                if (!source.enabled || !source.gameObject.activeInHierarchy) continue;
                Bounds local = TransformBounds(filter.sharedMesh.bounds, assembly.transform.worldToLocalMatrix * source.localToWorldMatrix);
                if (first) { bounds = local; first = false; } else bounds.Encapsulate(local);
            }
            if (first) bounds = new Bounds(Vector3.zero, Vector3.one);
            BuildGrid();
        }
        private void BuildGrid()
        {
            int radius = Mathf.Clamp(Mathf.CeilToInt(bounds.size.magnitude) + 3, 5, 128);
            int centerU = Mathf.RoundToInt(axis == WorkshopPlanarAxis.X ? pivot.z : pivot.x);
            int centerV = Mathf.RoundToInt(axis == WorkshopPlanarAxis.Y ? pivot.z : pivot.y);
            var vertices = new List<Vector3>((radius * 2 + 2) * 4);
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
            grid.Clear(); grid.SetVertices(vertices); grid.SetIndices(indices, MeshTopology.Lines, 0); grid.RecalculateBounds();
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
        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        private static Bounds TransformBounds(Bounds value, Matrix4x4 matrix)
        {
            Vector3 x = matrix.MultiplyVector(new Vector3(value.extents.x, 0f, 0f));
            Vector3 y = matrix.MultiplyVector(new Vector3(0f, value.extents.y, 0f));
            Vector3 z = matrix.MultiplyVector(new Vector3(0f, 0f, value.extents.z));
            return new Bounds(matrix.MultiplyPoint3x4(value.center), (Abs(x) + Abs(y) + Abs(z)) * 2f);
        }
        private void RestoreRenderers()
        {
            foreach (var part in parts)
                if (part.source != null) part.source.forceRenderingOff = part.previousForceOff;
            parts.Clear();
        }
        public void Dispose()
        {
            Bind(null);
            UnityEngine.Object.Destroy(grid); UnityEngine.Object.Destroy(marker);
            UnityEngine.Object.Destroy(directionArrow);
            UnityEngine.Object.Destroy(gridMaterial); UnityEngine.Object.Destroy(markerMaterial);
        }
    }
}
