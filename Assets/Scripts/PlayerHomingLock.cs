using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent, RequireComponent(typeof(WeaponHolder)), DefaultExecutionOrder(5)]
    public sealed class PlayerHomingLock : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float lockSeconds = 0.2f;
        [SerializeField, Range(0.05f, 0.5f), Tooltip("화면의 짧은 변 대비 락온 유지 원 반지름")]
        private float retentionRadius = 0.25f;
        private WeaponHolder holder;
        private EquipableWeapon weapon;
        private Component candidate;
        private Vector3 localAim;
        private float elapsed;
        private Component lockedTarget;
        private Vector3 lockedLocalAim;
        public Component Target => LockedTarget != null ? LockedTarget : IsAlive(candidate) ? candidate : null;
        public Component LockedTarget => IsAlive(lockedTarget) ? lockedTarget : null;
        public float Progress => LockedTarget != null ? 1f : Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lockSeconds));
        public Vector3 AimPoint => GetAimPoint(Target, LocalAim);
        public Vector3 LocalAim => LockedTarget != null ? lockedLocalAim : localAim;
        public float RetentionRadiusPixels => holder != null && holder.AimCamera != null
            ? Mathf.Min(holder.AimCamera.pixelWidth, holder.AimCamera.pixelHeight) * retentionRadius : 0f;
        private void Awake() { holder = GetComponent<WeaponHolder>(); Clear(); }
        private void OnDisable() => Clear();
        private void Clear() { candidate = lockedTarget = null; elapsed = 0f; weapon = null; }
        private bool WithinRetention(Component target, Vector3 localPoint)
        {
            if (!IsAlive(target) || holder.AimCamera == null) return false;
            Vector3 screen = holder.AimCamera.WorldToScreenPoint(GetAimPoint(target, localPoint));
            Vector2 offset = (Vector2)screen - holder.AimCamera.pixelRect.center;
            return screen.z > 0f && offset.sqrMagnitude <= RetentionRadiusPixels * RetentionRadiusPixels;
        }
        private void Update()
        {
            var equipped = holder.EquippedWeapon;
            if (GameManager.GameplayBlocked || equipped == null || equipped.IsMoving || !equipped.HasHoming)
            { Clear(); return; }
            if (weapon != equipped) { Clear(); weapon = equipped; }
            if (!WithinRetention(lockedTarget, lockedLocalAim)) lockedTarget = null;
            Component next = null;
            RaycastHit hit = default;
            if (holder.TryGetAimHit(out hit))
            {
                var core = RaidEnemyCore.FindForPart(hit.collider.transform);
                if (core != null)
                    next = core.GetComponent<TargetHealth>();
            }
            if (!IsAlive(next) || next == LockedTarget) { candidate = null; elapsed = 0f; return; }
            if (candidate != next) { elapsed = 0f; candidate = next; }
            localAim = candidate.transform.InverseTransformPoint(hit.point);
            elapsed = Mathf.Min(lockSeconds, elapsed + Time.deltaTime);
            if (elapsed >= lockSeconds && WithinRetention(candidate, localAim))
            {
                lockedTarget = candidate; lockedLocalAim = localAim;
                candidate = null; elapsed = 0f;
            }
        }
        public static bool IsAlive(Component target)
        {
            // A destroyed Unity component still matches a C# type pattern; check Unity null before native access.
            if (target == null || !(target is TargetHealth health) || !health.gameObject.activeInHierarchy || health.IsDead) return false;
            var core = health.GetComponent<RaidEnemyCore>();
            return core != null && core.isActiveAndEnabled && !core.IsDestroyed;
        }
        public static Vector3 GetAimPoint(Component target, Vector3 localPoint)
            => target == null ? Vector3.zero : target is TargetHealth health
                ? health.AimPoint : target.transform.TransformPoint(localPoint);
    }
}
