using TMPro;

using UnityEditor;
using UnityEngine;

namespace WeaponWorkshop.EditorTools
{
    /// <summary>
    /// 호버·주문 HUD 저작 명령입니다. 기존 HoveringUI가 런타임에 만들던 스캔 로딩 원(ScanLoading)과
    /// 스캔 문구(ScanningText)를 UIManager.prefab의 HudLayer 아래에 저작하고,
    /// HoverTooltipView·CustomerRequirementsView를 브리지와 같은 오브젝트에 추가해 참조를 연결합니다.
    /// 멱등: 같은 이름의 오브젝트가 이미 있으면 새로 만들지 않고 연결만 검증·보정하며,
    /// 기존 오브젝트를 삭제·재생성해 fileID를 바꾸지 않습니다.
    /// </summary>
    public static class HoverOrderAuthoring
    {
        private const string MENU_ITEM_PATH = "Tools/UI/Authoring/HoverOrder/Author";
        private const string UI_MANAGER_PREFAB_PATH = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string SCAN_LOADING_NAME = "ScanLoading";
        private const string SCAN_LOADING_TEXT_NAME = "ScanningText";
        private const string MODULE_CRATE_SCAN_HINT_NAME = "Text_ModuleCrateScanHint";
        private const string MODULE_CRATE_SCAN_HINT_TEXT = "Q키를 눌러 스캔하기";
        // develop 원본(2560 기준 28, 자동 크기 22~28)의 0.75배.
        private const float MODULE_CRATE_SCAN_HINT_FONT_SIZE = 21f;
        private const float MODULE_CRATE_SCAN_HINT_FONT_SIZE_MIN = 16.5f;

        // 기존 HoveringUI.CreateScanLoading의 값과 같다.
        private const float SCAN_TEXT_FONT_SIZE = 19.5f;
        private const float SCAN_TEXT_WIDTH = 165f;
        private const float SCAN_TEXT_HEIGHT = 30f;
        private const float SCAN_TEXT_GAP = 18f;
        private static readonly Color SCAN_LOADING_COLOR = new Color(0.5f, 0.9f, 1f);

