using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using System.Diagnostics;

namespace WeaponWorkshop
{
    public sealed partial class DestructibleTarget
    {
        private static readonly int PenetrationBaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int PenetrationColorId = Shader.PropertyToID("_Color");
        private static readonly int PenetrationFlashUntilId = Shader.PropertyToID("_PenetrationFlashUntil");
        private MaterialPropertyBlock penetrationFlashProperties, penetrationOriginalProperties;
        private Color penetrationBaseColor, penetrationColor;
        private float penetrationFlashUntil;
        private bool penetrationFlashActive;

        private void ShowPenetrationHit()
        {
            if (originalRenderer == null || originalRenderer.sharedMaterial == null) return;
            penetrationFlashProperties ??= new MaterialPropertyBlock();
            penetrationOriginalProperties ??= new MaterialPropertyBlock();
            if (!penetrationFlashActive)
            {
                originalRenderer.GetPropertyBlock(penetrationOriginalProperties);
                originalRenderer.GetPropertyBlock(penetrationFlashProperties);
                var material = originalRenderer.sharedMaterial;
                penetrationBaseColor = penetrationOriginalProperties.HasColor(PenetrationBaseColorId)
                    ? penetrationOriginalProperties.GetColor(PenetrationBaseColorId)
                    : material.HasProperty(PenetrationBaseColorId) ? material.GetColor(PenetrationBaseColorId) : Color.white;
                penetrationColor = penetrationOriginalProperties.HasColor(PenetrationColorId)
                    ? penetrationOriginalProperties.GetColor(PenetrationColorId)
                    : material.HasProperty(PenetrationColorId) ? material.GetColor(PenetrationColorId) : penetrationBaseColor;
            }
            penetrationFlashActive = true;
            penetrationFlashUntil = Time.time + 0.12f;
            damageProperties?.SetFloat(PenetrationFlashUntilId, penetrationFlashUntil);
            UpdatePenetrationFeedback();
            RefreshUpdaterRegistration();
        }

        private void UpdatePenetrationFeedback()
        {
            if (!penetrationFlashActive) return;
            float flash = Mathf.Clamp01((penetrationFlashUntil - Time.time) / 0.12f);
            if (flash <= 0f)
            {
                ResetPenetrationFeedback();
                RefreshUpdaterRegistration();
                return;
            }
            Color tint = Color.Lerp(penetrationBaseColor, Color.white, flash);
            tint.a = penetrationBaseColor.a;
            penetrationFlashProperties.SetColor(PenetrationBaseColorId, tint);
            tint = Color.Lerp(penetrationColor, Color.white, flash);
            tint.a = penetrationColor.a;
            penetrationFlashProperties.SetColor(PenetrationColorId, tint);
            if (damageProperties != null)
            { damageProperties.SetFloat(PenetrationFlashUntilId, penetrationFlashUntil); originalRenderer.SetPropertyBlock(damageProperties); }
            else originalRenderer.SetPropertyBlock(penetrationFlashProperties);
        }

        private void ResetPenetrationFeedback()
        {
            damageProperties?.SetFloat(PenetrationFlashUntilId, 0f);
            if (penetrationFlashActive && originalRenderer != null)
                originalRenderer.SetPropertyBlock(damageProperties ?? penetrationOriginalProperties);
            penetrationFlashActive = false;
        }

        // Cache bounded meshes per target; a narrow hole only rebuilds nearby chunks.
        private const int RenderChunkWidth = 8;
        private sealed class RenderChunk
        {
            public Mesh mesh;
            public bool dirty = true;
            // 드로 대상인지는 재구축 때만 갱신한다. 매 프레임 메시 정점수를 네이티브로 조회하지 않는다.
            public bool hasGeometry;
            // 격자에서 나오는 청크 경계(로컬). 청크가 비어도 유효해서 컬링 판정에 그대로 쓴다.
            public Vector3 localMin, localMax;
            // 표적·청크의 월드 경계. 드로 행렬이 그대로면 프레임마다 다시 만들지 않는다.
            public Vector3 worldCenter, worldExtents;
            // 청크 경계가 표적 경계와 같으면 표적 판정이 곧 청크 판정이라 두 번째 검사를 하지 않는다.
            public bool spansTargetBounds;
            public CombineInstance[] combines = System.Array.Empty<CombineInstance>();
        }
        private readonly Dictionary<Vector3Int, RenderChunk> renderChunks = new Dictionary<Vector3Int, RenderChunk>();
        // 매 프레임 돌리는 드로 리스트. Dictionary.Values는 접근마다 래퍼를 만들어 프레임 할당이 남는다.
        private readonly List<RenderChunk> chunkDrawList = new List<RenderChunk>();

