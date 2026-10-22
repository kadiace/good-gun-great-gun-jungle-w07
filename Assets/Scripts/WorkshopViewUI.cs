using TMPro;
using UnityEngine;
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
        [SerializeField] private TMP_Text frontLabel, upLabel, rightLabel;
        [SerializeField] private WorkshopPlanarGridGraphic grid;
        [SerializeField] private Color inactiveColor = new Color(.08f, .1f, .11f, .75f);
        [SerializeField] private Color activeColor = new Color(.85f, .68f, .18f, .95f);
        private WorkshopSession session;
        private CubeBuilder builder;
        private WorkshopPlanarView view;

        private void Awake()
        {
            controlsRoot.SetActive(false);
            grid.gameObject.SetActive(false);
            modeLabel.text = "2D 모드";
            modeButton.onClick.AddListener(Toggle);
            frontButton.onClick.AddListener(Front);
            upButton.onClick.AddListener(Up);
            rightButton.onClick.AddListener(Right);
        }

        private void Toggle() => view?.ToggleMode();
        private void Front() => view?.SelectAxis(WorkshopViewAxis.Front);
        private void Up() => view?.SelectAxis(WorkshopViewAxis.Up);
        private void Right() => view?.SelectAxis(WorkshopViewAxis.Right);

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
            if (grid.gameObject.activeSelf != planar) grid.gameObject.SetActive(planar);
            if (!visible) return;
            grid.Bind(view, builder);
            string caption = planar ? "3D 모드" : "2D 모드";
            if (modeLabel.text != caption) modeLabel.text = caption;
            modeButton.interactable = view != null && view.CanChangeView;
            SetAxis(frontButton, frontLabel, WorkshopViewAxis.Front, "FRONT", planar);
            SetAxis(upButton, upLabel, WorkshopViewAxis.Up, "UP", planar);
            SetAxis(rightButton, rightLabel, WorkshopViewAxis.Right, "RIGHT", planar);
        }

        private void SetAxis(Button button, TMP_Text label, WorkshopViewAxis axis, string text, bool planar)
        {
            bool selected = planar && view.Axis == axis;
            button.interactable = planar && view.CanChangeView;
            Color axisColor = axis == WorkshopViewAxis.Front ? new Color(0f, .7f, 1f) :
                axis == WorkshopViewAxis.Up ? new Color(.12f, .8f, .25f) : new Color(1f, .12f, .12f);
            button.image.color = selected ? axisColor : inactiveColor;
            label.color = selected ? Color.black : axisColor;
            string caption = selected ? "● " + text : text;
            if (label.text != caption) label.text = caption;
        }

        private void OnDisable()
        {
            if (controlsRoot != null) controlsRoot.SetActive(false);
            if (grid != null) grid.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            modeButton.onClick.RemoveListener(Toggle);
            frontButton.onClick.RemoveListener(Front);
            upButton.onClick.RemoveListener(Up);
            rightButton.onClick.RemoveListener(Right);
        }
    }
}
