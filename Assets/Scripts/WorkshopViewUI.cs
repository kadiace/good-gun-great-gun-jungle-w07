using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [DefaultExecutionOrder(100)]
    public sealed class WorkshopViewUI : MonoBehaviour
    {
        [SerializeField] private UIManager owner;
        [SerializeField] private GameObject controlsRoot;
        [SerializeField] private Button modeButton;
        [SerializeField] private TMP_Text modeLabel;
        [SerializeField] private Button frontButton, upButton, rightButton;
        [SerializeField] private Button backButton, downButton, leftButton;
        [SerializeField] private TMP_Text frontLabel, upLabel, rightLabel;
        [SerializeField] private TMP_Text backLabel, downLabel, leftLabel;
        [SerializeField] private WorkshopPlanarGridGraphic grid;
        [FormerlySerializedAs("extentUI")]
        [SerializeField] private WorkshopPlanarDirectionsGraphic directionsGraphic;
        [SerializeField] private WorkshopAxesGraphic axesGraphic;
        [SerializeField] private Button axisMenuButton;
        [SerializeField] private GameObject axisMenuRoot;
        [SerializeField] private Button[] axisMenuButtons;
        [SerializeField] private Color inactiveColor = new Color(.08f, .1f, .11f, .75f);
        private WorkshopSession session;
        private CubeBuilder builder;
        private WorkshopPlanarView view;
        private Button[] buttons;
        private TMP_Text[] labels, menuLabels;
        private UnityAction[] selectActions;
        private static readonly string[] Captions = { "+FRONT", "+UP", "+RIGHT", "-FRONT", "-UP", "-RIGHT" };

        private void Awake()
        {
            controlsRoot.SetActive(false);
            grid.gameObject.SetActive(false);
            axisMenuRoot.SetActive(false);
            directionsGraphic.Bind(null, false);
            axesGraphic.ClearOrientation();
            modeLabel.text = "2D 모드";
            buttons = new[] { frontButton, upButton, rightButton, backButton, downButton, leftButton };
            labels = new[] { frontLabel, upLabel, rightLabel, backLabel, downLabel, leftLabel };
            menuLabels = new TMP_Text[6];
            selectActions = new UnityAction[6];
            for (int i = 0; i < 6; i++)
            {
                int index = i;
                selectActions[i] = () => SelectAxis(index);
                buttons[i].onClick.AddListener(selectActions[i]);
                axisMenuButtons[i].onClick.AddListener(selectActions[i]);
                menuLabels[i] = axisMenuButtons[i].GetComponentInChildren<TMP_Text>(true);
            }
            modeButton.onClick.AddListener(Toggle);
            axisMenuButton.onClick.AddListener(ToggleAxisMenu);
        }

        private void Toggle() { axisMenuRoot.SetActive(false); view?.ToggleMode(); }
        private void SelectAxis(int index)
        {
            axisMenuRoot.SetActive(false);
            view?.SelectAxis((WorkshopViewAxis)index);
        }
        private void ToggleAxisMenu()
        {
            if (view != null && view.CanChangeView) axisMenuRoot.SetActive(!axisMenuRoot.activeSelf);
        }

        private void LateUpdate()
        {
            if (session == null && owner != null && owner.Player != null)
            {
                session = owner.Player.GetComponent<WorkshopSession>();
                builder = owner.Player.GetComponentInChildren<CubeBuilder>(true);
            }
            view = session != null ? session.PlanarView : null;
            bool visible = session != null && session.IsEditing && builder != null && !builder.IsCustomMode;
            if (controlsRoot.activeSelf != visible) controlsRoot.SetActive(visible);
            bool planar = visible && view != null && view.Is2D;
            directionsGraphic.Bind(view, planar);
            if (grid.gameObject.activeSelf != planar) grid.gameObject.SetActive(planar);
            if (!visible) { HideOrientation(); return; }
            grid.Bind(view, builder);
            string caption = planar ? "3D 모드" : "2D 모드";
            if (modeLabel.text != caption) modeLabel.text = caption;
            modeButton.interactable = view != null && view.CanChangeView;
            var core = session.ActiveWorkbench != null ? session.ActiveWorkbench.CurrentCore : null;
            if (core == null || session.WorkshopCamera == null) { HideOrientation(); return; }
            if (!axesGraphic.gameObject.activeSelf) axesGraphic.gameObject.SetActive(true);

            // Session.LateUpdate 이후의 최종 자세를 읽는다. 입력 delta나 Euler 각을 UI에 누적하지 않는다.
            Quaternion relative = Quaternion.Inverse(session.WorkshopCamera.transform.rotation) * core.transform.rotation;
            int selected = -1;
            if (planar) selected = (int)view.Axis;
            else
                for (int i = 0; i < 6; i++)
                    if (Quaternion.Angle(relative, WorkshopPlanarView.CoreRotationInView((WorkshopViewAxis)i)) < .5f)
                    { selected = i; break; }
            axesGraphic.SetOrientation(relative, selected);
            bool interactable = view != null && view.CanChangeView;
            axisMenuButton.interactable = interactable;
            if (!interactable) axisMenuRoot.SetActive(false);
            Rect bounds = axesGraphic.rectTransform.rect;
            if (bounds.width <= 0f || bounds.height <= 0f) return;
            for (int i = 0; i < 6; i++)
            {
                var projected = axesGraphic.GetAxis(i);
                var rect = (RectTransform)buttons[i].transform;
                Vector2 center = new Vector2((projected.Point.x - bounds.xMin) / bounds.width,
                    (projected.Point.y - bounds.yMin) / bounds.height);
                // CoreAxes의 좌표계에서 같은 투영 결과로 그림과 클릭 영역을 배치한다.
                rect.anchorMin = center - new Vector2(.17f, .07f);
                rect.anchorMax = center + new Vector2(.17f, .07f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                SetAxis(buttons[i], labels[i], i, selected, projected.Tint, interactable);
                SetAxis(axisMenuButtons[i], menuLabels[i], i, selected,
                    WorkshopAxesGraphic.AxisColor((WorkshopViewAxis)i), interactable);
            }
            // 그리기와 클릭 순서를 깊이 순으로 일치시킨다. 겹친 축은 앞쪽이 입력을 받는다.
            for (int order = 0; order < 6; order++)
            {
                Transform button = buttons[axesGraphic.GetDrawOrder(order)].transform;
                if (button.GetSiblingIndex() != order) button.SetSiblingIndex(order);
            }
        }

        private void SetAxis(Button button, TMP_Text label, int index, int selected, Color tint, bool interactable)
        {
            bool active = index == selected;
            button.interactable = interactable;
            button.image.color = active ? WorkshopAxesGraphic.AxisColor((WorkshopViewAxis)index) : inactiveColor;
            label.color = active ? Color.black : tint;
            string text = active ? "● " + Captions[index] : Captions[index];
            if (label.text != text) label.text = text;
        }

        private void HideOrientation()
        {
            if (axisMenuRoot != null) axisMenuRoot.SetActive(false);
            if (axesGraphic == null) return;
            axesGraphic.ClearOrientation();
            axesGraphic.gameObject.SetActive(false);
        }
        private void OnDisable()
        {
            if (directionsGraphic != null) directionsGraphic.Bind(null, false);
            if (controlsRoot != null) controlsRoot.SetActive(false);
            if (grid != null) grid.gameObject.SetActive(false);
            HideOrientation();
        }
        private void OnDestroy()
        {
            modeButton.onClick.RemoveListener(Toggle);
            axisMenuButton.onClick.RemoveListener(ToggleAxisMenu);
            if (selectActions == null) return;
            for (int i = 0; i < 6; i++)
            {
                buttons[i].onClick.RemoveListener(selectActions[i]);
                axisMenuButtons[i].onClick.RemoveListener(selectActions[i]);
            }
        }
    }
}