        // ==== 화면 밖 청크 드로 컬링 ====
        // Graphics.DrawMesh가 남은 청크 수만큼 그대로 나와 파괴가 진행될수록 드로가 1,000호대로 늘어난다.
        // 절두체 평면은 프레임당 한 번 만들어 모든 표적이 공유하고 청크 경계는 격자에서 한 번만 구해,
        // 화면에 걸리지 않는 드로는 발행 자체를 막는다.
        private static readonly ProfilerMarker chunkDrawMarker = new ProfilerMarker("DestructibleTarget.DrawChunkMeshes");
        private const int ChunkHidden = 0;
        private const int ChunkDrawn = 1;
        private const int ChunkShadowOnly = 2;
        // 그림자를 켠 점·스포트 광원 확인은 화면 밖 청크가 나왔을 때만, 이 간격으로 한다.
        private const float ShadowProbeInterval = 0.5f;
        private static readonly List<ChunkFrustum> chunkFrustums = new List<ChunkFrustum>(4);
        private static readonly Plane[] frustumPlaneScratch = new Plane[6];
        private static Camera[] chunkCullCameras = System.Array.Empty<Camera>();
        private static int chunkCullFrame = -1;
        // 표적 단위 고정비. 드로 행렬이 그대로면 월드 경계와 레이어를 다시 읽지 않는다.
        private Matrix4x4 chunkBoundsMatrix;
        private Vector3 chunkWorldCenter, chunkWorldExtents;
        private int chunkDrawLayer = int.MinValue;
        private int chunkBoundsCount = -1;
        // 투영 행렬이 망가져 절두체가 이상해지면 드로가 통째로 사라진다. 카메라가 자기 절두체를
        // 스스로 통과하는지 확인하고 아니하면 이번 프레임은 컬링을 켜지 않는다.
        private static bool chunkCullingTrusted = true;
        // 화면 밖 청크도 이 거리 안이면 그림자를 남길 수 있어 드로한다. 0이면 그림자 걱정이 없는 상태.
        private static float shadowCasterReach;
        private static float shadowProbeTime = float.NegativeInfinity;

