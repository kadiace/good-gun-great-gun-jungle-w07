using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WeaponWorkshop
{
    [DefaultExecutionOrder(10000)]
    public sealed class VisualDebrisCloud : MonoBehaviour
    {
        private struct Fragment
        {
            public Vector3 position, velocity, scale, spin;
            public Vector3 wallPosition;
            public float wallElapsed;
            public Quaternion rotation;
            public float age, lifetime, fade;
            public bool grounded;
            public bool absorbing;
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
        }
        private readonly List<FallingGroup> groups = new List<FallingGroup>();
        private readonly Stack<FallingGroup> groupCache = new Stack<FallingGroup>();
        private int maximumCachedGroups = 8;
        private DestructibleTarget owner;
        public void BindOwner(DestructibleTarget value) => owner = value;
        public void ReserveBuffers(int fragmentCapacity, int maximumGroups)
        {
            if (fragments.Capacity < fragmentCapacity) fragments.Capacity = fragmentCapacity;
            if(wallSelected.Length<fragmentCapacity)System.Array.Resize(ref wallSelected,fragmentCapacity);
            maximumCachedGroups = Mathf.Max(0, maximumGroups);
            while(groupCache.Count < maximumCachedGroups)
            {
                var group = new FallingGroup();
                group.parts.Capacity = Mathf.Min(512,Mathf.Max(0,fragmentCapacity));
                group.combinedMesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "ConnectedTargetChunk" };
                groupCache.Push(group);
            }
            for (int i = 0; i < buckets.Length; i++)
                if (buckets[i] == null) buckets[i] = new List<Matrix4x4>(Mathf.Max(16, fragmentCapacity / buckets.Length));
        }
        private FallingGroup RentGroup()
        {
            var group = groupCache.Count > 0 ? groupCache.Pop() : new FallingGroup();
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
            foreach (var bucket in buckets) bucket?.Clear();
        }
        private readonly List<Matrix4x4>[] buckets = new List<Matrix4x4>[16];
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
        private void SelectWallChecks(int quota)
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
                float radius=.5f*part.scale.magnitude+.3f+part.velocity.magnitude*Mathf.Max(Time.deltaTime,.016f);
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
            floorHeight = reference != null ? reference.position.y : 0f;
            if (reference != null)
            {
                float nearest = float.PositiveInfinity;
                foreach (var hit in Physics.RaycastAll(reference.position + Vector3.up * 0.1f,
                    Vector3.down, 1000f, ~LayerMask.GetMask("Energy"), QueryTriggerInteraction.Ignore))
                {
                    if (hit.normal.y < 0.5f || hit.collider.transform.IsChildOf(reference) ||
                        hit.collider.GetComponentInParent<DestructibleTarget>() != null) continue;
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
            ReserveBuffers(0, maximumCachedGroups);
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
                    appearance.SetColor("_BaseColor", color);
                    Graphics.DrawMesh(group.combinedMesh, Matrix4x4.TRS(group.position, group.rotation, Vector3.one),
                        groupMaterial, gameObject.layer, null, 0, null, ShadowCastingMode.On, true);
                }
            }
        }

        private static float HalfHeight(Quaternion rotation, Vector3 scale) => 0.5f * (
            Mathf.Abs((rotation * Vector3.right).y * scale.x) +
            Mathf.Abs((rotation * Vector3.up).y * scale.y) +
            Mathf.Abs((rotation * Vector3.forward).y * scale.z));

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

        private void LateUpdate()
        {
            if (material == null) return;
            if (fragments.Count == 0 && groups.Count == 0)
            {
                if (owner != null && owner.IsDestroyed) PoolManager.Release(gameObject);
                return;
            }
            foreach (var bucket in buckets) bucket.Clear();
            float dt = Time.deltaTime;
            AdvanceGroups(dt);
            Camera view = Camera.main;
            hasView = view != null;
            if (hasView) viewPosition = view.transform.position;
            int wallQuota = Mathf.Min(fragments.Count,PoolManager.WallChecksPerCloud);
            SelectWallChecks(wallQuota);
            Vector3 gravity = Physics.gravity;
            int distantDrawn = 0;
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
                    buckets[15].Add(Matrix4x4.TRS(part.position, part.rotation, part.scale * (1f - eased)));
                    continue;
                }
                if (Distant(part.position))
                {
                    part.distantElapsed += dt;
                    if (((Time.frameCount + (int)part.visualId) & 1) == 0)
                    {
                        AdvanceDistant(ref part, part.distantElapsed, gravity);
                        part.distantElapsed = 0f;
                    }
                    fragments[i] = part;
                    if (DrawDistant(part, ref distantDrawn))
                    {
                        float opacity = Mathf.Clamp01((part.lifetime - part.age) / part.fade);
                        buckets[Mathf.Clamp(Mathf.CeilToInt(opacity * 15f), 0, 15)]
                            .Add(Matrix4x4.TRS(part.position, part.rotation, part.scale));
                    }
                    continue;
                }
                float step = dt + part.distantElapsed;
                part.distantElapsed = 0f;
                float alpha = Mathf.Clamp01((part.lifetime - part.age) / part.fade);
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
                        Vector3 bestAxis = part.rotation * Vector3.up;
                        float best = Mathf.Abs(bestAxis.y);
                        Vector3 right = part.rotation * Vector3.right;
                        Vector3 forward = part.rotation * Vector3.forward;
                        if (Mathf.Abs(right.y) > best) { bestAxis = right; best = Mathf.Abs(right.y); }
                        if (Mathf.Abs(forward.y) > best) bestAxis = forward;
                        if (bestAxis.y < 0f) bestAxis = -bestAxis;
                        Quaternion flat = Quaternion.FromToRotation(bestAxis, Vector3.up) * part.rotation;
                        part.rotation = Quaternion.RotateTowards(part.rotation, flat, 180f * step);
                    }
                    part.spin = Vector3.zero;
                }
                else
                {
                    part.position += part.velocity * step + gravity * (0.5f * step * step);
                    part.velocity += gravity * step;
                }
                if (!part.grounded) part.rotation = (Quaternion.Euler(part.spin * step) * part.rotation).normalized;
                // Cube extent along world Y prevents rotating corners from sinking below the plane.
                float halfHeight = HalfHeight(part.rotation, part.scale);
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
                fragments[i] = part;
                int bucketIndex = Mathf.Clamp(Mathf.CeilToInt(alpha * 15f), 0, 15);
                buckets[bucketIndex].Add(Matrix4x4.TRS(part.position, part.rotation, part.scale));
            }
            for (int i = 1; i < buckets.Length; i++)
            {
                Color faded = color;
                faded.a *= i / 15f;
                appearance.SetColor("_BaseColor", faded);
                List<Matrix4x4> matrices = buckets[i];
                for (int start = 0; start < matrices.Count; start += batch.Length)
                {
                    int count = Mathf.Min(batch.Length, matrices.Count - start);
                    matrices.CopyTo(start, batch, 0, count);
                    Graphics.DrawMeshInstanced(mesh, 0, material, batch, count, appearance,
                        ShadowCastingMode.Off, false, gameObject.layer);
                }
            }
        }
        private void OnDisable()
        {
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




