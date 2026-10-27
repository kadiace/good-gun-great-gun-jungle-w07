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
        private ShootingTestController controller;
        public int HitCount { get; private set; }

        internal void Bind(ShootingTestController owner)
        {
            controller = owner;
            HitCount = 0;
        }

        internal void NotifyBulletHit(Transform weaponSource)
        {
            if (controller != null && controller.CanCountBulletHit(this, weaponSource) && HitCount < controller.Definition.RequiredHits)
                HitCount++;
        }

        public string DisplayName => displayName;
        public bool IsProtected => protectedTarget;
        public DestructibleTarget Target => target != null ? target : target = GetComponent<DestructibleTarget>();
        public float Destruction => Target == null ? 0f : Mathf.Clamp01(1f - (float)Target.RemainingCellCount / Mathf.Max(1, Target.InitialCellCount));
        public bool ProtectionBroken => Target == null || !Target.gameObject.activeInHierarchy || Target.AttachedCellCount < Target.InitialCellCount;
        public string ResistanceDescription => Target == null ? "내성 정보 없음" :
            $"내성: 관통 {Target.PenetrationResistance:0.##} / 폭발 {Target.ExplosionResistance:0.##} / 열 {Target.HeatResistance:0.##} / 물 {Target.WaterResistance:0.##}";
        private void Awake() => target = GetComponent<DestructibleTarget>();
    }
}
