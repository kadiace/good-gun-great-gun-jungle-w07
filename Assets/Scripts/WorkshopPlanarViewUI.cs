using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    internal sealed class WorkshopPlanarViewUI : IDisposable
    {
        private readonly GameObject root, toolbar, panel;
        private readonly RawImage viewport;
        private readonly TMP_Text toggleLabel, depthLabel, status;
        private readonly Button toggle;
        private readonly Button[] axes = new Button[3];
        private readonly Button previousDepth, nextDepth;
        private readonly List<RaycastResult> raycasts = new List<RaycastResult>();
        private PointerEventData pointerData;
        private EventSystem pointerOwner;
        internal RectTransform Viewport => viewport.rectTransform;

        internal WorkshopPlanarViewUI(Transform owner, Action toggleView, Action<WorkshopPlanarAxis> setAxis, Action<int> moveDepth)
        {
            root = new GameObject("Workshop Planar UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.hideFlags = HideFlags.DontSave;
            root.transform.SetParent(owner, false);
            var canvas = root.GetComponent<Canvas>();
            // Existing module palettes, input fields and modal UI must remain above this editing viewport.
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -1;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = .5f;

            toolbar = Rect("View Controls", root.transform, new Vector2(.22f, .83f), new Vector2(.76f, .9f)).gameObject;
            var background = toolbar.AddComponent<Image>(); background.color = new Color(.07f, .09f, .13f, .98f);
            toggle = Button("Toggle View", toolbar.transform, "2D 보기", 0f, .22f, toggleView);
            toggleLabel = toggle.GetComponentInChildren<TMP_Text>();
            for (int i = 0; i < 3; i++)
            {
                var axis = (WorkshopPlanarAxis)i;
                axes[i] = Button("Axis " + axis, toolbar.transform, axis.ToString(), .23f + i * .09f, .31f + i * .09f, () => setAxis(axis));
            }
            previousDepth = Button("Previous Depth", toolbar.transform, "−", .53f, .61f, () => moveDepth(-1));
            depthLabel = Text("Depth", toolbar.transform, "깊이 0", new Vector2(.62f, .1f), new Vector2(.88f, .9f), 21f);
            nextDepth = Button("Next Depth", toolbar.transform, "+", .9f, .99f, () => moveDepth(1));

            panel = Rect("2D Editing View", root.transform, new Vector2(.22f, .18f), new Vector2(.76f, .82f)).gameObject;
            viewport = panel.AddComponent<RawImage>();
            viewport.color = Color.white; viewport.raycastTarget = true;
            var footer = Rect("Surface Info", panel.transform, Vector2.zero, new Vector2(1f, .13f));
            var shade = footer.gameObject.AddComponent<Image>(); shade.color = new Color(.035f, .045f, .065f, .96f);
            status = Text("Status", footer, "", new Vector2(.02f, .05f), new Vector2(.98f, .95f), 19f);
            status.alignment = TextAlignmentOptions.MidlineLeft;
            panel.SetActive(false); toolbar.SetActive(false); root.SetActive(false);
        }

        internal void Show(bool editing, bool planar, bool available, WorkshopPlanarAxis axis, int depth, string message)
        {
            root.SetActive(editing); toolbar.SetActive(editing); panel.SetActive(editing && planar);
            if (!editing) return;
            toggle.interactable = available || planar;
            toggleLabel.text = planar ? "3D 보기" : "2D 보기";
            for (int i = 0; i < axes.Length; i++)
            {
                axes[i].interactable = planar;
                axes[i].targetGraphic.color = planar && i == (int)axis
                    ? new Color(.18f, .53f, .59f) : new Color(.18f, .22f, .28f);
            }
            depthLabel.text = axis + " 깊이 " + depth;
            previousDepth.interactable = planar && depth > -128;
            nextDepth.interactable = planar && depth < 128;
            status.text = message;
        }

        internal void SetTexture(Texture texture) => viewport.texture = texture;

        internal bool TryGetViewportPoint(Vector2 screen, out Vector2 uv)
        {
            uv = default;
            if (!panel.activeInHierarchy || !RectTransformUtility.ScreenPointToLocalPointInRectangle(Viewport, screen, null, out var local) ||
                !Viewport.rect.Contains(local)) return false;
            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                if (pointerData == null || pointerOwner != eventSystem)
                {
                    pointerOwner = eventSystem;
                    pointerData = new PointerEventData(eventSystem);
                }
                pointerData.position = screen; raycasts.Clear(); eventSystem.RaycastAll(pointerData, raycasts);
                foreach (var result in raycasts)
                {
                    if (!(result.module is GraphicRaycaster)) continue;
                    if (result.gameObject != viewport.gameObject) return false;
                    break;
                }
            }
            uv = (local - Viewport.rect.min) / Viewport.rect.size;
            return true;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static TMP_Text Text(string name, Transform parent, string value, Vector2 min, Vector2 max, float size)
        {
            var text = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset; text.fontSize = size; text.text = value;
            text.enableAutoSizing = true; text.fontSizeMin = 14f; text.fontSizeMax = size;
            text.color = new Color(.91f, .95f, 1f); text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false; return text;
        }

        private static Button Button(string name, Transform parent, string label, float min, float max, Action clicked)
        {
            var rect = Rect(name, parent, new Vector2(min, .1f), new Vector2(max, .9f));
            rect.offsetMin = new Vector2(4f, 0f); rect.offsetMax = new Vector2(-4f, 0f);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.18f, .22f, .28f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => clicked());
            Text("Label", rect, label, Vector2.zero, Vector2.one, 23f);
            return button;
        }

        public void Dispose() => UnityEngine.Object.Destroy(root);
    }
}
