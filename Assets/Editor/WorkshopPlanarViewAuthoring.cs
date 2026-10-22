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
                    if (PrefabUtility.SaveAsPrefabAsset(ui, UIPath) == null) throw new IOException("UI prefab save failed.");
                    uiChanged = true;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(ui); }
            if (playerChanged) AssetDatabase.ImportAsset(PlayerPath);
            if (uiChanged) AssetDatabase.ImportAsset(UIPath);
            File.WriteAllText("Library/WorkshopPlanarView.completed.txt", "2D workshop controls authored. Player changed: " +
                playerChanged + "; UI changed: " + uiChanged + ". No scene, play or test execution.");
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
