using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeaponWorkshop;

// PreSet 씬의 프리셋 지급기를 CoreDispenser 프리팹 베리언트로 저장하고 씬 인스턴스를 연결한다.
public static class MakePresetDispensers
{
    public static string Main()
    {
        const string scenePath = "Assets/Scenes/Test/PreSet.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        var log = new StringBuilder();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (CoreDispenser dispenser in root.GetComponentsInChildren<CoreDispenser>(true))
            {
                var prefab = new SerializedObject(dispenser).FindProperty("corePrefab").objectReferenceValue as EquipableWeapon;
                if (prefab == null || !prefab.name.StartsWith("Preset_")) continue;
                GameObject instance = PrefabUtility.GetOutermostPrefabInstanceRoot(dispenser.gameObject) ?? dispenser.gameObject;
                string path = "Assets/Prefabs/Presets/PresetDispenser_" + prefab.name.Substring("Preset_".Length) + ".prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(instance, path, InteractionMode.AutomatedAction, out bool ok);
                log.Append(instance.name).Append(" -> ").Append(path).Append(ok ? " ok; " : " FAILED; ");
            }
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        return log.ToString();
    }
}
