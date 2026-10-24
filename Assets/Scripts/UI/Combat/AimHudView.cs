using UnityEngine;

namespace WeaponWorkshop
{
    // 전투 HUD(조준점·탄퍼짐 눈금·유도 잠금·피격 방향)의 저작된 참조와 표시만 담당한다.
    // 무기·스코프·유도 상태 판정은 AimHudPresenter가 맡는다.
    public sealed class AimHudView : MonoBehaviour
    {
        private const string SCREEN_NAME = "AimHudView";

        [SerializeField] private GameObject m_dot;
        [SerializeField] private SpreadCrosshair m_crosshair;
        [SerializeField] private HomingLockGraphic m_homingLockMarker;
        [SerializeField] private HomingRetentionRing m_homingRetentionRing;
        [SerializeField] private DamageDirectionIndicator m_damageIndicator;

        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        private void Awake()
        {
            // 브리지가 먼저 Initialize한 뒤 늦게 실행되는 Awake가 초기 상태를 다시 적용하지 않게 한다.
            if (!m_isInitialized)
            {
                ApplyHiddenState();
                ActivateContainers();
            }
        }

        /// <summary>
        /// 씬 오버라이드가 적용된 브리지의 실효 참조를 Initialize 전에 전달합니다.
        /// </summary>
        public void Configure(GameObject _dot, SpreadCrosshair _crosshair, HomingLockGraphic _homingLockMarker,
            HomingRetentionRing _homingRetentionRing, DamageDirectionIndicator _damageIndicator)
        {
            m_dot = _dot;
            m_crosshair = _crosshair;
            m_homingLockMarker = _homingLockMarker;
            m_homingRetentionRing = _homingRetentionRing;
            m_damageIndicator = _damageIndicator;
        }

        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            ApplyHiddenState();
            ActivateContainers();
            if (!ValidateReferences())
            {
                return false;
            }

            m_isInitialized = true;
            return true;
        }

        public void Dispose()
        {
            ApplyHiddenState();
            m_isInitialized = false;
        }

        public void SetDotVisible(bool _visible)
        {
            SetActive(m_dot, _visible);
        }

        public void SetCrosshair(bool _show, float _spreadDegrees, Camera _aimCamera)
        {
            if (m_crosshair == null)
            {
                return;
            }

            SetActive(m_crosshair.gameObject, _show);
            m_crosshair.SetState(_show, _spreadDegrees, _aimCamera);
        }

        /// <summary>
        /// 유도 잠금 표시를 월드 조준점 위치에 맞춰 표시합니다. 화면 밖이면 원점에 진행률 0으로 남깁니다.
        /// </summary>
        public void ShowHomingLock(bool _hasPoint, Vector3 _worldPoint, Camera _aimCamera, float _progress)
        {
            if (m_homingLockMarker == null)
            {
                return;
            }

            Vector2 position = Vector2.zero;
            float progress = 0f;
            if (_hasPoint && _aimCamera != null)
            {
                Vector3 screen = _aimCamera.WorldToScreenPoint(_worldPoint);
                if (screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width &&
                    screen.y >= 0f && screen.y <= Screen.height)
                {
                    Canvas markerCanvas = m_homingLockMarker.canvas;
                    Camera uiCamera = markerCanvas != null && markerCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                        ? markerCanvas.worldCamera
                        : null;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        m_homingLockMarker.rectTransform.parent as RectTransform, screen, uiCamera, out position);
                    progress = _progress;
                }
            }

            SetActive(m_homingLockMarker.gameObject, true);
            m_homingLockMarker.rectTransform.anchoredPosition = position;
            m_homingLockMarker.SetProgress(progress);
        }

        public void SetRetentionRing(bool _visible, float _radiusPixels)
        {
            if (m_homingRetentionRing == null)
            {
                return;
            }

            SetActive(m_homingRetentionRing.gameObject, _visible);
            if (!_visible)
            {
                return;
            }

            Canvas ringCanvas = m_homingRetentionRing.canvas;
            float scale = ringCanvas != null ? Mathf.Max(0.001f, ringCanvas.scaleFactor) : 1f;
            m_homingRetentionRing.SetRadius(_radiusPixels / scale);
        }

        public void HideHomingLock()
        {
            if (m_homingLockMarker != null)
            {
                SetActive(m_homingLockMarker.gameObject, false);
            }

            if (m_homingRetentionRing != null)
            {
                SetActive(m_homingRetentionRing.gameObject, false);
            }
        }

        public void HideAll()
        {
            ApplyHiddenState();
        }

        private void ApplyHiddenState()
        {
            SetActive(m_dot, false);
            if (m_crosshair != null)
            {
                m_crosshair.SetState(false, 0f, null);
                SetActive(m_crosshair.gameObject, false);
            }

            if (m_damageIndicator != null)
            {
                m_damageIndicator.Hide();
            }

            HideHomingLock();
        }

        // 숨긴 표시 컨테이너가 있어도 컴포넌트 갱신은 계속 돌아야 한다.
        private void ActivateContainers()
        {
            ActivateParents(m_dot != null ? m_dot.transform : null);
            ActivateParents(m_crosshair != null ? m_crosshair.transform : null);
            ActivateParents(m_homingLockMarker != null ? m_homingLockMarker.transform : null);
            ActivateParents(m_homingRetentionRing != null ? m_homingRetentionRing.transform : null);
            ActivateParents(m_damageIndicator != null ? m_damageIndicator.transform : null);
        }

        private void ActivateParents(Transform _element)
        {
            if (_element == null)
            {
                return;
            }

            for (Transform parent = _element.parent; parent != null && parent.IsChildOf(transform); parent = parent.parent)
            {
                parent.gameObject.SetActive(true);
            }
        }

        private static void SetActive(GameObject _target, bool _state)
        {
            if (_target != null && _target.activeSelf != _state)
            {
                _target.SetActive(_state);
            }
        }

        private bool ValidateReferences()
        {
            bool isValid = m_dot != null && m_crosshair != null && m_homingLockMarker != null &&
                m_homingRetentionRing != null && m_damageIndicator != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    $"[{SCREEN_NAME}] Initialize 실패: 필수 참조 누락 " +
                    $"(m_dot={m_dot != null}, m_crosshair={m_crosshair != null}, " +
                    $"m_homingLockMarker={m_homingLockMarker != null}, " +
                    $"m_homingRetentionRing={m_homingRetentionRing != null}, " +
                    $"m_damageIndicator={m_damageIndicator != null}). " +
                    "Tools/UI/Authoring/CombatHud/Author 실행 결과를 확인하세요.", this);
            }

            return isValid;
        }
    }
}