        [MenuItem(MENU_ITEM_PATH)]
        public static void Author()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[UIAuthoring] FAIL HoverOrder/Author 플레이 모드에서는 실행할 수 없습니다.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(UI_MANAGER_PREFAB_PATH);
            try
            {
                string error = AuthorPrefab(root);
                if (error != null)
                {
                    Debug.LogError($"[UIAuthoring] FAIL HoverOrder/Author {UI_MANAGER_PREFAB_PATH}: {error}");
                    return;
                }

                if (PrefabUtility.SaveAsPrefabAsset(root, UI_MANAGER_PREFAB_PATH) == null)
                {
                    Debug.LogError($"[UIAuthoring] FAIL HoverOrder/Author {UI_MANAGER_PREFAB_PATH}: 프리팹 저장 실패");
                    return;
                }

                Debug.Log(
                    "[UIAuthoring] OK HoverOrder/Author UIManager.prefab" +
                    "(ScanLoading·ScanningText·Text_ModuleCrateScanHint 저작, HoverTooltipView·CustomerRequirementsView 연결)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string AuthorPrefab(GameObject root)
        {
            HoveringUI hoverBridge = root.GetComponentInChildren<HoveringUI>(true);
            if (hoverBridge == null)
            {
                return "HoveringUI 컴포넌트를 찾지 못했습니다";
            }

            CustomerRequirementsUI requirementsBridge = root.GetComponentInChildren<CustomerRequirementsUI>(true);
            if (requirementsBridge == null)
            {
                return "CustomerRequirementsUI 컴포넌트를 찾지 못했습니다";
            }

            SerializedObject hoverSo = new SerializedObject(hoverBridge);
            GameObject panel = hoverSo.FindProperty("panel").objectReferenceValue as GameObject;
            if (panel == null)
            {
                return "HoveringUI.panel 참조가 비어 있습니다";
            }

            TMP_Text title = hoverSo.FindProperty("title").objectReferenceValue as TMP_Text;
            if (title == null)
            {
                return "HoveringUI.title 참조가 비어 있습니다";
            }

            Transform hudLayer = panel.transform.parent;
            if (hudLayer == null)
            {
                return "도구 설명 패널의 부모(HudLayer)를 찾지 못했습니다";
            }

            float diameter = hoverSo.FindProperty("scanLoadingDiameter").floatValue;
            ScanLoadingGraphic scanLoading = GetOrCreateScanLoading(hudLayer, panel.layer, diameter, out string error);
            if (scanLoading == null)
            {
                return error;
            }

            TMP_Text scanLoadingText = GetOrCreateScanLoadingText(scanLoading.transform, title.font, diameter,
                out error);
            if (scanLoadingText == null)
            {
                return error;
            }

            TMP_Text description = hoverSo.FindProperty("description").objectReferenceValue as TMP_Text;
            if (description == null)
            {
                return "HoveringUI.description 참조가 비어 있습니다";
            }

            TMP_Text scanHint = GetOrCreateModuleCrateScanHint(description, out error);
            if (scanHint == null)
            {
                return error;
            }

            HoverTooltipView tooltipView = WireTooltipView(hoverBridge, hoverSo, scanLoading, scanLoadingText, scanHint);
            if (tooltipView == null)
            {
                return "HoverTooltipView 연결에 실패했습니다";
            }

            return WireRequirementsView(requirementsBridge);
        }

        private static HoverTooltipView WireTooltipView(HoveringUI bridge, SerializedObject bridgeSo,
            ScanLoadingGraphic scanLoading, TMP_Text scanLoadingText, TMP_Text scanHint)
        {
            HoverTooltipView view = bridge.GetComponent<HoverTooltipView>();
            if (view == null)
            {
                view = bridge.gameObject.AddComponent<HoverTooltipView>();
            }

            SerializedObject viewSo = new SerializedObject(view);
            CopyReference(bridgeSo, "panel", viewSo, "m_panel");
            CopyReference(bridgeSo, "title", viewSo, "m_title");
            CopyReference(bridgeSo, "description", viewSo, "m_description");
            CopyReference(bridgeSo, "interact", viewSo, "m_interact");
            CopyReference(bridgeSo, "textArea", viewSo, "m_textArea");
            CopyReference(bridgeSo, "point1", viewSo, "m_point1");
            CopyReference(bridgeSo, "point2", viewSo, "m_point2");
            CopyReference(bridgeSo, "connector", viewSo, "m_connector");
            CopyReference(bridgeSo, "scanModeText", viewSo, "m_scanModeText");
            CopyReference(bridgeSo, "tagGrid", viewSo, "m_tagGrid");
            CopyReference(bridgeSo, "tagTemplate", viewSo, "m_tagTemplate");
            CopyFloat(bridgeSo, "connectorWidth", viewSo, "m_connectorWidth");
            CopyVector2(bridgeSo, "textOffset", viewSo, "m_textOffset");
            CopyFloat(bridgeSo, "screenPadding", viewSo, "m_screenPadding");
            CopyFloat(bridgeSo, "scanLoadingDiameter", viewSo, "m_scanLoadingDiameter");
            CopyFloat(bridgeSo, "scanLoadingThickness", viewSo, "m_scanLoadingThickness");
            CopyFloat(bridgeSo, "scanTextRevealDuration", viewSo, "m_scanTextRevealDuration");
            viewSo.FindProperty("m_scanLoading").objectReferenceValue = scanLoading;
            viewSo.FindProperty("m_scanLoadingText").objectReferenceValue = scanLoadingText;
            viewSo.FindProperty("m_moduleCrateScanHint").objectReferenceValue = scanHint;

            // HUD 호버와 결과 화면 툴팁이 다른 UI 위에 표시되도록 독립 정렬 Canvas를 둔다.
            GameObject panel = bridgeSo.FindProperty("panel").objectReferenceValue as GameObject;
            if (panel != null)
            {
                Canvas sortingCanvas = panel.GetComponent<Canvas>();
                if (sortingCanvas == null)
                {
                    sortingCanvas = panel.AddComponent<Canvas>();
                }

                sortingCanvas.overrideSorting = true;
                sortingCanvas.sortingOrder = short.MaxValue;
                sortingCanvas.enabled = true;
                viewSo.FindProperty("m_resultSortingCanvas").objectReferenceValue = sortingCanvas;
            }

            viewSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            bridgeSo.FindProperty("m_view").objectReferenceValue = view;
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bridge);
            return view;
        }

        private static string WireRequirementsView(CustomerRequirementsUI bridge)
        {
            SerializedObject bridgeSo = new SerializedObject(bridge);
            SerializedProperty content = bridgeSo.FindProperty("content");
            SerializedProperty rowTemplate = bridgeSo.FindProperty("rowTemplate");
            if (content == null || content.objectReferenceValue == null)
            {
                return "CustomerRequirementsUI.content 참조가 비어 있습니다";
            }

            if (rowTemplate == null || rowTemplate.objectReferenceValue == null)
            {
                return "CustomerRequirementsUI.rowTemplate 참조가 비어 있습니다";
            }

            CustomerRequirementsView view = bridge.GetComponent<CustomerRequirementsView>();
            if (view == null)
            {
                view = bridge.gameObject.AddComponent<CustomerRequirementsView>();
            }

            SerializedObject viewSo = new SerializedObject(view);
            CopyReference(bridgeSo, "content", viewSo, "m_content");
            CopyReference(bridgeSo, "rowTemplate", viewSo, "m_rowTemplate");
            CopyReference(bridgeSo, "textSizing", viewSo, "m_textSizing");
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            bridgeSo.FindProperty("m_view").objectReferenceValue = view;
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bridge);
            return null;
        }

