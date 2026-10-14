using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WeaponWorkshop.Editor
{
    public static class PowerCoreSetup
    {
        [MenuItem("Weapon Workshop/Connect Projectile")]
        public static void SetUp()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnergyCube.prefab");
            if (prefab == null) throw new System.InvalidOperationException("Projectile prefab missing.");
            foreach (PowerCore core in Object.FindObjectsByType<PowerCore>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var settings = new SerializedObject(core);
                settings.FindProperty("energyTemplate").objectReferenceValue = prefab;
                settings.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(core.gameObject.scene);
            }
            EditorSceneManager.SaveOpenScenes();
        }
    }
}