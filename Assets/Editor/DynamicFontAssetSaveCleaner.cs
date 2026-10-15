using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

// 동적 TMP 폰트 에셋은 플레이·편집 중 쓰인 글자로 아틀라스가 채워지고, 저장 시 그 데이터가 파일에 기록되어 변경점이 생긴다.
// TMP가 에디터 종료·빌드 때 하는 정리(Clear Dynamic Data On Build 켜진 에셋)를 저장 직전에도 적용해 파일을 빈 상태로 유지한다.
internal sealed class DynamicFontAssetSaveCleaner : AssetModificationProcessor
{
    private static readonly MethodInfo ClearTables = typeof(TMP_FontAsset).GetMethod(
        "ClearCharacterAndGlyphTablesInternal", BindingFlags.Instance | BindingFlags.NonPublic);

    private static string[] OnWillSaveAssets(string[] paths)
    {
        if (ClearTables == null) return paths;
        foreach (string path in paths)
        {
            if (!path.EndsWith(".asset")) continue;
            var fontAsset = AssetDatabase.LoadMainAssetAtPath(path) as TMP_FontAsset;
            if (fontAsset == null || !ClearsDynamicData(fontAsset)) continue;
            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic && fontAsset.atlasPopulationMode != AtlasPopulationMode.DynamicOS) continue;
            if (fontAsset.atlasTexture == null || fontAsset.atlasTexture.width <= 1) continue;

            ClearTables.Invoke(fontAsset, null);
            // 화면의 텍스트가 비워진 아틀라스를 다시 채우도록 갱신을 알린다(다음 저장 때 다시 비워짐).
            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, fontAsset);
        }
        return paths;
    }

    // clearDynamicDataOnBuild 속성은 internal이라 직렬화 필드로 읽는다.
    private static bool ClearsDynamicData(TMP_FontAsset fontAsset)
    {
        SerializedProperty property = new SerializedObject(fontAsset).FindProperty("m_ClearDynamicDataOnBuild");
        return property != null && property.boolValue;
    }
}
