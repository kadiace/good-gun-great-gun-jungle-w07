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
        private WeaponScope scopeCache;
        // 컴포넌트 조회는 홀더가 바뀔 때만 한다. 매 프레임 없는 컴포넌트를 찾으면(에디터 전용 오류 경로) 비용과 GC가 크다.
        private WeaponHolder scopeOwner;
        private WeaponHolder homingLockOwner;

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
            SetActive(dot, false);
            if (crosshair != null) { crosshair.SetState(false, null, null); SetActive(crosshair.gameObject, false); }
            if (damageDirectionIndicator != null) damageDirectionIndicator.Hide();
            HideHomingLock();
        }

        // 이미 같은 상태의 SetActive는 걸러 쓴다.
        private static void SetActive(GameObject target, bool state)
        {
            if (target != null && target.activeSelf != state) target.SetActive(state);
        }

        private void LateUpdate()
        {
            var holder = uiManager != null ? uiManager.PlayerWeaponHolder : null;
            var player = uiManager != null ? uiManager.Player : null;
            bool available = player != null && player.isActiveAndEnabled && !GameManager.GameplayBlocked;
            var scope = ScopeOf(holder);
            bool aiming = scope != null && scope.IsAiming;
            SetActive(dot, available && !aiming);
            var weapon = holder != null ? holder.EquippedWeapon : null;
            var view = holder != null ? holder.AimCamera : null;
            if (crosshair != null)
            {
                bool show = available && !aiming && weapon != null && weapon.IsEquipped && view != null;
                SetActive(crosshair.gameObject, show);
                crosshair.SetState(show, weapon, view);
            }
            UpdateHomingLock();
        }
        // 스코프·유도 잠금은 홀더에 항상 붙은 컴포넌트라, 없으면 매 프레임 조회해도 실패한다. 홀더가 같은 동안은 결과만 쓴다.
        private WeaponScope ScopeOf(WeaponHolder holder)
        {
            if (holder == null) return null;
            if (ReferenceEquals(scopeOwner, holder)) return scopeCache;
            scopeOwner = holder;
            if (!holder.TryGetComponent(out scopeCache)) scopeCache = null;
            return scopeCache;
        }

        private PlayerHomingLock HomingLockOf(WeaponHolder holder)
        {
            if (holder == null) return null;
            if (ReferenceEquals(homingLockOwner, holder)) return homingLock;
            homingLockOwner = holder;
            if (!holder.TryGetComponent(out homingLock)) homingLock = null;
            return homingLock;
        }

        private void UpdateHomingLock()
        {
            var holder = uiManager != null ? uiManager.PlayerWeaponHolder : null;
            if (homingLockMarker == null || holder == null || holder.AimCamera == null ||
                GameManager.GameplayBlocked || holder.EquippedWeapon == null || !holder.EquippedWeapon.HasHoming)
            { HideHomingLock(); return; }
            homingLock = HomingLockOf(holder);
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
            SetActive(homingLockMarker.gameObject, true);
            homingLockMarker.rectTransform.anchoredPosition = position;
            homingLockMarker.SetProgress(progress);
            if (homingRetentionRing != null)
            {
                bool locked = homingLock != null && homingLock.LockedTarget != null;
                SetActive(homingRetentionRing.gameObject, locked);
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
            if (homingLockMarker != null) SetActive(homingLockMarker.gameObject, false);
            if (homingRetentionRing != null) SetActive(homingRetentionRing.gameObject, false);
        }
    }
}
