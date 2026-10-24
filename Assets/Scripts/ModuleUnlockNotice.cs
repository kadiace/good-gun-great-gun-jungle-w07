using UnityEngine;

namespace WeaponWorkshop
{
    // UIManager 루트의 모듈 해금 알림 브리지. 기존 직렬화 필드(uiManager)를 유지하고
    // 해금 이벤트 구독 및 알림 표시는 ModuleUnlockNoticePresenter와 ModuleUnlockNoticeView가 담당한다.
    public sealed class ModuleUnlockNotice : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private ModuleUnlockNoticeView m_view;

        private PlayerModuleUnlocks m_unlocks;
        private ModuleUnlockNoticePresenter m_presenter;

        private void Awake()
        {
            if (m_view == null)
            {
                m_view = GetComponent<ModuleUnlockNoticeView>();
            }

            if (m_view == null)
            {
                Debug.LogError(
                    "[ModuleUnlockNotice] Initialize 실패: m_view에 ModuleUnlockNoticeView 참조가 필요합니다. " +
                    "UIManager.prefab의 같은 오브젝트에 View를 추가하고 연결하세요.", this);
                return;
            }

            if (uiManager == null)
            {
                uiManager = GetComponent<UIManager>();
            }

            m_view.BindReferences(uiManager);
        }

        private void Start()
        {
            EnsurePresenter();
        }

        private void OnEnable()
        {
            EnsurePresenter();
            m_presenter?.Subscribe();
        }

        private void OnDisable()
        {
            m_presenter?.Unsubscribe();
        }

        private void OnDestroy()
        {
            if (m_presenter != null)
            {
                m_presenter.Dispose();
                m_presenter = null;
            }
        }

        private void EnsurePresenter()
        {
            if (m_view == null)
            {
                return;
            }

            if (m_unlocks == null && uiManager != null && uiManager.Player != null)
            {
                m_unlocks = uiManager.Player.GetComponent<PlayerModuleUnlocks>();
            }

            if (m_presenter == null)
            {
                m_presenter = new ModuleUnlockNoticePresenter(m_view, m_unlocks);
                m_presenter.Initialize();
            }
            else if (m_unlocks != null)
            {
                m_presenter.BindUnlocks(m_unlocks);
            }
        }
    }
}
