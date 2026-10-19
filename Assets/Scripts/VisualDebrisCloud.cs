using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace WeaponWorkshop
{
    // 파편 이동·벽·그리기 갱신은 VisualDebrisCloudManager 의 단일 LateUpdate 가 부른다.
    public sealed class VisualDebrisCloud : MonoBehaviour
    {
        private static readonly ProfilerMarker ReserveBuffersMarker = new ProfilerMarker("VisualDebrisCloud.ReserveBuffers");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private struct Fragment
        {
            public Vector3 position, velocity, scale, spin;
            public Vector3 wallPosition;
            public float wallElapsed;
            public Quaternion rotation;
            public float age, lifetime, fade;
            public bool grounded;
            public bool absorbing;
            public bool resting;            // 착지·무속도·무회전. 수명 종료까지 자세가 그대로다.
            public bool restedDistant;      // 멀리서 정지한 파편. 가까워지면 근거리 물리로 복귀해 납작 정렬을 이어간다.
            public Matrix4x4 cachedMatrix;  // resting 동안 재계산 없이 다시 쓰는 최종 행렬.
            public bool hasCachedMatrix;    // cachedMatrix가 유효한지. 먼 파편의 생략 프레임이 같은 행렬을 다시 쓴다.
            public Vector3 absorptionStart, absorptionCenter, spreadPosition;
            public float spreadFraction;
            public float distantElapsed;
            public uint visualId;
        }
        private readonly List<Fragment> fragments = new List<Fragment>();
        private sealed class FallingGroup
        {
            public Vector3 position, velocity, spin;
            public Quaternion rotation = Quaternion.identity;
            public readonly List<Fragment> parts = new List<Fragment>();
            public Mesh combinedMesh;
            public bool meshDirty = true;
            public CombineInstance[] combines = System.Array.Empty<CombineInstance>();
            public Matrix4x4 drawMatrix;    // 마지막에 그린 변환. 먼 구름의 생략 프레임 다시 그리기용.
        }
        private readonly List<FallingGroup> groups = new List<FallingGroup>();
        private readonly Stack<FallingGroup> groupCache = new Stack<FallingGroup>();
        private int maximumCachedGroups = 8;
        private DestructibleTarget owner;
        internal int managerIndex = -1;  // 매니저 순회 목록의 자리. -1은 미등록.
        internal int tickFrame = -1;     // 프레임당 한 번만 갱신하기 위한 스탬프.
        internal float pendingDeltaTime; // 매니저가 계산을 양보한 프레임의 시간 누적. 다음 Tick에 한꺼번에 더한다.
        public void BindOwner(DestructibleTarget value) => owner = value;
        public void ReserveBuffers(int initialFragmentCapacity, int maximumGroups)
        {
            using (ReserveBuffersMarker.Auto())
            {
                int initialCapacity = Mathf.Clamp(initialFragmentCapacity, 0, 64);
                if (fragments.Capacity < initialCapacity) fragments.Capacity = initialCapacity;
                if (wallSelected.Length < initialCapacity) System.Array.Resize(ref wallSelected, initialCapacity);
                maximumCachedGroups = Mathf.Max(0, maximumGroups);
                for (int i = 0; i < bucketMatrices.Length; i++)
                    if (bucketMatrices[i] == null) bucketMatrices[i] = new Matrix4x4[Mathf.Max(16, initialCapacity)];
            }
        }
        private FallingGroup RentGroup()
        {
            var group = groupCache.Count > 0 ? groupCache.Pop() : new FallingGroup();
            if (group.combinedMesh == null)
                group.combinedMesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "ConnectedTargetChunk" };
            group.position = group.velocity = group.spin = Vector3.zero;
            group.rotation = Quaternion.identity; group.meshDirty = true; group.parts.Clear();
            return group;
        }
        private void ReturnGroup(FallingGroup group)
        {
            group.parts.Clear();
            if (group.combinedMesh != null) group.combinedMesh.Clear();
            if (groupCache.Count < maximumCachedGroups) groupCache.Push(group);
            else if (group.combinedMesh != null) Destroy(group.combinedMesh);
        }
        // 매니저가 거리 등급 판정에 쓰는 기준점과 거리. 구름은 표적 자리에서 유지된다.
        internal Vector3 FocusPosition => owner != null ? owner.transform.position : transform.position;
        internal float FullDetailDistance => fullDetailDistance;
        // 매니저가 계산을 양보한 프레임에 직전 그리기 결과(그룹·버킷 행렬)만 다시 제출한다. 아직 계산한 적이 없으면 false.
        internal bool TrySubmitCache()
        {
            if (!drawCached || material == null) return false;
            RedrawCache();
            return true;
        }
        public int IntactGroupCount => groups.Count;
        public int IntactCellCount
        {
            get
            {
                int count = 0;
                foreach (FallingGroup group in groups) count += group.parts.Count;
                return count;
            }
        }
        private bool absorbEmission;
        private Vector3 emissionCenter;
        private float emissionDuration;
        private float emissionSpreadDistance;
        private float emissionSpreadFraction;
        public void SetAbsorptionEmission(bool active, Vector3 center, float duration, float spreadDistance = 0.35f, float spreadFraction = 0.25f)
        {
            absorbEmission = active;
            emissionCenter = center;
            emissionDuration = Mathf.Max(0.1f, duration);
            emissionSpreadDistance = Mathf.Max(0f, spreadDistance);
            emissionSpreadFraction = Mathf.Clamp(spreadFraction, 0.05f, 0.8f);
        }

        public void ClearDebris()
        {
            fragments.Clear();
            foreach (FallingGroup group in groups) ReturnGroup(group);
            groups.Clear();
            System.Array.Clear(bucketCounts, 0, bucketCounts.Length);
            drawCached = false; pausedTime = 0f; settled = false; settledElapsed = 0f;
        }
        // 알파 버킷 행렬은 List 대신 배열+카운트로 재사용한다. 그리기만 하는 프레임은 비우거나 채우지 않는다.
        private readonly Matrix4x4[][] bucketMatrices = new Matrix4x4[16][];
        private readonly int[] bucketCounts = new int[16];
        private readonly Matrix4x4[] batch = new Matrix4x4[1023];
        private MaterialPropertyBlock appearance;
        private Material material;
        private Material groupMaterial;
        private Mesh mesh;
        private Color color;
        private float floorHeight, floorBounce, floorFriction;
        private float rollingFriction = 3f, bounceMultiplier = 0.4f, motionMultiplier = 0.65f, wallSearchRadius = 12f;
        [SerializeField, Min(0f)] private float fullDetailDistance = 6f;
        [SerializeField, Min(0)] private int maximumDistantDraws = 256;
        [SerializeField, Min(0f)] private float reducedUpdateDistance = 48f; // 이 거리 밖 구름은 프레임 건너 재계산한다.
        private float pausedTime;   // 생략 프레임의 시간. 다음 재계산에 더해 흐름을 보존한다.
        private bool drawCached;    // 직전 프레임의 버킷·그룹 그리기 결과가 다시 그리기 가능한 상태인지.
        private int drawParity;     // 구름마다 건너뛰기 위상을 어긋나게 한다.
        // 정지 구름 스킵: 전체 계산에서 모든 파편이 정지했을 때 세우고, 버킷·만료 시점이 오거나 카메라가 움직이면 튼다.
        private bool settled;
        private float settledNextBucketIn, settledNextExpiryIn;  // 다음 알파 버킷 경계·파편 만료까지 남은 시간.
        private float settledElapsed;  // 스킵 동안 흐른 시간. 다음 전체 계산 때 나이에 한꺼번에 반영한다.
        private bool settledHasView;
        private Vector3 settledViewPosition;
        // 카메라가 3m 넘게 움직였을 때만 먼 파편 표본을 다시 고른다. 1m로 잡으면 걷는 동안 정지 구름이
        // 계산 생략을 거의 누리지 못해 상시 전체 계산으로 돌아갔다.
        private const float SettledViewMoveSq = 9f;
        private readonly DebrisWallCache wallCache = new DebrisWallCache();
        private int wallCursor;
        private bool[] wallSelected = System.Array.Empty<bool>();
        private int nearCursor;
        private uint nextVisualId;
        private bool hasView;
        private Vector3 viewPosition;
        private bool Distant(Vector3 position) => hasView && (position - viewPosition).sqrMagnitude > fullDetailDistance * fullDetailDistance;
        private bool MovingDebris(Fragment part) => !part.absorbing && !Distant(part.position) &&
            part.velocity.sqrMagnitude > .0225f && part.age < part.lifetime;
        private void SelectWallChecks(int quota, float dt)
        {
            if(wallSelected.Length<fragments.Count)System.Array.Resize(ref wallSelected,Mathf.NextPowerOfTwo(fragments.Count));
            System.Array.Clear(wallSelected,0,fragments.Count);
            if(fragments.Count==0||quota<=0)return;
            DebrisTargetIndex.Sync();
            int selected=0,visited=0;
            int fairQuota=Mathf.Max(1,quota/2);
            while(visited<fragments.Count && selected<fairQuota)
            {
                int index=wallCursor%fragments.Count;wallCursor=(index+1)%fragments.Count;visited++;
                if(!MovingDebris(fragments[index]))continue;
                wallSelected[index]=true;selected++;
            }
            visited=0;
            int priorityVisits=Mathf.Min(fragments.Count,Mathf.Max(64,quota*8));
            while(wallCache.HasNearbyObstacles && visited<priorityVisits && selected<quota)
            {
                int index=nearCursor%fragments.Count;nearCursor=(index+1)%fragments.Count;visited++;
                var part=fragments[index];
                if(wallSelected[index]||!MovingDebris(part))continue;
                float radius=.5f*part.scale.magnitude+.3f+part.velocity.magnitude*Mathf.Max(dt,.016f);
                if(!wallCache.Near(part.position,radius))continue;
                wallSelected[index]=true;selected++;
            }
            // Use unused priority slots for fair traversal, rather than wasting the budget.
            visited=0;
            while(visited<fragments.Count && selected<quota)
            {
                int index=wallCursor%fragments.Count;wallCursor=(index+1)%fragments.Count;visited++;
                if(wallSelected[index]||!MovingDebris(fragments[index]))continue;
                wallSelected[index]=true;selected++;
            }
        }
        public void ConfigureMotion(float friction,float bounce,float motion,float wallRadius)
        {
            rollingFriction=Mathf.Max(0f,friction);bounceMultiplier=Mathf.Clamp01(bounce);
            motionMultiplier=Mathf.Clamp01(motion);wallSearchRadius=Mathf.Max(0f,wallRadius);
        }
        public void Initialize(Mesh cubeMesh, Material source, float bounce, float friction, Transform reference,
            Material fragmentTemplate)
        {
            // Cache one horizontal floor plane per cloud. No per-fragment raycasts.
            drawParity = GetInstanceID() & 1;
            floorHeight = reference != null ? reference.position.y : 0f;
            if (reference != null)
            {
                float nearest = float.PositiveInfinity;
                DestructionCollisionBatch.Flush();
                foreach (var hit in Physics.RaycastAll(reference.position + Vector3.up * 0.1f,
                    Vector3.down, 1000f, ~LayerMask.GetMask("Energy"), QueryTriggerInteraction.Ignore))
                {
                    if (hit.normal.y < 0.5f || hit.collider.transform.IsChildOf(reference) ||
                        HasDestructibleParent(hit.collider)) continue;
                    if (hit.distance < nearest) { nearest = hit.distance; floorHeight = hit.point.y; }
                }
            }
            wallCache.Capture(reference,floorHeight,wallSearchRadius);
            floorBounce = Mathf.Clamp01(bounce) * bounceMultiplier;
            floorFriction = Mathf.Max(rollingFriction, friction);
            if (appearance == null) appearance = new MaterialPropertyBlock();
            appearance.Clear();
            mesh = cubeMesh;
            if (groupMaterial == null) groupMaterial = new Material(source);
            else { groupMaterial.shader = source.shader; groupMaterial.CopyPropertiesFromMaterial(source); }
            groupMaterial.SetFloat("_Surface", 0f);
            groupMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
            groupMaterial.SetFloat("_DstBlend", (float)BlendMode.Zero);
            groupMaterial.SetFloat("_ZWrite", 1f);
            groupMaterial.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            groupMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            groupMaterial.SetOverrideTag("RenderType", "Opaque");
            groupMaterial.renderQueue = (int)RenderQueue.Geometry;
            var template = fragmentTemplate != null ? fragmentTemplate : source;
            if (material == null) material = new Material(template);
            else { material.shader = template.shader; material.CopyPropertiesFromMaterial(template); }
            material.enableInstancing = true;
            color = source.GetColor("_BaseColor");
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            ReserveBuffers(64, maximumCachedGroups);
        }
        private static bool HasDestructibleParent(Collider collider)
        {
            for (Transform current = collider != null ? collider.transform : null; current != null; current = current.parent)
                if (current.TryGetComponent<DestructibleTarget>(out _)) return true;
            return false;
        }

        public struct Piece
        {
            public Vector3 position, scale;
            public Quaternion rotation;
        }

        private void VaryDebris(ref Fragment part)
        {
            // Visual-only variation; original voxel geometry and resistance are untouched.
            part.scale *= Random.Range(0.85f, 1.1f);
            part.rotation = Quaternion.Euler(Random.Range(-12f,12f),Random.Range(-12f,12f),
                Random.Range(-12f,12f)) * part.rotation;
            if (!part.absorbing)
            {
                float speed = part.velocity.magnitude;
                part.velocity = part.velocity * Random.Range(0.75f,1.25f)
                    + Random.insideUnitSphere * Mathf.Min(0.7f, speed*0.2f+0.08f);
            }
            if(!part.absorbing)
            {
                part.velocity *= motionMultiplier;
                part.spin *= 0.5f;
                part.wallPosition = part.position; part.wallElapsed = 0f;
            }
            part.lifetime *= Random.Range(0.9f,1.1f);
            part.fade = Mathf.Min(part.fade,part.lifetime);
        }

        public void Emit(Piece cell, Vector3 velocity, float lifetime, float fade)
        {
            // 새 파편은 그리기 캐시에 없으므로 다음 프레임은 반드시 다시 계산한다.
            drawCached = false;
            var part = new Fragment { position = cell.position, rotation = cell.rotation,
                scale = cell.scale, velocity = velocity,
                spin = Random.onUnitSphere * Random.Range(90f, 360f),
                absorbing = absorbEmission, absorptionStart = cell.position, absorptionCenter = emissionCenter,
                spreadPosition = cell.position + (cell.position - emissionCenter).normalized * emissionSpreadDistance,
                spreadFraction = emissionSpreadFraction,
                lifetime = absorbEmission ? emissionDuration : lifetime, fade = Mathf.Clamp(fade, 0.01f, lifetime) };
            VaryDebris(ref part);
            part.visualId = ++nextVisualId;
            fragments.Add(part);
        }

        public void EmitGroup(List<Piece> cells, float lifetime, float fade, Vector3 velocity = default)
        {
            drawCached = false;
            if (absorbEmission)
            {
                foreach (var cell in cells) Emit(cell, Vector3.zero, lifetime, fade);
                return;
            }
            if (cells.Count == 0) return;
            var group = RentGroup();
            group.spin = Random.onUnitSphere * 25f; group.velocity = velocity;
            foreach (Piece cell in cells) group.position += cell.position;
            group.position /= cells.Count;
            foreach (Piece cell in cells)
            {
                group.parts.Add(new Fragment { position = cell.position - group.position,
                    rotation = cell.rotation, scale = cell.scale,
                    spin = Random.onUnitSphere * Random.Range(90f, 360f), lifetime = lifetime,
                    fade = Mathf.Clamp(fade, 0.01f, lifetime) });
            }
            groups.Add(group);
        }

        public void BreakIntactGroups()
        {
            drawCached = false;
            foreach (var group in groups)
            {
                foreach (var part in group.parts)
                {
                    Vector3 position = group.position + group.rotation * part.position;
                    Emit(new Piece { position = position, rotation = group.rotation * part.rotation, scale = part.scale },
                        group.velocity + (position - group.position).normalized * 0.5f + Vector3.up * 0.2f,
                        part.lifetime, part.fade);
                }
                ReturnGroup(group);
            }
            groups.Clear();
        }

        private void AdvanceGroups(float dt)
        {
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                FallingGroup group = groups[i];
                group.position += group.velocity * dt + Physics.gravity * (0.5f * dt * dt);
                group.velocity += Physics.gravity * dt;
                group.rotation = (Quaternion.Euler(group.spin * dt) * group.rotation).normalized;
                float bottom = float.MaxValue;
                foreach (Fragment source in group.parts)
                {
                    Quaternion rotation = (group.rotation * source.rotation).normalized;
                    Vector3 position = group.position + group.rotation * source.position;
                    float height = HalfHeight(rotation, source.scale);
                    bottom = Mathf.Min(bottom, position.y - height);
                }
                bool contacting = bottom <= floorHeight;
                // The horizontal floor receives the downward velocity, before impact damping.
                float impactSpeed = Mathf.Max(0f, -group.velocity.y);
                if (contacting)
                {
                    // Resolve the rigid chunk's floor contact before choosing cells to break.
                    // Otherwise a long frame pushes multiple layers through the plane at once.
                    group.position.y += floorHeight - bottom;
                }
                for (int index = group.parts.Count - 1; index >= 0; index--)
                {
                    Fragment source = group.parts[index];
                    Fragment part = source;
                    part.position = group.position + group.rotation * source.position;
                    part.rotation = (group.rotation * source.rotation).normalized;
                    float halfHeight = HalfHeight(part.rotation, part.scale);
                    if (contacting)
                    {
                        part.position.y = Mathf.Max(part.position.y, floorHeight + halfHeight);
                        Vector3 scatter = Random.insideUnitSphere;
                        scatter.y = Mathf.Abs(scatter.y) + 0.3f;
                        float impactKick = impactSpeed * 0.3f;
                        part.velocity = scatter * impactKick
                            + new Vector3(group.velocity.x, impactKick * 0.25f, group.velocity.z);
                        VaryDebris(ref part);
                        part.visualId = ++nextVisualId;
                        part.resting = false;
                        fragments.Add(part);
                        group.parts[index] = group.parts[group.parts.Count - 1];
                        group.parts.RemoveAt(group.parts.Count - 1);
                        group.meshDirty = true;
                    }
                }
                // Keep the whole chunk intact until its first floor contact.
                if (group.parts.Count == 0)
                {
                    ReturnGroup(group);
                    groups[i] = groups[groups.Count - 1];
                    groups.RemoveAt(groups.Count - 1);
                }
                else
                {
                    if (group.meshDirty)
                    {
                        if (group.combinedMesh == null)
                            group.combinedMesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "ConnectedTargetChunk" };
                        if (group.combines.Length != group.parts.Count) System.Array.Resize(ref group.combines, group.parts.Count);
                        var combines = group.combines;
                        for (int partIndex = 0; partIndex < combines.Length; partIndex++)
                        {
                            Fragment part = group.parts[partIndex];
                            combines[partIndex] = new CombineInstance { mesh = mesh,
                                transform = Matrix4x4.TRS(part.position, part.rotation, part.scale) };
                        }
                        group.combinedMesh.Clear();
                        group.combinedMesh.CombineMeshes(combines, true, true, false);
                        group.meshDirty = false;
                    }
                    // 마지막 변환을 캐시해 먼 구름의 생략 프레임은 그룹 변환을 다시 만들지 않는다.
                    group.drawMatrix = ScaleTranslate(RotationMatrix(group.rotation), Vector3.one, group.position);
                    Graphics.DrawMesh(group.combinedMesh, group.drawMatrix,
                        groupMaterial, gameObject.layer, null, 0, null, ShadowCastingMode.On, true);
                }
            }
        }

        private static float HalfHeight(Quaternion rotation, Vector3 scale) => 0.5f * (
            Mathf.Abs((rotation * Vector3.right).y * scale.x) +
            Mathf.Abs((rotation * Vector3.up).y * scale.y) +
            Mathf.Abs((rotation * Vector3.forward).y * scale.z));

        // 쿼터니언→회전 행렬. Matrix4x4.TRS 내부 작업과 HalfHeight의 회전 세 번을 한 번의 계산으로 합치기 위한 것.
        private static Matrix4x4 RotationMatrix(Quaternion q)
        {
            float xx = q.x * q.x, yy = q.y * q.y, zz = q.z * q.z;
            float xy = q.x * q.y, xz = q.x * q.z, yz = q.y * q.z;
            float wx = q.w * q.x, wy = q.w * q.y, wz = q.w * q.z;
            var m = new Matrix4x4();
            m.m00 = 1f - 2f * (yy + zz); m.m01 = 2f * (xy - wz); m.m02 = 2f * (xz + wy); m.m03 = 0f;
            m.m10 = 2f * (xy + wz); m.m11 = 1f - 2f * (xx + zz); m.m12 = 2f * (yz - wx); m.m13 = 0f;
            m.m20 = 2f * (xz - wy); m.m21 = 2f * (yz + wx); m.m22 = 1f - 2f * (xx + yy); m.m23 = 0f;
            m.m30 = 0f; m.m31 = 0f; m.m32 = 0f; m.m33 = 1f;
            return m;
        }

        // 회전 행렬에 스케일·이동을 합친다. Matrix4x4.TRS와 같은 결과이며 임시 행렬 계산이 없다.
        private static Matrix4x4 ScaleTranslate(Matrix4x4 rotation, Vector3 scale, Vector3 position) => new Matrix4x4(
            new Vector4(rotation.m00 * scale.x, rotation.m10 * scale.x, rotation.m20 * scale.x, 0f),
            new Vector4(rotation.m01 * scale.y, rotation.m11 * scale.y, rotation.m21 * scale.y, 0f),
            new Vector4(rotation.m02 * scale.z, rotation.m12 * scale.z, rotation.m22 * scale.z, 0f),
            new Vector4(position.x, position.y, position.z, 1f));

        // 버킷 배열은 고수위를 넘을 때만 커지므로 정상 프레임에는 할당이 없다.
        private void AddMatrix(int bucket, Matrix4x4 matrix)
        {
            Matrix4x4[] matrices = bucketMatrices[bucket];
            int count = bucketCounts[bucket];
            if (count == matrices.Length)
            {
                System.Array.Resize(ref matrices, count * 2);
                bucketMatrices[bucket] = matrices;
            }
            matrices[count] = matrix;
            bucketCounts[bucket] = count + 1;
        }

        private void AdvanceDistant(ref Fragment part, float dt, Vector3 gravity)
        {
            if (part.grounded)
            {
                part.velocity.y = 0f;
                Vector3 previous = part.velocity;
                part.velocity = Vector3.MoveTowards(part.velocity, Vector3.zero, floorFriction * dt);
                if (part.velocity.sqrMagnitude < 0.0225f) part.velocity = Vector3.zero;
                part.position += (previous + part.velocity) * (0.5f * dt);
            }
            else
            {
                part.position += part.velocity * dt + gravity * (0.5f * dt * dt);
                part.velocity += gravity * dt;
                float restingY = floorHeight + HalfHeight(part.rotation, part.scale);
                if (part.position.y <= restingY)
                {
                    part.position.y = restingY;
                    if (part.velocity.y < 0f)
                    {
                        float rebound = -part.velocity.y * floorBounce;
                        part.grounded = rebound < 0.45f;
                        part.velocity.y = part.grounded ? 0f : rebound;
                    }
                }
            }
            part.wallPosition = part.position;
            part.wallElapsed = 0f;
        }

        private bool DrawDistant(Fragment part, ref int drawn)
        {
            if (drawn >= maximumDistantDraws) return false;
            float distance = Vector3.Distance(part.position, viewPosition);
            float fraction = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01((distance - fullDetailDistance) / 18f));
            if ((part.visualId * 2654435761u) / (float)uint.MaxValue >= fraction) return false;
            drawn++;
            return true;
        }

        // 매니저가 프레임당 한 번씩 부른다. 시선·시간·중력은 매니저에서 이미 읽은 값이다.
        internal void Tick(Camera view, float dt, Vector3 gravity)
        {
            if (material == null) return;
            if (fragments.Count == 0 && groups.Count == 0)
            {
                if (owner != null && owner.IsDestroyed) PoolManager.Release(gameObject);
                return;
            }
            hasView = view != null;
            if (hasView) viewPosition = view.transform.position;
            // 정지 구름은 파편 루프를 돌지 않고 캐시된 버킷 행렬만 다시 그린다. 알파 버킷이 바뀌거나
            // 파편이 만료되는 시점, 카메라가 충분히 움직인 시점에만 전체 계산으로 돌아온다.
            if (settled && drawCached && groups.Count == 0 && hasView == settledHasView &&
                (!hasView || (viewPosition - settledViewPosition).sqrMagnitude <= SettledViewMoveSq))
            {
                float elapsed = settledElapsed + dt;
                if (settledNextBucketIn > elapsed && settledNextExpiryIn > elapsed)
                {
                    settledElapsed = elapsed;
                    DrawBuckets();
                    return;
                }
            }
            // 먼 구름은 프레임 건너 재계산을 건너뛰고 직전 그리기 결과를 그대로 다시 제출한다. 지난 시간은 다음 계산에 더해진다.
            if (drawCached && ReducedUpdate())
            {
                pausedTime += dt;
                RedrawCache();
                return;
            }
            dt += pausedTime + settledElapsed;
            pausedTime = 0f; settledElapsed = 0f;
            drawCached = true;
            AdvanceGroups(dt);
            int wallQuota = Mathf.Min(fragments.Count,PoolManager.WallChecksPerCloud);
            SelectWallChecks(wallQuota,dt);
            System.Array.Clear(bucketCounts, 0, bucketCounts.Length);
            int distantDrawn = 0;
            int frameParity = Time.frameCount & 1;
            // 구름 전체 정지 판정: 움직이는 분기에 들어간 파편이 하나라도 있으면 무효가 된다.
            bool allResting = groups.Count == 0;
            float nextBucketIn = float.PositiveInfinity, nextExpiryIn = float.PositiveInfinity;
            for (int i = fragments.Count - 1; i >= 0; i--)
            {
                Fragment part = fragments[i];
                part.age += dt;
                if (part.age >= part.lifetime)
                {
                    fragments[i] = fragments[fragments.Count - 1];
                    fragments.RemoveAt(fragments.Count - 1);
                    continue;
                }
                float alpha = Mathf.Clamp01((part.lifetime - part.age) / part.fade);
                int bucketIndex = Mathf.Clamp(Mathf.CeilToInt(alpha * 15f), 0, 15);
                if (part.absorbing)
                {
                    float t = Mathf.Clamp01(part.age / part.lifetime);
                    float absorbT = Mathf.Clamp01((t - part.spreadFraction) / (1f - part.spreadFraction));
                    float eased = absorbT * absorbT * (3f - 2f * absorbT);
                    if (t < part.spreadFraction)
                    {
                        float spreadT = t / part.spreadFraction;
                        float outward = 1f - (1f - spreadT) * (1f - spreadT);
                        part.position = Vector3.LerpUnclamped(part.absorptionStart, part.spreadPosition, outward);
                    }
                    else part.position = Vector3.LerpUnclamped(part.spreadPosition, part.absorptionCenter, eased);
                    fragments[i] = part;
                    AddMatrix(15, ScaleTranslate(RotationMatrix(part.rotation), part.scale * (1f - eased), part.position));
                    allResting = false;
                    continue;
                }
                if (part.resting)
                {
                    // 멀리서 정지한 파편이 근거리로 들어오면 정지를 풀어 납작 정렬·높이 보정을 이어간다.
                    if (part.restedDistant && !Distant(part.position))
                    {
                        part.resting = false;
                        part.restedDistant = false;
                        part.hasCachedMatrix = false;  // 다시 멀어지면 첫 계산에서 새 행렬을 만들게 한다.
                    }
                    else
                    {
                        // 바닥에 앉은 파편은 수명 종료까지 자세가 고정: 물리·행렬 재계산 없이 캐시만 제출한다.
                        // 정지 조건(마찰로 속도 0 고정, 정렬 도달)은 되돌아오지 않으므로 연속 프레임 확인 없이 세운다.
                        fragments[i] = part;
                        if ((!Distant(part.position) || DrawDistant(part, ref distantDrawn)) && bucketIndex > 0)
                            AddMatrix(bucketIndex, part.cachedMatrix);
                        float remaining = part.lifetime - part.age;
                        if (remaining < nextExpiryIn) nextExpiryIn = remaining;
                        if (bucketIndex > 0)
                        {
                            float bucketIn = remaining - part.fade * ((bucketIndex - 1) * (1f / 15f));
                            if (bucketIn < nextBucketIn) nextBucketIn = bucketIn;
                        }
                        continue;
                    }
                }
                allResting = false;
                if (Distant(part.position))
                {
                    part.distantElapsed += dt;
                    bool advancing = ((frameParity + (int)part.visualId) & 1) == 0;
                    if (advancing)
                    {
                        AdvanceDistant(ref part, part.distantElapsed, gravity);
                        part.distantElapsed = 0f;
                        // 멀리 있는 파편은 회전 갱신이 없으므로 바닥 접촉 후 속도가 0이면 더는 움직이지 않는다. 여기서 정지로 본다.
                        // 가까워지면 납작 정렬을 이어가도록 먼 정지로 표시한다.
                        if (part.grounded && part.velocity.sqrMagnitude == 0f)
                        {
                            part.resting = true;
                            part.restedDistant = true;
                        }
                    }
                    fragments[i] = part;
                    // 움직이지 않는 교대 프레임은 지난 행렬을 다시 쓴다. 정지 파편의 cachedMatrix가 곧 최종 자세다.
                    Matrix4x4 farPose;
                    if (advancing || !part.hasCachedMatrix)
                    {
                        farPose = ScaleTranslate(RotationMatrix(part.rotation), part.scale, part.position);
                        part.cachedMatrix = farPose;
                        part.hasCachedMatrix = true;
                    }
                    else farPose = part.cachedMatrix;
                    if (DrawDistant(part, ref distantDrawn) && bucketIndex > 0)
                        AddMatrix(bucketIndex, farPose);
                    continue;
                }
                float step = dt + part.distantElapsed;
                part.distantElapsed = 0f;
                if (part.grounded)
                {
                    part.velocity.y = 0f;
                    Vector3 previousVelocity = part.velocity;
                    part.velocity = Vector3.MoveTowards(part.velocity, Vector3.zero, floorFriction * step);
                    if(part.velocity.sqrMagnitude<0.0225f)part.velocity=Vector3.zero;
                    Vector3 movement = (previousVelocity + part.velocity) * (0.5f * step);
                    part.position += movement;
                    float rollingRadius = Mathf.Max(0.01f, Mathf.Max(part.scale.x, part.scale.z) * 0.5f);
                    if (movement.sqrMagnitude > 0.00000001f)
                    {
                        Vector3 axis = Vector3.Cross(Vector3.up, movement).normalized;
                        part.rotation = (Quaternion.AngleAxis(movement.magnitude / rollingRadius * Mathf.Rad2Deg, axis) * part.rotation).normalized;
                    }
                    else
                    {
                        // Settle onto the nearest cube face without a collider/contact solver.
                        Quaternion previousRotation = part.rotation;
                        Vector3 bestAxis = part.rotation * Vector3.up;
                        float best = Mathf.Abs(bestAxis.y);
                        Vector3 right = part.rotation * Vector3.right;
                        Vector3 forward = part.rotation * Vector3.forward;
                        if (Mathf.Abs(right.y) > best) { bestAxis = right; best = Mathf.Abs(right.y); }
                        if (Mathf.Abs(forward.y) > best) bestAxis = forward;
                        if (bestAxis.y < 0f) bestAxis = -bestAxis;
                        Quaternion flat = Quaternion.FromToRotation(bestAxis, Vector3.up) * part.rotation;
                        part.rotation = Quaternion.RotateTowards(part.rotation, flat, 180f * step);
                        // 속도가 0이고 회전 변화가 사실상 사라지면 이후 수명 종료까지 제자리에 있다.
                        part.resting = part.velocity.sqrMagnitude == 0f &&
                            Quaternion.Dot(previousRotation, part.rotation) > 0.999999f;
                    }
                    part.spin = Vector3.zero;
                }
                else
                {
                    part.position += part.velocity * step + gravity * (0.5f * step * step);
                    part.velocity += gravity * step;
                }
                if (!part.grounded) part.rotation = (Quaternion.Euler(part.spin * step) * part.rotation).normalized;
                // 회전 행렬을 한 번 만들어 높이 검사와 최종 행렬이 함께 쓴다.
                Matrix4x4 rotation = RotationMatrix(part.rotation);
                // Cube extent along world Y prevents rotating corners from sinking below the plane.
                float halfHeight = 0.5f * (Mathf.Abs(rotation.m10) * part.scale.x +
                    Mathf.Abs(rotation.m11) * part.scale.y + Mathf.Abs(rotation.m12) * part.scale.z);
                float restingY = floorHeight + halfHeight;
                if (part.grounded || part.position.y <= restingY)
                {
                    part.position.y = restingY;
                    if (!part.grounded && part.velocity.y < 0f)
                    {
                        float impactSpeed = -part.velocity.y;
                        if (part.velocity.x * part.velocity.x + part.velocity.z * part.velocity.z < 0.04f)
                        {
                            Vector3 scatter = new Vector3(part.spin.z, 0f, -part.spin.x).normalized;
                            part.velocity += scatter * Mathf.Min(1.2f, impactSpeed * 0.15f);
                        }
                        float rebound = impactSpeed * floorBounce;
                        part.grounded = rebound < 0.45f;
                        part.velocity.y = part.grounded ? 0f : rebound;
                    }
                }
                part.wallElapsed += step;
                if(wallSelected[i] && part.velocity.sqrMagnitude>0.0225f && PoolManager.TryWallCheck())
                {
                    float wallRadius=0.5f*part.scale.magnitude;
                    wallCache.Resolve(part.wallPosition,ref part.position,ref part.velocity,wallRadius,part.wallElapsed,0.15f);
                    part.wallPosition=part.position;part.wallElapsed=0f;
                }
                else if(part.velocity.sqrMagnitude<=0.0225f)
                { part.wallPosition=part.position;part.wallElapsed=0f; }
                Matrix4x4 pose = ScaleTranslate(rotation, part.scale, part.position);
                if (part.resting) { part.cachedMatrix = pose; part.hasCachedMatrix = true; }
                fragments[i] = part;
                if (bucketIndex > 0) AddMatrix(bucketIndex, pose);
            }
            // 모든 파편이 정지했고 남은 덩어리가 없으면 다음 프레임부터 구름 단위로 루프를 건너뛴다.
            if (allResting && fragments.Count > 0)
            {
                settled = true;
                settledNextBucketIn = nextBucketIn;
                settledNextExpiryIn = nextExpiryIn;
                settledHasView = hasView;
                settledViewPosition = viewPosition;
            }
            else { settled = false; settledElapsed = 0f; }
            DrawBuckets();
        }

        // 바깥에서 보는 보크셀 크기보다 1프레임 자세 지연이 작아지는 거리부터 반 주기 갱신을 쓴다.
        private bool ReducedUpdate()
        {
            if (reducedUpdateDistance <= 0f || !hasView || owner == null) return false;
            float distance = reducedUpdateDistance;
            if ((owner.transform.position - viewPosition).sqrMagnitude <= distance * distance) return false;
            return ((Time.frameCount + drawParity) & 1) == 1;
        }

        // 재계산을 건너뛴 프레임: 직전 프레임의 그룹·버킷 행렬을 다시 제출하기만 한다.
        private void RedrawCache()
        {
            for (int i = 0; i < groups.Count; i++)
                if (groups[i].combinedMesh != null && !groups[i].meshDirty)
                    Graphics.DrawMesh(groups[i].combinedMesh, groups[i].drawMatrix, groupMaterial,
                    gameObject.layer, null, 0, null, ShadowCastingMode.On, true);
            DrawBuckets();
        }

        private void DrawBuckets()
        {
            for (int i = 1; i < bucketMatrices.Length; i++)
            {
                int count = bucketCounts[i];
                if (count == 0) continue;
                Color faded = color;
                faded.a *= i / 15f;
                appearance.SetColor(BaseColorId, faded);
                Matrix4x4[] matrices = bucketMatrices[i];
                if (count <= batch.Length)
                {
                    Graphics.DrawMeshInstanced(mesh, 0, material, matrices, count, appearance,
                        ShadowCastingMode.Off, false, gameObject.layer);
                }
                else
                {
                    // 최대 배치 크기를 넘는 버킷만 batch로 잘려 나간다.
                    for (int start = 0; start < count; start += batch.Length)
                    {
                        int batchCount = Mathf.Min(batch.Length, count - start);
                        System.Array.Copy(matrices, start, batch, 0, batchCount);
                        Graphics.DrawMeshInstanced(mesh, 0, material, batch, batchCount, appearance,
                            ShadowCastingMode.Off, false, gameObject.layer);
                    }
                }
            }
        }
        private void OnEnable() => VisualDebrisCloudManager.Register(this);
        private void OnDisable()
        {
            VisualDebrisCloudManager.Unregister(this);
            ClearDebris();
            var previousOwner = owner; owner = null;
            if (previousOwner != null) previousOwner.ForgetDebrisCloud(this);
            absorbEmission = false; emissionCenter = Vector3.zero;
            emissionDuration = emissionSpreadDistance = emissionSpreadFraction = 0f;
            mesh = null; appearance?.Clear(); wallCache.Clear(); wallCursor=nearCursor=0;
        }
        private void OnDestroy()
        {
            foreach (FallingGroup group in groups)
                if (group.combinedMesh != null) Destroy(group.combinedMesh);
            foreach (FallingGroup group in groupCache)
                if (group.combinedMesh != null) Destroy(group.combinedMesh);
            if (material != null) Destroy(material);
            if (groupMaterial != null) Destroy(groupMaterial);
        }
    }
}




