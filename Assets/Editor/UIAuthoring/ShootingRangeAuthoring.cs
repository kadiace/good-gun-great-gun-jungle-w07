using System;

using TMPro;

using UnityEditor;

using UnityEngine;
using UnityEngine.UI;

using WeaponWorkshop;

namespace WeaponWorkshop.Editor
{
    public static class ShootingRangeAuthoring
    {
        private const string UIMANAGER_PREFAB_PATH = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string CUSTOM_PANEL_NAME = "CustomCubeSettings";
        private const string CUSTOM_TOGGLE_NAME = "CustomCube";
        private const string CLEANUP_NAME = "ClearSpawnedTargets";
        private const string STATUS_NAME = "SpawnStatus";
        private const string SPAWN_NAME = "SpawnCustomCube";
        private const string HUD_PANEL_NAME = "ShootingTestHUD";
        private const int CUSTOM_INPUT_COUNT = 7;

        [MenuItem("Tools/UI/Authoring/ShootingRange/Author")]
        public static void Author()
        {
            const string step = "ShootingRange/Author";
            GameObject root = null;
            try
            {
                root = LoadPrefab(UIMANAGER_PREFAB_PATH);
                TargetSelectionUI selectionBridge = RequireSingleDescendantComponent<TargetSelectionUI>(root.transform);
                ShootingTestHUD hudBridge = RequireSingleDescendantComponent<ShootingTestHUD>(root.transform);

                AuthorTargetSelection(selectionBridge);
                AuthorShootingTestHud(hudBridge);

                SavePrefabOrThrow(root, UIMANAGER_PREFAB_PATH);
                Debug.Log($"[UIAuthoring] OK {step} TargetSelectionView·ShootingTestHudView 저작·참조 연결");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[UIAuthoring] FAIL {step} {exception.Message}");
            }
            finally
            {
                UnloadPrefab(root);
            }
        }

