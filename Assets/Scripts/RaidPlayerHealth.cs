using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonPlayer))]
    public sealed class RaidPlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float maximumHealth = 3f;
        [SerializeField, Min(0f)] private float invulnerabilitySeconds = 1f;
        [SerializeField, Min(0.01f)] private float knockbackDuration = 0.25f;
        private CharacterController controller;
        private float nextDamageTime;
        private Vector3 knockback;
        private float knockbackRemaining;
        public float CurrentHealth { get; private set; }
        public float MaximumHealth => maximumHealth;
        public bool IsDead => CurrentHealth <= 0f;
        public event System.Action Changed;
        public event System.Action<Vector3> HitReceived;
        public event System.Action Died;
        private void Awake() { controller = GetComponent<CharacterController>(); ResetHealth(); }
        public void ResetHealth()
        {
            CurrentHealth = maximumHealth; nextDamageTime = Time.time + invulnerabilitySeconds;
            knockback = Vector3.zero; knockbackRemaining = 0f; Changed?.Invoke();
        }
        public void ApplyHit(float damage, Vector3 push, Vector3 sourceDirection)
        {
            bool training = TutorialManager.TrainingRaidPending && NightRaidController.Instance != null &&
                NightRaidController.Instance.IsInRaid;
            if (IsDead || Time.time < nextDamageTime || damage <= 0f) return;
            nextDamageTime = Time.time + invulnerabilitySeconds;
            if (!training) CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            // Snap float residue so e.g. 3 - 0.5 x 6 reads as exactly dead.
            if (CurrentHealth < 0.0001f) CurrentHealth = 0f;
            knockback = push / knockbackDuration; knockbackRemaining = knockbackDuration;
            HitReceived?.Invoke(sourceDirection);
            Changed?.Invoke();
            if (IsDead) Died?.Invoke();
        }
        private void LateUpdate()
        {
            if (knockbackRemaining <= 0f || controller == null || !controller.enabled || GameManager.GameplayBlocked) return;
            float step = Mathf.Min(Time.deltaTime, knockbackRemaining);
            controller.Move(knockback * step); knockbackRemaining -= step;
        }
    }
}
