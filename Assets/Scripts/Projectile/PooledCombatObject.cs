using UnityEngine;
using System.Collections.Generic;

namespace WeaponWorkshop
{
    // Cached original appearance and explicit lifetime for one leased object.
    public sealed class PooledCombatObject : MonoBehaviour
    {
        public PoolManager Manager { get; private set; }
        internal PoolManager.Pool OwnerPool { get; private set; }
        public GameObject SourcePrefab => OwnerPool?.prefab;
        private ParticleSystem[] particles;
        private ParticleSystemRenderer[] particleRenderers;
        private Renderer[] renderers;
        private Material[][] originalMaterials;
        private Material[] originalTrailMaterials;
        private TrailRenderer[] trails;
        private VisualDebrisCloud debrisCloud;
        private readonly List<PooledCombatObject> pooledChildren = new List<PooledCombatObject>();
        private Vector3 originalScale;
        private bool autoReturn, playPending;
        private float remaining, age;

        internal void Initialize(PoolManager manager, PoolManager.Pool pool)
        {
            Manager = manager; OwnerPool = pool;
            originalScale = transform.localScale;
            TryGetComponent(out debrisCloud);
            pooledChildren.Clear();
            foreach (var child in GetComponentsInChildren<PooledCombatObject>(true))
                if (child != this && child.transform.parent == transform) pooledChildren.Add(child);
            particles = GetComponentsInChildren<ParticleSystem>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
            trails = GetComponentsInChildren<TrailRenderer>(true);
            originalMaterials = new Material[renderers.Length][];
            originalTrailMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalMaterials[i] = renderers[i].sharedMaterials;
                if (renderers[i] is ParticleSystemRenderer renderer) originalTrailMaterials[i] = renderer.trailMaterial;
            }
            foreach (var ps in particles)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.None;
                main.playOnAwake = false;
            }
            enabled = false;
        }
        internal VisualDebrisCloud DebrisCloud => debrisCloud;
        internal ParticleSystem[] Particles => particles;
        internal ParticleSystemRenderer[] ParticleRenderers => particleRenderers;
        internal IReadOnlyList<PooledCombatObject> PooledChildren => pooledChildren;
        internal void RegisterPooledChild(PooledCombatObject child)
        {
            if (child != null && child != this && !pooledChildren.Contains(child)) pooledChildren.Add(child);
        }
        internal void UnregisterPooledChild(PooledCombatObject child) => pooledChildren.Remove(child);
        internal void SetScaleMultiplier(float multiplier)
            => transform.localScale = originalScale * Mathf.Max(1f, multiplier);
        internal void ResetForRent()
        {
            transform.localScale = originalScale;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].sharedMaterials = originalMaterials[i];
                renderers[i].SetPropertyBlock(null);
                if (renderers[i] is ParticleSystemRenderer renderer) renderer.trailMaterial = originalTrailMaterials[i];
            }
            ResetForReturn();
        }
        internal void ResetForReturn()
        {
            transform.localScale = originalScale;
            autoReturn = playPending = false; age = remaining = 0f; enabled = false;
            foreach (var ps in particles) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var trail in trails) if (trail != null) trail.Clear();
        }
        public void PlayParticles(bool returnWhenFinished = false, float safetyLifetime = 10f)
        {
            autoReturn = returnWhenFinished;
            remaining = Mathf.Max(0.05f, safetyLifetime);
            age = 0f;
            playPending = true;
            enabled = true;
            gameObject.SetActive(true);
            if (gameObject.activeInHierarchy) BeginParticles();
        }
        private void OnEnable() { if (playPending) BeginParticles(); }
        private void BeginParticles()
        {
            if (!playPending) return;
            playPending = false;
            foreach (var ps in particles)
                if (ps != null && ps.gameObject.activeInHierarchy) ps.Play(false);
            if (!autoReturn) enabled = false;
        }
        private void Update()
        {
            if (!autoReturn || Time.deltaTime <= 0f) return;
            remaining -= Time.deltaTime; age += Time.deltaTime;
            if (remaining > 0f)
            {
                if (age < 0.05f) return;
                foreach (var ps in particles) if (ps != null && ps.IsAlive(false)) return;
            }
            PoolManager.Release(gameObject);
        }
    }
}
