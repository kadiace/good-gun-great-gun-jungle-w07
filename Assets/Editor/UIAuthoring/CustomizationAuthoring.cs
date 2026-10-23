using System;
using System.Collections.Generic;

using TMPro;

using UnityEditor;

using UnityEngine;
using UnityEngine.UI;

using Object = UnityEngine.Object;

namespace WeaponWorkshop.Editor
{
    // 커스텀 화면(도색·스티커·키링·탄색)의 저작 명령이다.
    // 이미 저작된 오브젝트는 삭제·재생성하지 않고, 없는 오브젝트·컴포넌트만 만들고 참조를 다시 연결한다.
    internal static class CustomizationAuthoring
    {
        private const string PREFAB_PATH = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string STEP = "Customization/Author";
        private const string PAINT_ALL_NAME = "PaintAllFaces";
        private const string COLOR_PICKER_PANEL_NAME = "ColorPickerPanel";

        private static readonly Color SLOT_COLOR = new Color(41f / 255f, 45f / 255f, 51f / 255f, 1f);
        private static readonly Color HIGHLIGHTED_COLOR = new Color32(0x3D, 0x44, 0x4F, 255);
        private static readonly Color PRESSED_COLOR = new Color32(0x1C, 0x20, 0x26, 255);
        private static readonly Color DISABLED_COLOR = new Color32(0x22, 0x25, 0x2B, 255);

