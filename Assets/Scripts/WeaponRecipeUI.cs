using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace WeaponWorkshop
{
    /// <summary>
    /// 기존 UIManager·WorkshopSession 세션과 레시피 화면(RecipeView/RecipePresenter)을 연결하는 브리지입니다.
    /// 씬 오버라이드 보존을 위해 기존 직렬화 필드(panel·openButton·buttonLabel·weaponInfo·scroll·inlineLayout·recipes)를
    /// 그대로 유지하고, Awake에서 유효 참조를 View에 주입한 뒤 Presenter를 초기화합니다.
    /// 프레임 진입은 이 컴포넌트의 LateUpdate가 Presenter.Tick으로 전달하며 기존 public API(IsOpen·TutorialButton·Open·Close)는 유지합니다.
    /// </summary>
    public sealed class WeaponRecipeUI : MonoBehaviour
    {
        [SerializeField] private RecipeView m_view;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button openButton;
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private RectTransform weaponInfo;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private LayoutElement inlineLayout;
        [SerializeField] private RecipeView.RecipeEntry[] recipes = new RecipeView.RecipeEntry[0];

        private RecipePresenter m_presenter;

        public bool IsOpen => m_presenter != null && m_presenter.IsOpen;

        public RectTransform TutorialButton => m_view != null ? m_view.TutorialButton : null;

        private void Awake()
        {
            if (m_view == null)
            {
                Debug.LogError("[WeaponRecipeUI] Initialize 실패: m_view 참조 누락. UIManager.prefab의 RecipeView 컴포넌트 연결이 필요합니다.", this);
                return;
            }

            // 씬 오버라이드가 적용된 브리지 직렬화 참조를 Initialize 전에 View에 주입한다.
            m_view.BindReferences(panel, openButton, buttonLabel, weaponInfo, scroll, inlineLayout, recipes);
            m_presenter = new RecipePresenter(m_view, ResolveSession);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
            }
        }

        private void LateUpdate()
        {
            if (m_presenter != null)
            {
                m_presenter.Tick();
            }
        }

        private void OnDisable()
        {
            if (m_presenter != null)
            {
                m_presenter.Close();
            }
        }

        private void OnDestroy()
        {
            if (m_presenter != null)
            {
                m_presenter.Dispose();
                m_presenter = null;
            }
        }

        public void Open()
        {
            if (m_presenter != null)
            {
                m_presenter.Open();
            }
        }

        public void Close()
        {
            if (m_presenter != null)
            {
                m_presenter.Close();
            }
        }

        /// <summary>
        /// 기존 경로와 같은 세션 탐색(GameManager.Player의 WorkshopSession)을 Presenter에 공급합니다.
        /// </summary>
        private WorkshopSession ResolveSession()
        {
            FirstPersonPlayer player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            return player != null ? player.GetComponent<WorkshopSession>() : null;
        }
    }
}
