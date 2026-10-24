using UnityEngine;

namespace WeaponWorkshop
{
    // 전투 HUD의 도메인 조회(무기·스코프·유도 잠금)와 표시 요청만 담당한다.
    // 표시와 저작 참조 보관은 AimHudView가, 피격 방향 상태 계산은 DamageDirectionPresenter가 맡는다.
    public sealed class AimHudPresenter
    {
        private readonly AimHudView m_view;
        private readonly UIManager m_uiManager;
        private readonly DamageDirectionIndicator m_damageIndicator;

        private DamageDirectionPresenter m_damagePresenter;
        private PlayerHomingLock m_homingLockCache;
        private WeaponScope m_scopeCache;
        private WeaponHolder m_scopeOwner;
        private WeaponHolder m_homingLockOwner;
        private bool m_hasLoggedDependencyError;

        public AimHudPresenter(AimHudView _view, UIManager _uiManager, DamageDirectionIndicator _damageIndicator)
        {
            m_view = _view;
            m_uiManager = _uiManager;
            m_damageIndicator = _damageIndicator;
        }

        public bool Initialize()
        {
            if (m_view == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError(
                        "[AimHudPresenter] Initialize 실패: AimHudView 참조가 없습니다. " +
                        "Tools/UI/Authoring/CombatHud/Author 실행 결과를 확인하세요.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            if (m_damageIndicator != null)
            {
                m_damagePresenter = new DamageDirectionPresenter(m_damageIndicator);
                if (!m_damagePresenter.Initialize(m_uiManager))
                {
                    m_damagePresenter = null;
                }
            }

            return true;
        }

        public void Dispose()
        {
            if (m_damagePresenter != null)
            {
                m_damagePresenter.Dispose();
                m_damagePresenter = null;
            }

            if (m_view != null)
            {
                m_view.Dispose();
            }

            m_scopeOwner = null;
            m_scopeCache = null;
            m_homingLockOwner = null;
            m_homingLockCache = null;
        }

        public void Suspend()
        {
            m_damagePresenter?.Suspend();
        }

        public void Refresh()
        {
            if (m_view == null)
            {
                return;
            }

            FirstPersonPlayer player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            WeaponHolder holder = player != null ? player.GetComponent<WeaponHolder>() : null;
            bool available = player != null && player.isActiveAndEnabled && !GameManager.GameplayBlocked;
            WeaponScope scope = ScopeOf(holder);
            bool aiming = scope != null && scope.IsAiming;

            m_view.SetDotVisible(available && !aiming);

            EquipableWeapon weapon = holder != null ? holder.EquippedWeapon : null;
            Camera aimCamera = holder != null ? holder.AimCamera : null;
            bool showCrosshair = available && !aiming && weapon != null && weapon.IsEquipped && aimCamera != null;
            float spreadDegrees = showCrosshair ? weapon.SpreadDegrees : 0f;
            m_view.SetCrosshair(showCrosshair, spreadDegrees, aimCamera);

            UpdateHomingLock(holder);
            m_damagePresenter?.Refresh();
        }

        // 스코프·유도 잠금은 홀더에 항상 붙은 컴포넌트라, 없으면 매 프레임 조회해도 실패한다. 홀더가 같은 동안은 결과만 쓴다.
        private WeaponScope ScopeOf(WeaponHolder _holder)
        {
            if (_holder == null)
            {
                return null;
            }

            if (ReferenceEquals(m_scopeOwner, _holder))
            {
                return m_scopeCache;
            }

            m_scopeOwner = _holder;
            if (!_holder.TryGetComponent(out m_scopeCache))
            {
                m_scopeCache = null;
            }

            return m_scopeCache;
        }

        private PlayerHomingLock HomingLockOf(WeaponHolder _holder)
        {
            if (_holder == null)
            {
                return null;
            }

            if (ReferenceEquals(m_homingLockOwner, _holder))
            {
                return m_homingLockCache;
            }

            m_homingLockOwner = _holder;
            if (!_holder.TryGetComponent(out m_homingLockCache))
            {
                m_homingLockCache = null;
            }

            return m_homingLockCache;
        }

        private void UpdateHomingLock(WeaponHolder _holder)
        {
            if (m_uiManager == null || _holder == null || _holder.AimCamera == null ||
                GameManager.GameplayBlocked || _holder.EquippedWeapon == null || !_holder.EquippedWeapon.HasHoming)
            {
                m_view.HideHomingLock();
                return;
            }

            PlayerHomingLock homingLock = HomingLockOf(_holder);
            bool hasPoint = homingLock != null && homingLock.Target != null;
            m_view.ShowHomingLock(hasPoint,
                hasPoint ? homingLock.AimPoint : Vector3.zero,
                _holder.AimCamera,
                hasPoint ? homingLock.Progress : 0f);

            bool locked = homingLock != null && homingLock.LockedTarget != null;
            m_view.SetRetentionRing(locked, locked ? homingLock.RetentionRadiusPixels : 0f);
        }
    }
}
