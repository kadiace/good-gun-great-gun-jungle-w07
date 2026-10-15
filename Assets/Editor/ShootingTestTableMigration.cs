#pragma warning disable 618
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WeaponWorkshop;

// Explicit authoring operation. Never regenerates definitions during play or ordinary scene edits.
[InitializeOnLoad]
internal static class ShootingTestTableMigration
{
    private const string RequestPath = "Library/AgentShootingTestTableMigration.txt";
    private const string DonePath = "Library/AgentShootingTestTableMigrationCompleted.txt";
    private static double nextCheck;
    static ShootingTestTableMigration() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 2d;
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(RequestPath);
        Migrate();
    }
    [MenuItem("Tools/Weapon Workshop/Migrate Shooting Test Table")]
    private static void Migrate()
    {
        const string tablePath = "Assets/Resources/ShootingTestTableData.json";
        const string requestPath = "Assets/Resources/CustomerRequestData.json";
        var table = File.Exists(tablePath) ? JsonUtility.FromJson<ShootingTestTableData>(File.ReadAllText(tablePath)) : new ShootingTestTableData();
        var entries = new List<ShootingTestTableData.Entry>(table.tests);
        var requests = JsonUtility.FromJson<CustomerRequestData>(File.ReadAllText(requestPath));
        var rows = new List<CustomerRequestData.Request>(requests.requests);
        var bindings = AssetDatabase.FindAssets("t:ShootingTestData", new[] { "Assets/Data/ShootingTests" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<ShootingTestData>).ToArray();
        foreach (var binding in bindings)
        {
            var data = new SerializedObject(binding);
            string id = data.FindProperty("testId").stringValue;
            if (string.IsNullOrWhiteSpace(id) || data.FindProperty("layoutPrefab").objectReferenceValue == null)
                throw new InvalidOperationException("사격 테스트 ID/배치 프리팹이 없습니다: " + binding.name);
            var entry = entries.Find(e => e.id == id);
            if (entry == null)
            {
                float time = data.FindProperty("timeLimit").floatValue;
                if (time <= 0f) throw new InvalidOperationException("이미 이관된 시험 정의를 JSON에서 복원하세요: " + id);
                entry = new ShootingTestTableData.Entry { id = id, name = data.FindProperty("displayName").stringValue,
                    instructions = data.FindProperty("instructions").stringValue, failureHint = data.FindProperty("failureHint").stringValue,
                    timeLimit = time, requiredDestruction = data.FindProperty("requiredDestruction").floatValue };
                entries.Add(entry);
            }
            string requestId = "shooting-test/" + id;
            if (!rows.Any(r => r.id == requestId)) rows.Add(new CustomerRequestData.Request {
                id = requestId, name = entry.name + " 통과", phrase = entry.name + "를 통과한 총",
                requiredTestIds = new[] { id } });
        }
        table.tests = entries.OrderBy(e => e.id, StringComparer.Ordinal).ToArray();
        requests.requests = rows.OrderBy(e => e.id, StringComparer.Ordinal).ToArray();
        var changed = new List<string>();
        Write(tablePath, JsonUtility.ToJson(table, true), changed);
        Write(requestPath, JsonUtility.ToJson(requests, true), changed);
        foreach (var binding in bindings)
        {
            var data = new SerializedObject(binding);
            foreach (string field in new[] { "displayName", "instructions", "failureHint" }) data.FindProperty(field).stringValue = "";
            data.FindProperty("timeLimit").floatValue = 0f;
            data.FindProperty("requiredDestruction").floatValue = 0f;
            if (!data.ApplyModifiedPropertiesWithoutUndo()) continue;
            AssetDatabase.SaveAssetIfDirty(binding);
            changed.Add(AssetDatabase.GetAssetPath(binding));
        }
        foreach (string path in changed.Distinct()) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TableManager.ReloadAfterTableEdit();
        File.WriteAllLines(DonePath, changed);
        Debug.Log("사격 테스트 정의 이관 및 NPC 요청 3종 추가 완료. 기존 NPC/조합 후보는 유지했습니다.");
    }
    private static void Write(string path, string value, List<string> changed)
    {
        if (File.Exists(path) && File.ReadAllText(path) == value) return;
        File.WriteAllText(path, value, new System.Text.UTF8Encoding(false)); changed.Add(path);
    }
}
