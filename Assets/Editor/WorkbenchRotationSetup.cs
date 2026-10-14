using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WeaponWorkshop.Editor
{
    public static class WorkbenchRotationSetup
    {
        public static void SetUp()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
            foreach (Workbench bench in Object.FindObjectsByType<Workbench>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var settings = new SerializedObject(bench);
                var pivot = settings.FindProperty("rotationTarget").objectReferenceValue as Transform;
                if (pivot == null) throw new System.InvalidOperationException("Workbench rotation target missing.");
                foreach (EquipableWeapon weapon in pivot.GetComponentsInChildren<EquipableWeapon>(true))
                {
                    var weaponSettings = new SerializedObject(weapon);
                    weaponSettings.FindProperty("workbench").objectReferenceValue = bench;
                    weaponSettings.ApplyModifiedProperties();
                }
                EditorSceneManager.MarkSceneDirty(bench.gameObject.scene);
            }
            RotationCircleSetup.SetUp();
            EditorSceneManager.SaveOpenScenes();
        }
    }
}
