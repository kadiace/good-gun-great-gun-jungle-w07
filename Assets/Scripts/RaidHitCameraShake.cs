using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent, RequireComponent(typeof(RaidPlayerHealth))]
    [DefaultExecutionOrder(30)]
    public sealed class RaidHitCameraShake : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField, Min(0f), Tooltip("피격 시 카메라가 떨리는 시간(초). 0이면 끕니다.")]
        private float shakeDuration = 0.25f;
        [SerializeField, Min(0f), Tooltip("카메라 회전 떨림의 최대 각도.")]
        private float shakeStrength = 1f;
        [SerializeField, Min(0f)] private float shakeFrequency = 25f;

        private RaidPlayerHealth health;
        private float elapsed, duration, phase;
        private bool offsetApplied;
        private Quaternion baseRotation, shakenRotation;

        private void Awake()
        {
            health = GetComponent<RaidPlayerHealth>();
            if (cameraTransform == null)
            {
                var player = GetComponent<FirstPersonPlayer>();
                var camera = player.ViewPoint != null ? player.ViewPoint.GetComponentInChildren<Camera>(true) : null;
                if (camera != null) cameraTransform = camera.transform;
            }
            duration = 0f;
            offsetApplied = false;
        }

        private void OnEnable() => health.HitReceived += StartShake;
        private void OnDisable()
        {
            health.HitReceived -= StartShake;
            RemoveOffset();
            duration = 0f;
        }

        private void StartShake(Vector3 sourceDirection)
        {
            elapsed = 0f;
            duration = shakeDuration;
            phase = Random.value * Mathf.PI * 2f;
        }

        // Remove the previous visual offset before scope/workbench camera updates.
        private void Update() => RemoveOffset();

        private void LateUpdate()
        {
            if (cameraTransform == null || duration <= 0f || elapsed >= duration || Time.deltaTime <= 0f) return;
            float strength = shakeStrength * (1f - elapsed / duration);
            float angle = phase + elapsed * shakeFrequency * Mathf.PI * 2f;
            baseRotation = cameraTransform.localRotation;
            shakenRotation = baseRotation * Quaternion.Euler(
                Mathf.Sin(angle) * strength,
                Mathf.Sin(angle * 1.37f + 1f) * strength,
                Mathf.Sin(angle * 0.83f + 2f) * strength * 0.5f);
            cameraTransform.localRotation = shakenRotation;
            offsetApplied = true;
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
        }

        private void RemoveOffset()
        {
            // Another camera controller may already have replaced the pose.
            if (offsetApplied && cameraTransform != null && Quaternion.Angle(cameraTransform.localRotation, shakenRotation) < 0.001f)
                cameraTransform.localRotation = baseRotation;
            offsetApplied = false;
        }
    }
}
