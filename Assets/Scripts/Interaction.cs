using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(Camera)), DefaultExecutionOrder(50)]
    public sealed class Interaction : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private FirstPersonPlayer player;
        [SerializeField] private HoverOutline hoverOutline;
        [SerializeField, Min(0f)] private float interactionDistance = 2.5f;
        [SerializeField] private LayerMask interactionLayers = Physics.DefaultRaycastLayers;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly List<MonoBehaviour> targetComponents = new List<MonoBehaviour>();
        private Camera view;
        private InputActionMap controls;
        private InputAction point;
        private IInteractable aimedTarget;
        public FirstPersonPlayer Player => player;
        public IInteractable AimedTarget => aimedTarget;
        public bool CanInteractTarget => aimedTarget is MonoBehaviour component && component.isActiveAndEnabled &&
            (TutorialManager.Instance == null || TutorialManager.Instance.AllowsInteraction(aimedTarget)) && aimedTarget.CanInteract(this);
        public EquipableWeapon HoveredWeapon { get; private set; }
        public Vector2 HoverPointer => view != null && Cursor.lockState == CursorLockMode.Locked ? view.pixelRect.center : point != null ? point.ReadValue<Vector2>() : Vector2.zero;

        private void Awake()
        {
            interactionLayers |= LayerMask.GetMask("Weapon", "DroppedGun");
            view = GetComponent<Camera>();
            if (inputActions == null || player == null) { enabled = false; return; }
            controls = inputActions.FindActionMap("Workshop", true).Clone();
            point = controls.FindAction("Point", true);
            controls.FindAction("Interact", true).performed += _ => TryInteract();
        }
        private void OnEnable() => controls?.Enable();
        private void OnDisable()
        {
            controls?.Disable();
            aimedTarget = null;
            HoveredWeapon = null;
            if (hoverOutline != null) hoverOutline.SetTarget(null);
        }
        private void OnDestroy() => controls?.Dispose();

        public bool TryInteract()
        {
            if (GameManager.GameplayBlocked) return false;
            if (player == null || !player.enabled) return false;
            RefreshTarget();
            // The player-owned mode decides whether this input belongs to a temporary interaction.
            if (player.GetComponent<PlayerShootingTest>()?.TryRetry(aimedTarget) == true) return true;
            if (aimedTarget != null && TutorialManager.Instance != null && !TutorialManager.Instance.TryAuthorizeInteraction(aimedTarget)) return false;
            bool interacted = CanInteractTarget && aimedTarget.Interact(this);
            if (interacted) RefreshTarget();
            return interacted;
        }

        public void RefreshTarget()
        {
            aimedTarget = null;
            HoveredWeapon = null;
            if (hoverOutline != null) hoverOutline.SetTarget(null);
            if (!TryGetViewedHit(interactionDistance, out var hit)) return;
            Transform hitTransform = hit.collider.transform;
            HoveredWeapon = hitTransform.GetComponentInParent<EquipableWeapon>();
            IInteractable target = null;
            hitTransform.GetComponentsInParent(false, targetComponents);
            foreach (MonoBehaviour component in targetComponents)
                if (component != null && component is IInteractable interactable)
                { target = interactable; break; }
            if (target == null) return;
            aimedTarget = target;
            if (hoverOutline != null) hoverOutline.SetTarget(aimedTarget, CanInteractTarget);
        }

        public bool TryGetViewedHit(float maxDistance, out RaycastHit hit, bool includeAllLayers = false)
        {
            hit = default;
            if (!isActiveAndEnabled || !Application.isFocused || GameManager.GameplayBlocked || view == null || player == null || !player.isActiveAndEnabled || point == null) return false;
            Vector2 screenPoint = Cursor.lockState == CursorLockMode.Locked
                ? view.pixelRect.center : point.ReadValue<Vector2>();
            if (!view.pixelRect.Contains(screenPoint)) return false;
            DestructionCollisionBatch.Flush();
            int count = Physics.RaycastNonAlloc(view.ScreenPointToRay(screenPoint), hits,
                maxDistance, includeAllLayers ? Physics.DefaultRaycastLayers : interactionLayers.value, QueryTriggerInteraction.Ignore);
            int nearest = -1;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(player.transform) || hits[i].distance >= distance) continue;
                nearest = i;
                distance = hits[i].distance;
            }
            if (nearest < 0) return false;
            hit = hits[nearest];
            return true;
        }

        private void LateUpdate()
        {
            if (Application.isFocused && controls != null) RefreshTarget();
            else if (hoverOutline != null) hoverOutline.SetTarget(null);
        }
    }
}
