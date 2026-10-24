using System;
using System.Collections.Generic;
using System.Globalization;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop.EditorTools
{
    /// <summary>
    /// UI Wave 12 Scene 단위 저작 도구: 씬 오버라이드 정리.
    /// 빌드 씬 9개의 UIManager.prefab / StartUI_Manager.prefab 인스턴스에서,
    /// 오버라이드 값이 현재 프리팹 소스 값과 동일하여 실효 효과가 없는 PropertyModification을 안전하게 제거한다.
    /// - 값 비교는 float 허용 오차(1e-4) 적용
    /// - 객체 참조 오버라이드는 동일 대상일 때만 제거 (씬 오브젝트 연결 유지)
    /// - m_IsActive 등 활성 상태 오버라이드는 값이 같아도 보수적으로 유지
    /// - 씬 추가 오브젝트/컴포넌트(added objects)는 건드리지 않음
    /// - 멱등성 보장 (2회 실행 시 정리 대상 0건)
    /// </summary>
    public static class SceneOverrideCleanup
    {
        private const string MENU_DRY_RUN = "Tools/UI/Authoring/SceneOverrides/DryRun";
        private const string MENU_CLEAN = "Tools/UI/Authoring/SceneOverrides/Clean";
        private const string STEP_DRY_RUN = "SceneOverrides/DryRun";
        private const string STEP_CLEAN = "SceneOverrides/Clean";

        private const string UI_MANAGER_PATH = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string START_UI_PATH = "Assets/Prefabs/UI/StartUI_Manager.prefab";

        private const float FLOAT_TOLERANCE = 1e-4f;

        private static readonly Dictionary<UnityEngine.Object, SerializedObject> S_TARGET_SO_CACHE =
            new Dictionary<UnityEngine.Object, SerializedObject>();

        [MenuItem(MENU_DRY_RUN)]
        public static void DryRun()
        {
            Run(true);
        }

        [MenuItem(MENU_CLEAN)]
        public static void Clean()
        {
            Run(false);
        }

        private static void Run(bool _dryRun)
        {
            string stepName = _dryRun ? STEP_DRY_RUN : STEP_CLEAN;

            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UIAuthoring] FAIL " + stepName + ": 플레이 모드에서는 실행할 수 없습니다.");
                return;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogError("[UIAuthoring] FAIL " + stepName + ": 프리팹 스테이지가 열려 있습니다. 프리팹 스테이지를 닫고 다시 실행하세요.");
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene openScene = SceneManager.GetSceneAt(i);
                if (openScene.isDirty)
                {
                    Debug.LogError("[UIAuthoring] FAIL " + stepName + ": 열린 씬에 저장하지 않은 변경이 있습니다: " + openScene.path);
                    return;
                }
            }

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            int totalScenesProcessed = 0;
            int totalRemovableCount = 0;
            int totalKeptCount = 0;
            var sceneSummaries = new List<string>();

            S_TARGET_SO_CACHE.Clear();

            try
            {
                foreach (string scenePath in GetBuildScenePaths())
                {
                    if (!System.IO.File.Exists(scenePath))
                    {
                        Debug.LogWarning("[UIAuthoring] WARN " + stepName + ": 빌드 씬 파일이 존재하지 않아 건너뜁니다: " + scenePath);
                        continue;
                    }

                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        Debug.LogError("[UIAuthoring] FAIL " + stepName + ": 씬 열기 실패: " + scenePath);
                        return;
                    }

                    int sceneRemovable = 0;
                    int sceneKept = 0;
                    bool sceneModified = false;

                    List<GameObject> instances = FindTargetInstances(scene);
                    foreach (GameObject instance in instances)
                    {
                        PropertyModification[] existingMods = PrefabUtility.GetPropertyModifications(instance);
                        if (existingMods == null || existingMods.Length == 0)
                        {
                            continue;
                        }

                        var remainingMods = new List<PropertyModification>(existingMods.Length);
                        int removedForInstance = 0;

                        foreach (PropertyModification mod in existingMods)
                        {
                            if (IsRedundantModification(mod))
                            {
                                removedForInstance++;
                            }
                            else
                            {
                                remainingMods.Add(mod);
                            }
                        }

                        if (removedForInstance > 0)
                        {
                            sceneRemovable += removedForInstance;
                            if (!_dryRun)
                            {
                                PrefabUtility.SetPropertyModifications(instance, remainingMods.ToArray());
                                EditorUtility.SetDirty(instance);
                                sceneModified = true;
                            }
                        }

                        sceneKept += remainingMods.Count;
                    }

                    if (!_dryRun && sceneModified)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        bool saved = EditorSceneManager.SaveScene(scene);
                        if (!saved)
                        {
                            Debug.LogError("[UIAuthoring] FAIL " + stepName + ": 씬 저장 실패: " + scenePath);
                            return;
                        }
                    }

                    totalScenesProcessed++;
                    totalRemovableCount += sceneRemovable;
                    totalKeptCount += sceneKept;

                    string sceneName = System.IO.Path.GetFileName(scenePath);
                    sceneSummaries.Add(sceneName + "(제거 " + sceneRemovable + ", 유지 " + sceneKept + ")");
                }

                string resultAction = _dryRun ? "정리 예정 (저장하지 않음)" : "정리 및 저장 완료";
                Debug.Log("[UIAuthoring] OK " + stepName + ": 총 " + totalScenesProcessed + "개 씬, " +
                          resultAction + " (제거: " + totalRemovableCount + "건, 유지: " + totalKeptCount + "건) -> " +
                          string.Join(", ", sceneSummaries));
            }
            catch (Exception exception)
            {
                Debug.LogError("[UIAuthoring] FAIL " + stepName + ": 예외 발생 " + exception.Message + "\n" + exception.StackTrace);
            }
            finally
            {
                S_TARGET_SO_CACHE.Clear();

                if (originalSetup != null && originalSetup.Length > 0)
                {
                    try
                    {
                        EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[UIAuthoring] WARN " + stepName + ": 씬 구성 복원 실패: " + ex.Message);
                    }
                }
            }
        }

        private static List<GameObject> FindTargetInstances(Scene _scene)
        {
            var instances = new List<GameObject>();
            var visited = new HashSet<GameObject>();

            foreach (GameObject rootObject in _scene.GetRootGameObjects())
            {
                foreach (Transform t in rootObject.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null)
                    {
                        continue;
                    }

                    GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
                    if (instanceRoot == null || !visited.Add(instanceRoot))
                    {
                        continue;
                    }

                    string sourcePath = GetSourcePrefabPath(instanceRoot);
                    if (sourcePath == UI_MANAGER_PATH || sourcePath == START_UI_PATH)
                    {
                        instances.Add(instanceRoot);
                    }
                }
            }

            return instances;
        }

        private static string GetSourcePrefabPath(GameObject _instanceRoot)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(_instanceRoot);
            return source != null ? AssetDatabase.GetAssetPath(source) : null;
        }

        private static IEnumerable<string> GetBuildScenePaths()
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                {
                    paths.Add(scene.path);
                }
            }

            // git 병합으로 바뀐 빌드 씬 목록은 에디터 재시작 전까지 메모리에 반영되지 않으므로 파일 목록도 함께 읽는다.
            const string BUILD_SETTINGS_FILE = "ProjectSettings/EditorBuildSettings.asset";
            if (System.IO.File.Exists(BUILD_SETTINGS_FILE))
            {
                bool enabled = false;
                foreach (string line in System.IO.File.ReadAllLines(BUILD_SETTINGS_FILE))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("- enabled:", StringComparison.Ordinal))
                    {
                        enabled = trimmed.EndsWith("1", StringComparison.Ordinal);
                    }
                    else if (enabled && trimmed.StartsWith("path: ", StringComparison.Ordinal))
                    {
                        string path = trimmed.Substring("path: ".Length).Trim();
                        if (System.IO.File.Exists(path))
                        {
                            paths.Add(path);
                        }
                    }
                }
            }

            return paths;
        }

        private static bool IsRedundantModification(PropertyModification _mod)
        {
            if (_mod == null || _mod.target == null || string.IsNullOrEmpty(_mod.propertyPath))
            {
                return false;
            }

            // 활성 상태 오버라이드는 값이 같아도 보수적으로 유지한다.
            if (_mod.propertyPath == "m_IsActive" || _mod.propertyPath.EndsWith(".m_IsActive", StringComparison.Ordinal))
            {
                return false;
            }

            if (!S_TARGET_SO_CACHE.TryGetValue(_mod.target, out SerializedObject targetSo))
            {
                targetSo = new SerializedObject(_mod.target);
                S_TARGET_SO_CACHE[_mod.target] = targetSo;
            }

            SerializedProperty targetProp = targetSo.FindProperty(_mod.propertyPath);
            if (targetProp == null)
            {
                // 소스 프리팹에서 속성을 찾지 못하면 보수적으로 유지
                return false;
            }

            switch (targetProp.propertyType)
            {
                case SerializedPropertyType.Float:
                {
                    if (float.TryParse(_mod.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float modFloat))
                    {
                        return Mathf.Abs(targetProp.floatValue - modFloat) < FLOAT_TOLERANCE;
                    }

                    return false;
                }
                case SerializedPropertyType.Integer:
                {
                    if (long.TryParse(_mod.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long modLong))
                    {
                        if (targetProp.longValue == modLong || (uint)targetProp.intValue == (uint)modLong)
                        {
                            return true;
                        }
                    }

                    if (uint.TryParse(_mod.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint modUint))
                    {
                        if ((uint)targetProp.intValue == modUint || (uint)targetProp.longValue == modUint)
                        {
                            return true;
                        }
                    }

                    return false;
                }
                case SerializedPropertyType.Boolean:
                {
                    bool modBool = _mod.value == "1" || string.Equals(_mod.value, "true", StringComparison.OrdinalIgnoreCase);
                    return targetProp.boolValue == modBool;
                }
                case SerializedPropertyType.String:
                {
                    return string.Equals(targetProp.stringValue, _mod.value, StringComparison.Ordinal);
                }
                case SerializedPropertyType.Enum:
                {
                    if (int.TryParse(_mod.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int modEnum))
                    {
                        return targetProp.intValue == modEnum;
                    }

                    return false;
                }
                case SerializedPropertyType.ObjectReference:
                {
                    // 객체 참조는 동일한 대상일 때만 제거 (프리팹의 null과 씬의 씬 오브젝트 참조는 당연히 상이하여 보존됨)
                    return targetProp.objectReferenceValue == _mod.objectReference;
                }
                default:
                {
                    return false;
                }
            }
        }
    }
}