        private static ScanLoadingGraphic GetOrCreateScanLoading(Transform hudLayer, int layer, float diameter,
            out string error)
        {
            error = null;
            Transform existing = hudLayer.Find(SCAN_LOADING_NAME);
            ScanLoadingGraphic graphic;
            if (existing == null)
            {
                GameObject loadingObject = new GameObject(SCAN_LOADING_NAME, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(ScanLoadingGraphic));
                loadingObject.layer = layer;
                RectTransform loadingRect = (RectTransform)loadingObject.transform;
                loadingRect.SetParent(hudLayer, false);
                graphic = loadingObject.GetComponent<ScanLoadingGraphic>();
                EditorUtility.SetDirty(loadingObject);
            }
            else
            {
                graphic = existing.GetComponent<ScanLoadingGraphic>();
                if (graphic == null)
                {
                    error = "HudLayer의 ScanLoading 오브젝트에 ScanLoadingGraphic이 없습니다";
                    return null;
                }
            }

            RectTransform rect = graphic.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.one * diameter;
            graphic.raycastTarget = false;
            graphic.color = SCAN_LOADING_COLOR;
            graphic.gameObject.SetActive(false);
            EditorUtility.SetDirty(graphic);
            return graphic;
        }

        private static TMP_Text GetOrCreateScanLoadingText(Transform scanLoading, TMP_FontAsset font, float diameter,
            out string error)
        {
            error = null;
            Transform existing = scanLoading.Find(SCAN_LOADING_TEXT_NAME);
            TextMeshProUGUI text;
            if (existing == null)
            {
                GameObject labelObject = new GameObject(SCAN_LOADING_TEXT_NAME, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelObject.layer = scanLoading.gameObject.layer;
                RectTransform labelRect = (RectTransform)labelObject.transform;
                labelRect.SetParent(scanLoading, false);
                text = labelObject.GetComponent<TextMeshProUGUI>();
                EditorUtility.SetDirty(labelObject);
            }
            else
            {
                text = existing.GetComponent<TextMeshProUGUI>();
                if (text == null)
                {
                    error = "ScanLoading의 ScanningText 오브젝트에 TextMeshProUGUI가 없습니다";
                    return null;
                }
            }

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -diameter * 0.5f - SCAN_TEXT_GAP);
            rect.sizeDelta = new Vector2(SCAN_TEXT_WIDTH, SCAN_TEXT_HEIGHT);
            text.font = font;
            text.fontSize = SCAN_TEXT_FONT_SIZE;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.color = SCAN_LOADING_COLOR;
            text.text = string.Empty;
            EditorUtility.SetDirty(text);
            return text;
        }

