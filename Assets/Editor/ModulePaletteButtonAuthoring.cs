using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop.Editor
{
    // 명시적 요청 때만 UIManager 프리팹을 편집한다. 플레이·씬 저장은 실행하지 않는다.
    [InitializeOnLoad]
    internal static class ModulePaletteButtonAuthoring
    {
        private const string Request = "Library/ModulePaletteButton.request";
        private const string PrefabPath = "Assets/Prefabs/Managers/UIManager.prefab";

        static ModulePaletteButtonAuthoring() => EditorApplication.update += ApplyPending;

        private static void ApplyPending()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try { Apply(); }
            catch (Exception exception)
            {
                File.WriteAllText("Library/ModulePaletteButton.error.txt", exception.ToString());
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Weapon Workshop/Create Module Palette Toggle Button")]
        private static void Apply()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var palette = root.GetComponentInChildren<ModulePaletteUI>(true);
                if (palette == null) throw new InvalidOperationException("ModulePaletteUI missing.");
                var data = new SerializedObject(palette);
                var buttonProperty = data.FindProperty("paletteToggleButton");
                var button = buttonProperty.objectReferenceValue as Button;
                if (button == null)
                {
                    var rect = new GameObject("PaletteToggleButton", typeof(RectTransform), typeof(Image), typeof(Button))
                        .GetComponent<RectTransform>();
                    rect.SetParent(palette.transform, false);
                    rect.gameObject.layer = palette.gameObject.layer;
                    rect.anchorMin = new Vector2(.082f, .13f);
                    rect.anchorMax = new Vector2(.247f, .17f);
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    button = rect.GetComponent<Button>();
                    button.targetGraphic = rect.GetComponent<Image>();
                    button.targetGraphic.color = new Color32(41, 45, 51, 255);
                    buttonProperty.objectReferenceValue = button;
                }
                var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label == null)
                {
                    var labelRect = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI))
                        .GetComponent<RectTransform>();
                    labelRect.SetParent(button.transform, false);
                    labelRect.gameObject.layer = palette.gameObject.layer;
                    label = labelRect.GetComponent<TextMeshProUGUI>();
                }
                label.rectTransform.anchorMin = new Vector2(.04f, .08f);
                label.rectTransform.anchorMax = new Vector2(.96f, .92f);
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                label.font = data.FindProperty("font").objectReferenceValue as TMP_FontAsset;
                label.text = "모듈 선택창 [TAB]";
                label.fontSize = label.fontSizeMax = 24f;
                label.fontSizeMin = 16f;
                label.enableAutoSizing = true;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.alignment = TextAlignmentOptions.Midline;
                label.color = Color.white;
                label.raycastTarget = false;
                button.gameObject.SetActive(false);
                data.FindProperty("paletteToggleLabel").objectReferenceValue = label;
                data.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new IOException("UIManager prefab save failed.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            File.WriteAllText("Library/ModulePaletteButton.completed.txt", PrefabPath);
        }
    }
}
