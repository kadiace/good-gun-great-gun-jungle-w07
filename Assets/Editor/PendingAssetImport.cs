using System.IO;
using UnityEditor;
using UnityEngine;

// 에이전트가 기록한 수정 파일만 한 번 임포트합니다. 플레이/테스트는 실행하지 않습니다.
[InitializeOnLoad]
internal static class PendingAssetImport
{
    private const string QueuePath = "Library/AgentPendingAssetImports.txt";
    private static double nextCheck;
    static PendingAssetImport() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 2d;
        ImportPending();
    }

    [MenuItem("Tools/Weapon Workshop/Import Pending Changed Assets")]
    private static void ImportPending()
    {
        if (!File.Exists(QueuePath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string[] paths = File.ReadAllLines(QueuePath);
        File.Delete(QueuePath);
        foreach (string path in paths)
            if (path.StartsWith("Assets/") && File.Exists(path))
                AssetDatabase.ImportAsset(path);
        File.WriteAllText("Library/AgentAssetImportCompleted.txt", string.Join("\n", paths));
        Debug.Log("수정한 재화 관련 Assets 파일의 개별 임포트 완료.");
        // 전체 리프레시가 필요한 경우(패키지 재리졸브 등) 큐에 REFRESH 한 줄을 남긴다.
        if (System.Array.IndexOf(paths, "REFRESH") >= 0)
        {
            Debug.Log("큐 요청으로 에셋 전체 리프레시 실행.");
            UnityEditor.PackageManager.Client.Resolve();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
    }
}
