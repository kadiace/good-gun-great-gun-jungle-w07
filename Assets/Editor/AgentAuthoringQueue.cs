using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 에이전트가 요청한 UI 저작 메뉴만 컴파일·임포트가 끝난 뒤 순서대로 한 번씩 실행합니다. 플레이/테스트는 실행하지 않습니다.
[InitializeOnLoad]
internal static class AgentAuthoringQueue
{
    private const string QUEUE_PATH = "Library/AgentPendingAuthoring.txt";
    private const string RESULT_PATH = "Library/AgentAuthoringCompleted.txt";
    private const string ALLOWED_MENU_PREFIX = "Tools/UI/Authoring/";
    private static double s_nextCheck;

    static AgentAuthoringQueue()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < s_nextCheck)
        {
            return;
        }

        s_nextCheck = EditorApplication.timeSinceStartup + 2d;
        if (!File.Exists(QUEUE_PATH) || File.Exists("Library/AgentPendingAssetImports.txt"))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string[] menuPaths = File.ReadAllLines(QUEUE_PATH);
        File.Delete(QUEUE_PATH);
        List<string> results = new List<string>();
        foreach (string menuPath in menuPaths)
        {
            string trimmed = menuPath.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            // 디스크에서 지운 에셋 경로는 전체 Refresh 없이 해당 경로만 데이터베이스에서 정리한다.
            if (trimmed.StartsWith("Assets/", StringComparison.Ordinal) && !File.Exists(trimmed))
            {
                // 경로 단위 정리로는 데이터베이스에서 빠지지 않아, 변경분만 반영하는 기본 Refresh를 한 번 실행한다(강제 재임포트 아님).
                if (AssetDatabase.LoadMainAssetAtPath(trimmed) != null)
                {
                    AssetDatabase.Refresh();
                }

                bool removed = AssetDatabase.LoadMainAssetAtPath(trimmed) == null;
                results.Add((removed ? "REMOVED\t" : "STILL_LOADED\t") + trimmed);
                continue;
            }

            if (!trimmed.StartsWith(ALLOWED_MENU_PREFIX, StringComparison.Ordinal))
            {
                results.Add("REJECTED\t" + trimmed);
                continue;
            }

            try
            {
                bool executed = EditorApplication.ExecuteMenuItem(trimmed);
                results.Add((executed ? "OK\t" : "MISSING\t") + trimmed);
            }
            catch (Exception exception)
            {
                results.Add("FAILED\t" + trimmed + "\t" + exception.Message);
                Debug.LogException(exception);
            }
        }

        File.WriteAllText(RESULT_PATH, string.Join("\n", results));
        Debug.Log("에이전트 UI 저작 메뉴 실행 완료: " + string.Join(", ", results));
    }
}
