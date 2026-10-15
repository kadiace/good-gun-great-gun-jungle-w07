using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using WeaponWorkshop;

// 빌드 때만 로그 서버 주소(LOGKIT_SERVER_URL 환경변수 또는 .env)를 Resources에 넣어 플레이어에 포함하고,
// 빌드가 끝나면 지운다. 생성 파일은 .gitignore 대상이라 저장소에 올라가지 않는다.
internal sealed class LogKitServerUrlBuildStep : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    private const string AssetPath = "Assets/Resources/" + LogKitBootstrap.ResourceName + ".txt";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        string url = LogKitBootstrap.ReadServerUrlForEditor();
        if (string.IsNullOrWhiteSpace(url))
        {
            Delete();
            Debug.LogWarning($"{LogKitBootstrap.EnvironmentKey}가 환경변수·.env에 없어 빌드는 LogKit 기본 서버 주소를 사용합니다.");
            return;
        }
        File.WriteAllText(AssetPath, url);
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
    }

    public void OnPostprocessBuild(BuildReport report) => Delete();

    private static void Delete()
    {
        if (File.Exists(AssetPath)) AssetDatabase.DeleteAsset(AssetPath);
    }
}
