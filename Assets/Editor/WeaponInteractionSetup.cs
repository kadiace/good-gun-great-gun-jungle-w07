using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WeaponWorkshop.Editor
{
    public static class WeaponInteractionSetup
    {
        public static void ConnectAttack()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            foreach (WeaponHolder holder in Object.FindObjectsByType<WeaponHolder>(FindObjectsSortMode.None))
            {
                var settings = new SerializedObject(holder);
                settings.FindProperty("inputActions").objectReferenceValue = asset;
                if (settings.FindProperty("aimCamera").objectReferenceValue == null)
                    settings.FindProperty("aimCamera").objectReferenceValue = Camera.main;
                settings.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(holder.gameObject.scene);
            }
            foreach (EquipableWeapon weapon in Object.FindObjectsByType<EquipableWeapon>(FindObjectsSortMode.None))
            {
                var settings = new SerializedObject(weapon);
                if (settings.FindProperty("powerCore").objectReferenceValue == null)
                    settings.FindProperty("powerCore").objectReferenceValue = weapon.GetComponentInChildren<PowerCore>(true);
                settings.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(weapon.gameObject.scene);
            }
            EditorSceneManager.SaveOpenScenes();
        }

        public static void SetUp()
        {
            InteractionSetup.SetUp();
            var player = Object.FindFirstObjectByType<FirstPersonPlayer>();
            Transform hand = player.transform.Find("HandPoint");
            if (hand == null) hand = player.ViewPoint.Find("HandPoint");
            if (hand == null)
            {
                var handObject = new GameObject("HandPoint");
                Undo.RegisterCreatedObjectUndo(handObject, "Create weapon hand point");
                hand = handObject.transform;
                hand.SetParent(player.ViewPoint, false);
                hand.localPosition = new Vector3(0.25f, -0.35f, 0.6f);
            }
            else if (hand.parent != player.ViewPoint)
                Undo.SetTransformParent(hand, player.ViewPoint, "Follow FPS aim with weapon");

            GameObject weaponRoot = Object.FindFirstObjectByType<PowerCore>().gameObject;
            if (weaponRoot.GetComponent<EquipableWeapon>() == null)
                Undo.AddComponent<EquipableWeapon>(weaponRoot);
            Camera camera = Camera.main;
            HoverOutline outline = camera.GetComponent<HoverOutline>();
            if (outline == null) outline = Undo.AddComponent<HoverOutline>(camera.gameObject);
            var outlineSettings = new SerializedObject(outline);
            outlineSettings.FindProperty("maskMaterial").objectReferenceValue = EnsureMaterial("HoverOutlineMask");
            outlineSettings.FindProperty("outlineMaterial").objectReferenceValue = EnsureMaterial("HoverOutlineFill");
            outlineSettings.ApplyModifiedProperties();
            WeaponHolder holder = player.GetComponent<WeaponHolder>();
            if (holder == null) holder = Undo.AddComponent<WeaponHolder>(player.gameObject);
            var holderSettings = new SerializedObject(holder);
            holderSettings.FindProperty("handPoint").objectReferenceValue = hand;
            if (holderSettings.FindProperty("aimCamera").objectReferenceValue == null)
                holderSettings.FindProperty("aimCamera").objectReferenceValue = camera;
            holderSettings.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            holderSettings.ApplyModifiedProperties();
            var weaponSettings = new SerializedObject(weaponRoot.GetComponent<EquipableWeapon>());
            if (weaponSettings.FindProperty("powerCore").objectReferenceValue == null)
                weaponSettings.FindProperty("powerCore").objectReferenceValue = weaponRoot.GetComponentInChildren<PowerCore>(true);
            weaponSettings.ApplyModifiedProperties();
            var interaction = new SerializedObject(camera.GetComponent<Interaction>());
            interaction.FindProperty("hoverOutline").objectReferenceValue = outline;
            interaction.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            EditorSceneManager.SaveScene(camera.gameObject.scene);
        }

        private static Material EnsureMaterial(string name)
        {
            const string directory = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(directory)) AssetDatabase.CreateFolder("Assets", "Materials");
            string path = directory + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("WeaponWorkshop/" + name);
            if (shader == null) throw new System.InvalidOperationException("Missing outline shader: " + name);
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
