using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 은퇴 화면의 기존 연결점(브리지). 직렬화 필드·Cost·IsOpen·씬 오버라이드 호환을 유지한다.
    // 표시·기본 선택은 RetireView, 로그·씬 이동은 RetirePresenter가 담당하며 실행 시 실효 참조를 View로 전달한다.
    public sealed class RetireUI : MonoBehaviour
    {
        public const int Cost = 250000;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text continueText;
        [SerializeField] private Button lobbyButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private _DayManager dayManager;
        [SerializeField] private RetireView m_view;

        private RetirePresenter m_presenter;

        public bool IsOpen => panel != null && panel.activeSelf;
        public _DayManager DayManager => dayManager;

        private void Awake()
        {
            // 씬에 저장된 활성 상태와 무관하게 닫힌 상태로 시작한다.
            if (panel != null)
            {
                panel.SetActive(false);
            }

            RetireView view = ResolveView();
            if (view == null)
            {
                Debug.LogError(
                    "[RetireUI] Awake 실패: RetireView 참조가 없습니다. " +
                    "Tools/UI/Authoring/Ending/Author 실행 결과를 확인하세요.", this);
                return;
            }

            view.Configure(panel, continueText, lobbyButton, continueButton);
            m_presenter = new RetirePresenter(view, dayManager);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
            }
        }

        private void OnDestroy()
        {
            m_presenter?.Dispose();
            m_presenter = null;
        }

        public void Open(bool firstEnding)
        {
            if (m_presenter == null)
            {
                return;
            }

            m_presenter.Open(firstEnding);
        }

        private RetireView ResolveView()
        {
            return m_view != null ? m_view : GetComponentInChildren<RetireView>(true);
        }
    }
}
