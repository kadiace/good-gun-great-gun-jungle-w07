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
            public CombineInstance[] combines = System.Array.Empty<CombineInstance>();
        }
        private readonly Dictionary<Vector3Int, RenderChunk> renderChunks = new Dictionary<Vector3Int, RenderChunk>();
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
            if (renderChunks.TryGetValue(RenderKey(coordinate), out var chunk)) chunk.dirty = true;
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
                        chunk.dirty = false;
                    }
                    renderDirty = false;
                }
            }
            foreach (var chunk in renderChunks.Values)
                if (chunk.mesh != null && chunk.mesh.vertexCount > 0)
                {
                    Graphics.DrawMesh(chunk.mesh, CellRoot.localToWorldMatrix, instancedMaterial, targetMesh.gameObject.layer,
                        null, 0, CurrentDamageProperties, UnityEngine.Rendering.ShadowCastingMode.On, true);
                }
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
        private Texture3D damageTexture;
        private Vector2[] damagePixels;
        private bool damageTextureDirty;
        private Color damageTint = Color.black, hitFlashColor = Color.white;
        private float hitFlashDuration = .12f, damageTintStrength = .45f;
        private static readonly int DamageTextureId = Shader.PropertyToID("_CellDamage");
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
            if (damageTexture == null)
            {
                // A data texture preserves cell-specific colour without splitting the intact mesh.
                // Resources reference keeps the shader available in player builds.
                var template = damageTemplate != null ? damageTemplate : Resources.Load<Material>("DestructibleDamageLit");
                if (template == null) throw new System.InvalidOperationException("DestructibleDamageLit material is missing.");
                undamagedMaterial = originalRenderer.sharedMaterial;
                damageMaterial = new Material(template);
                damageMaterial.CopyPropertiesFromMaterial(undamagedMaterial);
                damageTexture = new Texture3D(divisions.x, divisions.y, divisions.z, TextureFormat.RGFloat, false)
                    { name = "Target cell damage", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                damagePixels = new Vector2[divisions.x * divisions.y * divisions.z];
                damageProperties = new MaterialPropertyBlock();
                if (penetrationFlashActive) originalRenderer.SetPropertyBlock(penetrationOriginalProperties);
                originalRenderer.GetPropertyBlock(damageProperties);
                damageProperties.SetTexture(DamageTextureId, damageTexture);
                damageProperties.SetVector(DamageOriginId, gridOrigin);
                damageProperties.SetVector(DamageStepId, gridStep);
                damageProperties.SetVector(DamageDimensionsId, (Vector3)divisions);
                originalRenderer.sharedMaterial = damageMaterial;
                if (instancedMaterial != null) { Destroy(instancedMaterial); instancedMaterial = new Material(damageMaterial); }
            }
            var coordinate = cell.coordinate;
            damagePixels[coordinate.x + divisions.x * (coordinate.y + divisions.y * coordinate.z)] =
                new Vector2(Mathf.Clamp01((float)(1d - cell.explosionHealth)), cell.explosionHitTime);
            if (!damageTextureDirty) { damageTextureDirty = true; RefreshUpdaterRegistration(); }
        }

        private void UploadDamageAppearance()
        {
            if (!damageTextureDirty || damageTexture == null) return;
            damageProperties.SetColor(DamageTintId, damageTint);
            damageProperties.SetColor(FlashColorId, hitFlashColor);
            damageProperties.SetFloat(FlashDurationId, hitFlashDuration);
            damageProperties.SetFloat(DamageStrengthId, damageTintStrength);
            damageProperties.SetFloat(PenetrationFlashUntilId, penetrationFlashActive ? penetrationFlashUntil : 0f);
            damageTexture.SetPixelData(damagePixels, 0);
            damageTexture.Apply(false, false);
            originalRenderer.SetPropertyBlock(damageProperties);
            damageTextureDirty = false;
        }

        private void ResetDamageAppearance()
        {
            if (damageTexture == null) return;
            System.Array.Clear(damagePixels, 0, damagePixels.Length);
            damageTextureDirty = true;
            RefreshUpdaterRegistration();
        }

        private void DisposeDamageAppearance()
        {
            if (originalRenderer != null && undamagedMaterial != null) originalRenderer.sharedMaterial = undamagedMaterial;
            if (damageMaterial != null) Destroy(damageMaterial);
            if (damageTexture != null) Destroy(damageTexture);
        }

        private readonly HashSet<Vector3Int> changedBoundary = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> localSupportVisited = new HashSet<Vector3Int>();
        private readonly Queue<Vector3Int> localSupportQueue = new Queue<Vector3Int>();
        private bool supportValidated, resolvingSupportDrop;
        private static readonly ProfilerMarker localSupportMarker = new ProfilerMarker("DestructibleTarget.ResolveSupport.Local");
        private static readonly ProfilerMarker supportFallbackMarker = new ProfilerMarker("DestructibleTarget.ResolveSupport.FullFallback");
        private void RecordSupportRemoval(Vector3Int coordinate)
        {
            if (!supportValidated || resolvingSupportDrop) return;
            foreach (var direction in neighbors) changedBoundary.Add(coordinate + direction);
        }
        private void ResolveSupport()
        {
            if (attachedToMovingBody) return;
            using (supportMarker.Auto())
            {
                if (!supportValidated)
                {
                    using (supportFallbackMarker.Auto()) DropUnsupported();
                }
                else if (changedBoundary.Count > 0)
                    using (localSupportMarker.Auto()) DropUnsupportedNearRemovedBoundary();
                supportValidated = true;
                changedBoundary.Clear();
            }
        }
    }
}