        // 모듈 해금 상자 스캔 안내. TextArea 레이아웃에서 설명 바로 아래에 오도록 형제 순서를 맞춘다.
        private static TMP_Text GetOrCreateModuleCrateScanHint(TMP_Text description, out string error)
        {
            error = null;
            Transform textArea = description.transform.parent;
            if (textArea == null)
            {
                error = "HoveringUI.description의 부모(TextArea)를 찾지 못했습니다";
                return null;
            }

            Transform existing = textArea.Find(MODULE_CRATE_SCAN_HINT_NAME);
            TextMeshProUGUI text;
            if (existing == null)
            {
                GameObject hintObject = new GameObject(MODULE_CRATE_SCAN_HINT_NAME, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                hintObject.layer = description.gameObject.layer;
                hintObject.transform.SetParent(textArea, false);
                text = hintObject.GetComponent<TextMeshProUGUI>();
                EditorUtility.SetDirty(hintObject);
            }
            else
            {
                text = existing.GetComponent<TextMeshProUGUI>();
                if (text == null)
                {
                    error = "TextArea의 Text_ModuleCrateScanHint 오브젝트에 TextMeshProUGUI가 없습니다";
                    return null;
                }
            }

            int targetIndex = description.transform.GetSiblingIndex() + 1;
            if (text.transform.GetSiblingIndex() != targetIndex)
            {
                text.transform.SetSiblingIndex(targetIndex);
            }

            text.font = description.font;
            text.fontSharedMaterial = description.fontSharedMaterial;
            text.text = MODULE_CRATE_SCAN_HINT_TEXT;
            text.color = new Color(0.5f, 0.9f, 1f, 1f);
            text.enableAutoSizing = true;
            text.fontSize = MODULE_CRATE_SCAN_HINT_FONT_SIZE;
            text.fontSizeMax = MODULE_CRATE_SCAN_HINT_FONT_SIZE;
            text.fontSizeMin = MODULE_CRATE_SCAN_HINT_FONT_SIZE_MIN;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = false;
            text.raycastTarget = false;
            text.gameObject.SetActive(false);
            EditorUtility.SetDirty(text);
            return text;
        }

        private static void CopyReference(SerializedObject source, string sourceProperty, SerializedObject target,
            string targetProperty)
        {
            SerializedProperty from = source.FindProperty(sourceProperty);
            SerializedProperty to = target.FindProperty(targetProperty);
            if (from != null && to != null && from.objectReferenceValue != null)
            {
                to.objectReferenceValue = from.objectReferenceValue;
            }
        }

        private static void CopyFloat(SerializedObject source, string sourceProperty, SerializedObject target,
            string targetProperty)
        {
            SerializedProperty from = source.FindProperty(sourceProperty);
            SerializedProperty to = target.FindProperty(targetProperty);
            if (from != null && to != null)
            {
                to.floatValue = from.floatValue;
            }
        }

        private static void CopyVector2(SerializedObject source, string sourceProperty, SerializedObject target,
            string targetProperty)
        {
            SerializedProperty from = source.FindProperty(sourceProperty);
            SerializedProperty to = target.FindProperty(targetProperty);
            if (from != null && to != null)
            {
                to.vector2Value = from.vector2Value;
            }
        }
    }
}
