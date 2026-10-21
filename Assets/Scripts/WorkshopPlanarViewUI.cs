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
        private readonly GameObject root, toolbar;
        private readonly bool fixedXPlane;
        private readonly TMP_Text toggleLabel, depthLabel, status;
        private readonly Button toggle;
        private readonly Button[] axes = new Button[3];
        private readonly Button previousDepth, nextDepth;
        private readonly DirectionIndicator muzzleIndicator, upIndicator;
        private readonly Canvas canvas;
        private readonly List<RaycastResult> raycasts = new List<RaycastResult>();
        private PointerEventData pointerData;
        private EventSystem pointerOwner;
        private bool disposed;
        private bool IsAlive => !disposed && root != null;

        internal WorkshopPlanarViewUI(Transform owner, Action toggleView, Action<WorkshopPlanarAxis> setAxis, Action<int> moveDepth, bool fixedXPlane)
        {
            this.fixedXPlane = fixedXPlane;
            // Small controls in the existing workshop canvas; never cover the gun with a viewport.
            root = Rect("Workshop View Controls", owner, Vector2.zero, Vector2.one).gameObject;
            root.hideFlags = HideFlags.DontSave;
            root.SetActive(false);
            canvas = owner.GetComponentInParent<Canvas>();
            if (!fixedXPlane)
            {
                toolbar = Rect("View Controls", root.transform, new Vector2(.28f, .84f), new Vector2(.72f, .9f)).gameObject;
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
                toolbar.SetActive(false);
            }

            var footer = Rect("Grid Info", root.transform, new Vector2(.28f, .14f), new Vector2(.72f, .21f));
            status = Text("Status", footer, "", new Vector2(.02f, .05f), new Vector2(.98f, .95f), 19f);
            status.alignment = TextAlignmentOptions.MidlineLeft;
            status.gameObject.SetActive(false);
            muzzleIndicator = new DirectionIndicator(root.transform, "Muzzle Direction", "총구 · FRONT", new Color(1f, .67f, .22f));
            upIndicator = new DirectionIndicator(root.transform, "Up Direction", "위 · UP", new Color(.35f, 1f, .65f));
        }

        internal void Show(bool editing, bool planar, bool available, WorkshopPlanarAxis axis, int depth, string message, bool transitioning = false)
        {
            if (!IsAlive) return;
            root.SetActive(editing);
            if (!editing) { HideDirections(); return; }
            if (toolbar != null) toolbar.SetActive(true);
            if (status != null)
            {
                status.gameObject.SetActive(planar || !string.IsNullOrEmpty(message));
                status.text = message;
            }
            if (!planar || transitioning) HideDirections();
            if (fixedXPlane) return;
            toggle.interactable = !transitioning && (available || planar);
            toggleLabel.text = planar ? "3D 보기" : "2D 보기";
            for (int i = 0; i < axes.Length; i++)
            {
                axes[i].interactable = planar && !transitioning;
                axes[i].targetGraphic.color = planar && i == (int)axis
                    ? new Color(.18f, .53f, .59f) : new Color(.18f, .22f, .28f);
            }
            depthLabel.text = axis + " 깊이 " + depth;
            previousDepth.interactable = planar && !transitioning && depth > -128;
            nextDepth.interactable = planar && !transitioning && depth < 128;
        }

        internal void HideDirections()
        {
            if (disposed) return;
            muzzleIndicator?.Hide(); upIndicator?.Hide();
        }

        internal void UpdateDirections(Camera view, Matrix4x4 assemblyToDisplay, Vector3 forwardCell)
        {
            if (!IsAlive || !root.activeInHierarchy || view == null) { HideDirections(); return; }
            var rect = (RectTransform)root.transform;
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            UpdateDirection(muzzleIndicator, forwardCell, Vector3.forward);
            UpdateDirection(upIndicator, Vector3.zero, Vector3.up);

            void UpdateDirection(DirectionIndicator indicator, Vector3 cell, Vector3 axis)
            {
                Vector3 center = view.WorldToScreenPoint(assemblyToDisplay.MultiplyPoint3x4(cell));
                if (center.z <= view.nearClipPlane || !view.pixelRect.Contains(center) ||
                    !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, center, eventCamera, out var localCenter))
                { indicator.Hide(); return; }
                Vector3 endpoint = view.WorldToScreenPoint(assemblyToDisplay.MultiplyPoint3x4(cell + axis));
                if (endpoint.z <= view.nearClipPlane ||
                    !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, endpoint, eventCamera, out var localEnd))
                { indicator.Hide(); return; }
                indicator.Show(localCenter, localEnd - localCenter, rect.rect);
            }
        }

        // Only UI geometry: no colliders, materials, input bindings or assembly objects.
        private sealed class DirectionIndicator
        {
            private readonly RectTransform group, stem, leftHead, rightHead, label;
            private static readonly Vector2 LabelSize = new Vector2(140f, 32f);

            internal DirectionIndicator(Transform parent, string name, string caption, Color tint)
            {
                group = Rect(name, parent, Vector2.zero, Vector2.one);
                group.gameObject.SetActive(false);
                stem = Line("Shaft", tint);
                leftHead = Line("Arrowhead Left", tint);
                rightHead = Line("Arrowhead Right", tint);
                label = Rect("Label", group, Vector2.one * .5f, Vector2.one * .5f);
                label.sizeDelta = LabelSize;
                var shade = label.gameObject.AddComponent<Image>();
                shade.color = new Color(.035f, .045f, .06f, .94f); shade.raycastTarget = false;
                var text = Text("Text", label, caption, new Vector2(.03f, .04f), new Vector2(.97f, .96f), 21f);
                text.color = tint; text.fontStyle = FontStyles.Bold;
            }

            private RectTransform Line(string name, Color tint)
            {
                var line = Rect(name, group, Vector2.one * .5f, Vector2.one * .5f);
                line.pivot = new Vector2(0f, .5f);
                var image = line.gameObject.AddComponent<Image>();
                image.color = tint; image.raycastTarget = false;
                return line;
            }

            internal void Hide()
            {
                // The canvas can be destroyed before the player receives OnDisable/OnDestroy.
                // Unity's native-object null check is required; ?. would not detect this.
                if (group != null) group.gameObject.SetActive(false);
            }
            internal void Show(Vector2 center, Vector2 projectedAxis, Rect limits)
            {
                if (group == null || stem == null || leftHead == null || rightHead == null || label == null)
                { Hide(); return; }
                if (projectedAxis.sqrMagnitude < .01f) { Hide(); return; }
                Vector2 direction = projectedAxis.normalized;
                Vector2 start = center + direction * (projectedAxis.magnitude * .6f + 10f);
                Vector2 tip = start + direction * 44f;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Place(stem, start, 44f, angle);
                Place(leftHead, tip, 15f, angle + 150f);
                Place(rightHead, tip, 15f, angle - 150f);
                float clearance = Mathf.Abs(direction.x) * LabelSize.x * .5f + Mathf.Abs(direction.y) * LabelSize.y * .5f + 8f;
                Vector2 labelCenter = tip + direction * clearance;
                labelCenter.x = Mathf.Clamp(labelCenter.x, limits.xMin + LabelSize.x * .5f + 8f, limits.xMax - LabelSize.x * .5f - 8f);
                labelCenter.y = Mathf.Clamp(labelCenter.y, limits.yMin + LabelSize.y * .5f + 8f, limits.yMax - LabelSize.y * .5f - 8f);
                label.anchoredPosition = labelCenter;
                group.gameObject.SetActive(true);
            }
            private static void Place(RectTransform line, Vector2 position, float length, float angle)
            {
                line.anchoredPosition = position; line.sizeDelta = new Vector2(length, 4f);
                line.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        internal bool CanPoint(Vector2 screen, Camera camera)
        {
            if (!IsAlive || !root.activeInHierarchy || camera == null || !camera.pixelRect.Contains(screen)) return false;
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
                    if (result.module is GraphicRaycaster) return false;
                }
            }
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

        public void Dispose()
        {
            if (disposed) return;
            HideDirections(); disposed = true;
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}
