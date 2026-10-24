using UnityEngine;

namespace WeaponWorkshop
{
    // 튜토리얼 버튼 강조의 기존 연결점(브리지). 직렬화 필드와 커버 템플릿 참조 호환을 유지한다.
    // 커버 풀 표시는 TutorialButtonHighlightView, 강조 대상 선택은 TutorialButtonHighlightPresenter가 담당한다.
    public sealed class TutorialButtonHighlightUI : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image cover;
        [SerializeField] private TutorialButtonHighlightView m_view;

        private TutorialButtonHighlightPresenter m_presenter;

        private void Awake()
        {
            if (m_view == null)
            {
                Debug.LogError(
                    "[TutorialButtonHighlightUI] Awake 실패: m_view에 TutorialButtonHighlightView 참조가 필요합니다. " +
                    "Tools/UI/Authoring/TutorialHud/Author 실행 결과를 확인하세요.",
                    this);
                if (cover != null)
                {
                    cover.raycastTarget = false;
                    cover.gameObject.SetActive(false);
                }

                return;
            }

            m_view.Configure(cover);
            m_presenter = new TutorialButtonHighlightPresenter(m_view,
                GetComponentInChildren<TutorialSkipUI>(true),
                GetComponentInChildren<DialogueUI>(true),
                GetComponentInChildren<FeedbackUI>(true),
                GetComponentInChildren<WeaponRecipeUI>(true),
                GetComponentInChildren<PresetPanelUI>(true),
                GetComponentInChildren<NightRaidUI>(true),
                GetComponentInChildren<ModulePaletteUI>(true));
            if (m_presenter.Initialize())
            {
                return;
            }

            m_presenter = null;
        }

        private void LateUpdate()
        {
            m_presenter?.Tick();
        }

        private void OnDisable()
        {
            if (m_presenter != null)
            {
                m_presenter.OnDisabled();
                return;
            }

            if (cover != null)
            {
                cover.enabled = false;
            }
        }

        private void OnDestroy()
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
