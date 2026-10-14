using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WeaponWorkshop.Editor
{
    public static class RotationCircleSetup
    {
        public static void SetUp()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) throw new System.InvalidOperationException("Canvas missing.");
            Transform existing = canvas.transform.Find("RotationCircle");
            GameObject obj = existing != null ? existing.gameObject
                : new GameObject("RotationCircle", typeof(RectTransform), typeof(CanvasRenderer));
            if (existing == null) obj.transform.SetParent(canvas.transform, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            RotationCircleGraphic graphic = obj.GetComponent<RotationCircleGraphic>();
            if (graphic == null) graphic = obj.AddComponent<RotationCircleGraphic>();
            graphic.color = new Color(1f, 1f, 1f, 0.4f);
            graphic.raycastTarget = false;
            var session = Object.FindFirstObjectByType<WorkshopSession>();
            if (session == null) throw new System.InvalidOperationException("WorkshopSession missing.");
            var settings = new SerializedObject(session);
            settings.FindProperty("rotationCircle").objectReferenceValue = graphic;
            settings.ApplyModifiedProperties();
            obj.SetActive(false);
            EditorSceneManager.MarkSceneDirty(obj.scene);
            EditorSceneManager.SaveScene(obj.scene);
        }
    }
}
