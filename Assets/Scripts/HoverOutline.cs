using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(Camera)), DefaultExecutionOrder(100)]
    public sealed class HoverOutline : MonoBehaviour
    {
        [SerializeField] private Material maskMaterial;
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private Color outlineColor = new Color(1f, 0.75f, 0.15f, 1f);
        [SerializeField, Range(1f, 10f)] private float outlineWidth = 3f;
        private readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        // 32 source meshes x 8 offsets = 256 instances, below the shader's batch limit.
        private const int BatchSize = 32;
        private readonly Dictionary<(Mesh mesh, int layer), List<Matrix4x4>> batches =
            new Dictionary<(Mesh mesh, int layer), List<Matrix4x4>>();
        private readonly List<(Mesh mesh, int layer)> unusedBatches = new List<(Mesh mesh, int layer)>();
        private readonly Matrix4x4[] maskMatrices = new Matrix4x4[BatchSize];
        private readonly Matrix4x4[] outlineMatrices = new Matrix4x4[BatchSize * 8];
        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OffsetId = Shader.PropertyToID("_OutlineOffset");
        private static readonly Vector2[] OutlineOffsets =
        {
            Vector2.right, Vector2.left, Vector2.up, Vector2.down,
            new Vector2(1f, 1f).normalized, new Vector2(-1f, 1f).normalized,
            new Vector2(1f, -1f).normalized, new Vector2(-1f, -1f).normalized
        };
        private Camera view;
        private MaterialPropertyBlock properties;
        private MaterialPropertyBlock instancedProperties;
        private Transform batchTarget;
        public Transform Target { get; private set; }
        private bool targetAvailable;

        private void Awake()
        {
            view = GetComponent<Camera>();
            InitializeProperties();
        }

        private void OnEnable()
        {
            if (view == null) view = GetComponent<Camera>();
            InitializeProperties();
        }

        private void InitializeProperties()
        {
            if (properties != null) return;
            properties = new MaterialPropertyBlock();
            instancedProperties = new MaterialPropertyBlock();
            var offsets = new Vector4[BatchSize * OutlineOffsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 offset = OutlineOffsets[i % OutlineOffsets.Length];
                offsets[i] = new Vector4(offset.x, offset.y, 0f, 0f);
            }
            instancedProperties.SetVectorArray(OffsetId, offsets);
        }

        public void SetTarget(IInteractable target, bool available = true)
        {
            Target = target?.OutlineRoot;
            targetAvailable = available;
        }
        private void OnDisable()
        {
            Target = batchTarget = null;
            batches.Clear();
            renderers.Clear();
            unusedBatches.Clear();
        }

        private void LateUpdate()
        {
            // Interaction briefly clears Target during each raycast. Compare only the final target here.
            if (batchTarget != Target || Target == null)
            {
                batches.Clear();
                renderers.Clear();
                batchTarget = Target;
            }
            if (Target == null || maskMaterial == null || outlineMaterial == null) return;
            foreach (var matrices in batches.Values) matrices.Clear();
            // Refresh live geometry so assembly changes, destruction and active-state changes remain immediate.
            Target.GetComponentsInChildren<MeshRenderer>(false, renderers);
            foreach (MeshRenderer renderer in renderers)
            {
                if (!renderer.enabled || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var key = (filter.sharedMesh, renderer.gameObject.layer);
                if (!batches.TryGetValue(key, out var matrices))
                {
                    matrices = new List<Matrix4x4>();
                    batches.Add(key, matrices);
                }
                matrices.Add(renderer.localToWorldMatrix);
            }

            bool instanced = SystemInfo.supportsInstancing && maskMaterial.enableInstancing && outlineMaterial.enableInstancing;
            MaterialPropertyBlock drawProperties = instanced ? instancedProperties : properties;
            drawProperties.SetColor(ColorId, targetAvailable ? outlineColor : Color.white);
            drawProperties.SetFloat(WidthId, outlineWidth);
            unusedBatches.Clear();
            foreach (var batch in batches)
            {
                if (batch.Value.Count == 0) { unusedBatches.Add(batch.Key); continue; }
                if (instanced) DrawInstanced(batch.Key.mesh, batch.Key.layer, batch.Value);
                else DrawIndividual(batch.Key.mesh, batch.Key.layer, batch.Value);
            }
            // Do not retain meshes replaced by destruction while the same target stays hovered.
            foreach (var key in unusedBatches) batches.Remove(key);
            unusedBatches.Clear();
        }

        private void DrawInstanced(Mesh mesh, int layer, List<Matrix4x4> matrices)
        {
            int submeshCount = mesh.subMeshCount;
            for (int start = 0; start < matrices.Count; start += BatchSize)
            {
                int count = Mathf.Min(BatchSize, matrices.Count - start);
                for (int i = 0; i < count; i++)
                {
                    Matrix4x4 matrix = matrices[start + i];
                    maskMatrices[i] = matrix;
                    for (int offset = 0; offset < OutlineOffsets.Length; offset++)
                        outlineMatrices[i * OutlineOffsets.Length + offset] = matrix;
                }
                for (int submesh = 0; submesh < submeshCount; submesh++)
                {
                    Graphics.DrawMeshInstanced(mesh, submesh, maskMaterial, maskMatrices, count, null,
                        ShadowCastingMode.Off, false, layer, view, LightProbeUsage.Off);
                    Graphics.DrawMeshInstanced(mesh, submesh, outlineMaterial, outlineMatrices,
                        count * OutlineOffsets.Length, instancedProperties,
                        ShadowCastingMode.Off, false, layer, view, LightProbeUsage.Off);
                }
            }
        }

        private void DrawIndividual(Mesh mesh, int layer, List<Matrix4x4> matrices)
        {
            foreach (Matrix4x4 matrix in matrices)
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    Graphics.DrawMesh(mesh, matrix, maskMaterial, layer, view, submesh, null,
                        ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
                    foreach (Vector2 offset in OutlineOffsets)
                    {
                        properties.SetVector(OffsetId, new Vector4(offset.x, offset.y, 0f, 0f));
                        Graphics.DrawMesh(mesh, matrix, outlineMaterial, layer, view, submesh, properties,
                            ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
                    }
                }
        }

    }
}
