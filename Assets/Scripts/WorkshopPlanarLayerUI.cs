using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [DefaultExecutionOrder(108)]
    public sealed class WorkshopPlanarLayerUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title, hint;
        [SerializeField] private Button previous, next, core;
        [SerializeField] private Button[] layers;
        private static readonly string[] AxisLabels = { "+FRONT", "+UP", "+RIGHT", "−FRONT", "−UP", "−RIGHT" };
        private TMP_Text[] labels;
        private WorkshopPlanarView view;
        private CubeBuilder builder;
        private WorkshopPlanarLayerPresentation presentation;
        private void Awake()
        {
            panel.SetActive(false);
            previous.onClick.AddListener(Previous); next.onClick.AddListener(Next); core.onClick.AddListener(Core);
            labels = new TMP_Text[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                int index = i;
                labels[i] = layers[i].GetComponentInChildren<TMP_Text>(true);
                layers[i].onClick.AddListener(() => Select(index));
            }
        }
        public void Bind(WorkshopPlanarView value, CubeBuilder owner, WorkshopPlanarLayerPresentation display, bool show)
        {
            view = value; builder = owner; presentation = display;
            if (panel.activeSelf != show) panel.SetActive(show);
        }
        private void Previous() { if (view != null && view.SelectedDepth > int.MinValue) view.SetSelectedDepth(view.SelectedDepth - 1); }
        private void Next() { if (view != null && view.SelectedDepth < int.MaxValue) view.SetSelectedDepth(view.SelectedDepth + 1); }
        private void Core() => view?.SetSelectedDepth(0);
        private void Select(int index) => view?.SetSelectedDepth(view.SelectedDepth + index - layers.Length / 2);
        private void LateUpdate()
        {
            if (panel == null || !panel.activeSelf || view == null || !view.Is2D) return;
            title.text = $"{AxisLabels[(int)view.Axis]} · {view.SelectedDepth:+0;-0;0}층 편집";
            bool canEdit = view.CanChangeView;
            previous.interactable = next.interactable = core.interactable = canEdit;
            for (int i = 0; i < layers.Length; i++)
            {
                int depth = view.SelectedDepth + i - layers.Length / 2;
                int count = presentation != null ? presentation.CountAt(depth) : 0;
                labels[i].text = $"{depth:+0;-0;0}층\n{(count == 0 ? "빈 층" : count + "셀")}";
                layers[i].interactable = canEdit;
                layers[i].image.color = depth == view.SelectedDepth ? new Color(.15f, .48f, .55f, 1f) : new Color(.1f, .13f, .15f, .95f);
            }
            hint.text = builder != null && builder.HasPlanarStroke ? builder.PlanarStrokeCaption :
                "Ctrl+휠: 층 이동 · 좌클릭 드래그: 설치/삭제\n청록: +방향 층 / 주황: −방향 층 · 빨강: 삭제 범위";
        }
        private void OnDisable() { if (panel != null) panel.SetActive(false); }
    }
}
