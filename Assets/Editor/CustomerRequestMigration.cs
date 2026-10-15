#pragma warning disable 618
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WeaponWorkshop;

// Explicit migration of request definitions, not a watcher that regenerates tables from scene content.
[InitializeOnLoad]
internal static class CustomerRequestMigration
{
    private const string RequestPath = "Library/AgentCustomerRequestMigration.txt";
    private const string DonePath = "Library/AgentCustomerRequestMigrationCompleted.txt";
    private const string JsonPath = "Assets/Resources/CustomerRequestData.json";
    private static double nextCheck;
    static CustomerRequestMigration() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 2d;
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(RequestPath);
        Migrate();
    }

    [MenuItem("Tools/Weapon Workshop/Migrate NPC Request Table")]
    public static void Migrate()
    {
        var table = File.Exists(JsonPath) ? JsonUtility.FromJson<CustomerRequestData>(File.ReadAllText(JsonPath)) : new CustomerRequestData();
        var requests = new List<CustomerRequestData.Request>(table.requests ?? Array.Empty<CustomerRequestData.Request>());
        var tags = new List<CustomerRequestData.Tag>(table.tags ?? Array.Empty<CustomerRequestData.Tag>());
        var legacySlots = new List<CustomerRequestData.LegacySlot>(table.legacySlots ?? Array.Empty<CustomerRequestData.LegacySlot>());
        var sourceTags = Assets<WeaponTypeData>(); var sourceRequests = Assets<GunArchetype>();
        var profiles = Assets<CustomerProfile>(); var pools = Assets<CustomerRequestPool>();
        var selected = new Dictionary<CustomerProfile, string[]>();
        var poolSelected = new Dictionary<CustomerRequestPool, string[]>();
        foreach (var tag in sourceTags)
        {
            string id = Id(tag);
            if (tags.Any(t => t.id == id)) continue;
            var data = new SerializedObject(tag);
            if (!string.IsNullOrEmpty(data.FindProperty("tableId").stringValue))
                throw new InvalidOperationException("이관된 태그 정의가 JSON에서 삭제되었습니다. 표를 복원하세요: " + id);
            var conditions = new List<CustomerRequestData.Condition>();
            var raw = data.FindProperty("conditions");
            for (int i = 0; i < raw.arraySize; i++)
            {
                var condition = raw.GetArrayElementAtIndex(i);
                var stat = (StatType)condition.FindPropertyRelative("stat").enumValueIndex;
                bool modules = condition.FindPropertyRelative("source").enumValueIndex != 0;
                var converted = NewCondition(modules ? GunMeasure.PathModules : StatMeasure(stat), stat, 0f, float.MaxValue);
                int comparison = condition.FindPropertyRelative("comparison").enumValueIndex;
                float value = condition.FindPropertyRelative("value").floatValue;
                if (comparison == 0) converted.min = value;
                else { converted.max = value; converted.maxExclusive = comparison == 1; }
                conditions.Add(converted);
            }
            tags.Add(new CustomerRequestData.Tag { id = id, name = data.FindProperty("displayName").stringValue,
                color = data.FindProperty("tagColor").colorValue, sellMultiplier = data.FindProperty("sellMultiplier").floatValue,
                conditions = conditions.ToArray() });
        }
        foreach (var request in sourceRequests)
        {
            string id = Id(request);
            if (requests.Any(r => r.id == id)) continue;
            if (!string.IsNullOrEmpty(request.TableId))
                throw new InvalidOperationException("이관된 요청 정의가 JSON에서 삭제되었습니다. 표를 복원하세요: " + id);
            var required = new List<ModuleKind>();
            foreach (var module in request.requiredModules ?? Array.Empty<ModuleData>())
                if (module != null && module.Prefab != null && ModuleCounter.TryGetKind(module.Prefab.GetComponent<PlacedBlock>(), out var kind)) required.Add(kind);
            requests.Add(new CustomerRequestData.Request { id = id, name = request.displayName, phrase = request.request,
                tagId = request.weaponType != null ? Id(request.weaponType) : null,
                unlockDay = request.unlockDay, saleBonus = true,
                requiredModules = required.Distinct().ToArray(),
                excludes = (request.excludes ?? Array.Empty<GunArchetype>()).Where(r => r != null).Select(Id).ToArray(),
                conditions = ConvertConditions(request.conditions) });
        }
        foreach (var profile in profiles)
        {
            var ids = new List<string>();
            var legacy = profile.archetypes ?? Array.Empty<GunArchetype>();
            var inline = profile.conditions ?? Array.Empty<GunCondition>();
            if (legacy.Length == 0 && inline.Length == 0) ids.AddRange(profile.requestIds ?? Array.Empty<string>());
            else
            {
                foreach (var request in legacy) if (request != null) ids.Add(Id(request));
                foreach (var condition in ConvertConditions(inline))
                {
                    string id = Guid.NewGuid().ToString("N");
                    requests.Add(new CustomerRequestData.Request { id = id, name = condition.label,
                        saleBonus = false, unlockDay = 0, conditions = new[] { condition } });
                    ids.Add(id);
                }
            }
            selected[profile] = ids.ToArray();
        }
        foreach (var pool in pools)
        {
            var legacy = pool.requests ?? Array.Empty<GunArchetype>();
            poolSelected[pool] = legacy.Length > 0 ? legacy.Where(r => r != null).Select(Id).ToArray()
                : pool.requestIds ?? Array.Empty<string>();
            for (int i = 0; i < legacy.Length; i++)
                if (legacy[i] != null && !legacySlots.Any(s => s.poolId == Id(pool) && s.index == i))
                    legacySlots.Add(new CustomerRequestData.LegacySlot { poolId = Id(pool), index = i, requestId = Id(legacy[i]) });
        }
        table.requests = requests.OrderBy(r => r.id, StringComparer.Ordinal).ToArray();
        table.tags = tags.OrderBy(t => t.id, StringComparer.Ordinal).ToArray();
        table.legacySlots = legacySlots.ToArray();
        CustomerRequestInfo.Validate(table, TableManager.Instance.ShootingTestTable);
        foreach (var entry in selected)
            if (!table.ContainsOrder(entry.Value)) throw new InvalidOperationException("프로필의 요청 ID를 표에서 찾을 수 없습니다: " + entry.Key.name);
        foreach (var entry in poolSelected)
            if (!table.ContainsOrder(entry.Value)) throw new InvalidOperationException("조합 후보의 요청 ID를 표에서 찾을 수 없습니다: " + entry.Key.name);
        string json = JsonUtility.ToJson(table, true);
        File.WriteAllText(JsonPath, json);
        // Register the JSON before any compatibility asset reads through TableManager.
        AssetDatabase.ImportAsset(JsonPath, ImportAssetOptions.ForceSynchronousImport);
        TableManager.ReloadAfterTableEdit();

        var changed = new List<string> { JsonPath };
        foreach (var tag in sourceTags)
        {
            var data = new SerializedObject(tag);
            data.FindProperty("tableId").stringValue = Id(tag);
            data.FindProperty("displayName").stringValue = "";
            data.FindProperty("conditions").arraySize = 0;
            data.FindProperty("tagColor").colorValue = Color.clear;
            data.FindProperty("sellMultiplier").floatValue = 0f;
            Save(data, changed);
        }
        foreach (var request in sourceRequests)
        {
            var data = new SerializedObject(request);
            data.FindProperty("tableId").stringValue = Id(request);
            data.FindProperty("displayName").stringValue = ""; data.FindProperty("request").stringValue = "";
            data.FindProperty("conditions").arraySize = 0;
            data.FindProperty("weaponType").objectReferenceValue = null;
            data.FindProperty("requiredModules").arraySize = 0; data.FindProperty("excludes").arraySize = 0;
            data.FindProperty("unlockDay").intValue = 0;
            Save(data, changed);
        }
        foreach (var profile in profiles)
        {
            var data = new SerializedObject(profile);
            data.FindProperty("profileId").stringValue = Id(profile);
            SetIds(data.FindProperty("requestIds"), selected[profile]);
            data.FindProperty("archetypes").arraySize = 0; data.FindProperty("conditions").arraySize = 0;
            Save(data, changed);
        }
        foreach (var pool in pools)
        {
            var data = new SerializedObject(pool);
            data.FindProperty("poolId").stringValue = Id(pool);
            SetIds(data.FindProperty("requestIds"), poolSelected[pool]);
            data.FindProperty("requests").arraySize = 0;
            Save(data, changed);
        }
        const string uiPath = "Assets/Prefabs/Managers/UIManager.prefab";
        var root = PrefabUtility.LoadPrefabContents(uiPath);
        try
        {
            var ui = root.GetComponentInChildren<UIManager>(true);
            var tagsUI = root.GetComponentInChildren<WorkshopTagsUI>(true);
            var view = ui.GetComponent<CustomerRequirementsUI>() ?? ui.gameObject.AddComponent<CustomerRequirementsUI>();
            var data = new SerializedObject(view);
            data.FindProperty("priceLabel").objectReferenceValue = new SerializedObject(tagsUI).FindProperty("salePriceText").objectReferenceValue;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, uiPath); changed.Add(uiPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        foreach (string path in changed.Distinct()) if (path != JsonPath) AssetDatabase.ImportAsset(path);
        File.WriteAllText(DonePath, string.Join("\n", changed.Distinct()));
        Debug.Log("NPC 요청 ID·태그 정의 테이블 이관 및 판매가격 아래 요구사항 UI 연결 완료. 플레이/테스트 미실행.");
    }
    private static List<T> Assets<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/Data" })
        .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).Where(asset => asset != null).ToList();
    private static string Id(UnityEngine.Object asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
    private static void SetIds(SerializedProperty array, string[] ids)
    {
        array.arraySize = ids.Length;
        for (int i = 0; i < ids.Length; i++) array.GetArrayElementAtIndex(i).stringValue = ids[i];
    }
    private static void Save(SerializedObject data, List<string> paths)
    {
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(data.targetObject);
        paths.Add(AssetDatabase.GetAssetPath(data.targetObject));
    }
    private static CustomerRequestData.Condition[] ConvertConditions(GunCondition[] conditions) =>
        (conditions ?? Array.Empty<GunCondition>()).Where(c => c != null)
            .Select(c => NewCondition(c.measure,
                c.stat, c.min, c.max >= 999f ? float.MaxValue : c.max)).ToArray();
    private static GunMeasure StatMeasure(StatType stat) => GunMeasure.WeaponStat;
    private static CustomerRequestData.Condition NewCondition(GunMeasure measure, StatType stat, float min, float max)
    {
        string label;
        switch (measure)
        {
            case GunMeasure.PathModules:
                label = (stat == StatType.Penetration ? "관통" : stat == StatType.FireRate ? "연사" :
                    stat == StatType.Explosion ? "폭발" : stat == StatType.Speed ? "가속" : StatName(stat)) + " 모듈 개수";
                break;
            case GunMeasure.SplitTwoBlocks: label = "2방향 분할 모듈 개수"; break;
            case GunMeasure.SplitFourBlocks: label = "4방향 분할 모듈 개수"; break;
            case GunMeasure.TotalModules: label = "스탯 모듈 개수"; break;
            case GunMeasure.Muzzles: label = "발사 총구 수"; break;
            default: label = stat == StatType.Scope ? "스코프 개수" : StatName(stat); break;
        }
        return new CustomerRequestData.Condition { id = Guid.NewGuid().ToString("N"), label = label,
            measure = measure, stat = stat, min = min, max = max };
    }
    private static string StatName(StatType stat)
    {
        switch (stat)
        {
            case StatType.Penetration: return "관통력";
            case StatType.FireRate: return "연사력";
            case StatType.Explosion: return "폭발력";
            case StatType.Ricochet: return "반사";
            case StatType.Charge: return "차지";
            case StatType.Gravity: return "중력";
            case StatType.Heat: return "발열";
            case StatType.Spin: return "회전";
            case StatType.Water: return "물";
            case StatType.Homing: return "유도";
            case StatType.Shotgun: return "산탄";
            case StatType.Weight: return "무게";
            case StatType.Scope: return "스코프";
            case StatType.Speed: return "탄속 배율";
            case StatType.Color: return "컬러";
            case StatType.Spread: return "탄퍼짐";
            default: return stat.ToString();
        }
    }
}
