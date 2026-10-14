using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WeaponWorkshop;

public static class SetupRaidObjectives
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit mode required.");
        const string materialPath = "Assets/Materials/RaidExit.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(.06f, .7f, .3f));
            material.SetColor("_EmissionColor", new Color(.03f, .65f, .22f));
            material.EnableKeyword("_EMISSION");
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.ImportAsset(materialPath);
        }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NanumSquareNeo/TMP/NanumSquareNeo-Bold Dynamic.asset");
        if (font == null) throw new InvalidOperationException("Workshop UI font missing.");
        foreach (string name in new[] { "WoodenHouse", "BankGrand", "Millitary_base", "JungleCampus" })
        {
            string path = "Assets/Scenes/NightRaid/NightRaid_" + name + ".unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Unsaved scene: " + path);
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var area = roots.SelectMany(r => r.GetComponentsInChildren<NightRaidArea>(true)).Single();
                var ui = roots.SelectMany(r => r.GetComponentsInChildren<UIManager>(true)).Single();
                if (area.GetComponent<RaidObjectiveUI>() != null) continue;
                var player = ui.Player;
                var areaData = new SerializedObject(area);
                Vector2 size = areaData.FindProperty("mapSize").vector2Value;
                Vector3 center = areaData.FindProperty("mapCenter").vector3Value;
                var old = roots.SelectMany(r => r.GetComponentsInChildren<NightRaidBoundary>(true)).ToArray();
                if (old.Length != 1) throw new InvalidOperationException("Expected one original return gate: " + name);
                var exitRoot = new GameObject("RaidExits").transform;
                exitRoot.SetParent(area.transform, false);
                var exits = new NightRaidBoundary[4];
                var directions = new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right };
                var labels = new[] { "남쪽 탈출구", "북쪽 탈출구", "서쪽 탈출구", "동쪽 탈출구" };
                var names = new[] { "SouthExit", "NorthExit", "WestExit", "EastExit" };
                for (int i = 0; i < exits.Length; i++)
                {
                    var direction = directions[i];
                    var gate = i == 0 ? old[0].gameObject : new GameObject(names[i]);
                    gate.name = names[i];
                    gate.transform.SetParent(exitRoot, false);
                    float radius = (i < 2 ? size.y : size.x) * .5f - 4f;
                    gate.transform.localPosition = center + direction * radius;
                    gate.transform.localRotation = Quaternion.LookRotation(direction);
                    gate.transform.localScale = Vector3.one;
                    var boundary = gate.GetComponent<NightRaidBoundary>() ?? gate.AddComponent<NightRaidBoundary>();
                    var box = gate.GetComponent<BoxCollider>();
                    box.isTrigger = true; box.enabled = true;
                    box.center = new Vector3(0f, 1.5f, 0f);
                    box.size = new Vector3(4f, 3f, 3f);
                    var body = gate.GetComponent<Rigidbody>();
                    body.isKinematic = true; body.useGravity = false;
                    var data = new SerializedObject(boundary);
                    data.FindProperty("inwardDirection").vector3Value = area.transform.TransformDirection(-direction);
                    data.FindProperty("exitName").stringValue = labels[i];
                    data.ApplyModifiedPropertiesWithoutUndo();
                    exits[i] = boundary;
                    Part("ExitPad", gate.transform, new Vector3(0,.025f,0), new Vector3(4,.05f,3), material);
                    Part("LeftPost", gate.transform, new Vector3(-2.1f,1.6f,0), new Vector3(.2f,3.2f,.2f), material);
                    Part("RightPost", gate.transform, new Vector3(2.1f,1.6f,0), new Vector3(.2f,3.2f,.2f), material);
                    Part("Lintel", gate.transform, new Vector3(0,3.2f,0), new Vector3(4.4f,.25f,.2f), material);
                    for (int face = 0; face < 2; face++)
                    {
                        var sign = new GameObject(face == 0 ? "SignInside" : "SignOutside", typeof(TextMeshPro));
                        sign.transform.SetParent(gate.transform, false);
                        sign.transform.localPosition = new Vector3(0, 3.7f, face == 0 ? -.15f : .15f);
                        sign.transform.localRotation = Quaternion.Euler(0, face == 0 ? 0 : 180, 0);
                        var text = sign.GetComponent<TextMeshPro>();
                        text.font = font; text.fontSize = 5; text.alignment = TextAlignmentOptions.Center;
                        text.text = labels[i]; text.color = new Color(.4f, 1f, .6f);
                        text.rectTransform.sizeDelta = new Vector2(5,1);
                    }
                }
                var panel = new GameObject("RaidObjectivePanel", typeof(RectTransform)).GetComponent<RectTransform>();
                panel.SetParent(ui.transform, false);
                panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
                panel.offsetMin = panel.offsetMax = Vector2.zero;
                var status = Label("ObjectiveStatus", panel, font, new Vector2(850,50));
                status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(.5f,1f);
                status.rectTransform.anchoredPosition = new Vector2(0,-100);
                status.text = "";
                var template = Label("ObjectiveMarkerTemplate", panel, font, new Vector2(300,80));
                template.color = new Color(1f,.82f,.25f);
                template.gameObject.SetActive(false);
                panel.gameObject.SetActive(false);
                var hud = area.gameObject.AddComponent<RaidObjectiveUI>();
                var hudData = new SerializedObject(hud);
                hudData.FindProperty("player").objectReferenceValue = player;
                hudData.FindProperty("viewCamera").objectReferenceValue = player.GetComponentInChildren<Camera>(true);
                hudData.FindProperty("panel").objectReferenceValue = panel;
                hudData.FindProperty("status").objectReferenceValue = status;
                hudData.FindProperty("markerTemplate").objectReferenceValue = template;
                AssignArray(hudData.FindProperty("exits"), exits);
                AssignArray(hudData.FindProperty("spawners"), roots.SelectMany(r => r.GetComponentsInChildren<ModuleUnlockCrateSpawner>(true)).ToArray());
                hudData.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed: " + path);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            AssetDatabase.ImportAsset(path);
        }
        return "Saved four raid scenes with 16 exit zones and objective HUD references.";
    }

    private static void AssignArray(SerializedProperty property, UnityEngine.Object[] values)
    {
        property.arraySize = values.Length;
        for (int i=0; i<values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
    private static TextMeshProUGUI Label(string name, RectTransform parent, TMP_FontAsset font, Vector2 size)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f,.5f);
        label.rectTransform.sizeDelta = size;
        label.font = font; label.fontSize = 24; label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.outlineWidth = .2f;
        label.outlineColor = Color.black;
        return label;
    }
    private static void Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name; part.transform.SetParent(parent, false);
        part.transform.localPosition = position; part.transform.localScale = scale;
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial = material;
    }
}
