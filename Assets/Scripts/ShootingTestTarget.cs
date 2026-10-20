using UnityEngine;

namespace WeaponWorkshop
{
    // Each editable box owns its role. No hierarchy-name or material-based classification.
    [RequireComponent(typeof(DestructibleTarget))]
    public sealed class ShootingTestTarget : MonoBehaviour
    {
        [SerializeField] private string displayName;
        [SerializeField] private bool protectedTarget;
        private DestructibleTarget target;
        public string DisplayName => displayName;
        public bool IsProtected => protectedTarget;
        public DestructibleTarget Target => target != null ? target : target = GetComponent<DestructibleTarget>();
        public float Destruction => Target == null ? 0f : Mathf.Clamp01(1f - (float)Target.RemainingCellCount / Mathf.Max(1, Target.InitialCellCount));
        public bool ProtectionBroken => Target == null || !Target.gameObject.activeInHierarchy || Target.AttachedCellCount < Target.InitialCellCount;
        private void Awake() => target = GetComponent<DestructibleTarget>();
    }
}
