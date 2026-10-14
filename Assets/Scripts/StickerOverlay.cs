using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class StickerOverlay : MonoBehaviour
    {
        public const float DefaultMaximumDimension = 1.275f;
        public const float DefaultSizeMultiplier = 1f;
        public const float MinimumSizeMultiplier = 0.25f;
        public const float MaximumSizeMultiplier = 2f;
        public const float SizeStep = 0.25f;
        private const float SurfacePlaneTolerance = 0.003f;
        private const float MinimumClippedSize = 0.0001f;

        private Mesh stickerMesh;
        private Material stickerMaterial;
        private MeshFilter meshFilter;
        private Texture2D sourceTexture;
        private readonly List<BoxCollider> stickerColliders = new List<BoxCollider>();
        private float sizeMultiplier = DefaultSizeMultiplier;
        private bool flipHorizontally;

        public float SizeMultiplier => sizeMultiplier;
        public bool IsFlippedHorizontally => flipHorizontally;
        public bool CanDecreaseSize => sizeMultiplier > MinimumSizeMultiplier;
        public bool CanIncreaseSize => sizeMultiplier < MaximumSizeMultiplier;

        public bool Configure(Texture2D texture, float initialSizeMultiplier = DefaultSizeMultiplier,
            bool flipHorizontally = false)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0 ||
                initialSizeMultiplier < MinimumSizeMultiplier || initialSizeMultiplier > MaximumSizeMultiplier) return false;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) return false;

            sourceTexture = texture;
            sizeMultiplier = initialSizeMultiplier;
            this.flipHorizontally = flipHorizontally;
            if (!TryCreateMesh(out Mesh newMesh, out List<Rect> surfaceRects)) return false;

            stickerMesh = newMesh;
            stickerMaterial = CreateMaterial(shader, texture);
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = stickerMesh;
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = stickerMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            UpdateColliders(surfaceRects);
            return true;
        }

        public bool ToggleHorizontalFlip()
        {
            flipHorizontally = !flipHorizontally;
            if (RebuildSurfaceGeometry()) return true;
            flipHorizontally = !flipHorizontally;
            return false;
        }

        public bool AdjustSize(int direction)
        {
            if (direction == 0) return false;
            float nextSize = Mathf.Clamp(sizeMultiplier + Mathf.Sign(direction) * SizeStep,
                MinimumSizeMultiplier, MaximumSizeMultiplier);
            if (Mathf.Approximately(nextSize, sizeMultiplier)) return false;

            float previousSize = sizeMultiplier;
            sizeMultiplier = nextSize;
            if (RebuildSurfaceGeometry()) return true;
            sizeMultiplier = previousSize;
            return false;
        }

        private bool RebuildSurfaceGeometry()
        {
            if (!TryCreateMesh(out Mesh newMesh, out List<Rect> surfaceRects)) return false;

            Mesh oldMesh = stickerMesh;
            stickerMesh = newMesh;
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            meshFilter.sharedMesh = stickerMesh;
            UpdateColliders(surfaceRects);
            if (oldMesh != null)
            {
                if (Application.isPlaying) Destroy(oldMesh);
                else DestroyImmediate(oldMesh);
            }
            return true;
        }

        private bool TryCreateMesh(out Mesh mesh, out List<Rect> surfaceRects)
        {
            mesh = null;
            surfaceRects = null;
            if (sourceTexture == null) return false;

            float maximumDimension = DefaultMaximumDimension * sizeMultiplier;
            float aspect = (float)sourceTexture.width / sourceTexture.height;
            float width = aspect >= 1f ? maximumDimension : maximumDimension * aspect;
            float height = aspect >= 1f ? maximumDimension / aspect : maximumDimension;
            Rect stickerBounds = new Rect(-width * 0.5f, -height * 0.5f, width, height);
            if (!TryGetContinuousSurfaceRects(stickerBounds, out surfaceRects)) return false;

            mesh = CreateMesh(width, height, surfaceRects, flipHorizontally);
            return true;
        }

        private void UpdateColliders(List<Rect> surfaceRects)
        {
            while (stickerColliders.Count > surfaceRects.Count)
            {
                int last = stickerColliders.Count - 1;
                BoxCollider collider = stickerColliders[last];
                stickerColliders.RemoveAt(last);
                if (collider == null) continue;
                collider.enabled = false;
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }

            while (stickerColliders.Count < surfaceRects.Count)
                stickerColliders.Add(gameObject.AddComponent<BoxCollider>());

            for (int i = 0; i < surfaceRects.Count; i++)
            {
                Rect surfaceRect = surfaceRects[i];
                BoxCollider collider = stickerColliders[i];
                collider.center = new Vector3(surfaceRect.center.x, surfaceRect.center.y, 0f);
                collider.size = new Vector3(surfaceRect.width, surfaceRect.height, 0.002f);
                collider.enabled = true;
            }
        }

        private bool TryGetContinuousSurfaceRects(Rect stickerBounds, out List<Rect> result)
        {
            result = new List<Rect>();
            PlacedBlock selectedBlock = GetComponentInParent<PlacedBlock>();
            CubeAssembly assembly = selectedBlock != null
                ? selectedBlock.GetComponentInParent<CubeAssembly>()
                : null;
            if (assembly == null) return false;

            Vector3 stickerNormal = transform.forward.normalized;
            foreach (PlacedBlock block in assembly.GetComponentsInChildren<PlacedBlock>())
            {
                if (block == null || !block.gameObject.activeInHierarchy) continue;
                BoxCollider moduleCollider = block.GetComponent<BoxCollider>();
                if (moduleCollider == null || !moduleCollider.enabled) continue;

                Vector3 halfSize = moduleCollider.size * 0.5f;
                for (int axis = 0; axis < 3; axis++)
                {
                    Vector3 axisDirection = GetAxisDirection(axis);
                    Vector3 tangentU = axis == 0 ? Vector3.up : axis == 1 ? Vector3.forward : Vector3.right;
                    Vector3 tangentV = axis == 0 ? Vector3.forward : axis == 1 ? Vector3.right : Vector3.up;
                    float axisHalfSize = GetAxisValue(halfSize, axis);
                    float halfU = GetAxisValue(halfSize, axis == 0 ? 1 : axis == 1 ? 2 : 0);
                    float halfV = GetAxisValue(halfSize, axis == 0 ? 2 : axis == 1 ? 0 : 1);

                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 faceNormal = block.transform.TransformDirection(axisDirection * side).normalized;
                        if (Vector3.Dot(faceNormal, stickerNormal) < 0.999f) continue;

                        Vector3 faceCenter = moduleCollider.center + axisDirection * (axisHalfSize * side);
                        Vector3 worldFaceCenter = block.transform.TransformPoint(faceCenter);
                        float distanceFromStickerPlane = Mathf.Abs(
                            Vector3.Dot(worldFaceCenter - transform.position, stickerNormal));
                        if (distanceFromStickerPlane > SurfacePlaneTolerance) continue;

                        float minX = float.PositiveInfinity;
                        float maxX = float.NegativeInfinity;
                        float minY = float.PositiveInfinity;
                        float maxY = float.NegativeInfinity;
                        for (int corner = 0; corner < 4; corner++)
                        {
                            float signU = (corner & 1) == 0 ? -1f : 1f;
                            float signV = (corner & 2) == 0 ? -1f : 1f;
                            Vector3 faceCorner = faceCenter + tangentU * (halfU * signU) + tangentV * (halfV * signV);
                            Vector3 stickerLocal = transform.InverseTransformPoint(block.transform.TransformPoint(faceCorner));
                            minX = Mathf.Min(minX, stickerLocal.x);
                            maxX = Mathf.Max(maxX, stickerLocal.x);
                            minY = Mathf.Min(minY, stickerLocal.y);
                            maxY = Mathf.Max(maxY, stickerLocal.y);
                        }

                        Rect clippedRect = Intersect(stickerBounds, Rect.MinMaxRect(minX, minY, maxX, maxY));
                        if (clippedRect.width > MinimumClippedSize && clippedRect.height > MinimumClippedSize)
                            result.Add(clippedRect);
                    }
                }
            }

            return result.Count > 0;
        }

        private static Vector3 GetAxisDirection(int axis)
            => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        private static float GetAxisValue(Vector3 value, int axis)
            => axis == 0 ? value.x : axis == 1 ? value.y : value.z;

        private static Rect Intersect(Rect first, Rect second)
        {
            float minX = Mathf.Max(first.xMin, second.xMin);
            float minY = Mathf.Max(first.yMin, second.yMin);
            float maxX = Mathf.Min(first.xMax, second.xMax);
            float maxY = Mathf.Min(first.yMax, second.yMax);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static Mesh CreateMesh(float fullWidth, float fullHeight, List<Rect> clippedRects,
            bool flipHorizontally)
        {
            var mesh = new Mesh { name = "StickerQuad" };
            var vertices = new List<Vector3>(clippedRects.Count * 4);
            var uvs = new List<Vector2>(clippedRects.Count * 4);
            var triangles = new List<int>(clippedRects.Count * 6);
            foreach (Rect clippedRect in clippedRects)
            {
                float normalizedUMin = Mathf.Clamp01((clippedRect.xMin + fullWidth * 0.5f) / fullWidth);
                float normalizedUMax = Mathf.Clamp01((clippedRect.xMax + fullWidth * 0.5f) / fullWidth);
                float uAtMinX = flipHorizontally ? normalizedUMin : 1f - normalizedUMin;
                float uAtMaxX = flipHorizontally ? normalizedUMax : 1f - normalizedUMax;
                float vMin = Mathf.Clamp01((clippedRect.yMin + fullHeight * 0.5f) / fullHeight);
                float vMax = Mathf.Clamp01((clippedRect.yMax + fullHeight * 0.5f) / fullHeight);
                int start = vertices.Count;
                vertices.Add(new Vector3(clippedRect.xMin, clippedRect.yMin, 0f));
                vertices.Add(new Vector3(clippedRect.xMax, clippedRect.yMin, 0f));
                vertices.Add(new Vector3(clippedRect.xMax, clippedRect.yMax, 0f));
                vertices.Add(new Vector3(clippedRect.xMin, clippedRect.yMax, 0f));
                // Reverse U in the sticker's shared coordinate space so split faces remain one image.
                uvs.Add(new Vector2(uAtMinX, vMin));
                uvs.Add(new Vector2(uAtMaxX, vMin));
                uvs.Add(new Vector2(uAtMaxX, vMax));
                uvs.Add(new Vector2(uAtMinX, vMax));
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.forward;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateMaterial(Shader shader, Texture2D texture)
        {
            var material = new Material(shader) { name = "Sticker - " + texture.name };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private void OnDestroy()
        {
            if (stickerMesh != null)
            {
                if (Application.isPlaying) Destroy(stickerMesh);
                else DestroyImmediate(stickerMesh);
            }
            if (stickerMaterial != null)
            {
                if (Application.isPlaying) Destroy(stickerMaterial);
                else DestroyImmediate(stickerMaterial);
            }
        }
    }
}