        private static void AuthorTargetSelection(TargetSelectionUI _bridge)
        {
            SerializedObject bridgeData = new SerializedObject(_bridge);
            GameObject panel = RequireObject<GameObject>(bridgeData, "panel");
            RectTransform grid = RequireObject<RectTransform>(bridgeData, "grid");
            Button template = RequireObject<Button>(bridgeData, "buttonTemplate");
            Button closeButton = RequireObject<Button>(bridgeData, "closeButton");
            if (grid.parent == null)
            {
                throw new InvalidOperationException("TargetSelection Grid의 부모 Window를 찾지 못했습니다.");
            }

            Transform window = grid.parent;
            TMP_Text templateLabel = RequireComponent<TMP_Text>(RequireDescendant(template.transform, "Label"));
            TMP_FontAsset font = templateLabel.font;

            Button customToggle = EnsureTemplateButton(grid, CUSTOM_TOGGLE_NAME, template, "커스텀 큐브");
            Button cleanup = EnsureTemplateButton(window, CLEANUP_NAME, template, "내부 청소");
            ApplyCleanupStyle(cleanup);
            TMP_Text status = EnsureStatusText(window, font);
            TMP_InputField[] customInputs = AuthorCustomPanel(window, template, font, out GameObject customPanel);
            Button spawn = RequireComponent<Button>(RequireDescendant(customPanel.transform, SPAWN_NAME));

            TargetSelectionView view = EnsureComponent<TargetSelectionView>(_bridge.gameObject);
            SerializedObject viewData = new SerializedObject(view);
            SetObject(viewData, "m_panel", panel);
            SetObject(viewData, "m_grid", grid);
            SetObject(viewData, "m_buttonTemplate", template);
            SetObject(viewData, "m_closeButton", closeButton);
            SetObject(viewData, "m_customToggleButton", customToggle);
            SetObject(viewData, "m_customPanel", customPanel);
            SetObjectArray(viewData, "m_customInputs", customInputs);
            SetObject(viewData, "m_spawnButton", spawn);
            SetObject(viewData, "m_cleanupButton", cleanup);
            SetObject(viewData, "m_statusText", status);
            viewData.ApplyModifiedPropertiesWithoutUndo();

            SetObject(bridgeData, "m_view", view);
            bridgeData.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AuthorShootingTestHud(ShootingTestHUD _bridge)
        {
            SerializedObject bridgeData = new SerializedObject(_bridge);
            TMP_FontAsset font = RequireProperty(bridgeData, "font").objectReferenceValue as TMP_FontAsset;
            if (font == null)
            {
                font = TMP_Settings.defaultFontAsset;
            }

            if (font == null)
            {
                throw new InvalidOperationException("ShootingTestHUD 글꼴을 찾지 못했습니다.");
            }

            Transform parent = _bridge.transform;
            GameObject panel = EnsurePanel(parent, HUD_PANEL_NAME);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(.5f, 1f);
            panelRect.anchorMax = new Vector2(.5f, 1f);
            panelRect.pivot = new Vector2(.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -48f);
            panelRect.sizeDelta = new Vector2(525f, 255f);

            Image background = EnsureComponent<Image>(panel);
            background.color = new Color(.025f, .04f, .065f, .88f);
            background.raycastTarget = false;

            TMP_Text title = EnsureHudLabel(panel.transform, "Title", font, 12f, 18.75f, 22.5f);
            TMP_Text timer = EnsureHudLabel(panel.transform, "Timer", font, 36.75f, 20.25f, 24f);
            TMP_Text details = EnsureHudLabel(panel.transform, "Details", font, 66.75f, 12.75f, 138.75f);
            TMP_Text retry = EnsureHudLabel(panel.transform, "Retry", font, 211.5f, 13.5f, 33f);
            retry.color = new Color(1f, .85f, .25f);

            panel.SetActive(false);

            ShootingTestHudView view = EnsureComponent<ShootingTestHudView>(_bridge.gameObject);
            SerializedObject viewData = new SerializedObject(view);
            SetObject(viewData, "m_panel", panel);
            SetObject(viewData, "m_title", title);
            SetObject(viewData, "m_timer", timer);
            SetObject(viewData, "m_details", details);
            SetObject(viewData, "m_retry", retry);
            viewData.ApplyModifiedPropertiesWithoutUndo();

            SetObject(bridgeData, "m_view", view);
            bridgeData.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMP_InputField[] AuthorCustomPanel(Transform _window, Button _template, TMP_FontAsset _font,
            out GameObject _panel)
        {
            _panel = EnsurePanel(_window, CUSTOM_PANEL_NAME);
            RectTransform panelRect = (RectTransform)_panel.transform;
            panelRect.anchorMin = new Vector2(.5f, 0f);
            panelRect.anchorMax = new Vector2(.5f, 0f);
            panelRect.pivot = new Vector2(.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -9f);
            panelRect.sizeDelta = new Vector2(345f, 262.5f);

            Image background = EnsureComponent<Image>(_panel);
            background.color = new Color(.08f, .1f, .14f, 1f);

            string[] labels =
            {
                "가로 (m)", "세로 (m)", "높이 (m)", "관통 저항", "폭발 저항", "화속성 저항", "수속성 저항"
            };
            TMP_InputField[] inputs = new TMP_InputField[CUSTOM_INPUT_COUNT];
            for (int i = 0; i < CUSTOM_INPUT_COUNT; i++)
            {
                TMP_Text label = EnsureCustomLabel(_panel.transform, "Label" + i, _font, labels[i]);
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 1f);
                labelRect.anchorMax = new Vector2(0f, 1f);
                labelRect.pivot = new Vector2(0f, 1f);
                labelRect.anchoredPosition = new Vector2(18f, -13.5f - i * 28.5f);
                labelRect.sizeDelta = new Vector2(172.5f, 24f);

                inputs[i] = EnsureCustomInput(_panel.transform, "Value" + i, _font, i, i < 3 ? "3" : "0");
            }

            Button spawn = EnsureTemplateButton(_panel.transform, SPAWN_NAME, _template, "소환");
            RectTransform spawnRect = (RectTransform)spawn.transform;
            spawnRect.anchorMin = new Vector2(.5f, 0f);
            spawnRect.anchorMax = new Vector2(.5f, 0f);
            spawnRect.pivot = new Vector2(.5f, 0f);
            spawnRect.anchoredPosition = new Vector2(0f, 10.5f);
            spawnRect.sizeDelta = new Vector2(142.5f, 30f);

            _panel.SetActive(false);
            return inputs;
        }

        private static TMP_Text EnsureCustomLabel(Transform _parent, string _name, TMP_FontAsset _font, string _text)
        {
            Transform existing = _parent.Find(_name);
            GameObject labelObject;
            if (existing == null)
            {
                labelObject = new GameObject(_name, typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(_parent, false);
            }
            else
            {
                labelObject = existing.gameObject;
            }

            TMP_Text label = EnsureComponent<TextMeshProUGUI>(labelObject);
            label.font = _font;
            label.fontSize = 16.5f;
            label.text = _text;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static TMP_InputField EnsureCustomInput(Transform _parent, string _name, TMP_FontAsset _font,
            int _index, string _defaultText)
        {
            Transform existing = _parent.Find(_name);
            GameObject inputObject;
            if (existing == null)
            {
                inputObject = new GameObject(_name, typeof(RectTransform), typeof(Image));
                inputObject.transform.SetParent(_parent, false);
            }
            else
            {
                inputObject = existing.gameObject;
            }

            bool reactivate = inputObject.activeSelf;
            inputObject.SetActive(false);

            RectTransform inputRect = (RectTransform)inputObject.transform;
            inputRect.anchorMin = new Vector2(1f, 1f);
            inputRect.anchorMax = new Vector2(1f, 1f);
            inputRect.pivot = new Vector2(1f, 1f);
            inputRect.anchoredPosition = new Vector2(-18f, -13.5f - _index * 28.5f);
            inputRect.sizeDelta = new Vector2(112.5f, 24f);

            Image background = EnsureComponent<Image>(inputObject);
            background.color = new Color(.2f, .24f, .3f);

            Transform textTransform = inputObject.transform.Find("Text");
            GameObject textObject;
            if (textTransform == null)
            {
                textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(inputObject.transform, false);
            }
            else
            {
                textObject = textTransform.gameObject;
            }

            TMP_Text text = EnsureComponent<TextMeshProUGUI>(textObject);
            text.font = _font;
            text.fontSize = 16.5f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 1.5f);
            textRect.offsetMax = new Vector2(-6f, -1.5f);

            TMP_InputField field = EnsureComponent<TMP_InputField>(inputObject);
            field.textComponent = text;
            field.textViewport = inputRect;
            field.targetGraphic = background;
            field.contentType = TMP_InputField.ContentType.DecimalNumber;
            if (string.IsNullOrEmpty(field.text))
            {
                field.text = _defaultText;
            }

            if (reactivate)
            {
                inputObject.SetActive(true);
            }

            return field;
        }

        private static TMP_Text EnsureStatusText(Transform _window, TMP_FontAsset _font)
        {
            Transform existing = _window.Find(STATUS_NAME);
            GameObject statusObject;
            if (existing == null)
            {
                statusObject = new GameObject(STATUS_NAME, typeof(RectTransform), typeof(TextMeshProUGUI));
                statusObject.transform.SetParent(_window, false);
            }
            else
            {
                statusObject = existing.gameObject;
            }

            TMP_Text status = EnsureComponent<TextMeshProUGUI>(statusObject);
            status.font = _font;
            status.fontSize = 15f;
            status.color = new Color(1f, .35f, .35f, 1f);
            status.alignment = TextAlignmentOptions.Midline;
            status.raycastTarget = false;
            status.text = "";
            RectTransform rect = status.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.sizeDelta = new Vector2(-24f, 21f);
            rect.anchoredPosition = new Vector2(0f, 52.5f);
            return status;
        }

        private static void ApplyCleanupStyle(Button _cleanup)
        {
            RectTransform rect = (RectTransform)_cleanup.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(18f, 15f);
            rect.sizeDelta = new Vector2(135f, 31.5f);

            _cleanup.interactable = true;
            ColorBlock colors = _cleanup.colors;
            colors.normalColor = new Color(.8f, .12f, .12f, 1f);
            colors.highlightedColor = new Color(1f, .25f, .25f, 1f);
            colors.pressedColor = new Color(.55f, .06f, .06f, 1f);
            colors.selectedColor = colors.highlightedColor;
            _cleanup.colors = colors;
            if (_cleanup.targetGraphic != null)
            {
                _cleanup.targetGraphic.color = Color.white;
            }
        }

        private static Button EnsureTemplateButton(Transform _parent, string _name, Button _template, string _label)
        {
            Transform existing = _parent.Find(_name);
            Button button;
            if (existing == null)
            {
                button = UnityEngine.Object.Instantiate(_template, _parent);
                button.name = _name;
                button.gameObject.SetActive(true);
            }
            else
            {
                button = EnsureComponent<Button>(existing.gameObject);
                button.gameObject.SetActive(true);
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = _label;
            }

            return button;
        }

        private static TMP_Text EnsureHudLabel(Transform _parent, string _name, TMP_FontAsset _font, float _top,
            float _size, float _height)
        {
            Transform existing = _parent.Find(_name);
            GameObject labelObject;
            if (existing == null)
            {
                labelObject = new GameObject(_name, typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(_parent, false);
            }
            else
            {
                labelObject = existing.gameObject;
            }

            TMP_Text label = EnsureComponent<TextMeshProUGUI>(labelObject);
            label.font = _font;
            label.fontSize = _size + 3f;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.sizeDelta = new Vector2(-24f, _height);
            rect.anchoredPosition = new Vector2(0f, -_top);
            return label;
        }

        private static GameObject EnsurePanel(Transform _parent, string _name)
        {
            Transform existing = _parent.Find(_name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject panel = new GameObject(_name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_parent, false);
            return panel;
        }

        private static void SavePrefabOrThrow(GameObject _root, string _path)
        {
            if (PrefabUtility.SaveAsPrefabAsset(_root, _path) == null)
            {
                throw new InvalidOperationException("프리팹 저장 실패: " + _path);
            }
        }

        private static GameObject LoadPrefab(string _path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(_path);
            if (root == null)
            {
                throw new InvalidOperationException("프리팹을 불러오지 못했습니다: " + _path);
            }

            return root;
        }

        private static void UnloadPrefab(GameObject _root)
        {
            if (_root != null)
            {
                PrefabUtility.UnloadPrefabContents(_root);
            }
        }

        private static T RequireSingleDescendantComponent<T>(Transform _root) where T : Component
        {
            T[] components = _root.GetComponentsInChildren<T>(true);
            if (components.Length == 0)
            {
                throw new InvalidOperationException($"{typeof(T).Name} 컴포넌트를 찾지 못했습니다.");
            }

            if (components.Length > 1)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name} 컴포넌트가 {components.Length}개 있습니다. 대상을 하나로 정리하세요.");
            }

            return components[0];
        }

        private static Transform RequireDescendant(Transform _root, string _name)
        {
            Transform found = FindDescendant(_root, _name);
            if (found == null)
            {
                throw new InvalidOperationException($"'{_name}' 오브젝트를 찾지 못했습니다.");
            }

            return found;
        }

        private static Transform FindDescendant(Transform _root, string _name)
        {
            if (_root == null)
            {
                return null;
            }

            if (_root.name == _name)
            {
                return _root;
            }

            for (int i = 0; i < _root.childCount; i++)
            {
                Transform found = FindDescendant(_root.GetChild(i), _name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static T RequireComponent<T>(Transform _target) where T : Component
        {
            T component = _target.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(
                    $"'{_target.name}'에 {typeof(T).Name} 컴포넌트가 없습니다.");
            }

            return component;
        }

        private static T EnsureComponent<T>(GameObject _target) where T : Component
        {
            T component = _target.GetComponent<T>();
            return component != null ? component : _target.AddComponent<T>();
        }

        private static T RequireObject<T>(SerializedObject _serialized, string _name) where T : UnityEngine.Object
        {
            T value = RequireProperty(_serialized, _name).objectReferenceValue as T;
            if (value == null)
            {
                throw new InvalidOperationException(
                    $"{_serialized.targetObject.GetType().Name}.{_name} 참조가 비어 있습니다.");
            }

            return value;
        }

        private static void SetObject(SerializedObject _serialized, string _name, UnityEngine.Object _value)
        {
            RequireProperty(_serialized, _name).objectReferenceValue = _value;
        }

        private static void SetObjectArray<T>(SerializedObject _serialized, string _name, T[] _values)
            where T : UnityEngine.Object
        {
            SerializedProperty property = RequireProperty(_serialized, _name);
            property.arraySize = _values.Length;
            for (int i = 0; i < _values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = _values[i];
            }
        }

        private static SerializedProperty RequireProperty(SerializedObject _serialized, string _name)
        {
            SerializedProperty property = _serialized.FindProperty(_name);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{_serialized.targetObject.GetType().Name}에 직렬화 필드 '{_name}'가 없습니다.");
            }

            return property;
        }
    }
}
