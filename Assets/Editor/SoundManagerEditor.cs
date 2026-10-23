using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace WeaponWorkshop.Editor
{
    // 필수 음원과 습격별 두 조건을 한 화면에서 연결하고 누락을 즉시 표시한다.
    [CustomEditor(typeof(SoundManager))]
    public sealed class SoundManagerEditor : UnityEditor.Editor
    {
        /// <summary>
        /// 기본 SoundManager 설정과 습격별 필수 BGM(입장·첫 벽 파괴) 슬롯을 그리고 유효성을 검사한다.
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "raidMusicSets");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("습격별 BGM 조건 (모두 필수)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("각 습격의 입장 / 첫 벽 파괴 음원을 각각 넣어 주세요. 누락 시 플레이와 빌드를 시작할 수 없습니다.", MessageType.Info);
            var sets = serializedObject.FindProperty("raidMusicSets");
            for (int i = 0; i < sets.arraySize; i++)
            {
                var set = sets.GetArrayElementAtIndex(i);
                var stage = set.FindPropertyRelative("stage");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(stage, new GUIContent("습격"));
                EditorGUILayout.PropertyField(set.FindPropertyRelative("travel"));
                EditorGUILayout.PropertyField(set.FindPropertyRelative("firstWall"));
                EditorGUILayout.EndVertical();
            }
            serializedObject.ApplyModifiedProperties();
            if (!((SoundManager)target).ValidateBgmConfiguration(out string error))
                EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }

    // 사용자가 음원을 빠뜨린 채 실행하거나 빌드하는 것을 에디터 경계에서 막는다.
    [InitializeOnLoad]
    internal static class SoundManagerPlayGuard
    {
        static SoundManagerPlayGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            if (ValidatePrefab(out string error))
            {
                foreach (var manager in Object.FindObjectsByType<SoundManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!manager.ValidateBgmConfiguration(out error)) break;
            }
            if (error == null) return;
            EditorApplication.isPlaying = false;
            Debug.LogError(error);
        }

        internal static bool ValidatePrefab(out string error)
        {
            var prefab = Resources.Load<GameObject>(nameof(SoundManager));
            var manager = prefab != null ? prefab.GetComponent<SoundManager>() : null;
            if (manager != null) return manager.ValidateBgmConfiguration(out error);
            error = "필수 SoundManager 프리팹을 Resources에서 찾지 못했습니다.";
            return false;
        }
    }

    public sealed class SoundManagerBuildGuard : IPreprocessBuildWithReport, IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!SoundManagerPlayGuard.ValidatePrefab(out string error)) throw new BuildFailedException(error);
        }

        // 씬에 별도 매니저가 추가된 경우에도 필수 연결 검사를 우회하지 못하게 한다.
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var manager in root.GetComponentsInChildren<SoundManager>(true))
                    if (!manager.ValidateBgmConfiguration(out string error)) throw new BuildFailedException(error);
        }
    }
}
