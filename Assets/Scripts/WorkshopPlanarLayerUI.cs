using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace WeaponWorkshop
{
    // D09 층 이동 UI의 저작 참조 브리지다. 기존 직렬화 필드(panel/title/hint/previous/next/core/layers)와
    // Bind 시그니처를 유지하고 Presenter를 직접 소유·초기화·해제한다. 표시는 매니저가 저작한
    // WorkshopPlanarLayerView(m_view)에만 위임하며, 뷰 참조가 없을 때 컴포넌트를 생성하지 않는다.
    [DefaultExecutionOrder(108)]
    public sealed class WorkshopPlanarLayerUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title, hint;
        [SerializeField] private Button previous, next, core;
        [SerializeField] private Button[] layers;

        [SerializeField] private WorkshopPlanarLayerView m_view;

        private WorkshopPlanarLayerPresenter m_presenter;
        private bool m_hasLoggedViewError;

        private void Awake()
        {
            // 시작 상태를 명시적으로 숨긴다. 씬·프리팹에 저장된 활성 상태에 의존하지 않는다.
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }

            EnsureInitialized();
        }

        private void Start()
        {
            EnsureInitialized();
        }

        /// <summary>WorkshopViewUI의 기존 호출 시그니처다. 매개변수 이름(value/owner/display/show)도 호환을 유지한다.</summary>
        public void Bind(WorkshopPlanarView value, CubeBuilder owner, WorkshopPlanarLayerPresentation display, bool show)
        {
            if (!EnsureInitialized())
            {
                if (panel != null && panel.activeSelf)
                {
                    panel.SetActive(false);
                }

                return;
            }

            m_presenter.Bind(value, owner, display, show);
        }

        // WorkshopViewUI(100)의 Bind 뒤 표시 값을 다시 계산한다. 갱신 여부는 Presenter가 판단한다.
        private void LateUpdate()
        {
            if (m_presenter != null)
            {
                m_presenter.Refresh();
            }
        }

        private void OnDisable()
        {
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (m_presenter != null)
            {
                m_presenter.Dispose();
                m_presenter = null;
            }

            if (m_view != null)
            {
                m_view.Dispose();
            }
        }

        // 유효 브리지 참조를 뷰에 옮겨 초기화한 뒤 Presenter 구독까지 마친다. 중복 호출과 컴포넌트 Awake 순서에 안전하다.
        private bool EnsureInitialized()
        {
            if (m_presenter != null)
            {
                return true;
            }

            if (m_view == null)
            {
                LogViewError();
                return false;
            }

            m_view.Configure(panel, title, hint, previous, next, core, layers);
            if (!m_view.Initialize())
            {
                return false;
            }

            m_presenter = new WorkshopPlanarLayerPresenter(m_view);
            m_presenter.Initialize();
            return true;
        }

        private void LogViewError()
        {
            if (m_hasLoggedViewError)
            {
                return;
            }

            m_hasLoggedViewError = true;
            Debug.LogError("[WorkshopPlanarLayerUI] m_view(WorkshopPlanarLayerView) 참조가 비어 있다. " +
                "UIManager.prefab의 WorkshopViewOverlay에 WorkshopPlanarLayerView를 추가하고 m_view를 연결해야 층 이동 UI가 표시된다.");
        }
    }
}
