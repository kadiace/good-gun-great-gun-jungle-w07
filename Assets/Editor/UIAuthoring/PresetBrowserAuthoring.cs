using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WeaponWorkshop;

// Prefab authoring only. Runtime code never positions or resizes the browser.
public static class PresetBrowserAuthoring
{
    private const string Path = "Assets/Prefabs/UI/Workbench/PresetPanelScreen.prefab";
    private static readonly Color Background = new Color(.085f, .10f, .12f, 1);
    private static readonly Color Card = new Color(.14f, .17f, .20f, 1);
    private static readonly Color Gold = new Color(.91f, .74f, .38f, 1);
    private static readonly Color Muted = new Color(.65f, .70f, .75f, 1);
    private static TMP_FontAsset font;

    [MenuItem("Tools/UI/Authoring/PresetShare/Author Preset Browser")]
    public static void Author()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NanumSquareNeo/TMP/NanumSquareNeo-Regular Dynamic.asset");
        GameObject root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var view = root.GetComponent<PresetPanelView>();
            var bridge = new SerializedObject(root.GetComponent<PresetPanelUI>());
            var fields = new SerializedObject(view);
            // Preserve the original root, bar and three slot IDs referenced by UIManager overrides.
            var bar = (GameObject)bridge.FindProperty("panel").objectReferenceValue;
            var oldSlots = bridge.FindProperty("slots");
            var open = (Button)oldSlots.GetArrayElementAtIndex(0).objectReferenceValue;
            for (int i = 1; i < oldSlots.arraySize; i++)
            {
                var old = oldSlots.GetArrayElementAtIndex(i).objectReferenceValue as Button;
                if (old != null) old.gameObject.SetActive(false);
            }
            open.gameObject.name = "OpenPresetBrowser";
            foreach (var layout in bar.GetComponents<Behaviour>())
                if (layout is LayoutGroup || layout is ContentSizeFitter) layout.enabled = false;
            StyleOpener(bar, open);
            var oldLayout = open.GetComponent<LayoutElement>();
            if (oldLayout != null) oldLayout.ignoreLayout = true;

