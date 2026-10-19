using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace WeaponWorkshop
{
    // VisualDebrisCloud 갱신을 매니저 한 곳의 Update(1단계)·LateUpdate(2단계)로 모은다.
    // 클라우드 개수만큼 각자 Update/LateUpdate가 돌지 않는다. 등록은 OnEnable, 해제는 OnDisable에서 하며
    // 매니저는 씬 배치 없이 첫 등록 때 만든다.
    // 폭발이 겹치는 프레임에 모든 구름이 매번 전체 계산을 하면 Tick이 프레임 상위 비용이 되므로,
    // 먼 구름은 프레임을 건너 계산하고 프레임당 계산 시간 예산을 넘기면 남은 구름을 다음 프레임으로 미룬다(공정 순환).
    // 양보한 프레임의 시간은 구름에 누적해 다음 계산에 한꺼번에 더하므로 파편 움직임 속도는 변하지 않는다.
    // 파편 적분은 Burst Job 으로 옮겨졌다. 1단계에서 구름별 PrepareTick 으로 Job 을 모두 스케줄하고,
    // 2단계에서 FinishTick 이 완료를 기다린 뒤 벽 검사·제출만 메인 스레드에서 한다.
    // Job 을 LateUpdate 가 아니라 Update 뒤쪽에서 스케줄하므로 적분은 물리·다른 LateUpdate 와 겹쳐 돌고
    // 2단계는 남은 시간만 기다린다. Job 이 살아 있는 동안 구름 파편 버퍼를 고치는 경로는 먼저 완료한다.
    [DefaultExecutionOrder(10000)]
    internal sealed class VisualDebrisCloudManager : MonoBehaviour
    {
        // 기존 VisualDebrisCloud.LateUpdate 샘플을 한 자리에서 보인다.
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("VisualDebrisCloud.Tick");
        private static readonly ProfilerMarker FlushMarker = new ProfilerMarker("VisualDebrisCloud.Flush");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static VisualDebrisCloudManager instance;
        private static readonly List<VisualDebrisCloud> clouds = new List<VisualDebrisCloud>(16);

        // 프레임당 파편 구름 계산 예산(ms). 폭발 창에서 구름이 많을 때 계산 합이 프레임을 잠식하지 않게 한다.
        private const float TickBudgetMilliseconds = 4f;
        // 이 거리(m) 밖 구름은 프레임을 건너 계산한다. 구름 내부의 48m 밖 격프레임 갱신과 합쳐 4프레임 주기가 된다.
        private const float StaggeredUpdateDistanceSq = 96f * 96f;
        private int tickCursor;  // 다음 프레임에 계산 우선권을 받을 자리.
        private readonly System.Diagnostics.Stopwatch tickWatch = new System.Diagnostics.Stopwatch();
        private readonly List<VisualDebrisCloud> scheduledClouds = new List<VisualDebrisCloud>(16);

        // ==== 그리기 통합 싱크 ====
        // 구름은 표적 머티리얼 복사본 대신 매니저가 캐시한 공유 인스턴스를 쓴다. 같은 원본 머티리얼·색·
        // 메시·레이어를 쓰는 구름끼리 버킷 행렬을 한데 모아 프레임 끝에 버킵별로 한 번만 DrawMeshInstanced 한다.
        // 구름마다 알파 버킷을 따로 그리던 호출 수가 키 수 × 버킷 수로 줄어든다.
        internal readonly struct FragmentDrawKey : System.IEquatable<FragmentDrawKey>
        {
            public readonly Material Template;  // 파편 머티리얼 원본. 캐시 인스턴스는 이 키로 찾는다.
            public readonly Mesh Mesh;
            public readonly Color Color;
            public readonly int Layer;
            public FragmentDrawKey(Material template, Mesh mesh, Color color, int layer)
            {
                Template = template; Mesh = mesh; Color = color; Layer = layer;
            }
            public bool Equals(FragmentDrawKey other) => Template == other.Template && Mesh == other.Mesh &&
                Layer == other.Layer && Color.r == other.Color.r && Color.g == other.Color.g &&
                Color.b == other.Color.b && Color.a == other.Color.a;
            public override int GetHashCode() => unchecked(
                ((Template != null ? Template.GetInstanceID() : 0) * 486187739 ^
                 (Mesh != null ? Mesh.GetInstanceID() : 0) * 486187739 ^ Layer) * 486187739 ^
                Color.r.GetHashCode() ^ (Color.g.GetHashCode() << 2) ^
                (Color.b.GetHashCode() >> 2) ^ (Color.a.GetHashCode() << 4));
        }

        // 싱크 한 칸: 키 하나의 버킷별 행렬 버퍼. 풀에서 재사용해 정상 프레임에는 할당이 없다.
        private sealed class FragmentBatch
        {
            public FragmentDrawKey key;
            public Material instance;  // 키 원본에 맞춰 캐시한 공유 머티리얼 인스턴스.
            public readonly Matrix4x4[][] buckets = new Matrix4x4[DebrisIntegrationJob.AlphaBucketSteps + 1][];
            public readonly int[] counts = new int[DebrisIntegrationJob.AlphaBucketSteps + 1];

            public void ClearCounts()
            {
                for (int i = 0; i < counts.Length; i++) counts[i] = 0;
                instance = null;
            }
        }

        private static readonly List<FragmentBatch> frameBatches = new List<FragmentBatch>(8);       // 이번 프레임 키 배치(순서 보존)
        private static readonly Dictionary<FragmentDrawKey, FragmentBatch> frameBatchByKey = new Dictionary<FragmentDrawKey, FragmentBatch>(8);
        private static readonly List<FragmentBatch> batchPool = new List<FragmentBatch>(8);
        private static readonly Dictionary<Material, Material> fragmentMaterialCache = new Dictionary<Material, Material>(8);
        private static readonly Dictionary<Material, Material> groupMaterialCache = new Dictionary<Material, Material>(8);
        private static readonly List<Material> ownedMaterials = new List<Material>(16);  // 매니저가 만든 공유 인스턴스. 해제 주체는 매니저다.
        private static readonly Matrix4x4[] flushBatch = new Matrix4x4[1023];            // 1023개 배치 한도를 유지하기 위한 잘라내기 버퍼.
        private static readonly MaterialPropertyBlock flushBlock = new MaterialPropertyBlock();

        // 도메인 리로드 꺼짐·씬 전환 뒤에도 이전 세션의 클라우드를 순회하지 않도록 정적 목록을 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            clouds.Clear();
            // 정적 캐시 머티리얼은 여기서 만든 것이므로 함께 거둔다.
            for (int i = 0; i < ownedMaterials.Count; i++)
                if (ownedMaterials[i] != null) Object.Destroy(ownedMaterials[i]);
            ownedMaterials.Clear();
            fragmentMaterialCache.Clear();
            groupMaterialCache.Clear();
            frameBatches.Clear();
            frameBatchByKey.Clear();
            batchPool.Clear();
        }

        internal static void Register(VisualDebrisCloud cloud)
        {
            if (!Application.isPlaying || cloud == null || cloud.managerIndex >= 0) return;
            GetOrCreate();
            cloud.managerIndex = clouds.Count;
            clouds.Add(cloud);
        }

        internal static void Unregister(VisualDebrisCloud cloud)
        {
            if (cloud == null) return;
            int index = cloud.managerIndex;
            cloud.managerIndex = -1;
            if (index < 0 || index >= clouds.Count) return;
            // 목록이 초기화돼 자리 정보가 어긋났으면 이 클라우드의 자리는 이미 없다.
            if (clouds[index] != cloud) return;
            RemoveAtSwap(index);
        }

        private static void RemoveAtSwap(int index)
        {
            int last = clouds.Count - 1;
            if (index != last)
            {
                VisualDebrisCloud moved = clouds[last];
                clouds[index] = moved;
                if (moved != null) moved.managerIndex = index;
            }
            clouds.RemoveAt(last);
        }

        private static VisualDebrisCloudManager GetOrCreate()
        {
            if (instance != null) return instance;
            GameObject managerObject = new GameObject("VisualDebrisCloudManager");
            DontDestroyOnLoad(managerObject);
            instance = managerObject.AddComponent<VisualDebrisCloudManager>();
            return instance;
        }

        private void OnDisable()
        {
            if (instance == this) instance = null;
        }

        // 표적 머티리얼 복사본 대신 쓰는 공유 파편 머티리얼(투명·인스턴싱). 설정은 기존 구름 복사본과 같다.
        internal static Material ConfigureFragmentMaterial(Material material)
        {
            material.enableInstancing = true;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        // 덩어리(연결 낙하 그룹)가 쓰는 공유 불투명 머티리얼. 설정은 기존 구름 복사본과 같다.
        internal static Material ConfigureGroupMaterial(Material material)
        {
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry;
            return material;
        }

        // 같은 원본을 쓰는 구름이 하나의 인스턴스를 나눠 쓰게 캐시한다. 공유돼야 그리기가 키 하나로 묶인다.
        internal static Material GetFragmentMaterial(Material template)
        {
            if (template == null) return null;
            if (!Application.isPlaying) return ConfigureFragmentMaterial(new Material(template));
            GetOrCreate();
            if (fragmentMaterialCache.TryGetValue(template, out Material cached) && cached != null) return cached;
            Material material = ConfigureFragmentMaterial(new Material(template));
            fragmentMaterialCache[template] = material;
            ownedMaterials.Add(material);
            return material;
        }

        internal static Material GetGroupMaterial(Material source)
        {
            if (source == null) return null;
            if (!Application.isPlaying) return ConfigureGroupMaterial(new Material(source));
            GetOrCreate();
            if (groupMaterialCache.TryGetValue(source, out Material cached) && cached != null) return cached;
            Material material = ConfigureGroupMaterial(new Material(source));
            groupMaterialCache[source] = material;
            ownedMaterials.Add(material);
            return material;
        }

        // 구름이 이번 프레임 버킷 행렬을 키 배치에 이어 붙인다. 프레임 끝의 FlushDraws 가 한 번에 그린다.
        internal static void AddFragmentMatrices(in FragmentDrawKey key, int bucket, Matrix4x4[] matrices, int count)
        {
            if (count <= 0 || matrices == null || bucket <= 0 || bucket >= DebrisIntegrationJob.AlphaBucketSteps + 1) return;
            if (!frameBatchByKey.TryGetValue(key, out FragmentBatch batch))
            {
                if (batchPool.Count > 0)
                {
                    batch = batchPool[batchPool.Count - 1];
                    batchPool.RemoveAt(batchPool.Count - 1);
                }
                else batch = new FragmentBatch();
                batch.key = key;
                batch.instance = GetFragmentMaterial(key.Template);
                frameBatchByKey.Add(key, batch);
                frameBatches.Add(batch);
            }
            Matrix4x4[] target = batch.buckets[bucket];
            int needed = batch.counts[bucket] + count;
            if (target == null) target = new Matrix4x4[Mathf.Max(256, needed)];
            else if (target.Length < needed) System.Array.Resize(ref target, Mathf.Max(target.Length * 2, needed));
            System.Array.Copy(matrices, 0, target, batch.counts[bucket], count);
            batch.buckets[bucket] = target;
            batch.counts[bucket] = needed;
        }

        // 모아 둔 행렬을 키·버킷별로 한 번씩 그린다. 색 페이드는 기존과 같이 버킷 지수로 나눈다.
        private static void FlushDraws()
        {
            for (int b = 0; b < frameBatches.Count; b++)
            {
                FragmentBatch batch = frameBatches[b];
                for (int bucket = 1; bucket < batch.buckets.Length; bucket++)
                {
                    int count = batch.counts[bucket];
                    if (count == 0) continue;
                    Color faded = batch.key.Color;
                    faded.a *= bucket / (float)DebrisIntegrationJob.AlphaBucketSteps;
                    flushBlock.SetColor(BaseColorId, faded);
                    Matrix4x4[] matrices = batch.buckets[bucket];
                    // 최대 배치 크기를 넘는 버킷만 잘라 여러 번 그린다.
                    for (int start = 0; start < count; start += flushBatch.Length)
                    {
                        int batchCount = Mathf.Min(flushBatch.Length, count - start);
                        System.Array.Copy(matrices, start, flushBatch, 0, batchCount);
                        Graphics.DrawMeshInstanced(batch.key.Mesh, 0, batch.instance, flushBatch, batchCount,
                            flushBlock, ShadowCastingMode.Off, false, batch.key.Layer);
                    }
                }
                batch.ClearCounts();
                batchPool.Add(batch);
            }
            frameBatches.Clear();
            frameBatchByKey.Clear();
        }

        // 1단계: 구름별 준비와 Job 스케줄. 캐시 경로(정지·격프레임·예산 양보)는 여기서 싱크에 제출된다.
        private void Update()
        {
            // 구름이 사라진 뒤에도 이전 프레임 목록이 남아 2단계에서 다시 완료되지 않게 한다.
            scheduledClouds.Clear();
            if (clouds.Count == 0) return;
            using (TickMarker.Auto())
            {
                // 시선·시간·중력은 프레임당 한 번만 읽는다.
                Camera view = Camera.main;
                float dt = Time.deltaTime;
                Vector3 gravity = Physics.gravity;
                Vector3 viewPosition = view != null ? view.transform.position : Vector3.zero;
                int frame = Time.frameCount;
                float budgetLeft = TickBudgetMilliseconds;
                int firstDeferred = -1;  // 예산을 넘겨 처음 양보한 자리. 다음 프레임은 여기부터 다시 시작한다.
                int count = clouds.Count;
                if (tickCursor >= count) tickCursor = 0;
                for (int n = 0; n < count; n++)
                {
                    // 순회 중 등록·해제로 목록이 밀리면 자리를 현재 크기로 감싸 되돌린다.
                    // 두 번 읽힌 구름은 tickFrame 스탬프로 걸러진다.
                    if (clouds.Count == 0) break;
                    int i = (tickCursor + n) % clouds.Count;
                    VisualDebrisCloud cloud = clouds[i];
                    if (cloud == null) { RemoveAtSwap(i); continue; }
                    if (cloud.tickFrame == frame) continue;

                    // 먼 구름은 프레임을 건너 계산하고, 예산이 끝난 뒤의 구름은 다음 프레임으로 미룬다.
                    // 둘 다 직전 그리기 결과만 다시 제출해 화면에서 사라지지 않게 한다. 카메라가 없으면 원래대로 매번 계산한다.
                    bool deferred = false;
                    if (view != null)
                    {
                        bool far = (cloud.FocusPosition - viewPosition).sqrMagnitude > StaggeredUpdateDistanceSq;
                        if (far && ((frame + i) & 1) != 0) deferred = cloud.TrySubmitCache();
                        else if (budgetLeft <= 0f) deferred = cloud.TrySubmitCache();
                    }
                    if (deferred)
                    {
                        cloud.pendingDeltaTime += dt;
                        cloud.tickFrame = frame;
                        if (firstDeferred < 0) firstDeferred = i;
                        continue;
                    }

                    float combined = dt + cloud.pendingDeltaTime;
                    cloud.pendingDeltaTime = 0f;
                    cloud.tickFrame = frame;
                    tickWatch.Restart();
                    bool scheduled = cloud.PrepareTick(view, combined, gravity);
                    tickWatch.Stop();
                    budgetLeft -= (float)tickWatch.Elapsed.TotalMilliseconds;
                    if (scheduled) scheduledClouds.Add(cloud);
                }
                tickCursor = firstDeferred >= 0 ? firstDeferred : tickCursor + 1;
            }
        }

        // 2단계: Job 완료 + 벽 검사 + 싱크 제출. Update 에서 스케줄된 Job 은 물리·다른 LateUpdate 와
        // 겹쳐 돌았으므로 여기서 기다리는 시간은 그만큼 줄어든다.
        private void LateUpdate()
        {
            if (scheduledClouds.Count > 0)
            {
                using (TickMarker.Auto())
                {
                    float budgetLeft = TickBudgetMilliseconds;
                    // 완료 비용도 같은 예산에서 뺀다. 양보 판정이 모두 끝난 뒤에 잡히므로 다음 프레임 양보에 반영된다.
                    for (int s = 0; s < scheduledClouds.Count; s++)
                    {
                        tickWatch.Restart();
                        scheduledClouds[s].FinishTick();
                        tickWatch.Stop();
                        budgetLeft -= (float)tickWatch.Elapsed.TotalMilliseconds;
                    }
                }
            }
            // 프레임 끝에 키·버킷별로 모아 둔 행렬을 한 번에 그린다. 1단계 캐시 경로 제출도 여기에 모인다.
            if (frameBatches.Count > 0)
            {
                using (FlushMarker.Auto()) FlushDraws();
            }
        }
    }
}