        [MenuItem("Tools/UI/Authoring/Customization/Author")]
        private static void Author()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP + ": Edit mode required.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
            if (root == null)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP + ": Prefab not found: " + PREFAB_PATH);
                return;
            }

            bool saved = false;
            try
            {
                ModulePaletteUI bridge = root.GetComponentInChildren<ModulePaletteUI>(true);
                if (bridge == null)
                {
                    throw new InvalidOperationException("ModulePaletteUI missing from UI prefab.");
                }

                ColorPickerUI picker = root.GetComponentInChildren<ColorPickerUI>(true);
                if (picker == null)
                {
                    throw new InvalidOperationException("ColorPickerUI missing from UI prefab.");
                }

                SerializedObject bridgeSo = new SerializedObject(bridge);
                bridgeSo.Update();

                RectTransform paintPanel = RequireRef<RectTransform>(bridgeSo, "paintPanel");
                RectTransform stickerPanel = RequireRef<RectTransform>(bridgeSo, "stickerPanel");
                RectTransform keyringPanel = RequireRef<RectTransform>(bridgeSo, "keyringPanel");
                RectTransform customButtonRoot = RequireRef<RectTransform>(bridgeSo, "customButtonRoot");
                TMP_FontAsset bridgeFont = bridgeSo.FindProperty("font").objectReferenceValue as TMP_FontAsset;
                if (bridgeFont == null)
                {
                    bridgeFont = TMP_Settings.defaultFontAsset;
                }

                bool changed = false;
                changed |= AuthorPaintAllFaces(bridgeSo, paintPanel, bridgeFont, out Button paintAll);
                changed |= EnsureSpectrumAreas(bridgeSo);
                changed |= AuthorColorPickerPanel(picker, out _);
                changed |= AuthorCustomizationView(bridgeSo, bridge, customButtonRoot);
                changed |= AuthorPaintPanelView(bridgeSo, paintPanel, paintAll);
                changed |= AuthorStickerPanelView(bridgeSo, stickerPanel);
                changed |= AuthorKeyringPanelView(bridgeSo, keyringPanel);
                bridgeSo.ApplyModifiedPropertiesWithoutUndo();

                if (!changed)
                {
                    Debug.Log("[UIAuthoring] OK " + STEP + ": already authored. No save.");
                    return;
                }

                if (PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH) == null)
                {
                    throw new InvalidOperationException("Prefab save failed: " + PREFAB_PATH);
                }

                saved = true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP + ": " + exception.Message);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (saved)
            {
                AssetDatabase.ImportAsset(PREFAB_PATH);
                Debug.Log("[UIAuthoring] OK " + STEP + ": paint-all button, module color picker, " +
                    "ColorPickArea, View components and references are authored.");
            }
        }

        private static bool AuthorPaintAllFaces(SerializedObject _bridgeSo, RectTransform _paintPanel,
            TMP_FontAsset _font, out Button _paintAll)
        {
            bool changed = false;
            _paintAll = ReadObject(_bridgeSo, "paintAllButton") as Button;
            if (_paintAll == null)
            {
                RectTransform existing = FindChild(_paintPanel, PAINT_ALL_NAME);
                if (existing != null)
                {
                    _paintAll = existing.GetComponent<Button>();
                }
            }

            if (_paintAll == null)
            {
                _paintAll = CreatePaletteButton(_paintPanel, PAINT_ALL_NAME, "전체 도색",
                    new Vector2(112.5f, 33f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(115.5f, 10.5f), _font);
                changed = true;
            }
            else if (_paintAll.targetGraphic == null)
            {
                Image image = _paintAll.GetComponent<Image>();
                if (image == null)
                {
                    image = _paintAll.gameObject.AddComponent<Image>();
                }

                _paintAll.targetGraphic = image;
                ApplyPaletteColors(_paintAll);
                changed = true;
            }

            changed |= SetObject(_bridgeSo, "paintAllButton", _paintAll);
            return changed;
        }

        private static bool EnsureSpectrumAreas(SerializedObject _bridgeSo)
        {
            bool changed = false;
            RawImage colorSpectrum = RequireRef<RawImage>(_bridgeSo, "colorSpectrum");
            RawImage hueSpectrum = RequireRef<RawImage>(_bridgeSo, "hueSpectrum");
            changed |= EnsureComponentAdded<ColorPickArea>(colorSpectrum.gameObject);
            changed |= EnsureComponentAdded<ColorPickArea>(hueSpectrum.gameObject);
            return changed;
        }

        private static bool AuthorColorPickerPanel(ColorPickerUI _picker, out ModuleColorPickerView _view)
        {
            bool changed = false;
            Transform pickerRoot = _picker.transform;
            SerializedObject pickerSo = new SerializedObject(_picker);
            pickerSo.Update();
            TMP_FontAsset font = pickerSo.FindProperty("font").objectReferenceValue as TMP_FontAsset;
            if (font == null)
            {
                font = TMP_Settings.defaultFontAsset;
            }

            RectTransform panel = EnsureRect(pickerRoot, COLOR_PICKER_PANEL_NAME, new Vector2(255f, 225f),
                Vector2.zero, ref changed, out bool panelCreated);
            Image panelImage = EnsureComponent<Image>(panel.gameObject);
            if (panelCreated)
            {
                panelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
            }

            RectTransform titleRect = EnsureRect(panel, "Title", new Vector2(225f, 22.5f),
                new Vector2(0f, 96f), ref changed, out _);
            EnsureText(titleRect, font, 18f, "탄 색 선택", TextAlignmentOptions.Midline, true, ref changed);

            RectTransform squareRect = EnsureRect(panel, "SaturationValue", new Vector2(150f, 150f),
                new Vector2(-45f, 6f), ref changed, out _);
            EnsureComponent<RawImage>(squareRect.gameObject);
            EnsureComponent<ColorPickArea>(squareRect.gameObject);
            RectTransform squareMarker = EnsureMarker(squareRect, new Vector2(7.5f, 7.5f), ref changed);

            RectTransform hueRect = EnsureRect(panel, "Hue", new Vector2(18f, 150f),
                new Vector2(48.75f, 6f), ref changed, out _);
            EnsureComponent<RawImage>(hueRect.gameObject);
            EnsureComponent<ColorPickArea>(hueRect.gameObject);
            RectTransform hueMarker = EnsureMarker(hueRect, new Vector2(24f, 3f), ref changed);

            RectTransform previewRect = EnsureRect(panel, "Preview", new Vector2(37.5f, 37.5f),
                new Vector2(93.75f, 62.25f), ref changed, out bool previewCreated);
            Image preview = EnsureComponent<Image>(previewRect.gameObject);
            if (previewCreated)
            {
                preview.color = Color.white;
                preview.raycastTarget = false;
            }

            Button confirm = EnsurePickerButton(panel, "확인", new Vector2(-56.25f, -90f), font, ref changed);
            Button cancel = EnsurePickerButton(panel, "취소", new Vector2(56.25f, -90f), font, ref changed);

            if (panel.gameObject.activeSelf)
            {
                panel.gameObject.SetActive(false);
                changed = true;
            }

            _view = _picker.GetComponent<ModuleColorPickerView>();
            if (_view == null)
            {
                _view = _picker.gameObject.AddComponent<ModuleColorPickerView>();
                changed = true;
            }

            SerializedObject viewSo = new SerializedObject(_view);
            viewSo.Update();
            changed |= SetObject(viewSo, "m_panel", panel.gameObject);
            changed |= SetObject(viewSo, "m_square", squareRect.GetComponent<RawImage>());
            changed |= SetObject(viewSo, "m_hue", hueRect.GetComponent<RawImage>());
            changed |= SetObject(viewSo, "m_squareMarker", squareMarker);
            changed |= SetObject(viewSo, "m_hueMarker", hueMarker);
            changed |= SetObject(viewSo, "m_preview", preview);
            changed |= SetObject(viewSo, "m_confirm", confirm);
            changed |= SetObject(viewSo, "m_cancel", cancel);
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            changed |= SetObject(pickerSo, "view", _view);
            pickerSo.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static Button EnsurePickerButton(Transform _parent, string _name, Vector2 _position,
            TMP_FontAsset _font, ref bool _changed)
        {
            RectTransform rect = EnsureRect(_parent, _name, new Vector2(90f, 25.5f), _position,
                ref _changed, out bool created);
            Image image = EnsureComponent<Image>(rect.gameObject);
            if (created)
            {
                image.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            }

            Button button = EnsureComponent<Button>(rect.gameObject);
            RectTransform labelRect = EnsureRect(rect, "Label", new Vector2(90f, 25.5f), Vector2.zero,
                ref _changed, out _);
            EnsureText(labelRect, _font, 16.5f, _name, TextAlignmentOptions.Midline, false, ref _changed);
            return button;
        }

        private static RectTransform EnsureMarker(Transform _parent, Vector2 _size, ref bool _changed)
        {
            RectTransform rect = EnsureRect(_parent, "Marker", _size, Vector2.zero, ref _changed, out bool created);
            Image image = EnsureComponent<Image>(rect.gameObject);
            Outline outline = EnsureComponent<Outline>(rect.gameObject);
            if (created)
            {
                image.color = Color.white;
                image.raycastTarget = false;
                outline.effectColor = Color.black;
            }

            return rect;
        }

        private static bool AuthorCustomizationView(SerializedObject _bridgeSo, ModulePaletteUI _bridge,
            RectTransform _customButtonRoot)
        {
            bool changed = false;
            CustomizationView view = _bridge.GetComponent<CustomizationView>();
            if (view == null)
            {
                view = _bridge.gameObject.AddComponent<CustomizationView>();
                changed = true;
            }

            ReadCustomPanels(_bridgeSo, out Object[] panelButtons, out Object[] panels);
            SerializedObject viewSo = new SerializedObject(view);
            viewSo.Update();
            changed |= SetObject(viewSo, "m_customButton", _customButtonRoot.GetComponent<Button>());
            changed |= SetObject(viewSo, "m_navigation",
                RequireRef<RectTransform>(_bridgeSo, "customNavigation").gameObject);
            changed |= SetObject(viewSo, "m_contentArea",
                RequireRef<RectTransform>(_bridgeSo, "customContentArea").gameObject);
            changed |= SetObject(viewSo, "m_contentPrompt",
                RequireRef<TMP_Text>(_bridgeSo, "customContentPrompt").gameObject);
            changed |= SetObject(viewSo, "m_removeButton", RequireRef<Button>(_bridgeSo, "customRemoveButton"));
            changed |= SetArray(viewSo, "m_panelButtons", panelButtons);
            changed |= SetArray(viewSo, "m_panels", panels);
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            changed |= SetObject(_bridgeSo, "customView", view);
            return changed;
        }

        private static bool AuthorPaintPanelView(SerializedObject _bridgeSo, RectTransform _paintPanel,
            Button _paintAll)
        {
            bool changed = false;
            PaintPanelView view = _paintPanel.GetComponent<PaintPanelView>();
            if (view == null)
            {
                view = _paintPanel.gameObject.AddComponent<PaintPanelView>();
                changed = true;
            }

            SerializedObject viewSo = new SerializedObject(view);
            viewSo.Update();
            changed |= SetObject(viewSo, "m_title", RequireRef<TMP_Text>(_bridgeSo, "paintTitle"));
            changed |= SetObject(viewSo, "m_previousPage",
                RequireRef<Button>(_bridgeSo, "previousPaintPageButton"));
            changed |= SetObject(viewSo, "m_nextPage", RequireRef<Button>(_bridgeSo, "nextPaintPageButton"));
            changed |= SetObject(viewSo, "m_pageLabel", RequireRef<TMP_Text>(_bridgeSo, "paintPageLabel"));
            changed |= SetArray(viewSo, "m_slotButtons", ReadArray(_bridgeSo, "paintMaterialButtons"));
            changed |= SetArray(viewSo, "m_slotSwatches", ReadArray(_bridgeSo, "paintMaterialSwatches"));
            changed |= SetArray(viewSo, "m_slotLabels", ReadArray(_bridgeSo, "paintMaterialLabels"));
            changed |= SetArray(viewSo, "m_slotHighlights", ReadArray(_bridgeSo, "paintMaterialHighlights"));
            changed |= SetObject(viewSo, "m_customColorButton",
                RequireRef<Button>(_bridgeSo, "customColorModeButton"));
            changed |= SetObject(viewSo, "m_materialSelection",
                RequireRef<RectTransform>(_bridgeSo, "materialSelectionView").gameObject);
            changed |= SetObject(viewSo, "m_customColorPicker",
                RequireRef<RectTransform>(_bridgeSo, "customColorPicker").gameObject);
            changed |= SetObject(viewSo, "m_preview", RequireRef<RawImage>(_bridgeSo, "paintPreview"));
            changed |= SetObject(viewSo, "m_materialName", RequireRef<TMP_Text>(_bridgeSo, "paintName"));
            changed |= SetObject(viewSo, "m_status", RequireRef<TMP_Text>(_bridgeSo, "paintStatus"));
            changed |= SetObject(viewSo, "m_customPreview",
                RequireRef<RawImage>(_bridgeSo, "customColorPreview"));
            changed |= SetObject(viewSo, "m_customHex", RequireRef<TMP_Text>(_bridgeSo, "customColorHex"));
            changed |= SetArray(viewSo, "m_rgbInputs", ReadArray(_bridgeSo, "rgbInputs"));
            changed |= SetObject(viewSo, "m_colorSpectrum", RequireRef<RawImage>(_bridgeSo, "colorSpectrum"));
            changed |= SetObject(viewSo, "m_hueSpectrum", RequireRef<RawImage>(_bridgeSo, "hueSpectrum"));
            Button applyButton = RequireRef<Button>(_bridgeSo, "applyCustomColorButton");
            changed |= SetObject(viewSo, "m_applyColor", applyButton);
            changed |= SetObject(viewSo, "m_applyLabel",
                applyButton.GetComponentInChildren<TMP_Text>(true));
            changed |= SetObject(viewSo, "m_back", RequireRef<Button>(_bridgeSo, "backToMaterialsButton"));
            changed |= SetObject(viewSo, "m_undo", RequireRef<Button>(_bridgeSo, "paintUndoButton"));
            changed |= SetObject(viewSo, "m_fill", RequireRef<Button>(_bridgeSo, "paintFillButton"));
            changed |= SetObject(viewSo, "m_paintAll", _paintAll);
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            changed |= SetObject(_bridgeSo, "paintPanelView", view);
            return changed;
        }

        private static bool AuthorStickerPanelView(SerializedObject _bridgeSo, RectTransform _stickerPanel)
        {
            bool changed = false;
            StickerPanelView view = _stickerPanel.GetComponent<StickerPanelView>();
            if (view == null)
            {
                view = _stickerPanel.gameObject.AddComponent<StickerPanelView>();
                changed = true;
            }

            SerializedObject viewSo = new SerializedObject(view);
            viewSo.Update();
            changed |= SetArray(viewSo, "m_buttons", ReadArray(_bridgeSo, "stickerButtons"));
            changed |= SetArray(viewSo, "m_highlights", ReadArray(_bridgeSo, "stickerHighlights"));
            changed |= SetObject(viewSo, "m_status", RequireRef<TMP_Text>(_bridgeSo, "stickerStatus"));
            changed |= SetObject(viewSo, "m_empty", RequireRef<TMP_Text>(_bridgeSo, "stickerEmptyLabel"));
            changed |= SetObject(viewSo, "m_undo", RequireRef<Button>(_bridgeSo, "stickerUndoButton"));
            changed |= SetObject(viewSo, "m_erase", RequireRef<Button>(_bridgeSo, "stickerRemoveButton"));
            changed |= SetObject(viewSo, "m_mirror", RequireRef<Button>(_bridgeSo, "stickerMirrorButton"));
            changed |= SetObject(viewSo, "m_mirrorLabel",
                RequireRef<TMP_Text>(_bridgeSo, "stickerMirrorButtonLabel"));
            changed |= SetObject(viewSo, "m_sizeDown", RequireRef<Button>(_bridgeSo, "stickerSizeDownButton"));
            changed |= SetObject(viewSo, "m_sizeLabel", RequireRef<TMP_Text>(_bridgeSo, "stickerSizeLabel"));
            changed |= SetObject(viewSo, "m_sizeUp", RequireRef<Button>(_bridgeSo, "stickerSizeUpButton"));
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            changed |= SetObject(_bridgeSo, "stickerPanelView", view);
            return changed;
        }

        private static bool AuthorKeyringPanelView(SerializedObject _bridgeSo, RectTransform _keyringPanel)
        {
            bool changed = false;
            KeyringPanelView view = _keyringPanel.GetComponent<KeyringPanelView>();
            if (view == null)
            {
                view = _keyringPanel.gameObject.AddComponent<KeyringPanelView>();
                changed = true;
            }

            SerializedObject viewSo = new SerializedObject(view);
            viewSo.Update();
            changed |= SetArray(viewSo, "m_buttons", ReadArray(_bridgeSo, "keyringModelButtons"));
            changed |= SetArray(viewSo, "m_highlights", ReadArray(_bridgeSo, "keyringModelHighlights"));
            changed |= SetObject(viewSo, "m_empty", RequireRef<TMP_Text>(_bridgeSo, "keyringEmptyLabel"));
            changed |= SetObject(viewSo, "m_status", RequireRef<TMP_Text>(_bridgeSo, "keyringStatus"));
            changed |= SetObject(viewSo, "m_count", RequireRef<TMP_Text>(_bridgeSo, "keyringCountLabel"));
            changed |= SetObject(viewSo, "m_remove", RequireRef<Button>(_bridgeSo, "keyringRemoveButton"));
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            changed |= SetObject(_bridgeSo, "keyringPanelView", view);
            return changed;
        }

        private static Button CreatePaletteButton(Transform _parent, string _name, string _label,
            Vector2 _size, Vector2 _anchor, Vector2 _pivot, Vector2 _position, TMP_FontAsset _font)
        {
            RectTransform rect = CreateRect(_parent, _name, _size, _position, _anchor, _pivot);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ApplyPaletteColors(button);

            RectTransform labelRect = CreateRect(rect, "Label", Vector2.zero, Vector2.zero,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(3f, 1.5f);
            labelRect.offsetMax = new Vector2(-3f, -1.5f);
            TextMeshProUGUI label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font;
            label.fontSize = 16.5f;
            label.alignment = TextAlignmentOptions.Midline;
            label.color = Color.white;
            label.text = _label;
            label.raycastTarget = false;
            UIFonts.ApplyWeight(label, 700);
            return button;
        }

        private static void ApplyPaletteColors(Button _button)
        {
            _button.targetGraphic.color = Color.white;
            ColorBlock colors = _button.colors;
            colors.normalColor = SLOT_COLOR;
            colors.highlightedColor = colors.selectedColor = HIGHLIGHTED_COLOR;
            colors.pressedColor = PRESSED_COLOR;
            colors.disabledColor = DISABLED_COLOR;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            _button.colors = colors;
        }

        private static TextMeshProUGUI EnsureText(RectTransform _rect, TMP_FontAsset _font, float _fontSize,
            string _value, TextAlignmentOptions _alignment, bool _raycastTarget, ref bool _changed)
        {
            TextMeshProUGUI text = _rect.GetComponent<TextMeshProUGUI>();
            bool created = text == null;
            if (created)
            {
                text = _rect.gameObject.AddComponent<TextMeshProUGUI>();
                _changed = true;
            }

            if (text.font == null)
            {
                text.font = _font;
                _changed = true;
            }

            if (created)
            {
                text.fontSize = _fontSize;
                text.alignment = _alignment;
                text.color = Color.white;
                text.text = _value;
                text.raycastTarget = _raycastTarget;
            }

            return text;
        }

        private static RectTransform EnsureRect(Transform _parent, string _name, Vector2 _size,
            Vector2 _position, ref bool _changed, out bool _created)
        {
            RectTransform existing = FindChild(_parent, _name);
            if (existing != null)
            {
                _created = false;
                return existing;
            }

            _created = true;
            _changed = true;
            return CreateRect(_parent, _name, _size, _position, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
        }

        private static RectTransform CreateRect(Transform _parent, string _name, Vector2 _size,
            Vector2 _position, Vector2 _anchor, Vector2 _pivot)
        {
            GameObject gameObject = new GameObject(_name, typeof(RectTransform));
            gameObject.layer = _parent.gameObject.layer;
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(_parent, false);
            rect.anchorMin = _anchor;
            rect.anchorMax = _anchor;
            rect.pivot = _pivot;
            rect.anchoredPosition = _position;
            rect.sizeDelta = _size;
            return rect;
        }

        private static RectTransform FindChild(Transform _parent, string _name)
        {
            for (int i = 0; i < _parent.childCount; i++)
            {
                Transform child = _parent.GetChild(i);
                if (child.name == _name)
                {
                    return child as RectTransform;
                }
            }

            return null;
        }

        private static bool EnsureComponentAdded<T>(GameObject _target) where T : Component
        {
            if (_target.GetComponent<T>() != null)
            {
                return false;
            }

            _target.AddComponent<T>();
            return true;
        }

        private static T EnsureComponent<T>(GameObject _target) where T : Component
        {
            T component = _target.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return _target.AddComponent<T>();
        }

        private static T RequireRef<T>(SerializedObject _so, string _name) where T : Object
        {
            SerializedProperty property = _so.FindProperty(_name);
            if (property == null)
            {
                throw new InvalidOperationException("직렬화 필드를 찾을 수 없습니다: " + _name);
            }

            T value = property.objectReferenceValue as T;
            if (value == null)
            {
                throw new InvalidOperationException("직렬화 참조가 비어 있습니다: " + _name);
            }

            return value;
        }

        private static Object ReadObject(SerializedObject _so, string _name)
        {
            SerializedProperty property = _so.FindProperty(_name);
            if (property == null)
            {
                throw new InvalidOperationException("직렬화 필드를 찾을 수 없습니다: " + _name);
            }

            return property.objectReferenceValue;
        }

        private static Object[] ReadArray(SerializedObject _so, string _name)
        {
            SerializedProperty property = _so.FindProperty(_name);
            if (property == null)
            {
                throw new InvalidOperationException("직렬화 필드를 찾을 수 없습니다: " + _name);
            }

            Object[] values = new Object[property.arraySize];
            for (int i = 0; i < property.arraySize; i++)
            {
                values[i] = property.GetArrayElementAtIndex(i).objectReferenceValue;
            }

            return values;
        }

        private static void ReadCustomPanels(SerializedObject _so, out Object[] _buttons, out Object[] _panels)
        {
            SerializedProperty entries = _so.FindProperty("customPanels");
            if (entries == null)
            {
                throw new InvalidOperationException("직렬화 필드를 찾을 수 없습니다: customPanels");
            }

            int count = entries.arraySize;
            _buttons = new Object[count];
            _panels = new Object[count];
            for (int i = 0; i < count; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                _buttons[i] = entry.FindPropertyRelative("Button").objectReferenceValue;
                RectTransform panel = entry.FindPropertyRelative("Panel").objectReferenceValue as RectTransform;
                _panels[i] = panel != null ? panel.gameObject : null;
            }
        }

        private static bool SetObject(SerializedObject _so, string _name, Object _value)
        {
            SerializedProperty property = _so.FindProperty(_name);
            if (property == null)
            {
                throw new InvalidOperationException("직렬화 필드를 찾을 수 없습니다: " + _name);
            }

            if (property.objectReferenceValue == _value)
            {
                return false;
            }

            property.objectReferenceValue = _value;
            return true;
        }

        private static bool SetArray(SerializedObject _so, string _name, IReadOnlyList<Object> _values)
        {
            SerializedProperty property = _so.FindProperty(_name);
            if (property == null)
            {
                throw new InvalidOperationException("직렬화 필드를 찾을 수 없습니다: " + _name);
            }

            bool changed = property.arraySize != _values.Count;
            if (property.arraySize != _values.Count)
            {
                property.arraySize = _values.Count;
            }

            for (int i = 0; i < _values.Count; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != _values[i])
                {
                    element.objectReferenceValue = _values[i];
                    changed = true;
                }
            }

            return changed;
        }
    }
}
