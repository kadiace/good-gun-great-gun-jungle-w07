using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 엔드 상자 엔딩 화면의 기존 연결점(브리지). EndChest가 구동하는 SetWhite/SetEnd 공개 API와 직렬화 필드 호환을 유지한다.
    // 표시는 EndChestEndingView, 로그·계속/Intro 선택은 EndChestEndingPresenter가 담당한다.
    [DisallowMultipleComponent]
    public sealed class EndChestEndingUI : MonoBehaviour
    {
        [SerializeField] private EndChest chest;
        [SerializeField] private CanvasGroup whiteGroup;
        [SerializeField] private CanvasGroup endGroup;
        [SerializeField] private CanvasGroup buttonsGroup;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private string mainMenuScene = "Intro";
        [SerializeField] private EndChestEndingView m_view;

        private EndChestEndingPresenter m_presenter;

        private void Awake()
        {
            EndChestEndingView view = ResolveView();
            if (view == null)
            {
                Debug.LogError(
                    "[EndChestEndingUI] Awake 실패: EndChestEndingView 참조가 없습니다. " +
                    "Tools/UI/Authoring/Ending/Author 실행 결과를 확인하세요.", this);
                return;
            }

            view.Configure(whiteGroup, endGroup, buttonsGroup, continueButton, quitButton);
            m_presenter = new EndChestEndingPresenter(view, chest, mainMenuScene);
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

        public void SetWhite(float alpha)
        {
            m_presenter?.SetWhite(alpha);
        }

        public void SetEnd(float alpha)
        {
            m_presenter?.SetEnd(alpha);
        }

        private EndChestEndingView ResolveView()
        {
            return m_view != null ? m_view : GetComponent<EndChestEndingView>();
        }
    }
}
