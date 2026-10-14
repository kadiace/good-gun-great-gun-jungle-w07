using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WeaponWorkshop;

public static class RestoreManualsAndPresets
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit mode required.");
        var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        EditPrefab("Assets/Prefabs/Player/Player.prefab", root => AddReader(root, input));
        foreach (string name in new[] { "WorkshopManual", "RangeManual" })
            EditPrefab(ManualPath(name), root =>
            {
                foreach (var text in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    text.fontSize = .16f; // Restore the authored scene override from the original manual.
            });
        RestoreManualScene(input);
        EditPreset("Pistol", assembly =>
        {
            foreach (var block in assembly.GetComponentsInChildren<PlacedBlock>().ToArray())
                UnityEngine.Object.DestroyImmediate(block.gameObject);
            foreach (var cell in new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down,
                Vector3Int.back, new Vector3Int(0,0,-2), new Vector3Int(0,-1,-1),
                new Vector3Int(0,-2,-1), new Vector3Int(0,-3,-1), new Vector3Int(0,-2,0) })
                Attach(assembly, cell, 1);
            for (int z = 1; z <= 3; z++) Attach(assembly, new Vector3Int(0,0,z), 2);
            for (int z = 1; z <= 3; z++) Attach(assembly, new Vector3Int(0,1,z), 1);
            for (int z = 1; z <= 2; z++) Attach(assembly, new Vector3Int(0,-1,z), 1);
        });
        EditPreset("AssaultRifle", assembly => Replace(assembly, new Vector3Int(0,0,3), 8));
        EditPreset("Sniper", assembly =>
        {
            for (int z = 3; z <= 6; z++) Replace(assembly, new Vector3Int(0,0,z), 6);
        });
        EditScene("Assets/Scenes/Test/PreSet.unity", scene =>
        {
            foreach (var player in Find<FirstPersonPlayer>(scene)) AddReader(player.gameObject, input);
            foreach (var dispenser in Find<CoreDispenser>(scene))
            {
                var data = new SerializedObject(dispenser);
                var prefab = data.FindProperty("corePrefab").objectReferenceValue as EquipableWeapon;
                if (prefab == null || !prefab.name.StartsWith("Preset_")) continue;
                string type = prefab.name.Substring("Preset_".Length);
                var tag = AssetDatabase.LoadAssetAtPath<WeaponTypeData>("Assets/Data/WeaponTypes/Type_" + type + ".asset");
                dispenser.GetComponent<InteractionLabel>()?.SetTitle(tag.DisplayName + " 프리셋");
                PrefabUtility.RecordPrefabInstancePropertyModifications(dispenser.GetComponent<InteractionLabel>());
            }
        });
        return "Restored both manuals and player reader; rebuilt the three linked preset prefabs.";
    }

    private static void AddReader(GameObject player, InputActionAsset input)
    {
        var reader = player.GetComponent<PlayerManualReader>() ?? player.AddComponent<PlayerManualReader>();
        var data = new SerializedObject(reader);
        data.FindProperty("inputActions").objectReferenceValue = input;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RestoreManualScene(InputActionAsset input)
    {
        EditScene("Assets/Scenes/Main.unity", scene =>
        {
            var transforms = Find<Transform>(scene);
            var workspace = transforms.Single(t => t.name == "WorkSpace");
            var range = transforms.Single(t => t.name == "ShootingRange");
            foreach (var player in Find<FirstPersonPlayer>(scene))
                if (player.GetComponent<PlayerManualReader>() == null) AddReader(player.gameObject, input);
            var save = Find<GameSaveController>(scene).Single();
            PlaceManual("WorkshopManual", workspace, new Vector3(1.35f,.84f,1.5f), 0f,
                "65735d0a0cbe4b9887569b0fa9efcfc1", save);
            PlaceManual("RangeManual", range, new Vector3(-6.015f,.84f,-1.812f), 90f,
                "765f29df916541848b36f56d812a9526", save);
        });
    }

    private static void PlaceManual(string name, Transform parent, Vector3 position, float yaw, string id, GameSaveController save)
    {
        var manual = parent.GetComponentsInChildren<WorkshopManual>(true).FirstOrDefault(m => m.name == name);
        if (manual == null)
            manual = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ManualPath(name)), parent)).GetComponent<WorkshopManual>();
        manual.transform.localPosition = position;
        manual.transform.localRotation = Quaternion.Euler(0,yaw,0);
        manual.transform.localScale = Vector3.one;
        PrefabUtility.RecordPrefabInstancePropertyModifications(manual.transform);
        var identity = manual.GetComponent<WorldSaveIdentity>() ?? manual.gameObject.AddComponent<WorldSaveIdentity>();
        var data = new SerializedObject(identity);
        data.FindProperty("saveId").stringValue = id;
        data.ApplyModifiedPropertiesWithoutUndo();
        var saveData = new SerializedObject(save);
        var bindings = saveData.FindProperty("mapObjects");
        SerializedProperty binding = null;
        for (int i=0;i<bindings.arraySize;i++)
            if (bindings.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id)
                binding = bindings.GetArrayElementAtIndex(i);
        if (binding == null)
        {
            int index = bindings.arraySize++;
            binding = bindings.GetArrayElementAtIndex(index);
            binding.FindPropertyRelative("legacyId").stringValue = "";
            binding.FindPropertyRelative("alternateIds").arraySize = 0;
        }
        binding.FindPropertyRelative("id").stringValue = id;
        binding.FindPropertyRelative("target").objectReferenceValue = identity;
        saveData.ApplyModifiedPropertiesWithoutUndo();
    }

    // 매뉴얼 프리팹은 작업대/사격장 폴더로 나뉘어 있다.
    private static string ManualPath(string name) => "Assets/Prefabs/" + (name == "RangeManual" ? "Range" : "Workbench") + "/" + name + ".prefab";
    private static void EditPreset(string name, Action<CubeAssembly> edit)
    {
        EditPrefab("Assets/Prefabs/Presets/Preset_" + name + ".prefab", root =>
        {
            var weapon = root.GetComponent<EquipableWeapon>();
            bool locked = weapon.ConstructionLocked;
            weapon.UnlockConstruction();
            edit(root.GetComponent<CubeAssembly>());
            if (locked) weapon.LockConstruction();
        });
    }

    private static void Replace(CubeAssembly assembly, Vector3Int cell, int slot)
    {
        var old = assembly.GetComponentsInChildren<PlacedBlock>().Single(b => b.Cell == cell);
        UnityEngine.Object.DestroyImmediate(old.gameObject);
        Attach(assembly, cell, slot);
    }
    private static void Attach(CubeAssembly assembly, Vector3Int cell, int slot)
    {
        if (!assembly.TryAttach(cell, slot, Vector3Int.forward, Vector3Int.forward))
            throw new InvalidOperationException("Cannot attach slot " + slot + " at " + cell);
    }
    private static T[] Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    private static void EditPrefab(string path, Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.ImportAsset(path);
    }
    private static void EditScene(string path, Action<Scene> edit)
    {
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("Unsaved scene: " + path);
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try { edit(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        AssetDatabase.ImportAsset(path);
    }
}
