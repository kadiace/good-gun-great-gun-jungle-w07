using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class AimUI : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private GameObject dot;
        [SerializeField] private SpreadCrosshair crosshair;
        [SerializeField] private HomingLockGraphic homingLockMarker;
        [SerializeField] private HomingRetentionRing homingRetentionRing;
        [SerializeField] private DamageDirectionIndicator damageDirectionIndicator;
        private PlayerHomingLock homingLock;
        public void Initialize(UIManager owner)
        {
            uiManager = owner;
            Hide();
            // Hidden presentation containers must not prevent the controller from running.
            ActivateParents(dot != null ? dot.transform : null);
            ActivateParents(crosshair != null ? crosshair.transform : null);
            ActivateParents(homingLockMarker != null ? homingLockMarker.transform : null);
            ActivateParents(homingRetentionRing != null ? homingRetentionRing.transform : null);
            ActivateParents(damageDirectionIndicator != null ? damageDirectionIndicator.transform : null);
            if (damageDirectionIndicator != null) damageDirectionIndicator.Initialize(owner);
            enabled = true;
            gameObject.SetActive(true);
        }
        private void ActivateParents(Transform element)
        {
            if (element == null) return;
            for (var parent = element.parent; parent != null && parent.IsChildOf(transform); parent = parent.parent)
                parent.gameObject.SetActive(true);
        }
        private void Awake() => Hide();
        private void OnDisable() => Hide();
        private void Hide()
        {
            if (dot != null) dot.SetActive(false);
            if (crosshair != null) { crosshair.SetState(false, null, null); crosshair.gameObject.SetActive(false); }
            if (damageDirectionIndicator != null) damageDirectionIndicator.Hide();
            HideHomingLock();
        }
        private void LateUpdate()
        {
            var holder = uiManager != null ? uiManager.PlayerWeaponHolder : null;
            var player = uiManager != null ? uiManager.Player : null;
            bool available = player != null && player.isActiveAndEnabled && !GameManager.GameplayBlocked;
            var scope = holder != null ? holder.GetComponent<WeaponScope>() : null;
            bool aiming = scope != null && scope.IsAiming;
            if (dot != null) dot.SetActive(available && !aiming);
            var weapon = holder != null ? holder.EquippedWeapon : null;
            var view = holder != null ? holder.AimCamera : null;
            if (crosshair != null)
            {
                bool show = available && !aiming && weapon != null && weapon.IsEquipped && view != null;
                crosshair.gameObject.SetActive(show);
                crosshair.SetState(show, weapon, view);
            }
            UpdateHomingLock();
        }
        private void UpdateHomingLock()
        {
            var holder = uiManager != null ? uiManager.PlayerWeaponHolder : null;
            if (homingLockMarker == null || holder == null || holder.AimCamera == null ||
                GameManager.GameplayBlocked || holder.EquippedWeapon == null || !holder.EquippedWeapon.HasHoming)
            { HideHomingLock(); return; }
            if (homingLock == null || homingLock.gameObject != holder.gameObject)
                homingLock = holder.GetComponent<PlayerHomingLock>();
            var position = Vector2.zero;
            float progress = 0f;
            if (homingLock != null && homingLock.Target != null)
            {
                Vector3 screen = holder.AimCamera.WorldToScreenPoint(homingLock.AimPoint);
                if (screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height)
                {
                    var canvas = homingLockMarker.canvas;
                    Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(homingLockMarker.rectTransform.parent as RectTransform,
                        screen, uiCamera, out position);
                    progress = homingLock.Progress;
                }
            }
            homingLockMarker.gameObject.SetActive(true);
            homingLockMarker.rectTransform.anchoredPosition = position;
            homingLockMarker.SetProgress(progress);
            if (homingRetentionRing != null)
            {
                bool locked = homingLock != null && homingLock.LockedTarget != null;
                homingRetentionRing.gameObject.SetActive(locked);
                if (locked)
                {
                    var canvas = homingRetentionRing.canvas;
                    float scale = canvas != null ? Mathf.Max(0.001f, canvas.scaleFactor) : 1f;
                    homingRetentionRing.SetRadius(homingLock.RetentionRadiusPixels / scale);
                }
            }
        }
        private void HideHomingLock()
        {
            if (homingLockMarker != null) homingLockMarker.gameObject.SetActive(false);
            if (homingRetentionRing != null) homingRetentionRing.gameObject.SetActive(false);
        }
    }
}
