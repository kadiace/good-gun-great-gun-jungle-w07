using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace WeaponWorkshop.Editor
{
    [InitializeOnLoad]
    public static class MilitaryBaseSceneSetup
    {
        static MilitaryBaseSceneSetup()
        {
            EditorApplication.delayCall += ApplyPending;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += ApplyPending;
            };
        }
        [MenuItem("Tools/Weapon Workshop/Import Pending Military Base Assets")]
        static void ApplyPending()
        {
            const string pending = "Library/MilitaryImportPaths.txt";
            if (!File.Exists(pending) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string token = File.GetLastWriteTimeUtc(pending).Ticks.ToString();
            if (SessionState.GetString("MilitaryBaseImportToken", "") == token) return;
            SessionState.SetString("MilitaryBaseImportToken", token);
            try
            {
                foreach (string path in File.ReadAllLines(pending).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct())
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                if (!File.Exists("Library/MilitaryBaseSetupComplete.txt"))
                {
                    ConnectWorkshop("Assets/Scenes/Main.unity");
                    ConnectWorkshop("Assets/Scenes/Test/Time_Scene.unity");
                }
                File.Delete(pending);
                File.WriteAllText("Library/MilitaryBaseSetupComplete.txt", "Dense military base assets imported; map button 1 connection retained.");
            }
            catch
            {
                SessionState.EraseString("MilitaryBaseImportToken");
                throw;
            }
        }
        static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();
        static void Set(Object target, string name, Object value)
        {
            var data = new SerializedObject(target); var field = data.FindProperty(name);
            if (field != null) { field.objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        }
        static void ConnectWorkshop(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var manager = Find<GameManager>(scene); var ui = Find<UIManager>(scene); var day = Find<_DayManager>(scene);
            if (manager == null || ui == null || day == null) { if (opened) EditorSceneManager.CloseScene(scene, true); return; }
            var controller = Find<NightRaidController>(scene) ?? manager.gameObject.AddComponent<NightRaidController>();
            var raidUI = Find<NightRaidUI>(scene);
            if (raidUI == null) raidUI = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/NightRaidUI.prefab"), ui.transform)).GetComponent<NightRaidUI>();
            Set(controller, "dayManager", day); Set(controller, "gameManager", manager); Set(controller, "uiManager", ui); Set(controller, "ui", raidUI); Set(controller, "workshopSave", Find<GameSaveController>(scene));
            Set(controller, "carriedWeaponPrefab", AssetDatabase.LoadAssetAtPath<EquipableWeapon>("Assets/Prefabs/MainCore/MainCore.prefab"));
            // 목적지는 스테이지 테이블(Resources/RaidMapData.json)에서 읽으므로 이곳에서 설정하지 않는다.
            var settlement = Find<DaySettlementUI>(scene); if (settlement != null) Set(settlement, "nightRaid", controller);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }
}

