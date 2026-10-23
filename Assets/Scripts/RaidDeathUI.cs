using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 레거시 습격 사망 패널의 기존 연결점(브리지). 체력 구독 수명주기와 직렬화 필드 호환을 유지한다.
    // 표시는 RaidDeathView, 컨트롤러 전달·게임 상태 전환은 RaidDeathPresenter가 담당한다.
    public sealed class RaidDeathUI : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button returnButton;
        [SerializeField] private RaidDeathView m_view;

        private RaidDeathPresenter m_presenter;

        private void Awake()
        {
            // 씬에 저장된 활성 상태와 무관하게 닫힌 상태로 시작한다.
            if (panel != null)
            {
                panel.SetActive(false);
            }

            RaidDeathView view = ResolveView();
            if (view == null)
            {
                Debug.LogError(
                    "[RaidDeathUI] Awake 실패: RaidDeathView 참조가 없습니다. " +
                    "Tools/UI/Authoring/Ending/Author 실행 결과를 확인하세요.", this);
                return;
            }

            view.Configure(panel, returnButton);
            m_presenter = new RaidDeathPresenter(view, uiManager);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
            }
        }

        private void OnEnable()
        {
            m_presenter?.HandleEnabled();
        }

        private void Start()
        {
            m_presenter?.MarkStarted();
        }

        private void OnDisable()
        {
            m_presenter?.HandleDisabled();
        }

        private void OnDestroy()
        {
            m_presenter?.Dispose();
            m_presenter = null;
        }

        private RaidDeathView ResolveView()
        {
            return m_view != null ? m_view : GetComponent<RaidDeathView>();
        }
    }
}
