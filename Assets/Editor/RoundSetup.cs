using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace WeaponWorkshop.Editor
{
    public static class RoundSetup
    {
        private static Font font;
        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max,
            Vector2 pivot, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.layer = 5;
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        private static UnityEngine.UI.Text Label(string name, Transform parent, string text, Vector2 anchor,
            Vector2 position, Vector2 size, int fontSize = 22, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rect = Rect(name, parent, anchor, anchor, anchor, position, size);
            var label = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            label.font = font; label.text = text; label.fontSize = fontSize; label.color = Color.white;
            label.alignment = alignment; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }
        private static UnityEngine.UI.Button Button(string name, Transform parent, string text, float y)
        {
            var rect = Rect(name, parent.parent, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(1, 0), new Vector2(-24, y + 120), new Vector2(300, 48));
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.18f, 0.32f, 0.45f, 1);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var label = Label("Label", rect, text, Vector2.one * 0.5f, Vector2.zero, new Vector2(280, 44));
            return button;
        }
        private static RectTransform Panel(string name, Transform parent)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.02f, 0.025f, 0.04f, 0.85f);
            var card = Rect("Card", rect, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(460, 370));
            card.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.08f, 0.11f, 0.16f, 1);
            return card;
        }
        private static void Set(SerializedObject so, string field, Object value) => so.FindProperty(field).objectReferenceValue = value;
        private const string InputReferenceFolder = "Assets/Data/Input";
        private static InputActionReference Reference(InputActionAsset asset, string action)
        {
            string path = InputReferenceFolder + "/" + action.Replace('/', '_') + ".asset";
            var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
            if (reference == null) { reference = InputActionReference.Create(asset.FindAction(action, true)); AssetDatabase.CreateAsset(reference, path); }
            return reference;
        }
        public static void SetUp()
        {
            if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/WorkshopUI.ttf");
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            const string uiPath = "Assets/Prefabs/Managers/UIManager.prefab";
            var root = PrefabUtility.LoadPrefabContents(uiPath);
            try
            {
                var ui = root.GetComponent<UIManager>();
                if (ui == null) ui = root.AddComponent<UIManager>();
                if (root.transform.Find("GameHUD") == null)
                {
                    var hud = Rect("GameHUD", root.transform, Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
                    var footer = Rect("ModuleLegend", hud, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(-32, 68));
                    var bg = footer.gameObject.AddComponent<UnityEngine.UI.Image>(); bg.color = new Color(0.04f, 0.06f, 0.1f, 0.85f); bg.raycastTarget = false;
                    var layout = footer.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                    layout.padding = new RectOffset(10, 10, 6, 6); layout.spacing = 8;
                    layout.childControlWidth = true; layout.childControlHeight = true;
                    layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
                    string[] names = { "1  기본 블럭", "2  연장", "3  2방향 분할", "4  4방향 분할" };
                    for (int i = 0; i < names.Length; i++)
                    {
                        var label = Label("Module" + (i + 1), footer, names[i], Vector2.one * 0.5f, Vector2.zero, new Vector2(140, 50), 17);
                        label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
                    }
                    Label("ControlsHelp", hud,
                        "조작법\n\nWASD  이동\n마우스  시점\nF  장착 / 공방 진입·탈출\n좌클릭  발사 (누르면 연사)\nESC  일시정지\n\n공방\n1~4  모듈 선택\n좌클릭  설치 / 삭제\n우클릭 드래그  무기 회전\nE  삭제 모드\nR  회전 초기화\n공방 나가기: F / ESC 메뉴",
                        new Vector2(1, 1), new Vector2(-24, -80), new Vector2(250, 380), 17, TextAnchor.UpperLeft);
                    var pause = Panel("PausePanel", root.transform);
                    Label("Title", pause, "일시정지", Vector2.one * 0.5f, new Vector2(0, 130), new Vector2(400, 50), 32);
                    var resume = Button("ResumeButton", pause, "계속하기", 35);
                    var restart = Button("RestartButton", pause, "재시작", -30);
                    var leave = Button("LeaveWorkshopButton", pause, "공방 나가기", -95);
                    var so = new SerializedObject(ui);
                    Set(so, "pausePanel", pause.parent.gameObject);
                    Set(so, "resumeButton", resume); Set(so, "restartButton", restart);
                    Set(so, "leaveWorkshopButton", leave);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    pause.parent.gameObject.SetActive(false);
                }
                PrefabUtility.SaveAsPrefabAsset(root, uiPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var sceneUI = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            if (sceneUI == null)
                sceneUI = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(uiPath))).GetComponent<UIManager>();
            var manager = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                var obj = new GameObject("GameManager"); manager = obj.AddComponent<GameManager>();
                var settings = new SerializedObject(manager); Set(settings, "inputActions", input); settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAssetAndConnect(obj, "Assets/Prefabs/Managers/GameManager.prefab", InteractionMode.AutomatedAction);
            }
            var managerSettings = new SerializedObject(manager);
            Set(managerSettings, "uiManager", sceneUI); Set(managerSettings, "inputActions", input);
            Set(managerSettings, "workshopSession", Object.FindFirstObjectByType<WorkshopSession>(FindObjectsInactive.Include));
            managerSettings.ApplyModifiedPropertiesWithoutUndo();
            var uiSettings = new SerializedObject(sceneUI); Set(uiSettings, "gameManager", manager); uiSettings.ApplyModifiedPropertiesWithoutUndo();
            var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null) eventSystem = new GameObject("EventSystem").AddComponent<UnityEngine.EventSystems.EventSystem>();
            var old = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (old != null) Object.DestroyImmediate(old);
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            if (!AssetDatabase.IsValidFolder(InputReferenceFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
                AssetDatabase.CreateFolder("Assets/Data", "Input");
            }
            module.actionsAsset = input;
            module.point = Reference(input, "UI/Point"); module.leftClick = Reference(input, "UI/Click");
            module.rightClick = Reference(input, "UI/RightClick"); module.middleClick = Reference(input, "UI/MiddleClick");
            module.scrollWheel = Reference(input, "UI/ScrollWheel"); module.move = Reference(input, "UI/Navigate");
            module.submit = Reference(input, "UI/Submit"); module.cancel = Reference(input, "UI/Cancel");
            EditorUtility.SetDirty(manager); EditorUtility.SetDirty(sceneUI); EditorUtility.SetDirty(module);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(manager.gameObject.scene);
            AssetDatabase.SaveAssets();
        }
    }
}



