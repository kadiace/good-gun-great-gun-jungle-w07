using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using WeaponWorkshop;

// One explicit setup request connects the prototype; it never enters Play Mode or runs tests.
[InitializeOnLoad]
internal static class ShootingTestAuthoring
{
    private const string RequestPath = "Library/AgentShootingTestsSetup.txt";
    private const string CompletionPath = "Library/AgentShootingTestsSetupCompleted.txt";
    private const string PrefabFolder = "Assets/Prefabs/Range/ShootingTests";
    private const string DataFolder = "Assets/Data/ShootingTests";
    private static double nextPoll;
    static ShootingTestAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 2d;
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(RequestPath);
        Setup();
    }

    [MenuItem("Tools/Weapon Workshop/Connect Shooting Test Prototype")]
    public static void Setup()
    {
        var changed = new List<string>();
        EnsureFolder(PrefabFolder); EnsureFolder(DataFolder);
        var definitions = new ShootingTestData[3];
        string[] ids = { "behind-barrier", "wide-targets", "around-cover" };
        string[] names = { "장애물 뒤 표적 테스트", "넓게 흩어진 표적 테스트", "엄폐 뒤 표적 테스트" };
        string[] instructions = {
            "저항판 뒤 초록 표적 3개를 파괴하세요. 붉은 상자는 보호 대상입니다.",
            "좌우 초록 표적 3개를 파괴하세요. 총열 배치·분기·산탄·회전을 활용해 보세요.",
            "엄폐 뒤 초록 표적 2개를 파괴하세요. 밝은 벽에서 반사하거나 범위 공격을 활용하세요."
        };
        string[] hints = {
            "앞 저항판의 관통 저항은 2입니다. 관통·차지와 뒤쪽 폭발 범위를 조절해 보세요.",
            "표적 사이 간격이 넓습니다. 총구 위치·분기·산탄 범위를 조절하거나 불필요한 파츠를 줄여 보세요.",
            "엄폐물은 파괴할 수 없습니다. 반사각과 폭발 범위를 조절하고 붉은 상자를 피해 보세요."
        };
        var cube = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TagCheckTargets/CustomCube.prefab");
        if (cube == null) throw new System.InvalidOperationException("CustomCube 표적 프리팹을 찾을 수 없습니다.");
        for (int i = 0; i < definitions.Length; i++)
        {
            string layoutPath = PrefabFolder + "/Test_" + ids[i] + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(layoutPath) == null)
            {
                var root = new GameObject(names[i]);
                root.SetActive(false);
                root.AddComponent<ShootingTestLayout>();
                if (i == 0)
                {
                    Box(cube, root.transform, "관통 저항판", new Vector3(0f, 1.5f, 1.5f), new Vector3(2.8f, 2.4f, .25f), 2f, 100f, false, false);
                    Box(cube, root.transform, "왼쪽 목표", new Vector3(-.85f, 1.4f, 3.7f), new Vector3(.65f, 1.4f, .65f), 0f, 0f, true, false);
                    Box(cube, root.transform, "가운데 목표", new Vector3(0f, 1.4f, 3.7f), new Vector3(.65f, 1.4f, .65f), 0f, 0f, true, false);
                    Box(cube, root.transform, "오른쪽 목표", new Vector3(.85f, 1.4f, 3.7f), new Vector3(.65f, 1.4f, .65f), 0f, 0f, true, false);
                    Box(cube, root.transform, "보호 상자", new Vector3(2f, 1.2f, 4.4f), new Vector3(.65f, .9f, .65f), 0f, 0f, true, true);
                }
                else if (i == 1)
                {
                    Box(cube, root.transform, "왼쪽 목표", new Vector3(-2f, 1.5f, 2.8f), new Vector3(.9f, 1.6f, .7f), 0f, 0f, true, false);
                    Box(cube, root.transform, "가운데 목표", new Vector3(0f, 2.1f, 3.4f), new Vector3(.9f, 1.6f, .7f), 0f, 0f, true, false);
                    Box(cube, root.transform, "오른쪽 목표", new Vector3(2f, 1.5f, 2.8f), new Vector3(.9f, 1.6f, .7f), 0f, 0f, true, false);
                    Box(cube, root.transform, "보호 상자", new Vector3(0f, .5f, 2.2f), new Vector3(.7f, .7f, .7f), 0f, 0f, true, true);
                }
                else
                {
                    Wall(root.transform, "엄폐물", new Vector3(.8f, 1.45f, .6f), new Vector3(3.5f, 2.9f, .25f), Color.gray);
                    Wall(root.transform, "반사 벽", new Vector3(-2.1f, 1.45f, 1f), new Vector3(.2f, 2.9f, 7.5f), new Color(.8f, .9f, 1f));
                    Box(cube, root.transform, "앞쪽 목표", new Vector3(.3f, 1.5f, 3.1f), new Vector3(.8f, 1.4f, .7f), 0f, 0f, true, false);
                    Box(cube, root.transform, "뒤쪽 목표", new Vector3(.8f, 1.5f, 4.3f), new Vector3(.8f, 1.4f, .7f), 0f, 0f, true, false);
                    Box(cube, root.transform, "보호 상자", new Vector3(2f, 1.2f, 4.4f), new Vector3(.65f, .9f, .65f), 0f, 0f, true, true);
                }
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, layoutPath);
                Object.DestroyImmediate(root);
            }
            string dataPath = DataFolder + "/Test_" + ids[i] + ".asset";
            definitions[i] = AssetDatabase.LoadAssetAtPath<ShootingTestData>(dataPath);
            if (definitions[i] == null)
            {
                definitions[i] = ScriptableObject.CreateInstance<ShootingTestData>();
                var data = new SerializedObject(definitions[i]);
                data.FindProperty("testId").stringValue = ids[i];
                data.FindProperty("displayName").stringValue = names[i];
                data.FindProperty("instructions").stringValue = instructions[i];
                data.FindProperty("failureHint").stringValue = hints[i];
                data.FindProperty("layoutPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(layoutPath);
                data.FindProperty("timeLimit").floatValue = 30f;
                data.FindProperty("requiredDestruction").floatValue = .7f;
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(definitions[i], dataPath);
            }
            changed.Add(layoutPath); changed.Add(dataPath);
        }
        ConnectRange(changed);
        EditPrefab("Assets/Prefabs/Player/Player.prefab", root => {
            if (root.GetComponent<PlayerShootingTest>() == null) root.AddComponent<PlayerShootingTest>();
        }, changed);
        EditPrefab("Assets/Prefabs/Managers/UIManager.prefab", root => {
            var selection = root.GetComponentInChildren<TargetSelectionUI>(true);
            var data = new SerializedObject(selection);
            var tests = data.FindProperty("timedTests"); tests.arraySize = definitions.Length;
            for (int i = 0; i < definitions.Length; i++) tests.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            var button = data.FindProperty("buttonTemplate").objectReferenceValue as UnityEngine.UI.Button;
            var ui = root.GetComponentInChildren<UIManager>(true);
            var hud = ui.Screen<ShootingTestHUD>() ?? ui.gameObject.AddComponent<ShootingTestHUD>();
            var hudData = new SerializedObject(hud);
            if (button != null) hudData.FindProperty("font").objectReferenceValue = button.GetComponentInChildren<TMP_Text>(true)?.font;
            hudData.ApplyModifiedPropertiesWithoutUndo(); data.ApplyModifiedPropertiesWithoutUndo();
        }, changed);
        foreach (string path in changed) AssetDatabase.ImportAsset(path);
        File.WriteAllText(CompletionPath, string.Join("\n", changed));
        Debug.Log("사격 테스트 3종·TargetBell·플레이어 F 재시도·HUD 연결 완료. 플레이 테스트는 실행하지 않았습니다.");
    }

    private static void ConnectRange(List<string> changed)
    {
        EditPrefab("Assets/Prefabs/Map/ShootingRange.prefab", root => {
            var bell = root.GetComponentInChildren<TargetBell>(true);
            var controller = root.GetComponent<ShootingTestController>() ?? root.AddComponent<ShootingTestController>();
            // Unit-scale range root owns geometry; the bell itself has a nonuniform display scale.
            var point = root.transform.Find("TimedTestPoint");
            if (point == null)
            {
                point = new GameObject("TimedTestPoint").transform;
                point.SetParent(root.transform, false);
                point.localPosition = new Vector3(-2f, 0f, 0f);
                point.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            var data = new SerializedObject(controller);
            data.FindProperty("bell").objectReferenceValue = bell;
            data.FindProperty("layoutPoint").objectReferenceValue = point;
            data.FindProperty("retryDistance").floatValue = 3f;
            data.ApplyModifiedPropertiesWithoutUndo();
            var bellData = new SerializedObject(bell);
            bellData.FindProperty("testController").objectReferenceValue = controller;
            bellData.ApplyModifiedPropertiesWithoutUndo();
        }, changed);
    }

    private static void Box(GameObject template, Transform parent, string label, Vector3 position, Vector3 size,
        float penetration, float explosion, bool goal, bool protect)
    {
        var box = (GameObject)PrefabUtility.InstantiatePrefab(template);
        PrefabUtility.UnpackPrefabInstance(box, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        box.SetActive(false); box.name = label; box.transform.SetParent(parent, false);
        var target = box.GetComponent<DestructibleTarget>();
        target.ConfigureCustom(size, penetration, explosion, 0f, 0f);
        box.transform.localPosition = position - new Vector3(0f, size.y * .5f, 0f);
        var data = new SerializedObject(target);
        data.FindProperty("standaloneProp").boolValue = true;
        data.FindProperty("partSize").floatValue = .25f;
        data.ApplyModifiedPropertiesWithoutUndo();
        if (goal)
        {
            var marker = box.AddComponent<ShootingTestTarget>();
            var markerData = new SerializedObject(marker);
            markerData.FindProperty("displayName").stringValue = label;
            markerData.FindProperty("protectedTarget").boolValue = protect;
            markerData.ApplyModifiedPropertiesWithoutUndo();
        }
        Tint(box, protect ? new Color(.95f, .12f, .1f) : goal ? new Color(.2f, .8f, .4f) : new Color(.95f, .65f, .12f));
        box.SetActive(true);
    }

    private static void Wall(Transform parent, string label, Vector3 position, Vector3 size, Color color)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = label; wall.transform.SetParent(parent, false);
        wall.transform.localPosition = position; wall.transform.localScale = size;
        Tint(wall, color);
    }

    private static void Tint(GameObject target, Color color)
    {
        EnsureFolder("Assets/Materials/Range");
        string path = "Assets/Materials/Range/Test_" + ColorUtility.ToHtmlStringRGB(color) + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color; AssetDatabase.CreateAsset(material, path);
            AssetDatabase.ImportAsset(path);
        }
        foreach (var renderer in target.GetComponentsInChildren<MeshRenderer>(true)) renderer.sharedMaterial = material;
    }

    private static void EditPrefab(string path, System.Action<GameObject> edit, List<string> changed)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); changed.Add(path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
