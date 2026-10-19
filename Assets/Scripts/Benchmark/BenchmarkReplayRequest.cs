using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    // 재생 요청. 명령줄 인수(-benchmark-replay)나 pending_request.json에서 만들어지고,
    // 런 사이 씬을 다시 불러오는 동안에는 static Active가 값을 유지한다.
    public static class BenchmarkReplayRequest
    {
        private const string PendingFileName = "pending_request.json";

        [Serializable]
        public sealed class Data
        {
            public string recordingPath;
            // 비어 있으면 현재 씬에서 재생한다.
            public string sceneName;
            public int runs = 1;
            public int runIndex;
            public bool quitWhenDone;
            // 측정 구간을 프로파일러에 기록해 끝날 때 ProfilerCaptures에 .data로 저장한다(에디터 전용).
            // BenchmarkMenu의 토글이 이 값을 채워 넘기고, F10·명령줄 재생은 기본 끔이다.
            public bool captureProfiler;
        }

        // static 필드는 LoadScene을 거쳐도 살아 있으므로 다음 런의 BenchmarkSession이 다시 읽는다.
        public static Data Active { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActive() => Active = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Data data = FromCommandLine();
            if (data == null && TryConsumePending(out Data pending)) data = pending;
            if (data == null) return;
            // 씬에 상자를 깔기 전에 시드를 맞춘다.
            if (string.IsNullOrEmpty(data.recordingPath) || !BenchmarkFiles.TryLoad(data.recordingPath, out BenchmarkRecording recording))
            {
                Debug.LogError($"[Benchmark] 재생할 녹화를 읽을 수 없습니다: {data.recordingPath}");
                return;
            }
            data.runs = Mathf.Max(1, data.runs);
            data.runIndex = 0;
            UnityEngine.Random.InitState(recording.seed);
            Active = data;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LoadRequestedScene()
        {
            if (Active == null || string.IsNullOrEmpty(Active.sceneName)) return;
            if (SceneManager.GetActiveScene().name == Active.sceneName) return;
            if (BenchmarkFiles.TryLoad(Active.recordingPath, out BenchmarkRecording recording))
                UnityEngine.Random.InitState(recording.seed);
            SceneManager.LoadScene(Active.sceneName);
        }

        public static void WritePending(string recordingPath, string sceneName, int runs, bool quitWhenDone,
            bool captureProfiler = false)
        {
            Directory.CreateDirectory(BenchmarkFiles.Root);
            var data = new Data
            {
                recordingPath = recordingPath,
                sceneName = sceneName ?? string.Empty,
                runs = Mathf.Max(1, runs),
                runIndex = 0,
                quitWhenDone = quitWhenDone,
                captureProfiler = captureProfiler,
            };
            File.WriteAllText(Path.Combine(BenchmarkFiles.Root, PendingFileName), JsonUtility.ToJson(data));
        }

        // 읽는 즉시 파일을 지워 재요청이 남지 않게 한다.
        public static bool TryConsumePending(out Data data)
        {
            data = null;
            string path = Path.Combine(BenchmarkFiles.Root, PendingFileName);
            if (!File.Exists(path)) return false;
            string json = File.ReadAllText(path);
            File.Delete(path);
            data = JsonUtility.FromJson<Data>(json);
            return data != null;
        }

        // -benchmark-replay <경로|latest> [-benchmark-scene <씬 이름>] [-benchmark-runs <n>] [-benchmark-quit]
        private static Data FromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string path = null;
            string sceneName = null;
            int runs = 1;
            bool quit = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (i + 1 < args.Length && Same(args[i], "-benchmark-replay")) path = args[++i];
                else if (i + 1 < args.Length && Same(args[i], "-benchmark-scene")) sceneName = args[++i];
                else if (i + 1 < args.Length && Same(args[i], "-benchmark-runs") && int.TryParse(args[++i], out int parsed)) runs = parsed;
                else if (Same(args[i], "-benchmark-quit")) quit = true;
            }
            if (string.IsNullOrEmpty(path)) return null;
            if (Same(path, "latest")) path = BenchmarkFiles.LatestRecording();
            return new Data
            {
                recordingPath = path,
                sceneName = sceneName ?? string.Empty,
                runs = Mathf.Max(1, runs),
                runIndex = 0,
                quitWhenDone = quit,
            };
        }

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);
    }
}
