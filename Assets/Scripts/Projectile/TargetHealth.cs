using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class TargetHealth : MonoBehaviour
    {
        public event System.Action Destroyed;
        private const float BurnTickInterval = 0.5f;

        [SerializeField, Min(1f), Tooltip("임시 테스트값. 표적 체력 기획 확정 후 조정합니다.")]
        private float maximumHealth = 100f;
        [SerializeField, Tooltip("피격 즉시 파괴. 피해량·남은 체력과 무관하며 재화 상자에 사용합니다.")]
        private bool destroyOnImpact;

        private float currentHealth;
        private bool isDead;
        // Burn stacks: each fire hit adds a stack (up to a cap) and refreshes the duration.
        private int burnStacks;
        private float burnDamagePerStack;
        private float burnRemaining;
        private float burnTickTimer;
        private ParticleSystem burnEffect;
        private float burnEffectBaseRate;

        public float MaximumHealth => maximumHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => isDead;
        public int BurnStacks => burnStacks;
        public event System.Action Died;

        // Center of the target's colliders. The transform pivot may sit on the floor, so aim here instead.
        public Vector3 AimPoint
        {
            get
            {
                Bounds bounds = default;
                bool hasBounds = false;
                foreach (Collider collider in GetComponentsInChildren<Collider>())
                {
                    if (!collider.enabled) continue;
                    if (hasBounds) bounds.Encapsulate(collider.bounds);
                    else { bounds = collider.bounds; hasBounds = true; }
                }
                return hasBounds ? bounds.center : transform.position;
            }
        }

        private void Awake()
        {
            currentHealth = Mathf.Max(1f, maximumHealth);
            isDead = false;
            burnStacks = 0;
        }

        public void ApplyDamage(float amount)
        {
            if (isDead || amount <= 0f) return;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            if (currentHealth <= 0f) Die();
        }

        /// <summary>Destroys the target regardless of remaining health.</summary>
        public void Kill() => Die();

        public void ApplyImpact(float amount)
        {
            if (isDead) return;
            if (destroyOnImpact) Die();
            else ApplyDamage(amount);
        }

        public void AddBurn(float damagePerSecondPerStack, float duration, int maxStacks,
            ParticleSystem burnEffectPrefab = null)
        {
            if (isDead || damagePerSecondPerStack <= 0f || duration <= 0f) return;
            burnStacks = Mathf.Min(Mathf.Max(1, maxStacks), burnStacks + 1);
            burnDamagePerStack = Mathf.Max(burnDamagePerStack, damagePerSecondPerStack);
            burnRemaining = duration;
            if (burnEffect == null && burnEffectPrefab != null) burnEffect = CreateBurnEffect(burnEffectPrefab);
            UpdateBurnEffect();
        }

        /// <summary>Water puts the fire out.</summary>
        public void Extinguish()
        {
            if (burnStacks <= 0) return;
            burnStacks = 0;
            burnRemaining = 0f;
            burnDamagePerStack = 0f;
            StopBurnEffect();
        }

        private void Update()
        {
            if (isDead || burnStacks <= 0) return;
            float deltaTime = Time.deltaTime;
            burnRemaining -= deltaTime;
            burnTickTimer += deltaTime;
            if (burnTickTimer >= BurnTickInterval)
            {
                burnTickTimer -= BurnTickInterval;
                ApplyDamage(burnDamagePerStack * burnStacks * BurnTickInterval);
                if (isDead) return;
            }
            if (burnRemaining <= 0f) Extinguish();
        }

        private void UpdateBurnEffect()
        {
            if (burnEffect == null) return;
            var emission = burnEffect.emission;
            emission.rateOverTime = burnEffectBaseRate * burnStacks;
            if (!burnEffect.isEmitting) burnEffect.Play(true);
        }

        private ParticleSystem CreateBurnEffect(ParticleSystem prefab)
        {
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool hasBounds = false;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer) continue;
                if (hasBounds) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; hasBounds = true; }
            }
            ParticleSystem effect = Instantiate(prefab, bounds.center, Quaternion.identity, transform);
            // The effect prefab is authored for a 1m cube and scales with the hierarchy.
            Vector3 parentScale = transform.lossyScale;
            effect.transform.localScale = new Vector3(
                bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
            burnEffectBaseRate = effect.emission.rateOverTime.constantMax;
            effect.gameObject.SetActive(true);
            effect.Play(true);
            return effect;
        }

        private void StopBurnEffect()
        {
            if (burnEffect == null) return;
            burnEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(burnEffect.gameObject, 1f);
            burnEffect = null;
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;
            Destroyed?.Invoke();
            burnStacks = 0;
            Died?.Invoke();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(1f, maximumHealth);
        }
    }
}
