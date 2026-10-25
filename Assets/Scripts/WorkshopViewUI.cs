using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// UIManager의 플레이어에서 WorkshopSession·CubeBuilder를 늦게 찾아 공방 보기 UI를 구동하는 브리지입니다.
    /// 세션 탐색, Session.LateUpdate 이후의 최종 자세 읽기, 그리드·방향·층 표시 Bind는 여기에 남고
    /// 모드·축 버튼과 축 그래픽 갱신은 WorkshopOrientationView/Presenter로 위임합니다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class WorkshopViewUI : MonoBehaviour
    {
        [SerializeField] private UIManager owner;
        // 아래 직렬화 필드는 씬 오버라이드를 포함한 실효 참조 원본이다(FormerlySerializedAs extentUI 포함).
        // Awake에서 m_view.Configure로 View에 옮겨지고, 그리드·방향·층 표시는 브리지가 계속 직접 갱신한다.
        [SerializeField] private GameObject controlsRoot;
        [SerializeField] private Button modeButton;
        [SerializeField] private TMP_Text modeLabel;
        [SerializeField] private Button frontButton, upButton, rightButton;
        [SerializeField] private Button backButton, downButton, leftButton;
        [SerializeField] private TMP_Text frontLabel, upLabel, rightLabel;
        [SerializeField] private TMP_Text backLabel, downLabel, leftLabel;
        [SerializeField] private WorkshopPlanarGridGraphic grid;
        [SerializeField] private WorkshopPlanarLayerUI layerUI;
        [SerializeField] private WorkshopPlanarLayerPresentation layerPresentation;
        [FormerlySerializedAs("extentUI")]
        [SerializeField] private WorkshopPlanarDirectionsGraphic directionsGraphic;
        [SerializeField] private WorkshopAxesGraphic axesGraphic;
        [SerializeField] private Button axisMenuButton;
        [SerializeField] private GameObject axisMenuRoot;
        [SerializeField] private Button[] axisMenuButtons;
        [SerializeField] private Color inactiveColor = new Color(.08f, .1f, .11f, .75f);
        [SerializeField] private WorkshopOrientationView m_view;

        private WorkshopSession session;
        private CubeBuilder builder;
        private WorkshopOrientationPresenter m_presenter;
        private bool m_hasLoggedViewMissing;

        private void Awake()
        {
            // 원래 숨김 초기화 중 그리드·방향 표시는 브리지 참조로 끄고, 나머지는 View 초기화가 끝낸다.
            if (grid != null)
            {
                grid.gameObject.SetActive(false);
            }

            if (directionsGraphic != null)
            {
                directionsGraphic.Bind(null, false);
            }

            if (m_view == null)
            {
                if (!m_hasLoggedViewMissing)
                {
                    m_hasLoggedViewMissing = true;
                    Debug.LogError("[WorkshopViewUI] Initialize 실패: 필수 참조 누락 (m_view=False)", this);
                }

                return;
            }

            // 씬 오버라이드가 반영된 실효 참조를 View 먼저 넘기고 초기화한다. View 초기화는 지연 숨김 없이 여기서 끝난다.
            m_view.Configure(controlsRoot, modeButton, modeLabel,
                frontButton, upButton, rightButton, backButton, downButton, leftButton,
                frontLabel, upLabel, rightLabel, backLabel, downLabel, leftLabel,
                axesGraphic, axisMenuButton, axisMenuRoot, axisMenuButtons, inactiveColor);
            m_presenter = new WorkshopOrientationPresenter(m_view);
            m_presenter.Initialize();
        }

        private void LateUpdate()
        {
            if (session == null && owner != null && owner.Player != null)
            {
                session = owner.Player.GetComponent<WorkshopSession>();
                builder = owner.Player.GetComponentInChildren<CubeBuilder>(true);
            }

            WorkshopPlanarView planarView = session != null ? session.PlanarView : null;
            bool visible = session != null && session.IsEditing && builder != null && !builder.IsCustomMode;
            bool planar = visible && planarView != null && planarView.Is2D;
            if (directionsGraphic != null)
            {
                var guideCore = visible && session.ActiveWorkbench != null ? session.ActiveWorkbench.CurrentCore : null;
                CubeAssembly guideAssembly = guideCore != null && guideCore.TryGetComponent<CubeAssembly>(out var target) ? target : null;
                directionsGraphic.Bind(planarView, guideAssembly, session != null ? session.WorkshopCamera : null,
                    visible && !GameManager.GameplayBlocked);
            }

            if (layerPresentation != null)
            {
                layerPresentation.Bind(planarView, planar);
            }

            if (layerUI != null)
            {
                layerUI.Bind(planarView, builder, layerPresentation, planar);
            }

            if (grid != null && grid.gameObject.activeSelf != planar)
            {
                grid.gameObject.SetActive(planar);
            }

            if (m_presenter == null)
            {
                return;
            }

            if (!visible)
            {
                m_presenter.Refresh(default);
                return;
            }

            if (grid != null)
            {
                grid.Bind(planarView, builder, layerPresentation);
            }

            Quaternion relativeRotation = Quaternion.identity;
            bool hasPose = false;
            var core = session.ActiveWorkbench != null ? session.ActiveWorkbench.CurrentCore : null;
            if (core != null && session.WorkshopCamera != null)
            {
                // Session.LateUpdate 이후의 최종 자세를 읽는다. 입력 delta나 Euler 각을 UI에 누적하지 않는다.
                relativeRotation = Quaternion.Inverse(session.WorkshopCamera.transform.rotation) * core.transform.rotation;
                hasPose = true;
            }

            m_presenter.Refresh(new WorkshopOrientationState
            {
                PlanarView = planarView,
                ControlsVisible = true,
                Planar = planar,
                HasWorkshopPose = hasPose,
                RelativeRotation = relativeRotation
            });
        }

        private void OnDisable()
        {
            if (layerPresentation != null)
            {
                layerPresentation.Bind(null, false);
            }

            if (layerUI != null)
            {
                layerUI.Bind(null, null, null, false);
            }

            if (directionsGraphic != null)
            {
                directionsGraphic.Bind(null, false);
            }

            if (grid != null)
            {
                grid.gameObject.SetActive(false);
            }

            m_presenter?.Refresh(default);
        }

        private void OnDestroy()
        {
            if (m_presenter != null)
            {
                m_presenter.Dispose();
                m_presenter = null;
            }
        }
    }
}
