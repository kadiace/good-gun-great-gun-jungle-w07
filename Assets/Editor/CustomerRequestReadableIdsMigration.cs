#pragma warning disable 618
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using WeaponWorkshop;

// Explicit identifier migration. Existing conditions, rewards, order multiplicity and Unity GUIDs are preserved.
[InitializeOnLoad]
internal static class CustomerRequestReadableIdsMigration
{
    private const string Trigger = "Library/AgentReadableRequestIds.txt";
    private static double nextCheck;
    static CustomerRequestReadableIdsMigration() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 2d;
        if (!File.Exists(Trigger) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Trigger);
        Migrate();
    }
    [MenuItem("Tools/Weapon Workshop/Migrate Readable Request IDs")]
    private static void Migrate()
    {
        const string jsonPath = "Assets/Resources/CustomerRequestData.json";
        var table = JsonUtility.FromJson<CustomerRequestData>(File.ReadAllText(jsonPath));
        var profiles = Assets<CustomerProfile>();
        var bindings = Assets<GunArchetype>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var request in table.requests)
        {
            string next = request.id;
            if (Guid.TryParseExact(request.id, "N", out _))
            {
                var binding = bindings.FirstOrDefault(b => b.TableId == request.id);
                if (!string.IsNullOrEmpty(request.tagId))
                {
                    string tagName = table.FindTag(request.tagId)?.name;
                    next = "tag/" + TagName(tagName);
                    // Keep the original Want_* variants separate: unlock and exclusion rules can differ.
                    if (binding != null && binding.name.StartsWith("Want_", StringComparison.Ordinal)) next += "/legacy";
                }
                else
                {
                    if (request.conditions == null || request.conditions.Length != 1)
                        throw new InvalidOperationException("이 요청에는 직접 의미 있는 ID를 지정하세요: " + request.id);
                    next = ConditionId(request.conditions[0]);
                    if (binding == null)
                    {
                        var owners = profiles.Where(p => Array.IndexOf(p.requestIds ?? Array.Empty<string>(), request.id) >= 0).ToArray();
                        if (owners.Length != 1) throw new InvalidOperationException("조건 요청의 소유 프로필을 확인하세요: " + request.id);
                        next += "/" + Slug(owners[0].name);
                    }
                }
            }
            if (!names.Add(next)) throw new InvalidOperationException("요청 ID가 중복됩니다: " + next);
            if (next != request.id) renames.Add(request.id, next);
        }
        foreach (var request in table.requests)
        {
            if (renames.TryGetValue(request.id, out var next))
            {
                request.legacyIds = (request.legacyIds ?? Array.Empty<string>()).Concat(new[] { request.id }).Distinct().ToArray();
                request.id = next;
            }
            request.excludes = (request.excludes ?? Array.Empty<string>()).Select(id => renames.TryGetValue(id, out var target) ? target : id).ToArray();
        }
        foreach (var slot in table.legacySlots ?? Array.Empty<CustomerRequestData.LegacySlot>())
            if (renames.TryGetValue(slot.requestId, out var next)) slot.requestId = next;
        table.requests = table.requests.OrderBy(r => r.id, StringComparer.Ordinal).ToArray();
        CustomerRequestInfo.Validate(table, TableManager.Instance.ShootingTestTable);
        var changed = new List<string>();
        string json = JsonUtility.ToJson(table, true);
        if (File.ReadAllText(jsonPath) != json)
        { File.WriteAllText(jsonPath, json, new System.Text.UTF8Encoding(false)); changed.Add(jsonPath); }
        foreach (var profile in profiles) RewriteIds(profile, "requestIds", table, changed);
        foreach (var pool in Assets<CustomerRequestPool>()) RewriteIds(pool, "requestIds", table, changed);
        foreach (var binding in bindings)
        {
            var data = new SerializedObject(binding);
            var property = data.FindProperty("tableId");
            var row = table.FindRequest(property.stringValue);
            if (row != null) property.stringValue = row.id;
            Save(data, changed);
        }
        foreach (string path in changed.Distinct()) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TableManager.ReloadAfterTableEdit();
        File.WriteAllLines("Library/AgentReadableRequestIdsCompleted.txt", changed);
        Debug.Log("요청 ID를 읽기 쉬운 이름으로 변경하고 이전 ID 호환·참조를 갱신했습니다: " + renames.Count);
    }
    private static List<T> Assets<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets" })
        .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>).Where(a => a != null).ToList();
    private static void RewriteIds(UnityEngine.Object asset, string field, CustomerRequestData table, List<string> changed)
    {
        var data = new SerializedObject(asset);
        var ids = data.FindProperty(field);
        for (int i = 0; i < ids.arraySize; i++)
        {
            var value = ids.GetArrayElementAtIndex(i);
            value.stringValue = table.FindRequest(value.stringValue)?.id ?? value.stringValue;
        }
        Save(data, changed);
    }
    private static void Save(SerializedObject data, List<string> changed)
    {
        if (!data.ApplyModifiedPropertiesWithoutUndo()) return;
        AssetDatabase.SaveAssetIfDirty(data.targetObject); changed.Add(AssetDatabase.GetAssetPath(data.targetObject));
    }
    private static string Slug(string text) => Regex.Replace(Regex.Replace(text, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
    private static string Number(float value) => value.ToString("0.########", CultureInfo.InvariantCulture);
    private static string ConditionId(CustomerRequestData.Condition condition)
    {
        string prefix;
        switch (condition.measure)
        {
            case GunMeasure.SplitTwoBlocks: prefix = "module/split-two"; break;
            case GunMeasure.SplitFourBlocks: prefix = "module/split-four"; break;
            case GunMeasure.TotalModules: prefix = "module/stat-modules"; break;
            case GunMeasure.PathModules: prefix = "module/" + Slug(condition.stat.ToString()); break;
            case GunMeasure.Muzzles: prefix = "stat/muzzle-count"; break;
            case GunMeasure.WeaponStat: prefix = (condition.stat == StatType.Scope ? "module/" : "stat/") + Slug(condition.stat.ToString()); break;
            default: prefix = "stat/muzzle-" + Slug(condition.stat.ToString()); break;
        }
        if (condition.min == condition.max && !condition.maxExclusive) return prefix + "/exact-" + Number(condition.min);
        string bounds = "/min-" + Number(condition.min);
        if (condition.max < float.MaxValue) bounds += (condition.maxExclusive ? "-below-" : "-max-") + Number(condition.max);
        return prefix + bounds;
    }
    private static string TagName(string name)
    {
        switch (name)
        {
            case "권총": return "handgun";
            case "돌격소총": return "assault-rifle";
            case "저격소총": return "sniper-rifle";
            case "산탄총": return "shotgun";
            case "유탄": return "grenade-launcher";
            case "소형핵": return "mini-nuke";
            case "화염방사": return "flamethrower";
            case "중력건": return "gravity-gun";
            case "도탄": return "ricochet";
            case "회전": return "spin";
            case "물총": return "water-gun";
            case "유도": return "homing";
            case "차지": return "charge";
            default: throw new InvalidOperationException("태그의 개발용 이름을 지정하세요: " + name);
        }
    }
}
