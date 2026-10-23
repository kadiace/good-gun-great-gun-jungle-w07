using UnityEngine;

namespace WeaponWorkshop
{
    // 전투 HUD 브리지. 기존 직렬화 참조·public API를 유지하고 실효 참조를 AimHudView로 전달한다.
    // 무기·스코프·유도 상태 판정은 AimHudPresenter가 맡는다.
    public sealed class AimUI : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private GameObject dot;
        [SerializeField] private SpreadCrosshair crosshair;
        [SerializeField] private HomingLockGraphic homingLockMarker;
        [SerializeField] private HomingRetentionRing homingRetentionRing;
        [SerializeField] private DamageDirectionIndicator damageDirectionIndicator;
        [SerializeField] private AimHudView m_view;

        private AimHudPresenter m_presenter;
        private bool m_hasLoggedViewError;
        private bool m_hasInitializationFailed;

        public void Initialize(UIManager owner)
        {
            uiManager = owner;
            m_hasInitializationFailed = false;
            DisposePresenter();
            EnsurePresenter();
            enabled = true;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        private void Awake()
        {
            EnsurePresenter();
        }

        private void OnDisable()
        {
            m_view?.HideAll();
            m_presenter?.Suspend();
        }

        private void OnDestroy()
        {
            DisposePresenter();
        }

        private void LateUpdate()
        {
            if (m_presenter != null)
            {
                m_presenter.Refresh();
                return;
            }

            if (!m_hasInitializationFailed)
            {
                EnsurePresenter();
            }
        }

        private void EnsurePresenter()
        {
            if (m_presenter != null)
            {
                return;
            }

            if (m_view == null)
            {
                if (!m_hasLoggedViewError)
                {
                    m_hasLoggedViewError = true;
                    Debug.LogError(
                        "[AimUI] 초기화 실패: m_view에 AimHudView 참조가 필요합니다. " +
                        "Tools/UI/Authoring/CombatHud/Author 실행 결과를 확인하세요.", this);
                }

                return;
            }

            // UIManager.Awake가 Initialize(owner)로 넘겨주기 전에는 도메인 참조가 없을 수 있다.
            if (uiManager == null)
            {
                return;
            }

            m_view.Configure(dot, crosshair, homingLockMarker, homingRetentionRing, damageDirectionIndicator);
            m_presenter = new AimHudPresenter(m_view, uiManager, damageDirectionIndicator);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
                m_hasInitializationFailed = true;
            }
        }

        private void DisposePresenter()
        {
            if (m_presenter == null)
            {
                return;
            }

            m_presenter.Dispose();
            m_presenter = null;
        }
    }
}
