using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // Gravity impact: remove destructible voxels once, then retain only the visual field.
    public sealed class GravityField : MonoBehaviour
    {
        private float remaining, duration;
        private Transform lens;
        private Renderer lensRenderer;
        private MaterialPropertyBlock appearance;
        private readonly HashSet<DestructibleTarget> structures = new HashSet<DestructibleTarget>();

        public static GravityField Create(Vector3 center, float radius, float pullSpeed, float duration,
            LayerMask layers, Transform owner, Transform weapon, ParticleSystem effectPrefab,
            GameObject distortionPrefab = null)
        {
            if (radius <= 0f || duration <= 0f) return null;
            var field = PoolManager.RentComponent<GravityField>(center);
            field.lens = null; field.lensRenderer = null;
            field.structures.Clear();
            field.remaining = field.duration = duration;
            // One range query per impact, deduplicated across merged collision boxes.
            var structures = field.structures;
            int count = CombatPhysicsQueries.OverlapSphere(center, radius, layers & ~LayerMask.GetMask("Energy"), out var overlaps);
            for (int i = 0; i < count; i++)
            {
                var collider = overlaps[i];
                if ((owner != null && collider.transform.IsChildOf(owner)) ||
                    (weapon != null && collider.transform.IsChildOf(weapon))) continue;
                var structure = collider.GetComponentInParent<DestructibleTarget>();
                if (structure != null) structures.Add(structure);
            }
            foreach (var structure in structures) structure.AbsorbSphere(center, radius);
            structures.Clear();
            field.gameObject.SetActive(true);
            WorkshopSoundEffects.RaiseGravityFieldStarted(field);
            if (effectPrefab != null)
            {
                var effect = PoolManager.RentPrefab(effectPrefab.gameObject, center, Quaternion.identity, field.transform);
                effect.transform.localScale = Vector3.one * radius;
                effect.GetComponent<PooledCombatObject>().PlayParticles();
            }
            if (distortionPrefab != null)
            {
                var visual = PoolManager.RentPrefab(distortionPrefab, center, Quaternion.identity, field.transform);
                visual.SetActive(true);
                visual.transform.localScale = Vector3.one * (radius * 2f);
                field.lens = visual.transform;
                field.lensRenderer = visual.GetComponentInChildren<Renderer>();
                if (field.appearance == null) field.appearance = new MaterialPropertyBlock();
                field.appearance.Clear();
                field.appearance.SetFloat("_Opacity", 0f);
                field.lensRenderer?.SetPropertyBlock(field.appearance);
            }
            return field;
        }

        private void OnDisable()
        {
            WorkshopSoundEffects.RaiseGravityFieldStopped(this);
            remaining = duration = 0f; lens = null; lensRenderer = null; structures.Clear();
            appearance?.Clear();
        }
        private void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f) { PoolManager.Release(gameObject); return; }
            if (lens == null || lensRenderer == null) return;
            float elapsed = duration - remaining;
            float fade = Mathf.Min(Mathf.Clamp01(elapsed / 0.1f), Mathf.Clamp01(remaining / 0.3f));
            lensRenderer.GetPropertyBlock(appearance);
            appearance.SetFloat("_Opacity", fade);
            lensRenderer.SetPropertyBlock(appearance);
        }
    }
}
