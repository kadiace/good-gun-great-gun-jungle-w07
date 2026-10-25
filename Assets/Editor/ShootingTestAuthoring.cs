using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WeaponWorkshop;

// 명시적으로 요청한 자산 연결/임포트만 수행한다. 시험 수치와 배치는 덮어쓰지 않는다.
[InitializeOnLoad]
internal static class ShootingTestAuthoring
{
    private const string RequestPath = "Library/AgentShootingTestsSetup.txt";
    private const string CompletionPath = "Library/AgentShootingTestsSetupCompleted.txt";
    private const string PrefabFolder = "Assets/Prefabs/Range/ShootingTests";
    private const string UiPrefabPath = "Assets/Prefabs/Managers/UIManager.prefab";
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

    [MenuItem("Tools/Weapon Workshop/Connect Shooting Tests")]
    public static void Setup()
    {
        TableManager.ReloadAfterTableEdit();
        var root = PrefabUtility.LoadPrefabContents(UiPrefabPath);
        try
        {
            var selection = root.GetComponentInChildren<TargetSelectionUI>(true);
            if (selection == null) throw new InvalidOperationException("TargetSelectionUI 연결을 확인하세요.");
            var table = TableManager.Instance.ShootingTestTable;
            var rows = new List<ShootingTestTableData.Entry>();
            foreach (var row in table.tests)
                if (row != null && table.IsAvailable(row.id)) rows.Add(row);
            var data = new SerializedObject(selection);
            var bindings = data.FindProperty("timedTestLayouts");
            bindings.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                // 에디터 연결 편의 규칙이다. 런타임은 직렬화된 프리팹 참조만 사용한다.
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/Test_" + rows[i].id + ".prefab");
                if (prefab == null) throw new InvalidOperationException("시험 배치 프리팹이 없습니다: " + rows[i].id);
                var binding = bindings.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("testId").stringValue = rows[i].id;
                binding.FindPropertyRelative("layoutPrefab").objectReferenceValue = prefab;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, UiPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.ImportAsset(UiPrefabPath);
        File.WriteAllText(CompletionPath, UiPrefabPath);
        Debug.Log("시험 표의 ID와 배치 프리팹 연결 완료.");
    }
}
