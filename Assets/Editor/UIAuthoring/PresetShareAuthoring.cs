using UnityEditor;

using UnityEngine;
using UnityEngine.UI;

using TMPro;

using WeaponWorkshop;

/// <summary>
/// 프리셋·공유 코드·공방 태그 UI(UIManager.prefab)의 저작 명령.
/// 기존 런타임 생성 경로가 없으므로(이미 저작된 패널 사용), View 컴포넌트 추가·직렬화 참조 연결·브리지의 m_view 연결만 수행한다.
/// 멱등: 이미 저작돼 있으면 새로 만들지 않고 연결만 검증·보정한다. 기존 오브젝트를 삭제·재생성하지 않는다.
/// </summary>
public static class PresetShareAuthoring
{
    private const string PREFAB_PATH = "Assets/Prefabs/Managers/UIManager.prefab";

    [MenuItem("Tools/UI/Authoring/PresetShare/Author")]
    public static void Author()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) == null)
        {
            Debug.LogError($"[UIAuthoring] FAIL PresetShare/Author: UIManager 프리팹을 찾을 수 없습니다. {PREFAB_PATH}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        try
        {
            string summary = AuthorContents(root);
            if (PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH) == null)
            {
                Debug.LogError("[UIAuthoring] FAIL PresetShare/Author: 프리팹 저장 실패 " + PREFAB_PATH);
                return;
            }

            Debug.Log($"[UIAuthoring] OK PresetShare/Author: {summary}");
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[UIAuthoring] FAIL PresetShare/Author: {exception.Message}\n{exception.StackTrace}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string AuthorContents(GameObject _root)
    {
        bool createdAny = false;
        var summaries = new System.Collections.Generic.List<string>();

        // 1. PresetPanelUI 브리지와 PresetPanelView 연결
        createdAny |= AuthorPresetPanel(_root, summaries);

        // 2. WeaponCodeUI 브리지와 WeaponCodeView 연결
        createdAny |= AuthorWeaponCode(_root, summaries);

        // 3. WorkshopTagsUI 브리지와 WorkshopTagsView 연결
        createdAny |= AuthorWorkshopTags(_root, summaries);

        return $"{(createdAny ? "생성 포함" : "기존 저작물 검증")}: " + string.Join("; ", summaries);
    }

    private static bool AuthorPresetPanel(GameObject _root, System.Collections.Generic.List<string> _summaries)
    {
        var presetBridge = _root.GetComponentInChildren<PresetPanelUI>(true);
        if (presetBridge == null)
        {
            Debug.LogWarning("[UIAuthoring] PresetShare/Author: UIManager 프리팹에 PresetPanelUI 컴포넌트가 없습니다.");
            return false;
        }

        var presetBridgeSo = new SerializedObject(presetBridge);
        GameObject panel = presetBridgeSo.FindProperty("panel").objectReferenceValue as GameObject;
        GameObject choicePanel = presetBridgeSo.FindProperty("choicePanel").objectReferenceValue as GameObject;
        Button[] slots = ReadButtonArray(presetBridgeSo, "slots");
        Button saveButton = presetBridgeSo.FindProperty("saveButton").objectReferenceValue as Button;
        Button loadButton = presetBridgeSo.FindProperty("loadButton").objectReferenceValue as Button;
        Button closeButton = presetBridgeSo.FindProperty("closeButton").objectReferenceValue as Button;
        TMP_Text choiceMessage = presetBridgeSo.FindProperty("choiceMessage").objectReferenceValue as TMP_Text;

        Require(panel, "PresetPanelUI.panel");
        Require(choicePanel, "PresetPanelUI.choicePanel");
        Require(slots != null && slots.Length > 0, "PresetPanelUI.slots");
        Require(saveButton, "PresetPanelUI.saveButton");
        Require(loadButton, "PresetPanelUI.loadButton");
        Require(closeButton, "PresetPanelUI.closeButton");
        Require(choiceMessage, "PresetPanelUI.choiceMessage");

        // PresetPanelView가 같은 GameObject에 있는지 확인, 없으면 추가
        bool createdView = false;
        PresetPanelView view = presetBridge.GetComponent<PresetPanelView>();
        if (view == null)
        {
            view = presetBridge.gameObject.AddComponent<PresetPanelView>();
            createdView = true;
        }

        // View에 참조 연결
        var viewSo = new SerializedObject(view);
        viewSo.FindProperty("m_panel").objectReferenceValue = panel;
        viewSo.FindProperty("m_choicePanel").objectReferenceValue = choicePanel;
        SerializedProperty slotsProperty = viewSo.FindProperty("m_slots");
        slotsProperty.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
        {
            slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        }
        viewSo.FindProperty("m_saveButton").objectReferenceValue = saveButton;
        viewSo.FindProperty("m_loadButton").objectReferenceValue = loadButton;
        viewSo.FindProperty("m_closeButton").objectReferenceValue = closeButton;
        viewSo.FindProperty("m_choiceMessage").objectReferenceValue = choiceMessage;
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        // 브리지의 m_view 필드 연결 (향후 이관용)
        if (presetBridgeSo.FindProperty("m_view") != null)
        {
            presetBridgeSo.FindProperty("m_view").objectReferenceValue = view;
            presetBridgeSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // 명시적 Navigation: 슬롯 버튼들 간 좌우, 저장/불러오기/닫기 버튼은 상하로만
        ApplySlotNavigation(slots);
        ApplyActionButtonNavigation(saveButton, loadButton, closeButton);

        _summaries.Add($"PresetPanelView{(createdView ? "(신규)" : "(기존)")} 연결, " +
            $"{slots?.Length ?? 0}슬롯 Explicit Navigation, 액션 버튼 Explicit Navigation");
        return createdView;
    }

    private static bool AuthorWeaponCode(GameObject _root, System.Collections.Generic.List<string> _summaries)
    {
        var codeBridge = _root.GetComponentInChildren<WeaponCodeUI>(true);
        if (codeBridge == null)
        {
            Debug.LogWarning("[UIAuthoring] PresetShare/Author: UIManager 프리팹에 WeaponCodeUI 컴포넌트가 없습니다.");
            return false;
        }

        var codeBridgeSo = new SerializedObject(codeBridge);
        GameObject panel = codeBridgeSo.FindProperty("panel").objectReferenceValue as GameObject;
        Button openButton = codeBridgeSo.FindProperty("openButton").objectReferenceValue as Button;
        Button closeButton = codeBridgeSo.FindProperty("closeButton").objectReferenceValue as Button;
        Button copyButton = codeBridgeSo.FindProperty("copyButton").objectReferenceValue as Button;
        Button importButton = codeBridgeSo.FindProperty("importButton").objectReferenceValue as Button;
        TMP_InputField codeInput = codeBridgeSo.FindProperty("codeInput").objectReferenceValue as TMP_InputField;

        Require(panel, "WeaponCodeUI.panel");
        Require(openButton, "WeaponCodeUI.openButton");
        Require(closeButton, "WeaponCodeUI.closeButton");
        Require(copyButton, "WeaponCodeUI.copyButton");
        Require(importButton, "WeaponCodeUI.importButton");
        Require(codeInput, "WeaponCodeUI.codeInput");

        bool createdView = false;
        WeaponCodeView view = codeBridge.GetComponent<WeaponCodeView>();
        if (view == null)
        {
            view = codeBridge.gameObject.AddComponent<WeaponCodeView>();
            createdView = true;
        }

        var viewSo = new SerializedObject(view);
        viewSo.FindProperty("m_panel").objectReferenceValue = panel;
        viewSo.FindProperty("m_openButton").objectReferenceValue = openButton;
        viewSo.FindProperty("m_closeButton").objectReferenceValue = closeButton;
        viewSo.FindProperty("m_copyButton").objectReferenceValue = copyButton;
        viewSo.FindProperty("m_importButton").objectReferenceValue = importButton;
        viewSo.FindProperty("m_codeInput").objectReferenceValue = codeInput;
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        if (codeBridgeSo.FindProperty("m_view") != null)
        {
            codeBridgeSo.FindProperty("m_view").objectReferenceValue = view;
            codeBridgeSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // 명시적 Navigation: 열기 버튼은 단독, 패널 내 버튼들은 상하
        ApplyWeaponCodeNavigation(openButton, closeButton, copyButton, importButton);

        _summaries.Add($"WeaponCodeView{(createdView ? "(신규)" : "(기존)")} 연결, " +
            $"열기/닫기/복사/가져오기 Explicit Navigation");
        return createdView;
    }

    private static bool AuthorWorkshopTags(GameObject _root, System.Collections.Generic.List<string> _summaries)
    {
        var tagsBridge = _root.GetComponentInChildren<WorkshopTagsUI>(true);
        if (tagsBridge == null)
        {
            Debug.LogWarning("[UIAuthoring] PresetShare/Author: UIManager 프리팹에 WorkshopTagsUI 컴포넌트가 없습니다.");
            return false;
        }

        var tagsBridgeSo = new SerializedObject(tagsBridge);
        GameObject panel = tagsBridgeSo.FindProperty("panel").objectReferenceValue as GameObject;
        TMP_Text weightText = tagsBridgeSo.FindProperty("weightText").objectReferenceValue as TMP_Text;
        TMP_Text salePriceText = tagsBridgeSo.FindProperty("salePriceText").objectReferenceValue as TMP_Text;
        TMP_Text tagsText = tagsBridgeSo.FindProperty("tagsText").objectReferenceValue as TMP_Text;
        RectTransform tagContainer = tagsBridgeSo.FindProperty("tagContainer").objectReferenceValue as RectTransform;
        Image tagTemplate = tagsBridgeSo.FindProperty("tagTemplate").objectReferenceValue as Image;
        TMP_Text noTagsText = tagsBridgeSo.FindProperty("noTagsText").objectReferenceValue as TMP_Text;

        Require(panel, "WorkshopTagsUI.panel");
        Require(weightText, "WorkshopTagsUI.weightText");
        Require(salePriceText, "WorkshopTagsUI.salePriceText");
        Require(tagsText, "WorkshopTagsUI.tagsText");
        Require(tagContainer, "WorkshopTagsUI.tagContainer");
        Require(tagTemplate, "WorkshopTagsUI.tagTemplate");
        Require(noTagsText, "WorkshopTagsUI.noTagsText");

        bool createdView = false;
        WorkshopTagsView view = tagsBridge.GetComponent<WorkshopTagsView>();
        if (view == null)
        {
            view = tagsBridge.gameObject.AddComponent<WorkshopTagsView>();
            createdView = true;
        }

        var viewSo = new SerializedObject(view);
        viewSo.FindProperty("m_panel").objectReferenceValue = panel;
        viewSo.FindProperty("m_weightText").objectReferenceValue = weightText;
        viewSo.FindProperty("m_salePriceText").objectReferenceValue = salePriceText;
        viewSo.FindProperty("m_tagsHeaderText").objectReferenceValue = tagsText;
        viewSo.FindProperty("m_tagContainer").objectReferenceValue = tagContainer;
        viewSo.FindProperty("m_tagTemplate").objectReferenceValue = tagTemplate;
        viewSo.FindProperty("m_noTagsText").objectReferenceValue = noTagsText;
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        if (tagsBridgeSo.FindProperty("m_view") != null)
        {
            tagsBridgeSo.FindProperty("m_view").objectReferenceValue = view;
            tagsBridgeSo.ApplyModifiedPropertiesWithoutUndo();
        }

        _summaries.Add($"WorkshopTagsView{(createdView ? "(신규)" : "(기존)")} 연결, " +
            $"태그 템플릿 풀 보존");
        return createdView;
    }

    private static Button[] ReadButtonArray(SerializedObject _so, string _propertyName)
    {
        var property = _so.FindProperty(_propertyName);
        if (property == null || !property.isArray)
        {
            return null;
        }

        var list = new System.Collections.Generic.List<Button>();
        for (int i = 0; i < property.arraySize; i++)
        {
            var element = property.GetArrayElementAtIndex(i);
            list.Add(element.objectReferenceValue as Button);
        }
        return list.ToArray();
    }

    private static void ApplySlotNavigation(Button[] _slots)
    {
        if (_slots == null || _slots.Length == 0)
        {
            return;
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null)
            {
                continue;
            }

            Navigation nav = _slots[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.wrapAround = false;
            nav.selectOnUp = null;
            nav.selectOnDown = null;
            nav.selectOnLeft = i > 0 ? _slots[i - 1] : null;
            nav.selectOnRight = i < _slots.Length - 1 ? _slots[i + 1] : null;
            _slots[i].navigation = nav;
        }
    }

    private static void ApplyActionButtonNavigation(Button _save, Button _load, Button _close)
    {
        var buttons = new[] { _save, _load, _close };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
            {
                continue;
            }

            Navigation nav = buttons[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.wrapAround = false;
            nav.selectOnLeft = null;
            nav.selectOnRight = null;
            nav.selectOnUp = i > 0 ? buttons[i - 1] : null;
            nav.selectOnDown = i < buttons.Length - 1 ? buttons[i + 1] : null;
            buttons[i].navigation = nav;
        }
    }

    private static void ApplyWeaponCodeNavigation(Button _open, Button _close, Button _copy, Button _import)
    {
        if (_open != null)
        {
            Navigation nav = _open.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.wrapAround = false;
            nav.selectOnUp = nav.selectOnDown = nav.selectOnLeft = nav.selectOnRight = null;
            _open.navigation = nav;
        }

        var panelButtons = new[] { _close, _copy, _import };
        for (int i = 0; i < panelButtons.Length; i++)
        {
            if (panelButtons[i] == null)
            {
                continue;
            }

            Navigation nav = panelButtons[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.wrapAround = false;
            nav.selectOnLeft = nav.selectOnRight = null;
            nav.selectOnUp = i > 0 ? panelButtons[i - 1] : null;
            nav.selectOnDown = i < panelButtons.Length - 1 ? panelButtons[i + 1] : null;
            panelButtons[i].navigation = nav;
        }
    }

    private static void Require(UnityEngine.Object _reference, string _fieldName)
    {
        if (_reference == null)
        {
            throw new System.InvalidOperationException($"PresetShare/Author: {_fieldName} 참조가 비어 있어 저작을 진행할 수 없습니다.");
        }
    }

    private static void Require(bool _condition, string _fieldName)
    {
        if (!_condition)
        {
            throw new System.InvalidOperationException($"PresetShare/Author: {_fieldName} 조건을 만족하지 않아 저작을 진행할 수 없습니다.");
        }
    }
}