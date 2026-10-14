using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent, RequireComponent(typeof(FirstPersonPlayer)), DefaultExecutionOrder(55)]
    public sealed class PlayerScanMode : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField, Min(0f)] private float scanDistance = 50f;
        [SerializeField, Min(0f)] private float scanDuration = 0.5f;
        private float hoverElapsed;
        public float ScanProgress => !HasTarget ? 0f : scanDuration <= 0f ? 1f : Mathf.Clamp01(hoverElapsed / scanDuration);
        public bool ScanComplete => HasTarget && ScanProgress >= 1f;
        public float CompletedElapsed => Mathf.Max(0f, hoverElapsed - scanDuration);
        private InputAction scanHold;
        private Interaction interaction;
        public bool IsScanning { get; private set; }
        public DestructibleTarget Target { get; private set; }
        public EndChest ChestTarget { get; private set; }
        public bool HasTarget => Target != null || ChestTarget != null;
        public Color TargetColor { get; private set; } = Color.white;
        private void Awake()
        {
            IsScanning = false;
            Target = null;
            ChestTarget = null;
            hoverElapsed = 0f;
            interaction = GetComponentInChildren<Interaction>(true);
            if (inputActions != null) scanHold = inputActions.FindAction("Player/Scan", true).Clone();
        }
        private void OnEnable() => scanHold?.Enable();
        private void OnDisable()
        {
            scanHold?.Disable();
            IsScanning = false;
            Target = null;
            ChestTarget = null;
            hoverElapsed = 0f;
        }
        private void OnDestroy() => scanHold?.Dispose();
        private void Update()
        {
            if (GameManager.GameplayBlocked) { IsScanning = false; Target = null; ChestTarget = null; hoverElapsed = 0f; return; }
            IsScanning = Application.isFocused && scanHold != null && scanHold.IsPressed();
            if (!IsScanning) { Target = null; ChestTarget = null; hoverElapsed = 0f; }
        }
        private void LateUpdate()
        {
            var previous = Target;
            var previousChest = ChestTarget;
            Target = null;
            ChestTarget = null;
            if (!IsScanning || interaction == null || !interaction.TryGetViewedHit(scanDistance, out var hit, true))
            { hoverElapsed = 0f; return; }
            var target = hit.collider.GetComponentInParent<DestructibleTarget>();
            if (target != null && target.isActiveAndEnabled)
            {
                Target = target;
                if (previous != target) hoverElapsed = 0f;
                else hoverElapsed += Time.deltaTime;
                UpdateTargetColor(hit, target);
            }
            else
            {
                var chest = hit.collider.GetComponentInParent<EndChest>();
                if (chest != null && chest.isActiveAndEnabled && chest.IsClosed)
                {
                    ChestTarget = chest;
                    if (previousChest != chest) hoverElapsed = 0f;
                    else hoverElapsed += Time.deltaTime;
                    UpdateTargetColor(hit, chest);
                }
                else hoverElapsed = 0f;
            }
        }
        private void UpdateTargetColor(RaycastHit hit, Component target)
        {
            var renderer = hit.collider.GetComponent<Renderer>() ?? target.GetComponentInChildren<Renderer>(true);
            var material = renderer != null ? renderer.sharedMaterial : null;
            Color color = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") :
                material != null && material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            color.a = 1f;
            TargetColor = color;
        }
    }
}
