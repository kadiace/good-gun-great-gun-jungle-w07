using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop.Editor
{
    // 명시적 요청이 있을 때만 프리팹을 편집한다. 플레이·테스트·씬 저장은 실행하지 않는다.
    [InitializeOnLoad]
    internal static class WorkshopPlanarViewAuthoring
    {
        private const string Request = "Library/WorkshopPlanarView.request";
        private const string UIPath = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string PlayerPath = "Assets/Prefabs/Player/Player.prefab";
        static WorkshopPlanarViewAuthoring() => EditorApplication.update += ApplyPending;

        private static void ApplyPending()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try { Apply(); }
            catch (Exception exception)
            {
                File.WriteAllText("Library/WorkshopPlanarView.error.txt", exception.ToString());
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Weapon Workshop/Create 2D Workshop Controls")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit mode required.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Fonts/NanumSquareNeo/TMP/NanumSquareNeo-Bold Dynamic.asset");
            if (font == null) throw new InvalidOperationException("Workshop UI font missing.");
            bool playerChanged = false, uiChanged = false;
            string viewGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/WorkshopPlanarView.cs");
            bool playerNeedsSave = !File.ReadAllText(PlayerPath).Contains("guid: " + viewGuid);
            var player = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var session = player.GetComponentInChildren<WorkshopSession>(true);
                if (session == null) throw new InvalidOperationException("WorkshopSession missing from Player prefab.");
                if (session.GetComponent<WorkshopPlanarView>() == null)
                    session.gameObject.AddComponent<WorkshopPlanarView>();
                // RequireComponent로 메모리에만 보완된 컴포넌트도 프리팹에 명시적으로 저장한다.
                if (playerNeedsSave)
                {
                    if (PrefabUtility.SaveAsPrefabAsset(player, PlayerPath) == null) throw new IOException("Player prefab save failed.");
                    playerChanged = true;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            var ui = PrefabUtility.LoadPrefabContents(UIPath);
            try
            {
                if (ui.GetComponentInChildren<WorkshopViewUI>(true) == null)
                {
                    var owner = ui.GetComponent<UIManager>();
                    if (owner == null) throw new InvalidOperationException("UIManager missing from UI prefab.");
                    var gridRoot = MakeRect("WorkshopPlanarGrid", ui.transform, Vector2.zero, Vector2.one);
                    gridRoot.SetAsFirstSibling();
                    var grid = gridRoot.gameObject.AddComponent<WorkshopPlanarGridGraphic>();
                    grid.raycastTarget = false;
                    grid.color = new Color(.65f, .75f, .8f, .18f);
                    gridRoot.gameObject.SetActive(false);
                    var overlay = MakeRect("WorkshopViewOverlay", ui.transform, Vector2.zero, Vector2.one);
                    var presenter = overlay.gameObject.AddComponent<WorkshopViewUI>();
                    var controls = MakeRect("ViewControls", overlay, Vector2.zero, Vector2.one);
                    var mode = MakeButton("ViewMode", controls, new Vector2(.695f, .925f), new Vector2(.762f, .99f),
                        "2D 모드", font, Color.black);
                    mode.image.color = new Color(.62f, .62f, .62f, 1f);
                    var axes = MakeRect("CoreAxes", controls, new Vector2(.767f, .825f), new Vector2(.835f, .99f));
                    axes.gameObject.AddComponent<WorkshopAxesGraphic>().raycastTarget = false;
                    var front = MakeButton("Front", axes, new Vector2(.57f, .28f), new Vector2(1f, .43f),
                        "FRONT", font, new Color(0f, .75f, 1f));
                    var up = MakeButton("Up", axes, new Vector2(.28f, .88f), new Vector2(.8f, 1f),
                        "UP", font, new Color(.18f, .9f, .25f));
                    var right = MakeButton("Right", axes, new Vector2(0f, 0f), new Vector2(.53f, .15f),
                        "RIGHT", font, new Color(1f, .16f, .16f));
                    var data = new SerializedObject(presenter);
                    Assign(data, "owner", owner); Assign(data, "controlsRoot", controls.gameObject);
                    Assign(data, "modeButton", mode); Assign(data, "modeLabel", mode.GetComponentInChildren<TMP_Text>());
                    Assign(data, "frontButton", front); Assign(data, "frontLabel", front.GetComponentInChildren<TMP_Text>());
                    Assign(data, "upButton", up); Assign(data, "upLabel", up.GetComponentInChildren<TMP_Text>());
                    Assign(data, "rightButton", right); Assign(data, "rightLabel", right.GetComponentInChildren<TMP_Text>());
                    Assign(data, "grid", grid);
                    data.ApplyModifiedPropertiesWithoutUndo();
                    controls.gameObject.SetActive(false);
                    uiChanged = true;
                }
                var existing = ui.GetComponentInChildren<WorkshopViewUI>(true);
                var existingData = new SerializedObject(existing);
                if (existingData.FindProperty("backButton").objectReferenceValue == null)
                {
                    ConfigureSixAxes(existing, font);
                    uiChanged = true;
                }
                existingData.Update();
                if (existingData.FindProperty("axesGraphic").objectReferenceValue == null)
                {
                    ConfigureOrientation(existing, font);
                    uiChanged = true;
                }
                existingData.Update();
                if (existingData.FindProperty("directionsGraphic").objectReferenceValue == null)
                {
                    ConfigureDirectionsUI(ui.transform, existing, font);
                    uiChanged = true;
                }
                existingData.Update();
                var directions = (WorkshopPlanarDirectionsGraphic)existingData.FindProperty("directionsGraphic").objectReferenceValue;
                if (new SerializedObject(directions).FindProperty("forwardLabel").objectReferenceValue == null)
                {
                    ConfigureForwardLabel(directions, font);
                    uiChanged = true;
                }
                if (uiChanged && PrefabUtility.SaveAsPrefabAsset(ui, UIPath) == null)
                    throw new IOException("UI prefab save failed.");
            }
            finally { PrefabUtility.UnloadPrefabContents(ui); }
            if (playerChanged) AssetDatabase.ImportAsset(PlayerPath);
            if (uiChanged) AssetDatabase.ImportAsset(UIPath);
            File.WriteAllText("Library/WorkshopPlanarView.completed.txt", "2D workshop controls authored. Player changed: " +
                playerChanged + "; UI changed: " + uiChanged + ". No scene, play or test execution.");
        }

        private static void ConfigureDirectionsUI(Transform ui, WorkshopViewUI presenter, TMP_FontAsset font)
        {
            var root = MakeRect("WorkshopPlanarDirectionsOverlay", ui, Vector2.zero, Vector2.one);
            root.SetSiblingIndex(1); // 그리드 위, 기존 패널·컨트롤 아래에 표시한다.
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            var directions = root.gameObject.AddComponent<WorkshopPlanarDirectionsGraphic>();
            directions.raycastTarget = false;
            ConfigureForwardLabel(directions, font);
            var presenterData = new SerializedObject(presenter);
            Assign(presenterData, "directionsGraphic", directions);
            presenterData.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureForwardLabel(WorkshopPlanarDirectionsGraphic directions, TMP_FontAsset font)
        {
            var root = MakeRect("EquippedForwardLabel", directions.transform, Vector2.one * .5f, Vector2.one * .5f);
            var label = root.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSharedMaterial = font.material;
            label.text = "장착 전방 (+Forward)";
            label.fontSize = 24f; label.enableAutoSizing = true;
            label.fontSizeMin = 12f; label.fontSizeMax = 24f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.Left;
            label.color = Color.white; label.raycastTarget = false;
            root.gameObject.SetActive(false);
            var data = new SerializedObject(directions);
            Assign(data, "forwardLabel", label);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureOrientation(WorkshopViewUI presenter, TMP_FontAsset font)
        {
            var data = new SerializedObject(presenter);
            var front = (Button)data.FindProperty("frontButton").objectReferenceValue;
            var axes = front.GetComponentInParent<WorkshopAxesGraphic>(true);
            if (axes == null) throw new InvalidOperationException("Orientation graphic missing.");
            var controls = ((GameObject)data.FindProperty("controlsRoot").objectReferenceValue).transform;
            var menuToggle = MakeButton("AxisMenuToggle", axes.transform,
                new Vector2(.15f, -.18f), new Vector2(.85f, -.02f), "축 선택", font, Color.white);
            var menu = MakeRect("AxisSelectionMenu", controls, new Vector2(.767f, .65f), new Vector2(.835f, .785f));
            menu.gameObject.AddComponent<Image>().color = new Color(.04f, .05f, .06f, .97f);
            var list = data.FindProperty("axisMenuButtons");
            list.arraySize = 6;
            string[] names = { "Front", "Up", "Right", "Back", "Down", "Left" };
            string[] captions = { "+FRONT", "+UP", "+RIGHT", "-FRONT", "-UP", "-RIGHT" };
            for (int i = 0; i < 6; i++)
            {
                float x = i < 3 ? 0f : .5f;
                float y = 1f - (i % 3 + 1) / 3f;
                var button = MakeButton(names[i], menu, new Vector2(x, y), new Vector2(x + .5f, y + 1f / 3f),
                    captions[i], font, WorkshopAxesGraphic.AxisColor((WorkshopViewAxis)i));
                list.GetArrayElementAtIndex(i).objectReferenceValue = button;
            }
            Assign(data, "axesGraphic", axes); Assign(data, "axisMenuButton", menuToggle);
            Assign(data, "axisMenuRoot", menu.gameObject);
            data.ApplyModifiedPropertiesWithoutUndo();
            menu.gameObject.SetActive(false);
            axes.gameObject.SetActive(false);
        }

        private static void ConfigureSixAxes(WorkshopViewUI presenter, TMP_FontAsset font)
        {
            var data = new SerializedObject(presenter);
            var front = (Button)data.FindProperty("frontButton").objectReferenceValue;
            Transform axes = front.transform.parent;
            ConfigureAxis(data, axes, "Front", "front", "+FRONT", new Vector2(.72f, .43f), new Vector2(1f, .57f), font);
            ConfigureAxis(data, axes, "Back", "back", "-FRONT", new Vector2(0f, .43f), new Vector2(.28f, .57f), font);
            ConfigureAxis(data, axes, "Up", "up", "+UP", new Vector2(.33f, .86f), new Vector2(.67f, 1f), font);
            ConfigureAxis(data, axes, "Down", "down", "-UP", new Vector2(.33f, 0f), new Vector2(.67f, .14f), font);
            ConfigureAxis(data, axes, "Right", "right", "+RIGHT", new Vector2(0f, .12f), new Vector2(.38f, .26f), font);
            ConfigureAxis(data, axes, "Left", "left", "-RIGHT", new Vector2(.62f, .74f), new Vector2(1f, .88f), font);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAxis(SerializedObject data, Transform axes, string objectName,
            string field, string caption, Vector2 min, Vector2 max, TMP_FontAsset font)
        {
            var button = data.FindProperty(field + "Button").objectReferenceValue as Button;
            if (button == null) button = MakeButton(objectName, axes, min, max, caption, font, Color.white);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.text = caption;
            Assign(data, field + "Button", button); Assign(data, field + "Label", label);
        }

        private static void Assign(SerializedObject data, string name, UnityEngine.Object value)
            => data.FindProperty(name).objectReferenceValue = value;

        private static RectTransform MakeRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Button MakeButton(string name, Transform parent, Vector2 min, Vector2 max,
            string caption, TMP_FontAsset font, Color textColor)
        {
            var rect = MakeRect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.08f, .1f, .11f, .75f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var textRoot = MakeRect("Label", rect, new Vector2(.03f, .03f), new Vector2(.97f, .97f));
            var label = textRoot.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSharedMaterial = font.material;
            label.fontSize = 28; label.enableAutoSizing = true;
            label.fontSizeMin = 4; label.fontSizeMax = 64;
            label.alignment = TextAlignmentOptions.Center;
            label.color = textColor; label.raycastTarget = false;
            label.text = caption;
            return button;
        }
    }
}
