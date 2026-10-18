using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

// Damage materials copy the original Lit keywords at runtime. Preserve those local
// variants in builds even though the original materials use URP's shader.
internal sealed class DestructibleDamageShaderVariants : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => Rebuild();

    [MenuItem("Tools/Weapon Workshop/Update Damage Shader Variants")]
    internal static void Rebuild()
    {
        const string path = "Assets/Resources/DestructibleDamageVariants.shadervariants";
        var shader = Shader.Find("WeaponWorkshop/DestructibleDamageLit");
        if (shader == null) throw new BuildFailedException("DestructibleDamageLit shader is missing.");
        var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
        bool created = collection == null;
        if (created) collection = new ShaderVariantCollection();
        collection.Clear();
        var unique = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material == null || material.shader == null || material.shader.name != "Universal Render Pipeline/Lit") continue;
            var keywords = new List<string>();
            foreach (string keyword in material.shaderKeywords)
                if (shader.keywordSpace.FindKeyword(keyword).isValid) keywords.Add(keyword);
            keywords.Sort();
            if (!unique.Add(string.Join(" ", keywords))) continue;
            collection.Add(new ShaderVariantCollection.ShaderVariant(shader, PassType.ScriptableRenderPipeline, keywords.ToArray()));
        }
        if (created) AssetDatabase.CreateAsset(collection, path);
        EditorUtility.SetDirty(collection);
        AssetDatabase.SaveAssetIfDirty(collection);
        AssetDatabase.ImportAsset(path);
    }
}
