using System.Linq;
using UnityEditor;
using UnityEngine;
using WeaponWorkshop;

// One-time authoring command. No runtime UI creation or duplicate suppression.
public static class SharedFeedbackMigration
{
    [MenuItem("Tools/UI/Apply Shared Feedback")]
    public static void Apply()
    {
        Edit("Assets/Prefabs/UI/StartUI_Manager.prefab", root =>
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Managers/UIManager.prefab");
            var original = new SerializedObject(source.GetComponent<FeedbackUI>());
            var feedback = root.GetComponent<FeedbackUI>();
            if (feedback == null) feedback = root.AddComponent<FeedbackUI>();
            var choice = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "YesNoUI");
            if (choice == null)
            {
                choice = Object.Instantiate((GameObject)original.FindProperty("choicePanel").objectReferenceValue, root.transform).transform;
                choice.name = "YesNoUI";
            }
            var notification = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "NotificationText");
            if (notification == null)
            {
                notification = Object.Instantiate(((CanvasGroup)original.FindProperty("notificationGroup").objectReferenceValue).gameObject, root.transform).transform;
                notification.name = "NotificationText";
            }
            var so = new SerializedObject(feedback);
            so.FindProperty("choicePanel").objectReferenceValue = choice.gameObject;
            so.FindProperty("choiceMessage").objectReferenceValue = choice.Find("Card/Message").GetComponent<TMPro.TextMeshProUGUI>();
            so.FindProperty("yesButton").objectReferenceValue = choice.Find("Card/Yes").GetComponent<UnityEngine.UI.Button>();
            so.FindProperty("noButton").objectReferenceValue = choice.Find("Card/No").GetComponent<UnityEngine.UI.Button>();
            so.FindProperty("notificationGroup").objectReferenceValue = notification.GetComponent<CanvasGroup>();
            so.FindProperty("notificationText").objectReferenceValue = notification.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            so.FindProperty("inputActions").objectReferenceValue = original.FindProperty("inputActions").objectReferenceValue;
            so.ApplyModifiedPropertiesWithoutUndo();
        });
        AssetDatabase.SaveAssets();
    }
    private static void Edit(string path, System.Action<GameObject> change)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
        var root = PrefabUtility.LoadPrefabContents(path);
        try { change(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void RemoveReferences(Component component, params string[] names)
    {
        var so = new SerializedObject(component);
        foreach (var name in names)
        {
            var property = so.FindProperty(name);
            if (property == null) continue;
            var target = property.objectReferenceValue;
            property.objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            var go = target as GameObject;
            if (go == null && target is Component c) go = c.gameObject;
            if (go != null && go != component.gameObject && go.transform.IsChildOf(component.transform.root)) Object.DestroyImmediate(go);
        }
    }
    private static void RemoveNamed(GameObject root, params string[] names)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true).Where(t => names.Contains(t.name)).ToArray())
            if (transform != null && transform != root.transform) Object.DestroyImmediate(transform.gameObject);
    }
}
