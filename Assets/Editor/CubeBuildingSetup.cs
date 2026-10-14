using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop.Editor
{
    public static class CubeBuildingSetup
    {
        public static void ConnectEraseModeUI()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
            CubeBuilder builder = Camera.main.GetComponent<CubeBuilder>();
            GameObject indicator = null;
            foreach (Transform item in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item.name == "EraseMode" && item.GetComponentInParent<Canvas>(true) != null)
                { indicator = item.gameObject; break; }
            if (indicator == null) throw new System.InvalidOperationException("EraseMode UI object missing.");
            var settings = new SerializedObject(builder);
            settings.FindProperty("eraseModeUI").objectReferenceValue = indicator;
            settings.ApplyModifiedProperties();
            Undo.RecordObject(indicator, "Hide erase mode indicator initially");
            indicator.SetActive(false);
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
            EditorSceneManager.SaveScene(builder.gameObject.scene);
        }

        public static void SetUp()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var basic = LoadModule("Module_Blocker");
            var extension = LoadModule("Module_Extension");
            var two = LoadModule("Module_SplitTwo");
            var four = LoadModule("Module_SplitFour");
            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/MainCore/MainCore.prefab");
            try
            {
                foreach (CubeAssembly assembly in root.GetComponentsInChildren<CubeAssembly>(true))
                    Connect(assembly, basic, extension, two, four);
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/MainCore/MainCore.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            foreach (CubeAssembly assembly in Object.FindObjectsByType<CubeAssembly>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Connect(assembly, basic, extension, two, four);
                EditorSceneManager.MarkSceneDirty(assembly.gameObject.scene);
            }
            Camera camera = Camera.main;
            var builder = camera.GetComponent<CubeBuilder>();
            if (builder == null) builder = Undo.AddComponent<CubeBuilder>(camera.gameObject);
            var settings = new SerializedObject(builder);
            settings.FindProperty("inputActions").objectReferenceValue = input;
            settings.FindProperty("selectedSlot").intValue = 1;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        private static GameObject LoadModule(string name)
        {
            var module = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Modules/" + name + ".prefab");
            if (module == null) throw new System.InvalidOperationException(name + " prefab missing.");
            return module;
        }

        private static void Connect(CubeAssembly assembly, GameObject basic, GameObject extension, GameObject two, GameObject four)
        {
            var settings = new SerializedObject(assembly);
            settings.FindProperty("blockerPrefab").objectReferenceValue = basic;
            settings.FindProperty("extensionPrefab").objectReferenceValue = extension;
            settings.FindProperty("splitTwoPrefab").objectReferenceValue = two;
            settings.FindProperty("splitFourPrefab").objectReferenceValue = four;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
