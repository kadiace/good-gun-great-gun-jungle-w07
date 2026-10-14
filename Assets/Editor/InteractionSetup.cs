using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop.Editor
{
    public static class InteractionSetup
    {
        public static void IncludeWeaponLayer()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
            Interaction interaction = Camera.main.GetComponent<Interaction>();
            var settings = new SerializedObject(interaction);
            SerializedProperty mask = settings.FindProperty("interactionLayers");
            mask.intValue |= LayerMask.GetMask("Weapon");
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(interaction.gameObject.scene);
            EditorSceneManager.SaveScene(interaction.gameObject.scene);
        }

        public static void SetUp()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
            const string path = "Assets/InputSystem_Actions.inputactions";
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            InputActionMap map = asset.FindActionMap("Workshop");
            if (map == null) { map = new InputActionMap("Workshop"); asset.AddActionMap(map); }
            if (map.FindAction("Interact") == null)
            {
                InputAction enter = map.FindAction("Enter");
                if (enter != null) enter.Rename("Interact");
                else map.AddAction("Interact", InputActionType.Button, "<Keyboard>/f");
            }
            if (map.FindAction("Exit") == null) map.AddAction("Exit", InputActionType.Button, "<Keyboard>/escape");
            if (map.FindAction("Point") == null)
                map.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position").expectedControlType = "Vector2";
            System.IO.File.WriteAllText(path, asset.ToJson());
            AssetDatabase.ImportAsset(path);
            asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            GameObject root = GameObject.Find("Player");
            Transform cameraPos = GameObject.Find("CameraPos").transform;
            GameObject tableGroup = GameObject.Find("TableGroup");
            CharacterController controller = root.GetComponent<CharacterController>();
            if (controller == null)
            {
                CapsuleCollider capsule = root.GetComponentInChildren<CapsuleCollider>();
                controller = Undo.AddComponent<CharacterController>(root);
                controller.height = capsule != null ? capsule.bounds.size.y : 1.6f;
                controller.radius = capsule != null ? Mathf.Min(capsule.bounds.size.x, capsule.bounds.size.z) * 0.5f : 0.4f;
                controller.center = capsule != null ? root.transform.InverseTransformPoint(capsule.bounds.center) : Vector3.up * 0.8f;
                controller.skinWidth = 0.03f;
                controller.stepOffset = 0.25f;
                if (capsule != null) { Undo.RecordObject(capsule, "Use player CharacterController"); capsule.enabled = false; }
            }
            Transform eye = root.transform.Find("ViewPoint");
            if (eye == null)
            {
                var eyeObject = new GameObject("ViewPoint");
                eye = eyeObject.transform;
                eye.SetParent(root.transform, false);
                eye.localPosition = controller.center + Vector3.up * controller.height * 0.4f;
            }
            FirstPersonPlayer player = root.GetComponent<FirstPersonPlayer>();
            if (player == null) player = Undo.AddComponent<FirstPersonPlayer>(root);
            var playerSettings = new SerializedObject(player);
            playerSettings.FindProperty("inputActions").objectReferenceValue = asset;
            playerSettings.FindProperty("viewPoint").objectReferenceValue = eye;
            playerSettings.ApplyModifiedProperties();
            Camera camera = Camera.main;
            Interaction interaction = camera.GetComponent<Interaction>();
            if (interaction == null) interaction = Undo.AddComponent<Interaction>(camera.gameObject);
            var settings = new SerializedObject(interaction);
            settings.FindProperty("inputActions").objectReferenceValue = asset;
            settings.FindProperty("player").objectReferenceValue = player;
            settings.ApplyModifiedProperties();
            WorkshopSession session = root.GetComponent<WorkshopSession>();
            if (session == null) session = Undo.AddComponent<WorkshopSession>(root);
            var sessionSettings = new SerializedObject(session);
            sessionSettings.FindProperty("inputActions").objectReferenceValue = asset;
            sessionSettings.FindProperty("playerCamera").objectReferenceValue = camera;
            sessionSettings.ApplyModifiedProperties();
            Workbench bench = tableGroup.GetComponent<Workbench>();
            if (bench == null) bench = Undo.AddComponent<Workbench>(tableGroup);
            var benchSettings = new SerializedObject(bench);
            SetIfEmpty(benchSettings, "cameraPosition", cameraPos);
            SetIfEmpty(benchSettings, "builder", camera.GetComponent<CubeBuilder>());
            var circle = Object.FindFirstObjectByType<RotationCircleGraphic>();
            SetIfEmpty(benchSettings, "rotationCircle", circle != null ? circle.gameObject : null);
            Transform draw = tableGroup.transform.Find("Draw");
            if (draw == null) draw = tableGroup.transform.Find("Table");
            SetIfEmpty(benchSettings, "drawRoot", draw);
            benchSettings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
        }

        private static void SetIfEmpty(SerializedObject settings, string field, Object value)
        {
            SerializedProperty property = settings.FindProperty(field);
            if (property.objectReferenceValue == null) property.objectReferenceValue = value;
        }
    }
}
