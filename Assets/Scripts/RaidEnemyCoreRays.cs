using UnityEngine;
using UnityEngine.Rendering;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent, RequireComponent(typeof(RaidEnemyCore))]
    public sealed class RaidEnemyCoreRays : MonoBehaviour
    {
        [SerializeField, Tooltip("깊이로 가림을 확인하는 빛살 재질. 길이·폭·개수·색은 이 재질에서 조절합니다.")]
        private Material rayMaterial;

        private static Mesh sharedQuad;
        private static int meshUsers;
        private RenderParams renderParams;
        private Vector3 boundsSize;
        private bool ownsMesh;

        private void OnEnable()
        {
            if (rayMaterial == null) return;
            if (sharedQuad == null)
            {
                sharedQuad = new Mesh { name = "RaidCoreRaysQuad", hideFlags = HideFlags.DontSave };
                sharedQuad.vertices = new[]
                {
                    new Vector3(-1f, -1f, 0f), new Vector3(-1f, 1f, 0f),
                    new Vector3(1f, 1f, 0f), new Vector3(1f, -1f, 0f)
                };
                sharedQuad.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                sharedQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                sharedQuad.UploadMeshData(true);
            }
            meshUsers++;
            ownsMesh = true;

            renderParams = new RenderParams(rayMaterial)
            {
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
            };
            // Keep the visual out of the core's Renderer hierarchy: burn-effect sizing and outlines must
            // continue to use the physical core, rather than this much larger camera-facing quad.
            boundsSize = Vector3.one * (12f + transform.lossyScale.magnitude * 2f);
        }

        private void LateUpdate()
        {
            if (!ownsMesh || rayMaterial == null) return;
            renderParams.material = rayMaterial;
            // Explicit world bounds also cover the shader's maximum 6m length on the flat, scaled core.
            renderParams.worldBounds = new Bounds(transform.position, boundsSize);
            Graphics.RenderMesh(renderParams, sharedQuad, 0, transform.localToWorldMatrix);
        }

        private void OnDisable()
        {
            if (!ownsMesh) return;
            ownsMesh = false;
            if (--meshUsers == 0)
            {
                Destroy(sharedQuad);
                sharedQuad = null;
            }
        }
    }
}