        private sealed class ChunkFrustum
        {
            public Camera camera;
            // 법선·상수는 프레임당 한 번만 뽑아 두고 청크마다 평구조자를 다시 읽지 않는다.
            public readonly Vector4[] planes = new Vector4[6];
            // |법선|. 박스 반지름 계산에서 평면마다 절댓값을 다시 구하지 않게 미리 둔다.
            public readonly Vector3[] absNormals = new Vector3[6];
            public Vector3 position;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetChunkCulling()
        {
            chunkFrustums.Clear();
            chunkCullCameras = System.Array.Empty<Camera>();
            chunkCullFrame = -1;
            chunkCullingTrusted = true;
            shadowCasterReach = 0f;
            shadowProbeTime = float.NegativeInfinity;
        }
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
            Vector3Int key = RenderKey(coordinate);
            if (renderChunks.TryGetValue(key, out var chunk)) chunk.dirty = true;
            if (!SurfaceRendering) return;
            // 노출 면 방식은 칸 하나가 사라지면 맞닿던 이웃 칸의 면도 새로 드러난다.
            // 이웃이 같은 청크에 있으면 자기 청크 재구축에 포함되므로 청크 경계만 추가로 검사한다.
            for (int i = 0; i < neighbors.Length; i++)
            {
                Vector3Int neighborKey = RenderKey(coordinate + neighbors[i]);
                if (neighborKey == key) continue;
                if (renderChunks.TryGetValue(neighborKey, out var neighborChunk)) neighborChunk.dirty = true;
            }
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
                        if (SurfaceRendering) { RebuildSurfaceChunk(pair.Key, chunk); CacheChunkBounds(pair.Key, chunk); chunk.dirty = false; continue; }
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
                                    {
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
                        CacheChunkBounds(pair.Key, chunk);
                        chunk.dirty = false;
                    }
                    renderDirty = false;
                    // 청크 경계가 새로 정해졌으니 월드 경계도 다시 만들어야 한다.
                    chunkBoundsCount = -1;
                }
            }
            // 청크 사전은 만들 때 한 번만 채우므로 개수가 어긋난 프레임에만 리스트를 다시 채운다.
            if (chunkDrawList.Count != renderChunks.Count)
            {
                chunkDrawList.Clear();
                chunkDrawList.AddRange(renderChunks.Values);
            }
            Matrix4x4 matrix = CellRoot.localToWorldMatrix;
            PrepareChunkCulling();
            RefreshChunkWorldBounds(matrix);
            // 표적 전체가 모든 카메라 바깥이면 청크를 하나씩 볼 필요조차 없다.
            int targetVisibility = BoxVisibility(chunkWorldCenter, chunkWorldExtents);
            if (targetVisibility == ChunkHidden) return;
            MaterialPropertyBlock drawProperties = CurrentDamageProperties;
            using (chunkDrawMarker.Auto())
            {
                for (int i = 0; i < chunkDrawList.Count; i++)
                {
                    RenderChunk chunk = chunkDrawList[i];
                    if (!chunk.hasGeometry) continue;
                    int visibility = targetVisibility;
                    // 표적이 화면 안에 걸렸을 때만 청크 단위로 다시 잘라 본다. 경계가 표적과 같으면 판정도 같다.
                    if (targetVisibility == ChunkDrawn && !chunk.spansTargetBounds)
                    {
                        visibility = BoxVisibility(chunk.worldCenter, chunk.worldExtents);
                        if (visibility == ChunkHidden) continue;
                    }
                    // 화면 밖이지만 그림자를 남길 수 있는 청크는 그림자 패스만 태워 화면 결과를 유지한다.
                    Graphics.DrawMesh(chunk.mesh, matrix, instancedMaterial, chunkDrawLayer, null, 0, drawProperties,
                        visibility == ChunkShadowOnly ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                            : UnityEngine.Rendering.ShadowCastingMode.On, true);
                }
            }
        }

        // 표적과 청크의 월드 AABB·드로 레이어는 드로 행렬이 바뀔 때만 다시 만든다.
        // 정적 파츠는 프레임마다 행렬 견줌 비교만 하고, 컬링 판정만 남는다.
        private void RefreshChunkWorldBounds(Matrix4x4 matrix)
        {
            if (chunkBoundsCount == chunkDrawList.Count && HasSameWorldTransform(chunkBoundsMatrix, matrix)) return;
            chunkBoundsMatrix = matrix;
            chunkBoundsCount = chunkDrawList.Count;
            WorldBoxBounds(matrix, gridOrigin, gridOrigin + Vector3.Scale((Vector3)divisions, gridStep),
                out chunkWorldCenter, out chunkWorldExtents);
            for (int i = 0; i < chunkDrawList.Count; i++)
            {
                RenderChunk chunk = chunkDrawList[i];
                if (chunk.spansTargetBounds) continue;
                WorldBoxBounds(matrix, chunk.localMin, chunk.localMax, out chunk.worldCenter, out chunk.worldExtents);
            }
            // 드로 레이어는 표적 생성 이후 바뀌지 않는다. 재구축·이동처럼 경계를 다시 만드는 시점에만 읽는다.
            chunkDrawLayer = targetMesh.gameObject.layer;
        }

        private static bool HasSameWorldTransform(Matrix4x4 a, Matrix4x4 b) =>
            a.m00 == b.m00 && a.m01 == b.m01 && a.m02 == b.m02 && a.m03 == b.m03 &&
            a.m10 == b.m10 && a.m11 == b.m11 && a.m12 == b.m12 && a.m13 == b.m13 &&
            a.m20 == b.m20 && a.m21 == b.m21 && a.m22 == b.m22 && a.m23 == b.m23;

        // 프레임당 한 번: 이 프레임에 나오는 카메라의 절두체 평면을 한 번씩 만든다.
        private static void PrepareChunkCulling()
        {
            if (chunkCullFrame == Time.frameCount) return;
            chunkCullFrame = Time.frameCount;
            chunkCullingTrusted = true;
            // 그림자 도달 거리는 박스마다 확인할 게 아니라 프레임당 한 번만 갱신한다.
            ProbeShadowReach();
            // 꺼진 카메라는 목록에 나오지 않는다. 배열은 개수가 바뀔 때만 새로 만든다.
            int cameraCount = Camera.allCamerasCount;
            if (chunkCullCameras.Length != cameraCount) chunkCullCameras = new Camera[cameraCount];
            if (cameraCount > 0) Camera.GetAllCameras(chunkCullCameras);
            for (int i = chunkFrustums.Count - 1; i >= 0; i--)
                if (chunkFrustums[i].camera == null || !chunkFrustums[i].camera.isActiveAndEnabled) chunkFrustums.RemoveAt(i);
            for (int c = 0; c < chunkCullCameras.Length; c++)
            {
                Camera camera = chunkCullCameras[c];
                if (camera == null) continue;
                ChunkFrustum frustum = null;
                for (int i = 0; i < chunkFrustums.Count; i++)
                    if (chunkFrustums[i].camera == camera) { frustum = chunkFrustums[i]; break; }
                if (frustum == null) { frustum = new ChunkFrustum { camera = camera }; chunkFrustums.Add(frustum); }
                GeometryUtility.CalculateFrustumPlanes(camera, frustumPlaneScratch);
                // 상수는 GetDistanceToPoint(0,0,0)으로 받아 부호 규칙 없이 쓴다(절두체 안쪽이 +).
                for (int p = 0; p < frustum.planes.Length; p++)
                {
                    Vector3 normal = frustumPlaneScratch[p].normal;
                    frustum.planes[p] = new Vector4(normal.x, normal.y, normal.z,
                        frustumPlaneScratch[p].GetDistanceToPoint(Vector3.zero));
                    frustum.absNormals[p] = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                }
                frustum.position = camera.transform.position;
                // 카메라 바로 앞 축 위 점은 절두체 안에 있다. 투영 행렬이 이상하면 이 판정이 깨진다.
                Vector3 insidePoint = frustum.position + camera.transform.forward * (camera.nearClipPlane + 0.01f);
                for (int p = 0; p < frustum.planes.Length; p++)
                {
                    Vector4 plane = frustum.planes[p];
                    if (plane.x * insidePoint.x + plane.y * insidePoint.y + plane.z * insidePoint.z + plane.w < 0f)
                    {
                        chunkCullingTrusted = false;
                        break;
                    }
                }
            }
        }

        // 화면 밖 캐스터도 화면 안에 그림자를 드리울 수 있다. 방향광 그림자가 켜져 있으면 거리와 무관하게
        // 화면 밖 청크를 그림자 전용으로 남기고(그림자 컬링은 URP가 한다), 점·스포트 광원은 수신면이 그림자
        // 거리 안에서만 그림자를 받으므로 광원 범위를 두 번 더 본다. 그림자 광원이 없으면 화면 밖 청크를 지운다.
        // 확인을 놓친 최대 한 주기(0.5초) 동안만 새 그림자 광원의 그림자가 늦게 붙을 수 있다.
        private static void ProbeShadowReach()
        {
            if (Time.unscaledTime - shadowProbeTime < ShadowProbeInterval) return;
            shadowProbeTime = Time.unscaledTime;
            // URP는 품질 설정과 따로 그림자 거리를 정한다. 큰 값 쪽으로 보수적으로 잡는다.
            float shadowDistance = QualitySettings.shadowDistance;
            UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset pipeline =
                UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset;
            if (pipeline != null) shadowDistance = Mathf.Max(shadowDistance, pipeline.shadowDistance);
            shadowCasterReach = 0f;
            if (shadowDistance <= 0f) return;
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (!light.isActiveAndEnabled || light.shadows == LightShadows.None) continue;
                if (light.type == LightType.Directional) { shadowCasterReach = float.MaxValue; return; }
                if (light.type != LightType.Point && light.type != LightType.Spot) continue;
                shadowCasterReach = Mathf.Max(shadowCasterReach, shadowDistance + 2f * light.range);
            }
        }

        // 카메라 중 하나라도 박스를 보면 드로, 화면에는 없지만 그림자 도달 거리 안이면 그림자만 드로,
        // 모두 아니면 건너뛴다. 판정할 카메라가 없으면 드로하는 쪽으로 잡는다.
        private static int BoxVisibility(Vector3 center, Vector3 extents)
        {
            if (!chunkCullingTrusted || chunkFrustums.Count == 0) return ChunkDrawn;
            for (int i = 0; i < chunkFrustums.Count; i++)
                if (InsideFrustum(chunkFrustums[i].planes, chunkFrustums[i].absNormals, center, extents)) return ChunkDrawn;
            // 방향광 그림자가 켜진 상태는 거리와 무관하게 그림자가 남으므로 거리를 재지 않는다.
            if (shadowCasterReach <= 0f) return ChunkHidden;
            if (shadowCasterReach == float.MaxValue) return ChunkShadowOnly;
            for (int i = 0; i < chunkFrustums.Count; i++)
                if (NearPoint(center, extents, chunkFrustums[i].position, shadowCasterReach)) return ChunkShadowOnly;
            return ChunkHidden;
        }

        // 평면 법선 방향으로 가장 먼저 닿는 꼭짓점 하나만 보는 AABB-평면 판정. GeometryUtility.TestPlanesAABB와
        // 같은 판정을 관리 코드에서 하므로 거리 부호가 절두체 안쪽이다. 법선 절댓값은 미리 만들어 둔 값을 쓴다.
        private static bool InsideFrustum(Vector4[] planes, Vector3[] absNormals, Vector3 center, Vector3 extents)
        {
            for (int i = 0; i < planes.Length; i++)
            {
                Vector4 plane = planes[i];
                Vector3 absNormal = absNormals[i];
                float radius = absNormal.x * extents.x + absNormal.y * extents.y + absNormal.z * extents.z;
                if (plane.x * center.x + plane.y * center.y + plane.z * center.z + plane.w < -radius) return false;
            }
            return true;
        }

        // AABB와 점에서 거리가 reach 안쪽인지 제곱으로 비교한다.
        private static bool NearPoint(Vector3 center, Vector3 extents, Vector3 point, float reach)
        {
            float dx = Mathf.Max(Mathf.Abs(point.x - center.x) - extents.x, 0f);
            float dy = Mathf.Max(Mathf.Abs(point.y - center.y) - extents.y, 0f);
            float dz = Mathf.Max(Mathf.Abs(point.z - center.z) - extents.z, 0f);
            return dx * dx + dy * dy + dz * dz <= reach * reach;
        }

        // 로컬 박스를 회전·스케일까지 덮어 쓰는 world AABB로 옮긴다.
        private static void WorldBoxBounds(Matrix4x4 matrix, Vector3 min, Vector3 max, out Vector3 center, out Vector3 extents)
        {
            Vector3 localCenter = (min + max) * 0.5f;
            Vector3 localExtent = (max - min) * 0.5f;
            center = matrix.MultiplyPoint3x4(localCenter);
            extents = new Vector3(
                Mathf.Abs(matrix.m00) * localExtent.x + Mathf.Abs(matrix.m01) * localExtent.y + Mathf.Abs(matrix.m02) * localExtent.z,
                Mathf.Abs(matrix.m10) * localExtent.x + Mathf.Abs(matrix.m11) * localExtent.y + Mathf.Abs(matrix.m12) * localExtent.z,
                Mathf.Abs(matrix.m20) * localExtent.x + Mathf.Abs(matrix.m21) * localExtent.y + Mathf.Abs(matrix.m22) * localExtent.z);
        }

        // 청크 경계는 격자에서 바로 나온다. 메시 바운즈와 달리 청크가 비어도 유효해 재구축 때 한 번만 계산한다.
        private void CacheChunkBounds(Vector3Int key, RenderChunk chunk)
        {
            Vector3Int origin = key * RenderChunkWidth;
            Vector3Int finish = Vector3Int.Min(origin + Vector3Int.one * RenderChunkWidth, divisions);
            chunk.localMin = gridOrigin + Vector3.Scale((Vector3)origin, gridStep);
            chunk.localMax = gridOrigin + Vector3.Scale((Vector3)finish, gridStep);
            // 전체 격자를 하나로 덮는 청크는 표적 박스와 같은 박스다. 드로에서 컬링 판정을 한 번으로 끝낸다.
            chunk.spansTargetBounds = origin == Vector3Int.zero && finish == divisions;
            chunk.hasGeometry = chunk.mesh != null && chunk.mesh.vertexCount > 0;
        }

        private Material damageMaterial, undamagedMaterial;
        private static Material damageTemplate;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrepareDamageShader()
        {
            damageTemplate = Resources.Load<Material>("DestructibleDamageLit");
            Resources.Load<ShaderVariantCollection>("DestructibleDamageVariants")?.WarmUp();
        }
        private MaterialPropertyBlock damageProperties;
        private sealed class DamagePage
        {
            public Vector3Int coordinate;
            public int slot;
            public Vector2[] cells;
            public bool dirty;
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct DamagePageEntry
        {
            public int x, y, z, page;
        }
        // Only pages with damage exist. The GPU index is a sparse hash table, not a
        // volume-sized lookup, so a large untouched building allocates no damage storage.
        private readonly Dictionary<Vector3Int, DamagePage> damagePages = new();
        private readonly List<DamagePage> damagePageOrder = new();
        private readonly List<DamagePage> dirtyDamagePages = new();
        private GraphicsBuffer damageCellBuffer, damageIndexBuffer;
        private DamagePageEntry[] damagePageIndex = System.Array.Empty<DamagePageEntry>();
        private Vector3Int damageChunkSize;
        private bool damageIndexDirty, damageDataDirty;
        private int DamagePageCellCount => damageChunkSize.x * damageChunkSize.y * damageChunkSize.z;
        private Color damageTint = Color.black, hitFlashColor = Color.white;
        private float hitFlashDuration = .12f, damageTintStrength = .45f;
        private static readonly int DamageCellsId = Shader.PropertyToID("_DamageCells");
        private static readonly int DamageIndexId = Shader.PropertyToID("_DamagePageIndex");
        private static readonly int DamageIndexMaskId = Shader.PropertyToID("_DamageIndexMask");
        private static readonly int DamageChunkSizeId = Shader.PropertyToID("_DamageChunkSize");
        private static readonly int DamageOriginId = Shader.PropertyToID("_DamageOrigin");
        private static readonly int DamageStepId = Shader.PropertyToID("_DamageStep");
        private static readonly int DamageDimensionsId = Shader.PropertyToID("_DamageDimensions");
        private static readonly int DamageTintId = Shader.PropertyToID("_DamageTint");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashDurationId = Shader.PropertyToID("_FlashDuration");
        private static readonly int DamageStrengthId = Shader.PropertyToID("_DamageStrength");
        private MaterialPropertyBlock CurrentDamageProperties => damageProperties ?? (penetrationFlashActive ? penetrationFlashProperties : null);

        private void ConfigureDamageAppearance(ProjectileTuning tuning)
        {
            if (tuning == null) return;
            damageTint = tuning.explosionDamageTint; hitFlashColor = tuning.explosionHitFlashColor;
            hitFlashDuration = Mathf.Max(.01f, tuning.explosionHitFlashDuration);
            damageTintStrength = Mathf.Clamp01(tuning.explosionDamageTintStrength);
        }

        private void SetCellDamageAppearance(Cell cell)
        {
            if (damageProperties == null)
            {
                // The same forward pass reads only allocated damage pages, including
                // while the target still uses its intact, unsplit mesh.
                var template = damageTemplate != null ? damageTemplate : Resources.Load<Material>("DestructibleDamageLit");
                if (template == null) throw new System.InvalidOperationException("DestructibleDamageLit material is missing.");
                undamagedMaterial = originalRenderer.sharedMaterial;
                damageMaterial = new Material(template);
                damageMaterial.CopyPropertiesFromMaterial(undamagedMaterial);
                damageChunkSize = Vector3Int.Min(divisions, Vector3Int.one * RenderChunkWidth);
                damageProperties = new MaterialPropertyBlock();
                if (penetrationFlashActive) originalRenderer.SetPropertyBlock(penetrationOriginalProperties);
                originalRenderer.GetPropertyBlock(damageProperties);
                damageProperties.SetVector(DamageChunkSizeId, (Vector3)damageChunkSize);
                damageProperties.SetVector(DamageOriginId, gridOrigin);
                damageProperties.SetVector(DamageStepId, gridStep);
                damageProperties.SetVector(DamageDimensionsId, (Vector3)divisions);
                originalRenderer.sharedMaterial = damageMaterial;
                if (instancedMaterial != null) { Destroy(instancedMaterial); instancedMaterial = new Material(damageMaterial); }
            }
            var coordinate = cell.coordinate;
            var key = new Vector3Int(coordinate.x / damageChunkSize.x,
                coordinate.y / damageChunkSize.y, coordinate.z / damageChunkSize.z);
            if (!damagePages.TryGetValue(key, out var page))
            {
                page = new DamagePage { coordinate = key, slot = damagePageOrder.Count,
                    cells = new Vector2[DamagePageCellCount] };
                damagePages.Add(key, page);
                damagePageOrder.Add(page);
                damageIndexDirty = true;
            }
            var local = coordinate - Vector3Int.Scale(key, damageChunkSize);
            page.cells[local.x + damageChunkSize.x * (local.y + damageChunkSize.y * local.z)] =
                new Vector2(Mathf.Clamp01((float)(1d - cell.explosionHealth)), cell.explosionHitTime);
            MarkDamagePageDirty(page);
        }

        private void MarkDamagePageDirty(DamagePage page)
        {
            if (!page.dirty) { page.dirty = true; dirtyDamagePages.Add(page); }
            if (!damageDataDirty) { damageDataDirty = true; RefreshUpdaterRegistration(); }
        }

        private static uint DamagePageHash(Vector3Int key)
        {
            unchecked { return (uint)key.x * 73856093u ^ (uint)key.y * 19349663u ^ (uint)key.z * 83492791u; }
        }

        private void UploadDamageAppearance()
        {
            if (!damageDataDirty || damageProperties == null) return;
            int pageCells = DamagePageCellCount;
            int requiredCells = Mathf.NextPowerOfTwo(damagePageOrder.Count) * pageCells;
            bool grew = damageCellBuffer == null || damageCellBuffer.count < requiredCells;
            if (grew)
            {
                damageCellBuffer?.Release();
                damageCellBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, requiredCells, sizeof(float) * 2);
                damageProperties.SetBuffer(DamageCellsId, damageCellBuffer);
                // Capacity doubles only as new hit regions appear. Re-upload existing
                // allocated pages on growth; never allocate/upload untouched target cells.
                foreach (var page in damagePageOrder)
                    damageCellBuffer.SetData(page.cells, 0, page.slot * pageCells, pageCells);
            }
            else
                foreach (var page in dirtyDamagePages)
                    damageCellBuffer.SetData(page.cells, 0, page.slot * pageCells, pageCells);

            if (damageIndexDirty)
            {
                int indexSize = Mathf.NextPowerOfTwo(Mathf.Max(4, damagePageOrder.Count * 2));
                if (damagePageIndex.Length != indexSize)
                {
                    damagePageIndex = new DamagePageEntry[indexSize];
                    damageIndexBuffer?.Release();
                    damageIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, indexSize, sizeof(int) * 4);
                    damageProperties.SetBuffer(DamageIndexId, damageIndexBuffer);
                    damageProperties.SetFloat(DamageIndexMaskId, indexSize - 1);
                }
                for (int i = 0; i < indexSize; i++) damagePageIndex[i].page = -1;
                int mask = indexSize - 1;
                foreach (var page in damagePageOrder)
                {
                    var key = page.coordinate;
                    int bucket = (int)(DamagePageHash(key) & (uint)mask);
                    while (damagePageIndex[bucket].page >= 0) bucket = (bucket + 1) & mask;
                    damagePageIndex[bucket] = new DamagePageEntry { x = key.x, y = key.y, z = key.z, page = page.slot };
                }
                damageIndexBuffer.SetData(damagePageIndex);
                damageIndexDirty = false;
            }
            damageProperties.SetColor(DamageTintId, damageTint);
            damageProperties.SetColor(FlashColorId, hitFlashColor);
            damageProperties.SetFloat(FlashDurationId, hitFlashDuration);
            damageProperties.SetFloat(DamageStrengthId, damageTintStrength);
            damageProperties.SetFloat(PenetrationFlashUntilId, penetrationFlashActive ? penetrationFlashUntil : 0f);
            originalRenderer.SetPropertyBlock(damageProperties);
            foreach (var page in dirtyDamagePages) page.dirty = false;
            dirtyDamagePages.Clear();
            damageDataDirty = false;
        }

        private void ResetDamageAppearance()
        {
            foreach (var page in damagePageOrder)
            {
                System.Array.Clear(page.cells, 0, page.cells.Length);
                MarkDamagePageDirty(page);
            }
        }

        private void DisposeDamageAppearance()
        {
            if (originalRenderer != null && undamagedMaterial != null) originalRenderer.sharedMaterial = undamagedMaterial;
            if (damageMaterial != null) Destroy(damageMaterial);
            damageCellBuffer?.Release(); damageCellBuffer = null;
            damageIndexBuffer?.Release(); damageIndexBuffer = null;
            damagePages.Clear(); damagePageOrder.Clear(); dirtyDamagePages.Clear();
        }

        private readonly HashSet<Vector3Int> changedBoundary = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> localSupportVisited = new HashSet<Vector3Int>();
        private readonly Queue<Vector3Int> localSupportQueue = new Queue<Vector3Int>();
        private readonly List<int> localSupportGroup = new List<int>();
        private bool supportValidated, resolvingSupportDrop;
        // 국소 붕괴 판정은 프레임 예산을 넘기면 여기까지 본 상태로 멈추고 다음 프레임에 이어서 한다.
        // 같은 프레임에 ResolveSupport가 여러 번 불려도(중력 흡수 종료 + LateUpdate) 예산과 예약을
        // 공유하고 방문 기록을 호출마다 비우지 않으므로 같은 컴포넌트를 다시 훑지 않는다.
        private bool localSupportInProgress;
        private bool localSupportSupported;
        private Vector3Int localSupportSeed;
        private int localSuspendSeedCount;
        private int localSupportBudgetFrame = -1, localSupportBudgetLeft;
        private Quaternion localSupportRootRotation;
        // 프레임당 탐사 칸 수 예산. 지연 상한은 3프레임으로, 예약이 시작된 뒤 3프레임이 지나면
        // 예산을 무시하고 남은 판정을 한 번에 마쳐 잃은 지지가 그 이상 늦게 떨어지지 않게 한다.
        private const int LocalSupportVisitBudget = 8192;
        private const int LocalSupportMaxDelayFrames = 3;
        // 시든 시드 거르는 조건자를 호출마다 새로 만들지 않게 하나만 캐시한다.
        private System.Predicate<Vector3Int> staleBoundaryFilter;
        private int supportPendingSinceFrame = -1;
        private static readonly ProfilerMarker localSupportMarker = new ProfilerMarker("DestructibleTarget.ResolveSupport.Local");
        private static readonly ProfilerMarker supportFallbackMarker = new ProfilerMarker("DestructibleTarget.ResolveSupport.FullFallback");
        private void RecordSupportRemoval(Vector3Int coordinate)
        {
            if (!supportValidated || resolvingSupportDrop) return;
            // 국소 판정이 진행 중이면 파괴 하나만으로도 방문 기록이 낡아진다(리셋으로 칸이
            // 되살아나는 경우까지 포함). 기록을 버려 다시 탐색하지 않으면 끊긴 조각을 다시는
            // 검사하지 않게 되므로 여기서는 정확성을 위해 보수적으로 초기화한다.
            if (localSupportInProgress || localSupportVisited.Count > 0) ResetLocalSupportProgress();
            foreach (var direction in neighbors) changedBoundary.Add(coordinate + direction);
        }
        // 멈춰 둔 국소 탐색 상태를 버린다. 탐색 중이던 컴포넌트는 시작 시드를 예약 목록에 되돌려
        // 다음 호출이 현재 상태에서 처음부터 다시 판정하게 한다.
        private void ResetLocalSupportProgress()
        {
            if (!localSupportInProgress && localSupportVisited.Count == 0) return;
            if (localSupportInProgress)
            {
                changedBoundary.Add(localSupportSeed);
                localSupportInProgress = false;
            }
            localSupportQueue.Clear();
            localSupportVisited.Clear();
            localSupportGroup.Clear();
        }
        private void ResolveSupport()
        {
            if (attachedToMovingBody) return;
            using (supportMarker.Auto())
            {
                if (!supportValidated)
                {
                    using (supportFallbackMarker.Auto()) DropUnsupported();
                    // 전체 재판정 뒤에는 국소 예약·방문 기록이 무의미하다.
                    ResetLocalSupportProgress();
                    changedBoundary.Clear();
                    supportPendingSinceFrame = -1;
                }
                else if (changedBoundary.Count > 0 || localSupportInProgress)
                    using (localSupportMarker.Auto()) ResolveLocalSupportBudgeted();
                supportValidated = true;
            }
        }
        // 바뀐 경계에 붙은 연결 조각을 프레임 예산 안에서 검사한다. 컴포넌트는 판정이 "통과" 또는
        // "통째로 낙하" 둘 중 하나로만 끝나므로 탐색을 여러 프레임에 나눠 봐도 한 번에 다 본 것과
        // 붕괴 결과가 같다. 지연 상한(LocalSupportMaxDelayFrames) 안에는 반드시 마친다.
        private void ResolveLocalSupportBudgeted()
        {
            if (localSupportBudgetFrame != Time.frameCount)
            {
                localSupportBudgetFrame = Time.frameCount;
                localSupportBudgetLeft = LocalSupportVisitBudget;
            }
            bool finishNow = supportPendingSinceFrame >= 0 &&
                Time.frameCount - supportPendingSinceFrame >= LocalSupportMaxDelayFrames;
            if (staleBoundaryFilter == null) staleBoundaryFilter = coordinate => !HasCell(coordinate);
            changedBoundary.RemoveWhere(staleBoundaryFilter);
            // 멈춰 둔 탐색이 있을 때 새 시드가 끼어들었거나(이웃 파츠 지지 변화 등) 뿌리가 돌았으면
            // 방문 기록이 낡아진다. 기록을 비우고 다시 본다.
            if ((localSupportInProgress || localSupportVisited.Count > 0) &&
                (changedBoundary.Count != localSuspendSeedCount || CellRoot.rotation != localSupportRootRotation))
                ResetLocalSupportProgress();
            if (!(localSupportInProgress || changedBoundary.Count > 0))
            {
                // 남은 것이 시든 시드뿐이었다. 예약을 닫는다.
                localSupportVisited.Clear();
                localSuspendSeedCount = 0;
                supportPendingSinceFrame = -1;
                return;
            }
            if (!finishNow && localSupportBudgetLeft <= 0)
            {
                // 같은 프레임의 두 번째 호출(중력 흡수 종료 뒤 LateUpdate)은 예산을 새로 쓰지 않는다.
                SuspendLocalSupport();
                return;
            }
            int visits = finishNow ? int.MaxValue : localSupportBudgetLeft;

            CollectExternalSupport();
            externallyHeld = false;
            foreach (int supportedIndex in externallySupportedCells)
                if (supportedIndex >= 0 && supportedIndex < grid.Length && grid[supportedIndex])
                { externallyHeld = true; break; }

            while (true)
            {
                if (!localSupportInProgress)
                {
                    if (changedBoundary.Count == 0) break;
                    foreach (Vector3Int candidate in changedBoundary) { localSupportSeed = candidate; break; }
                    changedBoundary.Remove(localSupportSeed);
                    localSupportGroup.Clear();
                    localSupportSupported = false;
                    if (TryGetCell(localSupportSeed, out _) && localSupportVisited.Add(localSupportSeed))
                    {
                        localSupportQueue.Clear();
                        localSupportQueue.Enqueue(localSupportSeed);
                        localSupportInProgress = true;
                    }
                    // 이미 다른 시드로 다 본 컴포넌트면 판정을 다시 내지 않는다(기존 경로와 같다).
                    continue;
                }
                while (localSupportQueue.Count > 0)
                {
                    if (visits-- == 0)
                    {
                        localSupportBudgetLeft = 0;
                        SuspendLocalSupport();
                        return;
                    }
                    Vector3Int coordinate = localSupportQueue.Dequeue();
                    if (!TryGetCell(coordinate, out Cell cell)) continue;
                    int cellIndex = CellIndex(coordinate);
                    localSupportGroup.Add(cellIndex);
                    if (CellBottom(cell) <= transform.position.y + 0.001f || HasExternalSupport(cellIndex)) localSupportSupported = true;
                    foreach (Vector3Int direction in neighbors)
                    {
                        Vector3Int next = coordinate + direction;
                        if (TryGetCell(next, out _) && localSupportVisited.Add(next)) localSupportQueue.Enqueue(next);
                    }
                }
                // 컴포넌트 하나를 끝까지 봤다. 지지를 잃었으면 그 컴포넌트만 통째로 떨어뜨린다.
                localSupportInProgress = false;
                if (!localSupportSupported && localSupportGroup.Count > 0)
                {
                    groupPieces.Clear();
                    foreach (int cellIndex in localSupportGroup) groupPieces.Add(preparedCells[cellIndex].Snapshot);
                    EnsureDebrisCloud();
                    debrisCloud.EmitGroup(groupPieces, debrisLifetime, debrisFadeDuration);
                    DebrisEmissionBudget.NoteGroup(groupPieces.Count, debrisLifetime);
                    resolvingSupportDrop = true;
                    try
                    {
                        foreach (int cellIndex in localSupportGroup) RemoveCell(cellIndex);
                    }
                    finally { resolvingSupportDrop = false; }
                }
                if (cells.Count == 0) externallyHeld = false;
                if (visits == 0 && changedBoundary.Count > 0)
                {
                    localSupportBudgetLeft = 0;
                    SuspendLocalSupport();
                    return;
                }
            }
            // 예약을 모두 처리했다. 방문 기록은 다음 일괄 때 낡아지므로 비워 둔다.
            localSupportVisited.Clear();
            localSuspendSeedCount = 0;
            supportPendingSinceFrame = -1;
        }
        // 예산을 다 썼다. 이어하기 상태(방문 기록·큐·시드·남은 예약)를 보존하고 예약이 남아 있는
        // 동안 표적이 갱신 대상에서 빠지지 않게 structureDirty를 되살린다.
        private void SuspendLocalSupport()
        {
            localSuspendSeedCount = changedBoundary.Count;
            localSupportRootRotation = CellRoot.rotation;
            if (supportPendingSinceFrame < 0) supportPendingSinceFrame = Time.frameCount;
            MarkStructureDirty();
        }
    }
}
