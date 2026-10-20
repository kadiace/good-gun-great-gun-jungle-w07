using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop.EditorTools
{
    public static class RaidBlockIdentityEditor
    {
        private const string SceneFolder = "Assets/Scenes/NightRaid";
        public static int LastErrorCount { get; private set; }

        [MenuItem("Tools/Weapon Workshop/Block IDs/Assign Missing IDs")]
        private static void AssignMissing() => Debug.Log(Run(true));

        [MenuItem("Tools/Weapon Workshop/Block IDs/Validate All Stages")]
        private static void ValidateAll() => Debug.Log(Run(false));

        public static string Run(bool assignMissing)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before checking stage IDs.");
            var paths = AssetDatabase.FindAssets("t:Scene", new[] { SceneFolder });
            Array.Sort(paths, (a, b) => string.CompareOrdinal(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));
            int targets = 0, assigned = 0, errors = 0;
            foreach (string guid in paths)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    if (assignMissing && scene.isDirty)
                        throw new InvalidOperationException("Save or revert the open scene before assigning IDs: " + path);
                    var sceneTargets = new List<DestructibleTarget>();
                    foreach (GameObject root in scene.GetRootGameObjects())
                        sceneTargets.AddRange(root.GetComponentsInChildren<DestructibleTarget>(true));
                    var used = new Dictionary<string, DestructibleTarget>(StringComparer.Ordinal);
                    int next = 1;
                    foreach (var target in sceneTargets)
                    {
                        string id = target.TargetId;
                        if (string.IsNullOrEmpty(id)) continue;
                        if (!used.TryAdd(id, target))
                        {
                            Debug.LogError(path + ": duplicate TargetId " + id, target);
                            errors++;
                        }
                        if (id.StartsWith("Block_", StringComparison.Ordinal) && int.TryParse(id.Substring(6), out int number))
                            next = Math.Max(next, number + 1);
                    }
                    foreach (var target in sceneTargets)
                    {
                        targets++;
                        if (!string.IsNullOrEmpty(target.TargetId)) continue;
                        if (!assignMissing)
                        {
                            Debug.LogError(path + ": missing TargetId", target);
                            errors++;
                            continue;
                        }
                        var existing = target.GetComponent<WorldSaveIdentity>();
                        string id = existing != null && !string.IsNullOrEmpty(existing.Id) ? existing.Id : null;
                        if (string.IsNullOrEmpty(id))
                        {
                            do { id = "Block_" + next++.ToString("D4"); } while (used.ContainsKey(id));
                        }
                        if (used.ContainsKey(id))
                        {
                            Debug.LogError(path + ": existing WorldSaveIdentity duplicates TargetId " + id, target);
                            errors++;
                            continue;
                        }
                        var serialized = new SerializedObject(target);
                        serialized.FindProperty("targetId").stringValue = id;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        if (PrefabUtility.IsPartOfPrefabInstance(target))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                        EditorSceneManager.MarkSceneDirty(scene);
                        used.Add(id, target);
                        assigned++;
                    }
                    if (assignMissing && scene.isDirty) EditorSceneManager.SaveScene(scene);
                }
                finally
                {
                    if (opened) EditorSceneManager.CloseScene(scene, true);
                }
            }
            string result = $"NightRaid block IDs: {paths.Length} scenes, {targets} targets, {assigned} assigned, {errors} errors.";
            LastErrorCount = errors;
            if (errors != 0) Debug.LogError(result);
            return result;
        }
    }

    public sealed class RaidBlockIdentityBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            RaidBlockIdentityEditor.Run(false);
            if (RaidBlockIdentityEditor.LastErrorCount != 0)
                throw new BuildFailedException("NightRaid scene block IDs are missing or duplicated.");
        }
    }
}
