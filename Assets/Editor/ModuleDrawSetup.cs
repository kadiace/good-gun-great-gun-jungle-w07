using UnityEditor;
using UnityEngine;

namespace WeaponWorkshop.Editor
{
    public static class ModuleDrawSetup
    {
        public static void SetUp()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
            foreach (string name in new[] { "Module_Blocker", "Module_Extension", "Module_SplitTwo", "Module_SplitFour" })
            {
                string path = "Assets/Prefabs/Modules/" + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform draw = null;
                    foreach (Transform child in root.transform)
                        if (child.name.Contains("Draw")) { draw = child; break; }
                    if (draw == null) throw new System.Exception(name + " Draw missing.");
                    PlacedBlock block = root.GetComponent<PlacedBlock>();
                    ModuleDraw visual = root.GetComponent<ModuleDraw>();
                    if (visual == null) visual = root.AddComponent<ModuleDraw>();
                    var settings = new SerializedObject(visual);
                    settings.FindProperty("drawRoot").objectReferenceValue = draw;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    block.RefreshDraw();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }

    }
}