            Transform oldBrowser = root.transform.Find("PresetBrowser");
            if (oldBrowser != null) UnityEngine.Object.DestroyImmediate(oldBrowser.gameObject);
            var browser = Rect("PresetBrowser", root.transform, Vector2.zero, Vector2.zero);
            Stretch(browser);
            Image shade = browser.gameObject.AddComponent<Image>();
            shade.color = new Color(0, 0, 0, .72f);
            var window = Surface("Window", browser, Vector2.zero, new Vector2(1100, 690), Background);
            Place(window, Vector2.zero, new Vector2(1100, 690), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            Text("Title", window, "무기 프리셋", new Vector2(32, -26), new Vector2(700, 42), 30, Gold);
            Text("Subtitle", window, "저장된 설계를 선택하고 비용을 확인한 뒤 적용하세요.", new Vector2(32, -75), new Vector2(980, 28), 18, Muted);
            var close = Button("Close", window, "닫기", new Vector2(970, -26), new Vector2(98, 40));
            Surface("Divider", window, new Vector2(32, -119), new Vector2(1036, 2), new Color(.3f, .34f, .38f, 1));
            Text("LibraryTitle", window, "저장 목록", new Vector2(32, -137), new Vector2(680, 28), 21, Color.white);
            var grid = Rect("Slots", window, new Vector2(32, -180), new Vector2(678, 414));
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(214, 198); gridLayout.spacing = new Vector2(18, 18);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gridLayout.constraintCount = 3;
            var slots = new Button[WeaponPresets.SlotCount];
            var names = new TMP_Text[slots.Length]; var states = new TMP_Text[slots.Length];
            var previews = new PresetBlueprintGraphic[slots.Length]; var frames = new GameObject[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = Button("Slot" + (i + 1), grid, null, Vector2.zero, new Vector2(214, 198));
                slots[i] = slot;
                var frame = Surface("Selected", slot.transform, Vector2.zero, new Vector2(214, 198), Gold);
                frames[i] = frame.gameObject; frame.GetComponent<Image>().raycastTarget = false;
                Surface("Inset", frame, new Vector2(3, -3), new Vector2(208, 192), Card).GetComponent<Image>().raycastTarget = false;
                frames[i].SetActive(false);
                Text("Index", slot.transform, (i + 1).ToString("00"), new Vector2(14, -10), new Vector2(42, 24), 16, Gold);
                previews[i] = Preview("Silhouette", slot.transform, new Vector2(14, -40), new Vector2(186, 95));
                names[i] = Text("Name", slot.transform, "빈 슬롯", new Vector2(14, -140), new Vector2(186, 26), 19, Color.white);
                states[i] = Text("State", slot.transform, "현재 무기를 저장하세요", new Vector2(14, -170), new Vector2(186, 20), 14, Muted);
            }
            Text("SaveScope", window, "저장 범위: 이름 · 모듈 배치 · 방향 · 모듈 색상\n면 도색 · 스티커 · 키링은 저장되지 않습니다.", new Vector2(32, -618), new Vector2(678, 46), 16, Muted);

            var detail = Surface("SelectedPreset", window, new Vector2(738, -137), new Vector2(330, 527), Card);
            var title = Text("Name", detail, "프리셋을 선택하세요", new Vector2(18, -16), new Vector2(294, 35), 23, Gold);
            var detailPreview = Preview("Silhouette", detail, new Vector2(18, -62), new Vector2(294, 116));
            var status = Text("Status", detail, "", new Vector2(18, -187), new Vector2(294, 27), 17, Muted);
            var cost = Text("Cost", detail, "", new Vector2(18, -223), new Vector2(294, 56), 19, Color.white);
            var note = Text("Note", detail, "", new Vector2(18, -288), new Vector2(294, 60), 16, Muted);
            var rename = Input("RenameInput", detail, new Vector2(18, -357), new Vector2(208, 38));
            var renameButton = Button("Rename", detail, "변경", new Vector2(234, -357), new Vector2(78, 38));
            var save = Button("Save", detail, "현재 무기 저장", new Vector2(18, -408), new Vector2(294, 43));
            var load = Button("Apply", detail, "프리셋 적용", new Vector2(18, -460), new Vector2(202, 43));
            var delete = Button("Delete", detail, "삭제", new Vector2(228, -460), new Vector2(84, 43));
            load.image.color = new Color(.34f, .29f, .18f, 1);
            delete.image.color = new Color(.29f, .16f, .17f, 1);
            // Explicit controller/keyboard navigation; all input goes through existing EventSystem actions.
            for (int i = 0; i < slots.Length; i++)
                Nav(slots[i], i >= 3 ? slots[i - 3] : close, i < 3 ? slots[i + 3] : save,
                    i % 3 > 0 ? slots[i - 1] : null, i % 3 < 2 ? slots[i + 1] : rename);
            Nav(rename, close, save, slots[2], renameButton);
            Nav(renameButton, close, save, rename, null);
            Nav(save, rename, load, slots[5], null);
            Nav(load, save, close, slots[5], delete);
            Nav(delete, save, close, load, null);
            Nav(close, load, slots[0], slots[2], null);

            Ref(fields, "m_panel", bar); Ref(fields, "m_browser", browser.gameObject); Ref(fields, "m_openButton", open);
            Array(fields, "m_browserSlots", slots); Array(fields, "m_slotNames", names); Array(fields, "m_slotStates", states);
            Array(fields, "m_slotPreviews", previews); Array(fields, "m_selectionFrames", frames);
            Ref(fields, "m_detailPreview", detailPreview); Ref(fields, "m_detailTitle", title); Ref(fields, "m_detailStatus", status);
            Ref(fields, "m_detailCost", cost); Ref(fields, "m_detailNote", note);
            Ref(fields, "m_browserSave", save); Ref(fields, "m_browserLoad", load); Ref(fields, "m_browserClose", close);
            Ref(fields, "m_deleteButton", delete); Ref(fields, "m_renameInput", rename); Ref(fields, "m_renameButton", renameButton);
            fields.ApplyModifiedPropertiesWithoutUndo();
            bar.SetActive(false); browser.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, Path);
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);
            Debug.Log("Preset browser authored: six slots, local references, explicit save/apply and price details.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Update the opener without rebuilding the user's browser layout or its serialized IDs.
    [MenuItem("Tools/UI/Authoring/PresetShare/Improve Preset Opener")]
    public static void ImproveOpener()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NanumSquareNeo/TMP/NanumSquareNeo-Regular Dynamic.asset");
        GameObject root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var fields = new SerializedObject(root.GetComponent<PresetPanelView>());
            var bar = (GameObject)fields.FindProperty("m_panel").objectReferenceValue;
            var open = (Button)fields.FindProperty("m_openButton").objectReferenceValue;
            StyleOpener(bar, open);
            if (PrefabUtility.SaveAsPrefabAsset(root, Path) == null) throw new InvalidOperationException("프리셋 프리팹 저장 실패");
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void StyleOpener(GameObject bar, Button open)
    {
        Place((RectTransform)bar.transform, new Vector2(0, -63), new Vector2(276, 60), new Vector2(.5f, 1), new Vector2(.5f, 1));
        Stretch((RectTransform)open.transform);
        open.image.color = new Color(.94f, .74f, .32f, 1);
        var colors = open.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1f, 1f, .86f, 1);
        colors.selectedColor = new Color(1f, 1f, .86f, 1); colors.pressedColor = new Color(.78f, .78f, .78f, 1);
        colors.disabledColor = new Color(.6f, .6f, .6f, .8f); open.colors = colors;
        var outline = open.GetComponent<Outline>() ?? open.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, .89f, .57f, 1); outline.effectDistance = new Vector2(2, 2);
        var caption = open.GetComponentInChildren<TMP_Text>(true);
        caption.text = "무기 프리셋 열기"; caption.font = font; caption.fontSize = 26; caption.fontStyle = FontStyles.Bold;
        caption.color = new Color(.08f, .1f, .13f, 1); caption.alignment = TextAlignmentOptions.Center;
        caption.raycastTarget = false; caption.margin = new Vector4(12, 0, 12, 0);
        Stretch((RectTransform)caption.transform);
        open.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        Place(rect, position, size, new Vector2(0, 1), new Vector2(0, 1)); return rect;
    }
    private static void Place(RectTransform r, Vector2 pos, Vector2 size, Vector2 anchor, Vector2 pivot)
    { r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size; r.localScale = Vector3.one; }
    private static void Stretch(RectTransform r)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = r.sizeDelta = Vector2.zero; }
    private static RectTransform Surface(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    { var r = Rect(name, parent, pos, size); r.gameObject.AddComponent<Image>().color = color; return r; }
    private static TMP_Text Text(string name, Transform parent, string text, Vector2 pos, Vector2 size, int pointSize, Color color)
    {
        var r = Rect(name, parent, pos, size); var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.fontSize = pointSize; t.text = text; t.color = color; t.richText = false;
        t.raycastTarget = false; t.alignment = TextAlignmentOptions.TopLeft;
        t.overflowMode = TextOverflowModes.Ellipsis; return t;
    }
    private static Button Button(string name, Transform parent, string text, Vector2 pos, Vector2 size)
    {
        var r = Surface(name, parent, pos, size, Card); var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = r.GetComponent<Image>();
        var colors = b.colors; colors.selectedColor = new Color(1, .91f, .67f); colors.highlightedColor = new Color(1, .95f, .8f); b.colors = colors;
        if (text != null)
        { var t = Text("Label", r, text, Vector2.zero, size, 18, Color.white); Stretch((RectTransform)t.transform); t.alignment = TextAlignmentOptions.Center; }
        return b;
    }
    private static PresetBlueprintGraphic Preview(string name, Transform parent, Vector2 pos, Vector2 size)
    { var r = Rect(name, parent, pos, size); var graphic = r.gameObject.AddComponent<PresetBlueprintGraphic>(); graphic.raycastTarget = false; return graphic; }
    private static TMP_InputField Input(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var r = Surface(name, parent, pos, size, Background); var input = r.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = r.GetComponent<Image>();
        var viewport = Rect("Viewport", r, new Vector2(9, -4), size - new Vector2(18, 8)); viewport.gameObject.AddComponent<RectMask2D>();
        var text = Text("Text", viewport, "", Vector2.zero, viewport.sizeDelta, 17, Color.white); text.alignment = TextAlignmentOptions.MidlineLeft;
        var placeholder = Text("Placeholder", viewport, "프리셋 이름", Vector2.zero, viewport.sizeDelta, 17, Muted); placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        input.textViewport = viewport; input.textComponent = (TextMeshProUGUI)text; input.placeholder = placeholder;
        input.characterLimit = 40; input.lineType = TMP_InputField.LineType.SingleLine; input.richText = false;
        return input;
    }
    private static void Nav(Selectable item, Selectable up, Selectable down, Selectable left, Selectable right)
    { item.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right }; }
    private static void Ref(SerializedObject so, string name, UnityEngine.Object value)
    { var p = so.FindProperty(name); if (p == null) throw new InvalidOperationException("Missing view field: " + name); p.objectReferenceValue = value; }
    private static void Array<T>(SerializedObject so, string name, T[] values) where T : UnityEngine.Object
    { var p = so.FindProperty(name); if (p == null) throw new InvalidOperationException(name); p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
}
