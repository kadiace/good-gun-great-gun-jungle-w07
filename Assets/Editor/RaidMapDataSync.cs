using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace WeaponWorkshop.EditorTools
{
    public static class RaidMapDataSync
    {
        [MenuItem("Tools/Weapon Workshop/Update Raid Map Rewards")]
        public static void UpdateData()
        {
            const string path = "Assets/Resources/RaidMapData.json";
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException("RaidMapData.json이 없습니다.");
            var data = JsonUtility.FromJson<RaidMapData>(asset.text);
            string unlockGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/ModuleUnlockCrateSpawner.cs");
            string creditGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/ModuleCreditCrateSpawner.cs");
            string enemyGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/RaidEnemySpawnManager.cs");
            foreach (var region in data.regions)
            {
                string scene = File.ReadAllText("Assets/Scenes/Map/" + region.scene + ".unity");
                var codes = new HashSet<string>();
                int credits = 0;
                foreach (Match match in Regex.Matches(scene, @"(?m)^  m_Script:.*guid: (" + unlockGuid + "|" + creditGuid + @"),.*$"))
                {
                    int end = scene.IndexOf("\n---", match.Index, StringComparison.Ordinal);
                    string block = scene.Substring(match.Index, (end < 0 ? scene.Length : end) - match.Index);
                    var points = Regex.Matches(ArrayField(block, "spawnPoints"), @"fileID: (-?\d+)");
                    if (match.Groups[1].Value == unlockGuid)
                    {
                        var rewards = Regex.Matches(ArrayField(block, "rewardModules"), @"(?m)^  - (.*)$");
                        for (int i = 0; i < Math.Min(points.Count, rewards.Count); i++)
                        {
                            var guid = Regex.Match(rewards[i].Value, @"guid: ([a-f0-9]+)");
                            if (points[i].Groups[1].Value != "0" && guid.Success) codes.Add(guid.Groups[1].Value);
                        }
                    }
                    else
                    {
                        var valid = new HashSet<string>();
                        foreach (Match point in points) if (point.Groups[1].Value != "0") valid.Add(point.Groups[1].Value);
                        var count = Regex.Match(block, @"spawnCount: (\d+)");
                        if (count.Success) credits += Math.Min(int.Parse(count.Groups[1].Value), valid.Count);
                    }
                }
                region.moduleCodes = new List<string>(codes).ToArray();
                region.materialCrates = credits;
                region.helicopters = region.tanks = false;
                foreach (Match match in Regex.Matches(scene, @"(?m)^  m_Script:.*guid: " + enemyGuid + @",.*$"))
                {
                    int end = scene.IndexOf("\n---", match.Index, StringComparison.Ordinal);
                    string block = scene.Substring(match.Index, (end < 0 ? scene.Length : end) - match.Index);
                    region.helicopters |= !Regex.IsMatch(block, @"helicopterEnabled: 0") &&
                        Regex.IsMatch(block, @"helicopterPrefab: \{fileID: [1-9]\d*");
                    region.tanks |= Regex.IsMatch(block, @"tankTransportPrefab: \{fileID: [1-9]\d*");
                }
            }
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            AssetDatabase.ImportAsset(path);
        }
        private static string ArrayField(string block, string field) => Regex.Match(block,
            @"(?m)^  " + field + @":\r?\n((?:  -[^\r\n]*(?:\r?\n|\z))*)").Groups[1].Value;
    }
}
