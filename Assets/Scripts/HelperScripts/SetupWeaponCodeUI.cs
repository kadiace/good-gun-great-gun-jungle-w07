using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using WeaponWorkshop;

public static class SetupWeaponCodeUI
{
    private static TMP_FontAsset font;
    public static string Main()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ModuleData", new[] { "Assets/Data/Modules" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = new SerializedObject(AssetDatabase.LoadAssetAtPath<ModuleData>(path));
            data.FindProperty("codeId").stringValue = guid;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data.targetObject);
            AssetDatabase.SaveAssetIfDirty(data.targetObject);
        }
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NanumSquareNeo/TMP/NanumSquareNeo-Regular Dynamic.asset");
        string prefabPath = "Assets/Prefabs/Managers/UIManager.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var controller = root.AddComponent<WeaponCodeUI>();
            var open = Button("WeaponCodeButton", root.transform, "무기 이름 / 공유 코드", new Vector2(0, -32), new Vector2(230, 44));
            var openRect = (RectTransform)open.transform;
            openRect.anchorMin = openRect.anchorMax = new Vector2(.5f, 1);
            open.gameObject.SetActive(false);
            var panel = Rect("WeaponCodePanel", root.transform, Vector2.zero, Vector2.zero);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .68f);
            var card = Rect("Card", panel, Vector2.zero, new Vector2(680, 570));
            card.gameObject.AddComponent<Image>().color = new Color(.16f,.18f,.20f,1);
            Text("Title", card, "무기 이름 / 공유 코드", new Vector2(0,235), new Vector2(570,44), 27);
            Text("NameLabel", card, "무기 이름", new Vector2(0,181), new Vector2(580,26), 18);
            var name = Input("WeaponName", card, new Vector2(0,140), new Vector2(580,48), false);
            name.characterLimit = 40;
            Text("CodeLabel", card, "코드를 복사하거나 여기에 붙여넣어 주세요", new Vector2(0,89), new Vector2(580,28), 18);
            var code = Input("ShareCode", card, new Vector2(0,-32), new Vector2(580,200), true);
            code.characterLimit = 1000000;
            Text("Hint", card, "이름·모듈 배치·컬러를 포함합니다.\n도색·스티커·키링은 포함하지 않습니다.", new Vector2(0,-162), new Vector2(580,54), 16);
            var copy = Button("Copy", card, "현재 무기 코드 복사", new Vector2(-150,-232), new Vector2(280,44));
            var import = Button("Import", card, "붙여넣은 코드 적용", new Vector2(150,-232), new Vector2(280,44));
            var close = Button("Close", card, "닫기", new Vector2(267,235), new Vector2(82,36));
            var properties = new SerializedObject(controller);
            properties.FindProperty("panel").objectReferenceValue = panel.gameObject;
            properties.FindProperty("openButton").objectReferenceValue = open;
            properties.FindProperty("closeButton").objectReferenceValue = close;
            properties.FindProperty("copyButton").objectReferenceValue = copy;
            properties.FindProperty("importButton").objectReferenceValue = import;
            properties.FindProperty("nameInput").objectReferenceValue = name;
            properties.FindProperty("codeInput").objectReferenceValue = code;
            properties.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return "Created name/code UI and initialized stable module IDs.";
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        r.SetParent(parent,false); r.anchorMin = r.anchorMax = new Vector2(.5f,.5f);
        r.anchoredPosition = position; r.sizeDelta = size; return r;
    }
    private static TextMeshProUGUI Text(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var rect = Rect(name,parent,position,size); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font=font; text.text=value; text.fontSize=fontSize; text.color=new Color(.93f,.93f,.90f);
        text.alignment=TextAlignmentOptions.MidlineLeft; text.raycastTarget=false; text.richText=false;
        return text;
    }
    private static Button Button(string name, Transform parent, string caption, Vector2 position, Vector2 size)
    {
        var rect=Rect(name,parent,position,size); var image=rect.gameObject.AddComponent<Image>(); image.color=Color.white;
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;colors.normalColor=new Color(.22f,.25f,.28f);colors.highlightedColor=new Color(.31f,.35f,.39f);colors.pressedColor=new Color(.11f,.13f,.15f);button.colors=colors;
        var nav=button.navigation;nav.mode=Navigation.Mode.None;button.navigation=nav;
        Text("Label",rect,caption,Vector2.zero,size-new Vector2(16,0),18).alignment=TextAlignmentOptions.Center;
        return button;
    }
    private static TMP_InputField Input(string name, Transform parent, Vector2 position, Vector2 size, bool multiline)
    {
        var rect=Rect(name,parent,position,size);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.09f,.105f,.12f,1);
        var input=rect.gameObject.AddComponent<TMP_InputField>();input.targetGraphic=image;
        var viewport=Rect("Viewport",rect,Vector2.zero,size-new Vector2(24,12));viewport.gameObject.AddComponent<RectMask2D>();
        var text=Text("Text",viewport,"",Vector2.zero,viewport.sizeDelta,multiline?15:22);
        text.alignment=multiline?TextAlignmentOptions.TopLeft:TextAlignmentOptions.MidlineLeft;
        var placeholder=Text("Placeholder",viewport,multiline?"WGG1:…":"나만의 총",Vector2.zero,viewport.sizeDelta,multiline?15:22);
        placeholder.color=new Color(.55f,.58f,.60f);placeholder.alignment=text.alignment;
        input.textViewport=viewport;input.textComponent=text;input.placeholder=placeholder;
        input.lineType=multiline?TMP_InputField.LineType.MultiLineNewline:TMP_InputField.LineType.SingleLine;
        input.richText=false;return input;
    }
}
